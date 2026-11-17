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
    private static CcdsResult Sample() => CcdsCalculator.Calculate(Presets.Sample1());

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
        var i = Presets.Sample1();
        i.Equipment[0].RunsPerDayOverride = 5;
        Assert.Equal(5, CcdsCalculator.Calculate(i).EquipmentRows[0].RunsPerDay);
    }

    [Fact]
    public void Scenario_RoundsTreatmentsUpThenAppliesFactor()
    {
        var i = Presets.Sample1();
        var s = CcdsCalculator.BuildScenario(i);
        // A1: 450/1970 × 2900 = 662.4 → 663 × 0.8
        Near(663 * 0.8m, s.ARecipes[0].Gallons);
        Near(2900 * 1.0m, s.GallonsB);
        Assert.False(s.ScenarioEnabled);
        Assert.Equal(450, i.ARecipes[0].Gallons);                     // ไม่แก้ข้อมูลต้นฉบับ
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
