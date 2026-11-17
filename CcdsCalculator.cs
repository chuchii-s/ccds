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
    /// <summary>ปีแรกที่ยอดสะสมเป็นบวก (null = ไม่คืนทุนในช่วงที่วิเคราะห์)</summary>
    public int? BreakEvenYear { get; init; }
}

// ===================== ตัวคำนวณ =====================

public static class CcdsCalculator
{
    public static CcdsResult Calculate(CcdsInput i)
    {
        decimal t = i.TreatmentsPerMonth;
        decimal totalA = i.ARecipes.Sum(r => r.Gallons);
        decimal glB = i.GallonsB;

        // ---------- 1) แบบ GL ----------
        var aRows = i.ARecipes
            .Select(r => new GlRecipeRow(r.Name, r.Gallons, Div(r.Gallons, totalA) * t))
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
        var ccdsRows = new List<CcdsRecipeRow>();
        foreach (var (rec, row) in i.ARecipes.Zip(aRows))
        {
            if (rec.UsePowder)
                ccdsRows.Add(new CcdsRecipeRow(rec.Name, true, 0, row.Treatments, 0,
                    powderA.CostPerTreat, row.Treatments * powderA.CostPerTreat, row.Treatments * literA));
            else
                ccdsRows.Add(new CcdsRecipeRow(rec.Name, false, rec.Gallons, row.Treatments, i.CcdsPricePerGallonA,
                    avgA * i.CcdsPricePerGallonA, rec.Gallons * i.CcdsPricePerGallonA, 0));
        }
        ccdsRows.Add(new CcdsRecipeRow("B", true, 0, t, 0, powderB.CostPerTreat, t * powderB.CostPerTreat, t * literB));

        decimal powderCost = ccdsRows.Where(r => r.IsPowder).Sum(r => r.CostPerMonth);
        decimal otherGlCost = ccdsRows.Where(r => !r.IsPowder).Sum(r => r.CostPerMonth);
        decimal otherGallons = ccdsRows.Where(r => !r.IsPowder).Sum(r => r.Gallons);
        decimal ccdsGlSale = otherGallons * i.GallonSalePrice;
        decimal litersA = ccdsRows.Where(r => r.IsPowder && r.Name != "B").Sum(r => r.MixedLiters);
        decimal litersB = t * literB;
        decimal litersTotal = litersA + litersB;

        // ---------- 5) Utility ----------
        decimal waterM3 = litersTotal * i.WaterM3Per1000Liter / 1000m;
        decimal waterCost = waterM3 * i.WaterRate;

        int days = Math.Max(i.DaysPerMonth, 1);
        int autoRunsA = (int)Math.Ceiling(Div(litersA / days, i.MixTankLiterA));
        int autoRunsB = (int)Math.Ceiling(Div(litersB / days, i.MixTankLiterB));
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

        decimal ccdsNet = leasingMonth + powderCost + otherGlCost + utility - ccdsGlSale;

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
        int? breakEven = null;
        for (int y = 1; y <= Math.Max(i.HorizonYears, 1); y++)
        {
            decimal s = saving * 12;
            decimal pm = y > i.WarrantyYears ? i.PmPerYear : 0;
            decimal freed = y > n ? leasingMonth * 12 : 0;
            decimal net = s - pm + freed;
            cum += net;
            if (breakEven is null && cum > 0) breakEven = y;
            years.Add(new YearRow(y, s, pm, freed, net, cum));
        }

        return new CcdsResult
        {
            Input = i,
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

    /// <summary>
    /// สร้างข้อมูล Scenario: ปรับปริมาณแกลลอนให้เป็นค่ามาตรฐาน
    /// GL(สูตร A) = Treatment ของสูตรนั้น (ปัดขึ้น) × Factor A, GL(B) = Treatment ทั้งหมด × Factor B
    /// (ปัดขึ้นตามไฟล์ Excel ต้นฉบับ)
    /// </summary>
    public static CcdsInput BuildScenario(CcdsInput i)
    {
        decimal totalA = i.ARecipes.Sum(r => r.Gallons);
        var clone = System.Text.Json.JsonSerializer.Deserialize<CcdsInput>(
            System.Text.Json.JsonSerializer.Serialize(i))!;
        foreach (var r in clone.ARecipes)
            r.Gallons = Math.Ceiling(Div(r.Gallons, totalA) * i.TreatmentsPerMonth) * i.ScenarioFactorA;
        clone.GallonsB = i.TreatmentsPerMonth * i.ScenarioFactorB;
        clone.ScenarioEnabled = false;
        return clone;
    }

    private static PowderResult Powder(string name, PowderInput p, decimal literPerTreat)
    {
        decimal literPerBox = p.MixPerBox * p.MixLiter;
        decimal treatPerBox = Div(literPerBox, literPerTreat);
        return new PowderResult(name, p.CostPerBox, p.MixPerBox, p.MixLiter, literPerBox, treatPerBox,
            Div(p.CostPerBox, treatPerBox));
    }

    private static decimal Div(decimal a, decimal b) => b == 0 ? 0 : a / b;
}
