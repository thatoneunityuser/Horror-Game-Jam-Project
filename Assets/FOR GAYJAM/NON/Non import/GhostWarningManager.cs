using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ระบบลูกศรเตือนภัยขอบจอเมื่อผีปรากฏตัว (Ghost Warning Screen-Edge Indicator)
/// แสดงลูกศรชี้ไปทางฝั่งที่ผีอยู่ พร้อมขยับซ้าย-ขวาแจ้งเตือนผู้เล่นแบบเรียลไทม์
/// รองรับทั้ง RushEntity (Johnny Rush) อัตโนมัติ และสามารถลากพรีแฟบผีใหม่ๆ มาเพิ่มได้ตลอดเวลา
/// </summary>
[DisallowMultipleComponent]
public class GhostWarningManager : MonoBehaviour
{
    public static GhostWarningManager Instance { get; private set; }

    public enum ArrowPointingDirection
    {
        PointTowardsThreat, // หัวลูกศรชี้ออกไปทางผี (ซ้ายชี้ซ้าย, ขวาชี้ขวา)
        PointInward         // หัวลูกศรชี้เข้ามาในจอ (บอกทิศที่ผีกำลังพุ่งเข้ามา)
    }

    [Header("=== Ghost Selection (เลือกผีที่จะให้มีลูกศร) ===")]
    [Tooltip("ตรวจจับและสร้างลูกศรให้ RushEntity (Johnny Rush / REVERSE JOHNNY) อัตโนมัติ โดยไม่ต้องตั้งค่าใดๆ")]
    public bool autoTrackRushEntity = true;

    [Tooltip("ลากพรีแฟบผีที่ต้องการให้มีลูกศรมาใส่ในลิสต์นี้ได้เลย (เผื่อพี่/เพื่อนนำผีใหม่มาเพิ่มในอนาคต)")]
    public List<GameObject> trackedGhostPrefabs = new List<GameObject>();

    [Tooltip("แท็กของผีที่ต้องการให้ตรวจจับ (เช่น Enemy)")]
    public List<string> trackedTags = new List<string> { "Enemy" };

    [Tooltip("คีย์เวิร์ดชื่อออบเจกต์ของผี (เช่น rush, johnny)")]
    public List<string> trackedNameKeywords = new List<string> { "rush", "johnny" };

    [Header("=== Arrow Visuals (การแสดงผลลูกศร) ===")]
    [Tooltip("รูปภาพลูกศร (หากเว้นว่างจะโหลด GhostArrowIndicator ให้อัตโนมัติ)")]
    public Sprite arrowSprite;

    [Tooltip("สีเริ่มต้นของลูกศร")]
    public Color defaultArrowColor = Color.white;

    [Tooltip("ขนาดของลูกศรบนหน้าจอ (พิกเซล)")]
    public Vector2 arrowSize = new Vector2(56f, 56f);

    [Tooltip("ระยะห่างจากขอบจอ ซ้าย-ขวา (พิกเซล)")]
    public float horizontalMargin = 45f;

    [Tooltip("ระยะเว้นจากขอบจอบน-ล่าง เพื่อไม่ให้ลูกศรล้นจอ (พิกเซล)")]
    public float verticalMargin = 80f;

    [Tooltip("ให้ลูกศรเลื่อนขึ้น-ลงตามระดับความสูง (แกน Y) ของผี")]
    public bool trackVerticalPosition = true;

    [Tooltip("รูปแบบการหันหัวลูกศร")]
    public ArrowPointingDirection pointingDirection = ArrowPointingDirection.PointTowardsThreat;

    [Header("=== Bobbing Animation (การขยับซ้าย-ขวา) ===")]
    [Tooltip("ความเร็วในการขยับซ้าย-ขวา")]
    public float bobSpeed = 7.5f;

    [Tooltip("ระยะที่ลูกศรเด้งขยับเข้า-ออก (พิกเซล)")]
    public float bobDistance = 22f;

    [Tooltip("เปิดเอฟเฟกต์ย่อ-ขยาย (Pulse) ให้ดูลุ้นระทึก")]
    public bool enablePulseScale = true;

    [Tooltip("ความเร็วในการย่อขยาย")]
    public float pulseSpeed = 6f;

    [Tooltip("อัตราย่อขยายสูงสุด")]
    public float pulseMagnitude = 0.12f;

    [Header("=== Detection & Fade Settings ===")]
    [Tooltip("ซ่อนลูกศรเมื่อผีอยู่ในหน้าจอแล้ว")]
    public bool hideWhenOnScreen = false;

    [Tooltip("ความเร็วในการจางเข้า-ออก (Fade)")]
    public float fadeSpeed = 8f;

    [Tooltip("ความถี่ในการสแกนหาผีตัวใหม่ในฉาก (วินาที)")]
    public float scanInterval = 0.5f;

    [Header("=== References (จะค้นหาให้อัตโนมัติหากไม่ได้กำหนด) ===")]
    public Camera targetCamera;
    public Transform playerTransform;
    public RectTransform arrowContainer;

    // ระบบจัดการข้อมูลตัวเตือนผี
    private class TrackedGhost
    {
        public Transform transform;
        public GameObject gameObject;
        public GhostWarningTarget targetComponent;
        public RectTransform arrowRect;
        public CanvasGroup canvasGroup;
        public Image arrowImage;
        public float currentAlpha = 0f;
        public bool isCustomTarget = false;
        public bool hasPlayedAudio = false;
    }

    private readonly List<TrackedGhost> trackedGhosts = new List<TrackedGhost>();
    private readonly HashSet<GhostWarningTarget> registeredTargets = new HashSet<GhostWarningTarget>();
    private float nextScanTime = 0f;
    private AudioSource audioSource;

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

        EnsureDependencies();
    }

    private void Start()
    {
        EnsureDependencies();
    }

    private void EnsureDependencies()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) targetCamera = FindObjectOfType<Camera>();
        }

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (arrowSprite == null)
        {
            arrowSprite = Resources.Load<Sprite>("GhostArrowIndicator");
#if UNITY_EDITOR
            if (arrowSprite == null)
            {
                arrowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/GhostArrowIndicator.png");
            }
#endif
        }

        if (arrowContainer == null)
        {
            // ค้นหา Canvas ที่เป็น Root หรือสร้าง Container ใต้ GameObject นี้
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();

            if (canvas != null)
            {
                Transform existing = canvas.transform.Find("GhostWarning_Container");
                if (existing != null)
                {
                    arrowContainer = existing.GetComponent<RectTransform>();
                }
                else
                {
                    GameObject containerGo = new GameObject("GhostWarning_Container");
                    containerGo.transform.SetParent(canvas.transform, false);
                    arrowContainer = containerGo.AddComponent<RectTransform>();
                    arrowContainer.anchorMin = Vector2.zero;
                    arrowContainer.anchorMax = Vector2.one;
                    arrowContainer.sizeDelta = Vector2.zero;
                    arrowContainer.anchoredPosition = Vector2.zero;
                }
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }

    private void Update()
    {
        // อัปเดตกล้องหรือผู้เล่นหากเพิ่งโหลดเข้าฉากใหม่
        if (targetCamera == null) targetCamera = Camera.main;
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        // ค้นหาผีใหม่ตามรอบเวลา scanInterval
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            ScanForNewGhosts();
        }

        // อัปเดตตำแหน่งและการเคลื่อนไหวของลูกศรทุกลูก
        UpdateArrowIndicators();
    }

    /// <summary>
    /// สแกนหาผีในฉากอัตโนมัติ ทั้ง RushEntity, ออบเจกต์ที่ตรงกับชื่อ/แท็ก และ GhostWarningTarget
    /// </summary>
    private void ScanForNewGhosts()
    {
        // 1. ตรวจสอบ GhostWarningTarget ที่ลงทะเบียนไว้
        foreach (GhostWarningTarget target in registeredTargets)
        {
            if (target != null && !IsAlreadyTracked(target.gameObject))
            {
                AddTrackedGhost(target.gameObject, target);
            }
        }

        // 2. ตรวจสอบ RushEntity อัตโนมัติ
        if (autoTrackRushEntity)
        {
            RushEntity[] rushEntities = FindObjectsOfType<RushEntity>();
            foreach (RushEntity rush in rushEntities)
            {
                if (rush != null && !IsAlreadyTracked(rush.gameObject))
                {
                    GhostWarningTarget target = rush.GetComponent<GhostWarningTarget>();
                    AddTrackedGhost(rush.gameObject, target);
                }
            }
        }

        // 3. ตรวจสอบจาก AnomalyCheck (ถ้ามี)
        if (AnomalyCheck.instance != null)
        {
            // AnomalyCheck จะคอยเก็บ Anomaly ไว้
        }

        // 4. ตรวจสอบจาก Tracked Prefabs / Keywords / Tags
        if (trackedGhostPrefabs != null && trackedGhostPrefabs.Count > 0)
        {
            foreach (GameObject prefab in trackedGhostPrefabs)
            {
                if (prefab == null) continue;
                GameObject[] found = GameObject.FindGameObjectsWithTag("Enemy");
                foreach (GameObject go in found)
                {
                    if (go.name.Contains(prefab.name) && !IsAlreadyTracked(go))
                    {
                        AddTrackedGhost(go, go.GetComponent<GhostWarningTarget>());
                    }
                }
            }
        }

        // 5. ตรวจสอบจากชื่อคีย์เวิร์ด
        if (trackedNameKeywords != null && trackedNameKeywords.Count > 0)
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (GameObject enemy in enemies)
            {
                if (IsAlreadyTracked(enemy)) continue;
                string lowerName = enemy.name.ToLower();
                foreach (string kw in trackedNameKeywords)
                {
                    if (!string.IsNullOrEmpty(kw) && lowerName.Contains(kw.ToLower()))
                    {
                        AddTrackedGhost(enemy, enemy.GetComponent<GhostWarningTarget>());
                        break;
                    }
                }
            }
        }
    }

    private bool IsAlreadyTracked(GameObject go)
    {
        if (go == null) return true;
        for (int i = 0; i < trackedGhosts.Count; i++)
        {
            if (trackedGhosts[i].gameObject == go) return true;
        }
        return false;
    }

    private void AddTrackedGhost(GameObject go, GhostWarningTarget targetComp)
    {
        if (go == null || arrowContainer == null) return;

        // สร้าง UI ลูกศรสำหรับผีตัวนี้
        GameObject arrowGo = new GameObject($"GhostArrow_{go.name}");
        arrowGo.transform.SetParent(arrowContainer, false);

        RectTransform rt = arrowGo.AddComponent<RectTransform>();
        rt.sizeDelta = arrowSize;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        CanvasGroup cg = arrowGo.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        Image img = arrowGo.AddComponent<Image>();
        img.sprite = (targetComp != null && targetComp.customArrowSprite != null) ? targetComp.customArrowSprite : arrowSprite;
        img.color = (targetComp != null) ? targetComp.arrowColor : defaultArrowColor;
        img.preserveAspect = true;
        img.raycastTarget = false;

        TrackedGhost tracked = new TrackedGhost
        {
            transform = go.transform,
            gameObject = go,
            targetComponent = targetComp,
            arrowRect = rt,
            canvasGroup = cg,
            arrowImage = img,
            currentAlpha = 0f,
            isCustomTarget = (targetComp != null),
            hasPlayedAudio = false
        };

        trackedGhosts.Add(tracked);

        // เล่นเสียงแจ้งเตือนครั้งแรกถ้ามีกำหนดไว้
        if (targetComp != null && targetComp.warningSfx != null && audioSource != null)
        {
            audioSource.PlayOneShot(targetComp.warningSfx, targetComp.warningSfxVolume);
            tracked.hasPlayedAudio = true;
        }
    }

    private void UpdateArrowIndicators()
    {
        if (targetCamera == null || arrowContainer == null) return;

        Canvas rootCanvas = arrowContainer.GetComponentInParent<Canvas>();
        float scaleFactor = (rootCanvas != null) ? rootCanvas.scaleFactor : 1f;
        if (scaleFactor <= 0.001f) scaleFactor = 1f;

        Vector2 canvasSize = arrowContainer.rect.size;
        if (canvasSize.x <= 0 || canvasSize.y <= 0)
        {
            canvasSize = new Vector2(Screen.width / scaleFactor, Screen.height / scaleFactor);
        }

        float halfW = canvasSize.x * 0.5f;
        float halfH = canvasSize.y * 0.5f;

        for (int i = trackedGhosts.Count - 1; i >= 0; i--)
        {
            TrackedGhost ghost = trackedGhosts[i];

            // หากออบเจกต์ผีถูกทำลายหรือปิดการทำงาน ให้ทำลายลูกศรและลบออกจากลิสต์
            if (ghost.gameObject == null || !ghost.gameObject.activeInHierarchy || ghost.transform == null)
            {
                if (ghost.arrowRect != null)
                {
                    Destroy(ghost.arrowRect.gameObject);
                }
                trackedGhosts.RemoveAt(i);
                continue;
            }

            Vector3 ghostWorldPos = ghost.transform.position;
            Vector3 viewportPos = targetCamera.WorldToViewportPoint(ghostWorldPos);

            // ตรวจสอบระยะทางสูงสุด (ถ้ามีการกำหนดไว้)
            bool withinDistance = true;
            if (ghost.targetComponent != null && ghost.targetComponent.maxWarningDistance > 0f)
            {
                Vector3 referencePos = (playerTransform != null) ? playerTransform.position : targetCamera.transform.position;
                float dist = Vector2.Distance(ghostWorldPos, referencePos);
                if (dist > ghost.targetComponent.maxWarningDistance)
                {
                    withinDistance = false;
                }
            }

            // ตรวจสอบว่าอยู่ในจอหรือไม่
            bool isOnScreen = (viewportPos.z > 0 && viewportPos.x >= 0f && viewportPos.x <= 1f && viewportPos.y >= 0f && viewportPos.y <= 1f);
            bool shouldHideOnScreen = (ghost.targetComponent != null) ? ghost.targetComponent.hideWhenOnScreen : hideWhenOnScreen;

            bool shouldShow = withinDistance && (!shouldHideOnScreen || !isOnScreen);

            // คำนวณฝั่งที่ผีอยู่: ซ้าย (isLeft = true) หรือ ขวา (isLeft = false)
            bool isLeft;
            if (playerTransform != null)
            {
                isLeft = (ghostWorldPos.x < playerTransform.position.x);
            }
            else
            {
                isLeft = (viewportPos.x < 0.5f);
            }

            // ── การคำนวณการขยับซ้าย-ขวา (Horizontal Bobbing) ──
            float activeBobSpeed = (ghost.targetComponent != null && ghost.targetComponent.customBobSpeed > 0) ? ghost.targetComponent.customBobSpeed : bobSpeed;
            float activeBobDist = (ghost.targetComponent != null && ghost.targetComponent.customBobDistance > 0) ? ghost.targetComponent.customBobDistance : bobDistance;

            // ใช้ Sine Wave หรือ PingPong ให้ลูกศรเด้งขยับเข้า-ออกจากขอบจอ
            float bobOffset = (Mathf.Sin(Time.time * activeBobSpeed) * 0.5f + 0.5f) * activeBobDist;

            float targetX;
            float arrowRotationZ;

            if (isLeft)
            {
                // ผีอยู่ฝั่งซ้าย: ลูกศรอยู่ที่ขอบจอซ้าย และขยับเข้าหาตรงกลางแล้วเด้งกลับ
                targetX = -halfW + horizontalMargin + bobOffset;

                // รูปลูกศรชี้ลง (downward caret):
                // หมุน -90° (หรือ 270°) ให้หัวลูกศรชี้ไปทางซ้าย (ชี้ไปทางผี)
                if (pointingDirection == ArrowPointingDirection.PointTowardsThreat)
                {
                    arrowRotationZ = -90f; // ชี้ไปทางซ้าย
                }
                else
                {
                    arrowRotationZ = 90f;  // ชี้เข้ามาในจอ (ทางขวา)
                }
            }
            else
            {
                // ผีอยู่ฝั่งขวา: ลูกศรอยู่ที่ขอบจอขวา และขยับเข้าหาตรงกลางแล้วเด้งกลับ
                targetX = halfW - horizontalMargin - bobOffset;

                // หมุน +90° ให้หัวลูกศรชี้ไปทางขวา (ชี้ไปทางผี)
                if (pointingDirection == ArrowPointingDirection.PointTowardsThreat)
                {
                    arrowRotationZ = 90f;  // ชี้ไปทางขวา
                }
                else
                {
                    arrowRotationZ = -90f; // ชี้เข้ามาในจอ (ทางซ้าย)
                }
            }

            // ── การคำนวณตำแหน่งแนวตั้ง (Y Position) ──
            float targetY = 0f;
            if (trackVerticalPosition)
            {
                // แปลงพิกัด Viewport Y เป็นตำแหน่งบน Canvas พร้อมตัดขอบบน-ล่าง
                float clampedVpY = Mathf.Clamp(viewportPos.y, 0f, 1f);
                targetY = Mathf.Lerp(-halfH + verticalMargin, halfH - verticalMargin, clampedVpY);
            }

            // กำหนดตำแหน่งและการหมุน
            ghost.arrowRect.anchoredPosition = new Vector2(targetX, targetY);
            ghost.arrowRect.localEulerAngles = new Vector3(0f, 0f, arrowRotationZ);

            // ── เอฟเฟกต์ย่อขยาย Pulse Scale ──
            if (enablePulseScale)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseMagnitude;
                Vector2 baseScale = (ghost.targetComponent != null) ? ghost.targetComponent.arrowScale : Vector2.one;
                ghost.arrowRect.localScale = new Vector3(baseScale.x * pulse, baseScale.y * pulse, 1f);
            }

            // ── จัดการการจางเข้า-ออก (Smooth Fade In / Out) ──
            float targetAlpha = shouldShow ? 1f : 0f;
            ghost.currentAlpha = Mathf.MoveTowards(ghost.currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
            ghost.canvasGroup.alpha = ghost.currentAlpha;
        }
    }

    #region Public API สำหรับลงทะเบียนผี

    /// <summary>
    /// ลงทะเบียนผีผ่านสคริปต์ GhostWarningTarget
    /// </summary>
    public void RegisterTarget(GhostWarningTarget target)
    {
        if (target == null) return;
        if (!registeredTargets.Contains(target))
        {
            registeredTargets.Add(target);
        }
        if (!IsAlreadyTracked(target.gameObject))
        {
            AddTrackedGhost(target.gameObject, target);
        }
    }

    /// <summary>
    /// ยกเลิกการลงทะเบียนเมื่อผีตายหรือปิดตัว
    /// </summary>
    public void UnregisterTarget(GhostWarningTarget target)
    {
        if (target == null) return;
        registeredTargets.Remove(target);

        for (int i = trackedGhosts.Count - 1; i >= 0; i--)
        {
            if (trackedGhosts[i].gameObject == target.gameObject)
            {
                if (trackedGhosts[i].arrowRect != null)
                {
                    Destroy(trackedGhosts[i].arrowRect.gameObject);
                }
                trackedGhosts.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>
    /// สั่งติดตามผีตัวใหม่จากสคริปต์ภายนอก
    /// </summary>
    public static void TrackGhost(GameObject ghostGo)
    {
        if (Instance != null && ghostGo != null)
        {
            GhostWarningTarget targetComp = ghostGo.GetComponent<GhostWarningTarget>();
            if (!Instance.IsAlreadyTracked(ghostGo))
            {
                Instance.AddTrackedGhost(ghostGo, targetComp);
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureGhostWarningManagerExists()
    {
        if (Instance != null) return;

        GhostWarningManager existing = FindObjectOfType<GhostWarningManager>();
        if (existing != null) return;

        GameplayHUDManager hud = FindObjectOfType<GameplayHUDManager>();
        if (hud != null)
        {
            GhostWarningManager mgr = hud.gameObject.AddComponent<GhostWarningManager>();
            hud.ghostWarningManager = mgr;
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            canvas.gameObject.AddComponent<GhostWarningManager>();
        }
    }

    #endregion
}
