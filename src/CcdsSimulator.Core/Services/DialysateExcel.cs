using CcdsSimulator.Models;
using CcdsSimulator.Services.Xlsx;

namespace CcdsSimulator.Services;

/// <summary>
/// ไฟล์ Excel ของหน้า "คำนวณน้ำยา" — วางเซลล์ตามไฟล์ต้นฉบับ dialysate_calculator.xlsx
/// ช่องสีฟ้า = กรอก/แก้ได้, ช่องอื่นเป็นสูตร (มีค่าที่คำนวณไว้แล้วจาก DialysateCalculator)
/// </summary>
public static class DialysateExcel
{
    private static readonly Style Title = new(Bold: true, Size: 14, Color: "1E3A5F");
    private static readonly Style Note = new(Italic: true, Size: 9, Color: "66748A");
    private static readonly Style Sec = new(Bold: true, Size: 11, Color: "FFFFFF", Fill: Fill.Section);
    private static readonly Style Hd = new(Bold: true, Fill: Fill.Header, Border: Bd.Thin, Align: Al.Center, Wrap: true);
    private static readonly Style Lbl = new(Border: Bd.Thin);
    private static readonly Style Unit = new(Border: Bd.Thin, Align: Al.Center, Color: "66748A");
    private static readonly Style In2 = new(Border: Bd.Thin, Fill: Fill.Input, Num: Num.N2);
    private static readonly Style In0 = new(Border: Bd.Thin, Fill: Fill.Input, Num: Num.N0);
    private static readonly Style F2 = new(Border: Bd.Thin, Num: Num.N2);
    private static readonly Style F0 = new(Border: Bd.Thin, Num: Num.N0);
    private static readonly Style Tot = new(Bold: true, Border: Bd.Thick, Fill: Fill.Total);
    private static readonly Style Tot2 = new(Bold: true, Border: Bd.Thick, Fill: Fill.Total, Num: Num.N2);
    private static readonly Style Txt = new(Border: Bd.Thin, Color: "66748A");

    public static byte[] Build(DialysateCalcInput input)
    {
        var i = DialysateCalculator.Sanitized(input);
        var r = DialysateCalculator.Calculate(i);
        var wb = new Workbook();
        var s = new Sheet("คำนวณน้ำยา");
        wb.Sheets.Add(s);
        double[] w = { 34, 14, 24, 14, 17, 14, 44 };
        for (int c = 0; c < w.Length; c++) s.Widths[c + 1] = w[c];

        s.Set(1, 1, "ตารางคำนวณปริมาณน้ำยาไตเทียม (Dialysate Concentrate Calculator)", Title); s.Merge(1, 1, 1, 7);
        s.Heights[1] = 22;
        s.Set(2, 1, "แก้ไขได้เฉพาะช่องสีฟ้า — ช่องอื่นเป็นสูตรคำนวณอัตโนมัติ", Note); s.Merge(2, 1, 2, 7);

        // ---------- 1) ข้อมูลทั่วไป ----------
        Section(s, 4, "1  ข้อมูลทั่วไป");
        Input(s, 5, "เวลาฟอก (Treatment time)", i.TreatmentHours, "ชั่วโมง");
        Input(s, 6, "Priming", i.PrimingMinutes, "นาที");
        Input(s, 7, "Recirculation", i.RecirculationMinutes, "นาที");
        Input(s, 8, "Fill dialyser", i.FillMinutes, "นาที");
        Input(s, 9, "Dialysate Flow Rate (DFR)", i.FlowRate, "mL/min");
        Input(s, 10, "สัดส่วน A concentrate", i.RatioA, "ส่วน");
        Input(s, 11, "สัดส่วน B concentrate", i.RatioB, "ส่วน");
        Input(s, 12, "สัดส่วนรวม Dialysate", i.RatioTotal, "ส่วน");
        s.Set(5, 7, "← เปลี่ยนตัวเลขในช่องสีฟ้า แล้วผลด้านล่างจะคำนวณใหม่ทันที", Note);

        // ---------- 2) ต่อ 1 รอบ ----------
        Section(s, 14, "2  ผลการคำนวณต่อ 1 รอบ");
        Calc(s, 15, "เวลารวมทั้งหมด", r.TotalMinutes, "B5*60+B6+B7+B8", "นาที");
        Calc(s, 16, "Dialysate ที่ใช้ทั้งหมด", r.DialysateLiters, "B9*B15/1000", "ลิตร");
        Calc(s, 17, "สัดส่วน RO water (ส่วนที่เหลือ)", r.RatioRo, "B12-B10-B11", "ส่วน");
        Header(s, 19, "รายการ", "ปริมาณ (ลิตร)", "สูตรที่ใช้", "หมายเหตุ");
        s.Merge(19, 4, 19, 7);
        Row(s, 20, "A concentrate", r.LitersA, "IF(B12=0,0,B16*B10/B12)", "สูตร: Dialysate × A ÷ รวม");
        Row(s, 21, "B concentrate", r.LitersB, "IF(B12=0,0,B16*B11/B12)", "สูตร: Dialysate × B ÷ รวม");
        Row(s, 22, "RO water", r.LitersRo, "IF(B12=0,0,B16*B17/B12)", "สูตร: Dialysate × RO ÷ รวม");
        s.Set(23, 1, "รวมทั้งหมด (ตรวจสอบ)", Tot); s.Set(23, 2, r.SumCheck, Tot2, "SUM(B20:B22)"); s.Set(23, 3, "ต้องเท่ากับ Dialysate รวม", Txt);
        s.Set(24, 1, "ผลต่างจาก Dialysate รวม", Lbl); s.Set(24, 2, r.Difference, F2, "B23-B16");

        // ---------- 3) แกลลอน / ต้นทุนต่อ 1 รอบ ----------
        Section(s, 26, "3  จำนวนแกลลอน / ต้นทุนต่อ 1 รอบ");
        Header(s, 27, "รายการ", "ปริมาณที่ใช้ (L)", "ขนาดบรรจุ (L/แกลลอน)", "จำนวนแกลลอน", "ราคา/แกลลอน (บาท)", "ต้นทุน (บาท)");
        foreach (var (row, name, src, pack, price, gal, cost) in new[]
                 { (28, "A concentrate", 20, i.PackLitersA, i.PriceA, r.GallonsA, r.CostA),
                   (29, "B concentrate", 21, i.PackLitersB, i.PriceB, r.GallonsB, r.CostB) })
        {
            s.Set(row, 1, name, Lbl);
            s.Set(row, 2, src == 20 ? r.LitersA : r.LitersB, F2, $"B{src}");
            s.Set(row, 3, pack, In2);
            s.Set(row, 4, gal, F2, $"IFERROR(B{row}/C{row},0)");
            s.Set(row, 5, price, In2);
            s.Set(row, 6, cost, F2, $"D{row}*E{row}");
        }
        s.Set(28, 7, "← ขนาดบรรจุและราคา ใส่ตามจริงของโรงพยาบาล", Note);
        s.Set(30, 1, "รวมต้นทุนน้ำยา / รอบ", Tot); s.Merge(30, 1, 30, 5, Tot);
        s.Set(30, 6, r.CostPerCase, Tot2, "SUM(F28:F29)");
        s.Set(31, 1, "* ถ้า B concentrate เป็นแบบผง (BiCart / cartridge) ไม่ต้องใช้บรรทัด B — ให้คิดเป็นจำนวนกระป๋องตามที่เครื่องกำหนดแทน", Note);
        s.Merge(31, 1, 31, 7);

        // ---------- 4) หลายรอบ ----------
        Section(s, 33, "4  สรุปปริมาณการใช้น้ำยาทั้งเดือน");
        Input(s, 34, "จำนวนรอบ / วัน", i.CasesPerDay, "รอบ", In0);
        Input(s, 35, "จำนวนวันทำการ / เดือน", i.DaysPerMonth, "วัน", In0);
        s.Set(36, 1, "จำนวนรอบรวม / เดือน", Lbl);
        if (i.CasesPerMonth is decimal own) { s.Set(36, 2, own, In0); s.Set(36, 4, "(กรอกเอง — ลบแล้วพิมพ์ =B34*B35 เพื่อคำนวณอัตโนมัติ)", Note); }
        else s.Set(36, 2, r.CasesPerMonth, F0, "B34*B35");
        s.Set(36, 3, "รอบ", Unit);
        Header(s, 38, "รายการ", "ต่อ 1 รอบ", "ต่อวัน", "ต่อเดือน", "หน่วย");
        Line(s, 39, "A concentrate", r.A, "B20", "ลิตร");
        Line(s, 40, "B concentrate", r.B, "B21", "ลิตร");
        Line(s, 41, "RO water", r.Ro, "B22", "ลิตร");
        Line(s, 42, "Dialysate รวม", r.Dialysate, "B16", "ลิตร");
        Line(s, 43, "ต้นทุนน้ำยา", r.Cost, "F30", "บาท");
        Gallons(s, 44, "A concentrate (แกลลอน)", r.GallonsALine, "D28", 39, 28);
        Gallons(s, 45, "B concentrate (แกลลอน)", r.GallonsBLine, "D29", 40, 29);
        s.Set(44, 7, "← ต่อวัน/ต่อเดือน ปัดขึ้นเป็นแกลลอนเต็ม ใช้สำหรับสั่งซื้อ", Note);

        // ---------- หมายเหตุ ----------
        Section(s, 47, "หมายเหตุ / ข้อควรระวัง");
        string[] notes =
        {
            "", // ข้อ 1 เป็นสูตร (อัตราส่วนตามที่กรอก)
            "2. ตัวเลขที่ได้เป็นค่าทางทฤษฎี ปริมาณใช้จริงมักสูงกว่าเล็กน้อย จาก dead volume ในสายดูดและน้ำยาที่ค้างก้นแกลลอน",
            "", // ข้อ 3 เป็นสูตร
            "4. ราคาและขนาดบรรจุในหัวข้อ 3 เป็นตัวอย่างสมมติ กรุณาแก้เป็นค่าจริง",
        };
        for (int k = 0; k < notes.Length; k++)
        {
            int row = 48 + k;
            if (k == 0)
            {
                static string G(decimal v) => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
                const string tail = " เป็นค่าของน้ำยาบางยี่ห้อเท่านั้น ต้องตรวจสอบกับฉลากน้ำยาและ setting ของเครื่องทุกครั้ง";
                s.Set(row, 1, $"1. อัตราส่วน {G(i.RatioA)} : {G(i.RatioB)} : {G(r.RatioRo)} (รวม {G(i.RatioTotal)}){tail}", Lbl,
                    $"\"1. อัตราส่วน \"&B10&\" : \"&B11&\" : \"&B17&\" (รวม \"&B12&\"){tail}\"");
            }
            else if (k == 2)
            {
                string text = $"3. หากเครื่องหยุดปั๊ม concentrate (bypass / standby) ระหว่าง priming + recirculation ปริมาณจริงจะต่ำกว่านี้ประมาณ "
                    + $"{r.BypassShare:0.0%} ({i.PrimingMinutes + i.RecirculationMinutes:0.###}/{r.TotalMinutes:0.###} นาที) หรือ {r.BypassShareWithFill:0.0%} ถ้ารวม fill dialyser ด้วย";
                s.Set(row, 1, text, Lbl,
                    "\"3. หากเครื่องหยุดปั๊ม concentrate (bypass / standby) ระหว่าง priming + recirculation ปริมาณจริงจะต่ำกว่านี้ประมาณ \""
                    + "&TEXT(IF(B15=0,0,(B6+B7)/B15),\"0.0%\")&\" (\"&(B6+B7)&\"/\"&B15&\" นาที) หรือ \"&TEXT(IF(B15=0,0,(B6+B7+B8)/B15),\"0.0%\")&\" ถ้ารวม fill dialyser ด้วย\"");
            }
            else s.Set(row, 1, notes[k], Lbl);
            s.Merge(row, 1, row, 7);
        }
        return wb.ToBytes();
    }

    private static void Section(Sheet s, int row, string text) { s.Set(row, 1, text, Sec); s.Merge(row, 1, row, 7, Sec); }

    private static void Input(Sheet s, int row, string label, decimal v, string unit, Style? style = null)
    {
        s.Set(row, 1, label, Lbl); s.Set(row, 2, v, style ?? In2); s.Set(row, 3, unit, Unit);
    }

    private static void Calc(Sheet s, int row, string label, decimal v, string formula, string unit)
    {
        s.Set(row, 1, label, Lbl); s.Set(row, 2, v, F2, formula); s.Set(row, 3, unit, Unit);
    }

    private static void Header(Sheet s, int row, params string[] h)
    {
        for (int c = 0; c < h.Length; c++) s.Set(row, c + 1, h[c], Hd);
    }

    private static void Row(Sheet s, int row, string label, decimal v, string formula, string note)
    {
        s.Set(row, 1, label, Lbl); s.Set(row, 2, v, F2, formula); s.Set(row, 3, note, Txt);
    }

    private static void Line(Sheet s, int row, string label, DialysateLine l, string src, string unit)
    {
        s.Set(row, 1, label, Lbl);
        s.Set(row, 2, l.PerCase, F2, src);
        s.Set(row, 3, l.PerDay, F2, $"B{row}*$B$34");
        s.Set(row, 4, l.PerMonth, F2, $"B{row}*$B$36");
        s.Set(row, 5, unit, Unit);
    }

    private static void Gallons(Sheet s, int row, string label, DialysateLine l, string src, int litersRow, int packRow)
    {
        s.Set(row, 1, label, Lbl);
        s.Set(row, 2, l.PerCase, F2, src);
        s.Set(row, 3, l.PerDay, F0, $"IFERROR(ROUNDUP(ROUND(C{litersRow}/C{packRow},9),0),0)");
        s.Set(row, 4, l.PerMonth, F0, $"IFERROR(ROUNDUP(ROUND(D{litersRow}/C{packRow},9),0),0)");
        s.Set(row, 5, "แกลลอน", Unit);
    }
}
