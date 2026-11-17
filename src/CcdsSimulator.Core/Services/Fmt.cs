using System.Globalization;

namespace CcdsSimulator.Services;

/// <summary>รูปแบบตัวเลขที่ใช้ทั้งหน้าจอและรายงาน</summary>
public static class Fmt
{
    // ใช้ InvariantCulture (รูปแบบตัวเลขเหมือน en-US) เพราะเว็บเปิด InvariantGlobalization เพื่อลดขนาดไฟล์
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;
    private static readonly string[] ThaiMonths =
        { "ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค." };

    /// <summary>วันที่แบบไทย พ.ศ. เช่น "17 พ.ย. 2569 14:05" (shortYear: "17 พ.ย. 69 14:05")</summary>
    public static string ThaiDateTime(DateTime d, bool shortYear = false)
    {
        int be = d.Year + 543;
        return $"{d.Day} {ThaiMonths[d.Month - 1]} {(shortYear ? (be % 100).ToString("00") : be.ToString())} {d:HH:mm}";
    }

    // ปัดก่อนจัดรูปแบบ เพื่อไม่ให้ค่าติดลบเล็ก ๆ แสดงเป็น "-0"
    public static string N0(decimal v) => Fix(v, 0).ToString("#,##0", En);
    public static string N1(decimal v) => Fix(v, 1).ToString("#,##0.0", En);
    public static string N2(decimal v) => Fix(v, 2).ToString("#,##0.00", En);
    private static decimal Fix(decimal v, int dp) { var r = Math.Round(v, dp); return r == 0 ? 0m : r; }
    public static string Signed(decimal v, int dp = 0)
    {
        var s = dp == 0 ? N0(v) : N2(v);
        return (Math.Round(v, dp) > 0 ? "+" : "") + s;
    }
    public static string Pct(decimal part, decimal whole) =>
        whole == 0 ? "-" : (part / whole).ToString("+0.0%;-0.0%;0.0%", En);
    public static string Cls(decimal v) => v >= 0 ? "pos" : "neg";
    public static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "-" : s;
    public static string Short(decimal v)
    {
        decimal a = Math.Abs(v);
        string s = a >= 1_000_000 ? (v / 1_000_000m).ToString("0.00", En) + "M"
                 : a >= 1_000 ? (v / 1_000m).ToString("0", En) + "K"
                 : v.ToString("0", En);
        return v > 0 ? "+" + s : s;
    }
}
