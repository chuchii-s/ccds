using CcdsSimulator.Models;
using CcdsSimulator.Services;

namespace CcdsSimulator.Tests;

/// <summary>
/// ตรวจสูตรด้วย "ตัวอย่าง 1" (ตัวเลขสมมติ) เทียบกับค่าที่คำนวณมือด้วยวิธีคิดอีกแบบ
/// (สูตรเดิมตรวจเทียบกับไฟล์ Excel ต้นฉบับแล้ว — คลาดเคลื่อนไม่เกิน ~0.1% จากการปัดเศษ)
/// </summary>
public class CalculatorTests
{
    private static void Near(decimal expected, decimal actual, decimal tol = 0.01m) =>
        Assert.True(Math.Abs(expected - actual) <= tol, $"expected {expected:N4} but was {actual:N4}");

    // ตัวอย่าง 1: 2,900 Tx, A1 450 / A2 1,200 (ผง) / A3 150 / A6 170, B 2,000, ราคา A 60 / B 55, เครื่อง 2,600,000
    private static CcdsResult Sample() => CcdsCalculator.Calculate(TestData.S1());

    [Fact]
    public void GlMode()
    {
        var r = Sample();
        Near(1970, r.TotalGallonsA);
        Near(1970m * 60, r.GlCostMonthA);
        Near(2000m * 55, r.GlCostMonthB);               // B = GL × ราคา/GL
        Near((1970m + 2000) * 4, r.GlSaleRevenue);
        Near(118_200 + 110_000 - 15_880, r.GlNetMonth);
        Near(1200m / 1970 * 2900, r.ARows[1].Treatments, 0.0001m);
    }

    [Fact]
    public void Leasing_FlatInterest()
    {
        var r = Sample();
        Near(2_600_000m * 0.04m * 5, r.LeasingTotalInterest);
        Near(3_120_000m / 60, r.LeasingPerMonth);
    }

    [Fact]
    public void Powder_CostEqualsLitersMixedTimesPricePerLiter()
    {
        var r = Sample();
        // A2: 1,200 GL × 5.5 L = 6,600 L ; กล่อง A = 100 L ราคา 1,000
        Near(6_600m / 100 * 1_000, r.CcdsRows.Single(x => x.Name == "A2").CostPerMonth);
        // B: 2,000 GL × 6 L = 12,000 L ; กล่อง B = 240 L ราคา 1,100
        Near(12_000m / 240 * 1_100, r.CcdsRows.Single(x => x.Name == "B").CostPerMonth);
        Near(6_600 + 12_000, r.MixedLitersTotal);
        Near((450m + 150 + 170) * 50, r.CcdsOtherGlCost);
        Near((450m + 150 + 170) * 4, r.CcdsGlSaleRevenue);
    }

    [Fact]
    public void Utility()
    {
        var r = Sample();
        Near(18_600m * 2 / 1000 * 30, r.WaterCost);
        // Mixing A: 6,600/30 = 220 L/วัน ÷ 100 → 3 รอบ ; Mixing B: 400 L/วัน ÷ 120 → 4 รอบ
        Assert.Equal(3, r.EquipmentRows[0].RunsPerDay);
        Assert.Equal(4, r.EquipmentRows[1].RunsPerDay);
        Near(0.25m * 0.5m * 2 * 3 * 30 + 0.25m * 0.5m * 2 * 4 * 30 + 2 * 0.33m * 18 * 30, r.ElectricKwh);
        Near(r.WaterCost + r.ElectricKwh * 4.9m, r.UtilityCost);
    }

    [Fact]
    public void Ccds_Saving_LongTerm()
    {
        var r = Sample();
        decimal ccds = 52_000 + 66_000 + 55_000 + 38_500 + r.UtilityCost - 3_080;
        Near(ccds, r.CcdsNetMonth);
        Near(212_320 - ccds, r.SavingPerMonth);
        Near(r.SavingPerMonth * 60, r.SavingDuringLease);
        Near(160_000, r.PmDuringLease);                               // ผ่อน 5 ปี − Warranty 4 ปี = PM 1 ปี
        Near(r.SavingPerMonth + 52_000 - 160_000m / 12, r.SavingPerMonthAfterLease);
        Near(r.SavingDuringLeaseAfterPm + 5 * r.SavingPerYearAfterLease, r.SavingHorizon); // ปีที่ 6–10 = 5 ปี
        Near(r.SavingHorizon, r.Years[^1].Cumulative);
    }

    [Fact]
    public void RunsOverride_IsUsed()
    {
        var i = TestData.S1();
        i.Equipment[0].RunsPerDayOverride = 5;
        Assert.Equal(5, CcdsCalculator.Calculate(i).EquipmentRows[0].RunsPerDay);
    }

    [Fact]
    public void EmptyInput_DoesNotThrow()
    {
        var r = CcdsCalculator.Calculate(Presets.NewFrom(Presets.FactoryDefaults()));
        Assert.Equal(0, r.GlNetMonth);
        Assert.Equal(10, r.Years.Count);
    }

    [Fact]
    public void Fmt_NoNegativeZero()
    {
        Assert.Equal("0", Fmt.N0(-0.3m));
        Assert.Equal("0.00", Fmt.N2(-0.001m));
        Assert.Equal("+1,235", Fmt.Signed(1234.6m));
        Assert.Equal("-1,234.60", Fmt.Signed(-1234.6m, 2));
    }
}

public class FmtTests
{
    [Fact]
    public void ThaiDateTime_UsesBuddhistEra()
    {
        var d = new DateTime(2026, 11, 17, 14, 5, 0);
        Assert.Equal("17 พ.ย. 2569 14:05", Fmt.ThaiDateTime(d));
        Assert.Equal("17 พ.ย. 69 14:05", Fmt.ThaiDateTime(d, shortYear: true));
    }
}

public class BreakEvenTests
{
    [Fact]
    public void BreakEven_IgnoresEarlyPositiveYearsThatTurnNegative()
    {
        // PM ปีละ 260,000 หลังหมด Warranty 3 ปี: ยอดสะสมบวกปี 1–3, ติดลบปี 4–6, กลับเป็นบวกถาวรปี 7
        var i = TestData.S1();
        i.PmPerYear = 260_000;
        i.WarrantyYears = 3;
        var r = CcdsCalculator.Calculate(i);
        Assert.True(r.Years[0].Cumulative > 0);
        Assert.True(r.Years[3].Cumulative < 0);
        Assert.Equal(7, r.BreakEvenYear);
    }

    [Fact]
    public void BreakEven_NullWhenNeverPositiveAtEnd()
    {
        var i = TestData.S1();
        i.PmPerYear = 2_000_000;
        Assert.Null(CcdsCalculator.Calculate(i).BreakEvenYear);
    }
}

public class TreatmentOverrideTests
{
    private static void Near(decimal e, decimal a, decimal tol = 0.01m) =>
        Assert.True(Math.Abs(e - a) <= tol, $"expected {e:N4} but was {a:N4}");

    [Fact]
    public void Blank_UsesAutoProportion()
    {
        var r = CcdsCalculator.Calculate(TestData.S1());
        Near(450m / 1970 * 2900, r.ARows[0].Treatments, 0.0001m);
    }

    [Fact]
    public void Entered_IsUsedForPowderCost()
    {
        var i = TestData.S1();
        i.ARecipes[1].Treatments = 1800;    // A2 (ผงผสม)
        i.ARecipes[0].Treatments = 600;     // A1 (แกลลอน)
        var r = CcdsCalculator.Calculate(i);

        Assert.Equal(1800, r.ARows[1].Treatments);
        var a2 = r.CcdsRows.Single(x => x.Name == "A2");
        Near(1800 * r.PowderA.CostPerTreat, a2.CostPerMonth);
        Near(1800 * r.LiterPerTreatA, a2.MixedLiters);

        var a1 = r.CcdsRows.Single(x => x.Name == "A1");
        Near(450m * 50, a1.CostPerMonth);                // ค่าแกลลอนไม่ขึ้นกับ Treatment
        Near(450m * 50 / 600, a1.CostPerTreat);          // บาท/Treatment ของสูตรนี้

        Assert.Equal(2900, r.Treatments);               // ยอดรวมยังใช้ข้อ 1

        // ค่าเดียวกับที่ calculate() ใน CCDS_Simulator.html ได้ (ตรวจว่า C# กับ JavaScript ตรงกัน)
        Near(212_798.84448m, r.CcdsNetMonth);
        Near(2_102_538.66207m, r.SavingHorizon);
    }
}

public class UsageModeTests
{
    private static void Near(decimal e, decimal a, decimal tol = 0.01m) =>
        Assert.True(Math.Abs(e - a) <= tol, $"expected {e:N4} but was {a:N4}");

    /// <summary>ตัวอย่าง 1 กรอกแบบลิตร/Treatment</summary>
    internal static CcdsInput LitersSample()
    {
        var i = TestData.S1();
        i.UsageMode = UsageInputMode.LitersPerTreatment;
        (decimal lpt, decimal tx)[] a = { (3.5m, 660), (3.8m, 1770), (3.6m, 220), (3.7m, 250) };
        for (int k = 0; k < 4; k++) { i.ARecipes[k].LitersPerTreatment = a[k].lpt; i.ARecipes[k].Treatments = a[k].tx; }
        i.LitersPerTreatmentB = 4.2m;
        return i;
    }

    [Fact]
    public void Liters_ConvertsToGallons()
    {
        var r = CcdsCalculator.Calculate(LitersSample());
        Near(3.8m * 1770 / 5.5m, r.ARows[1].Gallons);
        Near(4.2m * 2900 / 6m, r.GallonsB);
        Near((3.5m * 660 + 3.8m * 1770 + 3.6m * 220 + 3.7m * 250) / 5.5m * 60, r.GlCostMonthA);
        Near(4.2m * 2900 / 6m * 55, r.GlCostMonthB);
        Assert.Equal(UsageInputMode.LitersPerTreatment, r.Input.UsageMode);     // Input เดิมไม่ถูกแก้
    }

    [Fact]
    public void Liters_MatchesHtmlVersion()
    {
        // ค่าที่ calculate() ใน CCDS_Simulator.html ได้จากข้อมูลชุดเดียวกัน
        var i = LitersSample();
        var r = CcdsCalculator.Calculate(i);
        Near(213_015.09091m, r.GlNetMonth);
        Near(210_263.93522m, r.CcdsNetMonth);
        Near(2_490_138.68238m, r.SavingHorizon);
        var j = TestData.S1();
        CcdsCalculator.SwitchMode(j, UsageInputMode.LitersPerTreatment);
        Near(3.74m, j.ARecipes[0].LitersPerTreatment);
        Near(4.14m, j.LitersPerTreatmentB);
    }

    [Fact]
    public void Liters_BlankTreatmentCountsAsZero()
    {
        var i = LitersSample();
        i.ARecipes[0].Treatments = null;
        Assert.Equal(0, CcdsCalculator.Calculate(i).ARows[0].Gallons);
    }

    [Fact]
    public void SwitchMode_KeepsResultsAboutTheSame()
    {
        var i = TestData.S1();
        var before = CcdsCalculator.Calculate(i);
        CcdsCalculator.SwitchMode(i, UsageInputMode.LitersPerTreatment);
        Assert.Equal(UsageInputMode.LitersPerTreatment, i.UsageMode);
        Assert.Equal(662.44m, i.ARecipes[0].Treatments);             // เก็บทศนิยม 2 ตำแหน่ง
        var liters = CcdsCalculator.Calculate(i);
        Near(before.GlNetMonth, liters.GlNetMonth, 1m);
        Near(before.SavingPerMonth, liters.SavingPerMonth, 1m);       // ไม่คลาดจากการปัด Treatment
        CcdsCalculator.SwitchMode(i, UsageInputMode.Gallons);
        Near(450, i.ARecipes[0].Gallons, 1);
        Near(2000, i.GallonsB, 1);
    }
}

public class IncludeUtilityTests
{
    [Fact]
    public void Excluded_RemovesUtilityFromCcdsCostOnly()
    {
        var i = TestData.S1();
        var on = CcdsCalculator.Calculate(i);
        i.IncludeUtility = false;
        var off = CcdsCalculator.Calculate(i);
        Assert.Equal(on.UtilityCost, off.UtilityCost);                 // ยังคำนวณไว้แสดง
        Assert.Equal(0, off.UtilityCostCounted);
        Assert.True(Math.Abs(on.CcdsNetMonth - on.UtilityCost - off.CcdsNetMonth) < 0.0001m);
        Assert.Equal(on.GlNetMonth, off.GlNetMonth);                    // แบบ GL ไม่เกี่ยว
        Assert.True(Math.Abs(on.SavingPerMonth + on.UtilityCost - off.SavingPerMonth) < 0.0001m);
    }
}

/// <summary>ข้อมูลทดสอบ = ตัวอย่าง 1 ที่ดอกเบี้ย 4% (ค่าที่คำนวณมือและค่าเทียบ HTML ใช้ 4%)</summary>
internal static class TestData
{
    public static CcdsInput S1()
    {
        var i = Presets.Sample1();
        i.InterestRatePercent = 4m;
        i.PowderMode = PowderCalcMode.Liters;   // ชุดทดสอบเดิมคิดผงจากจำนวนลิตร (ค่าตั้งต้นของโปรแกรมเป็น Dialysate flow)
        return i;
    }

    /// <summary>คิดผงจากจำนวนลิตร (โหมดเดิม) — ค่าตั้งต้นของโปรแกรมเป็น Dialysate flow</summary>
    public static CcdsInput L(CcdsInput i) { i.PowderMode = PowderCalcMode.Liters; return i; }
}

public class LeasingRateTests
{
    [Theory]
    [InlineData(1, 2.85)]
    [InlineData(2, 3.25)]
    [InlineData(3, 3.50)]
    [InlineData(4, 3.60)]
    [InlineData(5, 3.55)]
    public void YearsAndRate_AreLinkedBothWays(int years, double rate)
    {
        Assert.Equal((decimal)rate, LeasingRates.RateFor(years));
        Assert.Equal(years, LeasingRates.YearsFor((decimal)rate));
    }

    [Fact]
    public void UnknownValues_ReturnNull()
    {
        Assert.Null(LeasingRates.RateFor(6));
        Assert.Null(LeasingRates.YearsFor(4m));
    }

    [Fact]
    public void NewProjects_Default5YearsAt355()
    {
        var d = Presets.FactoryDefaults();
        Assert.Equal(5, d.InstallmentYears);
        Assert.Equal(3.55m, d.InterestRatePercent);
    }
}
