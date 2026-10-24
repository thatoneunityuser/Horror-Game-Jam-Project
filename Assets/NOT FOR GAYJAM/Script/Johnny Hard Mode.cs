using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class JohnnyHardMode : MonoBehaviour
{
    public static JohnnyHardMode Instance;


    // ── Phase / State ──────────────────────────────────────────────────────────
    public enum phasestate { Spawn, Phase1, Phase2, Phase3 }
    public phasestate currentPhase;
    [Range(1, 99)] public int Phase2_Threshould_Percent = 66;
    [Range(1, 99)] public int Phase3_Threshould_Percent = 33;

    public enum MovementStyle { Fly_Above_Player, Hold_On_The_Air, Dash_to_Player }
    public MovementStyle currentMovementStyle;
 

    private Camera GameCamera;
    private Transform Player;

    // ── Basic Stats ───────────────────────────────────────────────────────────
    [Header("Basic Stats")]
    public float Hp;
    public const int MAX_HEALTH = 676767;
    public TextMeshProUGUI healthText;
    public Slider hpbar;

    // ── Hurt / Damage Text ────────────────────────────────────────────────────
    [Header("Hurt")]
    public GameObject DamageTextPrefab;
    public Canvas DamageCanvas;
    public AnimationCurve textanimation;
    public Color Textalpha;

    // ── Fly Above Player ──────────────────────────────────────────────────────
    [Header("Fly Above Player")]
    public float flyHeight = 3f;        // how far above the player Johnny hovers
    public float flyDriftSpeed = 2f;    // horizontal drift speed left/right
    public float flyDriftRange = 4f;    // max horizontal distance from player center
    public float flyDriftRangeY = 1.5f; // max vertical distance from player center (above/below flyHeight)
    public float flyVerticalSpeed = 5f; // vertical speed to reach hover height
    private float flyDriftDir = 1f;     // current drift direction (+1 right, -1 left)
    private float flyDriftAngle = 0f;    // current angle of drift in radians
    public float flySmoothTime = 0.3f;  // time to reach target; lower is faster/tighter, higher is smoother/floatier
    public float flyMaxSpeed = 15f;     // maximum movement speed when flying
    private Vector3 flyVelocity = Vector3.zero; // tracking velocity for SmoothDamp
    private Rigidbody2D rb2d;


    // ── Dash ──────────────────────────────────────────────────────────────────
    [Header("Dash Skill")]
    public float dashSpeed = 18f;
    public float dashCoolDown = 0.5f;
    public float DamageOnDash = 20f;
    [Range(3, 8)] public int minDashes = 3;
    [Range(3, 8)] public int maxDashes = 8;
    public AudioClip dashSound;
    private bool isDashing = false;
    private bool damagedThisDash = false;
    private AudioSource audioSource;
  

    // ── Skill Scheduler ───────────────────────────────────────────────────────
    public enum SkillState { Chicken_Spam, Dash, Circular_Laser, BulletHell,SixSeven_Barrage,}
    [Header("Skill Scheduler")]
    public SkillState currentSkill;
    public SkillState[] phase1Skills = new SkillState[]
    {
        SkillState.Dash,
        SkillState.Circular_Laser,
        SkillState.BulletHell
    };
    public SkillState[] phase2Skills = new SkillState[]
    {
        SkillState.Dash,
        SkillState.Circular_Laser,
        SkillState.BulletHell,
        SkillState.SixSeven_Barrage
    };
    public float minSkillDuration = 5f;
    public float maxSkillDuration = 12f;

    // ── Chicken Spam ──────────────────────────────────────────────────────────
    [Header("Chicken Spam")]
    public GameObject chickenPrefab;
    public Transform chickenSpawnPoint;
    public float chickenCooldown = 0.6f;
    public int burstEveryNShots = 10;
    public float burstChickenSpeed = 8f;
    [Tooltip("5 spread angles in degrees (0 = toward player). Shown as gizmos.")]
    public float[] spreadAngles = new float[] { -60f, -30f, 0f, 30f, 60f };
    public float gizmoRayLength = 3f;
    public AudioClip chickenSpamSound;
    private int chickenShotCount = 0;

    // ── Circular Laser ────────────────────────────────────────────────────────
    [Header("Circular Laser")]
    public Transform[] laserRigs;                     // pivot(s) that rotate toward the player
    public GameObject[] lasers;                     // the 2 laser GameObjects — toggled on/off
    public GameObject[] ChargeUpEffect; 
    [Range(0f, 1f)] public float laserRotateSlowPercent = 0.20f;  // 20% slower than full tracking
    public float circularLaserDuration = 6f;
    public AudioClip circularLaserSound;
   

    // ── Bullet Hell ───────────────────────────────────────────────────────────
    [Header("Bullet Hell")]
    public GameObject bulletHellPrefab;
    public float bulletHellCooldown = 0.4f;
    public float bulletHellDuration = 8f;
    public float bulletSpeed = 6f;
    public AudioClip CHIMIMIIIII;

    [Header("Six Seven Barrage")]
    public GameObject sixSevenPrefab;
    public Transform sixSevenSpawnpoint;
    public float sixcooldown = 0.05f;
    public float sixsevenspeed = 8f;
    public float sixSevenDuration = 6f;
    public int sixSevenBarrageArms = 4;
    public float sixSevenBarrageAngleStep = 15f;
    public AudioClip SIXSEVEN;


    private bool isTransitioning = false;
    private float activeSpiralAngle;
    private bool isSixSevenBarrageActive = false;


    // ── Private coroutine handles ──────────────────────────────────────────────
    private Coroutine chickenCoroutine;
    private Coroutine skillCoroutine;
    private Coroutine schedulerCoroutine;
    private float stateSwitchTimer;

    // ─────────────────────────────────────────────────────────────────────────

    [Header("Phase 2 Entering")]
    public GameObject CutScene;
    public PlayableDirector CutsceneToPlayBeforeAllfunctionworked;

    [Header ("Phase 3 Entering")]
    public GameObject ChargeEffect;
    public Transform whereJohnnyCharging;
    private const int chargetime = 67;
    private Volume globalvolume;
    private Vignette vignette;
  
    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GameCamera = Camera.main;
        currentPhase = phasestate.Spawn;
        Hp = MAX_HEALTH;
        
        rb2d = GetComponent<Rigidbody2D>();

        globalvolume = FindObjectOfType<Volume>();
        if (globalvolume != null && globalvolume.profile != null)
        {
            globalvolume.profile.TryGet<Vignette>(out vignette);
        }


        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            Player = playerObj.transform;
        else
            Debug.LogError("JohnnyHardMode: No GameObject tagged 'Player' found!");

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        Invoke(nameof(EnterPhase1), 1f);
    }

    void Update()
    {
        // ── UI ─────────────────────────────────────────────────────────────────
        healthText.text = Hp.ToString("F0") + "/" + MAX_HEALTH.ToString();
        hpbar.value = Hp / MAX_HEALTH;

        if (Player == null || isTransitioning) return;

        // ── Phase transitions ──────────────────────────────────────────────────
        float hpPercent = (Hp / MAX_HEALTH) * 100f;
        if (currentPhase == phasestate.Phase1 && hpPercent <= Phase2_Threshould_Percent)
            EnterPhase2();
        else if (currentPhase == phasestate.Phase2 && hpPercent <= Phase3_Threshould_Percent)
            StartCoroutine(EnterPhase3());
    }

    // ── Movement ───────────────────────────────────────────────────────────────
    void FixedUpdate()
    {
        if (Player == null || isDashing) return;

        switch (currentMovementStyle)
        {
            case MovementStyle.Fly_Above_Player:
                Fly_Above_Player();
                break;
            case MovementStyle.Hold_On_The_Air:
                // Stop immediately
                flyVelocity = Vector3.zero;
                if (rb2d != null)
                {
                    rb2d.linearVelocity = Vector2.zero;
                    rb2d.angularVelocity = 0f;
                }
                break;
            case MovementStyle.Dash_to_Player:
                // Movement handled entirely in DashBurst coroutine
                break;
        }
    }

    void Fly_Above_Player()
    {
        if (Player == null) return;

        // Progress the angle around the ellipse
        // Angular speed = linear speed / radius. We use flyDriftRange as reference radius.
        float angularSpeed = flyDriftSpeed / Mathf.Max(0.1f, flyDriftRange);
        flyDriftAngle += angularSpeed * Time.fixedDeltaTime;
        if (flyDriftAngle > Mathf.PI * 2f)
            flyDriftAngle -= Mathf.PI * 2f;

        // Compute target position on the ellipse relative to the player
        float targetOffsetX = Mathf.Cos(flyDriftAngle) * flyDriftRange;
        float targetOffsetY = flyHeight + Mathf.Sin(flyDriftAngle) * flyDriftRangeY;

        Vector3 targetPos = new Vector3(
            Player.position.x + targetOffsetX,
            Player.position.y + targetOffsetY,
            transform.position.z
        );

        // Smoothly follow the player and the orbit target with acceleration/deceleration.
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            targetPos, 
            ref flyVelocity, 
            flySmoothTime, 
            flyMaxSpeed, 
            Time.fixedDeltaTime
        );
    }

    // ── Phase Transitions ──────────────────────────────────────────────────────
    void EnterPhase1()
    {
        currentPhase = phasestate.Phase1;
        Debug.Log("[JohnnyHardMode] Entered Phase 1");
        ReturnToBase();
        if (schedulerCoroutine != null) StopCoroutine(schedulerCoroutine);
        schedulerCoroutine = StartCoroutine(SkillScheduler());
    }

    void EnterPhase2()
    {
        currentPhase = phasestate.Phase2;
        minDashes = 6;
        
        Debug.Log("[JohnnyHardMode] Entered Phase 2");
        StartCoroutine(Phase2Transition());
    }

    // EnterPhase3 is now implemented as an IEnumerator at the bottom of the file.

    // ── Skill Helpers ──────────────────────────────────────────────────────────

    // Cancels all running skill coroutines
    void StopAllSkillCoroutines()
    {
        if (chickenCoroutine != null) { StopCoroutine(chickenCoroutine); chickenCoroutine = null; }
        if (skillCoroutine   != null) { StopCoroutine(skillCoroutine);   skillCoroutine   = null; }
    }

    // Returns to the default: Fly_Above_Player + Chicken_Spam
    void ReturnToBase()
    {
        StopAllSkillCoroutines();
        currentMovementStyle = MovementStyle.Fly_Above_Player;
        currentSkill = SkillState.Chicken_Spam;
        isDashing = false;
       

        if (Player != null)
        {
            Vector2 relativePos = (Vector2)(transform.position - Player.position) - new Vector2(0f, flyHeight);
            flyDriftAngle = Mathf.Atan2(relativePos.y, relativePos.x);
        }
        else
        {
            flyDriftAngle = 0f;
        }

        chickenCoroutine = StartCoroutine(ChickenSpamLogic());
    }

    // Randomly picks and starts the next skill after a random wait
    IEnumerator SkillScheduler()
    {
        while (true)
        {
            // Wait a random duration before picking next skill
            yield return new WaitForSeconds(Random.Range(minSkillDuration, maxSkillDuration));

            // Only switch if we're currently in the base state (avoid interrupting mid-dash etc.)
            if (currentSkill != SkillState.Chicken_Spam) continue;

            SkillState[] activeSkills = (currentPhase == phasestate.Phase2 || currentPhase == phasestate.Phase3)
                ? phase2Skills
                : phase1Skills;

            if (activeSkills == null || activeSkills.Length == 0) continue;

            SkillState next = activeSkills[Random.Range(0, activeSkills.Length)];
            Debug.Log($"[JohnnyHardMode] Starting skill: {next}");
            StopAllSkillCoroutines();

            switch (next)
            {
                case SkillState.Dash:
                    currentSkill = SkillState.Dash;
                    skillCoroutine = StartCoroutine(DashBurst());
                    break;
                case SkillState.Circular_Laser:
                    currentSkill = SkillState.Circular_Laser;
                    skillCoroutine = StartCoroutine(CircularLaserLogic());
                    break;
                case SkillState.BulletHell:
                    currentSkill = SkillState.BulletHell;
                    skillCoroutine = StartCoroutine(BulletHellLogic());
                    break;
                case SkillState.SixSeven_Barrage:
                    currentSkill = SkillState.SixSeven_Barrage;
                    skillCoroutine = StartCoroutine(SixSevenBarrageLogic());
                    break;
            }

            // Wait until the skill finishes (it calls ReturnToBase itself at the end)
            yield return new WaitUntil(() => currentSkill == SkillState.Chicken_Spam);
        }
    }

    // ── Skill: Chicken Spam ────────────────────────────────────────────────────
    IEnumerator ChickenSpamLogic()
    {
        while (true)
        {
            yield return new WaitForSeconds(chickenCooldown);

            if (Player == null || chickenPrefab == null || chickenSpawnPoint == null)
                continue;

            Vector2 toPlayer = (Vector2)(Player.position - chickenSpawnPoint.position);
            float angleToPlayer = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;

            chickenShotCount++;

            if (audioSource != null && chickenSpamSound != null)
            {
                audioSource.PlayOneShot(chickenSpamSound);
            }

            if (chickenShotCount >= burstEveryNShots)
            {
                // Spread burst
                chickenShotCount = 0;
                int count = Mathf.Min(spreadAngles.Length, 5);
                for (int i = 0; i < count; i++)
                {
                    float spreadAngle = angleToPlayer + spreadAngles[i];
                    float rad = spreadAngle * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                    Quaternion rot = Quaternion.Euler(0f, 0f, spreadAngle);
                    GameObject c = Instantiate(chickenPrefab, chickenSpawnPoint.position, rot);

                    Rigidbody2D crb = c.GetComponent<Rigidbody2D>();
                    if (crb != null) crb.linearVelocity = dir * burstChickenSpeed;
                }
            }
            else
            {
                // Normal single shot
                Quaternion rot = Quaternion.Euler(0f, 0f, angleToPlayer);
                GameObject c = Instantiate(chickenPrefab, chickenSpawnPoint.position, rot);

                Rigidbody2D crb = c.GetComponent<Rigidbody2D>();
                if (crb != null) crb.linearVelocity = toPlayer.normalized * burstChickenSpeed;
            }
        }
    }

    // ── Skill: Dash ───────────────────────────────────────────────────────────
    IEnumerator DashBurst()
    {
        // Delay 0.5s before casting skill (holding in place)
        currentMovementStyle = MovementStyle.Hold_On_The_Air;
        yield return new WaitForSeconds(0.5f);

        currentMovementStyle = MovementStyle.Dash_to_Player;
        isDashing = true;
        

        int dashes = Random.Range(minDashes, maxDashes + 1); // 3–8
        
        Vector2 dir = Vector2.zero;
        if (Player != null)
            dir = ((Vector2)(Player.position - transform.position)).normalized;

        for (int i = 0; i < dashes; i++)
        {
            if (audioSource != null && dashSound != null)
            {
                audioSource.PlayOneShot(dashSound);
            }
            damagedThisDash = false;
            float elapsed = 0f;

            while (elapsed < dashCoolDown)
            {
                transform.position += (Vector3)(dir * dashSpeed * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Re-aim toward player for next segment
            if (Player != null)
                dir = ((Vector2)(Player.position - transform.position)).normalized;
        }


        isDashing = false;
        ReturnToBase();
    }

    // ── Skill: Circular Laser ─────────────────────────────────────────────────
    IEnumerator CircularLaserLogic()
    {
        currentMovementStyle = MovementStyle.Hold_On_The_Air;

        // Show charge-up effects
        if (ChargeUpEffect != null)
            foreach (var fx in ChargeUpEffect)
                if (fx != null) fx.SetActive(true);

        yield return new WaitForSeconds(0.5f);

        if (audioSource != null && circularLaserSound != null)
        {
            audioSource.PlayOneShot(circularLaserSound);
        }

        // Turn lasers ON
        if (lasers != null)
            foreach (var l in lasers)
                if (l != null) l.SetActive(true);

        // Spin speed: 0% slow = 360°/s (full speed), 100% slow = 0°/s
        float spinSpeed = (1f - laserRotateSlowPercent) * 360f;
        float elapsed = 0f;

        while (elapsed < circularLaserDuration)
        {
            elapsed += Time.deltaTime;

            // Spin all rigs continuously on Z axis
            if (laserRigs != null)
            {
                foreach (Transform rig in laserRigs)
                {
                    if (rig == null) continue;
                    rig.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
                }
            }

            yield return null;
        }

        // Turn lasers and charge-up effects OFF
        if (lasers != null)
            foreach (var l in lasers)
                if (l != null) l.SetActive(false);

        if (ChargeUpEffect != null)
            foreach (var fx in ChargeUpEffect)
                if (fx != null) fx.SetActive(false);

        ReturnToBase();
    }

    // ── Skill: Bullet Hell ────────────────────────────────────────────────────
    IEnumerator BulletHellLogic()
    {
        currentMovementStyle = MovementStyle.Hold_On_The_Air;
        yield return new WaitForSeconds(0.5f);

        if (audioSource != null && CHIMIMIIIII != null)
        {
            audioSource.PlayOneShot(CHIMIMIIIII);
        }

        float endTime = Time.time + bulletHellDuration; // use real clock — not deltaTime accumulation

        while (Time.time < endTime)
        {
            if (bulletHellPrefab != null && Player != null && GameCamera != null)
            {
                // Pick a random point on the camera frame edge (viewport coords)
                Vector2 spawnViewport = RandomCameraEdgePoint();
                Vector3 spawnWorld = GameCamera.ViewportToWorldPoint(
                    new Vector3(spawnViewport.x, spawnViewport.y, GameCamera.nearClipPlane + 1f)
                );
                spawnWorld.z = 0f;

                // Aim at player
                Vector2 dir = ((Vector2)Player.position - (Vector2)spawnWorld).normalized;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                Quaternion rot = Quaternion.Euler(0f, 0f, angle);

                GameObject bullet = Instantiate(bulletHellPrefab, spawnWorld, rot);

                Rigidbody2D brb = bullet.GetComponent<Rigidbody2D>();
                if (brb != null) brb.linearVelocity = dir * bulletSpeed;
            }

            yield return new WaitForSeconds(bulletHellCooldown);
        }

        ReturnToBase();
    }

    IEnumerator Phase2Transition()
    {
        lasers[0].SetActive(false);
        lasers[1].SetActive(false);
        ChargeUpEffect[0].SetActive(false);
        ChargeUpEffect[1].SetActive(false);
        isTransitioning = true;
        currentMovementStyle = MovementStyle.Hold_On_The_Air;
        StopAllSkillCoroutines();
        if (schedulerCoroutine != null) StopCoroutine(schedulerCoroutine);

        flyVelocity = Vector3.zero;
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }

        if (CutScene != null)
        {
            CutScene.SetActive(true);
        }

        if (CutsceneToPlayBeforeAllfunctionworked != null)
        {
            CutsceneToPlayBeforeAllfunctionworked.Play();
            yield return new WaitForSeconds(0.1f);
            while (CutsceneToPlayBeforeAllfunctionworked.state == UnityEngine.Playables.PlayState.Playing)
            {
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(2f);
        }

        isTransitioning = false;
        yield return StartCoroutine(SixSevenBarrageLogic());
        
        if (schedulerCoroutine != null) StopCoroutine(schedulerCoroutine);
        schedulerCoroutine = StartCoroutine(SkillScheduler());
    }

    IEnumerator SixSevenBarrageLogic()
    {
        currentMovementStyle = MovementStyle.Hold_On_The_Air;
        yield return new WaitForSeconds(0.5f);

        currentSkill = SkillState.SixSeven_Barrage;
        isSixSevenBarrageActive = true;

        if (audioSource != null && SIXSEVEN != null)
        {
            audioSource.PlayOneShot(SIXSEVEN);
        }

        if (chickenCoroutine != null) { StopCoroutine(chickenCoroutine); chickenCoroutine = null; }

        float elapsed = 0f;
        // Compute rotation speed (deg/sec) from step and cooldown, with a guaranteed minimum
        float rotSpeed = (sixcooldown > 0f && sixSevenBarrageAngleStep > 0f)
            ? (sixSevenBarrageAngleStep / sixcooldown)
            : 300f; // fallback: ~1 revolution per second
        rotSpeed = Mathf.Max(rotSpeed, 60f); // ensure at least 60 deg/sec
        float startTime = Time.time;

        while (elapsed < sixSevenDuration)
        {
            // Time-based angle — guaranteed to spin, just like the gizmo preview
            float angle = ((Time.time - startTime) * rotSpeed) % 360f;
            activeSpiralAngle = angle;

            // Spin arm rigs in sync with the spiral barrage
            if (laserRigs != null)
            {
                for (int i = 0; i < laserRigs.Length; i++)
                {
                    if (laserRigs[i] == null) continue;
                    float rigAngle = angle + (i * 360f / laserRigs.Length);
                    laserRigs[i].rotation = Quaternion.Euler(0f, 0f, rigAngle);
                }
            }

            for (int arm = 0; arm < sixSevenBarrageArms; arm++)
            {
                float armAngle = angle + (arm * 360f / sixSevenBarrageArms);
                float rad = armAngle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Quaternion rot = Quaternion.Euler(0f, 0f, armAngle);

                if (sixSevenPrefab != null && sixSevenSpawnpoint != null)
                {
                    GameObject c = Instantiate(sixSevenPrefab, sixSevenSpawnpoint.position, rot);
                    Rigidbody2D crb = c.GetComponent<Rigidbody2D>();
                    if (crb != null)
                    {
                        crb.linearVelocity = dir * sixsevenspeed;
                    }
                }
            }

            yield return new WaitForSeconds(sixcooldown);
            elapsed += sixcooldown;
        }

        isSixSevenBarrageActive = false;
        ReturnToBase();
    }

    // Returns a random point on the viewport edge (0–1 range on each side)
    Vector2 RandomCameraEdgePoint()
    {
        int side = Random.Range(0, 4); // 0=top, 1=bottom, 2=left, 3=right
        switch (side)
        {
            case 0: return new Vector2(Random.value, 1f);  // top
            case 1: return new Vector2(Random.value, 0f);  // bottom
            case 2: return new Vector2(0f, Random.value);  // left
            default:return new Vector2(1f, Random.value);  // right
        }
    }

    // ── Trigger: Dash damage ──────────────────────────────────────────────────
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isDashing) return;

        if (other.CompareTag("Player") && !damagedThisDash)
        {
            PlayerStats ps = other.GetComponent<PlayerStats>();
            if (ps != null)
            {
                ps.TakeDamage(DamageOnDash);
                damagedThisDash = true;
            }
        }


    }

    // ── Gizmos ────────────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        // Fly area rings (drawn around player)
        Vector3 center = transform.position;
#if UNITY_EDITOR
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) center = playerObj.transform.position;
#endif
        // Cyan = hover height above player
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center + Vector3.up * flyHeight, 0.3f);
        Gizmos.DrawLine(center, center + Vector3.up * flyHeight);

        // Red ellipse = drift range (oval) around hover point
        Gizmos.color = Color.red;
        Vector3 ellipseCenter = center + Vector3.up * flyHeight;
        int segments = 36;
        Vector3 prevPoint = ellipseCenter + new Vector3(flyDriftRange, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i * 360f / segments) * Mathf.Deg2Rad;
            Vector3 nextPoint = ellipseCenter + new Vector3(
                Mathf.Cos(angle) * flyDriftRange,
                Mathf.Sin(angle) * flyDriftRangeY,
                0f
            );
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // ── 67 Spiral Barrage Active Gizmos ────────────────────────────────────
        if (isSixSevenBarrageActive && sixSevenSpawnpoint != null)
        {
            Gizmos.color = Color.magenta;
            for (int arm = 0; arm < sixSevenBarrageArms; arm++)
            {
                float armAngle = activeSpiralAngle + (arm * 360f / sixSevenBarrageArms);
                float rad = armAngle * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                Gizmos.DrawRay(sixSevenSpawnpoint.position, dir * gizmoRayLength);
                Gizmos.DrawSphere(sixSevenSpawnpoint.position + dir * gizmoRayLength, 0.15f);
            }
        }
        // ── 67 Spiral Barrage Editor Preview ───────────────────────────────────
        else if (!Application.isPlaying && sixSevenSpawnpoint != null && UnityEditor.Selection.Contains(gameObject))
        {
            Gizmos.color = new Color(1f, 0.2f, 0.8f, 0.5f);
            float previewAngle = (Time.realtimeSinceStartup * 60f) % 360f;
            for (int arm = 0; arm < sixSevenBarrageArms; arm++)
            {
                float armAngle = previewAngle + (arm * 360f / sixSevenBarrageArms);
                float rad = armAngle * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                Gizmos.DrawRay(sixSevenSpawnpoint.position, dir * gizmoRayLength * 1.5f);
                Gizmos.DrawWireSphere(sixSevenSpawnpoint.position + dir * gizmoRayLength * 1.5f, 0.1f);
            }
        }

        // Chicken spread rays
        if (chickenSpawnPoint != null && spreadAngles != null &&
            UnityEditor.Selection.Contains(gameObject))
        {
            float baseAngle = 0f;
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                Vector2 toP = (Vector2)(playerObj.transform.position - chickenSpawnPoint.position);
                baseAngle = Mathf.Atan2(toP.y, toP.x) * Mathf.Rad2Deg;
            }

            for (int i = 0; i < spreadAngles.Length; i++)
            {
                float angle = (baseAngle + spreadAngles[i]) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Gizmos.color = (i == spreadAngles.Length / 2) ? Color.yellow : new Color(1f, 0.5f, 0f);
                Gizmos.DrawRay(chickenSpawnPoint.position, dir * gizmoRayLength);
                Gizmos.DrawSphere(chickenSpawnPoint.position + dir * gizmoRayLength, 0.1f);
            }
        }
    }
#endif

    // ── Take damage ───────────────────────────────────────────────────────────
    public void TakeDamage(float damageAmount)
    {
        if (isTransitioning) return;

        Hp -= damageAmount;
        Hp = Mathf.Clamp(Hp, 0, MAX_HEALTH);

        if (DamageTextPrefab != null && DamageCanvas != null)
        {
            GameObject textObj = Instantiate(DamageTextPrefab, DamageCanvas.transform);

            RectTransform canvasRect = DamageCanvas.GetComponent<RectTransform>();
            RectTransform rt = textObj.GetComponent<RectTransform>();
            if (rt != null && canvasRect != null)
            {
                Vector2 screenPos = Camera.main.WorldToScreenPoint(transform.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPos,
                    DamageCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
                    out Vector2 localPos
                );
                rt.localPosition = new Vector3(
                    localPos.x + Random.Range(-2f, 2f),
                    localPos.y + Random.Range(0f, 2f), 0f);
            }

            TextMeshProUGUI tmp = textObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = "-" + (int)damageAmount;
                tmp.color = Textalpha;
                StartCoroutine(ShowDamage(tmp, textObj));
            }
        }

        if (Hp <= 0)
            UnityEngine.SceneManagement.SceneManager.LoadScene("YouWin");
    }

    IEnumerator ShowDamage(TextMeshProUGUI tmp, GameObject textObj)
    {
        float t = 0f;
        while (t < 1f)
        {
            if (textObj == null) yield break;
            t += Time.deltaTime;
            Color c = Textalpha;
            c.a = Textalpha.a * (1f - t);
            tmp.color = c;
            yield return null;
        }
        if (textObj != null)
            Destroy(textObj);
    }
    IEnumerator EnterPhase3()
    {
        currentPhase = phasestate.Phase3;
        Debug.Log("[JohnnyHardMode] Entered Phase 3");
        lasers[0].SetActive(false);
        lasers[1].SetActive(false);
        ChargeUpEffect[0].SetActive(false);
        ChargeUpEffect[1].SetActive(false);


        // Cancel all existing loop/skill actions
        StopAllSkillCoroutines();
        if (schedulerCoroutine != null)
        {
            StopCoroutine(schedulerCoroutine);
            schedulerCoroutine = null;
        }

        // Freeze Johnny at the charging location
        if (whereJohnnyCharging != null)
        {
            transform.position = whereJohnnyCharging.position;
        }
        currentMovementStyle = MovementStyle.Hold_On_The_Air;
        flyVelocity = Vector3.zero;
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }

        // Activate the charging visual effect
        if (ChargeEffect != null)
        {
            ChargeEffect.SetActive(true);
        }

        // Start the background countdown to death (GameOver)
        StartCoroutine(Phase3DeathTimer());

        // Continuously fire Bullet Hell at the player
        while (true)
        {
            if (bulletHellPrefab != null && Player != null && GameCamera != null)
            {
                // Pick a random point on the camera frame edge
                Vector2 spawnViewport = RandomCameraEdgePoint();
                Vector3 spawnWorld = GameCamera.ViewportToWorldPoint(
                    new Vector3(spawnViewport.x, spawnViewport.y, GameCamera.nearClipPlane + 1f)
                );
                spawnWorld.z = 0f;

                // Aim directly at player
                Vector2 dir = ((Vector2)Player.position - (Vector2)spawnWorld).normalized;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                Quaternion rot = Quaternion.Euler(0f, 0f, angle);

                GameObject bullet = Instantiate(bulletHellPrefab, spawnWorld, rot);

                Rigidbody2D brb = bullet.GetComponent<Rigidbody2D>();
                if (brb != null) brb.linearVelocity = dir * bulletSpeed;
            }

            yield return new WaitForSeconds(bulletHellCooldown+0.2f);
        }
    }

    IEnumerator Phase3DeathTimer()
    {
        float elapsed = 0f;
        float startIntensity = 0f;

        if (vignette != null)
        {
            startIntensity = vignette.intensity.value;
        }

        while (elapsed < chargetime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / chargetime;
            float currentVal = Mathf.Lerp(startIntensity, 1f, progress);

            if (vignette != null)
            {
                vignette.intensity.overrideState = true;
                vignette.intensity.value = currentVal;
            }

            yield return null;
        }

        if (vignette != null)
        {
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 1f;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("CUTSCENE");
    }
}
