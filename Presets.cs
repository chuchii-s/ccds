using CcdsSimulator.Models;

namespace CcdsSimulator.Services;

/// <summary>
/// ค่าตั้งต้นและข้อมูลตัวอย่าง (ตัวเลขสมมติ ไม่ใช่ข้อมูลลูกค้าจริง)
/// ผู้ใช้แต่ละคนตั้งค่าตั้งต้นของตัวเองได้ที่หน้า "ตั้งค่า" (เก็บในเบราว์เซอร์)
/// </summary>
public static class Presets
{
    /// <summary>ค่าตั้งต้นจากโรงงาน สำหรับโครงการใหม่</summary>
    public static CcdsInput FactoryDefaults() => new()
    {
        ARecipes =
        [
            new() { Name = "A1" },
            new() { Name = "A2", UsePowder = true },
            new() { Name = "A3" },
            new() { Name = "A6" },
        ],
        PricePerGallonA = 60,
        PricePerGallonB = 55,
        PowderA = new() { CostPerBox = 1000, MixPerBox = 20, MixLiter = 5 },
        PowderB = new() { CostPerBox = 1100, MixPerBox = 40, MixLiter = 6 },
        Equipment =
        [
            new() { Name = "Mixing A", Kind = EquipmentKind.MixingA, Kw = 0.25m, Hours = 0.5m, Qty = 2 },
            new() { Name = "Mixing B", Kind = EquipmentKind.MixingB, Kw = 0.25m, Hours = 0.5m, Qty = 2 },
            new() { Name = "Cir. Pump A", Kind = EquipmentKind.Continuous, Kw = 0.33m, Hours = 18m, Qty = 1 },
            new() { Name = "Cir. Pump B", Kind = EquipmentKind.Continuous, Kw = 0.33m, Hours = 18m, Qty = 1 },
        ],
    };

    /// <summary>ตัวอย่าง 1: หน่วยไตเทียมขนาดกลาง (ตัวเลขสมมติ)</summary>
    public static CcdsInput Sample1(CcdsInput? defaults = null) => Sample(defaults,
        "ตัวอย่าง หน่วยไตเทียม 28 เครื่อง", "ตัวอย่าง 1", 28, 2900, 450, 1200, 150, 170, 2000, 2_600_000);

    /// <summary>ตัวอย่าง 2: หน่วยไตเทียมขนาดใหญ่ (ตัวเลขสมมติ)</summary>
    public static CcdsInput Sample2(CcdsInput? defaults = null) => Sample(defaults,
        "ตัวอย่าง หน่วยไตเทียม 36 เครื่อง", "ตัวอย่าง 2", 36, 3000, 700, 1100, 150, 130, 2200, 2_700_000);

    /// <summary>โครงการใหม่ว่าง ๆ จากค่าตั้งต้น</summary>
    public static CcdsInput NewFrom(CcdsInput defaults)
    {
        var x = Clone(defaults);
        x.SiteName = x.ModelName = x.Month = "";
        x.Machines = 0;
        x.TreatmentsPerMonth = 0;
        foreach (var r in x.ARecipes) r.Gallons = 0;
        x.GallonsB = 0;
        x.LeasingPrice = 0;
        return x;
    }

    public static CcdsInput Clone(CcdsInput i) =>
        System.Text.Json.JsonSerializer.Deserialize<CcdsInput>(System.Text.Json.JsonSerializer.Serialize(i))!;

    private static CcdsInput Sample(CcdsInput? defaults, string site, string model, int machines, decimal t,
        decimal a1, decimal a2, decimal a3, decimal a6, decimal b, decimal leasing)
    {
        var x = Clone(defaults ?? FactoryDefaults());
        x.SiteName = site;
        x.ModelName = model;
        x.Month = DateTime.Today.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture);
        x.Machines = machines;
        x.TreatmentsPerMonth = t;
        x.ARecipes =
        [
            new() { Name = "A1", Gallons = a1 },
            new() { Name = "A2", Gallons = a2, UsePowder = true },
            new() { Name = "A3", Gallons = a3 },
            new() { Name = "A6", Gallons = a6 },
        ];
        x.GallonsB = b;
        x.LeasingPrice = leasing;
        return x;
    }
}
