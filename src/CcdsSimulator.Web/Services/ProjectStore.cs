using System.Text.Json;
using System.Text.Json.Nodes;
using CcdsSimulator.Models;
using Microsoft.JSInterop;

namespace CcdsSimulator.Web.Services;

/// <summary>
/// เก็บสถานที่และค่าตั้งต้นไว้ใน localStorage ของเบราว์เซอร์
/// (ไม่มีฐานข้อมูล — ข้อมูลของแต่ละคนอยู่ในเบราว์เซอร์ของคนนั้นเท่านั้น)
///
/// กันข้อมูลหาย:
/// • อ่าน localStorage ใหม่ทุกครั้งก่อนแก้ แล้วแก้เฉพาะรายการตาม Id (เปิดหลายแท็บพร้อมกันไม่ทับกัน)
/// • รายการที่อ่านไม่ได้ (ข้อมูลเสีย / รูปแบบเก่า) จะถูกข้ามตอนแสดง แต่เก็บไว้เหมือนเดิม ไม่ถูกลบ
/// • ถ้าทั้งก้อนอ่านไม่ได้ จะสำรองข้อความเดิมไว้ที่ key ".backup" ก่อนเริ่มรายการใหม่
/// </summary>
public class ProjectStore(IJSRuntime js, UserSession user)
{
    // key แยกตามผู้ใช้ที่เข้าสู่ระบบ (เช่น ccds.projects.v1.u.ab12cd34ef)
    private string ProjectsKey => user.Key("ccds.projects.v1");
    private string DefaultsKey => user.Key("ccds.defaults.v1");
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    /// <summary>false = เบราว์เซอร์ไม่อนุญาตให้บันทึก (เช่น private mode / พื้นที่เต็ม)</summary>
    public bool StorageAvailable { get; private set; } = true;

    /// <summary>จำนวนรายการที่อ่านไม่ได้ในการโหลดครั้งล่าสุด (ไม่ถูกลบ แค่ไม่แสดง)</summary>
    public int UnreadableCount { get; private set; }

    // ---------- อ่าน / เขียนก้อน JSON ดิบ ----------
    private async Task<JsonArray> LoadArrayAsync()
    {
        var raw = await js.InvokeAsync<string?>("ccds.get", ProjectsKey);
        if (string.IsNullOrWhiteSpace(raw)) return new JsonArray();
        try
        {
            if (JsonNode.Parse(raw) is JsonArray arr) return arr;
        }
        catch (JsonException) { }
        // ทั้งก้อนอ่านไม่ได้ → สำรองไว้ก่อน (ครั้งเดียว) ไม่ทับทิ้ง
        await js.InvokeAsync<bool>("ccds.set", user.Key("ccds.projects.v1.backup"), raw);
        return new JsonArray();
    }

    private async Task SaveArrayAsync(JsonArray arr) =>
        StorageAvailable = await js.InvokeAsync<bool>("ccds.set", ProjectsKey, arr.ToJsonString());

    private static string? IdOf(JsonNode? n)
    {
        try { return n?["Id"]?.GetValue<string>(); } catch { return null; }
    }

    private static Project? TryRead(JsonNode? n)
    {
        try { return n?.Deserialize<Project>(); } catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException) { return null; }
    }

    // ---------- API ----------
    public async Task<List<Project>> GetAllAsync()
    {
        var arr = await LoadArrayAsync();
        var list = new List<Project>();
        int bad = 0;
        foreach (var n in arr)
        {
            var p = TryRead(n);
            if (p is null || string.IsNullOrEmpty(p.Id)) { bad++; continue; }
            p.Input ??= new CcdsInput();
            list.Add(p);
        }
        UnreadableCount = bad;
        return list;
    }

    public async Task<Project?> GetAsync(string id) => (await GetAllAsync()).FirstOrDefault(p => p.Id == id);

    public async Task<Project> CreateAsync(CcdsInput input)
    {
        var p = new Project { Input = input };
        await UpsertAsync(p);
        return p;
    }

    public async Task SaveAsync(Project p)
    {
        p.UpdatedAt = DateTime.Now;
        await UpsertAsync(p);
    }

    /// <summary>แทนที่รายการที่ Id ตรงกัน (ตำแหน่งเดิม) หรือเพิ่มไว้บนสุด — รายการอื่นไม่ถูกแตะ</summary>
    private async Task UpsertAsync(Project p)
    {
        if (!await user.EnsureCurrentAsync()) return;   // ผู้ใช้ถูกลบจากแท็บอื่น → ไม่เขียนข้อมูลกำพร้า
        var arr = await LoadArrayAsync();
        var node = JsonSerializer.SerializeToNode(p);
        for (int k = 0; k < arr.Count; k++)
        {
            if (IdOf(arr[k]) == p.Id) { arr[k] = node; await SaveArrayAsync(arr); return; }
        }
        arr.Insert(0, node);
        await SaveArrayAsync(arr);
    }

    public async Task DeleteAsync(string id)
    {
        if (!await user.EnsureCurrentAsync()) return;
        var arr = await LoadArrayAsync();
        for (int k = arr.Count - 1; k >= 0; k--)
            if (IdOf(arr[k]) == id) arr.RemoveAt(k);
        await SaveArrayAsync(arr);
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
        try { if (!string.IsNullOrEmpty(raw) && JsonSerializer.Deserialize<CcdsInput>(raw) is { } d) return d; }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { }
        return CcdsSimulator.Services.Presets.FactoryDefaults();
    }

    public async Task SaveDefaultsAsync(CcdsInput d)
    {
        if (!await user.EnsureCurrentAsync()) return;
        StorageAvailable = await js.InvokeAsync<bool>("ccds.set", DefaultsKey, JsonSerializer.Serialize(d));
    }

    // ---------- สำรองข้อมูล ----------
    /// <summary>ไฟล์สำรองทุกสถานที่ (รวมรายการที่อ่านไม่ได้ด้วย เพื่อไม่ให้ข้อมูลหาย)</summary>
    public async Task<string> ExportAllAsync() => (await LoadArrayAsync()).ToJsonString(Pretty);
}
