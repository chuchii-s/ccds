using CcdsSimulator.Models;
using CcdsSimulator.Services.Xlsx;

namespace CcdsSimulator.Services;

/// <summary>
/// สร้างไฟล์ Excel (.xlsx) ชีตเดียว จัดวางแบบไฟล์ Excel ต้นฉบับ (CCDS Simulate)
/// ช่องสีฟ้า = ข้อมูลที่กรอก/แก้ได้, ช่องอื่นเป็นสูตร — แก้ตัวเลขในไฟล์แล้วผลคำนวณเปลี่ยนตามทันที
/// ทุกสูตรมีค่าที่คำนวณไว้แล้ว (จาก CcdsCalculator) เพื่อให้แสดงผลได้แม้เปิดในโปรแกรมที่ไม่คำนวณสูตร
/// </summary>
public static class CcdsExcel
{
    public const string Powder = "ผง", Gallon = "แกลลอน", Yes = "คิด", No = "ไม่คิด";
    /// <summary>ค่าในช่อง "คิดราคา/Tx จาก" (ข้อความอื่น = คิดจากลิตร)</summary>
    public const string ModeLiters = "ลิตร", ModeFlow = "Dialysate flow";

    // ---------- สไตล์ ----------
    private static readonly Style Title = new(Bold: true, Size: 14, Underline: true, Color: "1E3A5F");
    private static readonly Style Sec = new(Bold: true, Size: 11, Color: "FFFFFF", Fill: Fill.Section);
    private static readonly Style Lbl = new();
    private static readonly Style LblB = new(Bold: true);
    private static readonly Style Note = new(Italic: true, Size: 9, Color: "66748A");
    private static readonly Style Hd = new(Bold: true, Fill: Fill.Header, Border: Bd.Thin, Align: Al.Center, Wrap: true);
    private static readonly Style TxtB = new(Border: Bd.Thin);
    private static readonly Style TxtC = new(Border: Bd.Thin, Align: Al.Center);
    private static readonly Style InTxt = new(Border: Bd.Thin, Fill: Fill.Input);
    private static readonly Style InTxtC = new(Border: Bd.Thin, Fill: Fill.Input, Align: Al.Center);
    private static readonly Style In0 = new(Border: Bd.Thin, Fill: Fill.Input, Num: Num.N0);
    private static readonly Style In2 = new(Border: Bd.Thin, Fill: Fill.Input, Num: Num.N2);
    private static readonly Style F0 = new(Border: Bd.Thin, Num: Num.N0);
    private static readonly Style F2 = new(Border: Bd.Thin, Num: Num.N2);
    private static readonly Style F0c = new(Border: Bd.Thin, Num: Num.N0, Align: Al.Center);
    private static readonly Style Tot = new(Bold: true, Border: Bd.Thick, Fill: Fill.Total);
    private static readonly Style Tot0 = new(Bold: true, Border: Bd.Thick, Fill: Fill.Total, Num: Num.N0);
    private static readonly Style Tot2 = new(Bold: true, Border: Bd.Thick, Fill: Fill.Total, Num: Num.N2);
    private static readonly Style Big0 = new(Bold: true, Size: 12, Border: Bd.Thick, Fill: Fill.Total, Num: Num.N0);

    public static byte[] Build(CcdsInput input)
    {
        input = CcdsCalculator.Sanitized(input);          // ค่าเดียวกับที่ใช้คำนวณ (ตัดช่วงค่าแล้ว)
        var r = CcdsCalculator.Calculate(input);
        var i = CcdsCalculator.ToGallons(input);     // ค่าแกลลอน (ใช้เป็นค่าที่คำนวณไว้)
        bool liters = input.UsageMode == UsageInputMode.LitersPerTreatment;

        var wb = new Workbook();
        var s = new Sheet("CCDS");
        wb.Sheets.Add(s);
        void Name(string n, int row, int col) => wb.Names.Add((n, $"'{s.Name}'!{Sheet.Abs(row, col)}"));
        static string R(int row, int col) => Sheet.Ref(row, col);

        // ความกว้างคอลัมน์: ซ้าย A–K, เว้น L, ขวา M–S
        double[] w = { 27, 11, 11, 10, 11, 10, 10, 10, 10, 10, 12, 3, 31, 12, 12, 11, 12, 20, 12 };
        for (int c = 0; c < w.Length; c++) s.Widths[c + 1] = w[c];

        // ================= หัวเรื่อง & ข้อมูลทั่วไป =================
        var model = string.IsNullOrWhiteSpace(input.ModelName) ? "" : $" ({input.ModelName} Model)";
        s.Set(1, 1, "Feasibility study for CCDS" + model, Title);
        s.Heights[1] = 22;
        s.Set(2, 1, "ชื่อหน่วยไตเทียม / ลูกค้า", Lbl);
        s.Set(2, 2, input.SiteName, InTxt); s.Merge(2, 2, 2, 6);
        s.Set(2, 7, "ข้อมูลเดือน", Lbl);
        s.Set(2, 8, input.Month, InTxtC);
        s.Set(3, 1, "จำนวนเครื่อง", Lbl);
        s.Set(3, 2, input.Machines, In0); Name("Machines", 3, 2);
        s.Set(3, 3, "เครื่อง", Lbl);
        s.Set(3, 4, "ปริมาณน้ำยาที่ใช้ต่อเดือน", Lbl);
        s.Set(3, 7, input.TreatmentsPerMonth, In0); Name("TxMonth", 3, 7);
        s.Set(3, 8, "Treatment", Lbl);
        s.Set(4, 1, "ช่องสีฟ้า = กรอก/แก้ไขได้ • ช่องอื่นเป็นสูตรคำนวณอัตโนมัติ • ตัวเลขติดลบแสดงเป็นสีเบอร์กันดี", Note);

        // ================= 1) แบบแกลลอน (GL) =================
        int row = 6;
        s.Set(row, 1, "1) ค่าใช้จ่ายแบบแกลลอน (GL) — ปัจจุบัน", Sec); s.Merge(row, 1, row, 11);
        row++;
        string[] hd = { "Recipe", "GL / เดือน", "Treatment", "ลิตร / Treat", "เมื่อใช้ CCDS", "Total GL", "Average GL/Treat", "Liter / Treat", "Price / GL", "Cost / Treat", "Cost / MN" };
        for (int c = 0; c < hd.Length; c++) s.Set(row, c + 1, hd[c], Hd);
        s.Heights[row] = 30;

        // ค่าตั้งต้นที่สูตรด้านบนอ้างถึง (อยู่ใต้ตาราง) — กำหนดแถวไว้ก่อน
        int nA = input.ARecipes.Count;
        int a1 = row + 1, aN = row + nA, rB = row + nA + 1, rTot = rB + 1;
        int rSale = rTot + 2, rGlNet = rSale + 1;
        int rPar = rGlNet + 2;      // หัวข้อ "ค่าตั้งต้น"
        int rLpgA = rPar + 1, rLpgB = rPar + 2, rCcdsPrice = rPar + 3;
        Name("LpG_A", rLpgA, 2); Name("LpG_B", rLpgB, 2); Name("CcdsPriceA", rCcdsPrice, 2);
        Name("SalePrice", rSale, 2);

        string totalA = nA > 0 ? $"{Sheet.Abs(a1, 2)}:{Sheet.Abs(aN, 2)}" : "";
        string sumA = nA > 0 ? $"SUM({totalA})" : "0";

        for (int k = 0; k < nA; k++)
        {
            int rr = a1 + k;
            var src = input.ARecipes[k];
            var g = r.ARows[k];
            s.Set(rr, 1, src.Name, InTxtC);
            if (liters)
            {
                s.Set(rr, 2, i.ARecipes[k].Gallons, F0, $"{R(rr, 4)}*{R(rr, 3)}/LpG_A");
                s.Set(rr, 3, src.Treatments ?? 0, In0);
                s.Set(rr, 4, src.LitersPerTreatment, In2);
            }
            else
            {
                s.Set(rr, 2, src.Gallons, In0);
                if (src.Treatments is decimal tx) s.Set(rr, 3, tx, In0);
                else s.Set(rr, 3, g.Treatments, F0, $"IF({sumA}=0,0,{R(rr, 2)}/{sumA}*TxMonth)");
                s.Set(rr, 4, g.Treatments == 0 ? 0 : src.Gallons * input.LiterPerGallonA / g.Treatments, F2,
                    $"IF({R(rr, 3)}=0,0,{R(rr, 2)}*LpG_A/{R(rr, 3)})");
            }
            s.Set(rr, 5, src.UsePowder ? Powder : Gallon, InTxtC);
        }
        if (nA > 0)
        {
            s.Lists.Add(($"{R(a1, 5)}:{R(aN, 5)}", $"{Powder},{Gallon}"));
            // กลุ่ม A (รวมเซลล์แนวตั้ง แบบไฟล์ต้นฉบับ)
            s.Set(a1, 6, r.TotalGallonsA, F0c, sumA); Name("GL_A", a1, 6);
            s.Set(a1, 7, r.AvgGallonPerTreatA, F2, $"IF(TxMonth=0,0,{R(a1, 6)}/TxMonth)");
            s.Set(a1, 8, r.LiterPerTreatA, F2, $"{R(a1, 7)}*LpG_A"); Name("LiterTx_A", a1, 8);
            s.Set(a1, 9, input.PricePerGallonA, In2); Name("PriceA", a1, 9);
            s.Set(a1, 10, r.GlCostPerTreatA, F2, $"{R(a1, 7)}*{R(a1, 9)}");
            s.Set(a1, 11, r.GlCostMonthA, F0, $"{R(a1, 6)}*{R(a1, 9)}");
            for (int c = 6; c <= 11; c++) s.Merge(a1, c, aN, c);
        }
        // แถว B
        s.Set(rB, 1, "B", TxtC);
        if (liters)
        {
            s.Set(rB, 2, r.GallonsB, F0, $"{R(rB, 4)}*{R(rB, 3)}/LpG_B");
            s.Set(rB, 3, r.Treatments, F0, "TxMonth");
            s.Set(rB, 4, input.LitersPerTreatmentB, In2);
        }
        else
        {
            s.Set(rB, 2, input.GallonsB, In0);
            s.Set(rB, 3, r.Treatments, F0, "TxMonth");
            s.Set(rB, 4, r.Treatments == 0 ? 0 : input.GallonsB * input.LiterPerGallonB / r.Treatments, F2,
                $"IF({R(rB, 3)}=0,0,{R(rB, 2)}*LpG_B/{R(rB, 3)})");
        }
        s.Set(rB, 5, Powder, TxtC);
        s.Set(rB, 6, r.GallonsB, F0c, R(rB, 2)); Name("GL_B", rB, 6);
        s.Set(rB, 7, r.AvgGallonPerTreatB, F2, $"IF(TxMonth=0,0,{R(rB, 6)}/TxMonth)");
        s.Set(rB, 8, r.LiterPerTreatB, F2, $"{R(rB, 7)}*LpG_B"); Name("LiterTx_B", rB, 8);
        s.Set(rB, 9, input.PricePerGallonB, In2);
        s.Set(rB, 10, r.GlCostPerTreatB, F2, $"{R(rB, 7)}*{R(rB, 9)}");
        s.Set(rB, 11, r.GlCostMonthB, F0, $"{R(rB, 6)}*{R(rB, 9)}");
        if (nA == 0) wb.Names.Add(("GL_A", "0")); // ไม่มีสูตร A
        if (nA == 0) wb.Names.Add(("LiterTx_A", "0"));

        // รวม
        s.Set(rTot, 1, "Total GL per Month", Tot); s.Merge(rTot, 1, rTot, 5);
        s.Set(rTot, 6, r.TotalGallons, Tot0, "GL_A+GL_B"); Name("GL_Total", rTot, 6);
        s.Set(rTot, 7, "Total Cost for A & B per month", Tot); s.Merge(rTot, 7, rTot, 10);
        s.Set(rTot, 11, r.GlCostMonth, Tot0, nA > 0 ? $"{R(a1, 11)}+{R(rB, 11)}" : R(rB, 11)); Name("GL_Cost", rTot, 11);

        s.Set(rSale, 1, "ขาย GL (แกลลอนเปล่า)", Lbl);
        s.Set(rSale, 2, input.GallonSalePrice, In2);
        s.Set(rSale, 3, "THB / GL", Lbl);
        s.Set(rSale, 4, "Total", Lbl);
        s.Set(rSale, 5, r.GlSaleRevenue, F0, "GL_Total*SalePrice"); Name("GL_Sale", rSale, 5);
        s.Set(rSale, 6, "THB / MN", Lbl);
        s.Set(rGlNet, 1, "ค่าใช้จ่าย / เดือน สำหรับ A & B แบบ GL", LblB); s.Merge(rGlNet, 1, rGlNet, 4);
        s.Set(rGlNet, 5, r.GlNetMonth, Big0, "GL_Cost-GL_Sale"); Name("GL_Net", rGlNet, 5);
        s.Set(rGlNet, 6, "THB", LblB);

        s.Set(rPar, 1, "ค่าตั้งต้น", Hd); s.Set(rPar, 2, "ค่า", Hd); s.Set(rPar, 3, "หน่วย", Hd);
        s.Set(rLpgA, 1, "ลิตร / แกลลอน A", TxtB); s.Set(rLpgA, 2, input.LiterPerGallonA, In2); s.Set(rLpgA, 3, "ลิตร", TxtC);
        s.Set(rLpgB, 1, "ลิตร / แกลลอน B", TxtB); s.Set(rLpgB, 2, input.LiterPerGallonB, In2); s.Set(rLpgB, 3, "ลิตร", TxtC);
        s.Set(rCcdsPrice, 1, "ราคา A แบบแกลลอนเมื่อใช้ CCDS", TxtB); s.Set(rCcdsPrice, 2, input.CcdsPricePerGallonA, In2); s.Set(rCcdsPrice, 3, "บาท / GL", TxtC);
        int leftBottom = rCcdsPrice;

        // ================= ฝั่งขวา: CCDS =================
        const int M = 13;
        int y = 6;
        s.Set(y, M, $"CCDS for {input.Machines} Machines", Sec, $"\"CCDS for \"&Machines&\" Machines\""); s.Merge(y, M, y, M + 6);
        // --- Leasing ---
        y++;
        string[] lh = { "Leasing", "Price", "ดอกเบี้ย Flat / ปี", "Year", "Total Interest", "Total", "CCDS Payment / M" };
        for (int c = 0; c < lh.Length; c++) s.Set(y, M + c, lh[c], Hd);
        y++;
        s.Set(y, M, "ค่าเช่าซื้อเครื่อง", TxtB);
        s.Set(y, M + 1, input.LeasingPrice, In0); Name("LeasePrice", y, M + 1);
        s.Set(y, M + 2, input.InterestRatePercent, new Style(Border: Bd.Thin, Fill: Fill.Input, Num: Num.Pct)); Name("LeaseRate", y, M + 2);
        s.Set(y, M + 3, input.InstallmentYears, In0); Name("Years", y, M + 3);
        s.Set(y, M + 4, r.LeasingTotalInterest, F0, "LeasePrice*LeaseRate/100*Years");
        s.Set(y, M + 5, r.LeasingTotal, F0, $"LeasePrice+{R(y, M + 4)}");
        s.Set(y, M + 6, r.LeasingPerMonth, F2, $"IF(Years=0,0,{R(y, M + 5)}/(Years*12))"); Name("LeaseMonth", y, M + 6);
        y++;
        s.Set(y, M, "อัตราอ้างอิง: 1 ปี 2.85% • 2 ปี 3.25% • 3 ปี 3.50% • 4 ปี 3.60% • 5 ปี 3.55%", Note);

        // --- Powder ---
        y += 2;
        bool flow = input.PowderMode == PowderCalcMode.DialysateFlow;
        string[] ph = { "Powder", "Cost Per Box", "ผสมแล้ว=แกลลอน", "ลิตร / Tx", "Liter / Box", "No. Treatment / Box", "Cost / Treat" };
        for (int c = 0; c < ph.Length; c++) s.Set(y, M + c, ph[c], Hd);
        string isFlow = $"PMode=\"{ModeFlow}\"";
        foreach (var (p, res, key) in new[] { (input.PowderA, r.PowderA, "A"), (input.PowderB, r.PowderB, "B") })
        {
            y++;
            s.Set(y, M, key, TxtC);
            s.Set(y, M + 1, p.CostPerBox, In2); Name("CostBox_" + key, y, M + 1);
            // ลิตร/กล่อง คงที่ (A 100, B 240) — ผสมแล้ว=แกลลอน และ ลิตร / Tx เป็นสูตร (ลิตร / Tx จากข้อ 2 หรือ Dialysate flow)
            s.Set(y, M + 2, res.MixPerBox, F2, $"IF(PLiterTx_{key}=0,0,{R(y, M + 4)}/PLiterTx_{key})");
            s.Set(y, M + 3, res.MixLiter, F2, $"PLiterTx_{key}");
            s.Set(y, M + 4, res.LiterPerBox, F2); Name("LiterBox_" + key, y, M + 4);
            s.Set(y, M + 5, res.TreatmentsPerBox, F2, $"IF(PLiterTx_{key}=0,0,{R(y, M + 4)}/PLiterTx_{key})");
            s.Set(y, M + 6, res.CostPerTreat, F2, $"IF({R(y, M + 5)}=0,0,{R(y, M + 1)}/{R(y, M + 5)})");
            Name("PowderTx_" + key, y, M + 6);
        }
        s.Set(++y, M, $"หมายเหตุ: A 1 กล่อง ผสมได้ {CcdsCalculator.BoxLitersA:0}L, B 1 กล่อง ผสมได้ {CcdsCalculator.BoxLitersB:0}L", Note);

        // --- ค่าที่ใช้คิดผง: ลิตร / Treatment (จากแกลลอน หรือจาก Dialysate flow) ---
        y += 2;
        s.Set(y, M, "ค่าที่ใช้คิดผง", Hd); s.Set(y, M + 1, "ค่า", Hd); s.Set(y, M + 2, "หน่วย", Hd);
        void Param(string label, decimal? input, decimal value, string? formula, string unit, string name)
        {
            y++;
            s.Set(y, M, label, TxtB);
            if (input is decimal v) s.Set(y, M + 1, v, In2); else s.Set(y, M + 1, value, F2, formula);
            s.Set(y, M + 2, unit, TxtC);
            Name(name, y, M + 1);
        }
        y++;
        s.Set(y, M, "คิดราคา/Tx จาก", TxtB);
        s.Set(y, M + 1, flow ? ModeFlow : ModeLiters, InTxtC);
        s.Set(y, M + 2, "", TxtC);
        s.Set(y, M + 3, $"พิมพ์ \"{ModeLiters}\" (ใช้ลิตร / Treatment จากแกลลอน) หรือ \"{ModeFlow}\"", Note);
        Name("PMode", y, M + 1);
        Param("Dialysate flow", input.DialysateFlow, 0, null, "ml / min", "Qd");
        Param("เวลาฟอก", input.TreatmentHours, 0, null, "ชม. / Tx", "TxHours");
        Param("เวลาเตรียมเครื่อง (Priming)", input.PrimingMinutes, 0, null, "นาที / Tx", "PrimeMin");
        Param("สัดส่วนผสม B (A = 1 ส่วน)", input.MixRatioB, 0, null, "ส่วน", "RatioB");
        Param("ส่วนผสมรวม (A + B + น้ำ)", input.MixRatioTotal, 0, null, "ส่วน", "RatioTotal");
        Param("น้ำยาฟอก / Treatment", null, r.DialysateLitersPerTreat, "Qd*(TxHours*60+PrimeMin)/1000", "ลิตร", "DialL");
        // กรอกเองในโปรแกรม (ข้อ 5) → เป็นช่องกรอกสีฟ้า; ไม่กรอก → สูตรอัตโนมัติ
        Param("ลิตร / Tx ที่ใช้คิดผง A" + (CcdsCalculator.OwnLitersPerTx(input, input.PowderA) is null ? "" : " (กรอกเอง)"), CcdsCalculator.OwnLitersPerTx(input, input.PowderA), r.PowderLiterPerTreatA,
            $"IF({isFlow},IF(RatioTotal=0,0,DialL/RatioTotal),LiterTx_A)", "ลิตร", "PLiterTx_A");
        Param("ลิตร / Tx ที่ใช้คิดผง B" + (CcdsCalculator.OwnLitersPerTx(input, input.PowderB) is null ? "" : " (กรอกเอง)"), CcdsCalculator.OwnLitersPerTx(input, input.PowderB), r.PowderLiterPerTreatB,
            $"IF({isFlow},IF(RatioTotal=0,0,DialL*RatioB/RatioTotal),LiterTx_B)", "ลิตร", "PLiterTx_B");

        // --- การใช้น้ำยาเมื่อใช้ CCDS ---
        y += 2;
        string[] uh = { "เมื่อใช้ CCDS (สูตร)", "แบบ", "Treatment", "GL ที่ยังซื้อ", "Cost / Treat", "Cost / MN", "ลิตรที่ผสม" };
        for (int c = 0; c < uh.Length; c++) s.Set(y, M + c, uh[c], Hd);
        int u1 = y + 1;
        for (int k = 0; k < nA; k++)
        {
            y++;
            var cr = r.CcdsRows[k];
            int src = a1 + k;
            string isP = $"{R(y, M + 1)}=\"{Powder}\"";
            s.Set(y, M, cr.Name, TxtC, R(src, 1));
            s.Set(y, M + 1, cr.IsPowder ? Powder : Gallon, TxtC, R(src, 5));
            s.Set(y, M + 2, cr.Treatments, F0, R(src, 3));
            s.Set(y, M + 3, cr.Gallons, F0, $"IF({isP},0,{R(src, 2)})");
            s.Set(y, M + 4, cr.CostPerTreat, F2, $"IF({isP},PowderTx_A,IF({R(y, M + 2)}=0,0,{R(y, M + 3)}*CcdsPriceA/{R(y, M + 2)}))");
            // ค่าผง = ลิตรที่ผสม ÷ ลิตรต่อกล่อง × ราคากล่อง; ลิตรที่ผสม = Treatment × แกลลอน A รวม × ลิตร/แกลลอน ÷ Treatment/เดือน
            // (ถ้า Treatment/เดือน = 0 ใช้ แกลลอนของสูตร × ลิตร/แกลลอน) — ตรงกับ CcdsCalculator
            s.Set(y, M + 5, cr.CostPerMonth, F0, $"IF({isP},IF(LiterBox_A=0,0,{R(y, M + 6)}/LiterBox_A*CostBox_A),{R(y, M + 3)}*CcdsPriceA)");
            // ลิตรที่ผสม: Dialysate flow / ลิตร/Tx กรอกเอง → Treatment × ลิตร/Tx (เมื่อ Treatment/เดือน > 0) — ตรงกับ CcdsCalculator
            string useTxA = CcdsCalculator.OwnLitersPerTx(input, input.PowderA) is not null ? "TxMonth>0" : $"AND({isFlow},TxMonth>0)";
            s.Set(y, M + 6, cr.MixedLiters, F0,
                $"IF({isP},IF({useTxA},{R(y, M + 2)}*PLiterTx_A,IF(TxMonth>0,{R(y, M + 2)}*GL_A*LpG_A/TxMonth,{R(src, 2)}*LpG_A)),0)");
        }
        int uAEnd = y;
        y++;
        var crB = r.CcdsRows[^1];
        s.Set(y, M, "B", TxtC);
        s.Set(y, M + 1, Powder, TxtC);
        s.Set(y, M + 2, crB.Treatments, F0, "TxMonth");
        s.Set(y, M + 3, 0m, F0);
        s.Set(y, M + 4, crB.CostPerTreat, F2, "PowderTx_B");
        s.Set(y, M + 5, crB.CostPerMonth, F0, $"IF(LiterBox_B=0,0,{R(y, M + 6)}/LiterBox_B*CostBox_B)");
        string useTxB = CcdsCalculator.OwnLitersPerTx(input, input.PowderB) is not null ? "TxMonth>0" : $"AND({isFlow},TxMonth>0)";
        s.Set(y, M + 6, crB.MixedLiters, F0, $"IF({useTxB},TxMonth*PLiterTx_B,GL_B*LpG_B)"); Name("MixLit_B", y, M + 6);
        int uEnd = y;
        string rangeKind = $"{Sheet.Abs(u1, M + 1)}:{Sheet.Abs(uEnd, M + 1)}";
        string rangeCost = $"{Sheet.Abs(u1, M + 5)}:{Sheet.Abs(uEnd, M + 5)}";
        y++;
        s.Set(y, M, "รวม", Tot); s.Merge(y, M, y, M + 2);
        s.Set(y, M + 3, r.CcdsOtherGallons, Tot0, $"SUM({R(u1, M + 3)}:{R(uEnd, M + 3)})"); Name("OtherGallons", y, M + 3);
        s.Set(y, M + 4, "", Tot);
        s.Set(y, M + 5, r.CcdsPowderCost + r.CcdsOtherGlCost, Tot0, $"SUM({rangeCost})");
        s.Set(y, M + 6, r.MixedLitersTotal, Tot0, $"SUM({R(u1, M + 6)}:{R(uEnd, M + 6)})");
        if (nA > 0) wb.Names.Add(("MixLit_A", $"SUM('{s.Name}'!{Sheet.Abs(u1, M + 6)}:{Sheet.Abs(uAEnd, M + 6)})"));
        else wb.Names.Add(("MixLit_A", "0"));
        wb.Names.Add(("PowderCost", $"SUMIF('{s.Name}'!{rangeKind},\"{Powder}\",'{s.Name}'!{rangeCost})"));
        wb.Names.Add(("OtherGlCost", $"SUMIF('{s.Name}'!{rangeKind},\"{Gallon}\",'{s.Name}'!{rangeCost})"));

        // --- Utility ---
        y += 2;
        string[] th = { "Utility", "หน่วยละ", "Consume", "Utility Cost" };
        for (int c = 0; c < th.Length; c++) s.Set(y, M + c, th[c], Hd);
        int rWater = y + 1, rElec = y + 2, rUtil = y + 3, rInc = y + 4;
        int rM3 = y + 6, rDays = y + 7, rTankA = y + 8, rTankB = y + 9;
        Name("M3Per1000", rM3, M + 1); Name("DaysMonth", rDays, M + 1); Name("TankA", rTankA, M + 1); Name("TankB", rTankB, M + 1);
        s.Set(rWater, M, "ค่าน้ำ (m3)", TxtB);
        s.Set(rWater, M + 1, input.WaterRate, In2);
        s.Set(rWater, M + 2, r.WaterM3, F2, "(MixLit_A+MixLit_B)*M3Per1000/1000");
        s.Set(rWater, M + 3, r.WaterCost, F0, $"{R(rWater, M + 1)}*{R(rWater, M + 2)}");
        s.Set(rElec, M, "ค่าไฟฟ้า (kWh)", TxtB);
        s.Set(rElec, M + 1, input.ElectricRate, In2);
        s.Set(rElec, M + 2, r.ElectricKwh, F0, "KwhMonth");
        s.Set(rElec, M + 3, r.ElectricCost, F0, $"{R(rElec, M + 1)}*{R(rElec, M + 2)}");
        s.Set(rUtil, M + 1, "Total Utility Cost / Month", Tot); s.Merge(rUtil, M + 1, rUtil, M + 2);
        s.Set(rUtil, M + 3, r.UtilityCost, Tot0, $"{R(rWater, M + 3)}+{R(rElec, M + 3)}"); Name("UtilityCost", rUtil, M + 3);
        s.Set(rInc, M, "นำ Utility มาคิดต้นทุน CCDS", LblB); s.Merge(rInc, M, rInc, M + 1);
        s.Set(rInc, M + 2, input.IncludeUtility ? Yes : No, InTxtC); Name("IncludeUtility", rInc, M + 2);
        s.Lists.Add((R(rInc, M + 2), $"{Yes},{No}"));
        s.Set(rInc, M + 3, $"(\"{Yes}\" / \"{No}\")", Note);
        s.Set(rM3 - 1, M, "ค่าตั้งต้น Utility", Hd); s.Set(rM3 - 1, M + 1, "ค่า", Hd); s.Set(rM3 - 1, M + 2, "หน่วย", Hd);
        s.Set(rM3, M, "น้ำ RO ต่อน้ำยาผสม 1,000 ลิตร", TxtB); s.Set(rM3, M + 1, input.WaterM3Per1000Liter, In2); s.Set(rM3, M + 2, "m3", TxtC);
        s.Set(rDays, M, "วันทำงาน / เดือน", TxtB); s.Set(rDays, M + 1, input.DaysPerMonth, In0); s.Set(rDays, M + 2, "วัน", TxtC);
        s.Set(rTankA, M, "ถังผสม A ต่อครั้ง", TxtB); s.Set(rTankA, M + 1, input.MixTankLiterA, In0); s.Set(rTankA, M + 2, "ลิตร", TxtC);
        s.Set(rTankB, M, "ถังผสม B ต่อครั้ง", TxtB); s.Set(rTankB, M + 1, input.MixTankLiterB, In0); s.Set(rTankB, M + 2, "ลิตร", TxtC);

        // --- Equipment ---
        y = rTankB + 2;
        string[] eh = { "Equipment", "KW", "ทำงานกี่ชม./รอบ", "จำนวนอุปกรณ์", "ทำงานกี่รอบ/วัน", "KW / Day", "KW / Month" };
        for (int c = 0; c < eh.Length; c++) s.Set(y, M + c, eh[c], Hd);
        int e1 = y + 1;
        for (int k = 0; k < input.Equipment.Count; k++)
        {
            y++;
            var e = input.Equipment[k];
            var er = r.EquipmentRows[k];
            s.Set(y, M, e.Name, InTxt);
            s.Set(y, M + 1, e.Kw, In2);
            s.Set(y, M + 2, e.Hours, In2);
            s.Set(y, M + 3, e.Qty, In0);
            if (e.RunsPerDayOverride is int runs) s.Set(y, M + 4, runs, In0);
            else if (e.Kind == EquipmentKind.MixingA) s.Set(y, M + 4, er.RunsPerDay, F0, "MIN(ROUNDUP(ROUND(IF(TankA=0,0,MixLit_A/MAX(DaysMonth,1)/TankA),6),0),10000)");
            else if (e.Kind == EquipmentKind.MixingB) s.Set(y, M + 4, er.RunsPerDay, F0, "MIN(ROUNDUP(ROUND(IF(TankB=0,0,MixLit_B/MAX(DaysMonth,1)/TankB),6),0),10000)");
            else s.Set(y, M + 4, 1m, In0);
            s.Set(y, M + 5, er.KwhPerDay, F2, $"{R(y, M + 1)}*{R(y, M + 2)}*{R(y, M + 3)}*{R(y, M + 4)}");
            s.Set(y, M + 6, er.KwhPerMonth, F0, $"{R(y, M + 5)}*MAX(DaysMonth,1)");
        }
        y++;
        s.Set(y, M + 4, "Total KW / Month", Tot); s.Merge(y, M + 4, y, M + 5);
        s.Set(y, M + 6, r.ElectricKwh, Tot0, input.Equipment.Count > 0 ? $"SUM({R(e1, M + 6)}:{R(y - 1, M + 6)})" : "0");
        Name("KwhMonth", y, M + 6);
        y++;
        s.Set(y, M, "ครั้ง/วัน ของถังผสม = ปริมาณที่ผสมต่อวัน ÷ ขนาดถัง (ปัดขึ้น) • ปั๊มหมุนเวียน = 1 รอบ/วัน", Note);

        // --- สรุปค่าใช้จ่าย CCDS ---
        y += 2;
        s.Set(y, M, $"ค่าใช้จ่ายสำหรับ CCDS Base on {r.Treatments:#,##0} Treatment / Month", LblB,
            "\"ค่าใช้จ่ายสำหรับ CCDS Base on \"&TEXT(TxMonth,\"#,##0\")&\" Treatment / Month\"");
        int c1 = y + 1;
        (string L, string? LF, decimal V, string F)[] rows =
        {
            ($"Leasing ({input.InstallmentYears} ปี)", "\"Leasing (\"&Years&\" ปี)\"", r.LeasingPerMonth, "LeaseMonth"),
            (CcdsCalculator.PowderLabel(r), null, r.CcdsPowderCost, "PowderCost"),
            ("GL ที่ยังซื้อ (สูตร A)", null, r.CcdsOtherGlCost, "OtherGlCost"),
            (input.IncludeUtility ? "Utility" : "Utility (ไม่นำมาคิด)", $"IF(IncludeUtility=\"{No}\",\"Utility (ไม่นำมาคิด)\",\"Utility\")",
                r.UtilityCostCounted, $"IF(IncludeUtility=\"{No}\",0,UtilityCost)"),
            ("หัก ขายแกลลอนคืน", null, -r.CcdsGlSaleRevenue, "-OtherGallons*SalePrice"),
        };
        foreach (var (l, lf, v, f) in rows)
        {
            y++;
            s.Set(y, M, l, TxtB, lf);
            s.Set(y, M + 1, v, F0, f);
        }
        y++;
        s.Set(y, M, "Total", Tot);
        s.Set(y, M + 1, r.CcdsNetMonth, Big0, $"SUM({R(c1, M + 1)}:{R(y - 1, M + 1)})"); Name("CCDS_Net", y, M + 1);
        y += 2;
        s.Set(y, M, "Diff. GL Vs CCDS (Saving / เดือน)", new Style(Bold: true, Color: "800020"));
        s.Set(y, M + 1, r.SavingPerMonth, Big0, "GL_Net-CCDS_Net"); Name("SavingMonth", y, M + 1);
        y++;
        s.Set(y, M, "ต้นทุนต่อ Treatment: แบบ GL", TxtB); s.Set(y, M + 1, r.GlCostPerTreatment, F2, "IF(TxMonth=0,0,GL_Net/TxMonth)");
        y++;
        s.Set(y, M, "ต้นทุนต่อ Treatment: แบบ CCDS", TxtB); s.Set(y, M + 1, r.CcdsCostPerTreatment, F2, "IF(TxMonth=0,0,CCDS_Net/TxMonth)");
        int rightBottom = y;

        // ================= 2) ผลตอบแทนระยะยาว =================
        row = leftBottom + 3;
        s.Set(row, 1, "2) ผลตอบแทนระยะยาว", Sec); s.Merge(row, 1, row, 11);
        row++;
        int rPm = row, rWar = row + 1, rHor = row + 2;
        s.Set(rPm, 1, "ค่า PM ต่อปี (หลังหมด Warranty)", TxtB); s.Merge(rPm, 1, rPm, 3, TxtB); s.Set(rPm, 4, input.PmPerYear, In0); Name("PmYear", rPm, 4);
        s.Set(rWar, 1, "Warranty (ปี)", TxtB); s.Merge(rWar, 1, rWar, 3, TxtB); s.Set(rWar, 4, input.WarrantyYears, In0); Name("Warranty", rWar, 4);
        // ตารางรายปีเผื่อไว้อีก 5 ปี (แก้ระยะเวลาวิเคราะห์ในไฟล์ได้ถึงจำนวนแถวที่มี)
        int MaxYears = Math.Min(30, Math.Max(input.HorizonYears, 1) + 5);
        s.Set(rHor, 1, $"ระยะเวลาวิเคราะห์ (ปี, สูงสุด {MaxYears})", TxtB); s.Merge(rHor, 1, rHor, 3, TxtB);
        s.Set(rHor, 4, Math.Min(input.HorizonYears, MaxYears), In0); Name("Horizon", rHor, 4);

        // ตารางรายปีอยู่ใต้สรุป — กำหนดตำแหน่งก่อน เพื่ออ้างถึงในสูตรสรุป
        int sumTop = row;
        int yHead = sumTop + 8, y1 = yHead + 1, yN = yHead + MaxYears;
        string netRange = $"{Sheet.Abs(y1, 5)}:{Sheet.Abs(yN, 5)}";
        string flagRange = $"{Sheet.Abs(y1, 7)}:{Sheet.Abs(yN, 7)}";
        string yearRange = $"{Sheet.Abs(y1, 1)}:{Sheet.Abs(yN, 1)}";

        (string L, object V, string F, Style St)[] facts =
        {
            ("Cost Saving ช่วงผ่อน", r.SavingDuringLease, "SavingMonth*12*Years", F2),
            ("หัก PM หลังหมด Warranty (ช่วงผ่อน)", -r.PmDuringLease, "-MAX(0,Years-Warranty)*PmYear", F2),
            ("สุทธิเมื่อผ่อนครบ", r.SavingDuringLeaseAfterPm, "", F2),
            ("หลังผ่อนครบ ต่อเดือน (หัก PM แล้ว)", r.SavingPerMonthAfterLease, "SavingMonth+LeaseMonth-PmYear/12", F2),
            ("หลังผ่อนครบ ต่อปี", r.SavingPerYearAfterLease, "", F2),
            ("Cost Saving สะสมตามระยะเวลาวิเคราะห์", r.SavingHorizon, $"SUM({netRange})", Tot2),
            // ปีคืนทุน = ปีแรกที่มีเครื่องหมาย ✓ (ยอดสะสมเป็นบวกต่อเนื่องจนจบช่วงวิเคราะห์)
            ("ปีที่เริ่มคืนทุน", r.BreakEvenYear is int be ? $"ปีที่ {be}" : "ไม่คืนทุน",
                $"IFERROR(\"ปีที่ \"&INDEX({yearRange},MATCH(\"✓\",{flagRange},0)),\"ไม่คืนทุน\")", Tot),
        };
        int fr = sumTop;
        const int FC = 7;   // สรุปอยู่คอลัมน์ G–K
        foreach (var (l, v, f, st) in facts)
        {
            bool total = st == Tot2 || st == Tot;
            s.Set(fr, FC, l, total ? Tot : TxtB); s.Merge(fr, FC, fr, FC + 2, total ? Tot : TxtB);
            string formula = f switch
            {
                "" when l.StartsWith("สุทธิ") => $"{R(fr - 2, FC + 3)}+{R(fr - 1, FC + 3)}",
                "" => $"{R(fr - 1, FC + 3)}*12",
                _ => f
            };
            var valStyle = st == Tot ? new Style(Bold: true, Border: Bd.Thick, Fill: Fill.Total, Align: Al.Right) : st;
            s.Set(fr, FC + 3, v, valStyle, formula);
            s.Merge(fr, FC + 3, fr, FC + 4, valStyle);
            fr++;
        }

        string[] yh = { "ปีที่", "Saving ค่าใช้จ่าย", "หัก ค่า PM", "ไม่ต้องจ่าย Leasing", "สุทธิรายปี", "สะสม", "คืนทุน" };
        for (int c = 0; c < yh.Length; c++) s.Set(yHead, c + 1, yh[c], Hd);
        s.Heights[yHead] = 30;
        for (int k = 1; k <= MaxYears; k++)
        {
            int rr = yHead + k;
            var yr = k <= r.Years.Count && k <= input.HorizonYears ? r.Years[k - 1] : null;
            string blank = $"{R(rr, 1)}=\"\"";
            s.Set(rr, 1, yr is null ? "" : k, F0c, $"IF({k}<=Horizon,{k},\"\")");
            s.Set(rr, 2, yr is null ? "" : yr.Saving, F0, $"IF({blank},\"\",SavingMonth*12)");
            s.Set(rr, 3, yr is null ? "" : -yr.Pm, F0, $"IF({blank},\"\",IF({R(rr, 1)}>Warranty,-PmYear,0))");
            s.Set(rr, 4, yr is null ? "" : yr.LeasingFreed, F0, $"IF({blank},\"\",IF({R(rr, 1)}>Years,LeaseMonth*12,0))");
            s.Set(rr, 5, yr is null ? "" : yr.Net, new Style(Bold: true, Border: Bd.Thin, Num: Num.N0), $"IF({blank},\"\",{R(rr, 2)}+{R(rr, 3)}+{R(rr, 4)})");
            s.Set(rr, 6, yr is null ? "" : yr.Cumulative, new Style(Bold: true, Border: Bd.Thin, Num: Num.N0, Color: "1F5AA6"),
                $"IF({blank},\"\",SUM({Sheet.Abs(y1, 5)}:{R(rr, 5)}))");
            bool beFlag = yr is not null && r.BreakEvenYear is int b2 && k >= b2;
            s.Set(rr, 7, beFlag ? "✓" : "", new Style(Bold: true, Border: Bd.Thin, Align: Al.Center, Color: "15803D"),
                $"IF({blank},\"\",IF(AND({R(rr, 6)}>0,COUNTIF({R(rr, 6)}:{Sheet.Abs(yN, 6)},\"<=0\")=0),\"✓\",\"\"))");
        }
        _ = rightBottom;
        return wb.ToBytes();
    }

}
