// ---- ตัวช่วยฝั่งเบราว์เซอร์ (เรียกจาก C# ผ่าน JS interop) ----
window.ccds = {
    // localStorage อาจใช้ไม่ได้ (private mode / ถูกปิด) จึงครอบด้วย try/catch
    get: key => { try { return localStorage.getItem(key); } catch { return null; } },
    set: (key, value) => { try { localStorage.setItem(key, value); return true; } catch { return false; } },
    download: (fileName, text, mime) => {
        const blob = new Blob([text], { type: mime || "application/octet-stream" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    },
    print: () => window.print(),
    confirm: msg => window.confirm(msg)
};
