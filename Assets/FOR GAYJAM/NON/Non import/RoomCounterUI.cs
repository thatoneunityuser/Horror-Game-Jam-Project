using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// UI แสดงผลตัวเลขนับห้อง (Room Counter UI)
/// - แสดงผลเป็นตัวเลขเปล่าๆ ชัดเจน เรียบง่าย สไตล์มินิมอล (Clean & Minimalist)
/// - ค่าเริ่มต้นสีขาวล้วน (Pure White) ตามที่ต้องการ
/// - มีระบบเปลี่ยนสีตัวเลขอัตโนมัติเมื่อถึงชั้นสำคัญ (Milestone Colors)
/// - มี API ฟังก์ชัน SetNumberColor ให้สคริปต์อื่นเรียกเปลี่ยนสีตามแมคคานิกเกมได้ทันที
/// </summary>
public class RoomCounterUI : MonoBehaviour
{
    public static RoomCounterUI Instance { get; private set; }

    [Header("=== UI Reference ===")]
    [Tooltip("TextMeshProUGUI สำหรับแสดงตัวเลขห้อง")]
    public TextMeshProUGUI roomNumberText;

    [Header("=== Color & Style Settings ===")]
    [Tooltip("สีเริ่มต้นของตัวเลข (ค่าเริ่มต้น: สีขาวล้วน)")]
    public Color defaultColor = Color.white;

    [Tooltip("แสดงเลข 0 นำหน้าหรือไม่ เช่น 01, 02 (ถ้า false จะแสดง 1, 2)")]
    public bool showLeadingZero = false;

    [Tooltip("ตัวเลขที่จะบวกเพิ่มจาก PassedRoomCount (เช่น ถ้าห้องเริ่มนับที่ 1 ให้ใส่ 0 หรือ 1)")]
    public int roomOffset = 0;

    [Header("=== Milestone Room Colors (ชั้นสำคัญเปลี่ยนสีอัตโนมัติ) ===")]
    [Tooltip("รายการชั้นสำคัญที่ต้องการให้เปลี่ยนสีอัตโนมัติ (เช่น ชั้น 30, 34, 50)")]
    public List<MilestoneRoomColor> milestoneColors = new List<MilestoneRoomColor>()
    {
        new MilestoneRoomColor(30, new Color(1f, 0.25f, 0.25f, 1f), "Event Room 30 (Red)"),
        new MilestoneRoomColor(34, new Color(0.8f, 0.3f, 1f, 1f), "Event End 34 (Purple)"),
        new MilestoneRoomColor(50, new Color(1f, 0.85f, 0.2f, 1f), "Final Room 50 (Gold)")
    };

    [Header("=== Animation Settings ===")]
    [Tooltip("มีแอนิเมชันเด้งขยายเล็กน้อยตอนเปลี่ยนเลขห้องหรือไม่")]
    public bool enablePunchAnimation = true;
    public float punchScale = 1.35f;
    public float punchDuration = 0.25f;

    // สถานะภายใน
    private int currentRoomNumber = -1;
    private Color currentColor;
    private Coroutine flashCoroutine;
    private Coroutine punchCoroutine;
    private Vector3 originalScale = Vector3.one;

    public int CurrentRoomNumber => currentRoomNumber;
    public Color CurrentColor => currentColor;

    [System.Serializable]
    public struct MilestoneRoomColor
    {
        public int roomNumber;
        public Color color;
        public string label;

        public MilestoneRoomColor(int room, Color col, string desc)
        {
            roomNumber = room;
            color = col;
            label = desc;
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (roomNumberText == null)
        {
            roomNumberText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (roomNumberText == null)
        {
            GameObject textGo = new GameObject("RoomNumber_Text");
            textGo.transform.SetParent(transform, false);
            roomNumberText = textGo.AddComponent<TextMeshProUGUI>();
            roomNumberText.fontSize = 72;
            roomNumberText.fontStyle = FontStyles.Bold;
            roomNumberText.alignment = TextAlignmentOptions.TopRight;
            roomNumberText.outlineWidth = 0.15f;
            roomNumberText.outlineColor = new Color32(0, 0, 0, 220);

            RectTransform rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
        }

        if (roomNumberText != null)
        {
            originalScale = roomNumberText.transform.localScale;
            currentColor = defaultColor;
            roomNumberText.color = currentColor;
        }
    }

    void Start()
    {
        SyncWithRoomCounter();
    }

    void Update()
    {
        SyncWithRoomCounter();
    }

    /// <summary>
    /// อัปเดตและซิงค์ตัวเลขห้องจาก RoomCounter ในฉากอัตโนมัติ
    /// </summary>
    public void SyncWithRoomCounter()
    {
        int room = 0;
        if (RoomCounter.instance != null)
        {
            room = RoomCounter.instance.PassedRoomCount + roomOffset;
        }
        else
        {
            RoomCounter rc = FindObjectOfType<RoomCounter>();
            if (rc != null) room = rc.PassedRoomCount + roomOffset;
        }

        if (room != currentRoomNumber)
        {
            SetRoomNumber(room);
        }
    }

    /// <summary>
    /// กำหนดตัวเลขห้อง และตรวจสอบว่าตรงกับชั้นสำคัญที่ต้องเปลี่ยนสีหรือไม่
    /// </summary>
    public void SetRoomNumber(int room)
    {
        bool isInitial = (currentRoomNumber == -1);
        currentRoomNumber = room;

        // อัปเดตข้อความตัวเลข (เป็นเลขเปล่าๆ ตามที่ต้องการ)
        if (roomNumberText != null)
        {
            if (showLeadingZero && room < 10)
            {
                roomNumberText.text = $"0{room}";
            }
            else
            {
                roomNumberText.text = room.ToString();
            }
        }

        // ตรวจสอบสีประจำชั้นสำคัญ (Milestone Colors)
        Color milestoneCol = GetMilestoneColor(room);
        SetNumberColor(milestoneCol, false);

        // แอนิเมชัน Punch เมื่อตัวเลขขยับ (ไม่เล่นตอนเริ่มเกมครั้งแรก)
        if (!isInitial && enablePunchAnimation && gameObject.activeInHierarchy)
        {
            if (punchCoroutine != null) StopCoroutine(punchCoroutine);
            punchCoroutine = StartCoroutine(PunchRoutine());
        }
    }

    private Color GetMilestoneColor(int room)
    {
        if (milestoneColors != null)
        {
            for (int i = 0; i < milestoneColors.Count; i++)
            {
                if (milestoneColors[i].roomNumber == room)
                {
                    return milestoneColors[i].color;
                }
            }
        }
        return defaultColor;
    }

    #region Public APIs for Gameplay Mechanics (สำหรับเรียกใช้งานจากโค้ดอื่น)

    /// <summary>
    /// [API] เปลี่ยนสีตัวเลขทันที (เช่น เมื่อเจออีเวนต์สำคัญ, ห้องพิเศษ, บอส, หรือฉากหลอน)
    /// </summary>
    /// <param name="newColor">สีที่ต้องการให้เปลี่ยน</param>
    /// <param name="overrideDefault">ถ้า true จะบันทึกเป็นสีหลักใหม่</param>
    public void SetNumberColor(Color newColor, bool overrideDefault = false)
    {
        currentColor = newColor;
        if (overrideDefault)
        {
            defaultColor = newColor;
        }

        if (roomNumberText != null)
        {
            roomNumberText.color = currentColor;
        }
    }

    /// <summary>
    /// [API] รีเซ็ตสีกลับเป็นสีขาวเริ่มต้น
    /// </summary>
    public void ResetColor()
    {
        SetNumberColor(defaultColor, false);
    }

    /// <summary>
    /// [API] กะพริบสีชั่วคราวแล้วคืนค่าเดิม (เช่น กะพริบแดงตอนผีไล่ล่า หรือกะพริบเหลืองตอนผ่านชั้น)
    /// </summary>
    public void FlashColor(Color flashColor, float duration = 1.0f)
    {
        if (!gameObject.activeInHierarchy) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashColorRoutine(flashColor, duration));
    }

    private IEnumerator FlashColorRoutine(Color flashCol, float dur)
    {
        Color startCol = currentColor;
        if (roomNumberText != null) roomNumberText.color = flashCol;

        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            if (roomNumberText != null)
            {
                roomNumberText.color = Color.Lerp(flashCol, startCol, t);
            }
            yield return null;
        }

        if (roomNumberText != null) roomNumberText.color = startCol;
    }

    private IEnumerator PunchRoutine()
    {
        if (roomNumberText == null) yield break;

        float halfDur = punchDuration * 0.5f;
        float elapsed = 0f;

        Vector3 targetPunch = originalScale * punchScale;

        // ขยายออก
        while (elapsed < halfDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDur;
            roomNumberText.transform.localScale = Vector3.Lerp(originalScale, targetPunch, t);
            yield return null;
        }

        // ยุบกลับ
        elapsed = 0f;
        while (elapsed < halfDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDur;
            roomNumberText.transform.localScale = Vector3.Lerp(targetPunch, originalScale, t);
            yield return null;
        }

        roomNumberText.transform.localScale = originalScale;
    }

    #endregion
}
