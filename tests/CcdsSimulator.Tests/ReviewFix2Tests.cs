using CcdsSimulator.Models;
using CcdsSimulator.Services;
using Xunit;

namespace CcdsSimulator.Tests;

public class ReviewFix2Tests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PowderRecipe_WithZeroTreatments_IsTreatedAsAuto_NotFreePowder(decimal tx)
    {
        foreach (var mode in new[] { PowderCalcMode.DialysateFlow, PowderCalcMode.Liters })
        {
            var i = Presets.Sample1(); i.PowderMode = mode;
            var auto = CcdsCalculator.Calculate(i);
            i.ARecipes[1].Treatments = tx;      // A2 (ผง) มี 1,200 GL แต่กรอก Treatment = 0 / ติดลบ
            var r = CcdsCalculator.Calculate(i);
            Assert.Equal(auto.CcdsPowderCost, r.CcdsPowderCost);
            Assert.Equal(auto.SavingPerMonth, r.SavingPerMonth);
        }
    }

    [Fact]
    public void LitersUsageMode_ZeroTreatmentsStaysZero()
    {
        var i = TestData.L(Presets.Sample1());
        CcdsCalculator.SwitchMode(i, UsageInputMode.LitersPerTreatment);
        i.ARecipes[0].Treatments = 0;
        CcdsCalculator.Sanitize(i);
        Assert.Equal(0, i.ARecipes[0].Treatments);   // โหมดลิตร: 0 = ไม่ได้ใช้สูตรนี้ (ไม่เปลี่ยนเป็นอัตโนมัติ)
    }

    [Fact]
    public void ExcelBuilds_WhenGallonSizeIsZero()
    {
        var i = Presets.Sample1(); CcdsCalculator.SwitchMode(i, UsageInputMode.LitersPerTreatment);
        i.LiterPerGallonA = 0; i.LiterPerGallonB = 0;
        Assert.NotEmpty(CcdsExcel.Build(i));
    }
}
