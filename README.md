# CCDS Feasibility Simulator

Web application สำหรับคำนวณความคุ้มค่า (Feasibility Study) ของระบบ CCDS
เทียบกับการซื้อน้ำยาแบบแกลลอน (GL) — เขียนด้วย **C# .NET 8 / Blazor WebAssembly**
รันในเบราว์เซอร์ทั้งหมด host บน **GitHub Pages** ได้ ไม่ต้องมี server หรือฐานข้อมูล

## ความสามารถ

- **สถานที่เสนอระบบ CCDS** — เพิ่ม / แก้ไข / ทำสำเนา / ลบ ได้หลายสถานที่ บันทึกอัตโนมัติ
- **ข้อมูลอยู่ในเบราว์เซอร์ของแต่ละคน** (localStorage) ไม่ถูกส่งขึ้น server — ปุ่ม "สำรองข้อมูลทั้งหมด" ดาวน์โหลดเก็บเป็นไฟล์ .json
- **ตั้งค่า** — ผู้ใช้แต่ละคนตั้งราคาน้ำยา ราคาผง ค่าน้ำ ค่าไฟ ค่า PM ฯลฯ ของตัวเอง เป็นค่าตั้งต้นของโครงการใหม่
- **รายงาน A4** 3 หน้า (สรุปผู้บริหาร / สรุปผลการวิเคราะห์ / รายละเอียดการคำนวณ) → พิมพ์ / ดาวน์โหลด PDF / JPG — ถ้าเครื่องเปิดหน้าต่างพิมพ์ไม่ได้ (เช่น เปิดจากไอคอนหน้าจอโฮมของ iOS หรือเบราว์เซอร์ในแอป LINE) ปุ่ม "พิมพ์" จะสร้าง PDF ให้แชร์ไปพิมพ์ (หน้าต่างแชร์ของเครื่องมีปุ่มพิมพ์) หรือดาวน์โหลดแทน
- **JPG** — แปลงรายงานเป็นภาพ JPG (หน้าละ 1 ไฟล์) แชร์ไป Line / Email ผ่านหน้าต่างแชร์ของเครื่อง หรือดาวน์โหลด (ใช้ html-to-image, MIT)
- **Excel** — ดาวน์โหลดไฟล์ .xlsx ชีตเดียว จัดวางแบบไฟล์ Excel ต้นฉบับ ใส่สูตรคำนวณไว้ครบ (ช่องสีฟ้า = แก้ได้, ช่องอื่น = สูตร) ตัวเลขติดลบเป็นสีเบอร์กันดี — สร้างด้วยโค้ด C# เอง (`CcdsExcel.cs`, `Xlsx.cs`) ไม่ใช้ไลบรารีภายนอก
- **ผู้ใช้หลายคนในเบราว์เซอร์เดียว** — เลือกผู้ใช้ก่อนใช้งาน (ตั้ง PIN 4–6 หลักได้, ไม่บังคับ) สถานที่และค่าตั้งต้นของแต่ละคนเก็บแยก key กัน (`ccds.projects.v1.u.{id}`) ผู้ใช้ที่เข้าสู่ระบบอยู่จำไว้ใน sessionStorage (ปิดแท็บแล้วต้องเลือกใหม่) ข้อมูลเดิมก่อนมีระบบผู้ใช้จะย้ายให้ผู้ใช้คนแรก — PIN กันการเปิดดูโดยบังเอิญ ไม่ใช่ระบบความปลอดภัยเต็มรูปแบบ
- **เปรียบเทียบ** — เปรียบเทียบหลายสถานที่: Cost Saving / เดือน, สะสม, ต้นทุนต่อ Treatment
- **โหมดสว่าง / มืด / อัตโนมัติ** — ปุ่มที่แถบด้านบน (รายงาน A4 เป็นพื้นขาวเสมอ)
- **Undo / Redo แยกแต่ละข้อ** — ปุ่ม ↶ ↷ มุมขวาบนของกรอบข้อ 1–7

## ติดตั้งบน GitHub Pages (ครั้งแรกครั้งเดียว)

1. อัปโหลดไฟล์ทั้งหมดใน repo นี้ขึ้น GitHub (รวมโฟลเดอร์ `.github`)
2. ไปที่ **Settings → Pages → Build and deployment → Source** เลือก **GitHub Actions**
3. ไปที่แท็บ **Actions** → เลือก **Deploy to GitHub Pages** → **Run workflow** (หรือ push อะไรก็ได้เข้า `main`)
4. รอ ~3 นาที เว็บจะอยู่ที่ `https://<ชื่อผู้ใช้>.github.io/<ชื่อ repo>/`

หลังจากนั้นทุกครั้งที่แก้ไฟล์ใน `main` เว็บจะ build + test + deploy ใหม่ให้อัตโนมัติ

> GitHub Pages ของบัญชีฟรีต้องเป็น repo **public** — โค้ดและเว็บเปิดดูได้ทุกคน
> (ข้อมูลโครงการที่ผู้ใช้กรอกไม่ได้อยู่ใน repo เพราะเก็บในเบราว์เซอร์ของแต่ละคน)

## รันบนเครื่อง

```bash
dotnet run --project src/CcdsSimulator.Web     # เปิด URL ที่แสดง
dotnet test                                    # ทดสอบสูตร
```

## โครงสร้าง

| ไฟล์ | หน้าที่ |
|---|---|
| `src/CcdsSimulator.Core/Services/CcdsCalculator.cs` | **สูตรคำนวณทั้งหมด** |
| `src/CcdsSimulator.Core/Models/CcdsInput.cs` | ข้อมูลที่กรอก |
| `src/CcdsSimulator.Core/Services/Presets.cs` | ค่าตั้งต้นจากโรงงาน + ข้อมูลตัวอย่าง (ตัวเลขสมมติ) |
| `src/CcdsSimulator.Web/Pages/` | หน้าโครงการ, แก้ไข, รายงาน, Dashboard, ตั้งค่า |
| `src/CcdsSimulator.Web/Components/` | ฟอร์ม, แผงผลลัพธ์, รายงาน A4 |
| `src/CcdsSimulator.Core/Services/DialysateCalculator.cs` + `Pages/Dialysate.razor` | เมนู **คำนวณน้ำยา**: คำนวณปริมาณการใช้น้ำยา A / B / RO ต่อรอบ ต่อวัน ต่อเดือน และต้นทุน (สูตรเดียวกับไฟล์ dialysate_calculator.xlsx) — มีรายงาน A4 (`Components/DialysateReport.razor`, `Pages/DialysateReportPage.razor`), JPG และ Excel (`DialysateExcel.cs`) |
| `src/CcdsSimulator.Web/Services/ProjectStore.cs` | เก็บข้อมูลใน localStorage |
| `src/CcdsSimulator.Web/wwwroot/icons/` + `manifest.webmanifest` | ไอคอนโปรแกรม (แท็บเบราว์เซอร์ + หน้าจอโฮม iPhone / iPad / Android) — ต้นแบบคือ `icons/icon.svg` |
| `src/CcdsSimulator.Web/wwwroot/CCDS_Simulator.html` | โปรแกรมเวอร์ชันไฟล์เดียว (ใช้กับ Export HTML) |
| `tests/CcdsSimulator.Tests/` | ทดสอบสูตร |
| `.github/workflows/deploy.yml` | build + test + deploy ขึ้น GitHub Pages |

> ถ้าแก้สูตรใน `CcdsCalculator.cs` ต้องแก้ฟังก์ชัน `calculate()` ใน `CCDS_Simulator.html` ให้ตรงกันด้วย

## สูตรคำนวณ

| รายการ | สูตร |
|---|---|
| Treatment ต่อสูตร A | GL สูตรนั้น ÷ GL A รวม × Treatment/เดือน |
| ลิตร / Treatment | (GL ÷ Treatment) × ลิตร/แกลลอน (A = 5.5, B = 6) |
| ค่าใช้จ่ายแบบ GL | GL A × ราคา A + GL B × ราคา B − GL ทั้งหมด × ราคาขายแกลลอนคืน |
| Leasing / เดือน | (ราคา + ราคา × ดอกเบี้ย × ปี) ÷ (ปี × 12) |
| ดอกเบี้ยตามปีผ่อน | 1 ปี 2.85% • 2 ปี 3.25% • 3 ปี 3.50% • 4 ปี 3.60% • 5 ปี 3.55% (แก้ได้ที่ `Services/LeasingRates.cs`) |
| ผง / Treatment | ราคากล่อง ÷ ผสมแล้ว=แกลลอน; ผสมแล้ว=แกลลอน = ลิตรต่อกล่อง (A 100 L, B 240 L — `CcdsCalculator.BoxLitersA/B`) ÷ ลิตร / Tx |
| ลิตร / Treatment (ผง) | ข้อ 5 เลือกได้ (ค่าตั้งต้น: **Dialysate flow** 500 ml/min, 4 ชม., Priming 30 นาที): **จำนวนลิตร** = ลิตรที่ใช้จริงจากแกลลอนในข้อ 2 • **Dialysate flow** = flow (ml/min) × (ชม.ฟอก × 60 + นาที Priming) ÷ 1000 → A = ÷ ส่วนผสมรวม, B = × สัดส่วน B ÷ ส่วนผสมรวม (ค่าตั้งต้น 1 : 1.225 : รวม 35) — ทั้งสองโหมด ลิตร / Tx และ ผสมแล้ว=แกลลอน คำนวณให้อัตโนมัติ และแก้ตัวเลขเองได้ (เก็บแยกตามโหมด: `PowderInput.LitersPerTx` / `FlowLitersPerTx`; แก้ช่องหนึ่งอีกช่องคำนวณตามสูตรเดิม; ลบตัวเลข = กลับเป็นอัตโนมัติ) |
| ค่าน้ำ | ลิตรน้ำยาที่ผสม × 2 m³ / 1,000 ลิตร × ค่าน้ำ |
| รอบผสม/วัน (อัตโนมัติ) | ปัดขึ้น(ลิตรผสมต่อวัน ÷ ขนาดถัง) — ถัง A 100 L, B 120 L |
| kWh / เดือน | kW × ชม. × จำนวน × รอบ/วัน × วันทำงาน |
| ค่าใช้จ่ายแบบ CCDS | Leasing + ผงผสม + สูตร A ที่ยังใช้ GL + Utility − GL ที่ยังใช้ × ราคาขายคืน |
| Cost Saving / เดือน | ค่าใช้จ่ายแบบ GL − ค่าใช้จ่ายแบบ CCDS |
| สุทธิเมื่อผ่อนครบ | Saving × 12 × ปีผ่อน − PM × (ปีผ่อน − ปี Warranty) |
| ปีหลังผ่อนครบ / เดือน | Saving + Leasing − PM ÷ 12 |
| ครบ N ปี | สุทธิเมื่อผ่อนครบ + (N − ปีผ่อน) × Saving ปีหลังผ่อนครบ |
