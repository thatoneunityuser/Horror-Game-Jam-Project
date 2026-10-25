# คู่มือระบบปุ่ม E ลอยได้ ฉบับ OOP & Instant Plug-and-Play
### (Universal Floating Interact E Button Guide - OOP Architecture)
*พร้อมใช้งานได้ทันทีกับทุก GameObject ในทุกโปรเจกต์ Unity (Zero-Setup & Fully Customizable)*

---

## 🌟 จุดเด่นของเวอร์ชันใหม่ (New Features)

1. **Plug-and-Play ใช้งานได้ทันที 100% (Instant Use)**:
   - ลากสคริปต์ `FloatingInteractPrompt` ไปแปะที่ GameObject ใดๆ ในเกมได้ทันที **ไม่ต้องสร้าง Prefab หรือ Canvas ล่วงหน้า**
   - ระบบจะ **Auto-Generate** ปุ่ม Badge ทรงกลมลอยได้ พร้อมตัวอักษร "E" (TextMeshPro) คมชัดระดับโปร ให้เองอัตโนมัติ
   - **ไม่ต้องมี Collider ก็ทำงานได้** ด้วยโหมด `PlayerDistance` (ตรวจจับระยะอัตโนมัติ)
2. **ระบบ Single-Active Prompt (แสดงผลปุ่มเดียว แม้วัตถุจะอยู่ชิดกัน)**:
   - หากมีวัตถุ 2 ชิ้นขึ้นไปวางอยู่ข้างกันในระยะของผู้เล่น **ระบบจะแสดงปุ่ม E ให้เพียงปุ่มเดียวที่ใกล้ที่สุดเท่านั้น**
   - ป้องกัน UI ซ้อนทับกัน และป้องกันบั๊กกดปุ่ม E ครั้งเดียวแล้วไปสั่งงาน 2 วัตถุพร้อมกัน
   - สลับปุ่มไปยังอีกวัตถุให้อัตโนมัติเมื่อผู้เล่นขยับเข้าใกล้อีกฝั่ง หรือเลื่อนเมาส์ไปชี้
   - มีช่อง `interactionPriority` ให้กำหนดลำดับความสำคัญของวัตถุได้
3. **สถาปัตยกรรม OOP เต็มรูปแบบ (True OOP Architecture)**:
   - มี `IInteractable` (Interface) สำหรับ Interface Segregation
   - มี `InteractableBase` (Base Class) จัดการ Lifecycle, Cooldown, Single-use, Focus State
   - มี `FloatingInteractPrompt` (Visual & Physics Class) จัดการ Spring Physics, Sine Wave, Parallax Follow, World-Space Canvas
   - มี **Virtual Methods** (`CanInteract()`, `OnInteract()`, `OnFocusEnter()`, `OnFocusExit()`, `OnInteractFailed()`) ให้สคริปต์อื่นสามารถ **Inherit (สืบทอด)** ไปใส่ Logic ของตัวเองได้ง่ายๆ
4. **ปรับแต่งได้ทุกอย่างผ่าน Inspector (Fully Adjustable)**:
   - ปรับตัวอักษร (เช่น `E`, `F`, `SPACE`, `TALK`, `OPEN`), ขนาดฟอนต์, สีปุ่ม, สีขอบ, ขนาด Badge
   - ปรับตำแหน่งลอย (`promptOffset`), ระยะตรวจจับ (`interactionDistance`), ความสูงแกน Y (`maxVerticalDistance`)
   - ปรับความเด้งของสปริง (`bounceSpring`, `bounceDamp`, `squashScale`)
   - ปรับการขยับตามเมาส์ (`enableMouseParallax`) และการลอยพริ้ว (`floatAmplitude`, `floatSpeed`)
   - ใส่เสียงตอนโผล่ (`focusSound`) และเสียงตอนกด (`interactSound`)

---

## 🏗️ 1. สถาปัตยกรรม OOP (OOP Architecture Overview)

```
       <<interface>>
       IInteractable
       [CanInteract(), Interact(), OnFocusEnter(), OnFocusExit()]
             ▲
             │ (Implements)
     InteractableBase  (Abstract Class)
     - interactKey, isSingleUse, interactCooldown
     - onInteract, onFocusEnter, onFocusExit (UnityEvents)
     - Virtual methods: CanInteract(), OnInteract(), OnInteractFailed()
             ▲
             │ (Inherits)
   FloatingInteractPrompt  (Concrete & Extensible Class)
   - Auto-generates World-Space Badge (Canvas + TMPro)
   - Spring Pop-up & Squash Physics
   - Sine wave floating & Mouse Parallax
   - Multiple detection modes (Distance, Hover, Trigger)
             ▲
             │ (Can be inherited by your custom game objects!)
   ┌─────────┴─────────────┐
   │                       │
DoorInteractable       ChestInteractable / NPC / WaterMachine ...
```

---

## 🚀 2. วิธีการใช้งานทันที (Instant Plug-and-Play)

### แบบที่ 1: สาย No-Code (ใช้ UnityEvent ใน Inspector)
1. เลือก GameObject ใดๆ ในฉากของคุณ (เช่น ประตู, หีบ, ไอเทม, สวิตช์)
2. กดปุ่ม **Add Component** ใน Inspector แล้วเลือก **`FloatingInteractPrompt`**
3. **เสร็จสิ้น!** ทดลองกด Play ได้เลย ตัวปุ่ม E จะลอยขึ้นมาอัตโนมัติเมื่อผู้เล่นเดินเข้าใกล้ และเมื่อกด E จะมีแอนิเมชันเด้งดึ๋ง
4. ในช่อง **On Interact ()**: กดเครื่องหมาย `+` เพื่อลากฟังก์ชันที่คุณต้องการให้ทำงานตอนกด E มาใส่

---

### แบบที่ 2: สาย OOP Developer (เขียนสคริปต์สืบทอด Inheritance)
สร้างสคริปต์ C# ใหม่ แล้วเปลี่ยนจาก `: MonoBehaviour` เป็น `: FloatingInteractPrompt` จากนั้น Override ฟังก์ชันที่ต้องการ:

```csharp
using UnityEngine;

public class MyDoor : FloatingInteractPrompt
{
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private bool requiresKey = true;
    public bool hasKey = false;

    // 1. [OOP Override] กำหนดเงื่อนไขว่ากดได้มั้ย (ถ้า return false ปุ่มจะไม่ทำงาน)
    public override bool CanInteract()
    {
        if (!base.CanInteract()) return false;
        if (requiresKey && !hasKey) return false;
        return true;
    }

    // 2. [OOP Override] ใส่คำสั่งเมื่อผู้เล่นกดปุ่มสำเร็จ
    public override void OnInteract()
    {
        base.OnInteract(); // เรียกแอนิเมชันปุ่มบีบตัวเด้ง (Squash) + เสียงกด
        doorAnimator.SetTrigger("OpenDoor");
        Debug.Log("Door opened successfully!");
    }

    // 3. [OOP Override] ใส่คำสั่งเมื่อกดแต่เงื่อนไขไม่ผ่าน (เช่น ประตูล็อค)
    public override void OnInteractFailed()
    {
        base.OnInteractFailed();
        Debug.Log("Door is locked! Key required.");
    }
}
```

---

## ⚙️ 3. คำอธิบายการปรับแต่งใน Inspector (Inspector Settings Reference)

| หมวดหมู่ | ตัวแปร | ค่าตั้งต้น (Default) | คำอธิบาย |
|---|---|---|---|
| **Single-Active** | `enableSinglePromptOnly` | `true` | จำกัดให้แสดงผลเพียงปุ่มเดียวในฉาก แม้จะมีหลายวัตถุอยู่ข้างกัน (เลือกตัวที่ใกล้สุดหรือชี้เมาส์) |
| | `interactionPriority` | `0` | ลำดับความสำคัญของวัตถุ (Priority สูงกว่าจะแสดงผลก่อนแม้ระยะจะใกล้เคียงกัน) |
| **Detection Mode** | `detectionMode` | `PlayerDistance` | รูปแบบตรวจจับ: เดินเข้าใกล้ (`PlayerDistance`), เอาเมาส์ชี้ (`MouseHover`), ชนทริกเกอร์ (`TriggerCollider`), หรือทั้งสองอย่าง (`DistanceOrMouseHover`) |
| | `playerTag` | `"Player"` | Tag ของผู้เล่นที่ระบบจะค้นหาอัตโนมัติ |
| | `interactionDistance` | `2.5` | ระยะห่างสูงสุดที่สามารถกดได้ (เมตร) |
| | `useVerticalDistanceLimit` | `true` | จำกัดระยะสูงต่ำแกน Y ป้องกันการกดข้ามชั้นบันได |
| | `maxVerticalDistance` | `2.5` | ระยะความสูงแกน Y สูงสุดที่อนุญาต |
| **Visual Appearance** | `promptText` | `"E"` | ตัวหนังสือบนปุ่ม (แก้เป็น `"F"`, `"SPACE"`, `"[E] TALK"`, `"OPEN"` ได้) |
| | `promptFontSize` | `48` | ขนาดตัวอักษร |
| | `promptTextColor` | `White` | สีของตัวอักษร |
| | `badgeBackgroundColor` | ดำกึ่งโปร่งใส | สีพื้นหลังของปุ่ม Badge |
| | `badgeOutlineColor` | ขาวกึ่งโปร่งใส | สีขอบของปุ่ม Badge |
| | `badgeScale` | `1.0` | ขนาดโดยรวมของปุ่มลอย |
| | `promptOffset` | `(0, 1.5, 0)` | ตำแหน่งลอยเทียบกับวัตถุ (ปรับ Y เพื่อยกให้สูงขึ้น) |
| | `customPromptPrefab` | `None` | (ทางเลือก) ถ้าต้องการใช้ Prefab ส่วนตัว ให้ลากมาใส่ช่องนี้ |
| **Hover & Highlight** | `hoverSprite` | `None` | รูป Sprite ที่จะเปลี่ยนเมื่อเข้าใกล้/ชี้ |
| | `useHoverColor` | `false` | เปลี่ยนสี Tint วัตถุตอน Focus หรือไม่ |
| **Dynamic Animation** | `enableMouseParallax` | `true` | เปิด/ปิด ให้ปุ่มลอยเอียงตามเคอร์เซอร์เมาส์ |
| | `maxFollowDistance` | `0.4` | ระยะสูงสุดที่ปุ่มจะพุ่งตามเมาส์ |
| | `followSpeed` | `10` | ความลื่นไหลในการขยับตามเมาส์ |
| | `floatAmplitude` | `0.12` | ความสูงคลื่นลอยขึ้นลง |
| | `floatSpeed` | `3.0` | ความเร็วในการลอยขึ้นลง |
| **Spring Pop-up** | `bounceSpring` | `150` | ความเด้งของสปริงตอนปุ่มโผล่ (ยิ่งเยอะยิ่งดีดเร็ว) |
| | `bounceDamp` | `10` | ความหนืดสปริง (ยิ่งน้อยยิ่งดึ๋งหลายรอบ) |
| | `squashScale` | `0.6` | สเกลบีบตัวตอนกดปุ่ม (เช่น 0.6 = ย่อเหลือ 60% แล้วเด้งคืน) |
| **Audio Feedback** | `interactSound` | `None` | เสียง Sound Effect ตอนกด Interact สำเร็จ |
| | `focusSound` | `None` | เสียงตอนปุ่ม E เด้งโผล่ขึ้นมา |
| **3D Settings** | `billboardToCamera` | `true` | ให้ปุ่มหันหน้าเข้าหากล้องตลอดเวลาในฉาก 3D |

---

## 🎨 4. ตัวอย่างการใช้งานจริงในเกม (Common Use Cases)

### กรณีที่ 1: หีบสมบัติ (Chest) — กดได้ครั้งเดียว
- ตั้งค่า `isSingleUse = true`
- ใน Inspector ช่อง `On Interact ()`: ลากฟังก์ชันเปิดหีบ และเพิ่มคะแนน/ไอเทมเข้ามาใส่
- เมื่อกดแล้ว ปุ่ม E จะหดตัวหายไปและไม่แสดงขึ้นมาอีก

### กรณีที่ 2: วัตถุที่ใช้เมาส์คลิกหรือชี้ (Mouse Hover Target)
- เปลี่ยน `detectionMode = MouseHover`
- ติดตั้ง `Collider2D` หรือ `Collider` ที่วัตถุนั้น
- เมื่อเลื่อนเคอร์เซอร์เมาส์มาชี้ ปุ่ม E จะเด้งขึ้นมา และเอียงพริ้วตามเมาส์

### กรณีที่ 3: NPC สนทนา
- ตั้งค่า `promptText = "TALK"` หรือ `"[E] TALK"`
- ปรับ `promptOffset = (0, 2.2, 0)` ให้อยู่เหนือศีรษะ NPC
- เมื่อเดินเข้าใกล้ จะเห็นปุ่มลอยคำว่า TALK สวยงาม
