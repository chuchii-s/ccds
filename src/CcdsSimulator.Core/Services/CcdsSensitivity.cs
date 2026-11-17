using CcdsSimulator.Models;

namespace CcdsSimulator.Services;

/// <summary>ผลคำนวณเมื่อจำนวน Treatment เปลี่ยน (ปริมาณน้ำยาเปลี่ยนตามสัดส่วนเดียวกัน)</summary>
public record SensitivityPoint(decimal ChangePercent, decimal Treatments, decimal SavingPerMonth, decimal SavingHorizon);

/// <summary>
/// ส่วนเผื่อความเสี่ยง
/// BreakEvenLeaseTx   = Treatment/เดือน ขั้นต่ำที่ช่วงผ่อน ค่าใช้จ่ายรายเดือนไม่สูงกว่าแบบแกลลอน
/// BreakEvenHorizonTx = Treatment/เดือน ขั้นต่ำที่ตลอดระยะเวลาวิเคราะห์ยังไม่ขาดทุน
/// ถ้าตอนนี้คุ้มอยู่แล้ว: ทุกค่าระหว่าง BreakEven ถึงค่าปัจจุบันคุ้มทั้งหมด (ลดลงได้จริงตามที่บอก)
/// ถ้าตอนนี้ยังไม่คุ้ม: ค่าแรกที่มากกว่าปัจจุบันซึ่งคุ้ม
/// null = หาไม่ได้ (ไม่มี Treatment ตั้งต้น, ไม่คุ้มแม้เพิ่มถึงขีดจำกัด, หรือตัวเลขใหญ่จนคำนวณไม่ได้)
/// </summary>
public record SensitivityResult(decimal BaseTreatments, decimal? BreakEvenLeaseTx, decimal? BreakEvenHorizonTx, IReadOnlyList<SensitivityPoint> Points);

public static class CcdsSensitivity
{
    private const decimal MaxFactor = 10m;
    /// <summary>ขีดจำกัดจำนวนเดียวกับ CcdsCalculator.Sanitize (Treatment / แกลลอน)</summary>
    private const decimal QtyCap = 10_000_000m;
    private const int MaxSteps = 2_000;

    /// <summary>
    /// สำเนาข้อมูลที่ปรับจำนวน Treatment และปริมาณน้ำยาทุกสูตรด้วยตัวคูณเดียวกัน
    /// (ลิตร/Treatment และราคาทั้งหมดคงเดิม — จำลองว่าคนไข้มาก/น้อยลง)
    /// </summary>
    public static CcdsInput Scale(CcdsInput input, decimal factor)
    {
        // Sanitized คืนสำเนาเสมอ → ToGallons แก้สำเนาได้โดยไม่กระทบข้อมูลเดิม
        var c = CcdsCalculator.ToGallons(CcdsCalculator.Sanitized(input));
        c.TreatmentsPerMonth *= factor;
        foreach (var r in c.ARecipes)
        {
            r.Gallons *= factor;
            if (r.Treatments is decimal t) r.Treatments = t * factor;
        }
        c.GallonsB *= factor;
        return c;
    }

    /// <param name="changesPercent">กรณีที่ต้องการดู เช่น -10, -20 (ลดลง 10%, 20%)</param>
    public static SensitivityResult Analyze(CcdsInput input, params decimal[] changesPercent)
    {
        var baseInput = CcdsCalculator.Sanitized(input);
        decimal tx = baseInput.TreatmentsPerMonth;

        var points = new List<SensitivityPoint>();
        foreach (var pct in new[] { 0m }.Concat(changesPercent))
        {
            var f = Math.Max(0, 1 + pct / 100m);
            var r = CcdsCalculator.Calculate(Scale(input, f));
            points.Add(new SensitivityPoint(pct, tx * f, r.SavingPerMonth, r.SavingHorizon));
        }

        decimal? lease = null, horizon = null;
        if (tx > 0)
        {
            try
            {
                var m = new Model(input, tx);
                lease = m.BreakEven(m.LeaseThreshold, r => r.SavingPerMonth >= 0);
                horizon = m.BreakEven(m.HorizonThreshold, r => r.SavingHorizon >= 0);
            }
            catch (OverflowException) { lease = horizon = null; }   // ตัวเลขใหญ่จนคำนวณไม่ได้ → ไม่แสดงจุดคุ้มทุน
        }
        return new SensitivityResult(tx, lease, horizon, points);
    }

    /// <summary>
    /// โครงสร้างของผลประหยัดรายเดือน S(f) เมื่อปรับปริมาณด้วยตัวคูณ f:
    /// • ทุกส่วนแปรตาม f แบบเส้นตรง ยกเว้นค่าไฟถังผสม ซึ่งเพิ่มเป็นขั้น ๆ ตามจำนวนครั้งผสม/วัน (ปัดขึ้น)
    /// • ระหว่างจุดเปลี่ยนจำนวนครั้งผสม S(f) เป็นเส้นตรงที่ความชันเท่ากันทุกช่วง (a)
    /// • ผลประหยัดสะสม = 12 × ปี × S + ค่าคงที่ → ทั้งสองเกณฑ์คือ "S ≥ ค่าหนึ่ง"
    /// จึงหาจุดคุ้มทุนได้ตรง ๆ ทีละช่วง (ไม่ต้องเดา/ไม่พลาดเพราะค่าไฟกระโดด)
    /// </summary>
    private sealed class Model
    {
        private readonly CcdsInput input;
        private readonly decimal tx, fMax, a;
        private readonly List<decimal> steps = new();   // ตัวคูณ f ที่จำนวนครั้งผสม/วันเพิ่มขึ้น (เรียงจากน้อยไปมาก)
        private readonly Dictionary<decimal, CcdsResult> memo = new();

        public decimal LeaseThreshold => 0;
        public decimal HorizonThreshold { get; }

        public Model(CcdsInput input, decimal tx)
        {
            this.input = input;
            this.tx = tx;
            var g = CcdsCalculator.ToGallons(CcdsCalculator.Sanitized(input));

            // ไม่ขยายเกินขีดจำกัดที่ตัวคำนวณตัดค่า (มิฉะนั้นผลจะแบนผิดความจริง)
            decimal biggest = new[] { tx, g.GallonsB }
                .Concat(g.ARecipes.Select(r => r.Gallons))
                .Concat(g.ARecipes.Select(r => r.Treatments ?? 0)).Max();
            fMax = biggest > 0 ? Math.Clamp(QtyCap / biggest, 1, MaxFactor) : MaxFactor;

            var r1 = At(1);
            int h = Math.Max(1, r1.Years.Count);
            HorizonThreshold = -(r1.SavingHorizon - 12m * h * r1.SavingPerMonth) / (12m * h);

            if (g.IncludeUtility)
            {
                int days = Math.Max(g.DaysPerMonth, 1);
                AddSteps(g, EquipmentKind.MixingA, r1.MixedLitersA, g.MixTankLiterA, days);
                AddSteps(g, EquipmentKind.MixingB, r1.MixedLitersB, g.MixTankLiterB, days);
            }
            steps = steps.Distinct().OrderBy(x => x).ToList();

            // ความชันจากสองจุดในช่วงเดียวกับ f = 1
            decimal prev = steps.Where(x => x < 1).DefaultIfEmpty(0).Max();
            decimal d = (1 - prev) / 2;
            a = (S(1) - S(1 - d)) / d;
        }

        private void AddSteps(CcdsInput g, EquipmentKind kind, decimal litersAt1, decimal tank, int days)
        {
            bool used = g.Equipment.Any(e => e.Kind == kind && e.RunsPerDayOverride is null && e.Kw * e.Hours * e.Qty > 0);
            if (!used || litersAt1 <= 0 || tank <= 0) return;
            decimal step = days * tank / litersAt1;               // f ที่ปริมาณผสม/วัน = ขนาดถัง 1 ครั้ง
            for (int k = 1; k <= MaxSteps && k * step < fMax; k++) steps.Add(k * step);
        }

        private CcdsResult At(decimal f)
        {
            if (memo.TryGetValue(f, out var r)) return r;
            r = CcdsCalculator.Calculate(Scale(input, f));
            if (r.Overflow) throw new OverflowException();
            return memo[f] = r;
        }

        private decimal S(decimal f) => At(f).SavingPerMonth;

        public decimal? BreakEven(decimal t, Func<CcdsResult, bool> ok)
        {
            decimal s1 = S(1);
            if (a <= 0) return s1 >= t ? 0 : null;    // Treatment มากขึ้นไม่ได้ช่วยให้คุ้มขึ้น

            if (s1 >= t)
            {
                // ตอนนี้คุ้ม: ไล่ลงทีละช่วง หาจุดต่ำสุดที่ "ตั้งแต่จุดนั้นถึงปัจจุบันคุ้มทั้งหมด"
                decimal right = 1, sRight = s1;
                var below = steps.Where(x => x < 1).OrderByDescending(x => x).ToList();
                foreach (var left in below.Append(0m))
                {
                    decimal sLeft = sRight - a * (right - left);  // ค่าทางขวาของจุดเปลี่ยน (ก่อนค่าไฟลดลง)
                    if (sLeft < t) return ToTx(right - (sRight - t) / a, ok, max: tx);
                    if (left == 0) return 0;
                    right = left; sRight = S(left);
                }
                return 0;
            }
            else
            {
                // ตอนนี้ยังไม่คุ้ม: ไล่ขึ้นทีละช่วง หาค่าแรกที่คุ้ม
                decimal left = 1;
                foreach (var right in steps.Where(x => x > 1).Append(fMax))
                {
                    decimal sRight = S(right);
                    if (sRight >= t) return ToTx(Math.Max(left, right - (sRight - t) / a), ok, max: null);
                    left = right;
                    if (right >= fMax) break;
                }
                return null;
            }
        }

        /// <summary>แปลงตัวคูณเป็นจำนวน Treatment เต็ม แล้วตรวจกับผลคำนวณจริง (กันปัดเศษ)</summary>
        private decimal? ToTx(decimal f, Func<CcdsResult, bool> ok, decimal? max)
        {
            var c = Math.Max(0, Math.Ceiling(Math.Round(f * tx, 6)));
            for (int k = 0; k < 50; k++, c++)
            {
                if (max is decimal m && c > m) return Math.Ceiling(m);
                if (ok(At(c / tx))) return c;
            }
            return c;
        }
    }
}
