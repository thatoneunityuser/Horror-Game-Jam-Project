using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ระบบแสดงผลหลอด Stamina (Stamina Bar UI) ติดด้านล่างจอ
/// - แถบหลอดสีขาวล้วน สะอาดตา สไตล์ Minimalist Horror
/// - อยู่ติดขอบล่างของหน้าจอ
/// - เมื่อไม่ได้วิ่ง หลอดจะค่อยๆ Fade จางหายไป
/// - เมื่อเริ่มวิ่ง (Sprinting) หลอดจะโผล่ (Fade in) ขึ้นมาทันที
/// </summary>
[DisallowMultipleComponent]
public class StaminaBarUI : MonoBehaviour
{
    public static StaminaBarUI Instance { get; private set; }

    [Header("=== References (จะค้นหา/สร้างให้อัตโนมัติหากเว้นว่าง) ===")]
    [Tooltip("คอมโพเนนต์การเคลื่อนไหวของผู้เล่น")]
    public SideScrollMovement playerMovement;

    [Tooltip("CanvasGroup สำหรับควบคุมการ Fade เข้า-ออก")]
    public CanvasGroup canvasGroup;

    [Tooltip("Image รูปหลอด Stamina (แถบสีขาว)")]
    public Image fillImage;

    [Tooltip("Image พื้นหลังหลอด Stamina")]
    public Image backgroundImage;

    [Tooltip("Root RectTransform ของหลอด Stamina")]
    public RectTransform barContainer;

    [Header("=== Appearance (ขนาดและตำแหน่งติดล่างจอ) ===")]
    [Tooltip("ความกว้างของหลอด (พิกเซล)")]
    public float barWidth = 420f;

    [Tooltip("ความสูง/ความหนาของหลอด (พิกเซล)")]
    public float barHeight = 8f;

    [Tooltip("ระยะห่างจากขอบล่างของจอ (0 = ติดขอบล่างจอเลย)")]
    public float bottomOffset = 0f;

    [Tooltip("สีกรอบ/พื้นหลังหลอด (สีดำโปร่งแสง)")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.45f);

    [Tooltip("สีของแถบ Stamina (สีขาวล้วน)")]
    public Color fillColor = Color.white;

    [Tooltip("ขยายเต็มความกว้างขอบจอล่างหรือไม่ (หากเปิด จะยืดเต็มซ้ายจรดขวา)")]
    public bool stretchFullScreenWidth = false;

    [Header("=== Fade Settings (การค่อยๆ จางหาย / โผล่) ===")]
    [Tooltip("ความเร็วในการโผล่ขึ้นมาตอนเริ่มวิ่ง (Fade In)")]
    public float fadeInSpeed = 8f;

    [Tooltip("ความเร็วในการค่อยๆ จางหายไปตอนหยุดวิ่ง (Fade Out)")]
    public float fadeOutSpeed = 1.8f;

    [Tooltip("หน่วงเวลาก่อนเริ่มจางหายหลังจากหยุดวิ่ง (วินาที)")]
    public float fadeOutDelay = 0.35f;

    [Tooltip("ให้แสดงหลอดตอน Stamina กำลังฟื้นฟูด้วยหรือไม่")]
    public bool showWhileRegenerating = false;

    private float currentAlpha = 0f;
    private float stopSprintingTimer = 0f;
    private bool wasSprinting = false;

    private void Awake()
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

        EnsureComponents();
    }

    private void Start()
    {
        EnsureComponents();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            currentAlpha = 0f;
        }
    }

    private void Update()
    {
        FindPlayerIfNeeded();

        if (playerMovement == null) return;

        UpdateStaminaFill();
        UpdateFadeVisibility();
    }

    private void FindPlayerIfNeeded()
    {
        if (playerMovement != null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerMovement = player.GetComponent<SideScrollMovement>();
        }

        if (playerMovement == null)
        {
            playerMovement = FindObjectOfType<SideScrollMovement>();
        }
    }

    private void UpdateStaminaFill()
    {
        if (playerMovement == null || fillImage == null) return;

        float max = Mathf.Max(1f, playerMovement.maxStamina);
        float current = Mathf.Clamp(playerMovement.stamina, 0f, max);
        float targetFill = current / max;

        // ปรับแถบ Stamina ตามอัตราส่วนปัจจุบัน
        fillImage.fillAmount = targetFill;
    }

    private void UpdateFadeVisibility()
    {
        if (playerMovement == null || canvasGroup == null) return;

        bool isSprinting = playerMovement.isSprinting;
        
        // หากผู้เล่นเพิ่งกดแดชหรือใช้สตามิน่า ก็ถือว่ากำลังใช้
        bool isConsumingStamina = isSprinting || (playerMovement.isDashing);

        if (isConsumingStamina)
        {
            // กำลังวิ่ง: รีเซ็ตตัวนับเวลาหน่วง
            stopSprintingTimer = 0f;
            wasSprinting = true;
        }
        else if (wasSprinting)
        {
            stopSprintingTimer += Time.deltaTime;
        }

        // เงื่อนไขในการแสดงผล:
        // 1. กำลังวิ่ง/ใช้สตามิน่า
        // 2. หรือหยุดวิ่งยังไม่เกินเวลา fadeOutDelay
        // 3. หรือเปิด showWhileRegenerating และสตามิน่ายังไม่เต็ม
        bool shouldShow = isConsumingStamina;
        if (!isConsumingStamina && wasSprinting && stopSprintingTimer < fadeOutDelay)
        {
            shouldShow = true;
        }

        if (showWhileRegenerating && playerMovement.stamina < playerMovement.maxStamina - 0.5f)
        {
            shouldShow = true;
        }

        if (stopSprintingTimer >= fadeOutDelay)
        {
            wasSprinting = false;
        }

        // คำนวณความเร็วและ Alpha
        float speed = shouldShow ? fadeInSpeed : fadeOutSpeed;
        float targetAlpha = shouldShow ? 1f : 0f;

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * speed);
        canvasGroup.alpha = currentAlpha;
    }

    /// <summary>
    /// สร้าง UI หลอด Stamina ติดขอบล่างจออัตโนมัติหากยังไม่มี
    /// </summary>
    public void EnsureComponents()
    {
        if (barContainer == null)
        {
            barContainer = GetComponent<RectTransform>();
            if (barContainer == null)
            {
                barContainer = gameObject.AddComponent<RectTransform>();
            }
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        // ตรวจสอบตำแหน่งให้อยู่ติดด้านล่างจอ
        if (barContainer != null)
        {
            if (stretchFullScreenWidth)
            {
                barContainer.anchorMin = new Vector2(0f, 0f);
                barContainer.anchorMax = new Vector2(1f, 0f);
                barContainer.pivot = new Vector2(0.5f, 0f);
                barContainer.sizeDelta = new Vector2(0f, barHeight);
                barContainer.anchoredPosition = new Vector2(0f, bottomOffset);
            }
            else
            {
                barContainer.anchorMin = new Vector2(0.5f, 0f);
                barContainer.anchorMax = new Vector2(0.5f, 0f);
                barContainer.pivot = new Vector2(0.5f, 0f);
                barContainer.sizeDelta = new Vector2(barWidth, barHeight);
                barContainer.anchoredPosition = new Vector2(0f, bottomOffset);
            }
        }

        // 1. ตรวจสอบหรือสร้าง Background Image
        if (backgroundImage == null)
        {
            Transform bgTrans = transform.Find("Stamina_Background");
            if (bgTrans != null)
            {
                backgroundImage = bgTrans.GetComponent<Image>();
            }
            else
            {
                GameObject bgGo = new GameObject("Stamina_Background");
                bgGo.transform.SetParent(transform, false);
                RectTransform bgRect = bgGo.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = Vector2.zero;
                bgRect.anchoredPosition = Vector2.zero;

                backgroundImage = bgGo.AddComponent<Image>();
                backgroundImage.color = backgroundColor;
                backgroundImage.raycastTarget = false;
            }
        }
        else
        {
            backgroundImage.color = backgroundColor;
        }

        // 2. ตรวจสอบหรือสร้าง Fill Image (แถบสีขาว)
        if (fillImage == null)
        {
            Transform fillTrans = transform.Find("Stamina_Fill");
            if (fillTrans != null)
            {
                fillImage = fillTrans.GetComponent<Image>();
            }
            else
            {
                GameObject fillGo = new GameObject("Stamina_Fill");
                fillGo.transform.SetParent(transform, false);
                RectTransform fillRect = fillGo.AddComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.sizeDelta = Vector2.zero;
                fillRect.anchoredPosition = Vector2.zero;

                fillImage = fillGo.AddComponent<Image>();
                fillImage.color = fillColor;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImage.raycastTarget = false;
            }
        }
        else
        {
            fillImage.color = fillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoEnsureStaminaBar()
    {
        if (Instance != null) return;

        StaminaBarUI existing = FindObjectOfType<StaminaBarUI>();
        if (existing != null) return;

        // ค้นหา Canvas ในเกม
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameplayHUDManager hud = FindObjectOfType<GameplayHUDManager>();
            if (hud != null) canvas = hud.GetComponent<Canvas>();
        }

        if (canvas != null)
        {
            GameObject staminaGo = new GameObject("StaminaBar_HUD", typeof(RectTransform));
            staminaGo.transform.SetParent(canvas.transform, false);
            staminaGo.AddComponent<StaminaBarUI>();
        }
    }
}
