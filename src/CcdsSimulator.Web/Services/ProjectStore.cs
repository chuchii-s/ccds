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
        var bk = user.Key("ccds.projects.v1.backup");
        if (string.IsNullOrEmpty(await js.InvokeAsync<string?>("ccds.get", bk)))   // มีสำรองอยู่แล้ว → ไม่ทับ
            await js.InvokeAsync<bool>("ccds.set", bk, raw);
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

    // ---------- ปรับข้อมูลเดิมครั้งเดียว: ข้อ 5 ผงผสม คิดราคา/Tx จาก Dialysate flow ----------
    private string FlowMigrationKey => user.Key("ccds.migrated.flow.v1");
    private string? flowMigratedFor;   // key ของผู้ใช้ที่ย้ายข้อมูลแล้ว (สลับผู้ใช้ในแท็บเดียวกันต้องตรวจใหม่)

    /// <summary>ทุกสถานที่และค่าตั้งต้นที่บันทึกไว้ → PowderMode = Dialysate flow (ทำครั้งเดียวต่อผู้ใช้)
    /// ถ้าเดิมคิดจากจำนวนลิตรและ Priming = 0 (ค่าตั้งต้นเก่า) → ตั้ง Priming 30 นาที ตามค่าตั้งต้นใหม่
    /// หลังจากนี้ผู้ใช้สลับกลับเป็นจำนวนลิตรได้ตามปกติ (ไม่ถูกเปลี่ยนซ้ำ)</summary>
    private async Task MigrateFlowAsync()
    {
        var flagKey = FlowMigrationKey;
        if (flowMigratedFor == flagKey) return;
        if (!string.IsNullOrEmpty(await js.InvokeAsync<string?>("ccds.get", flagKey))) { flowMigratedFor = flagKey; return; }
        if (!await user.EnsureCurrentAsync()) return;
        var arr = await LoadArrayAsync();
        bool changed = false;
        foreach (var n in arr) changed |= ToFlow(n?["Input"] as JsonObject);
        if (changed) await SaveArrayAsync(arr);
        var raw = await js.InvokeAsync<string?>("ccds.get", DefaultsKey);
        if (!string.IsNullOrEmpty(raw))
        {
            try
            {
                if (JsonNode.Parse(raw) is JsonObject d && ToFlow(d))
                    await js.InvokeAsync<bool>("ccds.set", DefaultsKey, d.ToJsonString());
            }
            catch (JsonException) { }
        }
        if (StorageAvailable && await js.InvokeAsync<bool>("ccds.set", flagKey, "1")) flowMigratedFor = flagKey;
    }

    private static bool ToFlow(JsonObject? inp)
    {
        if (inp is null) return false;
        int mode;
        try { mode = inp["PowderMode"]?.GetValue<int>() ?? -1; } catch { mode = -1; }
        if (mode == (int)PowderCalcMode.DialysateFlow) return false;
        inp["PowderMode"] = (int)PowderCalcMode.DialysateFlow;
        decimal prime;
        try { prime = inp["PrimingMinutes"]?.GetValue<decimal>() ?? 0; } catch { prime = 0; }
        if (prime == 0) inp["PrimingMinutes"] = 30;
        return true;
    }

    // ---------- API ----------
    public async Task<List<Project>> GetAllAsync()
    {
        await MigrateFlowAsync();
        var arr = await LoadArrayAsync();
        var list = new List<Project>();
        int bad = 0;
        foreach (var n in arr)
        {
            var p = TryRead(n);
            if (p is null || string.IsNullOrEmpty(p.Id)) { bad++; continue; }
            p.Input ??= new CcdsInput();
            CcdsSimulator.Services.CcdsCalculator.Sanitize(p.Input);   // กันค่า null / นอกช่วงจาก JSON เก่า (หน้าจอ = ค่าที่คำนวณ)
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

    /// <summary>บันทึก — คืน false ถ้าบันทึกไม่สำเร็จ (พื้นที่เต็ม / ไม่อนุญาตให้บันทึก / ผู้ใช้ถูกลบ)</summary>
    public async Task<bool> SaveAsync(Project p)
    {
        p.UpdatedAt = DateTime.Now;
        return await UpsertAsync(p);
    }

    /// <summary>แทนที่รายการที่ Id ตรงกัน (ตำแหน่งเดิม) หรือเพิ่มไว้บนสุด — รายการอื่นไม่ถูกแตะ</summary>
    private async Task<bool> UpsertAsync(Project p)
    {
        if (!await user.EnsureCurrentAsync()) return false;   // ผู้ใช้ถูกลบจากแท็บอื่น → ไม่เขียนข้อมูลกำพร้า
        var arr = await LoadArrayAsync();
        var node = JsonSerializer.SerializeToNode(p);
        int at = -1;
        for (int k = 0; k < arr.Count; k++) if (IdOf(arr[k]) == p.Id) { at = k; break; }
        if (at >= 0) arr[at] = node; else arr.Insert(0, node);
        await SaveArrayAsync(arr);
        return StorageAvailable;
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
        await MigrateFlowAsync();
        var raw = await js.InvokeAsync<string?>("ccds.get", DefaultsKey);
        try { if (!string.IsNullOrEmpty(raw) && JsonSerializer.Deserialize<CcdsInput>(raw) is { } d) return d; }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { }
        return CcdsSimulator.Services.Presets.FactoryDefaults();
    }

    public async Task<bool> SaveDefaultsAsync(CcdsInput d)
    {
        if (!await user.EnsureCurrentAsync()) return false;
        return StorageAvailable = await js.InvokeAsync<bool>("ccds.set", DefaultsKey, JsonSerializer.Serialize(d));
    }

    // ---------- หน้า "คำนวณน้ำยา" (จำค่าที่กรอกล่าสุดของผู้ใช้) ----------
    public const string DialysateKeyBase = "ccds.dialysate.v1";
    private string DialysateKey => user.Key(DialysateKeyBase);

    public async Task<DialysateCalcInput> GetDialysateAsync()
    {
        var raw = await js.InvokeAsync<string?>("ccds.get", DialysateKey);
        try { if (!string.IsNullOrEmpty(raw) && JsonSerializer.Deserialize<DialysateCalcInput>(raw) is { } d) return d; }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { }
        return CcdsSimulator.Services.DialysateCalculator.Defaults();
    }

    public async Task<bool> SaveDialysateAsync(DialysateCalcInput d)
    {
        if (!await user.EnsureCurrentAsync()) return false;
        return StorageAvailable = await js.InvokeAsync<bool>("ccds.set", DialysateKey, JsonSerializer.Serialize(d));
    }

    // ---------- สำรองข้อมูล ----------
    /// <summary>ไฟล์สำรองทุกสถานที่ (รวมรายการที่อ่านไม่ได้ด้วย เพื่อไม่ให้ข้อมูลหาย)</summary>
    public async Task<string> ExportAllAsync() => (await LoadArrayAsync()).ToJsonString(Pretty);
}
