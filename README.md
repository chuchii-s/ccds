# CCDS Feasibility Simulator

Web application สำหรับคำนวณความคุ้มค่า (Feasibility Study) ของระบบ CCDS
เทียบกับการซื้อน้ำยาแบบแกลลอน (GL) — เขียนด้วย **C# .NET 8 / Blazor WebAssembly**
รันในเบราว์เซอร์ทั้งหมด host บน **GitHub Pages** ได้ ไม่ต้องมี server หรือฐานข้อมูล

## ความสามารถ

- **โครงการ** — สร้าง / แก้ไข / ทำสำเนา / ลบ โครงการได้หลายรายการ บันทึกอัตโนมัติ
- **ข้อมูลอยู่ในเบราว์เซอร์ของแต่ละคน** (localStorage) ไม่ถูกส่งขึ้น server — ใช้ "สำรองข้อมูลทั้งหมด" / "นำเข้าไฟล์ .json" เพื่อย้ายเครื่อง
- **ตั้งค่า** — ผู้ใช้แต่ละคนตั้งราคาน้ำยา ราคาผง ค่าน้ำ ค่าไฟ ค่า PM ฯลฯ ของตัวเอง เป็นค่าตั้งต้นของโครงการใหม่
- **รายงาน A4** 3 หน้า (สรุปผู้บริหาร / รายละเอียดการคำนวณ / Scenario) → พิมพ์หรือบันทึกเป็น PDF จากเบราว์เซอร์
- **Export HTML** — ไฟล์ .html ไฟล์เดียวพร้อมข้อมูลโครงการ ส่งให้คนอื่นเปิดดู / แก้ / พิมพ์ได้โดยไม่ต้องเข้าเว็บ
- **Dashboard** — เปรียบเทียบหลายโครงการ: Cost Saving / เดือน, สะสม, ต้นทุนต่อ Treatment

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
| `src/CcdsSimulator.Web/Services/ProjectStore.cs` | เก็บข้อมูลใน localStorage |
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
| ผง / Treatment | ราคากล่อง ÷ ((ครั้งผสม/กล่อง × ลิตร/ครั้ง) ÷ ลิตร/Treatment) |
| ค่าน้ำ | ลิตรน้ำยาที่ผสม × 2 m³ / 1,000 ลิตร × ค่าน้ำ |
| รอบผสม/วัน (อัตโนมัติ) | ปัดขึ้น(ลิตรผสมต่อวัน ÷ ขนาดถัง) — ถัง A 100 L, B 120 L |
| kWh / เดือน | kW × ชม. × จำนวน × รอบ/วัน × วันทำงาน |
| ค่าใช้จ่ายแบบ CCDS | Leasing + ผงผสม + สูตร A ที่ยังใช้ GL + Utility − GL ที่ยังใช้ × ราคาขายคืน |
| Cost Saving / เดือน | ค่าใช้จ่ายแบบ GL − ค่าใช้จ่ายแบบ CCDS |
| สุทธิเมื่อผ่อนครบ | Saving × 12 × ปีผ่อน − PM × (ปีผ่อน − ปี Warranty) |
| ปีหลังผ่อนครบ / เดือน | Saving + Leasing − PM ÷ 12 |
| ครบ N ปี | สุทธิเมื่อผ่อนครบ + (N − ปีผ่อน) × Saving ปีหลังผ่อนครบ |
| Scenario | GL A = ปัดขึ้น(Treatment สูตรนั้น) × Factor A, GL B = Treatment × Factor B แล้วคำนวณใหม่ทั้งหมด |
