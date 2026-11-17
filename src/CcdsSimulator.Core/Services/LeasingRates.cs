namespace CcdsSimulator.Services;

/// <summary>
/// ตารางดอกเบี้ย Flat ต่อปี ตามจำนวนปีผ่อน (Leasing)
/// เลือกปีผ่อน → ได้ดอกเบี้ย, เลือกดอกเบี้ย → ได้ปีผ่อน
/// </summary>
public static class LeasingRates
{
    public static readonly (int Years, decimal RatePercent)[] Table =
    {
        (1, 2.85m),
        (2, 3.25m),
        (3, 3.50m),
        (4, 3.60m),
        (5, 3.55m),
    };

    public static decimal? RateFor(int years) =>
        Table.Where(t => t.Years == years).Select(t => (decimal?)t.RatePercent).FirstOrDefault();

    public static int? YearsFor(decimal ratePercent) =>
        Table.Where(t => t.RatePercent == ratePercent).Select(t => (int?)t.Years).FirstOrDefault();
}
