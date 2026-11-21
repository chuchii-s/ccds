using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.JSInterop;

namespace CcdsSimulator.Web.Services;

/// <summary>ผู้ใช้ในเบราว์เซอร์นี้ (ชื่อ + PIN ไม่บังคับ) — ข้อมูลของแต่ละคนเก็บแยก key กัน</summary>
public class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Name { get; set; } = "";
    /// <summary>SHA-256 ของ (Id + PIN) — null = ไม่ตั้ง PIN</summary>
    public string? PinHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool HasPin => !string.IsNullOrEmpty(PinHash);
}

/// <summary>
/// จัดการผู้ใช้หลายคนบนเบราว์เซอร์เดียวกัน (ไม่มี server):
/// • รายชื่อผู้ใช้เก็บใน localStorage "ccds.users.v1"
/// • ผู้ใช้ที่เข้าสู่ระบบอยู่เก็บใน sessionStorage (ปิดแท็บ/เบราว์เซอร์แล้วต้องเลือกผู้ใช้ใหม่)
/// • ข้อมูลของแต่ละคนอยู่ใน key ที่ลงท้ายด้วย ".u.{Id}"
/// • PIN ใช้กันการเปิดดูโดยบังเอิญ ไม่ใช่ระบบความปลอดภัยเต็มรูปแบบ (ข้อมูลยังอยู่ในเบราว์เซอร์เครื่องนี้)
/// </summary>
public class UserSession(IJSRuntime js)
{
    private const string UsersKey = "ccds.users.v1";
    private const string CurrentKey = "ccds.currentUser";
    /// <summary>key เดิมก่อนมีระบบผู้ใช้ — ย้ายให้ผู้ใช้คนแรกที่สร้าง</summary>
    public static readonly string[] LegacyKeys = ["ccds.projects.v1", "ccds.defaults.v1", "ccds.projects.v1.backup"];

    public UserProfile? Current { get; private set; }
    public bool Ready { get; private set; }
    public event Action? Changed;

    public string Key(string baseKey) =>
        Current is null ? throw new InvalidOperationException("ยังไม่ได้เลือกผู้ใช้") : $"{baseKey}.u.{Current.Id}";

    public async Task InitAsync()
    {
        if (Ready) return;
        try
        {
            var id = await js.InvokeAsync<string?>("ccds.sget", CurrentKey);
            Current = (await GetUsersAsync()).FirstOrDefault(u => u.Id == id);
        }
        catch (Exception) { Current = null; }   // JS ใช้ไม่ได้ / ข้อมูลเสีย → ไปหน้าเลือกผู้ใช้ ไม่ค้างที่ "กำลังโหลด"
        finally { Ready = true; }
    }

    // ---------- อ่าน / เขียนรายชื่อผู้ใช้ (ทีละรายการ: รายการที่อ่านไม่ได้ข้ามไป แต่เก็บไว้ไม่ลบ) ----------
    private async Task<(List<UserProfile> Users, List<JsonNode?> Bad)> LoadAsync()
    {
        var raw = await js.InvokeAsync<string?>("ccds.get", UsersKey);
        var users = new List<UserProfile>(); var bad = new List<JsonNode?>();
        if (string.IsNullOrWhiteSpace(raw)) return (users, bad);
        JsonArray? arr = null;
        try { arr = JsonNode.Parse(raw) as JsonArray; } catch (JsonException) { }
        if (arr is null)
        {
            await js.InvokeAsync<bool>("ccds.set", UsersKey + ".backup", raw);   // ทั้งก้อนเสีย → สำรองไว้ก่อน
            return (users, bad);
        }
        foreach (var n in arr)
        {
            UserProfile? u = null;
            try { u = n?.Deserialize<UserProfile>(); } catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException) { }
            if (u is null || string.IsNullOrEmpty(u.Id) || string.IsNullOrWhiteSpace(u.Name)) { if (n is not null) bad.Add(n.DeepClone()); continue; }
            users.Add(u);
        }
        return (users, bad);
    }

    public async Task<List<UserProfile>> GetUsersAsync() => (await LoadAsync()).Users;

    private async Task<bool> SaveAsync(List<UserProfile> users, List<JsonNode?> bad)
    {
        var arr = new JsonArray();
        foreach (var u in users) arr.Add(JsonSerializer.SerializeToNode(u));
        foreach (var b in bad) arr.Add(b?.DeepClone());
        return await js.InvokeAsync<bool>("ccds.set", UsersKey, arr.ToJsonString());
    }

    /// <summary>มีข้อมูลจากก่อนมีระบบผู้ใช้อยู่หรือไม่ (จะย้ายให้ผู้ใช้คนแรก)</summary>
    public async Task<bool> HasLegacyDataAsync()
    {
        foreach (var k in LegacyKeys)
            if (!string.IsNullOrEmpty(await js.InvokeAsync<string?>("ccds.get", k))) return true;
        return false;
    }

    public static bool ValidPin(string? pin) => pin is null || pin.Length == 0 || (pin.Length is >= 4 and <= 6 && pin.All(char.IsAsciiDigit));

    private static string Hash(string id, string pin) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id + ":" + pin)));

    public bool CheckPin(UserProfile u, string? pin) => !u.HasPin || (pin is not null && Hash(u.Id, pin) == u.PinHash);

    /// <summary>สร้างผู้ใช้ (ตรวจชื่อซ้ำจากรายชื่อล่าสุด) — คืน null ถ้าชื่อซ้ำ</summary>
    public async Task<UserProfile?> CreateAsync(string name, string? pin)
    {
        var (users, bad) = await LoadAsync();
        name = name.Trim();
        if (users.Any(u => string.Equals(u.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))) return null;
        var u = new UserProfile { Name = name };
        if (!string.IsNullOrEmpty(pin)) u.PinHash = Hash(u.Id, pin);
        // ผู้ใช้คนแรก: รับข้อมูลเดิม (ก่อนมีระบบผู้ใช้) ไปเป็นของตัวเอง — ลบของเดิมเฉพาะเมื่อคัดลอกสำเร็จ
        if (users.Count == 0 && bad.Count == 0)
        {
            foreach (var k in LegacyKeys)
            {
                var raw = await js.InvokeAsync<string?>("ccds.get", k);
                if (string.IsNullOrEmpty(raw)) continue;
                var target = $"{k}.u.{u.Id}";
                if (await js.InvokeAsync<bool>("ccds.set", target, raw)) { await js.InvokeVoidAsync("ccds.del", k); continue; }
                // พื้นที่ไม่พอเก็บ 2 ชุดพร้อมกัน: ย้ายแบบลบก่อนแล้วเขียนใหม่ ถ้ายังไม่ได้ให้คืนของเดิมไว้ที่เดิม
                await js.InvokeVoidAsync("ccds.del", k);
                if (!await js.InvokeAsync<bool>("ccds.set", target, raw)) await js.InvokeAsync<bool>("ccds.set", k, raw);
            }
        }
        users.Add(u);
        await SaveAsync(users, bad);
        await LoginAsync(u);
        return u;
    }

    public async Task LoginAsync(UserProfile u)
    {
        Current = u;
        await js.InvokeVoidAsync("ccds.sset", CurrentKey, u.Id);
        Changed?.Invoke();
    }

    public async Task LogoutAsync()
    {
        Current = null;
        await js.InvokeVoidAsync("ccds.sset", CurrentKey, null);
        Changed?.Invoke();
    }

    /// <summary>
    /// ตรวจว่าผู้ใช้ที่เข้าสู่ระบบอยู่ยังมีอยู่จริง (อาจถูกลบจากแท็บอื่น) — ถ้าไม่มีแล้วจะออกจากระบบ และคืน false
    /// เรียกก่อนบันทึกข้อมูลทุกครั้ง เพื่อไม่ให้เขียนข้อมูลกำพร้าที่ไม่มีใครเปิดได้
    /// </summary>
    public async Task<bool> EnsureCurrentAsync()
    {
        if (Current is null) return false;
        var me = (await GetUsersAsync()).FirstOrDefault(u => u.Id == Current.Id);
        if (me is null) { await LogoutAsync(); return false; }
        if (me.Name != Current.Name || me.PinHash != Current.PinHash) { Current = me; Changed?.Invoke(); }
        return true;
    }

    private async Task<bool> UpdateCurrentAsync(Action<UserProfile> change)
    {
        if (Current is null) return false;
        var (users, bad) = await LoadAsync();
        var u = users.FirstOrDefault(x => x.Id == Current.Id);
        if (u is null) { await LogoutAsync(); return false; }
        change(u);
        if (!await SaveAsync(users, bad)) return false;
        Current = u;
        Changed?.Invoke();
        return true;
    }

    /// <summary>เปลี่ยนชื่อ — คืน false ถ้าผู้ใช้ถูกลบไปแล้ว / บันทึกไม่ได้</summary>
    public Task<bool> RenameAsync(string name) => UpdateCurrentAsync(u => u.Name = name.Trim());

    /// <summary>ตั้ง / เปลี่ยน PIN (pin ว่าง = ยกเลิก PIN) — คืน false ถ้าผู้ใช้ถูกลบไปแล้ว / บันทึกไม่ได้</summary>
    public Task<bool> SetPinAsync(string? pin) =>
        UpdateCurrentAsync(u => u.PinHash = string.IsNullOrEmpty(pin) ? null : Hash(u.Id, pin));

    /// <summary>ลบผู้ใช้ปัจจุบันและข้อมูลทั้งหมดของผู้ใช้นี้ แล้วออกจากระบบ</summary>
    public async Task DeleteCurrentAsync()
    {
        if (Current is null) return;
        var id = Current.Id;
        foreach (var k in LegacyKeys) await js.InvokeVoidAsync("ccds.del", $"{k}.u.{id}");
        await js.InvokeVoidAsync("ccds.del", $"{ProjectStore.DialysateKeyBase}.u.{id}");   // ค่าหน้า "คำนวณน้ำยา"
        var (users, bad) = await LoadAsync();
        users.RemoveAll(u => u.Id == id);
        await SaveAsync(users, bad);
        await LogoutAsync();
    }
}
