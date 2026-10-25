using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

#region OOP Core Interfaces & Base Classes

/// <summary>
/// อินเทอร์เฟซสำหรับวัตถุทั้งหมดในเกมที่สามารถมีปฏิสัมพันธ์ (Interact) ได้
/// Interface for all interactable objects in the game.
/// </summary>
public interface IInteractable
{
    bool CanInteract();
    void Interact();
    void OnFocusEnter();
    void OnFocusExit();
}

/// <summary>
/// คลาสฐาน OOP สำหรับระบบ Interact ทุกประเภท
/// มี Virtual Methods ให้คลาสลูกนำไป Override เพื่อใส่ Logic เฉพาะตัวได้
/// Base OOP class providing interaction lifecycle, cooldowns, and virtual hooks.
/// </summary>
public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    [Header("=== Base Interaction Settings ===")]
    [Tooltip("ปุ่มที่ใช้กด Interact บนคีย์บอร์ด (Key required to interact)")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("สามารถกดได้ครั้งเดียวหรือไม่ (เช่น หีบสมบัติที่เปิดแล้วไม่ให้เปิดซ้ำ)")]
    public bool isSingleUse = false;

    [Tooltip("ระยะเวลา Cooldown ระหว่างการกดแต่ละครั้ง (วินาที)")]
    public float interactCooldown = 0.2f;

    [Header("=== Base Events (No-Code Hook) ===")]
    [Tooltip("Event ที่จะทำงานเมื่อผู้เล่นกด Interact สำเร็จ")]
    public UnityEvent onInteract;

    [Tooltip("Event ที่ทำงานเมื่อเป้าหมายถูก Focus (เมาส์ชี้ หรือ เดินเข้าใกล้)")]
    public UnityEvent onFocusEnter;

    [Tooltip("Event ที่ทำงานเมื่อเป้าหมายหลุด Focus")]
    public UnityEvent onFocusExit;

    // สถานะภายใน (Internal State)
    protected bool isFocused = false;
    protected bool hasBeenInteracted = false;
    protected float lastInteractTime = -999f;

    public bool IsFocused => isFocused;
    public bool HasBeenInteracted => hasBeenInteracted;

    /// <summary>
    /// ตรวจสอบเงื่อนไขว่าสามารถ Interact ได้หรือไม่ (Override เพื่อเพิ่มเงื่อนไข เช่น ต้องมีกุญแจ, เลือดไม่เต็ม ฯลฯ)
    /// </summary>
    public virtual bool CanInteract()
    {
        if (isSingleUse && hasBeenInteracted) return false;
        if (Time.time - lastInteractTime < interactCooldown) return false;
        return true;
    }

    /// <summary>
    /// สั่งให้เกิดการ Interact (พร้อมตรวจสอบ CanInteract และ Cooldown)
    /// </summary>
    public virtual void Interact()
    {
        if (!CanInteract())
        {
            OnInteractFailed();
            return;
        }

        lastInteractTime = Time.time;
        hasBeenInteracted = true;

        // เรียก Virtual Method สำหรับ OOP Subclass
        OnInteract();

        // เรียก UnityEvent สำหรับ Inspector
        onInteract?.Invoke();
    }

    /// <summary>
    /// ฟังก์ชันที่คลาสลูกนำไป Override เพื่อใส่ Logic เมื่อกดปุ่มสำเร็จ
    /// </summary>
    public virtual void OnInteract() { }

    /// <summary>
    /// ฟังก์ชันที่ทำงานเมื่อพยายามกด แต่เงื่อนไข CanInteract() ไม่ผ่าน (เช่น ของล็อค)
    /// </summary>
    public virtual void OnInteractFailed() { }

    /// <summary>
    /// ทำงานเมื่อเริ่ม Focus วัตถุ
    /// </summary>
    public virtual void OnFocusEnter()
    {
        if (isFocused) return;
        isFocused = true;
        onFocusEnter?.Invoke();
    }

    /// <summary>
    /// ทำงานเมื่อหลุด Focus จากวัตถุ
    /// </summary>
    public virtual void OnFocusExit()
    {
        if (!isFocused) return;
        isFocused = false;
        onFocusExit?.Invoke();
    }
}

#endregion

/// <summary>
/// สคริปต์ปุ่ม E ลอยได้ อเนกประสงค์ (Universal Floating Interact Prompt)
/// - นำไปแปะกับ GameObject ใดๆ ในเกมได้ทันที ใช้งานได้ทันที 100% ไม่ต้องเซ็ตอะไรเพิ่ม
/// - ระบบ Single-Active Prompt: แสดงผลเพียงปุ่มเดียว แม้จะมีหลายวัตถุวางอยู่ข้างกัน (เลือกตัวที่ใกล้ที่สุดอัตโนมัติ)
/// - สร้างปุ่ม E ให้อัตโนมัติ (Auto-generate) หากไม่ได้ใส่ Prefab
/// - รองรับ OOP: สคริปต์อื่นสามารถสืบทอด (Inherit) คลาสนี้ไปใส่ Logic เองได้
/// </summary>
public class FloatingInteractPrompt : InteractableBase
{
    public enum DetectionMode
    {
        PlayerDistance,         // ตรวจจับระยะห่างจากตัวผู้เล่นอัตโนมัติ (ไม่ต้องมี Collider)
        MouseHover,             // เมาส์ชี้ที่วัตถุ (ต้องมี Collider หรือ Collider2D)
        TriggerCollider,        // ผู้เล่นเดินชน Trigger (OnTriggerEnter2D/3D)
        DistanceOrMouseHover    // ทำงานทั้งสองแบบ: ตัวอยู่ใกล้ หรือ เมาส์ชี้ก็ได้
    }

    [Header("=== Single Prompt Exclusivity (แสดงผลปุ่มเดียว) ===")]
    [Tooltip("จำกัดให้แสดงผลปุ่มเพียงปุ่มเดียวในฉาก แม้จะมีหลาย Object อยู่ข้างกัน (เลือกตัวที่ใกล้สุดหรือชี้เมาส์)")]
    public static bool enableSinglePromptOnly = true;

    [Tooltip("ลำดับความสำคัญของวัตถุชิ้นนี้ (Priority สูงกว่าจะแสดงผลก่อนแม้ระยะจะใกล้เคียงกัน)")]
    public int interactionPriority = 0;

    [Header("=== Detection Mode ===")]
    [Tooltip("เลือกรูปแบบการตรวจจับ: ระยะเดินใกล้, เมาส์ชี้, ชน Trigger, หรือ ทั้งระยะ+เมาส์")]
    public DetectionMode detectionMode = DetectionMode.PlayerDistance;

    [Tooltip("Tag ของผู้เล่นสำหรับตรวจจับระยะ (Default: Player)")]
    public string playerTag = "Player";

    [Tooltip("ระยะห่างสูงสุดที่สามารถกดได้ (สำหรับโหมด PlayerDistance / DistanceOrMouseHover)")]
    public float interactionDistance = 2.5f;

    [Tooltip("จำกัดระยะความสูงแกน Y (ป้องกันการกดข้ามชั้นบันได/เพดาน)")]
    public bool useVerticalDistanceLimit = true;
    public float maxVerticalDistance = 2.5f;

    [Header("=== Visual & Prompt Appearance ===")]
    [Tooltip("ตัวหนังสือที่จะแสดงบนปุ่ม (เช่น 'E', 'F', 'SPACE', 'TALK', 'OPEN')")]
    public string promptText = "E";

    [Tooltip("ขนาดตัวอักษรของปุ่ม")]
    public float promptFontSize = 48f;

    [Tooltip("สีของตัวอักษร")]
    public Color promptTextColor = Color.white;

    [Tooltip("สีพื้นหลังของปุ่ม Badge")]
    public Color badgeBackgroundColor = new Color(0.12f, 0.12f, 0.16f, 0.88f);

    [Tooltip("สีขอบของปุ่ม Badge")]
    public Color badgeOutlineColor = new Color(1f, 1f, 1f, 0.5f);

    [Tooltip("สเกลขนาดรวมของปุ่มลอย")]
    public float badgeScale = 1.0f;

    [Tooltip("ตำแหน่งความสูง/เยื้องของปุ่มเทียบกับวัตถุ (X = ซ้ายขวา, Y = สูงต่ำ, Z = ลึก)")]
    public Vector3 promptOffset = new Vector3(0, 1.5f, 0);

    [Tooltip("(ไม่บังคับ) หากต้องการใช้ Prefab หรือ UI เฉพาะของคุณเอง ให้ลากมาใส่ช่องนี้ ถ้าเว้นว่างไว้ระบบจะสร้างปุ่ม E สวยงามให้อัตโนมัติ")]
    public GameObject customPromptPrefab;

    [Header("=== Hover / Sprite Highlight (Optional) ===")]
    [Tooltip("(ไม่บังคับ) รูป Sprite ที่ต้องการให้เปลี่ยนตอน Focus")]
    public Sprite hoverSprite;

    [Tooltip("ไฮไลท์เปลี่ยนสีวัตถุตอน Focus หรือไม่")]
    public bool useHoverColor = false;
    public Color hoverColor = new Color(1.2f, 1.2f, 1.0f, 1f);

    [Header("=== Floating & Dynamic Animation ===")]
    [Tooltip("เปิดให้ปุ่มขยับเอียงตามเคอร์เซอร์เมาส์ (Parallax Follow)")]
    public bool enableMouseParallax = true;

    [Tooltip("ระยะมากสุดที่ปุ่มจะเอียงตามเมาส์")]
    public float maxFollowDistance = 0.4f;

    [Tooltip("ความเร็วในการเลื่อนตามเมาส์")]
    public float followSpeed = 10f;

    [Tooltip("ระยะความสูงของการลอยพริ้วขึ้นลง (Sine Wave)")]
    public float floatAmplitude = 0.12f;

    [Tooltip("ความเร็วในการลอยพริ้วขึ้นลง")]
    public float floatSpeed = 3f;

    [Header("=== Pop-up Bounce (Spring Physics) ===")]
    [Tooltip("ความเด้งของสปริงตอนปุ่มโผล่ (ยิ่งเยอะยิ่งดีดเร็ว)")]
    public float bounceSpring = 150f;

    [Tooltip("ความหนืดของสปริง (ยิ่งน้อยยิ่งดึ๋งหลายรอบ)")]
    public float bounceDamp = 10f;

    [Tooltip("เปอร์เซ็นต์สเกลตอนถูกกด (Squash effect)")]
    [Range(0.1f, 0.9f)]
    public float squashScale = 0.6f;

    [Header("=== Audio Feedback (Optional) ===")]
    [Tooltip("เสียงตอนกด Interact")]
    public AudioClip interactSound;

    [Tooltip("เสียงตอนปุ่ม E เด้งโผล่ขึ้นมา")]
    public AudioClip focusSound;

    [Header("=== 3D Settings ===")]
    [Tooltip("หันหน้าเข้าหากล้องตลอดเวลา (แนะนำสำหรับเกม 3D)")]
    public bool billboardToCamera = true;

    // References ภายใน
    protected Camera mainCamera;
    protected Transform playerTransform;
    protected GameObject promptInstance;
    protected TextMeshProUGUI promptTMPText;
    protected SpriteRenderer spriteRenderer;
    protected Sprite originalSprite;
    protected Color originalColor = Color.white;

    // Animation physics state
    protected Vector3 originalScale = Vector3.one;
    protected Vector3 targetScale = Vector3.zero;
    protected Vector3 currentScale = Vector3.zero;
    protected Vector3 scaleVelocity = Vector3.zero;

    private bool isMouseHovering = false;
    private bool isTriggerColliding = false;
    private AudioSource audioSource;

    // ระบบจัดการการแสดงผลเพียงปุ่มเดียวระดับ Global (Single-Active Coordinator)
    private static readonly List<FloatingInteractPrompt> activeCandidates = new List<FloatingInteractPrompt>();
    private static FloatingInteractPrompt currentActivePrompt = null;
    public static FloatingInteractPrompt CurrentActivePrompt => currentActivePrompt;

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        CacheComponents();
        SetupPromptVisual();
    }

    protected virtual void Start()
    {
        FindPlayerReference();
    }

    protected virtual void Update()
    {
        UpdateDetection();
        HandleInteractionInput();
        HandlePromptAnimation();
    }

    protected virtual void OnDisable()
    {
        UnregisterFromCandidates();
    }

    protected virtual void OnDestroy()
    {
        UnregisterFromCandidates();
    }

    #endregion

    #region Setup & Initialization

    private void CacheComponents()
    {
        mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindObjectOfType<Camera>();

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalSprite = spriteRenderer.sprite;
            originalColor = spriteRenderer.color;
        }

        audioSource = GetComponent<AudioSource>();
    }

    private void FindPlayerReference()
    {
        if (playerTransform == null && !string.IsNullOrEmpty(playerTag))
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    /// <summary>
    /// จัดการสร้างหรือเตรียม Visual Prompt: ถ้าไม่มี custom prefab จะ auto-generate WorldSpace Badge ให้ทันที
    /// </summary>
    private void SetupPromptVisual()
    {
        if (customPromptPrefab != null)
        {
            // ถ้าเป็น prefab ใน Asset ให้ Instantiate แต่ถ้าเป็น GameObject ในฉากอยู่แล้วให้ใช้ตรงๆ
            if (customPromptPrefab.scene.rootCount == 0)
            {
                promptInstance = Instantiate(customPromptPrefab, transform.position + promptOffset, Quaternion.identity, transform);
            }
            else
            {
                promptInstance = customPromptPrefab;
            }
        }
        else
        {
            promptInstance = CreateAutoPromptBadge();
        }

        if (promptInstance != null)
        {
            promptTMPText = promptInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (promptTMPText != null)
            {
                promptTMPText.text = promptText;
            }

            originalScale = promptInstance.transform.localScale;
            if (originalScale.magnitude < 0.01f)
            {
                originalScale = Vector3.one;
            }

            currentScale = Vector3.zero;
            targetScale = Vector3.zero;
            scaleVelocity = Vector3.zero;
            promptInstance.transform.localScale = Vector3.zero;
            promptInstance.SetActive(false);
        }
    }

    /// <summary>
    /// Auto-Generate WorldSpace Canvas พร้อมปุ่ม Badge สวยงามระดับมืออาชีพ
    /// </summary>
    private GameObject CreateAutoPromptBadge()
    {
        GameObject canvasGo = new GameObject("_AutoPrompt_Badge_" + gameObject.name);
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = promptOffset;

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500; // วาดอยู่หน้า Sprite และ Layer อื่นๆ
        if (mainCamera != null) canvas.worldCamera = mainCamera;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(100f, 100f);
        canvasGo.transform.localScale = Vector3.one * (0.015f * badgeScale);

        // 1. พื้นหลังวงกลมมน (Background Badge)
        GameObject bgGo = new GameObject("BadgeBackground");
        bgGo.transform.SetParent(canvasGo.transform, false);
        Image bgImage = bgGo.AddComponent<Image>();
        bgImage.color = badgeBackgroundColor;
        bgImage.raycastTarget = false;

        // สร้าง Texture ทรงกลมนุ่มตาอัตโนมัติ (Anti-aliased circle)
        Texture2D circleTex = GenerateCircleTexture(64, 64);
        if (circleTex != null)
        {
            bgImage.sprite = Sprite.Create(circleTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
        }

        // เพิ่มขอบ (Outline) ให้ Badge ดูคมชัด
        Outline outline = bgGo.AddComponent<Outline>();
        outline.effectColor = badgeOutlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 2. ตัวอักษร TextMeshPro
        GameObject textGo = new GameObject("PromptText");
        textGo.transform.SetParent(canvasGo.transform, false);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = promptText;
        tmp.fontSize = promptFontSize;
        tmp.color = promptTextColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        return canvasGo;
    }

    private static Texture2D GenerateCircleTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float radius = width * 0.5f;
        Vector2 center = new Vector2(radius, radius);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01((radius - dist) + 0.5f);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    #endregion

    #region Detection & Global Single-Prompt Arbitration

    public bool CheckDetectionCriteria()
    {
        switch (detectionMode)
        {
            case DetectionMode.PlayerDistance:
                return CheckPlayerProximity();

            case DetectionMode.MouseHover:
                return isMouseHovering;

            case DetectionMode.TriggerCollider:
                return isTriggerColliding;

            case DetectionMode.DistanceOrMouseHover:
                return isMouseHovering || CheckPlayerProximity();
        }
        return false;
    }

    private void UpdateDetection()
    {
        bool inRange = CheckDetectionCriteria();
        bool isCandidate = inRange && CanInteract();

        if (enableSinglePromptOnly)
        {
            if (isCandidate)
            {
                if (!activeCandidates.Contains(this))
                {
                    activeCandidates.Add(this);
                }
            }
            else
            {
                if (activeCandidates.Contains(this))
                {
                    activeCandidates.Remove(this);
                }
            }

            // ตัดสินผู้ชนะเพียง 1 เดียวในบรรดา Candidates ทั้งหมด
            UpdateGlobalActivePrompt();
        }
        else
        {
            // โหมดเก่า (ถ้าปิด single prompt)
            if (isCandidate)
            {
                if (!isFocused) ShowPrompt();
            }
            else
            {
                if (isFocused) HidePrompt();
            }
        }
    }

    /// <summary>
    /// ระบบคัดเลือกวัตถุที่ดีที่สุดเพียงหนึ่งเดียว (Arbitration Coordinator)
    /// 1. วัตถุที่เมาส์ชี้อยู่ ชนะเป็นอันดับแรก
    /// 2. วัตถุที่มี Priority สูงกว่า ชนะ
    /// 3. วัตถุที่ใกล้ตัวผู้เล่นที่สุด ชนะ
    /// </summary>
    private static void UpdateGlobalActivePrompt()
    {
        // กวาดล้าง Candidates ที่หลุดระยะ, ปิดการทำงาน หรือ Interact ไม่ได้แล้ว
        for (int i = activeCandidates.Count - 1; i >= 0; i--)
        {
            var cand = activeCandidates[i];
            if (cand == null || !cand.isActiveAndEnabled || !cand.CanInteract() || !cand.CheckDetectionCriteria())
            {
                activeCandidates.RemoveAt(i);
            }
        }

        if (activeCandidates.Count == 0)
        {
            if (currentActivePrompt != null)
            {
                currentActivePrompt.HidePrompt();
                currentActivePrompt = null;
            }
            return;
        }

        FloatingInteractPrompt bestCandidate = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < activeCandidates.Count; i++)
        {
            var cand = activeCandidates[i];
            float score = cand.GetDistanceToPlayer();

            // ถ้าเมาส์ชี้อยู่ ได้แต้มความสำคัญสูงสุดทันที
            if (cand.isMouseHovering)
            {
                score -= 2000f;
            }

            // นำ Priority มาร่วมคำนวณ (1 priority = เทียบเท่าใกล้กว่า 10 เมตร)
            score -= (cand.interactionPriority * 10f);

            if (score < bestScore)
            {
                bestScore = score;
                bestCandidate = cand;
            }
        }

        if (currentActivePrompt != bestCandidate)
        {
            // ซ่อนปุ่มเดิมที่หลุดอันดับ
            if (currentActivePrompt != null)
            {
                currentActivePrompt.HidePrompt();
            }

            // แสดงผลปุ่มของผู้ชนะคนใหม่
            currentActivePrompt = bestCandidate;

            if (currentActivePrompt != null)
            {
                currentActivePrompt.ShowPrompt();
            }
        }
    }

    private void UnregisterFromCandidates()
    {
        if (activeCandidates.Contains(this))
        {
            activeCandidates.Remove(this);
        }

        if (currentActivePrompt == this)
        {
            HidePrompt();
            currentActivePrompt = null;
            UpdateGlobalActivePrompt();
        }
    }

    public float GetDistanceToPlayer()
    {
        if (playerTransform == null)
        {
            FindPlayerReference();
            if (playerTransform == null)
            {
                if (mainCamera != null) return Vector3.Distance(transform.position, mainCamera.transform.position);
                return 0f;
            }
        }
        return Vector3.Distance(transform.position, playerTransform.position);
    }

    private bool CheckPlayerProximity()
    {
        if (playerTransform == null)
        {
            FindPlayerReference();
            if (playerTransform == null) return false;
        }

        if (useVerticalDistanceLimit)
        {
            float verticalDiff = Mathf.Abs(playerTransform.position.y - transform.position.y);
            if (verticalDiff > maxVerticalDistance) return false;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        return distance <= interactionDistance;
    }

    private void HandleInteractionInput()
    {
        // ในระบบ Single-Active จะอนุญาตให้เฉพาะวัตถุที่เป็น Active ปัจจุบันเท่านั้นที่รับคำสั่งกดปุ่ม
        bool isEligibleToInteract = enableSinglePromptOnly ? (currentActivePrompt == this) : isFocused;

        if (isEligibleToInteract && CanInteract())
        {
            if (Input.GetKeyDown(interactKey))
            {
                Interact();
            }
        }
    }

    public override void Interact()
    {
        // แอนิเมชันบีบตัว (Squash) แล้วเด้งคืน
        currentScale = originalScale * squashScale;

        // เล่นเสียง
        PlayAudio(interactSound);

        // เรียก base logic (OnInteract virtual method & UnityEvent)
        base.Interact();

        // ตรวจสอบกรณีใช้ครั้งเดียว
        if (isSingleUse && hasBeenInteracted)
        {
            HidePrompt();
            UnregisterFromCandidates();
        }
    }

    #endregion

    #region Visual Feedback & Animation

    public void ShowPrompt()
    {
        OnFocusEnter();

        // เปลี่ยน Sprite หรือสี
        if (spriteRenderer != null)
        {
            if (hoverSprite != null) spriteRenderer.sprite = hoverSprite;
            if (useHoverColor) spriteRenderer.color = hoverColor;
        }

        PlayAudio(focusSound);

        if (promptInstance != null)
        {
            if (!promptInstance.activeSelf)
            {
                currentScale = Vector3.zero;
                scaleVelocity = Vector3.zero;
                promptInstance.transform.localScale = Vector3.zero;
                ResetPromptPosition();
            }

            promptInstance.SetActive(true);
            targetScale = originalScale;
        }
    }

    public void HidePrompt()
    {
        OnFocusExit();

        // คืนค่า Sprite หรือสี
        if (spriteRenderer != null)
        {
            if (originalSprite != null) spriteRenderer.sprite = originalSprite;
            if (useHoverColor) spriteRenderer.color = originalColor;
        }

        if (promptInstance != null)
        {
            targetScale = Vector3.zero;
        }
    }

    private void HandlePromptAnimation()
    {
        if (promptInstance == null || !promptInstance.activeSelf) return;

        // 1. Spring Physics Scaling
        Vector3 diff = targetScale - currentScale;
        scaleVelocity += diff * bounceSpring * Time.deltaTime;
        scaleVelocity -= scaleVelocity * bounceDamp * Time.deltaTime;
        currentScale += scaleVelocity * Time.deltaTime;
        promptInstance.transform.localScale = currentScale;

        if (targetScale == Vector3.zero && currentScale.magnitude < 0.05f)
        {
            promptInstance.SetActive(false);
            currentScale = Vector3.zero;
            scaleVelocity = Vector3.zero;
            promptInstance.transform.localScale = Vector3.zero;
            return;
        }

        // 2. Sine Wave Floating & Mouse Parallax
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null && currentScale.magnitude > 0.01f)
        {
            float floatY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            Vector3 baseWorldPos = transform.position + promptOffset + new Vector3(0, floatY, 0);

            Vector3 targetPos = baseWorldPos;

            if (enableMouseParallax)
            {
                Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorldPos.z = baseWorldPos.z;

                Vector3 offsetToMouse = mouseWorldPos - baseWorldPos;
                if (offsetToMouse.magnitude > maxFollowDistance)
                {
                    offsetToMouse = offsetToMouse.normalized * maxFollowDistance;
                }
                targetPos = baseWorldPos + offsetToMouse;
            }

            // ตรวจสอบว่า Canvas เป็นโหมด ScreenSpaceOverlay หรือไม่
            bool isScreenSpace = false;
            Canvas parentCanvas = promptInstance.GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                isScreenSpace = true;
            }

            if (isScreenSpace)
            {
                Vector3 targetScreenPos = mainCamera.WorldToScreenPoint(targetPos);
                promptInstance.transform.position = Vector3.Lerp(promptInstance.transform.position, targetScreenPos, Time.deltaTime * followSpeed);
            }
            else
            {
                promptInstance.transform.position = Vector3.Lerp(promptInstance.transform.position, targetPos, Time.deltaTime * followSpeed);

                // Billboard หันหน้าเข้าหากล้อง
                if (billboardToCamera)
                {
                    promptInstance.transform.rotation = mainCamera.transform.rotation;
                }
            }
        }
    }

    private void ResetPromptPosition()
    {
        Vector3 startPos = transform.position + promptOffset;
        Canvas parentCanvas = promptInstance.GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay && mainCamera != null)
        {
            promptInstance.transform.position = mainCamera.WorldToScreenPoint(startPos);
        }
        else
        {
            promptInstance.transform.position = startPos;
        }
    }

    private void PlayAudio(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f;
        }
        audioSource.PlayOneShot(clip);
    }

    #endregion

    #region Collider & Mouse Events

    void OnMouseEnter()
    {
        isMouseHovering = true;
    }

    void OnMouseExit()
    {
        isMouseHovering = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag)) isTriggerColliding = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag)) isTriggerColliding = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag)) isTriggerColliding = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag)) isTriggerColliding = false;
    }

    #endregion

    #region Editor Gizmos

    protected virtual void OnDrawGizmosSelected()
    {
        // วาดระยะกด (สีเหลืองอ่อน)
        if (detectionMode == DetectionMode.PlayerDistance || detectionMode == DetectionMode.DistanceOrMouseHover)
        {
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, interactionDistance);
        }

        // วาดจุดที่ปุ่ม E จะลอย (สีฟ้า Cyan)
        Gizmos.color = Color.cyan;
        Vector3 promptWorldPos = transform.position + promptOffset;
        Gizmos.DrawWireSphere(promptWorldPos, 0.15f);
        Gizmos.DrawLine(transform.position, promptWorldPos);
    }

    #endregion
}
