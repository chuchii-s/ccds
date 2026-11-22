using System.IO.Compression;
using CcdsSimulator.Models;
using CcdsSimulator.Services;
using Xunit;

namespace CcdsSimulator.Tests;

public class ExcelTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "s1", Presets.Sample1() };
        yield return new object[] { "s2", Presets.Sample2() };
        var lit = Presets.Sample1();
        CcdsCalculator.SwitchMode(lit, UsageInputMode.LitersPerTreatment);
        lit.ARecipes[0].LitersPerTreatment += 0.37m;
        yield return new object[] { "liters", lit };
        var noUtil = Presets.Sample2();
        noUtil.IncludeUtility = false;
        noUtil.ARecipes[1].Treatments = 500;
        noUtil.Equipment[2].RunsPerDayOverride = 2;
        yield return new object[] { "noutil", noUtil };
        yield return new object[] { "empty", Presets.FactoryDefaults() };
        var d25 = Presets.Sample1(); d25.DaysPerMonth = 25;
        yield return new object[] { "days25", d25 };
        var t0 = Presets.Sample1(); t0.TreatmentsPerMonth = 0;
        yield return new object[] { "tx0", t0 };
        var flow = Presets.Sample1(); flow.PowderMode = PowderCalcMode.DialysateFlow; flow.PrimingMinutes = 15; flow.ARecipes[2].UsePowder = true;
        yield return new object[] { "flow", flow };
        var flowLit = Presets.Sample2(); CcdsCalculator.SwitchMode(flowLit, UsageInputMode.LitersPerTreatment);
        flowLit.PowderMode = PowderCalcMode.DialysateFlow; flowLit.DialysateFlow = 600; flowLit.TreatmentHours = 4.5m;
        yield return new object[] { "flowlit", flowLit };
        var flowBox = Presets.Sample1(); flowBox.PowderMode = PowderCalcMode.DialysateFlow; flowBox.TreatmentHours = 5; flowBox.PrimingMinutes = 30;
        flowBox.PowderA.MixPerBox = flowBox.PowderA.MixLiter = 0;
        flowBox.PowderB.MixPerBox = flowBox.PowderB.MixLiter = 0;
        yield return new object[] { "flowbox", flowBox };
        var own = Presets.Sample2(); own.PowderA.LitersPerTx = 5; own.PowderB.LitersPerTx = 4.8m;
        yield return new object[] { "own", own };
        var ownFlow = Presets.Sample1(); ownFlow.PowderMode = PowderCalcMode.DialysateFlow; ownFlow.PowderB.FlowLitersPerTx = 5; ownFlow.PowderA.LitersPerTx = 9;
        yield return new object[] { "ownflow", ownFlow };
        var ownT0 = Presets.Sample1(); ownT0.TreatmentsPerMonth = 0; ownT0.PowderA.LitersPerTx = 5; ownT0.PowderB.LitersPerTx = 6;
        yield return new object[] { "ownt0", ownT0 };
        var flowT0 = Presets.Sample2(); flowT0.TreatmentsPerMonth = 0; flowT0.PowderMode = PowderCalcMode.DialysateFlow;
        yield return new object[] { "flowt0", flowT0 };
        var lpg0 = Presets.Sample1(); CcdsCalculator.SwitchMode(lpg0, UsageInputMode.LitersPerTreatment); lpg0.LiterPerGallonA = 0; lpg0.LiterPerGallonB = 0;
        yield return new object[] { "lpg0", lpg0 };
        var h40 = Presets.Sample2(); h40.HorizonYears = 40; h40.InstallmentYears = 0;
        yield return new object[] { "h40", h40 };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Builds_valid_xlsx(string name, CcdsInput input)
    {
        var bytes = CcdsExcel.Build(input);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
        Assert.NotNull(zip.GetEntry("xl/styles.xml"));
        var dir = Environment.GetEnvironmentVariable("CCDS_XLSX_OUT");
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".xlsx"), bytes);
        }
    }
}

public class DefaultModeTests
{
    [Fact]
    public void New_site_starts_in_gallons_mode_even_if_defaults_were_liters()
    {
        var d = Presets.FactoryDefaults();
        d.UsageMode = UsageInputMode.LitersPerTreatment;
        Assert.Equal(UsageInputMode.Gallons, Presets.NewFrom(d).UsageMode);
        Assert.Equal(UsageInputMode.Gallons, Presets.Sample1(d).UsageMode);
    }
}

public class GallonsInLitersModeTests
{
    [Fact]
    public void Typing_gallons_computes_liters_per_treatment()
    {
        var i = Presets.Sample1();
        CcdsCalculator.SwitchMode(i, UsageInputMode.LitersPerTreatment);
        var a1 = i.ARecipes[0];
        Assert.True(CcdsCalculator.SetGallonsA(i, a1, 500));
        Assert.Equal(Math.Round(500 * i.LiterPerGallonA / a1.Treatments!.Value, CcdsCalculator.LiterDecimals), a1.LitersPerTreatment);
        Assert.True(Math.Abs(CcdsCalculator.Calculate(i).ARows[0].Gallons - 500) < 0.01m);
        Assert.True(CcdsCalculator.SetGallonsB(i, 2100));
        Assert.True(Math.Abs(CcdsCalculator.Calculate(i).GallonsB - 2100) < 0.01m);
    }

    [Fact]
    public void Gallons_need_treatments_first()
    {
        var i = Presets.Sample1();
        CcdsCalculator.SwitchMode(i, UsageInputMode.LitersPerTreatment);
        i.ARecipes[0].Treatments = null;
        Assert.False(CcdsCalculator.SetGallonsA(i, i.ARecipes[0], 500));
    }

    [Fact]
    public void Switching_modes_no_longer_drifts_gallons()
    {
        var i = Presets.Sample1();
        var before = CcdsCalculator.Calculate(i);
        CcdsCalculator.SwitchMode(i, UsageInputMode.LitersPerTreatment);
        var after = CcdsCalculator.Calculate(i);
        for (int k = 0; k < 4; k++) Assert.True(Math.Abs(before.ARows[k].Gallons - after.ARows[k].Gallons) < 0.5m);
        Assert.True(Math.Abs(before.GallonsB - after.GallonsB) < 0.5m);
    }
}

public class PowderLabelTests
{
    [Fact]
    public void Label_lists_powder_recipes()
    {
        var i = Presets.Sample1();                      // A2 ใช้ผง
        Assert.Equal("ผงผสมสูตร A2 & B", CcdsCalculator.PowderLabel(CcdsCalculator.Calculate(i)));
        i.ARecipes[0].UsePowder = true;                  // เพิ่ม A1
        Assert.Equal("ผงผสมสูตร A1 & A2 & B", CcdsCalculator.PowderLabel(CcdsCalculator.Calculate(i)));
    }
}

/// <summary>บั๊กที่พบจากการรีวิว — กันไม่ให้กลับมาอีก</summary>
public class ReviewRegressionTests
{
    [Fact]
    public void Mixing_runs_not_overcounted_on_exact_multiples()
    {
        var i = TestData.L(Presets.Sample1()); i.DaysPerMonth = 25;           // 12,000 ลิตร B ÷ 25 วัน ÷ ถัง 120 = 4 ครั้งพอดี
        var r = CcdsCalculator.Calculate(i);
        Assert.Equal(12000m, r.MixedLitersB);
        Assert.Equal(4m, r.EquipmentRows.First(e => e.Name == "Mixing B").RunsPerDay);
    }

    [Fact]
    public void Powder_is_not_free_when_treatments_per_month_is_zero()
    {
        var i = TestData.L(Presets.Sample1());
        var normal = CcdsCalculator.Calculate(i);
        i.TreatmentsPerMonth = 0;
        var r = CcdsCalculator.Calculate(i);
        Assert.True(Math.Abs(normal.CcdsPowderCost - r.CcdsPowderCost) < 1m);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(40, 30)]
    public void Horizon_is_clamped_1_to_30(int horizon, int expectedRows)
    {
        var i = TestData.L(Presets.Sample1()); i.HorizonYears = horizon;
        Assert.Equal(expectedRows, CcdsCalculator.Calculate(i).Years.Count);
    }

    [Fact]
    public void Zero_installment_years_still_pays_the_machine()
    {
        var i = TestData.L(Presets.Sample1()); i.InstallmentYears = 0;
        Assert.True(CcdsCalculator.Calculate(i).LeasingPerMonth > 0);
    }

    [Fact]
    public void Switch_to_liters_refused_when_treatments_missing_and_data_kept()
    {
        var i = TestData.L(Presets.Sample1()); i.TreatmentsPerMonth = 0;
        Assert.False(CcdsCalculator.SwitchMode(i, UsageInputMode.LitersPerTreatment));
        Assert.Equal(UsageInputMode.Gallons, i.UsageMode);
        Assert.Equal(450, i.ARecipes[0].Gallons);
        Assert.Null(i.ARecipes[0].Treatments);
    }

    [Fact]
    public void Toggling_modes_on_new_site_keeps_treatments_auto()
    {
        var i = Presets.NewFrom(TestData.L(Presets.FactoryDefaults()));
        Assert.True(CcdsCalculator.SwitchMode(i, UsageInputMode.LitersPerTreatment));
        Assert.True(CcdsCalculator.SwitchMode(i, UsageInputMode.Gallons));
        Assert.All(i.ARecipes, r => Assert.Null(r.Treatments));
        var s = TestData.L(Presets.Sample1());
        CcdsCalculator.SwitchMode(s, UsageInputMode.LitersPerTreatment);
        CcdsCalculator.SwitchMode(s, UsageInputMode.Gallons);
        Assert.All(s.ARecipes, r => Assert.Null(r.Treatments));    // ค่าอัตโนมัติกลับเป็นว่าง
        Assert.Equal(450, s.ARecipes[0].Gallons);
    }

    [Fact]
    public void Recipe_named_B_still_counts_its_mixed_liters()
    {
        var i = TestData.L(Presets.Sample1());
        var before = CcdsCalculator.Calculate(i).MixedLitersA;
        i.ARecipes[1].Name = "B";
        Assert.Equal(before, CcdsCalculator.Calculate(i).MixedLitersA);
    }

    [Fact]
    public void Huge_numbers_do_not_throw()
    {
        var i = TestData.L(Presets.Sample1());
        i.PricePerGallonA = 79228162514264337593543950335m; i.ARecipes[0].Gallons = 10;
        i.MixTankLiterA = 0.0000001m;
        var r = CcdsCalculator.Calculate(i);                      // ต้องไม่ล่ม
        Assert.NotNull(r);
    }

    [Fact]
    public void Excel_strips_xml_invalid_characters()
    {
        var i = TestData.L(Presets.Sample1()); i.SiteName = "Site\u000BName & <x>";
        var bytes = CcdsExcel.Build(i);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        using var st = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        System.Xml.Linq.XDocument.Load(st);                         // XML ต้องอ่านได้
    }
}
