using CcdsSimulator.Models;
using CcdsSimulator.Services;
using Xunit;

namespace CcdsSimulator.Tests;

/// <summary>เทียบกับค่าที่ Excel คำนวณไว้ในไฟล์ dialysate_calculator.xlsx (ค่าตั้งต้น)</summary>
public class DialysateCalculatorTests
{
    private static void Near(decimal expected, decimal actual, decimal tol = 0.000001m) =>
        Assert.True(Math.Abs(expected - actual) <= tol, $"expected {expected}, got {actual}");

    [Fact]
    public void Defaults_MatchExcelFile()
    {
        var r = DialysateCalculator.Calculate(DialysateCalculator.Defaults());
        Assert.Equal(263, r.TotalMinutes);                       // B15
        Assert.Equal(131.5m, r.DialysateLiters);                 // B16
        Assert.Equal(32.775m, r.RatioRo);                        // B17
        Near(3.75714285714286m, r.LitersA, 1e-12m);              // B20
        Assert.Equal(4.6025m, r.LitersB);                        // B21
        Near(123.140357142857m, r.LitersRo, 1e-11m);             // B22
        Near(131.5m, r.SumCheck); Near(0, r.Difference);         // B23, B24
        Near(0.626190476190476m, r.GallonsA, 1e-12m);            // D28
        Near(0.767083333333333m, r.GallonsB, 1e-12m);            // D29
        Near(34.4404761904762m, r.CostA, 1e-11m);                // F28
        Near(34.51875m, r.CostB);                                // F29
        Near(68.9592261904762m, r.CostPerCase, 1e-11m);          // F30
        Assert.Equal(260, r.CasesPerMonth);                      // B36
        Near(37.5714285714286m, r.A.PerDay, 1e-11m);             // C39
        Near(976.857142857143m, r.A.PerMonth, 1e-10m);           // D39
        Near(1196.65m, r.B.PerMonth);                            // D40
        Near(32016.4928571429m, r.Ro.PerMonth, 1e-9m);           // D41
        Assert.Equal(34190m, r.Dialysate.PerMonth);              // D42
        Near(17929.3988095238m, r.Cost.PerMonth, 1e-9m);         // D43
        Assert.Equal(7, r.GallonsALine.PerDay);                  // C44
        Assert.Equal(163, r.GallonsALine.PerMonth);              // D44
        Assert.Equal(8, r.GallonsBLine.PerDay);                  // C45
        Assert.Equal(200, r.GallonsBLine.PerMonth);              // D45
        Near(20m / 263, r.BypassShare);                          // หมายเหตุข้อ 4: 7.6%
        Near(23m / 263, r.BypassShareWithFill);                  // 8.7%
    }

    [Fact]
    public void CasesPerMonth_CanBeEnteredDirectly()
    {
        var i = new DialysateCalcInput { CasesPerDay = 78, DaysPerMonth = 26 };
        Assert.Equal(2028, DialysateCalculator.Calculate(i).CasesPerMonth);       // อัตโนมัติ 78 × 26
        i.CasesPerMonth = 2000;                                                    // กรอกเอง
        var r = DialysateCalculator.Calculate(i);
        Assert.Equal(2000, r.CasesPerMonth);
        Near(r.LitersA * 2000, r.A.PerMonth);
        Near(r.CostPerCase * 2000, r.Cost.PerMonth);
        Assert.Equal(Math.Ceiling(Math.Round(r.LitersA * 2000 / 6, 9)), r.GallonsALine.PerMonth);
        Near(r.LitersA * 78, r.A.PerDay);                                          // ต่อวันยังใช้ เคส/วัน
        i.CasesPerMonth = 0;                                                       // 0 = กลับเป็นอัตโนมัติ
        Assert.Equal(2028, DialysateCalculator.Calculate(i).CasesPerMonth);
    }

    [Fact]
    public void ExactMultiple_DoesNotRoundUpExtraGallon()
    {
        var i = new DialysateCalcInput { CasesPerDay = 1, DaysPerMonth = 1, PackLitersB = 4.6025m };
        var r = DialysateCalculator.Calculate(i);
        Assert.Equal(1, r.GallonsBLine.PerDay);
    }

    [Fact]
    public void ZerosAndNegatives_DoNotThrow()
    {
        var r = DialysateCalculator.Calculate(new DialysateCalcInput
        {
            RatioTotal = 0, PackLitersA = 0, PackLitersB = -1, FlowRate = -5, CasesPerDay = -3,
            TreatmentHours = 0, PrimingMinutes = 0, RecirculationMinutes = 0, FillMinutes = 0,
        });
        Assert.Equal(0, r.LitersA);
        Assert.Equal(0, r.GallonsALine.PerMonth);
        Assert.Equal(0, r.BypassShare);
    }
}

public class DialysateExcelTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "dc_default", DialysateCalculator.Defaults() };
        yield return new object[] { "dc_own", new DialysateCalcInput { CasesPerDay = 78, CasesPerMonth = 2000, FlowRate = 600, TreatmentHours = 4.5m, PackLitersB = 0 } };
        yield return new object[] { "dc_zero", new DialysateCalcInput { RatioTotal = 0, TreatmentHours = 0, PrimingMinutes = 0, RecirculationMinutes = 0, FillMinutes = 0 } };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Builds_valid_xlsx(string name, DialysateCalcInput input)
    {
        var bytes = DialysateExcel.Build(input);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
        var dir = Environment.GetEnvironmentVariable("CCDS_XLSX_OUT");
        if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir, name + ".xlsx"), bytes); }
    }
}
