using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// สคริปต์ควบคุมไอคอนไฟฉายบนหน้าจอ (Icon-Only Flashlight HUD)
/// - ใช้งานง่ายสุดๆ: เพียงลากสคริปต์นี้ใส่ GameObject ไอคอนไฟฉาย (Image) ใน Canvas ก็พร้อมใช้งานทันที
/// - รองรับการกดปุ่มคีย์บอร์ด (เช่น ปุ่ม T หรือคลิกขวา) และสามารถคลิกที่ตัวไอคอนไฟฉายบนจอได้โดยตรง
/// - ตรวจหาไฟฉายในฉาก (GameObject ชื่อ "Flashlight'" หรือ Light2D ใต้ Player) ให้อัตโนมัติ
/// - ปรับเปลี่ยนหน้าตาไอคอนเมื่อ เปิด (สว่าง/มีลำแสง) หรือ ปิด (หรี่ลง/โปร่งแสง) อย่างสวยงาม
/// </summary>
[RequireComponent(typeof(Image))]
[AddComponentMenu("Horror UI/Flashlight UI (Icon Only)")]
public class FlashlightUI : MonoBehaviour, IPointerClickHandler
{
    public static FlashlightUI Instance { get; private set; }

    [Header("=== Flashlight Icon (องค์ประกอบภาพ) ===")]
    [Tooltip("Image ไอคอนไฟฉาย (ระบบจะดึงจากตัวมันเองให้อัตโนมัติ)")]
    public Image flashlightIcon;

    [Tooltip("รูปไอคอนไฟฉายตอนเปิด (ON) - มีลำแสงสว่าง")]
    public Sprite iconOnSprite;

    [Tooltip("รูปไอคอนไฟฉายตอนปิด (OFF) - ลำแสงดับ (ถ้าไม่ใส่จะใช้การหรี่สี/ความโปร่งแสงแทนอัตโนมัติ)")]
    public Sprite iconOffSprite;

    [Header("=== Icon Colors (สีและความโปร่งแสง) ===")]
    [Tooltip("สีของไอคอนตอนเปิด (ON) - สว่างเต็มที่")]
    public Color onColor = Color.white;

    [Tooltip("สีของไอคอนตอนปิด (OFF) - หรี่มืดลงหรือโปร่งแสง")]
    public Color offColor = new Color(0.6f, 0.6f, 0.65f, 0.35f);

    [Header("=== In-Game Light Target (เป้าหมายไฟฉายในเกม) ===")]
    [Tooltip("GameObject ไฟฉายในฉาก (ถ้าเว้นว่างไว้ ระบบจะค้นหา GameObject ชื่อ 'Flashlight\'' หรือใต้ตัว Player ให้อัตโนมัติ)")]
    public GameObject targetLightObject;

    [Tooltip("คอมโพเนนต์แสง (Light2D หรือ Light) - ถ้ามีจะเปิด/ปิดแสงที่ตัวนี้โดยตรง")]
    public Behaviour targetLightComponent;

    [Tooltip("สถานะเริ่มต้นเมื่อเริ่มเกม (เปิดหรือปิด)")]
    public bool isFlashlightOn = true;

    [Header("=== Controls & Input (การควบคุม) ===")]
    [Tooltip("ปุ่มคีย์บอร์ดสำหรับกดเปิด/ปิดไฟฉาย (ค่าเริ่มต้น: KeyCode.T เพื่อไม่ให้ชนกับปุ่ม F ที่ใช้ Dash)")]
    public KeyCode toggleKey = KeyCode.T;

    [Tooltip("อนุญาตให้คลิกขวา (Mouse Right Click) เพื่อเปิด/ปิดไฟฉายได้ด้วยหรือไม่")]
    public bool allowRightClick = false;

    [Tooltip("อนุญาตให้คลิกเมาส์ที่ตัวไอคอนไฟฉายบนหน้าจอเพื่อเปิด/ปิดไฟฉาย")]
    public bool allowClickOnIcon = true;

    [Header("=== Tactile Punch Animation (เด้งดึ๋งเมื่อกด) ===")]
    [Tooltip("เปิดแอนิเมชันขยายเด้งดึ๋งเล็กน้อยเมื่อสลับสถานะไฟฉาย")]
    public bool enablePunchAnimation = true;
    public float punchScale = 1.25f;
    public float punchDuration = 0.18f;

    [Header("=== Audio Feedback (เสียงคลิกสวิตช์) ===")]
    [Tooltip("เสียงคลิกตอนเปิดไฟฉาย")]
    public AudioClip switchOnSound;

    [Tooltip("เสียงคลิกตอนปิดไฟฉาย")]
    public AudioClip switchOffSound;

    [Header("=== Events ===")]
    [Tooltip("Event ที่จะส่งค่าสถานะไฟฉาย (true = เปิด, false = ปิด) ออกไปให้ระบบอื่นใช้งาน")]
    public UnityEvent<bool> onFlashlightToggled;

    // สถานะภายใน
    private AudioSource audioSource;
    private Coroutine punchCoroutine;
    private Vector3 initialScale = Vector3.one;

    public bool IsOn => isFlashlightOn;

    void Awake()
    {
        // กำหนด Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            // หากมีชิ้นอื่นอยู่แล้ว ให้ลบตัวซ้ำออก
            Destroy(gameObject);
            return;
        }

        // ดึง Image อัตโนมัติ
        if (flashlightIcon == null)
        {
            flashlightIcon = GetComponent<Image>();
        }

        if (flashlightIcon != null)
        {
            initialScale = flashlightIcon.transform.localScale;

            // หากยังไม่ได้ตั้ง iconOnSprite ให้จำ sprite ปัจจุบันไว้
            if (iconOnSprite == null && flashlightIcon.sprite != null)
            {
                iconOnSprite = flashlightIcon.sprite;
            }
        }

        // ตั้งค่า AudioSource
        SetupAudio();

        // โหลด Resource พื้นฐานอัตโนมัติหากยังไม่มี
        AutoLoadDefaultResources();

        // ค้นหาเป้าหมายแสงไฟฉายในฉาก
        FindInGameLightTarget();
    }

    void Start()
    {
        // ปรับหน้าตาและสถานะเริ่มต้น
        UpdateVisuals(false);
        ApplyLightState();
    }

    void Update()
    {
        HandleKeyboardInput();
    }

    /// <summary>
    /// รับการคลิกที่ตัวไอคอนบนหน้าจอโดยตรงผ่าน IPointerClickHandler
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!allowClickOnIcon) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            ToggleFlashlight();
        }
    }

    private void HandleKeyboardInput()
    {
        bool triggered = false;

        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
        {
            triggered = true;
        }

        if (allowRightClick && Input.GetMouseButtonDown(1))
        {
            triggered = true;
        }

        if (triggered)
        {
            ToggleFlashlight();
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
    /// [API] สั่งเปิดหรือปิดไฟฉายตามค่า boolean ที่กำหนด
    /// </summary>
    public void SetFlashlight(bool state)
    {
        // ตรวจหาเป้าหมายแสงอีกครั้งเผื่อกรณี Player เพิ่ง Spawn
        if (targetLightObject == null && targetLightComponent == null)
        {
            FindInGameLightTarget();
        }

        isFlashlightOn = state;

        // 1. ควบคุมแสงไฟในเกม
        ApplyLightState();

        // 2. อัปเดตรูปลักษณ์ไอคอน
        UpdateVisuals(true);

        // 3. เล่นเสียงคลิกสวิตช์
        PlaySwitchSound(isFlashlightOn);

        // 4. ส่ง Event ให้ระบบภายนอก
        onFlashlightToggled?.Invoke(isFlashlightOn);
    }

    #endregion

    /// <summary>
    /// อัปเดตสี รูปร่าง และแอนิเมชันของไอคอน
    /// </summary>
    public void UpdateVisuals(bool animate)
    {
        if (flashlightIcon == null) return;

        // สลับ Sprite (ถ้ามี sprite แยก On/Off)
        Sprite targetSprite = isFlashlightOn ? iconOnSprite : (iconOffSprite != null ? iconOffSprite : iconOnSprite);
        if (targetSprite != null)
        {
            flashlightIcon.sprite = targetSprite;
        }

        // ปรับเปลี่ยนสีหรือความโปร่งแสง
        flashlightIcon.color = isFlashlightOn ? onColor : offColor;

        // เล่นแอนิเมชันเด้งดึ๋ง
        if (animate && enablePunchAnimation && gameObject.activeInHierarchy)
        {
            if (punchCoroutine != null) StopCoroutine(punchCoroutine);
            punchCoroutine = StartCoroutine(DoPunchScale());
        }
    }

    private void ApplyLightState()
    {
        if (targetLightComponent != null)
        {
            targetLightComponent.enabled = isFlashlightOn;
        }
        if (targetLightObject != null)
        {
            targetLightObject.SetActive(isFlashlightOn);

            Behaviour[] lightBehaviours = targetLightObject.GetComponentsInChildren<Behaviour>(true);
            foreach (var b in lightBehaviours)
            {
                if (b != null && (b.GetType().Name.Contains("Light2D") || b.GetType().Name == "Light"))
                {
                    b.enabled = isFlashlightOn;
                }
            }
        }

        // Sync with player's FLASHLIGHT component if present
        FLASHLIGHT fl = targetLightObject != null ? targetLightObject.GetComponentInParent<FLASHLIGHT>() : FindObjectOfType<FLASHLIGHT>();
        if (fl != null && fl.IsOn != isFlashlightOn)
        {
            fl.SetLightStateDirect(isFlashlightOn);
        }
    }

    private IEnumerator DoPunchScale()
    {
        if (flashlightIcon == null) yield break;

        float half = punchDuration * 0.5f;
        float elapsed = 0f;
        Vector3 target = initialScale * punchScale;

        // ขยายออก
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            flashlightIcon.transform.localScale = Vector3.Lerp(initialScale, target, elapsed / half);
            yield return null;
        }

        // หดกลับเท่าเดิม
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            flashlightIcon.transform.localScale = Vector3.Lerp(target, initialScale, elapsed / half);
            yield return null;
        }

        flashlightIcon.transform.localScale = initialScale;
    }

    /// <summary>
    /// ค้นหา GameObject หรือ Light2D ของไฟฉายในฉากให้อัตโนมัติ
    /// </summary>
    public void FindInGameLightTarget()
    {
        if (targetLightObject != null || targetLightComponent != null) return;

        // 1. ค้นหา GameObject ชื่อ "Flashlight'" หรือ "Flashlight"
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

        // 3. ตรวจหา Light2D / Light component
        if (targetLightObject != null)
        {
            Behaviour[] behaviours = targetLightObject.GetComponents<Behaviour>();
            foreach (var b in behaviours)
            {
                if (b != null && (b.GetType().Name.Contains("Light2D") || b.GetType().Name == "Light"))
                {
                    targetLightComponent = b;
                    break;
                }
            }
        }
    }

    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void AutoLoadDefaultResources()
    {
        if (iconOnSprite == null)
        {
            iconOnSprite = Resources.Load<Sprite>("FloatingPrompt/Flashlight_Icon_On");
        }
        if (iconOffSprite == null)
        {
            iconOffSprite = Resources.Load<Sprite>("FloatingPrompt/Flashlight_Icon_Off");
        }
        if (switchOnSound == null)
        {
            switchOnSound = Resources.Load<AudioClip>("FloatingPrompt/E_Interact_HorrorClick");
        }
        if (switchOffSound == null)
        {
            switchOffSound = Resources.Load<AudioClip>("FloatingPrompt/E_Focus_SubtleTick");
        }
    }

    private void PlaySwitchSound(bool on)
    {
        AudioClip clip = on ? switchOnSound : switchOffSound;
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, 0.65f);
        }
    }
}
