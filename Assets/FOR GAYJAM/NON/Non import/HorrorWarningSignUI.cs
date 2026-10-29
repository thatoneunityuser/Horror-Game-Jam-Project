using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ระบบแจ้งเตือนภัยแบบ Minimalist Horror UI (Icon + ตัวหนังสือสีขาว)
/// 3 ระดับความรุนแรงตามที่ผู้ใช้กำหนด:
/// - ระดับ 1: สีขาว (เตือนว่าให้ไปอยู่ใต้แสงสว่าง)
/// - ระดับ 2: สีเหลือง (เตือนว่าตัวตาบอดสงสัยว่าเราขยับ)
/// - ระดับ 3: สีแดง (เตือนว่าไอตาบอดรู้ว่าเราอยู่ไหน)
/// 
/// ดีไซน์:
/// - ไม่มีป้ายกรอบสี่เหลี่ยมเทอะทะ ("ไม่ต้องใช้ป้ายนั้น")
/// - แสดงเฉพาะไอคอนสามเหลี่ยมเตือนภัย (ขาว/เหลือง/แดง) คู่กับตัวหนังสือสีขาว
/// - ตรวจจับสถานะในเกมอัตโนมัติ (BlindedEntity & SnapshotAnomaly)
/// </summary>
[DisallowMultipleComponent]
public class HorrorWarningSignUI : MonoBehaviour
{
    public static HorrorWarningSignUI Instance { get; private set; }

    public enum WarningLevel
    {
        None = 0,
        White_Light = 1,     // ระดับสีขาว: เตือนว่าให้ไปอยู่ใต้แสงสว่าง
        Yellow_Suspect = 2,  // ระดับสีเหลือง: ตัวตาบอดสงสัยว่าเราขยับ
        Red_Detected = 3     // ระดับสีแดง: ไอตาบอดมันรู้ว่าเราอยู่ไหน
    }

    [Header("=== Auto Monitoring ===")]
    [Tooltip("ตรวจจับสถานะ BlindedEntity และ SnapshotAnomaly ในฉากอัตโนมัติ")]
    public bool autoMonitorGameEvents = true;
    public float scanInterval = 0.2f;

    [Header("=== UI Elements ===")]
    public RectTransform containerRect;
    public CanvasGroup canvasGroup;
    public Image iconImage;
    public TextMeshProUGUI warningText;
    public TMP_FontAsset thaiFontAsset;

    [Header("=== Sprites (ไอคอน 3 ระดับ) ===")]
    public Sprite iconWhite;
    public Sprite iconYellow;
    public Sprite iconRed;

    [Header("=== Warning Configurations (ข้อความตัวหนังสือสีขาว) ===")]
    [Tooltip("ข้อความระดับสีขาว (เตือนให้อยู่ใต้แสงสว่าง)")]
    public string whiteLightMessage = "รีบไปอยู่ใต้แสงสว่าง!";

    [Tooltip("ข้อความระดับสีเหลือง (ตัวตาบอดสงสัยว่าเราขยับ)")]
    public string yellowSuspectMessage = "ตัวตาบอดกำลังสงสัยว่าเราขยับ... หยุดนิ่ง!";

    [Tooltip("ข้อความระดับสีแดง (ไอตาบอดรู้ว่าเราอยู่ไหน)")]
    public string redDetectedMessage = "ไอตาบอดรู้ตำแหน่งของเราแล้ว! หนีเร็ว!";

    [Header("=== Animation & Audio ===")]
    public float fadeInSpeed = 10f;
    public float fadeOutSpeed = 3f;
    public bool enablePulseOnRed = true;
    public float pulseSpeed = 7f;
    public float pulseAmount = 0.08f;

    public AudioSource audioSource;
    public AudioClip whiteSfx;
    public AudioClip yellowSfx;
    public AudioClip redSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    // สถานะปัจจุบัน
    private WarningLevel currentLevel = WarningLevel.None;
    private float currentAlpha = 0f;
    private float manualTimer = 0f;
    private bool isManualOverride = false;
    private float nextScanTime = 0f;

    private BlindedEntity[] cachedBlindedEntities = new BlindedEntity[0];
    private SnapshotAnomaly[] cachedSnapshotAnomalies = new SnapshotAnomaly[0];

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
        if (autoMonitorGameEvents && Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            ScanForGameEvents();
        }

        UpdateWarningState();
        UpdateAnimations();
    }

    private void ScanForGameEvents()
    {
        cachedBlindedEntities = FindObjectsOfType<BlindedEntity>();
        cachedSnapshotAnomalies = FindObjectsOfType<SnapshotAnomaly>();
    }

    private void UpdateWarningState()
    {
        if (isManualOverride)
        {
            if (manualTimer > 0f)
            {
                manualTimer -= Time.deltaTime;
                if (manualTimer <= 0f)
                {
                    isManualOverride = false;
                }
            }
            return;
        }

        // ตรวจจับอัตโนมัติ
        WarningLevel targetLevel = WarningLevel.None;

        bool hasBlindDetected = false;
        bool hasBlindSuspicious = false;

        for (int i = 0; i < cachedBlindedEntities.Length; i++)
        {
            BlindedEntity entity = cachedBlindedEntities[i];
            if (entity == null || !entity.gameObject.activeInHierarchy) continue;

            if (entity.currentState == BlindedEntity.State.Detected)
            {
                hasBlindDetected = true;
                break;
            }
            else if (entity.currentState == BlindedEntity.State.Suspicious)
            {
                hasBlindSuspicious = true;
            }
        }

        bool needsLight = false;
        for (int i = 0; i < cachedSnapshotAnomalies.Length; i++)
        {
            SnapshotAnomaly anomaly = cachedSnapshotAnomalies[i];
            if (anomaly == null || !anomaly.gameObject.activeInHierarchy) continue;

            if (!anomaly.hasSnapshotted && !anomaly.IsPlayerInSafeArea())
            {
                needsLight = true;
                break;
            }
        }

        // ลำดับความรุนแรง:
        // 1. แดง (ไอตาบอดรู้ตำแหน่งแล้ว)
        // 2. เหลือง (ตัวตาบอดสงสัยว่าเราขยับ)
        // 3. ขาว (เตือนให้ไปอยู่ใต้แสงสว่าง)
        if (hasBlindDetected)
        {
            targetLevel = WarningLevel.Red_Detected;
        }
        else if (hasBlindSuspicious)
        {
            targetLevel = WarningLevel.Yellow_Suspect;
        }
        else if (needsLight && !SnapshotAnomaly.isSnapshotted)
        {
            targetLevel = WarningLevel.White_Light;
        }

        if (targetLevel != currentLevel)
        {
            ApplyWarningLevel(targetLevel);
        }
    }

    private void ApplyWarningLevel(WarningLevel level, string customText = null)
    {
        if (currentLevel == level && string.IsNullOrEmpty(customText)) return;

        currentLevel = level;

        if (currentLevel == WarningLevel.None) return;

        Sprite targetSprite = null;
        string message = "";
        AudioClip clipToPlay = null;

        switch (currentLevel)
        {
            case WarningLevel.White_Light:
                targetSprite = iconWhite;
                message = !string.IsNullOrEmpty(customText) ? customText : whiteLightMessage;
                clipToPlay = whiteSfx;
                break;

            case WarningLevel.Yellow_Suspect:
                targetSprite = iconYellow;
                message = !string.IsNullOrEmpty(customText) ? customText : yellowSuspectMessage;
                clipToPlay = yellowSfx;
                break;

            case WarningLevel.Red_Detected:
                targetSprite = iconRed;
                message = !string.IsNullOrEmpty(customText) ? customText : redDetectedMessage;
                clipToPlay = redSfx;
                break;
        }

        if (iconImage != null && targetSprite != null)
        {
            iconImage.sprite = targetSprite;
            iconImage.color = Color.white; // ใช้สีจากรูปสามเหลี่ยมต้นฉบับโดยตรง
        }

        if (warningText != null)
        {
            string adjusted = ThaiFontFixer.Adjust(message);
            warningText.text = adjusted;
            warningText.color = Color.white; // ตัวหนังสือสีขาวตามที่ขอ
        }

        if (audioSource != null && clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay, sfxVolume);
        }
    }

    private void UpdateAnimations()
    {
        if (canvasGroup == null) return;

        bool shouldShow = (currentLevel != WarningLevel.None);
        float speed = shouldShow ? fadeInSpeed : fadeOutSpeed;
        float targetAlpha = shouldShow ? 1f : 0f;

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * speed);
        canvasGroup.alpha = currentAlpha;

        // แอนิเมชันกะพริบ/ชีพจรตอนเป็นสีแดง
        if (shouldShow && currentLevel == WarningLevel.Red_Detected && enablePulseOnRed && containerRect != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            containerRect.localScale = new Vector3(pulse, pulse, 1f);
        }
        else if (containerRect != null)
        {
            containerRect.localScale = Vector3.one;
        }
    }

    #region Public Static API

    /// <summary>
    /// สั่งแสดงป้ายเตือนตามระดับ (White_Light, Yellow_Suspect, Red_Detected)
    /// </summary>
    public static void Show(WarningLevel level, float duration = 4.0f, string customText = null)
    {
        if (Instance != null)
        {
            Instance.isManualOverride = (duration > 0f);
            Instance.manualTimer = duration;
            Instance.ApplyWarningLevel(level, customText);
        }
    }

    /// <summary>
    /// สั่งซ่อนการเตือนทันที
    /// </summary>
    public static void Hide()
    {
        if (Instance != null)
        {
            Instance.isManualOverride = false;
            Instance.manualTimer = 0f;
            Instance.currentLevel = WarningLevel.None;
        }
    }

    #endregion

    /// <summary>
    /// สร้าง/จัดระเบียบโครงสร้าง UI (Icon + ตัวหนังสือสีขาว) โดยไม่มีป้ายกรอบหลัง
    /// </summary>
    public void EnsureComponents()
    {
        containerRect = GetComponent<RectTransform>();
        if (containerRect == null) containerRect = gameObject.AddComponent<RectTransform>();

        // จัดให้อยู่ด้านบนกึ่งกลางจอ
        containerRect.anchorMin = new Vector2(0.5f, 1f);
        containerRect.anchorMax = new Vector2(0.5f, 1f);
        containerRect.pivot = new Vector2(0.5f, 1f);
        containerRect.anchoredPosition = new Vector2(0f, -50f);
        containerRect.sizeDelta = new Vector2(650f, 60f);

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        // ลบพื้นหลังป้ายกรอบเดิมออกถ้ามี ("ไม่ต้องใช้ป้ายนั้น")
        Transform oldBg = transform.Find("Banner_Background");
        if (oldBg != null) DestroyImmediate(oldBg.gameObject);

        Transform oldLine = transform.Find("Accent_Line");
        if (oldLine != null) DestroyImmediate(oldLine.gameObject);

        Transform oldSub = transform.Find("Subtitle_Text");
        if (oldSub != null) DestroyImmediate(oldSub.gameObject);

        // 1. โหลดรูปไอคอน 3 ระดับถ้ายังไม่ได้ใส่
#if UNITY_EDITOR
        if (iconWhite == null)
            iconWhite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/WarningIcon_White.png");
        if (iconYellow == null)
            iconYellow = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/WarningIcon_Yellow.png");
        if (iconRed == null)
            iconRed = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/WarningIcon_Red.png");
#endif

        // 2. Horizontal Layout Group เพื่อจัดกึ่งกลาง Icon กับข้อความสีขาวให้อยู่คู่กันสวยงาม
        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 14f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 3. Icon Image
        if (iconImage == null)
        {
            Transform iconTrans = transform.Find("Warning_Icon");
            if (iconTrans != null)
            {
                iconImage = iconTrans.GetComponent<Image>();
            }
            else
            {
                GameObject iconGo = new GameObject("Warning_Icon", typeof(RectTransform));
                iconGo.transform.SetParent(transform, false);
                iconImage = iconGo.AddComponent<Image>();
            }
        }
        RectTransform iconRt = iconImage.GetComponent<RectTransform>();
        iconRt.sizeDelta = new Vector2(44f, 42f);
        iconImage.sprite = (iconWhite != null) ? iconWhite : iconYellow;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        // 4. Warning Text (ตัวหนังสือสีขาว)
        if (warningText == null)
        {
            Transform textTrans = transform.Find("Warning_Text");
            if (textTrans != null)
            {
                warningText = textTrans.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                // หากมี Title_Text เดิม ให้นำมาใช้
                Transform oldTitle = transform.Find("Title_Text");
                if (oldTitle != null)
                {
                    oldTitle.name = "Warning_Text";
                    warningText = oldTitle.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    GameObject textGo = new GameObject("Warning_Text", typeof(RectTransform));
                    textGo.transform.SetParent(transform, false);
                    warningText = textGo.AddComponent<TextMeshProUGUI>();
                }
            }
        }

        if (thaiFontAsset == null)
        {
            thaiFontAsset = LoadThaiFont();
        }

        RectTransform textRt = warningText.GetComponent<RectTransform>();
        textRt.sizeDelta = new Vector2(500f, 48f);
        warningText.text = "ป้ายแจ้งเตือน";
        warningText.fontSize = 26f;
        warningText.fontStyle = FontStyles.Bold;
        warningText.alignment = TextAlignmentOptions.MidlineLeft;
        warningText.color = Color.white; // สีขาวล้วน
        warningText.raycastTarget = false;
        warningText.outlineWidth = 0.2f;
        warningText.outlineColor = new Color32(0, 0, 0, 220); // ขอบดำบางๆ ชัดเจนทุกสภาพแสง
        if (thaiFontAsset != null) warningText.font = thaiFontAsset;

        if (iconImage != null) iconImage.transform.SetSiblingIndex(0);
        if (warningText != null) warningText.transform.SetSiblingIndex(1);

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }

    private TMP_FontAsset LoadThaiFont()
    {
#if UNITY_EDITOR
        string[] candidates = new string[]
        {
            "Assets/NOT FOR GAYJAM/TH Sarabun New Regular/RSU_BOLD SDF.asset",
            "Assets/NOT FOR GAYJAM/TH Sarabun New Regular/NotoSansThai-VariableFont_wght SDF.asset",
            "Assets/FOR GAYJAM/NON/Font non/XTypewriter-Bold SDF.asset"
        };

        foreach (string path in candidates)
        {
            TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null) return font;
        }
#endif
        return null;
    }
}
