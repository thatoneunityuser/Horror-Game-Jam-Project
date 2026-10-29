using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ระบบ UI ผีเสี่ย (SecondEventGhost / Sniff Ghost HUD)
/// ออกแบบเชิงสัญลักษณ์ (Minimalist Reactive Horror HUD):
/// 1. ไม่มี UI แสดงค้างเมื่อปลอดภัย (ลบ Safe icon และ Ghost count ออกทั้งหมด)
/// 2. แสดงเตือนเฉพาะเมื่อตกอยู่ในอันตรายจริงเท่านั้น:
///    - ผีเสี่ยเข้ามาในห้องเรา -> ประตูล็อค (ไอคอนแม่กุญแจสีแดงกะพริบเตือน [LOCKED])
///    - ผีเสี่ยเห็นเรา -> ไอคอนดวงตาสีแดงกะพริบ (Spotted Eye Alert)
///    - ตู้ที่ผีเสี่ยจะไปดม -> ตัวตู้เปลี่ยนสีเตือนชัดเจน พร้อมไอคอนหัวคว่ำชี้เป้า
/// 3. ปลอดภัย ไม่กระทบระบบอีเวนต์เดิมที่ทำไว้
/// </summary>
[DisallowMultipleComponent]
public class SniffGhostHUD : MonoBehaviour
{
    private static SniffGhostHUD _instance;
    public static SniffGhostHUD Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<SniffGhostHUD>();
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("=== Ghost Reference ===")]
    [Tooltip("Reference to SecondEventGhost (auto-searched if unassigned)")]
    public SecondEventGhost ghost;

    [Header("=== Debug & Preview ===")]
    [Tooltip("Force show HUD for testing/preview")]
    public bool debugForceShow = false;

    [Header("=== Event Range ===")]
    public int eventStartRoom = 45;
    public int eventEndRoom = 48;
    [Tooltip("ซ่อน UI อัตโนมัติถ้ายังไม่ถึงห้องอีเวนต์และไม่มีผีในฉาก")]
    public bool autoHideOutsideEvent = true;

    [Header("=== Master CanvasGroup ===")]
    public CanvasGroup masterCanvasGroup;
    public float fadeSpeed = 8f;

    [Header("=== 1. Door Locked Danger Alert (เตือนเมื่อผีอยู่ในห้องเราจนประตูล็อค) ===")]
    [Tooltip("Panel แสดงเตือนประตูล็อค (จะปรากฏขึ้นเฉพาะตอนผีอยู่ในห้องเราเท่านั้น นอกนั้นซ่อนหมดจด)")]
    public RectTransform doorLockAlertPanel;
    public CanvasGroup doorLockCanvasGroup;
    public Image doorLockIconImage;
    public TextMeshProUGUI doorLockStatusText;

    [Header("=== 2. Spotted / Chase Symbolic Eye Alert ===")]
    [Tooltip("ไอคอนดวงตาสีแดงแจ้งเตือนเมื่อผีเสี่ยเห็นเราแล้ว")]
    public RectTransform spottedAlertPanel;
    public CanvasGroup spottedCanvasGroup;
    public Image spottedIconImage;
    public TextMeshProUGUI spottedText;
    public float spottedPulseSpeed = 8f;

    [Header("=== 3. Locker Sniff Indicator & Color Tint ===")]
    [Tooltip("GameObject ป้ายไอคอนเตือนกะพริบลอยเหนือหัวตู้ที่ผีเสี่ยกำลังจะดม")]
    public GameObject lockerWarningBadge;
    public RectTransform lockerWarningRect;
    public CanvasGroup lockerWarningCanvasGroup;
    public Image lockerWarningIcon;
    public Vector3 lockerHeadOffset = new Vector3(0f, 2.5f, 0f);
    public float lockerBlinkSpeed = 6f;

    [Tooltip("สีที่ตู้จะเปลี่ยนไปตอนตกเป็นเป้าหมาย (สีแดงเตือนภัยเด่นชัด)")]
    public Color lockerTargetColor = new Color(1.0f, 0.22f, 0.22f, 1f);
    [Tooltip("ความเข้มข้นของการย้อมสีตู้")]
    [Range(0.1f, 1f)]
    public float lockerTintIntensity = 0.85f;

    [Header("=== Symbolic Icons ===")]
    public Sprite iconDoorLocked;
    public Sprite iconEyeSpotted;
    public Sprite iconLockerHazard;
    public Sprite iconWarningWhite;
    public Sprite iconWarningYellow;
    public Sprite iconWarningRed;

    [Header("=== Font Asset ===")]
    public TMP_FontAsset fontAsset;

    // Runtime state
    private Camera mainCamera;
    private bool isGhostActive = false;
    private GameObject currentTargetLocker = null;
    private readonly Dictionary<SpriteRenderer, Color> originalLockerColors = new Dictionary<SpriteRenderer, Color>();

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

        mainCamera = Camera.main;
        LoadDefaultSpritesIfNeeded();
        BuildUIHierarchyIfNeeded();
    }

    void Start()
    {
        if (ghost == null)
        {
            ghost = (SecondEventGhost.ActiveGhosts.Count > 0 ? System.Linq.Enumerable.FirstOrDefault(SecondEventGhost.ActiveGhosts) : null) ?? FindObjectOfType<SecondEventGhost>();
        }
    }

    void OnDisable()
    {
        RestoreLockerColors();
    }

    void OnDestroy()
    {
        RestoreLockerColors();
    }

    void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // หาอ้างอิงผีเสี่ยถ้ายังไม่มี
        if (ghost == null)
        {
            ghost = (SecondEventGhost.ActiveGhosts.Count > 0 ? System.Linq.Enumerable.FirstOrDefault(SecondEventGhost.ActiveGhosts) : null) ?? FindObjectOfType<SecondEventGhost>();
        }

        // ตรวจสอบว่าอยู่ในช่วงอีเวนต์ผีเสี่ยหรือไม่
        int currentRoom = RoomCounter.instance != null ? RoomCounter.instance.PassedRoomCount : 0;
        bool inEventRange = (currentRoom >= eventStartRoom && currentRoom <= eventEndRoom);
        isGhostActive = (ghost != null && ghost.gameObject.activeInHierarchy) || inEventRange || debugForceShow;

        // อัปเดต Alpha รวมของ Master HUD
        if (masterCanvasGroup != null)
        {
            float targetAlpha = (autoHideOutsideEvent && !isGhostActive && !debugForceShow) ? 0f : 1f;
            masterCanvasGroup.alpha = Mathf.MoveTowards(masterCanvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            masterCanvasGroup.blocksRaycasts = masterCanvasGroup.alpha > 0.5f;

            if (masterCanvasGroup.alpha <= 0.01f)
            {
                if (doorLockAlertPanel != null && doorLockAlertPanel.gameObject.activeSelf)
                {
                    doorLockAlertPanel.gameObject.SetActive(false);
                }
                if (lockerWarningBadge != null && lockerWarningBadge.activeSelf)
                {
                    lockerWarningBadge.SetActive(false);
                }
                RestoreLockerColors();
                return;
            }
        }

        UpdateDoorLockedDangerAlert();
        UpdateSpottedAlert();
        UpdateLockerSniffAndColorWarning();
    }

    #region 1. Door Locked Danger Alert (แสดงเฉพาะตอนผีอยู่ในห้องเราจนประตูล็อค)

    private void UpdateDoorLockedDangerAlert()
    {
        bool inPlayerRoom = (ghost != null && ghost.IsGhostInPlayerRoom);

        if (doorLockCanvasGroup != null)
        {
            float targetAlpha = inPlayerRoom ? 1f : 0f;
            doorLockCanvasGroup.alpha = Mathf.MoveTowards(doorLockCanvasGroup.alpha, targetAlpha, Time.deltaTime * (inPlayerRoom ? 16f : 8f));

            if (doorLockCanvasGroup.alpha > 0.02f)
            {
                if (doorLockAlertPanel != null && !doorLockAlertPanel.gameObject.activeSelf)
                {
                    doorLockAlertPanel.gameObject.SetActive(true);
                }

                // เอฟเฟกต์กะพริบสีแดงเตือนภัยว่าประตูล็อค
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 10f);
                Color redDanger = new Color(1f, 0.2f, 0.2f, pulse);

                if (doorLockStatusText != null)
                {
                    doorLockStatusText.text = "LOCKED";
                    doorLockStatusText.color = redDanger;
                }

                if (doorLockIconImage != null)
                {
                    if (iconDoorLocked != null) doorLockIconImage.sprite = iconDoorLocked;
                    doorLockIconImage.color = redDanger;
                    doorLockIconImage.transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.time * 8f));
                }
            }
            else
            {
                if (doorLockAlertPanel != null && doorLockAlertPanel.gameObject.activeSelf)
                {
                    doorLockAlertPanel.gameObject.SetActive(false);
                }
            }
        }
    }

    #endregion

    #region 2. Ghost Spotted Player Alert UI (Symbolic Eye)

    private void UpdateSpottedAlert()
    {
        bool isChasing = (ghost != null && ghost.IsChasingPlayer);

        if (spottedCanvasGroup != null)
        {
            float targetAlpha = isChasing ? 1f : 0f;
            spottedCanvasGroup.alpha = Mathf.MoveTowards(spottedCanvasGroup.alpha, targetAlpha, Time.deltaTime * (isChasing ? 18f : 8f));

            if (spottedCanvasGroup.alpha > 0.05f)
            {
                if (spottedAlertPanel != null && !spottedAlertPanel.gameObject.activeSelf)
                {
                    spottedAlertPanel.gameObject.SetActive(true);
                }

                // แอนิเมชันดวงตาสีแดงกะพริบระทึกขวัญ (Pulsing Dilated Eye)
                float pulse = 1.0f + Mathf.Sin(Time.time * spottedPulseSpeed) * 0.18f;
                spottedAlertPanel.localScale = Vector3.one * pulse;

                if (spottedIconImage != null)
                {
                    if (iconEyeSpotted != null) spottedIconImage.sprite = iconEyeSpotted;
                    spottedIconImage.color = new Color(1f, 0.2f, 0.2f, 1f);
                }

                if (spottedText != null)
                {
                    spottedText.text = "DETECTED";
                    spottedText.color = new Color(1f, 0.2f, 0.2f, 1f);
                }
            }
            else
            {
                if (spottedAlertPanel != null && spottedAlertPanel.gameObject.activeSelf)
                {
                    spottedAlertPanel.gameObject.SetActive(false);
                }
            }
        }
    }

    #endregion

    #region 3. Locker Sniff Indicator & Color Tint (ตู้ที่เสี่ยงโดนจะเปลี่ยนสีเตือนชัดเจน)

    private void UpdateLockerSniffAndColorWarning()
    {
        // ค้นหาตู้เป้าหมายที่ผีเสี่ยกำลังจะไปดม
        GameObject targetLocker = null;
        if (ghost != null)
        {
            if (ghost.CurrentLockerTarget != null)
            {
                targetLocker = ghost.CurrentLockerTarget;
            }
            else if (ghost.lockerToSniff != null && ghost.lockerToSniff.Count > 0)
            {
                targetLocker = ghost.lockerToSniff[0];
            }
        }

        // 1. จัดการการเปลี่ยนสีของตัวตู้ (Change Locker Color to make it clearly visible)
        if (targetLocker != null)
        {
            ApplyLockerColorHighlight(targetLocker);
        }
        else
        {
            RestoreLockerColors();
        }

        // 2. จัดการป้ายไอคอนกะพริบเหนือหัวตู้ (Symbolic Floating Hazard Icon)
        if (targetLocker == null)
        {
            if (lockerWarningBadge != null && lockerWarningBadge.activeSelf)
            {
                if (lockerWarningCanvasGroup != null)
                {
                    lockerWarningCanvasGroup.alpha = Mathf.MoveTowards(lockerWarningCanvasGroup.alpha, 0f, Time.deltaTime * 12f);
                    if (lockerWarningCanvasGroup.alpha <= 0.01f)
                    {
                        lockerWarningBadge.SetActive(false);
                    }
                }
                else
                {
                    lockerWarningBadge.SetActive(false);
                }
            }
            return;
        }

        if (lockerWarningBadge != null)
        {
            if (!lockerWarningBadge.activeSelf)
            {
                lockerWarningBadge.SetActive(true);
                if (lockerWarningCanvasGroup != null) lockerWarningCanvasGroup.alpha = 0f;
            }

            // คำนวณตำแหน่งเหนือหัวตู้
            Vector3 worldPos = targetLocker.transform.position + lockerHeadOffset;
            Collider2D col = targetLocker.GetComponent<Collider2D>();
            if (col != null)
            {
                worldPos = new Vector3(col.bounds.center.x, col.bounds.max.y + 0.5f, col.bounds.center.z);
            }

            // เพิ่มแอนิเมชันลอยขึ้นลงเล็กน้อย (Floating Bobbing Motion)
            float bob = Mathf.Sin(Time.time * lockerBlinkSpeed) * 0.12f;
            worldPos.y += bob;

            if (lockerWarningBadge.transform.parent == transform || lockerWarningBadge.GetComponent<Canvas>() == null)
            {
                if (mainCamera != null)
                {
                    Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPos);
                    if (screenPoint.z > 0)
                    {
                        Canvas rootCanvas = GetComponentInParent<Canvas>();
                        if (rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                        {
                            RectTransform rootRt = rootCanvas.GetComponent<RectTransform>();
                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRt, screenPoint, null, out Vector2 localPt))
                            {
                                lockerWarningRect.anchoredPosition = localPt;
                            }
                            else
                            {
                                lockerWarningRect.position = screenPoint;
                            }
                        }
                        else
                        {
                            lockerWarningRect.position = screenPoint;
                        }
                    }
                }
            }
            else
            {
                lockerWarningBadge.transform.position = worldPos;
            }

            // เอฟเฟกต์กะพริบเร็วเตือนภัย (Blinking Hazard Pulse)
            float blinkAlpha = 0.4f + Mathf.PingPong(Time.time * lockerBlinkSpeed, 0.6f);
            if (lockerWarningCanvasGroup != null)
            {
                lockerWarningCanvasGroup.alpha = blinkAlpha;
            }

            float pulseScale = 1.0f + Mathf.Sin(Time.time * lockerBlinkSpeed * 2f) * 0.12f;
            if (lockerWarningRect != null)
            {
                lockerWarningRect.localScale = Vector3.one * pulseScale;
            }

            if (lockerWarningIcon != null)
            {
                if (iconLockerHazard != null) lockerWarningIcon.sprite = iconLockerHazard;
                else if (iconWarningYellow != null) lockerWarningIcon.sprite = iconWarningYellow;
            }
        }
    }

    /// <summary>
    /// ย้อมสี SpriteRenderer ของตู้เป้าหมายให้กะพริบเป็นสีแดงเตือนภัยเด่นชัด
    /// </summary>
    private void ApplyLockerColorHighlight(GameObject lockerGo)
    {
        if (currentTargetLocker != lockerGo)
        {
            RestoreLockerColors();
            currentTargetLocker = lockerGo;

            if (lockerGo != null)
            {
                SpriteRenderer[] renderers = lockerGo.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (SpriteRenderer sr in renderers)
                {
                    if (sr != null && !originalLockerColors.ContainsKey(sr))
                    {
                        originalLockerColors[sr] = sr.color;
                    }
                }
            }
        }

        if (currentTargetLocker != null && originalLockerColors.Count > 0)
        {
            // แอนิเมชันกะพริบสีแดงสลับส้มเตือนภัย (High-Visibility Danger Pulse)
            float t = Mathf.PingPong(Time.time * 5f, 1f);
            Color alertCol = Color.Lerp(lockerTargetColor, new Color(1f, 0.75f, 0.1f, 1f), t);

            foreach (var kvp in originalLockerColors)
            {
                SpriteRenderer sr = kvp.Key;
                if (sr != null)
                {
                    Color orig = kvp.Value;
                    sr.color = Color.Lerp(orig, alertCol, lockerTintIntensity);
                }
            }
        }
    }

    /// <summary>
    /// คืนค่าสีเดิมของตู้เมื่อผีเลิกสนใจหรือดมเสร็จสิ้น
    /// </summary>
    private void RestoreLockerColors()
    {
        if (originalLockerColors.Count > 0)
        {
            foreach (var kvp in originalLockerColors)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.color = kvp.Value;
                }
            }
            originalLockerColors.Clear();
        }
        currentTargetLocker = null;
    }

    #endregion

    #region Helper & Hierarchy Builder

    private void LoadDefaultSpritesIfNeeded()
    {
#if UNITY_EDITOR
        if (iconDoorLocked == null)
            iconDoorLocked = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/Icon_DoorLocked.png");
        if (iconEyeSpotted == null)
            iconEyeSpotted = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/Icon_EyeSpotted.png");
        if (iconLockerHazard == null)
            iconLockerHazard = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/Icon_LockerHazard.png");

        if (iconWarningWhite == null)
            iconWarningWhite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/WarningIcon_White.png");
        if (iconWarningYellow == null)
            iconWarningYellow = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/WarningIcon_Yellow.png");
        if (iconWarningRed == null)
            iconWarningRed = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/WarningIcon_Red.png");

        if (fontAsset == null)
        {
            fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FOR GAYJAM/NON/Font non/Coolvetica Rg SDF.asset");
            if (fontAsset == null)
            {
                fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FOR GAYJAM/NON/Font non/XTypewriter-Bold SDF.asset");
            }
        }
#endif
    }

    public void BuildUIHierarchyIfNeeded()
    {
        if (masterCanvasGroup == null)
        {
            masterCanvasGroup = GetComponent<CanvasGroup>();
            if (masterCanvasGroup == null)
            {
                masterCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }

        // ลบแผง Status เดิมที่เคยมี Ghost_Group / SAFE text ถ้ามีอยู่
        Transform oldStatusPanel = transform.Find("SniffGhost_StatusPanel");
        if (oldStatusPanel != null)
        {
            DestroyImmediate(oldStatusPanel.gameObject);
        }

        // 1. Door Locked Danger Alert Panel (มุมบนซ้าย ปรากฏเฉพาะตอนประตูล็อค!)
        if (doorLockAlertPanel == null)
        {
            Transform existing = transform.Find("SniffGhost_DoorLockAlert");
            if (existing != null)
            {
                doorLockAlertPanel = existing.GetComponent<RectTransform>();
                doorLockCanvasGroup = doorLockAlertPanel.GetComponent<CanvasGroup>();
                var doorImg = doorLockAlertPanel.Find("Door_Icon");
                if (doorImg != null) doorLockIconImage = doorImg.GetComponent<Image>();
                var doorTxt = doorLockAlertPanel.Find("Door_Text");
                if (doorTxt != null) doorLockStatusText = doorTxt.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                GameObject alertGo = new GameObject("SniffGhost_DoorLockAlert", typeof(RectTransform));
                alertGo.transform.SetParent(transform, false);
                doorLockAlertPanel = alertGo.GetComponent<RectTransform>();

                // ตำแหน่ง: มุมบนซ้าย (Top-Left) กะทัดรัด
                doorLockAlertPanel.anchorMin = new Vector2(0f, 1f);
                doorLockAlertPanel.anchorMax = new Vector2(0f, 1f);
                doorLockAlertPanel.pivot = new Vector2(0f, 1f);
                doorLockAlertPanel.anchoredPosition = new Vector2(30f, -30f);
                doorLockAlertPanel.sizeDelta = new Vector2(130f, 44f);

                doorLockCanvasGroup = alertGo.AddComponent<CanvasGroup>();
                doorLockCanvasGroup.alpha = 0f; // เริ่มต้นซ่อนสนิท 100%

                // พื้นหลังสีดำโปร่งแสง Minimalist
                Image bg = alertGo.AddComponent<Image>();
                bg.color = new Color(0.04f, 0.01f, 0.01f, 0.75f);

                HorizontalLayoutGroup hlg = alertGo.AddComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(10, 10, 6, 6);
                hlg.spacing = 8f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = false;
                hlg.childControlHeight = true;

                // Red Locked Padlock Icon
                GameObject dIconGo = new GameObject("Door_Icon", typeof(RectTransform));
                dIconGo.transform.SetParent(alertGo.transform, false);
                doorLockIconImage = dIconGo.AddComponent<Image>();
                if (iconDoorLocked != null) doorLockIconImage.sprite = iconDoorLocked;
                doorLockIconImage.preserveAspect = true;
                dIconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(26f, 26f);

                // Locked Text
                GameObject dTxtGo = new GameObject("Door_Text", typeof(RectTransform));
                dTxtGo.transform.SetParent(alertGo.transform, false);
                doorLockStatusText = dTxtGo.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) doorLockStatusText.font = fontAsset;
                doorLockStatusText.fontSize = 18;
                doorLockStatusText.fontStyle = FontStyles.Bold;
                doorLockStatusText.color = new Color(1f, 0.2f, 0.2f, 1f);
                doorLockStatusText.text = "LOCKED";
                dTxtGo.GetComponent<RectTransform>().sizeDelta = new Vector2(65f, 32f);

                alertGo.SetActive(false); // ซ่อนตอนปกติ
            }
        }

        // 2. Symbolic Spotted Alert (ไอคอนดวงตาสีแดงตรงกลางด้านบน)
        if (spottedAlertPanel == null)
        {
            Transform existing = transform.Find("SniffGhost_SpottedAlert");
            if (existing != null)
            {
                spottedAlertPanel = existing.GetComponent<RectTransform>();
                var eyeImg = spottedAlertPanel.Find("Alert_Icon");
                if (eyeImg != null) spottedIconImage = eyeImg.GetComponent<Image>();
                var alertTxt = spottedAlertPanel.Find("Alert_Text");
                if (alertTxt != null) spottedText = alertTxt.GetComponent<TextMeshProUGUI>();
                spottedCanvasGroup = spottedAlertPanel.GetComponent<CanvasGroup>();
            }
            else
            {
                GameObject alertGo = new GameObject("SniffGhost_SpottedAlert", typeof(RectTransform));
                alertGo.transform.SetParent(transform, false);
                spottedAlertPanel = alertGo.GetComponent<RectTransform>();

                // กลางจอด้านบน
                spottedAlertPanel.anchorMin = new Vector2(0.5f, 0.85f);
                spottedAlertPanel.anchorMax = new Vector2(0.5f, 0.85f);
                spottedAlertPanel.pivot = new Vector2(0.5f, 0.5f);
                spottedAlertPanel.sizeDelta = new Vector2(220f, 60f);

                spottedCanvasGroup = alertGo.AddComponent<CanvasGroup>();
                spottedCanvasGroup.alpha = 0f;

                HorizontalLayoutGroup hlg = alertGo.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 10f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;

                // Eye Icon (เด่นชัด)
                GameObject alertIconGo = new GameObject("Alert_Icon", typeof(RectTransform));
                alertIconGo.transform.SetParent(alertGo.transform, false);
                spottedIconImage = alertIconGo.AddComponent<Image>();
                if (iconEyeSpotted != null) spottedIconImage.sprite = iconEyeSpotted;
                spottedIconImage.preserveAspect = true;
                alertIconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(50f, 50f);

                // Alert Text (สั้นกระชับ)
                GameObject alertTextGo = new GameObject("Alert_Text", typeof(RectTransform));
                alertTextGo.transform.SetParent(alertGo.transform, false);
                spottedText = alertTextGo.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) spottedText.font = fontAsset;
                spottedText.fontSize = 24;
                spottedText.fontStyle = FontStyles.Bold;
                spottedText.color = new Color(1f, 0.22f, 0.22f, 1f);
                spottedText.outlineWidth = 0.25f;
                spottedText.outlineColor = new Color32(0, 0, 0, 255);
                spottedText.text = "DETECTED";
                alertTextGo.GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 40f);

                alertGo.SetActive(false);
            }
        }

        // 3. Symbolic Locker Warning Badge (ไอคอนเตือนภัยหัวตู้ ไม่มีข้อความรกรุงรัง)
        if (lockerWarningBadge == null)
        {
            Transform existing = transform.Find("SniffGhost_LockerWarningBadge");
            if (existing != null)
            {
                lockerWarningBadge = existing.gameObject;
                lockerWarningRect = existing.GetComponent<RectTransform>();
                var badgeImg = lockerWarningBadge.transform.Find("Badge_Icon");
                if (badgeImg != null) lockerWarningIcon = badgeImg.GetComponent<Image>();
                lockerWarningCanvasGroup = lockerWarningBadge.GetComponent<CanvasGroup>();
            }
            else
            {
                lockerWarningBadge = new GameObject("SniffGhost_LockerWarningBadge", typeof(RectTransform));
                lockerWarningBadge.transform.SetParent(transform, false);
                lockerWarningRect = lockerWarningBadge.GetComponent<RectTransform>();
                lockerWarningRect.sizeDelta = new Vector2(48f, 48f);
                lockerWarningRect.pivot = new Vector2(0.5f, 0f);

                lockerWarningCanvasGroup = lockerWarningBadge.AddComponent<CanvasGroup>();

                // Warning Icon Only (Inverted Triangle Hazard Arrow)
                GameObject badgeIconGo = new GameObject("Badge_Icon", typeof(RectTransform));
                badgeIconGo.transform.SetParent(lockerWarningBadge.transform, false);
                lockerWarningIcon = badgeIconGo.AddComponent<Image>();
                if (iconLockerHazard != null) lockerWarningIcon.sprite = iconLockerHazard;
                else if (iconWarningYellow != null) lockerWarningIcon.sprite = iconWarningYellow;
                lockerWarningIcon.preserveAspect = true;
                badgeIconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(44f, 44f);

                lockerWarningBadge.SetActive(false);
            }
        }
    }

    #endregion
}
