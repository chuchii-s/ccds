using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace CcdsSimulator.Services.Xlsx;

// ตัวเขียนไฟล์ .xlsx ขนาดเล็ก (ไม่ต้องพึ่งไลบรารีภายนอก ทำงานใน Blazor WebAssembly ได้)
// รองรับ: ข้อความ/ตัวเลข/สูตร (พร้อมค่าที่คำนวณไว้แล้ว), สไตล์, รวมเซลล์, ความกว้างคอลัมน์,
// ชื่อที่กำหนด (Defined Names) และรายการตัวเลือก (Data Validation)

public enum Fill { None, Input, Total, Header, Section, Soft }
public enum Num { General, N0, N2, Pct }
public enum Bd { None, Thin, Thick }
public enum Al { Default, Left, Center, Right }

public record struct Style(
    bool Bold = false, double Size = 10, bool Italic = false, string? Color = null, bool Underline = false,
    Fill Fill = Fill.None, Bd Border = Bd.None, Num Num = Num.General, Al Align = Al.Default, bool Wrap = false);

public sealed class Cell
{
    public object? Value;     // string | decimal | null — ค่าที่แสดง (สำหรับสูตร = ค่าที่คำนวณไว้)
    public string? Formula;   // ไม่มีเครื่องหมาย "=" นำหน้า
    public Style Style;
}

public sealed class Sheet
{
    public string Name { get; }
    public Dictionary<(int R, int C), Cell> Cells { get; } = new();
    public List<string> Merges { get; } = new();
    public Dictionary<int, double> Widths { get; } = new();
    public HashSet<int> HiddenCols { get; } = new();
    public Dictionary<int, double> Heights { get; } = new();
    public List<(string Range, string List)> Lists { get; } = new();
    public Sheet(string name) => Name = name;

    public Cell Set(int r, int c, object? value, Style style = default, string? formula = null)
    {
        var cell = new Cell { Value = value, Formula = formula, Style = style };
        Cells[(r, c)] = cell;
        return cell;
    }

    public void Merge(int r1, int c1, int r2, int c2, Style? fillStyle = null)
    {
        if (r1 == r2 && c1 == c2) return;
        Merges.Add($"{Ref(r1, c1)}:{Ref(r2, c2)}");
        // ใส่สไตล์ (เส้นขอบ/พื้น) ให้ทุกเซลล์ในช่วง เพื่อให้กรอบรวมเซลล์แสดงครบ
        var st = fillStyle ?? (Cells.TryGetValue((r1, c1), out var first) ? first.Style : default);
        for (int r = r1; r <= r2; r++)
            for (int c = c1; c <= c2; c++)
                if (!Cells.ContainsKey((r, c))) Cells[(r, c)] = new Cell { Style = st };
    }

    public static string Col(int c)
    {
        var s = "";
        while (c > 0) { int m = (c - 1) % 26; s = (char)('A' + m) + s; c = (c - 1) / 26; }
        return s;
    }
    public static string Ref(int r, int c) => Col(c) + r;
    public static string Abs(int r, int c) => "$" + Col(c) + "$" + r;
}

public sealed class Workbook
{
    public List<Sheet> Sheets { get; } = new();
    public List<(string Name, string Target)> Names { get; } = new();

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public byte[] ToBytes()
    {
        var styles = new List<Style> { default };
        int StyleId(Style s)
        {
            int i = styles.IndexOf(s);
            if (i < 0) { styles.Add(s); i = styles.Count - 1; }
            return i;
        }

        var sheetXml = Sheets.Select(s => SheetXml(s, StyleId)).ToList();

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Add(string path, string xml)
            {
                var e = zip.CreateEntry(path, CompressionLevel.Optimal);
                using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
                w.Write(xml);
            }

            var sb = new StringBuilder();
            sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");
            for (int k = 1; k <= Sheets.Count; k++)
                sb.Append($"""<Override PartName="/xl/worksheets/sheet{k}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
            sb.Append("</Types>");
            Add("[Content_Types].xml", sb.ToString());

            Add("_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");

            sb.Clear();
            sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><bookViews><workbookView/></bookViews><sheets>""");
            for (int k = 0; k < Sheets.Count; k++)
                sb.Append($"""<sheet name="{X(Sheets[k].Name)}" sheetId="{k + 1}" r:id="rId{k + 1}"/>""");
            sb.Append("</sheets>");
            if (Names.Count > 0)
            {
                sb.Append("<definedNames>");
                foreach (var (n, t) in Names) sb.Append($"""<definedName name="{X(n)}">{X(t)}</definedName>""");
                sb.Append("</definedNames>");
            }
            sb.Append("""<calcPr calcId="191029" fullCalcOnLoad="1"/></workbook>""");
            Add("xl/workbook.xml", sb.ToString());

            sb.Clear();
            sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
            for (int k = 1; k <= Sheets.Count; k++)
                sb.Append($"""<Relationship Id="rId{k}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{k}.xml"/>""");
            sb.Append($"""<Relationship Id="rId{Sheets.Count + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            Add("xl/_rels/workbook.xml.rels", sb.ToString());

            for (int k = 0; k < Sheets.Count; k++) Add($"xl/worksheets/sheet{k + 1}.xml", sheetXml[k]);
            Add("xl/styles.xml", StylesXml(styles));
        }
        return ms.ToArray();
    }

    private static string SheetXml(Sheet s, Func<Style, int> styleId)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");
        sb.Append("""<sheetPr><pageSetUpPr fitToPage="1"/></sheetPr>""");
        sb.Append("""<sheetViews><sheetView workbookViewId="0" showGridLines="0" zoomScale="100"/></sheetViews><sheetFormatPr defaultRowHeight="16.5"/>""");
        if (s.Widths.Count > 0 || s.HiddenCols.Count > 0)
        {
            sb.Append("<cols>");
            foreach (var c in s.Widths.Keys.Union(s.HiddenCols).OrderBy(x => x))
            {
                var w = s.Widths.TryGetValue(c, out var ww) ? ww : 10;
                sb.Append($"""<col min="{c}" max="{c}" width="{w.ToString(Inv)}" customWidth="1"{(s.HiddenCols.Contains(c) ? " hidden=\"1\"" : "")}/>""");
            }
            sb.Append("</cols>");
        }
        sb.Append("<sheetData>");
        foreach (var row in s.Cells.GroupBy(kv => kv.Key.R).OrderBy(g => g.Key))
        {
            var h = s.Heights.TryGetValue(row.Key, out var hh) ? $" ht=\"{hh.ToString(Inv)}\" customHeight=\"1\"" : "";
            sb.Append($"<row r=\"{row.Key}\"{h}>");
            foreach (var (key, cell) in row.OrderBy(kv => kv.Key.C).Select(kv => (kv.Key, kv.Value)))
            {
                var r = Sheet.Ref(key.R, key.C);
                int st = styleId(cell.Style);
                var sAttr = st == 0 ? "" : $" s=\"{st}\"";
                var f = cell.Formula is null ? "" : $"<f>{X(cell.Formula)}</f>";
                switch (cell.Value)
                {
                    case null when cell.Formula is null:
                        sb.Append($"<c r=\"{r}\"{sAttr}/>");
                        break;
                    case null:
                        sb.Append($"<c r=\"{r}\"{sAttr}>{f}</c>");
                        break;
                    case string str when cell.Formula is null:
                        sb.Append($"<c r=\"{r}\"{sAttr} t=\"inlineStr\"><is><t xml:space=\"preserve\">{X(str)}</t></is></c>");
                        break;
                    case string str:
                        sb.Append($"<c r=\"{r}\"{sAttr} t=\"str\">{f}<v>{X(str)}</v></c>");
                        break;
                    default:
                        var d = Convert.ToDecimal(cell.Value, Inv);
                        sb.Append($"<c r=\"{r}\"{sAttr}>{f}<v>{((double)d).ToString("R", Inv)}</v></c>");
                        break;
                }
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData>");
        if (s.Merges.Count > 0)
        {
            sb.Append($"<mergeCells count=\"{s.Merges.Count}\">");
            foreach (var m in s.Merges) sb.Append($"<mergeCell ref=\"{m}\"/>");
            sb.Append("</mergeCells>");
        }
        if (s.Cells.Count > 0)
        {
            int maxR = s.Cells.Keys.Max(k => k.R), maxC = s.Cells.Keys.Max(k => k.C);
            sb.Append($"<conditionalFormatting sqref=\"A1:{Sheet.Ref(maxR, maxC)}\"><cfRule type=\"cellIs\" dxfId=\"0\" priority=\"1\" operator=\"lessThan\"><formula>0</formula></cfRule></conditionalFormatting>");
        }
        if (s.Lists.Count > 0)
        {
            sb.Append($"<dataValidations count=\"{s.Lists.Count}\">");
            foreach (var (range, list) in s.Lists)
                sb.Append($"<dataValidation type=\"list\" allowBlank=\"1\" showErrorMessage=\"1\" sqref=\"{range}\"><formula1>\"{X(list)}\"</formula1></dataValidation>");
            sb.Append("</dataValidations>");
        }
        sb.Append("""<pageMargins left="0.4" right="0.4" top="0.5" bottom="0.5" header="0.3" footer="0.3"/><pageSetup paperSize="9" orientation="landscape" fitToWidth="1" fitToHeight="0"/></worksheet>""");
        return sb.ToString();
    }

    private static string StylesXml(List<Style> styles)
    {
        // ตัวเลขติดลบเป็นสีเบอร์กันดีด้วย Conditional Formatting (dxf 0) — ใช้ได้ทั้ง Excel / Google Sheets / LibreOffice
        var fmts = new Dictionary<Num, int> { [Num.General] = 0, [Num.N0] = 164, [Num.N2] = 165, [Num.Pct] = 166 };
        var fonts = new List<string>();
        var fills = new List<string> { "<fill><patternFill patternType=\"none\"/></fill>", "<fill><patternFill patternType=\"gray125\"/></fill>" };
        var borders = new List<string>();
        var xfs = new StringBuilder();

        int Index(List<string> list, string item)
        {
            int i = list.IndexOf(item);
            if (i < 0) { list.Add(item); i = list.Count - 1; }
            return i;
        }

        foreach (var s in styles)
        {
            var font = "<font>" + (s.Bold ? "<b/>" : "") + (s.Italic ? "<i/>" : "") + (s.Underline ? "<u/>" : "")
                       + $"<sz val=\"{(s.Size > 0 ? s.Size : 10).ToString(Inv)}\"/>" + $"<color rgb=\"FF{s.Color ?? "1B2433"}\"/>"
                       + "<name val=\"Tahoma\"/><family val=\"2\"/></font>";
            var fillColor = s.Fill switch
            {
                Fill.Input => "BDE3FA", Fill.Total => "FFF27A", Fill.Header => "EEF3F9",
                Fill.Section => "1E3A5F", Fill.Soft => "F6F8FB", _ => null
            };
            var fill = fillColor is null ? fills[0]
                : $"<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF{fillColor}\"/><bgColor indexed=\"64\"/></patternFill></fill>";
            var line = s.Border switch { Bd.Thin => "thin", Bd.Thick => "medium", _ => null };
            var border = line is null ? "<border><left/><right/><top/><bottom/><diagonal/></border>"
                : $"<border><left style=\"{line}\"><color rgb=\"FF8A97A8\"/></left><right style=\"{line}\"><color rgb=\"FF8A97A8\"/></right><top style=\"{line}\"><color rgb=\"FF8A97A8\"/></top><bottom style=\"{line}\"><color rgb=\"FF8A97A8\"/></bottom><diagonal/></border>";
            if (s.Border == Bd.Thick) border = border.Replace("8A97A8", "1B2433");

            int fi = Index(fonts, font), fl = Index(fills, fill), bi = Index(borders, border);
            var h = s.Align switch { Al.Left => "left", Al.Center => "center", Al.Right => "right", _ => null };
            var align = $"<alignment{(h is null ? "" : $" horizontal=\"{h}\"")} vertical=\"center\"{(s.Wrap ? " wrapText=\"1\"" : "")}/>";
            xfs.Append($"<xf numFmtId=\"{fmts[s.Num]}\" fontId=\"{fi}\" fillId=\"{fl}\" borderId=\"{bi}\" xfId=\"0\"" +
                       $" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\">{align}</xf>");
        }

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
               + "<numFmts count=\"3\">"
               + "<numFmt numFmtId=\"164\" formatCode=\"#,##0;\\-#,##0;\\-\"/>"
               + "<numFmt numFmtId=\"165\" formatCode=\"#,##0.00;\\-#,##0.00;\\-\"/>"
               + "<numFmt numFmtId=\"166\" formatCode=\"0.00\\%\"/>"
               + "</numFmts>"
               + $"<fonts count=\"{fonts.Count}\">{string.Concat(fonts)}</fonts>"
               + $"<fills count=\"{fills.Count}\">{string.Concat(fills)}</fills>"
               + $"<borders count=\"{borders.Count}\">{string.Concat(borders)}</borders>"
               + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
               + $"<cellXfs count=\"{styles.Count}\">{xfs}</cellXfs>"
               + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"
               + "<dxfs count=\"1\"><dxf><font><color rgb=\"FF800020\"/></font></dxf></dxfs>"
               + "</styleSheet>";
    }

    // ตัดอักขระควบคุมที่ XML 1.0 ไม่อนุญาต (ยกเว้น tab / ขึ้นบรรทัด) ก่อน escape — ไม่งั้น Excel เปิดไฟล์ไม่ได้
    private static string X(string s) =>
        SecurityElement.Escape(new string(s.Where(c => c >= 0x20 || c is '\t' or '\n' or '\r').ToArray())) ?? "";
}
