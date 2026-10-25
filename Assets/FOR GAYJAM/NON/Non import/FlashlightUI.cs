using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// UI แสดงสถานะไฟฉาย (Flashlight Status UI)
/// - แสดงผลสถานะ ON / OFF เรียบง่าย มินิมอล ชัดเจน (Clean & Minimalist)
/// - ไม่ต้องมีหลอดเลือด/หลอดแบตเตอรี่ ตามที่ต้องการ
/// - รองรับการกดปุ่มเปิด-ปิดไฟฉายในเกม (Toggle Key)
/// - ควบคุมการเปิด-ปิด Light2D หรือ GameObject ไฟฉายในฉากให้อัตโนมัติ
/// </summary>
public class FlashlightUI : MonoBehaviour
{
    public static FlashlightUI Instance { get; private set; }

    [Header("=== Light Target Control ===")]
    [Tooltip("GameObject ของไฟฉาย (ถ้าเว้นว่างไว้ ระบบจะค้นหา GameObject ชื่อ Flashlight' ให้อัตโนมัติ)")]
    public GameObject targetLightObject;

    [Tooltip("คอมโพเนนต์ Light2D (ถ้ามี ระบบจะเปิด/ปิด Light2D ให้อัตโนมัติ)")]
    public Behaviour targetLight2D;

    [Tooltip("สถานะเริ่มต้นของไฟฉาย")]
    public bool isFlashlightOn = true;

    [Header("=== Controls & Input ===")]
    [Tooltip("ปุ่มคีย์บอร์ดสำหรับเปิด/ปิดไฟฉาย (ค่าเริ่มต้น: KeyCode.T หรือ KeyCode.R เพื่อไม่ชนกับ F ที่เป็น Dash)")]
    public KeyCode toggleKey = KeyCode.T;

    [Tooltip("อนุญาตให้คลิกขวา (Mouse Right Click) เพื่อเปิด/ปิดไฟฉายด้วยหรือไม่")]
    public bool allowRightClickToggle = false;

    [Header("=== UI Elements ===")]
    [Tooltip("ข้อความแสดงสถานะ 'ON' / 'OFF'")]
    public TextMeshProUGUI statusText;

    [Tooltip("หัวข้อกำกับ (เช่น 'FLASHLIGHT')")]
    public TextMeshProUGUI labelText;

    [Tooltip("จุดไฟ LED เล็กๆ แสดงสถานะ (สีสว่างตอน ON, สีมืดตอน OFF)")]
    public Image indicatorDot;

    [Header("=== Color Settings (Clean & Minimal) ===")]
    [Tooltip("สีข้อความสถานะตอนไฟฉายเปิด (ON)")]
    public Color onTextColor = new Color(0.95f, 0.98f, 1f, 1f);

    [Tooltip("สีข้อความสถานะตอนไฟฉายปิด (OFF)")]
    public Color offTextColor = new Color(0.45f, 0.45f, 0.5f, 0.65f);

    [Tooltip("สีจุดไฟ LED ตอนเปิด (ON)")]
    public Color onDotColor = new Color(0.3f, 1f, 0.5f, 1f);

    [Tooltip("สีจุดไฟ LED ตอนปิด (OFF)")]
    public Color offDotColor = new Color(0.85f, 0.2f, 0.2f, 0.5f);

    [Header("=== Audio Feedback (Optional) ===")]
    [Tooltip("เสียงคลิกสวิตช์ตอนเปิดไฟฉาย")]
    public AudioClip toggleOnSound;

    [Tooltip("เสียงคลิกสวิตช์ตอนปิดไฟฉาย")]
    public AudioClip toggleOffSound;

    [Header("=== Events ===")]
    public UnityEvent<bool> onFlashlightToggled;

    private AudioSource audioSource;

    public bool IsOn => isFlashlightOn;

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

        if (statusText == null)
        {
            statusText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (statusText == null)
        {
            GameObject stGo = new GameObject("Status_Text");
            stGo.transform.SetParent(transform, false);
            statusText = stGo.AddComponent<TextMeshProUGUI>();
            statusText.fontSize = 24;
            statusText.fontStyle = FontStyles.Bold;
            statusText.alignment = TextAlignmentOptions.Left;
            statusText.text = isFlashlightOn ? "ON" : "OFF";
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        AutoFindFlashlightIfNeeded();
        AutoLoadDefaultSounds();
    }

    void Start()
    {
        UpdateVisuals(false);
        ApplyLightState();
    }

    void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        bool toggleTriggered = false;

        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
        {
            toggleTriggered = true;
        }

        if (allowRightClickToggle && Input.GetMouseButtonDown(1))
        {
            toggleTriggered = true;
        }

        if (toggleTriggered)
        {
            ToggleFlashlight();
        }
    }

    /// <summary>
    /// ค้นหา GameObject ไฟฉายของผู้เล่นในฉากอัตโนมัติหากยังไม่ได้ผูกไว้
    /// </summary>
    public void AutoFindFlashlightIfNeeded()
    {
        if (targetLightObject == null && targetLight2D == null)
        {
            // 1. ค้นหา GameObject ชื่อ Flashlight' หรือ Flashlight
            GameObject fl = GameObject.Find("Flashlight'");
            if (fl == null) fl = GameObject.Find("Flashlight");
            if (fl == null) fl = GameObject.Find("flashlight");

            if (fl != null)
            {
                targetLightObject = fl;
            }
            else
            {
                // 2. ค้นหาใต้ตัว Player
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    Transform t = player.transform.Find("Flashlight'");
                    if (t == null) t = player.transform.Find("Flashlight");
                    if (t != null)
                    {
                        targetLightObject = t.gameObject;
                    }
                }
            }

            // 3. ค้นหา Component Light2D
            if (targetLightObject != null)
            {
                // ตรวจหา Component ใดๆ ที่เป็น Light2D หรือ Light
                Behaviour[] behaviours = targetLightObject.GetComponents<Behaviour>();
                foreach (var b in behaviours)
                {
                    if (b != null && (b.GetType().Name.Contains("Light2D") || b.GetType().Name == "Light"))
                    {
                        targetLight2D = b;
                        break;
                    }
                }
            }
        }
    }

    private void AutoLoadDefaultSounds()
    {
        if (toggleOnSound == null)
        {
            toggleOnSound = Resources.Load<AudioClip>("FloatingPrompt/E_Interact_HorrorClick");
        }
        if (toggleOffSound == null)
        {
            toggleOffSound = Resources.Load<AudioClip>("FloatingPrompt/E_Focus_SubtleTick");
        }
    }

    #region Public APIs

    /// <summary>
    /// [API] สลับสถานะเปิด/ปิดไฟฉาย
    /// </summary>
    public void ToggleFlashlight()
    {
        SetFlashlight(!isFlashlightOn);
    }

    /// <summary>
    /// [API] สั่งเปิดหรือปิดไฟฉายตามค่าที่ส่งเข้ามา
    /// </summary>
    public void SetFlashlight(bool state)
    {
        if (isFlashlightOn == state) return;

        isFlashlightOn = state;

        // สั่งงานแสงจริงในฉาก
        ApplyLightState();

        // อัปเดตหน้าตา UI
        UpdateVisuals(true);

        // เล่นเสียงสวิตช์
        PlayToggleSound(isFlashlightOn);

        // ยิง Event ให้ระบบอื่นๆ รับรู้
        onFlashlightToggled?.Invoke(isFlashlightOn);
    }

    #endregion

    private void ApplyLightState()
    {
        if (targetLight2D != null)
        {
            targetLight2D.enabled = isFlashlightOn;
        }
        else if (targetLightObject != null)
        {
            targetLightObject.SetActive(isFlashlightOn);
        }
    }

    private void UpdateVisuals(bool animate)
    {
        if (statusText != null)
        {
            statusText.text = isFlashlightOn ? "ON" : "OFF";
            statusText.color = isFlashlightOn ? onTextColor : offTextColor;

            if (animate)
            {
                // ขยายเล็กน้อยตอนกดสลับสถานะ
                statusText.transform.localScale = Vector3.one * 1.2f;
                LeanTweenOrSimplePunch(statusText.gameObject);
            }
        }

        if (indicatorDot != null)
        {
            indicatorDot.color = isFlashlightOn ? onDotColor : offDotColor;
        }
    }

    private void LeanTweenOrSimplePunch(GameObject go)
    {
        // Simple coroutine-free punch bounce using scale reset
        if (go != null)
        {
            StopAllCoroutines();
            StartCoroutine(ScaleBackRoutine(go.transform));
        }
    }

    private System.Collections.IEnumerator ScaleBackRoutine(Transform t)
    {
        float elapsed = 0f;
        float dur = 0.2f;
        Vector3 start = t.localScale;
        Vector3 end = Vector3.one;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            t.localScale = Vector3.Lerp(start, end, elapsed / dur);
            yield return null;
        }
        t.localScale = end;
    }

    private void PlayToggleSound(bool on)
    {
        AudioClip clip = on ? toggleOnSound : toggleOffSound;
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, 0.6f);
        }
    }
}
