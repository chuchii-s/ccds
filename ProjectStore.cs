using System.Text.Json;
using CcdsSimulator.Models;
using Microsoft.JSInterop;

namespace CcdsSimulator.Web.Services;

/// <summary>
/// เก็บโครงการและค่าตั้งต้นไว้ใน localStorage ของเบราว์เซอร์
/// (ไม่มีฐานข้อมูล — ข้อมูลของแต่ละคนอยู่ในเบราว์เซอร์ของคนนั้นเท่านั้น)
/// </summary>
public class ProjectStore(IJSRuntime js)
{
    private const string ProjectsKey = "ccds.projects.v1";
    private const string DefaultsKey = "ccds.defaults.v1";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private List<Project>? _projects;

    /// <summary>false = เบราว์เซอร์ไม่อนุญาตให้บันทึก (เช่น private mode)</summary>
    public bool StorageAvailable { get; private set; } = true;

    public async Task<List<Project>> GetAllAsync()
    {
        if (_projects is not null) return _projects;
        var raw = await js.InvokeAsync<string?>("ccds.get", ProjectsKey);
        try { _projects = string.IsNullOrEmpty(raw) ? new() : JsonSerializer.Deserialize<List<Project>>(raw) ?? new(); }
        catch (JsonException) { _projects = new(); }
        return _projects;
    }

    public async Task<Project?> GetAsync(string id) => (await GetAllAsync()).FirstOrDefault(p => p.Id == id);

    public async Task<Project> CreateAsync(CcdsInput input)
    {
        var p = new Project { Input = input };
        (await GetAllAsync()).Insert(0, p);
        await PersistAsync();
        return p;
    }

    public async Task SaveAsync(Project p)
    {
        p.UpdatedAt = DateTime.Now;
        var all = await GetAllAsync();
        if (!all.Contains(p)) all.Insert(0, p);
        await PersistAsync();
    }

    public async Task DeleteAsync(string id)
    {
        (await GetAllAsync()).RemoveAll(p => p.Id == id);
        await PersistAsync();
    }

    public async Task<Project> DuplicateAsync(Project src)
    {
        var copy = CcdsSimulator.Services.Presets.Clone(src.Input);
        copy.SiteName = (copy.SiteName + " (สำเนา)").Trim();
        return await CreateAsync(copy);
    }

    // ---------- ค่าตั้งต้นของผู้ใช้ ----------
    public async Task<CcdsInput> GetDefaultsAsync()
    {
        var raw = await js.InvokeAsync<string?>("ccds.get", DefaultsKey);
        try { if (!string.IsNullOrEmpty(raw)) return JsonSerializer.Deserialize<CcdsInput>(raw)!; }
        catch (JsonException) { }
        return CcdsSimulator.Services.Presets.FactoryDefaults();
    }

    public async Task SaveDefaultsAsync(CcdsInput d) =>
        StorageAvailable = await js.InvokeAsync<bool>("ccds.set", DefaultsKey, JsonSerializer.Serialize(d));

    // ---------- นำเข้า / ส่งออก ----------
    /// <summary>ไฟล์สำรองทุกโครงการ</summary>
    public async Task<string> ExportAllAsync() => JsonSerializer.Serialize(await GetAllAsync(), Json);

    public static string ExportOne(CcdsInput input) => JsonSerializer.Serialize(input, Json);

    /// <summary>
    /// นำเข้าไฟล์ .json ได้ 2 แบบ: ไฟล์ข้อมูลโครงการเดียว (จากปุ่ม "บันทึก JSON" / โปรแกรม HTML)
    /// หรือไฟล์สำรองทุกโครงการ คืนค่าจำนวนโครงการที่นำเข้า
    /// </summary>
    public async Task<int> ImportAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var all = await GetAllAsync();
        int count = 0;
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in JsonSerializer.Deserialize<List<Project>>(json) ?? new())
            {
                if (all.Any(x => x.Id == p.Id)) p.Id = new Project().Id;
                all.Insert(0, p);
                count++;
            }
        }
        else
        {
            var input = JsonSerializer.Deserialize<CcdsInput>(json) ?? throw new JsonException("ไฟล์ว่าง");
            all.Insert(0, new Project { Input = input });
            count = 1;
        }
        await PersistAsync();
        return count;
    }

    private async Task PersistAsync() =>
        StorageAvailable = await js.InvokeAsync<bool>("ccds.set", ProjectsKey, JsonSerializer.Serialize(_projects));
}
