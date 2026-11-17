namespace CcdsSimulator.Models;

/// <summary>โครงการ 1 รายการ (เก็บใน localStorage ของเบราว์เซอร์)</summary>
public class Project
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public CcdsInput Input { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Input.SiteName) ? "(ยังไม่ตั้งชื่อ)" : Input.SiteName;
}
