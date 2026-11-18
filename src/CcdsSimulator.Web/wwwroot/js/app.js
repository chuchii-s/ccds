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

    /**
     * สร้างไฟล์ PDF ขนาด A4 จากหน้ารายงาน (.page) — หน้าละ 1 หน้า PDF (รายงาน 3 หน้า = PDF 3 หน้าพอดีทุกเครื่อง)
     * ไม่พึ่งหน้าต่างพิมพ์ของเบราว์เซอร์ (ซึ่งบนมือถือเลือกขนาดกระดาษ/ขอบเองจนหน้าเกิน)
     *  mode "share" → แชร์ไฟล์ (ถ้าเครื่องรองรับ) ไม่งั้นดาวน์โหลด, "download" → ดาวน์โหลด
     * คืนค่า "shared" | "downloaded" | "cancelled"
     */
    reportPdf: async (containerId, fileName, title, mode) => {
        const root = document.getElementById(containerId);
        const pages = [...root.querySelectorAll(".page")];
        await document.fonts.ready;
        ccds.fitReport(root);
        const imgs = [];
        for (const pg of pages) {
            const url = await htmlToImage.toJpeg(pg, { quality: 0.93, pixelRatio: 2.5, backgroundColor: "#ffffff" });
            const bytes = new Uint8Array(await (await fetch(url)).arrayBuffer());
            const size = await new Promise((ok, fail) => { const im = new Image(); im.onload = () => ok([im.naturalWidth, im.naturalHeight]); im.onerror = fail; im.src = url; });
            imgs.push({ bytes, w: size[0], h: size[1] });
        }
        const file = new File([ccds.buildPdf(imgs, title)], fileName, { type: "application/pdf" });
        if (mode === "share" && navigator.canShare && navigator.canShare({ files: [file] })) {
            try { await navigator.share({ files: [file], title, text: title }); return "shared"; }
            catch (e) { if (e && e.name === "AbortError") return "cancelled"; }
        }
        const a = document.createElement("a");
        a.href = URL.createObjectURL(file); a.download = file.name;
        document.body.appendChild(a); a.click(); a.remove();
        setTimeout(() => URL.revokeObjectURL(a.href), 4000);
        return "downloaded";
    },

    /** เขียนไฟล์ PDF แบบง่าย: แต่ละหน้า A4 (595.28 × 841.89 pt) มีภาพ JPEG เต็มหน้า 1 ภาพ */
    buildPdf: (imgs, title) => {
        const enc = new TextEncoder(), parts = [], offs = [];
        let len = 0;
        const put = x => { const b = typeof x === "string" ? enc.encode(x) : x; parts.push(b); len += b.length; };
        const W = 595.28, H = 841.89, n = imgs.length;
        const obj = (id, body, stream) => {
            offs[id] = len;
            put(`${id} 0 obj\n`); put(body);
            if (stream) { put("\nstream\n"); put(stream); put("\nendstream"); }
            put("\nendobj\n");
        };
        put("%PDF-1.4\n"); put(new Uint8Array([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]));
        // หัวเรื่องเอกสาร (UTF-16BE hex รองรับภาษาไทย)
        const hex = [...(title || "CCDS")].map(c => c.charCodeAt(0).toString(16).padStart(4, "0")).join("");
        obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        obj(2, `<< /Type /Pages /Kids [${imgs.map((_, k) => `${4 + 3 * k} 0 R`).join(" ")}] /Count ${n} >>`);
        obj(3, `<< /Title <FEFF${hex}> /Producer (CCDS Feasibility Simulator) >>`);
        imgs.forEach((im, k) => {
            const pid = 4 + 3 * k, cid = pid + 1, iid = pid + 2;
            obj(pid, `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${W} ${H}] /Resources << /XObject << /Im0 ${iid} 0 R >> >> /Contents ${cid} 0 R >>`);
            const content = enc.encode(`q ${W} 0 0 ${H} 0 0 cm /Im0 Do Q`);
            obj(cid, `<< /Length ${content.length} >>`, content);
            obj(iid, `<< /Type /XObject /Subtype /Image /Width ${im.w} /Height ${im.h} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length ${im.bytes.length} >>`, im.bytes);
        });
        const size = 4 + 3 * n, xref = len;
        put(`xref\n0 ${size}\n0000000000 65535 f \n`);
        for (let id = 1; id < size; id++) put(`${String(offs[id]).padStart(10, "0")} 00000 n \n`);
        put(`trailer\n<< /Size ${size} /Root 1 0 R /Info 3 0 R >>\nstartxref\n${xref}\n%%EOF\n`);
        return new Blob(parts, { type: "application/pdf" });
    },

    // ---------- JPG: สร้างภาพทุกหน้าไว้ก่อน แล้วให้ผู้ใช้เลือกหน้าที่จะแชร์ / ดาวน์โหลด ----------
    _jpg: { files: [], urls: [] },
    _jpgGen: 0,
    /**
     * แปลงทุกหน้า .page ใน #containerId เป็น JPG เก็บไว้ คืน URL ของภาพ (ใช้แสดงตัวอย่าง)
     * ถ้าระหว่างสร้างมีการปิดหน้าต่าง/เปิดใหม่ (jpgClear หรือ jpgPrepare รอบใหม่) ผลรอบนี้จะถูกทิ้ง คืน null
     */
    jpgPrepare: async (containerId, baseName) => {
        ccds.jpgClear();
        const gen = ccds._jpgGen;
        const root = document.getElementById(containerId);
        if (!root) return null;
        const pages = [...root.querySelectorAll(".page")];
        await document.fonts.ready;
        ccds.fitReport(root);
        const files = [], urls = [];
        for (let k = 0; k < pages.length; k++) {
            const url = await htmlToImage.toJpeg(pages[k], { quality: 0.92, pixelRatio: 2, backgroundColor: "#ffffff" });
            const blob = await (await fetch(url)).blob();
            const f = new File([blob], `${baseName}_p${k + 1}.jpg`, { type: "image/jpeg" });
            files.push(f); urls.push(URL.createObjectURL(f));
            if (gen !== ccds._jpgGen) break;
        }
        if (gen !== ccds._jpgGen) { urls.forEach(u => URL.revokeObjectURL(u)); return null; }
        ccds._jpg = { files, urls };
        return urls;
    },
    jpgCanShare: () => {
        try {
            const f = new File([new Blob(["x"], { type: "image/jpeg" })], "x.jpg", { type: "image/jpeg" });
            return !!(navigator.share && navigator.canShare && navigator.canShare({ files: [f, f] }));
        } catch { return false; }
    },
    /** แชร์ภาพที่เลือก (ต้องเรียกจากการกดปุ่มโดยตรง) คืน "shared" | "cancelled" | "unsupported" | "failed" */
    jpgShare: async (idx, title) => {
        const files = idx.map(i => ccds._jpg.files[i]).filter(Boolean);
        if (!files.length || !navigator.canShare || !navigator.canShare({ files })) return "unsupported";
        try { await navigator.share({ files, title }); return "shared"; }
        catch (e) { return e && e.name === "AbortError" ? "cancelled" : "failed"; }
    },
    /** ดาวน์โหลดภาพที่เลือกทีละไฟล์ เว้นช่วงระหว่างไฟล์ (เบราว์เซอร์มักบล็อกการดาวน์โหลดหลายไฟล์ที่ออกมาพร้อมกัน) */
    jpgDownload: async idx => {
        for (let k = 0; k < idx.length; k++) {
            const f = ccds._jpg.files[idx[k]], url = ccds._jpg.urls[idx[k]];
            if (!f) continue;
            if (k > 0) await new Promise(r => setTimeout(r, 900));
            const a = document.createElement("a");
            a.href = url; a.download = f.name;
            document.body.appendChild(a); a.click(); a.remove();
        }
    },
    jpgClear: () => {
        // หน่วงก่อนคืนหน่วยความจำ เผื่อการดาวน์โหลดล่าสุดยังอ่านไฟล์อยู่
        const old = ccds._jpg.urls;
        ccds._jpg = { files: [], urls: [] };
        ccds._jpgGen++;
        if (old.length) setTimeout(() => old.forEach(u => URL.revokeObjectURL(u)), 10000);
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
// มือถือ/แท็บเล็ต: ติดป้าย print-safe ให้ CSS ย่อหน้ารายงานตอนพิมพ์ (ดู @media print ใน app.css)
ccds.markPrintSafe = () => {
    try {
        const touch = matchMedia("(hover: none) and (pointer: coarse)").matches
            || /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent)
            || (navigator.maxTouchPoints > 1 && /Macintosh/.test(navigator.userAgent));   // iPad (โหมดเดสก์ท็อป)
        document.documentElement.classList.toggle("print-safe", touch);
    } catch { }
};
ccds.markPrintSafe();
window.addEventListener("beforeprint", () => { ccds.markPrintSafe(); try { ccds.fitReport(document); } catch { } });
