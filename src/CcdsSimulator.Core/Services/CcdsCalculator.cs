using CcdsSimulator.Models;

namespace CcdsSimulator.Services;

// ===================== ผลลัพธ์ =====================

/// <summary>แถวตารางปริมาณน้ำยาแบบ GL</summary>
public record GlRecipeRow(string Name, decimal Gallons, decimal Treatments);

/// <summary>แถวตาราง "ใช้ระบบ CCDS" (ผงผสม หรือ ยังใช้ GL)</summary>
public record CcdsRecipeRow(
    string Name,
    bool IsPowder,
    decimal Gallons,        // จำนวนแกลลอน (เฉพาะสูตรที่ยังใช้ GL)
    decimal Treatments,
    decimal PricePerGallon, // เฉพาะสูตรที่ยังใช้ GL
    decimal CostPerTreat,
    decimal CostPerMonth,
    decimal MixedLiters);   // ลิตรน้ำยาที่ผสม (เฉพาะผง)

public record PowderResult(
    string Name, decimal CostPerBox, decimal MixPerBox, decimal MixLiter,
    decimal LiterPerBox, decimal TreatmentsPerBox, decimal CostPerTreat);

public record EquipmentRow(
    string Name, decimal Kw, decimal Hours, decimal Qty, decimal RunsPerDay,
    decimal KwhPerDay, decimal KwhPerMonth);

public record YearRow(int Year, decimal Saving, decimal Pm, decimal LeasingFreed, decimal Net, decimal Cumulative);

public record CcdsResult
{
    public required CcdsInput Input { get; init; }
    public decimal Treatments { get; init; }

    // ----- แบบ GL -----
    public required List<GlRecipeRow> ARows { get; init; }
    public decimal TotalGallonsA { get; init; }
    public decimal GallonsB { get; init; }
    public decimal TotalGallons { get; init; }
    public decimal AvgGallonPerTreatA { get; init; }
    public decimal AvgGallonPerTreatB { get; init; }
    public decimal LiterPerTreatA { get; init; }
    public decimal LiterPerTreatB { get; init; }
    public decimal GlCostPerTreatA { get; init; }
    public decimal GlCostPerTreatB { get; init; }
    public decimal GlCostMonthA { get; init; }
    public decimal GlCostMonthB { get; init; }
    public decimal GlCostMonth { get; init; }
    public decimal GlSaleRevenue { get; init; }
    /// <summary>ค่าใช้จ่าย/เดือน แบบ GL (หักขายแกลลอนคืนแล้ว)</summary>
    public decimal GlNetMonth { get; init; }

    // ----- Leasing -----
    public decimal LeasingTotalInterest { get; init; }
    public decimal LeasingTotal { get; init; }
    public decimal LeasingPerMonth { get; init; }

    // ----- Powder -----
    public required PowderResult PowderA { get; init; }
    public required PowderResult PowderB { get; init; }

    // ----- CCDS -----
    public required List<CcdsRecipeRow> CcdsRows { get; init; }
    public decimal CcdsPowderCost { get; init; }
    public decimal CcdsOtherGlCost { get; init; }
    public decimal CcdsOtherGallons { get; init; }
    public decimal CcdsGlSaleRevenue { get; init; }
    public decimal MixedLitersA { get; init; }
    public decimal MixedLitersB { get; init; }
    public decimal MixedLitersTotal { get; init; }

    // ----- Utility -----
    public decimal WaterM3 { get; init; }
    public decimal WaterCost { get; init; }
    public required List<EquipmentRow> EquipmentRows { get; init; }
    public decimal ElectricKwh { get; init; }
    public decimal ElectricCost { get; init; }
    public decimal UtilityCost { get; init; }
    /// <summary>ค่า Utility ที่นำมาคิดในต้นทุนแบบ CCDS (0 เมื่อเลือก "ไม่นำมาคิด")</summary>
    public decimal UtilityCostCounted { get; init; }

    /// <summary>ค่าใช้จ่าย/เดือน แบบ CCDS</summary>
    public decimal CcdsNetMonth { get; init; }

    // ----- เปรียบเทียบ -----
    public decimal SavingPerMonth { get; init; }
    public decimal GlCostPerTreatment { get; init; }
    public decimal CcdsCostPerTreatment { get; init; }
    public decimal SavingDuringLease { get; init; }
    public decimal PmDuringLease { get; init; }
    public decimal SavingDuringLeaseAfterPm { get; init; }
    public decimal SavingPerMonthAfterLease { get; init; }
    public decimal SavingPerYearAfterLease { get; init; }
    public decimal SavingHorizon { get; init; }
    public required List<YearRow> Years { get; init; }
    /// <summary>ปีแรกที่ยอดสะสมเป็นบวกและเป็นบวกต่อเนื่องจนจบช่วงวิเคราะห์ (null = ไม่คืนทุน)</summary>
    public int? BreakEvenYear { get; init; }
    /// <summary>true = ตัวเลขที่กรอกใหญ่เกินกว่าจะคำนวณได้ (ผลลัพธ์เป็นศูนย์ ให้ผู้ใช้ตรวจตัวเลข)</summary>
    public bool Overflow { get; init; }
}

// ===================== ตัวคำนวณ =====================

public static class CcdsCalculator
{
    public static CcdsResult Calculate(CcdsInput input)
    {
        try
        {
            return CalculateCore(input, Sanitized(input));
        }
        catch (OverflowException)
        {
            // ตัวเลขใหญ่ผิดปกติ: คืนผลว่าง (ไม่ให้ทั้งแอปล่ม) และติดธงให้หน้าจอแจ้งเตือน
            return CalculateCore(input, Presets.NewFrom(Sanitized(input))) with { Overflow = true };
        }
    }

    private static CcdsResult CalculateCore(CcdsInput input, CcdsInput clean)
    {
        // โหมดลิตร/Treatment: แปลงเป็นแกลลอนก่อน แล้วคำนวณแบบเดียวกับโหมดแกลลอน
        var i = ToGallons(clean);
        decimal t = i.TreatmentsPerMonth;
        decimal totalA = i.ARecipes.Sum(r => r.Gallons);
        decimal glB = i.GallonsB;

        // ---------- 1) แบบ GL ----------
        var aRows = i.ARecipes
            .Select(r => new GlRecipeRow(r.Name, r.Gallons, r.Treatments ?? AutoTreatments(r, totalA, t)))
            .ToList();

        decimal avgA = Div(totalA, t);
        decimal avgB = Div(glB, t);
        decimal literA = avgA * i.LiterPerGallonA;
        decimal literB = avgB * i.LiterPerGallonB;

        decimal glCostA = totalA * i.PricePerGallonA;
        decimal glCostB = glB * i.PricePerGallonB;
        decimal totalGl = totalA + glB;
        decimal glSale = totalGl * i.GallonSalePrice;
        decimal glNet = glCostA + glCostB - glSale;

        // ---------- 2) Leasing (ดอกเบี้ยแบบ Flat) ----------
        decimal interest = i.LeasingPrice * i.InterestRatePercent / 100m * i.InstallmentYears;
        decimal leasingTotal = i.LeasingPrice + interest;
        decimal leasingMonth = Div(leasingTotal, i.InstallmentYears * 12);

        // ---------- 3) ผงผสม ----------
        var powderA = Powder("A", i.PowderA, literA);
        var powderB = Powder("B", i.PowderB, literB);

        // ---------- 4) ใช้ระบบ CCDS ----------
        // ลิตรที่ต้องผสม: คูณก่อนหาร (ไม่ให้ทศนิยมคลาด) และไม่ขึ้นกับ Treatment/เดือน = 0
        //   สูตร A ที่ใช้ผง = Treatment ของสูตร × ลิตร/Treatment เฉลี่ยของ A (= แกลลอนของสูตร × ลิตรต่อแกลลอน เมื่อ Treatment อัตโนมัติ)
        //   B = แกลลอน B × ลิตรต่อแกลลอน B
        // ค่าผงต่อเดือน = ลิตรที่ผสม ÷ ลิตรต่อกล่อง × ราคากล่อง (เท่ากับ Treatment × ต้นทุนผงต่อ Treatment)
        decimal literBoxA = i.PowderA.MixPerBox * i.PowderA.MixLiter;
        decimal literBoxB = i.PowderB.MixPerBox * i.PowderB.MixLiter;
        var ccdsRows = new List<CcdsRecipeRow>();
        foreach (var (rec, row) in i.ARecipes.Zip(aRows))
        {
            if (rec.UsePowder)
            {
                decimal mixed = t > 0 ? Div(row.Treatments * totalA * i.LiterPerGallonA, t) : rec.Gallons * i.LiterPerGallonA;
                ccdsRows.Add(new CcdsRecipeRow(rec.Name, true, 0, row.Treatments, 0,
                    powderA.CostPerTreat, Div(mixed, literBoxA) * i.PowderA.CostPerBox, mixed));
            }
            else
                ccdsRows.Add(new CcdsRecipeRow(rec.Name, false, rec.Gallons, row.Treatments, i.CcdsPricePerGallonA,
                    Div(rec.Gallons * i.CcdsPricePerGallonA, row.Treatments), rec.Gallons * i.CcdsPricePerGallonA, 0));
        }
        decimal mixedB = glB * i.LiterPerGallonB;
        ccdsRows.Add(new CcdsRecipeRow("B", true, 0, t, 0, powderB.CostPerTreat, Div(mixedB, literBoxB) * i.PowderB.CostPerBox, mixedB));

        decimal powderCost = ccdsRows.Where(r => r.IsPowder).Sum(r => r.CostPerMonth);
        decimal otherGlCost = ccdsRows.Where(r => !r.IsPowder).Sum(r => r.CostPerMonth);
        decimal otherGallons = ccdsRows.Where(r => !r.IsPowder).Sum(r => r.Gallons);
        decimal ccdsGlSale = otherGallons * i.GallonSalePrice;
        decimal litersA = ccdsRows.Take(i.ARecipes.Count).Where(r => r.IsPowder).Sum(r => r.MixedLiters);   // ไม่อิงชื่อ (สูตร A อาจตั้งชื่อว่า "B")
        decimal litersB = mixedB;
        decimal litersTotal = litersA + litersB;

        // ---------- 5) Utility ----------
        decimal waterM3 = litersTotal * i.WaterM3Per1000Liter / 1000m;
        decimal waterCost = waterM3 * i.WaterRate;

        int days = Math.Max(i.DaysPerMonth, 1);
        decimal autoRunsA = Runs(litersA, days, i.MixTankLiterA);
        decimal autoRunsB = Runs(litersB, days, i.MixTankLiterB);
        var eqRows = i.Equipment.Select(e =>
        {
            decimal runs = e.RunsPerDayOverride ?? e.Kind switch
            {
                EquipmentKind.MixingA => autoRunsA,
                EquipmentKind.MixingB => autoRunsB,
                _ => 1
            };
            decimal perDay = e.Kw * e.Hours * e.Qty * runs;
            return new EquipmentRow(e.Name, e.Kw, e.Hours, e.Qty, runs, perDay, perDay * days);
        }).ToList();
        decimal kwh = eqRows.Sum(e => e.KwhPerMonth);
        decimal electricCost = kwh * i.ElectricRate;
        decimal utility = waterCost + electricCost;

        decimal utilityCounted = i.IncludeUtility ? utility : 0;
        decimal ccdsNet = leasingMonth + powderCost + otherGlCost + utilityCounted - ccdsGlSale;

        // ---------- 6) เปรียบเทียบ & ระยะยาว ----------
        decimal saving = glNet - ccdsNet;
        int n = Math.Max(i.InstallmentYears, 0);
        decimal savingLease = saving * 12 * n;
        decimal pmLease = Math.Max(0, n - i.WarrantyYears) * i.PmPerYear;
        decimal savingLeaseAfterPm = savingLease - pmLease;
        decimal monthAfter = saving + leasingMonth - i.PmPerYear / 12m;
        decimal yearAfter = monthAfter * 12;

        var years = new List<YearRow>();
        decimal cum = 0;
        for (int y = 1; y <= Math.Max(i.HorizonYears, 1); y++)
        {
            decimal s = saving * 12;
            decimal pm = y > i.WarrantyYears ? i.PmPerYear : 0;
            decimal freed = y > n ? leasingMonth * 12 : 0;
            decimal net = s - pm + freed;
            cum += net;
            years.Add(new YearRow(y, s, pm, freed, net, cum));
        }

        // ปีคืนทุน = ปีแรกที่ยอดสะสมเป็นบวก "และเป็นบวกต่อเนื่องจนจบช่วงวิเคราะห์"
        // (ถ้าบวกช่วงแรกแล้วกลับไปติดลบเพราะค่า PM จะไม่นับปีแรกนั้น)
        int? breakEven = null;
        for (int k = years.Count - 1; k >= 0 && years[k].Cumulative > 0; k--)
            breakEven = years[k].Year;

        return new CcdsResult
        {
            Input = input,
            Treatments = t,
            ARows = aRows,
            TotalGallonsA = totalA,
            GallonsB = glB,
            TotalGallons = totalGl,
            AvgGallonPerTreatA = avgA,
            AvgGallonPerTreatB = avgB,
            LiterPerTreatA = literA,
            LiterPerTreatB = literB,
            GlCostPerTreatA = avgA * i.PricePerGallonA,
            GlCostPerTreatB = avgB * i.PricePerGallonB,
            GlCostMonthA = glCostA,
            GlCostMonthB = glCostB,
            GlCostMonth = glCostA + glCostB,
            GlSaleRevenue = glSale,
            GlNetMonth = glNet,
            LeasingTotalInterest = interest,
            LeasingTotal = leasingTotal,
            LeasingPerMonth = leasingMonth,
            PowderA = powderA,
            PowderB = powderB,
            CcdsRows = ccdsRows,
            CcdsPowderCost = powderCost,
            CcdsOtherGlCost = otherGlCost,
            CcdsOtherGallons = otherGallons,
            CcdsGlSaleRevenue = ccdsGlSale,
            MixedLitersA = litersA,
            MixedLitersB = litersB,
            MixedLitersTotal = litersTotal,
            WaterM3 = waterM3,
            WaterCost = waterCost,
            EquipmentRows = eqRows,
            ElectricKwh = kwh,
            ElectricCost = electricCost,
            UtilityCost = utility,
            UtilityCostCounted = utilityCounted,
            CcdsNetMonth = ccdsNet,
            SavingPerMonth = saving,
            GlCostPerTreatment = Div(glNet, t),
            CcdsCostPerTreatment = Div(ccdsNet, t),
            SavingDuringLease = savingLease,
            PmDuringLease = pmLease,
            SavingDuringLeaseAfterPm = savingLeaseAfterPm,
            SavingPerMonthAfterLease = monthAfter,
            SavingPerYearAfterLease = yearAfter,
            SavingHorizon = cum,
            Years = years,
            BreakEvenYear = breakEven
        };
    }

    /// <summary>จำนวนครั้งผสมต่อวัน = ปัดขึ้น(ลิตรต่อวัน ÷ ขนาดถัง) — ปัดทศนิยม 6 ตำแหน่งก่อน กันปัดขึ้นเกินจากเศษทศนิยม; สูงสุด 10,000</summary>
    public const decimal MaxRunsPerDay = 10_000m;
    private static decimal Runs(decimal liters, int days, decimal tank) =>
        Math.Min(MaxRunsPerDay, Math.Ceiling(Math.Round(Div(Div(liters, days), tank), 6)));

    private static PowderResult Powder(string name, PowderInput p, decimal literPerTreat)
    {
        decimal literPerBox = p.MixPerBox * p.MixLiter;
        decimal treatPerBox = Div(literPerBox, literPerTreat);
        return new PowderResult(name, p.CostPerBox, p.MixPerBox, p.MixLiter, literPerBox, treatPerBox,
            Div(p.CostPerBox, treatPerBox));
    }

    /// <summary>
    /// คืนข้อมูลในรูปแบบ "แกลลอน/เดือน" เสมอ
    /// โหมด LitersPerTreatment: แกลลอน = ลิตร/Tx × Treatment ÷ ลิตรต่อแกลลอน (Treatment สูตร A ที่ว่าง = 0)
    /// </summary>
    public static CcdsInput ToGallons(CcdsInput i)
    {
        if (i.UsageMode != UsageInputMode.LitersPerTreatment) return i;
        var c = Clone(i);
        foreach (var r in c.ARecipes)
        {
            r.Treatments ??= 0;
            r.Gallons = Div(r.LitersPerTreatment * r.Treatments.Value, i.LiterPerGallonA);
        }
        c.GallonsB = Div(i.LitersPerTreatmentB * i.TreatmentsPerMonth, i.LiterPerGallonB);
        c.UsageMode = UsageInputMode.Gallons;
        return c;
    }

    /// <summary>
    /// สลับโหมดการกรอก โดยแปลงค่าเดิมให้ผลคำนวณเท่าเดิม
    /// คืนค่า false (ไม่สลับ) ถ้ามีแกลลอนแต่ยังไม่มี Treatment/เดือน เพราะแปลงเป็นลิตร/Treatment ไม่ได้ (ข้อมูลจะหาย)
    /// </summary>
    public static bool SwitchMode(CcdsInput i, UsageInputMode mode)
    {
        if (i.UsageMode == mode) return true;
        if (mode == UsageInputMode.LitersPerTreatment)
        {
            decimal totalA = i.ARecipes.Sum(r => r.Gallons);
            bool hasGallons = totalA > 0 || i.GallonsB > 0;
            if (hasGallons && i.TreatmentsPerMonth <= 0) return false;
            // Treatment อัตโนมัติ → เก็บเป็นตัวเลข (ทศนิยม 2 ตำแหน่ง ให้ผลคำนวณแทบไม่เปลี่ยน); ไม่มีแกลลอน = ปล่อยว่างไว้
            var txs = i.ARecipes.Select(r => r.Treatments ??
                (r.Gallons > 0 ? Math.Round(AutoTreatments(r, totalA, i.TreatmentsPerMonth), 2) : (decimal?)null)).ToList();
            // ตรวจก่อนแก้ข้อมูล: มีแกลลอนแต่ Treatment = 0 → แปลงไม่ได้ (ไม่แตะข้อมูลเลย)
            if (i.ARecipes.Where((r, k) => r.Gallons > 0 && (txs[k] ?? 0) <= 0).Any()) return false;
            for (int k = 0; k < i.ARecipes.Count; k++)
            {
                var r = i.ARecipes[k];
                r.Treatments = txs[k];
                r.LitersPerTreatment = Math.Round(Div(r.Gallons * i.LiterPerGallonA, txs[k] ?? 0), LiterDecimals);
            }
            i.LitersPerTreatmentB = Math.Round(Div(i.GallonsB * i.LiterPerGallonB, i.TreatmentsPerMonth), LiterDecimals);
        }
        else
        {
            var g = ToGallons(i);
            decimal totalA = g.ARecipes.Sum(r => r.Gallons);
            for (int k = 0; k < i.ARecipes.Count; k++)
            {
                var r = i.ARecipes[k];
                r.Gallons = Math.Round(g.ARecipes[k].Gallons, 2);
                // Treatment ที่เท่ากับค่าอัตโนมัติ (หรือ 0) กลับเป็น "ว่าง = อัตโนมัติ"
                var auto = Math.Round(AutoTreatments(r, totalA, i.TreatmentsPerMonth), 2);
                if (r.Treatments is decimal tx && (tx == 0 || Math.Abs(tx - auto) < 0.01m)) r.Treatments = null;
            }
            i.GallonsB = Math.Round(g.GallonsB, 2);
        }
        i.UsageMode = mode;
        return true;
    }

    // ===================== ตรวจช่วงค่าที่กรอก =====================

    /// <summary>สำเนาข้อมูลที่ตัดค่าให้อยู่ในช่วงที่ใช้คำนวณได้ (ข้อมูลเดิมไม่ถูกแก้)</summary>
    public static CcdsInput Sanitized(CcdsInput i) { var c = Clone(i); Sanitize(c); return c; }

    /// <summary>
    /// ตัดค่าที่กรอกให้อยู่ในช่วงที่สมเหตุสมผล: ไม่ติดลบ, ไม่ใหญ่จนคำนวณล้น, ปี/วันอยู่ในช่วงที่ใช้ได้
    /// (ระยะเวลาวิเคราะห์ 1–30 ปี, ผ่อน 1–30 ปี, Warranty 0–30 ปี, วันทำงาน 1–31 วัน)
    /// </summary>
    public static void Sanitize(CcdsInput i)
    {
        static decimal C(decimal v, decimal max, int dp = 6) => v <= 0 ? 0 : Math.Round(Math.Min(v, max), dp);
        const decimal Qty = 10_000_000m, Price = 1_000_000m, Money = 1_000_000_000_000m;
        i.Machines = Math.Clamp(i.Machines, 0, 100_000);
        i.TreatmentsPerMonth = C(i.TreatmentsPerMonth, Qty, 2);
        i.ARecipes ??= new();
        foreach (var r in i.ARecipes)
        {
            r.Name ??= "";
            r.Gallons = C(r.Gallons, Qty);
            r.Treatments = r.Treatments is decimal tx ? C(tx, Qty, 2) : null;
            r.LitersPerTreatment = C(r.LitersPerTreatment, 1_000m);
        }
        i.GallonsB = C(i.GallonsB, Qty);
        i.LitersPerTreatmentB = C(i.LitersPerTreatmentB, 1_000m);
        i.LiterPerGallonA = C(i.LiterPerGallonA, 1_000m);
        i.LiterPerGallonB = C(i.LiterPerGallonB, 1_000m);
        i.PricePerGallonA = C(i.PricePerGallonA, Price);
        i.PricePerGallonB = C(i.PricePerGallonB, Price);
        i.GallonSalePrice = C(i.GallonSalePrice, Price);
        i.CcdsPricePerGallonA = C(i.CcdsPricePerGallonA, Price);
        i.LeasingPrice = C(i.LeasingPrice, Money);
        i.InterestRatePercent = C(i.InterestRatePercent, 100m);
        i.InstallmentYears = Math.Clamp(i.InstallmentYears, 1, 30);
        foreach (var p in new[] { i.PowderA ??= new(), i.PowderB ??= new() })
        {
            p.CostPerBox = C(p.CostPerBox, Price);
            p.MixPerBox = C(p.MixPerBox, 10_000m, 2);
            p.MixLiter = C(p.MixLiter, 10_000m, 2);
        }
        i.WaterRate = C(i.WaterRate, Price);
        i.WaterM3Per1000Liter = C(i.WaterM3Per1000Liter, 10_000m);
        i.ElectricRate = C(i.ElectricRate, 10_000m);
        i.DaysPerMonth = Math.Clamp(i.DaysPerMonth, 1, 31);
        i.MixTankLiterA = C(i.MixTankLiterA, Qty, 2);
        i.MixTankLiterB = C(i.MixTankLiterB, Qty, 2);
        i.Equipment ??= new();
        foreach (var e in i.Equipment)
        {
            e.Name ??= "";
            e.Kw = C(e.Kw, 10_000m);
            e.Hours = C(e.Hours, 24m);
            e.Qty = C(e.Qty, 10_000m);
            if (e.RunsPerDayOverride is int ro) e.RunsPerDayOverride = Math.Clamp(ro, 0, (int)MaxRunsPerDay);
        }
        i.PmPerYear = C(i.PmPerYear, Money);
        i.WarrantyYears = Math.Clamp(i.WarrantyYears, 0, 30);
        i.HorizonYears = Math.Clamp(i.HorizonYears, 1, 30);
    }

    /// <summary>ชื่อสูตรที่ใช้ผงผสม เช่น "A2 & B" (สูตร A ที่เลือกใช้ผง + B ซึ่งใช้ผงเสมอ)</summary>
    public static string PowderRecipeNames(CcdsResult r) =>
        string.Join(" & ", r.CcdsRows.Where(x => x.IsPowder).Select(x => x.Name));

    /// <summary>ป้ายแถวค่าผงผสม เช่น "ผงผสมสูตร A2 & B"</summary>
    public static string PowderLabel(CcdsResult r) => "ผงผสมสูตร " + PowderRecipeNames(r);

    /// <summary>ทศนิยมของลิตร/Treatment ที่คำนวณจากแกลลอน (6 ตำแหน่ง → แปลงกลับเป็นแกลลอนคลาดไม่ถึง 0.001; ช่องกรอกแสดง 3 ตำแหน่ง)</summary>
    public const int LiterDecimals = 6;

    /// <summary>
    /// โหมดลิตร/Treatment: กรอกแกลลอน/เดือนของสูตร A แล้วคำนวณลิตร/Treatment = แกลลอน × ลิตรต่อแกลลอน ÷ Treatment
    /// คืนค่า false ถ้ายังไม่มี Treatment ของสูตรนี้ (คำนวณไม่ได้)
    /// </summary>
    public static bool SetGallonsA(CcdsInput i, ARecipeInput r, decimal gallons)
    {
        var tx = r.Treatments ?? 0;
        if (tx <= 0) return false;
        r.LitersPerTreatment = Math.Round(gallons * i.LiterPerGallonA / tx, LiterDecimals);
        return true;
    }

    /// <summary>โหมดลิตร/Treatment: กรอกแกลลอน B/เดือน แล้วคำนวณลิตร B/Treatment (ใช้ Treatment/เดือน)</summary>
    public static bool SetGallonsB(CcdsInput i, decimal gallons)
    {
        if (i.TreatmentsPerMonth <= 0) return false;
        i.LitersPerTreatmentB = Math.Round(gallons * i.LiterPerGallonB / i.TreatmentsPerMonth, LiterDecimals);
        return true;
    }

    private static CcdsInput Clone(CcdsInput i) =>
        System.Text.Json.JsonSerializer.Deserialize<CcdsInput>(System.Text.Json.JsonSerializer.Serialize(i))!;

    /// <summary>Treatment ของสูตร A แบบอัตโนมัติ = แกลลอนสูตรนี้ ÷ แกลลอน A รวม × Treatment/เดือน</summary>
    public static decimal AutoTreatments(ARecipeInput r, decimal totalGallonsA, decimal treatmentsPerMonth) =>
        Div(r.Gallons, totalGallonsA) * treatmentsPerMonth;

    private static decimal Div(decimal a, decimal b) => b == 0 ? 0 : a / b;
}
