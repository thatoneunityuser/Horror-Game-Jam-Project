# คู่มือระบบ Gameplay HUD (Room Counter & Flashlight Status)
### (Minimalist Horror Gameplay HUD Guide)

ระบบ HUD สำหรับเกมเพลย์ ออกแบบตามความต้องการ:
1. **ตัวเลขนับห้อง (Room Counter)**: แสดงเป็น **เลขเปล่าๆ สีขาวล้วน (Clean White Raw Number)** พร้อมระบบเปลี่ยนสีตัวเลขเมื่อถึงชั้นสำคัญ
2. **สถานะไฟฉาย (Flashlight Status)**: แสดงผลสถานะ **ON / OFF** เรียบง่าย ชัดเจน **ไม่มีหลอดเลือด/หลอดแบตเตอรี่** กวนสายตา
3. **ฟอนต์ในเกม (Font Integration)**: ใช้ฟอนต์สไตล์พิมพ์ดีดสยองขวัญที่มีอยู่แล้วในโปรเจกต์ (`XTypewriter-Bold SDF`)

---

## 🌟 1. วิธีติดตั้งลงในฉาก (Quick Setup)

คุณสามารถนำ HUD ไปใช้งานได้ทันที 2 วิธี:

### วิธีที่ 1: กด 1-Click ผ่านเมนูด้านบนของ Unity
- ที่แถบเมนูข้างบนสุดของ Unity เลือก:
  **`Tools` ➔ `Horror UI` ➔ `Create or Update Gameplay HUD`**
  *(หรือคลิกขวาในหน้าต่าง Hierarchy ➔ `UI` ➔ `Horror Gameplay HUD`)*
- ระบบจะสร้าง Canvas พร้อมจัดตำแหน่งตัวเลขนับห้อง (มุมขวาบน) และไฟฉาย (มุมขวาล่าง) ให้เสร็จสมบูรณ์ทันที!

### วิธีที่ 2: ลาก Prefab เข้าฉาก
- เปิดโฟลเดอร์ `Assets/FOR GAYJAM/NON/`
- ลากไฟล์ **`GameplayHUD.prefab`** เข้ามาวางใน Hierarchy ได้เลย

---

## 🔢 2. ระบบตัวเลขนับห้อง (RoomCounterUI)

### หน้าตาและการทำงาน
- ตัวเลขแสดงผลอยู่ **มุมขวาบน** เป็นเลขเปล่าๆ (เช่น `1`, `2`, `15`, `30`)
- **สีเริ่มต้นคือ สีขาวล้วน (`#FFFFFF`)** พร้อมขอบดำบางๆ ทำให้อ่านง่ายบนทุกสภาพแสง
- ซิงค์ค่าอัตโนมัติกับ `RoomCounter.instance.PassedRoomCount`
- มีแอนิเมชัน Punch เล็กน้อยเมื่อเปลี่ยนเลขห้อง เพื่อให้ผู้เล่นรับรู้ได้ชัดเจน

### การเปลี่ยนสีตัวเลขตามชั้นสำคัญ (Milestone Colors)
ใน Inspector ของ `RoomCounter_Container` คุณสามารถกำหนดชั้นสำคัญและสีที่ต้องการได้ทันที:

| ชั้นห้อง (Room) | สีที่ตั้งไว้ล่วงหน้า | เหตุผล |
|---|---|---|
| **ชั้นปกติ (1-29)** | สีขาว (`#FFFFFF`) | ชั้นสำรวจปกติ |
| **ชั้น 30** | สีแดง (`#FF4040`) | จุดเริ่มต้น Event Rooms (30-34) |
| **ชั้น 34** | สีม่วง (`#CC4DFF`) | จุดสิ้นสุด Event Rooms |
| **ชั้น 50** | สีทอง (`#FFD700`) | ชั้นสุดท้าย Final Room |

*(คุณสามารถกด `+` หรือแก้เลขห้องและสีใน Inspector ได้อิสระ)*

### คำสั่ง C# สำหรับเปลี่ยนสีตัวเลขด้วยตัวเอง (Scripting API)
หากต้องการเปลี่ยนสีตัวเลขจากสคริปต์ของคุณเอง (เช่น เมื่อผู้เล่นเดินเข้า trigger, เจอผี, หรือเกิดอีเวนต์พิเศษ):

```csharp
// 1. สั่งเปลี่ยนสีตัวเลขทันที
GameplayHUDManager.SetRoomColor(Color.red);
// หรือ
RoomCounterUI.Instance.SetNumberColor(Color.red);

// 2. สั่งกะพริบสีชั่วคราว (เช่น สีแดง 1.5 วินาที แล้วคืนค่าเดิม)
RoomCounterUI.Instance.FlashColor(Color.red, 1.5f);

// 3. รีเซ็ตสีกลับเป็นสีขาวเริ่มต้น
RoomCounterUI.Instance.ResetColor();

// 4. สั่งเปลี่ยนตัวเลขห้องโดยตรง
RoomCounterUI.Instance.SetRoomNumber(40);
```

---

## 🔦 3. ระบบสถานะไฟฉาย (FlashlightUI)

### หน้าตาและการทำงาน
- แสดงผลอยู่ **มุมขวาล่าง** สไตล์เรียบง่ายคลีนๆ:
  - หัวข้อเล็กๆ: `FLASHLIGHT`
  - ไฟ LED แสดงสถานะ: สีเขียวสว่างเมื่อเปิด (`ON`) / สีแดงมืดเมื่อปิด (`OFF`)
  - ข้อความสถานะ: **`ON`** (สีขาวนวล) / **`OFF`** (สีเทามืด)
- **ไม่มีหลอดเลือด/หลอดแบตเตอรี่** ตามที่ระบุ

### การควบคุมไฟฉาย (Controls)
- ปุ่มเปิด/ปิดเริ่มต้น: **ปุ่ม `T`** (สามารถเปลี่ยนเป็นปุ่มอื่นได้ใน Inspector ช่อง `Toggle Key` เช่น `R` หรือ `G` เพื่อไม่ให้ชนกับปุ่ม `F` ซึ่งเป็น Dash ของผู้เล่น)
- รองรับการเปิด/ปิดผ่านคลิกขวา (ตั้งค่า `Allow Right Click Toggle = true` ได้ใน Inspector)
- **ค้นหาไฟฉายให้อัตโนมัติ**: ระบบจะตรวจหา GameObject ชื่อ `Flashlight'` หรือ `Light2D` ในตัว Player และสั่งเปิด-ปิดไฟจริงในฉากทันที
- มีเสียงสวิตช์คลิกแบบ tactile (`E_Interact_HorrorClick.wav`) ทั้งตอนเปิดและปิด

### คำสั่ง C# สำหรับควบคุมไฟฉาย (Scripting API)
```csharp
// สลับสถานะไฟฉาย (Toggle)
GameplayHUDManager.ToggleFlashlight();
// หรือ
FlashlightUI.Instance.ToggleFlashlight();

// สั่งเปิดหรือปิดไฟฉายโดยตรง
FlashlightUI.Instance.SetFlashlight(true);  // เปิด
FlashlightUI.Instance.SetFlashlight(false); // ปิด

// ตรวจสอบว่าไฟฉายเปิดอยู่หรือไม่
bool isLightOn = FlashlightUI.Instance.IsOn;
```

---

## 📁 4. โครงสร้างไฟล์ในโปรเจกต์

- **Prefab สำเร็จรูปพร้อมใช้**: [`GameplayHUD.prefab`](file:///c:/Git%20Not%20OneDrive/Horror-Game-Jam-Project/Assets/FOR%20GAYJAM/NON/GameplayHUD.prefab)
- **สคริปต์นับห้อง**: [`RoomCounterUI.cs`](file:///c:/Git%20Not%20OneDrive/Horror-Game-Jam-Project/Assets/FOR%20GAYJAM/NON/Non%20import/RoomCounterUI.cs)
- **สคริปต์สถานะไฟฉาย**: [`FlashlightUI.cs`](file:///c:/Git%20Not%20OneDrive/Horror-Game-Jam-Project/Assets/FOR%20GAYJAM/NON/Non%20import/FlashlightUI.cs)
- **สคริปต์ผู้จัดการ HUD**: [`GameplayHUDManager.cs`](file:///c:/Git%20Not%20OneDrive/Horror-Game-Jam-Project/Assets/FOR%20GAYJAM/NON/Non%20import/GameplayHUDManager.cs)
- **เครื่องมือ 1-Click Builder ใน Editor**: [`GameplayHUDBuilder.cs`](file:///c:/Git%20Not%20OneDrive/Horror-Game-Jam-Project/Assets/FOR%20GAYJAM/NON/Editor/GameplayHUDBuilder.cs)
