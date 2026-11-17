// ---- ตัวช่วยฝั่งเบราว์เซอร์ (เรียกจาก C# ผ่าน JS interop) ----
window.ccds = {
    // localStorage อาจใช้ไม่ได้ (private mode / ถูกปิด) จึงครอบด้วย try/catch
    get: key => { try { return localStorage.getItem(key); } catch { return null; } },
    set: (key, value) => { try { localStorage.setItem(key, value); return true; } catch { return false; } },
    del: key => { try { localStorage.removeItem(key); } catch { } },
    // sessionStorage: ผู้ใช้ที่เข้าสู่ระบบอยู่ (หายเมื่อปิดแท็บ/เบราว์เซอร์ → คนถัดไปต้องเลือกผู้ใช้ใหม่)
    sget: key => { try { return sessionStorage.getItem(key); } catch { return null; } },
    sset: (key, value) => { try { if (value === null) sessionStorage.removeItem(key); else sessionStorage.setItem(key, value); } catch { } },
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
    /**
     * แปลงรายงาน (ทุกหน้า .page ใน #containerId) เป็น JPG แล้ว
     *  mode "share"    → เปิดหน้าต่างแชร์ของเครื่อง (Line / Email / ...) ถ้าเครื่องรองรับ ไม่งั้นดาวน์โหลดแทน
     *  mode "download" → ดาวน์โหลดไฟล์
     * คืนค่า "shared" | "downloaded" | "cancelled"
     */
    /**
     * ให้เนื้อหารายงานแต่ละหน้าพอดีกระดาษ A4: ถ้า .content สูง/กว้างเกินพื้นที่ที่เหลือในหน้า
     * จะย่อทั้งเนื้อหาด้วย zoom เท่าที่จำเป็น (ไม่ต่ำกว่า 60%) — ใช้ทั้งบนจอ ตอนพิมพ์ และก่อนแปลง JPG
     */
    fitReport: root => {
        const scope = typeof root === "string" ? document.querySelector(root) : (root || document);
        if (!scope) return;
        scope.querySelectorAll(".page").forEach(pg => {
            const c = pg.querySelector(":scope > .content");
            if (!c) return;
            c.style.zoom = "";
            const probe = document.createElement("div");
            probe.style.cssText = "position:absolute;visibility:hidden;height:297mm;width:210mm;pointer-events:none";
            pg.appendChild(probe);
            const H = probe.offsetHeight, W = probe.offsetWidth;
            probe.remove();
            const cs = getComputedStyle(pg);
            const padV = parseFloat(cs.paddingTop) + parseFloat(cs.paddingBottom);
            const padH = parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight);
            let others = 0;
            for (const el of pg.children) {
                if (el === c) continue;
                const m = getComputedStyle(el);
                if (m.position === "absolute" || m.position === "fixed") continue;
                others += el.offsetHeight + parseFloat(m.marginTop) + parseFloat(m.marginBottom);
            }
            const availH = H - padV - others - 2, availW = W - padH;
            const z = Math.min(1, availH / c.scrollHeight, availW / c.scrollWidth);
            if (z < 0.995) c.style.zoom = Math.max(0.6, z).toFixed(3);
        });
    },

    fitReportWhenReady: async root => { await document.fonts.ready; ccds.fitReport(root); },

    reportJpg: async (containerId, baseName, title, mode) => {
        const pages = [...document.querySelectorAll(`#${containerId} .page`)];
        await document.fonts.ready;
        ccds.fitReport(document.getElementById(containerId));
        const files = [];
        for (let k = 0; k < pages.length; k++) {
            const url = await htmlToImage.toJpeg(pages[k], { quality: 0.92, pixelRatio: 2, backgroundColor: "#ffffff" });
            const blob = await (await fetch(url)).blob();
            files.push(new File([blob], `${baseName}_p${k + 1}.jpg`, { type: "image/jpeg" }));
        }
        if (mode === "share" && navigator.canShare && navigator.canShare({ files })) {
            try { await navigator.share({ files, title, text: title }); return "shared"; }
            catch (e) { if (e && e.name === "AbortError") return "cancelled"; }
        }
        for (const f of files) {
            const a = document.createElement("a");
            a.href = URL.createObjectURL(f); a.download = f.name;
            document.body.appendChild(a); a.click(); a.remove();
            setTimeout(() => URL.revokeObjectURL(a.href), 2000);
        }
        return "downloaded";
    },
    // ธีม: "light" | "dark" | "auto" (อัตโนมัติ ตามการตั้งค่าของเครื่อง) — จำไว้ในเบราว์เซอร์
    getTheme: () => { try { return localStorage.getItem("ccds.theme") || "auto"; } catch { return "auto"; } },
    setTheme: t => {
        try { localStorage.setItem("ccds.theme", t); } catch { }
        if (t === "light" || t === "dark") document.documentElement.dataset.theme = t;
        else delete document.documentElement.dataset.theme;
    }
};

// ---------- ปุ่มกลับขึ้นด้านบน (Back to top): โผล่เมื่อเลื่อนหน้าจอลงมา, อยู่มุมขวาล่าง ----------
(function () {
    function init() {
        if (document.getElementById("to-top")) return;
        const b = document.createElement("button");
        b.id = "to-top"; b.type = "button";
        b.title = "กลับขึ้นด้านบน"; b.setAttribute("aria-label", "กลับขึ้นด้านบน");
        b.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 19V5"/><path d="M5.5 11.5L12 5l6.5 6.5"/></svg>';
        b.addEventListener("click", () => {
            const smooth = !window.matchMedia("(prefers-reduced-motion: reduce)").matches;
            window.scrollTo({ top: 0, behavior: smooth ? "smooth" : "auto" });
        });
        document.body.appendChild(b);
        let ticking = false;
        const update = () => { b.classList.toggle("show", window.scrollY > 300); ticking = false; };
        window.addEventListener("scroll", () => { if (!ticking) { ticking = true; requestAnimationFrame(update); } }, { passive: true });
        update();
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init); else init();
})();

// ก่อนพิมพ์ (Ctrl+P / ปุ่มพิมพ์) ตรวจให้ทุกหน้ารายงานพอดี A4 อีกครั้ง
window.addEventListener("beforeprint", () => { try { ccds.fitReport(document); } catch { } });
