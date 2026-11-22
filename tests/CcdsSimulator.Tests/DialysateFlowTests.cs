using CcdsSimulator.Models;
using CcdsSimulator.Services;
using Xunit;

namespace CcdsSimulator.Tests;

/// <summary>ข้อ 5 ผงผสม: คิด Treatment/กล่อง จาก Dialysate flow</summary>
public class DialysateFlowTests
{
    private static CcdsInput Flow(decimal qd = 500, decimal hours = 4, decimal priming = 0)
    {
        var i = Presets.Sample1();
        i.PowderMode = PowderCalcMode.DialysateFlow;
        i.DialysateFlow = qd; i.TreatmentHours = hours; i.PrimingMinutes = priming;
        return i;
    }

    private static void Near(decimal expected, decimal actual, decimal tol = 0.01m) =>
        Assert.True(Math.Abs(expected - actual) <= tol, $"expected {expected}, got {actual}");

    [Fact]
    public void Example_500mlmin_4h_GivesUsersNumbers()
    {
        var r = CcdsCalculator.Calculate(Flow());
        Near(120, r.DialysateLitersPerTreat);
        Near(120m / 35, r.PowderLiterPerTreatA, 0.0001m);            // 3.43 ลิตร/Tx
        Near(120m * 1.225m / 35, r.PowderLiterPerTreatB, 0.0001m);   // 4.20 ลิตร/Tx
        Near(29.17m, r.PowderA.TreatmentsPerBox);                     // 100 ลิตร/กล่อง ÷ 3.43
        Near(57.14m, r.PowderB.TreatmentsPerBox);                     // 240 ลิตร/กล่อง ÷ 4.20
        Near(1000m / (100m / (120m / 35)), r.PowderA.CostPerTreat);
    }

    [Fact]
    public void FlowMode_FillsMixPerBoxAndLitersPerMixAutomatically()
    {
        var r = CcdsCalculator.Calculate(Flow());
        Near(120m / 35, r.PowderA.MixLiter, 0.0001m);          // ลิตร/ครั้ง = ลิตร/Tx
        Near(29.17m, r.PowderA.MixPerBox);                      // ผสมได้ = 100 ลิตร/กล่อง ÷ 3.43
        Near(57.14m, r.PowderB.MixPerBox);
        Assert.Equal(r.PowderA.TreatmentsPerBox, r.PowderA.MixPerBox);
    }

    [Fact]
    public void FlowMode_UsesFixedBoxLiters_EvenWhenMixFieldsAreZero()
    {
        // ภาพของผู้ใช้: 500 ml/min, 4 ชม., Priming 30 นาที; A 1 กล่อง = 100 L, B 1 กล่อง = 240 L (คงที่)
        var i = Flow(500, 4, 30);
        i.PowderA.MixPerBox = i.PowderA.MixLiter = 0;
        i.PowderB.MixPerBox = i.PowderB.MixLiter = 0;
        var r = CcdsCalculator.Calculate(i);
        Near(135, r.DialysateLitersPerTreat);
        Assert.Equal(100, r.PowderA.LiterPerBox);
        Assert.Equal(240, r.PowderB.LiterPerBox);
        Near(3.86m, r.PowderA.MixLiter);
        Near(4.72m, r.PowderB.MixLiter);
        Near(25.93m, r.PowderA.MixPerBox);
        Near(50.79m, r.PowderB.MixPerBox);
        Near(38.57m, r.PowderA.CostPerTreat);
        Near(21.66m, r.PowderB.CostPerTreat);
        Near(r.MixedLitersB / 240 * i.PowderB.CostPerBox, r.CcdsRows[^1].CostPerMonth);
    }

    [Fact]
    public void LitersMode_AlsoUsesFixedBoxLiters_AndAutoMixPerBox()
    {
        // โหมดจำนวนลิตร: ลิตร / Tx จากข้อ 2, ผสมแล้ว=แกลลอน = 100 (หรือ 240) ÷ ลิตร / Tx — ช่องผสมได้/ลิตรต่อครั้งเดิมไม่มีผล
        var i = TestData.L(Presets.Sample1());
        i.PowderA.MixPerBox = 0; i.PowderA.MixLiter = 5.37m;
        i.PowderB.MixPerBox = 0; i.PowderB.MixLiter = 4.14m;
        var r = CcdsCalculator.Calculate(i);
        Assert.Equal(100, r.PowderA.LiterPerBox);
        Assert.Equal(240, r.PowderB.LiterPerBox);
        Assert.Equal(r.LiterPerTreatA, r.PowderA.MixLiter);
        Near(100 / r.LiterPerTreatA, r.PowderA.MixPerBox);
        Near(240 / r.LiterPerTreatB, r.PowderB.MixPerBox);
        Assert.True(r.CcdsPowderCost > 0);
    }

    [Fact]
    public void Override_LitersPerTx_DrivesMixedGallonsAndCost()
    {
        var i = TestData.L(Presets.Sample1());
        i.PowderA.LitersPerTx = 5;                        // กรอก ลิตร / Tx เอง
        i.PowderB.LitersPerTx = 240m / 50;                // กรอก ผสมแล้ว=แกลลอน = 50 → ลิตร / Tx = 240 ÷ 50
        var r = CcdsCalculator.Calculate(i);
        Assert.Equal(20, r.PowderA.MixPerBox);            // 100 ÷ 5 (สูตรเดิม)
        Assert.Equal(50, r.PowderB.MixPerBox);
        Assert.Equal(50, r.PowderA.CostPerTreat);         // 1000 ÷ 20
        Near(r.Treatments * 4.8m, r.CcdsRows[^1].MixedLiters);
        Near(r.Treatments * r.PowderB.CostPerTreat, r.CcdsRows[^1].CostPerMonth);
        foreach (var row in r.CcdsRows.Take(i.ARecipes.Count).Where(x => x.IsPowder))
            Near(row.Treatments * 5, row.MixedLiters);
    }

    [Fact]
    public void Override_InFlowMode_AndZeroMeansAuto()
    {
        var i = Flow();
        CcdsCalculator.SetOwnLitersPerTx(i, i.PowderA, 4);
        Assert.Equal(4, i.PowderA.FlowLitersPerTx);
        Assert.Null(i.PowderA.LitersPerTx);
        Assert.Equal(25, CcdsCalculator.Calculate(i).PowderA.MixPerBox);
        CcdsCalculator.SetOwnLitersPerTx(i, i.PowderA, 0);
        Assert.Null(i.PowderA.FlowLitersPerTx);
        Near(29.17m, CcdsCalculator.Calculate(i).PowderA.MixPerBox);
        i.PowderA.FlowLitersPerTx = -3;
        CcdsCalculator.Sanitize(i);
        Assert.Null(i.PowderA.FlowLitersPerTx);
    }

    [Fact]
    public void Override_EachModeKeepsItsOwnValue()
    {
        var i = TestData.L(Presets.Sample1()); i.PrimingMinutes = 0;
        CcdsCalculator.SetOwnLitersPerTx(i, i.PowderA, 5);       // โหมดลิตร: A = 5
        i.PowderMode = PowderCalcMode.DialysateFlow;
        Near(29.17m, CcdsCalculator.Calculate(i).PowderA.MixPerBox);   // โหมด flow ยังอัตโนมัติ
        CcdsCalculator.SetOwnLitersPerTx(i, i.PowderA, 4);       // โหมด flow: A = 4
        Assert.Equal(25, CcdsCalculator.Calculate(i).PowderA.MixPerBox);
        i.PowderMode = PowderCalcMode.Liters;
        Assert.Equal(20, CcdsCalculator.Calculate(i).PowderA.MixPerBox);  // กลับโหมดลิตร ค่า 5 ยังอยู่
        i.PowderMode = PowderCalcMode.DialysateFlow;
        Assert.Equal(25, CcdsCalculator.Calculate(i).PowderA.MixPerBox);  // กลับโหมด flow ค่า 4 ยังอยู่
    }

    [Fact]
    public void Priming_AddsDialysateAtSameFlow()
    {
        var r = CcdsCalculator.Calculate(Flow(priming: 15));
        Near(127.5m, r.DialysateLitersPerTreat);                      // 500 × (240 + 15) ÷ 1000
        Near(100m / (127.5m / 35), r.PowderA.TreatmentsPerBox);
    }

    [Fact]
    public void MixedLitersAndPowderCost_FollowTreatments()
    {
        var i = Flow();
        var r = CcdsCalculator.Calculate(i);
        var b = r.CcdsRows[^1];
        Near(r.Treatments * r.PowderLiterPerTreatB, b.MixedLiters);
        Near(r.Treatments * r.PowderB.CostPerTreat, b.CostPerMonth);
        foreach (var (row, k) in r.CcdsRows.Take(i.ARecipes.Count).Select((x, k) => (x, k)).Where(x => x.x.IsPowder))
        {
            Near(row.Treatments * r.PowderLiterPerTreatA, row.MixedLiters);
            Near(row.Treatments * r.PowderA.CostPerTreat, row.CostPerMonth);
        }
        Near(r.MixedLitersA + r.MixedLitersB, r.MixedLitersTotal);
    }

    [Fact]
    public void LitersMode_Unchanged()
    {
        var i = TestData.L(Presets.Sample1());
        var r = CcdsCalculator.Calculate(i);
        Assert.Equal(r.LiterPerTreatA, r.PowderLiterPerTreatA);
        Assert.Equal(r.LiterPerTreatB, r.PowderLiterPerTreatB);
        // ค่าใน Dialysate flow ไม่มีผลในโหมดลิตร
        i.DialysateFlow = 800; i.PrimingMinutes = 30;
        Assert.Equal(r.CcdsPowderCost, CcdsCalculator.Calculate(i).CcdsPowderCost);
    }

    [Fact]
    public void ZeroValues_DoNotThrow()
    {
        var i = Flow(0, 0, 0); i.MixRatioTotal = 0;
        var r = CcdsCalculator.Calculate(i);
        Assert.Equal(0, r.PowderA.TreatmentsPerBox);
        Assert.Equal(0, r.CcdsPowderCost);
    }

    [Fact]
    public void Sanitize_ClampsAndFixesMode()
    {
        var i = Flow(-5, 99, 9999);
        i.PowderMode = (PowderCalcMode)42;
        CcdsCalculator.Sanitize(i);
        Assert.Equal(PowderCalcMode.Liters, i.PowderMode);
        Assert.Equal(0, i.DialysateFlow);
        Assert.Equal(24, i.TreatmentHours);
        Assert.Equal(600, i.PrimingMinutes);
    }

    [Fact]
    public void OldSavedData_WithoutMode_DefaultsToDialysateFlow()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Presets.Sample1())
            .Replace("\"PowderMode\":1,", "").Replace("\"DialysateFlow\":500,", "").Replace("\"PrimingMinutes\":30,", "");
        Assert.DoesNotContain("PowderMode", json);
        Assert.DoesNotContain("DialysateFlow", json);
        var i = System.Text.Json.JsonSerializer.Deserialize<CcdsInput>(json)!;
        Assert.Equal(PowderCalcMode.DialysateFlow, i.PowderMode);
        Assert.Equal(500, i.DialysateFlow);
        Assert.Equal(30, i.PrimingMinutes);
    }

    [Fact]
    public void NewSite_DefaultsToDialysateFlow_500_4h_Priming30()
    {
        var i = Presets.NewFrom(Presets.FactoryDefaults());
        Assert.Equal(PowderCalcMode.DialysateFlow, i.PowderMode);
        Assert.Equal(500, i.DialysateFlow); Assert.Equal(4, i.TreatmentHours); Assert.Equal(30, i.PrimingMinutes);
        Assert.Equal(1.225m, i.MixRatioB); Assert.Equal(35, i.MixRatioTotal);
    }

    [Fact]
    public void Sensitivity_WorksInFlowMode()
    {
        var i = Flow(priming: 10);
        var s = CcdsSensitivity.Analyze(i, -10, -20);
        Assert.Equal(CcdsCalculator.Calculate(i).SavingPerMonth, s.Points[0].SavingPerMonth);
        // ลด Treatment 10% → ลิตรที่ผสมลด 10% (โหมด flow ขึ้นกับ Treatment โดยตรง)
        var r90 = CcdsCalculator.Calculate(CcdsSensitivity.Scale(i, 0.9m));
        Near(CcdsCalculator.Calculate(i).MixedLitersTotal * 0.9m, r90.MixedLitersTotal, 0.5m);
    }
}

public class ReviewFixTests
{
    [Fact]
    public void OwnLitersPerTx_WithZeroTreatments_StillChargesPowderFromGallons()
    {
        var i = TestData.L(Presets.Sample1()); i.TreatmentsPerMonth = 0;
        var auto = CcdsCalculator.Calculate(i).CcdsPowderCost;
        Assert.True(auto > 0);
        i.PowderA.LitersPerTx = 5; i.PowderB.LitersPerTx = 6;
        Assert.Equal(auto, CcdsCalculator.Calculate(i).CcdsPowderCost);   // ไม่กลายเป็น 0
        i.PowderMode = PowderCalcMode.DialysateFlow;
        Assert.Equal(auto, CcdsCalculator.Calculate(i).CcdsPowderCost);   // โหมด flow ก็เช่นกัน
    }

    [Fact]
    public void Dialysate_RatioAPlusBOverTotal_IsFlagged()
    {
        Assert.True(DialysateCalculator.Calculate(new DialysateCalcInput { RatioA = 20, RatioB = 20, RatioTotal = 35 }).RatioInvalid);
        Assert.False(DialysateCalculator.Calculate(DialysateCalculator.Defaults()).RatioInvalid);
    }
}
