using CcdsSimulator.Models;
using CcdsSimulator.Services;
using Xunit;

namespace CcdsSimulator.Tests;

public class SensitivityTests
{
    private static CcdsInput Sample()
    {
        var i = TestData.L(Presets.FactoryDefaults());
        i.TreatmentsPerMonth = 2900; i.Machines = 28; i.LeasingPrice = 2_600_000m;
        i.ARecipes = new() { new() { Name = "A1", Gallons = 3500, UsePowder = true }, new() { Name = "A2", Gallons = 300 } };
        i.GallonsB = 1033;
        return i;
    }

    [Fact]
    public void FactorOne_MatchesBaseResult()
    {
        var i = Sample();
        var a = CcdsCalculator.Calculate(i);
        var b = CcdsCalculator.Calculate(CcdsSensitivity.Scale(i, 1));
        Assert.Equal(a.SavingPerMonth, b.SavingPerMonth);
        Assert.Equal(a.SavingHorizon, b.SavingHorizon);
    }

    [Fact]
    public void BreakEven_IsTheThreshold()
    {
        var i = Sample();
        var s = CcdsSensitivity.Analyze(i, -10, -20);
        Assert.Equal(3, s.Points.Count);
        Assert.NotNull(s.BreakEvenHorizonTx);
        decimal be = s.BreakEvenHorizonTx!.Value;
        decimal At(decimal tx) => CcdsCalculator.Calculate(CcdsSensitivity.Scale(i, tx / i.TreatmentsPerMonth)).SavingHorizon;
        Assert.True(At(be) >= 0, $"be={be} at={At(be)}");
        Assert.True(At(be - 1) < 0, $"be={be} at-1={At(be - 1)} at-2={At(be - 2)} at-10={At(be - 10)}");
        // ยอดลดลงตามจำนวน Treatment
        Assert.True(s.Points[0].SavingHorizon > s.Points[1].SavingHorizon);
        Assert.True(s.Points[1].SavingHorizon > s.Points[2].SavingHorizon);
    }

    [Fact]
    public void LitersMode_ScalesLikeGallonsMode()
    {
        var i = Sample();
        var l = Presets.Clone(i);
        Assert.True(CcdsCalculator.SwitchMode(l, UsageInputMode.LitersPerTreatment));
        var a = CcdsSensitivity.Analyze(i, -10);
        var b = CcdsSensitivity.Analyze(l, -10);
        // โหมดลิตรปัดลิตร/Treatment 6 ตำแหน่ง → ต่างกันเล็กน้อยได้
        Assert.True(Math.Abs(a.Points[1].SavingHorizon - b.Points[1].SavingHorizon) <= Math.Abs(a.Points[1].SavingHorizon) * 0.001m);
    }

    [Fact]
    public void NoTreatments_NoBreakEven_AndOriginalUntouched()
    {
        var i = Sample(); i.TreatmentsPerMonth = 0;
        var s = CcdsSensitivity.Analyze(i, -10);
        Assert.Null(s.BreakEvenLeaseTx);
        Assert.Equal(3500, i.ARecipes[0].Gallons);
    }

    // ---------- ค่าไฟถังผสมเพิ่มเป็นขั้น (ผลประหยัดไม่ได้เพิ่มตาม Treatment เสมอ) ----------
    private static CcdsInput Stepped()
    {
        var i = Sample();
        i.LeasingPrice = 0;
        i.Equipment[0].Kw = 5; i.Equipment[0].Hours = 1;
        i.MixTankLiterA = 50;
        return i;
    }

    private static CcdsResult At(CcdsInput i, decimal tx) => CcdsCalculator.Calculate(CcdsSensitivity.Scale(i, tx / i.TreatmentsPerMonth));

    /// <summary>ตรวจทุกค่า Treatment จริง: ตอนนี้คุ้ม → ตั้งแต่ BE ถึงปัจจุบันคุ้มทั้งหมด และ BE−1 ไม่คุ้ม</summary>
    private static void AssertDownward(CcdsInput i, decimal? be, Func<CcdsResult, bool> ok)
    {
        decimal tx = i.TreatmentsPerMonth;
        Assert.True(ok(At(i, tx)));
        Assert.NotNull(be);
        Assert.True(be <= tx, $"BE {be} > base {tx}");
        for (decimal t = be!.Value; t <= tx; t++) Assert.True(ok(At(i, t)), $"BE={be} but Tx {t} is not worth it");
        if (be > 0) Assert.False(ok(At(i, be.Value - 1)), $"BE={be} but Tx {be - 1} is also worth it");
    }

    [Fact]
    public void SteppedElectricity_BreakEvenHoldsForEveryTreatmentUpToBase()
    {
        var i = Stepped();
        var s = CcdsSensitivity.Analyze(i, -10, -20);
        AssertDownward(i, s.BreakEvenHorizonTx, r => r.SavingHorizon >= 0);
        AssertDownward(i, s.BreakEvenLeaseTx, r => r.SavingPerMonth >= 0);
    }

    [Fact]
    public void NotWorthItYet_FindsFirstTreatmentAboveBase()
    {
        var i = Sample(); i.LeasingPrice = 6_000_000m;
        Assert.True(CcdsCalculator.Calculate(i).SavingHorizon < 0);
        var s = CcdsSensitivity.Analyze(i);
        Assert.NotNull(s.BreakEvenHorizonTx);
        decimal be = s.BreakEvenHorizonTx!.Value;
        Assert.True(be > i.TreatmentsPerMonth);
        Assert.True(At(i, be).SavingHorizon >= 0);
        Assert.True(At(i, be - 1).SavingHorizon < 0);
    }

    [Fact]
    public void HugeVolumes_BreakEvenIsReallyBreakEven()
    {
        var i = Sample();
        i.TreatmentsPerMonth *= 400; i.GallonsB *= 400; i.LeasingPrice *= 400; i.PmPerYear *= 400;
        foreach (var r in i.ARecipes) r.Gallons *= 400;
        var s = CcdsSensitivity.Analyze(i);
        if (s.BreakEvenLeaseTx is decimal l) Assert.True(At(i, l).SavingPerMonth >= 0);
        if (s.BreakEvenHorizonTx is decimal h) Assert.True(At(i, h).SavingHorizon >= 0);
    }

    [Fact]
    public void Overflow_GivesNoBreakEven()
    {
        var i = TestData.L(Presets.FactoryDefaults());
        i.TreatmentsPerMonth = 0.01m; i.Equipment.Clear(); i.PmPerYear = 0; i.HorizonYears = 30; i.LiterPerGallonA = 1000;
        i.PowderA = new() { CostPerBox = 1_000_000, MixPerBox = 0.01m, MixLiter = 0.01m };
        i.ARecipes = new() { new() { Name = "A1", Gallons = 1_000_000, UsePowder = true, Treatments = 100_000 } };
        var s = CcdsSensitivity.Analyze(i);
        Assert.Null(s.BreakEvenLeaseTx);
        Assert.Null(s.BreakEvenHorizonTx);
    }
}
