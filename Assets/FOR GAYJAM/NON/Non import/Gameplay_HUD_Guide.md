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

### 🔴 การปรับเป็นตัวเลขสีแดง (Red Mode)
ออกแบบมาให้พร้อมสลับเป็นสีแดงได้ทันทีเมื่อต้องการ (ค่าเริ่มต้นปิดไว้ `false` ยังไม่ได้ใช้งาน):
- **ปรับผ่านหน้าต่าง Unity Inspector**:
  - เลือกที่ `RoomCounterUI` ➔ ดูหมวด **`=== Red Mode ===`**
  - ติ๊กถูกช่อง **`Is Red Mode`** ตัวเลขจะเปลี่ยนเป็นสีแดงทันที (มี Live Preview ใน Scene View ทันที)
  - สามารถปรับเฉดสีแดงได้อิสระที่ช่อง **`Red Color`**
- **สั่งเปิด/ปิดสีแดงผ่านโค้ด C#**:
  ```csharp
  // เปิดโหมดตัวเลขสีแดง
  GameplayHUDManager.SetRoomRedMode(true);
  // หรือ
  RoomCounterUI.Instance.SetRedMode(true);

  // ปิดโหมดสีแดง (กลับเป็นสีขาว)
  GameplayHUDManager.SetRoomRedMode(false);
  // หรือ
  RoomCounterUI.Instance.SetRedMode(false);

  // สลับสถานะเปิด/ปิด
  GameplayHUDManager.ToggleRoomRedMode();
  ```

### การเปลี่ยนสีตัวเลขตามชั้นสำคัญ (Milestone Colors)
- มีตัวเลือก `Enable Milestone Colors` (ค่าเริ่มต้น: ปิดไว้ เพื่อคงสีขาวตามปกติ)
- เมื่อต้องการใช้งานในอนาคต สามารถติ๊กเปิดและกำหนดชั้นสำคัญได้:

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

## 🔦 3. ระบบสถานะไฟฉาย (FlashlightUI - Pure Icon Only)

### หน้าตาและการทำงาน
- แสดงผลอยู่ **มุมขวาล่าง** สไตล์ **Pure Icon Only** (ตรงตามภาพ Screenshot ที่จัดวางไว้):
  - **ไอคอนไฟฉายเพียวๆ (Flashlight Icon)**: 
    - ตอนเปิด (`ON`): ไอคอนไฟฉายสว่างคมชัด สีขาว พร้อมมีลำแสงส่องสว่างออกมา
    - ตอนปิด (`OFF`): ไอคอนไฟฉายดับลง ลำแสงหายไป และหรี่โปร่งแสง (`alpha 0.35`)
  - **ไม่มีข้อความและไม่มีกล่องพื้นหลัง**: สะอาดตา คลีน มินิมอล 100%
  - **กดคลิกที่ไอคอนบนจอได้โดยตรง** หรือกดปุ่มคีย์บอร์ด

### การนำไปใส่ในไอคอนที่คุณจัดวางไว้แล้ว (Plug & Play)
- หากคุณสร้าง Image ไอคอนไฟฉายไว้ใน Canvas อยู่แล้วเหมือนในภาพ:
  1. ลากสคริปต์ [FlashlightUI.cs](file:///c:/Git%20Not%20OneDrive/Horror-Game-Jam-Project/Assets/FOR%20GAYJAM/NON/Non%20import/FlashlightUI.cs) ไปแปะที่ GameObject ไอคอนนั้นได้ทันที
  2. ระบบจะดึงคอมโพเนนต์ `Image` และค้นหาไฟฉายในฉาก (`Flashlight'` หรือ `Light2D` ใต้ Player) ให้อัตโนมัติ
  3. ปรับแต่งปุ่มกด หรือเลือก Sprite ตอนเปิด/ปิด ได้อิสระผ่าน Inspector

### การควบคุมไฟฉาย (Controls)
- ปุ่มเปิด/ปิดเริ่มต้น: **ปุ่ม `T`** (สามารถเปลี่ยนเป็นปุ่มอื่นได้ใน Inspector ช่อง `Toggle Key` เช่น `F`, `R` หรือ `G`)
- **คลิกเมาส์ที่ตัวไอคอนไฟฉายบนหน้าจอ**: คลิกเปิด/ปิดได้ทันที
- คลิกขวา (Right Click): สามารถเปิดใช้งานได้ใน Inspector (`Allow Right Click = true`)
- **ค้นหาไฟฉายให้อัตโนมัติ**: ระบบจะตรวจหา GameObject ชื่อ `Flashlight'` หรือ `Light2D` ในตัว Player และสั่งเปิด-ปิดไฟจริงในฉากทันที
- มีเสียงสวิตช์คลิกแบบ tactile (`E_Interact_HorrorClick.wav`) ทั้งตอนเปิดและปิด
- มีแอนิเมชันเด้งดึ๋งเล็กน้อย (Tactile Punch Bounce) สเกล 1.25x สวยงามตอบสนองนิ้วผู้เล่น

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
