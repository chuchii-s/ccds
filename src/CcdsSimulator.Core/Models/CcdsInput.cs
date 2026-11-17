namespace CcdsSimulator.Models;

/// <summary>ข้อมูลน้ำยาสูตร A แต่ละตัว (A1, A2, A3, A6 …)</summary>
public class ARecipeInput
{
    public string Name { get; set; } = "";
    /// <summary>จำนวนแกลลอนที่ใช้ต่อเดือน</summary>
    public decimal Gallons { get; set; }
    /// <summary>true = เปลี่ยนไปใช้ผงผสม (CCDS), false = ยังซื้อเป็นแกลลอน</summary>
    public bool UsePowder { get; set; }
    /// <summary>
    /// จำนวน Treatment/เดือนของสูตรนี้ (กรอกเอง)
    /// null = คำนวณอัตโนมัติ: แกลลอนสูตรนี้ ÷ แกลลอน A รวม × Treatment/เดือน
    /// </summary>
    public decimal? Treatments { get; set; }
    /// <summary>ปริมาณน้ำยาที่ใช้จริง ลิตร/Treatment (ใช้เมื่อ UsageMode = LitersPerTreatment)</summary>
    public decimal LitersPerTreatment { get; set; }
}

/// <summary>วิธีกรอกปริมาณน้ำยาในข้อ 2</summary>
public enum UsageInputMode
{
    /// <summary>กรอกจำนวนแกลลอน/เดือน</summary>
    Gallons = 0,
    /// <summary>กรอกปริมาณที่ใช้จริง ลิตร/Treatment → แกลลอน = ลิตร/Tx × Treatment ÷ ลิตรต่อแกลลอน</summary>
    LitersPerTreatment = 1
}

/// <summary>ข้อมูลผงผสม A หรือ B</summary>
public class PowderInput
{
    public decimal CostPerBox { get; set; }
    /// <summary>จำนวนครั้งที่ผสมได้ต่อกล่อง (Treat/Box ในไฟล์ Excel)</summary>
    public decimal MixPerBox { get; set; }
    /// <summary>ลิตรต่อการผสม 1 ครั้ง (Mix. Liter)</summary>
    public decimal MixLiter { get; set; }
}

public enum EquipmentKind
{
    /// <summary>ถังผสม A — จำนวนครั้ง/วัน คำนวณจากปริมาณน้ำยา A ที่ผสม</summary>
    MixingA,
    /// <summary>ถังผสม B — จำนวนครั้ง/วัน คำนวณจากปริมาณน้ำยา B ที่ผสม</summary>
    MixingB,
    /// <summary>ทำงานต่อเนื่อง (เช่น ปั๊มหมุนเวียน) 1 รอบ/วัน ตามจำนวนชั่วโมง</summary>
    Continuous
}

public class EquipmentInput
{
    public string Name { get; set; } = "";
    public EquipmentKind Kind { get; set; }
    public decimal Kw { get; set; }
    /// <summary>ชั่วโมงต่อรอบ (Mixing) หรือ ชั่วโมงทำงานต่อวัน (Continuous)</summary>
    public decimal Hours { get; set; }
    /// <summary>จำนวนมอเตอร์/ตัว</summary>
    public decimal Qty { get; set; } = 1;
    /// <summary>กำหนดจำนวนครั้ง/วันเอง (ว่าง = คำนวณอัตโนมัติ)</summary>
    public int? RunsPerDayOverride { get; set; }
}

public class CcdsInput
{
    // ---------- ข้อมูลทั่วไป ----------
    public string SiteName { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string Month { get; set; } = "";
    public int Machines { get; set; }
    public decimal TreatmentsPerMonth { get; set; }

    // ---------- ปริมาณน้ำยา ----------
    public UsageInputMode UsageMode { get; set; } = UsageInputMode.Gallons;
    public List<ARecipeInput> ARecipes { get; set; } = new();
    /// <summary>แกลลอน B/เดือน (โหมด Gallons)</summary>
    public decimal GallonsB { get; set; }
    /// <summary>ลิตร B/Treatment (โหมด LitersPerTreatment)</summary>
    public decimal LitersPerTreatmentB { get; set; }

    // ---------- แบบ GL (ปัจจุบัน) ----------
    public decimal LiterPerGallonA { get; set; } = 5.5m;
    public decimal LiterPerGallonB { get; set; } = 6m;
    public decimal PricePerGallonA { get; set; } = 60m;
    public decimal PricePerGallonB { get; set; } = 55m;
    /// <summary>ขายแกลลอนเปล่าคืน (บาท/แกลลอน)</summary>
    public decimal GallonSalePrice { get; set; } = 4m;

    // ---------- แบบ CCDS ----------
    /// <summary>ราคา/แกลลอน ของสูตร A ที่ยังซื้อเป็นแกลลอน เมื่อใช้ระบบ CCDS</summary>
    public decimal CcdsPricePerGallonA { get; set; } = 50m;

    public decimal LeasingPrice { get; set; }
    /// <summary>ดอกเบี้ยต่อปี (Flat rate) หน่วย %</summary>
    public decimal InterestRatePercent { get; set; } = 3.55m;   // คู่กับผ่อน 5 ปี (LeasingRates)
    public int InstallmentYears { get; set; } = 5;

    public PowderInput PowderA { get; set; } = new() { CostPerBox = 1000, MixPerBox = 20, MixLiter = 5 };
    public PowderInput PowderB { get; set; } = new() { CostPerBox = 1100, MixPerBox = 40, MixLiter = 6 };

    // ---------- Utility ----------
    /// <summary>true = นำค่าน้ำ + ค่าไฟ มาคิดในต้นทุนแบบ CCDS, false = แสดงอย่างเดียว ไม่นำมาคิด</summary>
    public bool IncludeUtility { get; set; } = true;
    public decimal WaterRate { get; set; } = 30m;            // บาท/m3
    /// <summary>ปริมาณน้ำ RO ที่ใช้ (m3) ต่อน้ำยาผสม 1,000 ลิตร</summary>
    public decimal WaterM3Per1000Liter { get; set; } = 2m;
    public decimal ElectricRate { get; set; } = 4.9m;        // บาท/kWh
    public int DaysPerMonth { get; set; } = 30;
    public decimal MixTankLiterA { get; set; } = 100m;       // ลิตร/ครั้งผสม A
    public decimal MixTankLiterB { get; set; } = 120m;       // ลิตร/ครั้งผสม B
    public List<EquipmentInput> Equipment { get; set; } = new();

    // ---------- ระยะยาว ----------
    public decimal PmPerYear { get; set; } = 160_000m;
    public int WarrantyYears { get; set; } = 4;
    public int HorizonYears { get; set; } = 10;
}
