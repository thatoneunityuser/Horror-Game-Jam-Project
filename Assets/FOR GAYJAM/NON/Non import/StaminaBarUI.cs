using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ระบบแสดงผลหลอด Stamina (Stamina Bar UI) ติดด้านล่างจอ
/// - แถบหลอดสีขาวล้วน สะอาดตา สไตล์ Minimalist Horror
/// - ปรับขนาดอัตโนมัติทั้งแบบ Image.fillAmount และ RectTransform Anchor เพื่อความแน่นอน 100%
/// - ตรวจจับและแสดงผลสถานะการ เพิ่มขึ้น (Regenerating) หรือ ลดลง (Draining) อย่างชัดเจน
/// - เมื่อไม่ได้ใช้งาน และ Stamina เต็ม หลอดจะค่อยๆ Fade จางหายไป
/// - เมื่อเริ่มวิ่ง (Sprinting) หรือขณะกำลังฟื้นฟู (Regenerating) หลอดจะโผล่ขึ้นมาให้เห็นชัดเจน
/// </summary>
[DisallowMultipleComponent]
public class StaminaBarUI : MonoBehaviour
{
    public static StaminaBarUI Instance { get; private set; }

    public enum StaminaStatus
    {
        Idle,           // สตามิน่าเต็ม หรือนิ่งสนิท
        Draining,       // กำลังลดลง (วิ่ง / แดช / ใช้งาน)
        Regenerating,   // กำลังเพิ่มขึ้น (ฟื้นฟูพลัง)
        Exhausted       // สตามิน่าหมดเกลี้ยง (ติดสถานะเหนื่อยหอบ)
    }

    [Header("=== References (จะค้นหา/สร้างให้อัตโนมัติหากเว้นว่าง) ===")]
    [Tooltip("คอมโพเนนต์การเคลื่อนไหวของผู้เล่น (SideScrollMovement)")]
    public SideScrollMovement playerMovement;

    [Tooltip("CanvasGroup สำหรับควบคุมการ Fade เข้า-ออก")]
    public CanvasGroup canvasGroup;

    [Tooltip("Image รูปหลอด Stamina (แถบสีขาว)")]
    public Image fillImage;

    [Tooltip("Image พื้นหลังหลอด Stamina")]
    public Image backgroundImage;

    [Tooltip("Image หลอดแสดงรอยยุบตามหลัง (Ghost Trail) ตอนสตามิน่าลดลง")]
    public Image ghostFillImage;

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
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);

    [Tooltip("สีของแถบ Stamina ปกติ (สีขาวล้วน)")]
    public Color fillColor = Color.white;

    [Tooltip("สีของหลอดตอนเหนื่อยหอบ / สตามิน่าหมด (สีแดงเตือน)")]
    public Color exhaustedColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Tooltip("สีของ Ghost Trail หลอดตามหลัง")]
    public Color ghostColor = new Color(1f, 1f, 1f, 0.35f);

    [Tooltip("ขยายเต็มความกว้างขอบจอล่างหรือไม่ (หากเปิด จะยืดเต็มซ้ายจรดขวา)")]
    public bool stretchFullScreenWidth = false;

    [Header("=== Visual Animation (แอนิเมชันและความลื่นไหล) ===")]
    [Tooltip("เปิดแอนิเมชันเคลื่อนไหวแบบสมูท")]
    public bool smoothFill = true;
    public float fillSmoothSpeed = 15f;

    [Tooltip("เปิดแถบ Ghost Trail แสดงรอยลดลงของสตามิน่า")]
    public bool enableGhostTrail = true;
    public float ghostCatchupSpeed = 2.5f;

    [Header("=== Visibility & Fade Settings ===")]
    [Tooltip("แสดงหลอดค้างไว้ตลอดเวลา ไม่ต้อง Fade หายไป")]
    public bool alwaysShow = false;

    [Tooltip("ให้แสดงหลอดค้างไว้ระหว่างที่ Stamina กำลังฟื้นฟู (เปิดไว้เพื่อให้เห็นตอนพลังกำลังเพิ่ม)")]
    public bool showWhileRegenerating = true;

    [Tooltip("ความเร็วในการโผล่ขึ้นมาตอนเริ่มวิ่ง/ใช้งาน (Fade In)")]
    public float fadeInSpeed = 8f;

    [Tooltip("ความเร็วในการค่อยๆ จางหายไป (Fade Out)")]
    public float fadeOutSpeed = 1.8f;

    [Tooltip("หน่วงเวลาก่อนเริ่มจางหายหลังจากหยุดวิ่ง (วินาที)")]
    public float fadeOutDelay = 0.5f;

    [Tooltip("หน่วงเวลาก่อนจางหายหลังจากฟื้นฟูเต็ม 100% (วินาที)")]
    public float fullStaminaFadeDelay = 0.8f;

    [Header("=== Realtime Debug Status (ดูค่าสถานะได้สดๆ ใน Inspector) ===")]
    [Tooltip("สถานะปัจจุบัน: Idle, Draining (กำลังลด), Regenerating (กำลังเพิ่ม), Exhausted")]
    public StaminaStatus currentStatus = StaminaStatus.Idle;
    public float currentStamina = 100f;
    public float maxStamina = 100f;
    [Range(0f, 1f)] public float fillRatio = 1f;
    public bool isDraining = false;
    public bool isRegenerating = false;

    // สถานะภายใน
    private float currentAlpha = 0f;
    private float displayedFill = 1f;
    private float ghostFill = 1f;
    private float previousStamina = 100f;
    private float stopSprintingTimer = 0f;
    private float fullStaminaTimer = 0f;
    private bool wasSprinting = false;
    private static Sprite defaultWhiteSprite;

    public bool IsDecreasing => isDraining;
    public bool IsIncreasing => isRegenerating;

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
            canvasGroup.alpha = alwaysShow ? 1f : 0f;
            currentAlpha = canvasGroup.alpha;
        }

        FindPlayerIfNeeded();
        if (playerMovement != null)
        {
            previousStamina = playerMovement.stamina;
            displayedFill = Mathf.Clamp01(playerMovement.stamina / Mathf.Max(1f, playerMovement.maxStamina));
            ghostFill = displayedFill;
        }
    }

    private void Update()
    {
        FindPlayerIfNeeded();

        if (playerMovement == null) return;

        UpdateStaminaStatus();
        UpdateStaminaFill();
        UpdateFadeVisibility();
    }

    private void FindPlayerIfNeeded()
    {
        if (playerMovement != null && playerMovement.gameObject.activeInHierarchy) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerMovement = player.GetComponent<SideScrollMovement>();
            if (playerMovement == null)
            {
                playerMovement = player.GetComponentInChildren<SideScrollMovement>();
            }
        }

        if (playerMovement == null)
        {
            playerMovement = FindObjectOfType<SideScrollMovement>();
        }
    }

    private void UpdateStaminaStatus()
    {
        maxStamina = Mathf.Max(1f, playerMovement.maxStamina);
        currentStamina = Mathf.Clamp(playerMovement.stamina, 0f, maxStamina);
        fillRatio = currentStamina / maxStamina;

        float delta = currentStamina - previousStamina;
        float threshold = 0.005f;

        bool exhausted = playerMovement.isExhausted || currentStamina <= 0.05f;

        if (exhausted)
        {
            currentStatus = StaminaStatus.Exhausted;
            isDraining = delta < -threshold;
            isRegenerating = delta > threshold;
        }
        else if (delta < -threshold)
        {
            currentStatus = StaminaStatus.Draining;
            isDraining = true;
            isRegenerating = false;
        }
        else if (delta > threshold && currentStamina < maxStamina - 0.05f)
        {
            currentStatus = StaminaStatus.Regenerating;
            isDraining = false;
            isRegenerating = true;
        }
        else
        {
            currentStatus = StaminaStatus.Idle;
            isDraining = false;
            isRegenerating = false;
        }

        previousStamina = currentStamina;
    }

    private void UpdateStaminaFill()
    {
        if (fillImage == null) return;

        // ตรวจสอบให้แน่ใจว่า Image มี Sprite เพื่อให้ระบบ Filled ของ Unity ทำงานได้อย่างสมบูรณ์
        if (fillImage.sprite == null)
        {
            fillImage.sprite = GetOrCreateWhiteSprite();
        }

        // คำนวณความลื่นไหลของแถบ Stamina
        if (smoothFill)
        {
            displayedFill = Mathf.MoveTowards(displayedFill, fillRatio, Time.deltaTime * fillSmoothSpeed);
        }
        else
        {
            displayedFill = fillRatio;
        }

        // 1. กำหนดค่า fillAmount ของ Image
        fillImage.fillAmount = displayedFill;

        // 2. กำหนดขนาด RectTransform ตามอัตราส่วน Anchor ควบคู่กัน (รับประกัน 100% ว่าแถบจะขยาย/หดได้เสมอแม้ไม่ได้ใช้ Sprite)
        RectTransform fillRect = fillImage.rectTransform;
        if (fillRect != null)
        {
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(displayedFill, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        // 3. ปรับปรุง Ghost Trail (แถบเงาลดตามหลังเมื่อสตามิน่าลดลง)
        if (ghostFillImage != null && enableGhostTrail)
        {
            if (ghostFillImage.sprite == null)
            {
                ghostFillImage.sprite = GetOrCreateWhiteSprite();
            }

            if (ghostFill < displayedFill)
            {
                ghostFill = displayedFill;
            }
            else if (ghostFill > displayedFill)
            {
                ghostFill = Mathf.MoveTowards(ghostFill, displayedFill, Time.deltaTime * ghostCatchupSpeed);
            }

            ghostFillImage.fillAmount = ghostFill;
            RectTransform ghostRect = ghostFillImage.rectTransform;
            if (ghostRect != null)
            {
                ghostRect.anchorMin = Vector2.zero;
                ghostRect.anchorMax = new Vector2(ghostFill, 1f);
                ghostRect.offsetMin = Vector2.zero;
                ghostRect.offsetMax = Vector2.zero;
            }
        }

        // 4. การตอบสนองทางสี: หากเหนื่อยหอบจนหมดหลอดจะกะพริบสีแดงเตือน นอกนั้นเป็นสีขาวปกติ
        if (currentStatus == StaminaStatus.Exhausted)
        {
            float pulse = 0.45f + Mathf.PingPong(Time.time * 6f, 0.55f);
            fillImage.color = Color.Lerp(exhaustedColor, fillColor, pulse);
        }
        else
        {
            fillImage.color = fillColor;
        }
    }

    private void UpdateFadeVisibility()
    {
        if (canvasGroup == null) return;

        if (alwaysShow)
        {
            canvasGroup.alpha = 1f;
            currentAlpha = 1f;
            return;
        }

        bool isConsumingStamina = isDraining || playerMovement.isSprinting || playerMovement.isDashing;

        if (isConsumingStamina)
        {
            stopSprintingTimer = 0f;
            fullStaminaTimer = 0f;
            wasSprinting = true;
        }
        else if (wasSprinting)
        {
            stopSprintingTimer += Time.deltaTime;
        }

        // การตัดสินใจแสดงผลหลอด:
        bool shouldShow = false;

        if (isConsumingStamina)
        {
            shouldShow = true;
        }
        else if (wasSprinting && stopSprintingTimer < fadeOutDelay)
        {
            shouldShow = true;
        }

        // แสดงผลขณะสตามิน่ากำลังฟื้นฟู (เพื่อให้ผู้เล่นเห็นว่าสตามิน่ากำลังเพิ่มขึ้นเรื่อยๆ)
        if (showWhileRegenerating && currentStamina < maxStamina - 0.1f)
        {
            shouldShow = true;
            fullStaminaTimer = 0f;
        }
        else if (currentStamina >= maxStamina - 0.1f)
        {
            // เมื่อฟื้นฟูจนเต็ม 100%: แสดงค้างไว้ชั่วครู่ก่อนค่อยๆ Fade หายไปอย่างนุ่มนวล
            if (fullStaminaTimer < fullStaminaFadeDelay && wasSprinting)
            {
                fullStaminaTimer += Time.deltaTime;
                shouldShow = true;
            }
            else
            {
                wasSprinting = false;
            }
        }

        float speed = shouldShow ? fadeInSpeed : fadeOutSpeed;
        float targetAlpha = shouldShow ? 1f : 0f;

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * speed);
        canvasGroup.alpha = currentAlpha;
    }

    public static Sprite GetOrCreateWhiteSprite()
    {
        if (defaultWhiteSprite == null)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.SetPixel(0, 1, Color.white);
            tex.SetPixel(1, 0, Color.white);
            tex.SetPixel(1, 1, Color.white);
            tex.Apply();
            defaultWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        }
        return defaultWhiteSprite;
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
                backgroundImage.sprite = GetOrCreateWhiteSprite();
                backgroundImage.color = backgroundColor;
                backgroundImage.raycastTarget = false;
            }
        }
        else
        {
            if (backgroundImage.sprite == null) backgroundImage.sprite = GetOrCreateWhiteSprite();
            backgroundImage.color = backgroundColor;
        }

        // 2. ตรวจสอบหรือสร้าง Ghost Trail Image (แถบเงายุบตามหลัง)
        if (ghostFillImage == null)
        {
            Transform ghostTrans = transform.Find("Stamina_Ghost");
            if (ghostTrans != null)
            {
                ghostFillImage = ghostTrans.GetComponent<Image>();
            }
            else
            {
                GameObject ghostGo = new GameObject("Stamina_Ghost");
                ghostGo.transform.SetParent(transform, false);
                RectTransform ghostRect = ghostGo.AddComponent<RectTransform>();
                ghostRect.anchorMin = Vector2.zero;
                ghostRect.anchorMax = Vector2.one;
                ghostRect.sizeDelta = Vector2.zero;
                ghostRect.anchoredPosition = Vector2.zero;

                ghostFillImage = ghostGo.AddComponent<Image>();
                ghostFillImage.sprite = GetOrCreateWhiteSprite();
                ghostFillImage.color = ghostColor;
                ghostFillImage.type = Image.Type.Filled;
                ghostFillImage.fillMethod = Image.FillMethod.Horizontal;
                ghostFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                ghostFillImage.raycastTarget = false;
            }
        }

        // 3. ตรวจสอบหรือสร้าง Fill Image (แถบสีขาวหลัก)
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
                fillImage.sprite = GetOrCreateWhiteSprite();
                fillImage.color = fillColor;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImage.raycastTarget = false;
            }
        }
        else
        {
            if (fillImage.sprite == null) fillImage.sprite = GetOrCreateWhiteSprite();
            fillImage.color = fillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        // จัดลำดับเลเยอร์ให้ Fill Image อยู่ด้านหน้าสุดเสมอ
        if (backgroundImage != null) backgroundImage.transform.SetSiblingIndex(0);
        if (ghostFillImage != null) ghostFillImage.transform.SetSiblingIndex(1);
        if (fillImage != null) fillImage.transform.SetAsLastSibling();
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
