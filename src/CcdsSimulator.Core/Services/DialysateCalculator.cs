using CcdsSimulator.Models;

namespace CcdsSimulator.Models
{
    /// <summary>
    /// ข้อมูลหน้า "คำนวณปริมาณการใช้น้ำยา" (ตามไฟล์ dialysate_calculator.xlsx)
    /// ค่าตั้งต้น = ค่าในไฟล์: ฟอก 4 ชม. + Priming 10 + Recirculation 10 + Fill dialyser 3 นาที, DFR 500 mL/min, 1 : 1.225 : รวม 35
    /// </summary>
    public class DialysateCalcInput
    {
        // 1) ข้อมูลนำเข้า
        public decimal TreatmentHours { get; set; } = 4m;
        public decimal PrimingMinutes { get; set; } = 10m;
        public decimal RecirculationMinutes { get; set; } = 10m;
        public decimal FillMinutes { get; set; } = 3m;
        /// <summary>Dialysate Flow Rate (mL/min)</summary>
        public decimal FlowRate { get; set; } = 500m;
        public decimal RatioA { get; set; } = 1m;
        public decimal RatioB { get; set; } = 1.225m;
        public decimal RatioTotal { get; set; } = 35m;

        // 3) แกลลอน / ต้นทุน
        public decimal PackLitersA { get; set; } = 6m;
        public decimal PriceA { get; set; } = 55m;
        public decimal PackLitersB { get; set; } = 6m;
        public decimal PriceB { get; set; } = 45m;

        // 4) หลายรอบ
        public decimal CasesPerDay { get; set; } = 10m;
        public decimal DaysPerMonth { get; set; } = 26m;
        /// <summary>จำนวนรอบรวม / เดือน ที่กรอกเอง (null = รอบ/วัน × วันทำการ/เดือน)</summary>
        public decimal? CasesPerMonth { get; set; }
    }
}

namespace CcdsSimulator.Services
{
    /// <summary>ปริมาณ / ต้นทุน ของรายการหนึ่ง (A, B, RO, Dialysate) ต่อรอบ ต่อวัน ต่อเดือน</summary>
    public record DialysateLine(decimal PerCase, decimal PerDay, decimal PerMonth);

    public record DialysateCalcResult
    {
        // 2) ต่อ 1 รอบ
        public decimal TotalMinutes { get; init; }
        public decimal DialysateLiters { get; init; }
        public decimal RatioRo { get; init; }
        /// <summary>สัดส่วน A + B มากกว่าสัดส่วนรวม (RO ติดลบ) — ข้อมูลผิด ควรแจ้งเตือน</summary>
        public bool RatioInvalid => RatioRo < 0;
        public decimal LitersA { get; init; }
        public decimal LitersB { get; init; }
        public decimal LitersRo { get; init; }
        public decimal SumCheck { get; init; }
        public decimal Difference { get; init; }

        // 3) แกลลอน / ต้นทุนต่อ 1 รอบ
        public decimal GallonsA { get; init; }
        public decimal GallonsB { get; init; }
        public decimal CostA { get; init; }
        public decimal CostB { get; init; }
        public decimal CostPerCase { get; init; }

        // 4) หลายรอบ
        public decimal CasesPerMonth { get; init; }
        public required DialysateLine A { get; init; }
        public required DialysateLine B { get; init; }
        public required DialysateLine Ro { get; init; }
        public required DialysateLine Dialysate { get; init; }
        public required DialysateLine Cost { get; init; }
        /// <summary>แกลลอน A / B ต่อวัน และต่อเดือน (ปัดขึ้นเป็นแกลลอนเต็ม สำหรับสั่งซื้อ) — PerCase ไม่ปัด</summary>
        public required DialysateLine GallonsALine { get; init; }
        public required DialysateLine GallonsBLine { get; init; }

        // หมายเหตุข้อ 4: สัดส่วนเวลาที่เครื่องอาจไม่ดูด concentrate
        public decimal BypassShare { get; init; }          // (Priming + Recirculation) ÷ เวลารวม
        public decimal BypassShareWithFill { get; init; }  // (Priming + Recirculation + Fill) ÷ เวลารวม
    }

    /// <summary>สูตรเดียวกับไฟล์ dialysate_calculator.xlsx (ชีต "คำนวณน้ำยา")</summary>
    public static class DialysateCalculator
    {
        public static DialysateCalcInput Defaults() => new();

        public static DialysateCalcResult Calculate(DialysateCalcInput input)
        {
            var i = Sanitized(input);
            decimal total = i.TreatmentHours * 60 + i.PrimingMinutes + i.RecirculationMinutes + i.FillMinutes;   // B15
            decimal dial = i.FlowRate * total / 1000m;                                                            // B16
            decimal roParts = i.RatioTotal - i.RatioA - i.RatioB;                                                 // B17
            decimal a = Div(dial * i.RatioA, i.RatioTotal);                                                       // B20
            decimal b = Div(dial * i.RatioB, i.RatioTotal);                                                       // B21
            decimal ro = Div(dial * roParts, i.RatioTotal);                                                       // B22
            decimal sum = a + b + ro;                                                                             // B23

            decimal ga = Div(a, i.PackLitersA), gb = Div(b, i.PackLitersB);                                       // D28, D29
            decimal ca = ga * i.PriceA, cb = gb * i.PriceB;                                                       // F28, F29
            decimal cost = ca + cb;                                                                               // F30

            decimal perDay = i.CasesPerDay, perMonth = i.CasesPerMonth ?? i.CasesPerDay * i.DaysPerMonth;        // B34, B36 (แก้เองได้)
            DialysateLine L(decimal x) => new(x, x * perDay, x * perMonth);
            var lineA = L(a); var lineB = L(b);
            DialysateLine G(DialysateLine liters, decimal perCaseGallons, decimal pack) =>                       // C44:D45
                new(perCaseGallons, CeilDiv(liters.PerDay, pack), CeilDiv(liters.PerMonth, pack));

            return new DialysateCalcResult
            {
                TotalMinutes = total,
                DialysateLiters = dial,
                RatioRo = roParts,
                LitersA = a,
                LitersB = b,
                LitersRo = ro,
                SumCheck = sum,
                Difference = sum - dial,
                GallonsA = ga,
                GallonsB = gb,
                CostA = ca,
                CostB = cb,
                CostPerCase = cost,
                CasesPerMonth = perMonth,
                A = lineA,
                B = lineB,
                Ro = L(ro),
                Dialysate = L(dial),
                Cost = L(cost),
                GallonsALine = G(lineA, ga, i.PackLitersA),
                GallonsBLine = G(lineB, gb, i.PackLitersB),
                BypassShare = Div(i.PrimingMinutes + i.RecirculationMinutes, total),
                BypassShareWithFill = Div(i.PrimingMinutes + i.RecirculationMinutes + i.FillMinutes, total),
            };
        }

        /// <summary>ตัดค่าให้อยู่ในช่วงที่ใช้ได้ (ไม่ติดลบ ไม่ใหญ่จนล้น) — คืนสำเนา</summary>
        public static DialysateCalcInput Sanitized(DialysateCalcInput i)
        {
            static decimal C(decimal v, decimal max) => v <= 0 ? 0 : Math.Round(Math.Min(v, max), 6);
            return new DialysateCalcInput
            {
                TreatmentHours = C(i.TreatmentHours, 24),
                PrimingMinutes = C(i.PrimingMinutes, 600),
                RecirculationMinutes = C(i.RecirculationMinutes, 600),
                FillMinutes = C(i.FillMinutes, 600),
                FlowRate = C(i.FlowRate, 2_000),
                RatioA = C(i.RatioA, 1_000),
                RatioB = C(i.RatioB, 1_000),
                RatioTotal = C(i.RatioTotal, 10_000),
                PackLitersA = C(i.PackLitersA, 10_000),
                PriceA = C(i.PriceA, 1_000_000),
                PackLitersB = C(i.PackLitersB, 10_000),
                PriceB = C(i.PriceB, 1_000_000),
                CasesPerDay = C(i.CasesPerDay, 100_000),
                DaysPerMonth = C(i.DaysPerMonth, 31),
                CasesPerMonth = i.CasesPerMonth is decimal m && m > 0 ? C(m, 10_000_000) : null,
            };
        }

        private static decimal Div(decimal a, decimal b) => b == 0 ? 0 : a / b;
        /// <summary>ROUNDUP(x / pack, 0) — ปัดทศนิยม 9 ตำแหน่งก่อน กันปัดขึ้นเกินจากเศษทศนิยม</summary>
        private static decimal CeilDiv(decimal x, decimal pack) => pack == 0 ? 0 : Math.Ceiling(Math.Round(x / pack, 9));
    }
}
