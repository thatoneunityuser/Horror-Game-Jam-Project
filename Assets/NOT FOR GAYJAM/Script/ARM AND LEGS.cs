using UnityEngine;
using System.Collections;

// ═══════════════════════════════════════════════════════════════════════════
// ARM AND LEGS — Detachable limb AI for Johnny boss fight
// ═══════════════════════════════════════════════════════════════════════════
// State loop:
//   In_The_Body (6s) → move to edge → On_The_Edge_Of_Screen (aim at player)
//   → Steady_Aim (reposition + 1s freeze) → Attack → back to edge (3s)
//   → return to body → repeat
//
// Steady Aim behaviour:
//   Arms  — slide to player's Y, freeze 1 second, then attack
//   Legs  — decrease Y, slide X toward assigned edge, freeze 1 second
//
// Attack types (1 per GameObject, set in Inspector):
//   PewPewPew — chicken-spam style burst fire
//   Laser     — toggled laser beam that tracks the player
//   Dash      — single high-speed charge toward the player
// ═══════════════════════════════════════════════════════════════════════════

public class ARMANDLEGS : MonoBehaviour
{
    // ── State Machine ────────────────────────────────────────────────────────
    public enum State { In_The_Body, On_The_Edge_Of_Screen, Steady_Aim, Attack }
    public State currentState;

    public enum EdgePos { Top_Left, Top_right, Bottom_Left, Bottom_Right }
    public EdgePos currentEdgePos;

    public enum LimbType { Arm, Leg }
    [Header("Limb Config")]
    public LimbType limbType;

    public enum AttackType { PewPewPew, Laser, Dash }
    public AttackType attackType;

    // ── Timing ───────────────────────────────────────────────────────────────
    [Header("Timing")]
    [Tooltip("How long the limb stays attached to the body before detaching")]
    public float bodyDuration = 6f;
    [Tooltip("How long the limb waits at the edge after attacking before returning to body")]
    public float edgeWaitAfterAttack = 3f;
    [Tooltip("Freeze duration during Steady_Aim before attacking")]
    public float steadyAimDuration = 1f;
    [Tooltip("How long the attack lasts")]
    public float attackDuration = 3f;

    // ── Movement ─────────────────────────────────────────────────────────────
    [Header("Movement")]
    public float moveSpeed = 10f;
    [Tooltip("Viewport margin from screen edge (0-1). Smaller = closer to edge.")]
    [Range(0.02f, 0.2f)] public float edgeMargin = 0.08f;
    [Tooltip("How far legs drop below the player during Steady_Aim")]
    public float legDropOffset = 2f;
    [Tooltip("How far legs slide horizontally toward their edge during Steady_Aim")]
    public float legSlideOffset = 2f;

    // ── PewPewPew Attack (Chicken Spam) ──────────────────────────────────────
    [Header("PewPewPew Attack")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 0.3f;
    public float bulletSpeed = 8f;
    public int burstEveryNShots = 5;
    [Tooltip("Spread angles for burst shots (degrees, 0 = toward player)")]
    public float[] spreadAngles = new float[] { -30f, -15f, 0f, 15f, 30f };

    // ── Laser Attack ─────────────────────────────────────────────────────────
    [Header("Laser Attack")]
    public GameObject laserObject;
    public Transform laserRig;
    [Range(0f, 1f)] public float laserRotateSlowPercent = 0.20f;
    public GameObject chargeUpEffect;

    // ── Dash Attack ──────────────────────────────────────────────────────────
    [Header("Dash Attack")]
    public float dashSpeed = 18f;
    public float dashDamage = 20f;

    [Header("References")]
    [Tooltip("Drag your game camera here. Falls back to Camera.main if empty.")]
    public Camera gameCamera;
    [Tooltip("The parent transform to attach to during In_The_Body. Falls back to current parent if empty.")]
    public Transform bodyParent;
    [Tooltip("If checked, the limb will attach using the manual position and rotation specified below. If unchecked, it will automatically record and use the edit-time position and rotation on Awake.")]
    public bool manualAttachedPose = false;
    [Tooltip("The manual local position of the limb when attached to the body (only used if Manual Attached Pose is checked).")]
    public Vector3 bodyLocalPosition;
    [Tooltip("The manual local rotation (Euler angles) of the limb when attached to the body (only used if Manual Attached Pose is checked).")]
    public Vector3 bodyLocalRotEuler;
    [Tooltip("The target scale/size of the limb when unparented from the body.")]
    public Vector3 bodyLocalSize = Vector3.zero;
    [Tooltip("How fast the limb scales up to bodyLocalSize when unparented. Set to 0 or negative for instant scaling.")]
    public float scaleSpeed = 5f;
    [Tooltip("Extra rotation offset (Euler angles) added on top of the look-at-player rotation when unparented.")]
    public Vector3 unparentedRotEuler;
    [Tooltip("Length of the debug ray drawn in Scene view to show aim direction.")]
    public float aimGizmoLength = 3f;
    private Transform player;
    private Vector3 bodyLocalScale;     // local scale when attached to body
    private Quaternion parentLocalRot;  // local rotation of the parent in edit mode
    private Quaternion laserRigLocalRot; // local rotation of the laser rig in edit mode
    private float attackStartAngle;     // the angle to the player at the start of attack
    private bool isDashing = false;
    private bool damagedThisDash = false;
    

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Record the parent and scale
        if (bodyParent == null) bodyParent = transform.parent;
        bodyLocalScale = transform.localScale;

        // If manual override is disabled, automatically record the edit-mode pose
        if (!manualAttachedPose)
        {
            bodyLocalPosition = transform.localPosition;
            bodyLocalRotEuler = transform.localEulerAngles;
        }

        if (bodyParent != null)
        {
            parentLocalRot = bodyParent.localRotation;
        }

        if (bodyLocalSize == Vector3.zero)
        {
            bodyLocalSize = bodyLocalScale;
        }

        if (laserRig != null)
        {
            laserRigLocalRot = laserRig.localRotation;
        }
    }

    void Start()
    {
        if (gameCamera == null) gameCamera = Camera.main;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
        else
            Debug.LogError("[ARMANDLEGS] No GameObject tagged 'Player' found!");

     

        currentState = State.In_The_Body;
        StartCoroutine(StateLoop());
    }

    void Update()
    {
        // Look at player in every state except In_The_Body
        // BUT for laser limbs, ONLY look at player during Steady_Aim state, using laserRotateSlowPercent.
        if (currentState != State.In_The_Body)
        {
            if (attackType == AttackType.Laser)
            {
                if (currentState == State.Steady_Aim)
                {
                    AimLaserAtPlayer();
                }
            }
            else
            {
                AimAtPlayer();
            }
        }

        // Scale management: scale up to bodyLocalSize when unparented
        if (currentState != State.In_The_Body)
        {
            if (scaleSpeed > 0f)
            {
                transform.localScale = Vector3.MoveTowards(transform.localScale, bodyLocalSize, scaleSpeed * Time.deltaTime);
            }
            else
            {
                transform.localScale = bodyLocalSize;
            }
        }
        else
        {
            // Scale is handled by InTheBodyLogic / ReAttachToBody, but we ensure it is bodyLocalScale
            transform.localScale = bodyLocalScale;
        }
        if (JohnnyHardMode.Instance != null && JohnnyHardMode.Instance.currentPhase == JohnnyHardMode.phasestate.Phase3)
        {
          currentState = State.In_The_Body;
          ReAttachToBody();
          StopAllCoroutines();
          return;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  STATE MACHINE LOOP
    // ═════════════════════════════════════════════════════════════════════════

    IEnumerator StateLoop()
    {
        while (true)
        {
            // ── 1. In_The_Body ────────────────────────────────────────────────
            currentState = State.In_The_Body;
            ReAttachToBody();
            yield return StartCoroutine(InTheBodyLogic());

            // ── 2. Detach & fly to screen edge ───────────────────────────────
            DetachFromBody();
            currentState = State.On_The_Edge_Of_Screen;
            yield return StartCoroutine(MoveToWorldPosition(GetEdgeWorldPosition()));

            // ── 3. Steady_Aim — reposition + freeze ──────────────────────────
            currentState = State.Steady_Aim;
            yield return StartCoroutine(SteadyAimLogic());

            // ── 4. Attack ────────────────────────────────────────────────────
            currentState = State.Attack;
            RecordAttackStartAngle();
            yield return StartCoroutine(AttackLogic());

            // ── 5. Return to edge, follow player for edgeWaitAfterAttack ──────
            currentState = State.On_The_Edge_Of_Screen;
            yield return StartCoroutine(MoveToWorldPosition(GetEdgeWorldPosition()));
            yield return StartCoroutine(FollowPlayerAtEdge(edgeWaitAfterAttack));

            // ── 6. Fly back to body & re-attach ──────────────────────────────
            Vector3 bodyTarget = (bodyParent != null)
                ? bodyParent.TransformPoint(bodyLocalPosition)
                : transform.position;
            yield return StartCoroutine(MoveToWorldPosition(bodyTarget));
        }
    }

    /// <summary>
    /// Unparent so the limb moves freely in world space,
    /// unaffected by the boss flying/dashing around.
    /// </summary>
    void DetachFromBody()
    {
        Vector3 currentWorldPos = transform.position;
        Quaternion currentWorldRot = transform.rotation;

        // Use false to prevent Unity from corrupting the local rotation/scale matrices
        transform.SetParent(null, false);

        // Restore correct positioning in world space
        transform.position = currentWorldPos;
        transform.rotation = currentWorldRot;

        if (scaleSpeed <= 0f)
        {
            transform.localScale = bodyLocalSize;
        }
        else
        {
            transform.localScale = bodyLocalScale;
        }
    }

    /// <summary>
    /// Re-parent back to the boss and snap to the saved local position.
    /// </summary>
    void ReAttachToBody()
    {
        if (bodyParent != null)
        {
            // Reset the parent's rotation to its default edit mode rotation
            bodyParent.localRotation = parentLocalRot;

            // Use false to bypass Unity's worldPositionStays calculation
            transform.SetParent(bodyParent, false);
            transform.localPosition = bodyLocalPosition;
            transform.localRotation = Quaternion.Euler(bodyLocalRotEuler);
            transform.localScale = bodyLocalScale;

            if (laserRig != null)
            {
                laserRig.localRotation = laserRigLocalRot;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  STATE: In_The_Body
    // ═════════════════════════════════════════════════════════════════════════

    IEnumerator InTheBodyLogic()
    {
        float elapsed = 0f;
        while (elapsed < bodyDuration)
        {
            if (bodyParent != null)
            {
                // Ensure parent stays reset
                bodyParent.localRotation = parentLocalRot;
            }

            // Force saved pose so the limb stays glued to the body
            transform.localPosition = bodyLocalPosition;
            transform.localRotation = Quaternion.Euler(bodyLocalRotEuler);
            transform.localScale = bodyLocalScale;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  EDGE POSITION HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    Vector3 GetEdgeWorldPosition()
    {
        if (gameCamera == null) return transform.position;

        float m = edgeMargin;
        Vector3 vp;

        switch (currentEdgePos)
        {
            case EdgePos.Top_Left:     vp = new Vector3(m, 1f - m, 0f);       break;
            case EdgePos.Top_right:    vp = new Vector3(1f - m, 1f - m, 0f);  break;
            case EdgePos.Bottom_Left:  vp = new Vector3(m, m, 0f);            break;
            case EdgePos.Bottom_Right: vp = new Vector3(1f - m, m, 0f);       break;
            default:                   vp = new Vector3(0.5f, 0.5f, 0f);      break;
        }

        Vector3 world = gameCamera.ViewportToWorldPoint(
            new Vector3(vp.x, vp.y, gameCamera.nearClipPlane + 10f));
        world.z = 0f;
        return world;
    }

    /// <summary>
    /// Returns the edge world position but with one axis tracking the player.
    /// Arms (left/right edge): X locked at edge, Y follows player.
    /// Legs (top/bottom edge): Y locked at edge, X follows player.
    /// </summary>
    Vector3 GetEdgeFollowingPlayer()
    {
        Vector3 edgePos = GetEdgeWorldPosition();
        if (player == null) return edgePos;

        if (limbType == LimbType.Arm)
        {
            // Arms sit on left/right edge — follow player's Y
            edgePos.y = player.position.y;
        }
        else
        {
            // Legs sit on top/bottom edge — follow player's X
            edgePos.x = player.position.x;
        }

        return edgePos;
    }

    /// <summary>
    /// Stay at the screen edge while following the player for a set duration.
    /// </summary>
    IEnumerator FollowPlayerAtEdge(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            Vector3 target = GetEdgeFollowingPlayer();
            transform.position = Vector3.MoveTowards(
                transform.position, target, moveSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// Returns true if this limb's edge is on the left side of the screen.
    /// </summary>
    bool IsLeftEdge()
    {
        return currentEdgePos == EdgePos.Top_Left || currentEdgePos == EdgePos.Bottom_Left;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MOVEMENT & AIMING
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Smoothly moves to a world position. Forces world position even if this
    /// object is a child of something (as per design: "even if it's child of
    /// something, it will force to move to the edge").
    /// </summary>
    IEnumerator MoveToWorldPosition(Vector3 target)
    {
        while (Vector2.Distance(transform.position, target) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, target, moveSpeed * Time.deltaTime);

            // Aim at player while moving (only for non-laser limbs)
            if (attackType != AttackType.Laser)
            {
                AimAtPlayer();
            }

            yield return null;
        }

        transform.position = new Vector3(target.x, target.y, transform.position.z);
    }

    void RecordAttackStartAngle()
    {
        if (player != null)
        {
            Vector2 dir = (Vector2)(player.position - transform.position);
            attackStartAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
        else
        {
            attackStartAngle = 0f;
        }
    }

    void AimAtPlayer()
    {
        if (player == null) return;
        Vector2 dir = (Vector2)(player.position - transform.position);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        // Look at player + apply extra angle offset from unparentedRotEuler
        transform.rotation = Quaternion.Euler(
            unparentedRotEuler.x,
            unparentedRotEuler.y,
            angle + unparentedRotEuler.z
        );
    }

    void AimLaserAtPlayer()
    {
        if (player == null) return;

        if (laserRig != null)
        {
            Vector2 toPlayer = (Vector2)(player.position - laserRig.position);
            float targetAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg - 90f;
            float trackSpeed = (1f - laserRotateSlowPercent) * 180f; // deg/sec
            float curAngle = laserRig.eulerAngles.z;
            float newAngle = Mathf.MoveTowardsAngle(curAngle, targetAngle, trackSpeed * Time.deltaTime);
            laserRig.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }
        else
        {
            Vector2 toPlayer = (Vector2)(player.position - transform.position);
            float targetAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg + unparentedRotEuler.z;
            float trackSpeed = (1f - laserRotateSlowPercent) * 180f; // deg/sec
            float curAngle = transform.eulerAngles.z;
            float newAngle = Mathf.MoveTowardsAngle(curAngle, targetAngle, trackSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(unparentedRotEuler.x, unparentedRotEuler.y, newAngle);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  STATE: Steady_Aim
    // ═════════════════════════════════════════════════════════════════════════

    IEnumerator SteadyAimLogic()
    {
        if (player == null)
        {
            yield return new WaitForSeconds(steadyAimDuration);
            yield break;
        }

        if (limbType == LimbType.Arm)
        {
            // ── Arms: slide to player's Y, keep current X ────────────────────
            Vector3 aimPos = new Vector3(transform.position.x, player.position.y, 0f);
            yield return StartCoroutine(MoveToWorldPosition(aimPos));
        }
        else // Leg
        {
            // ── Legs: decrease Y, then slide X toward assigned edge ──────────
            Vector3 legPos = transform.position;

            // Step 1 — move down (decrease Y)
            legPos.y = player.position.y - legDropOffset;
            yield return StartCoroutine(MoveToWorldPosition(legPos));

            // Step 2 — slide X toward the edge side
            if (IsLeftEdge())
                legPos.x -= legSlideOffset;
            else
                legPos.x += legSlideOffset;
            yield return StartCoroutine(MoveToWorldPosition(legPos));
        }

        // Freeze in place, aiming at player, for steadyAimDuration
        // (Aiming is handled automatically in Update while in Steady_Aim state)
        yield return new WaitForSeconds(steadyAimDuration);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  STATE: Attack
    // ═════════════════════════════════════════════════════════════════════════

    IEnumerator AttackLogic()
    {
        switch (attackType)
        {
            case AttackType.PewPewPew:
                yield return StartCoroutine(PewPewPewAttack());
                break;
            case AttackType.Laser:
                yield return StartCoroutine(LaserAttack());
                break;
            case AttackType.Dash:
                yield return StartCoroutine(DashAttack());
                break;
        }
    }

    // ── PewPewPew — Chicken Spam ─────────────────────────────────────────────
    IEnumerator PewPewPewAttack()
    {
        Transform spawnPt = (firePoint != null) ? firePoint : transform;
        int shotsFired = 0;
        float elapsed = 0f;

        while (elapsed < attackDuration)
        {
            if (player != null && bulletPrefab != null)
            {
                Vector2 toPlayer = (Vector2)(player.position - spawnPt.position);
                float angleToPlayer = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;

                shotsFired++;

                if (burstEveryNShots > 0 && shotsFired % burstEveryNShots == 0
                    && spreadAngles != null && spreadAngles.Length > 0)
                {
                    // ── Spread burst (like Johnny's burst fire) ──────────────
                    for (int i = 0; i < spreadAngles.Length; i++)
                    {
                        float sAngle = angleToPlayer + spreadAngles[i];
                        float rad = sAngle * Mathf.Deg2Rad;
                        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                        Quaternion rot = Quaternion.Euler(0f, 0f, sAngle);
                        GameObject b = Instantiate(bulletPrefab, spawnPt.position, rot);

                        Rigidbody2D rb = b.GetComponent<Rigidbody2D>();
                        if (rb != null) rb.linearVelocity = dir * bulletSpeed;
                    }
                }
                else
                {
                    // ── Single shot aimed at player ──────────────────────────
                    Quaternion rot = Quaternion.Euler(0f, 0f, angleToPlayer);
                    GameObject b = Instantiate(bulletPrefab, spawnPt.position, rot);

                    Rigidbody2D rb = b.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.linearVelocity = toPlayer.normalized * bulletSpeed;
                }

                AimAtPlayer();
            }

            yield return new WaitForSeconds(fireRate);
            elapsed += fireRate;
        }
    }

    // ── Laser — Tracking Beam ────────────────────────────────────────────────
    IEnumerator LaserAttack()
    {
        // Show charge-up effect
        if (chargeUpEffect != null) chargeUpEffect.SetActive(true);
        yield return new WaitForSeconds(0.5f);

        // Turn laser ON
        if (laserObject != null) laserObject.SetActive(true);

        // Spin speed: 0% slow = 360°/s (full speed), 100% slow = 0°/s
        float spinSpeed = (1f - laserRotateSlowPercent) * 360f;
        float elapsed = 0f;

        while (elapsed < attackDuration)
        {
            elapsed += Time.deltaTime;

            // Spin the rig (or the whole limb) continuously on Z axis
            if (laserRig != null)
            {
                laserRig.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            }
            else
            {
                transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            }

            yield return null;
        }

        // Turn laser and charge-up effect OFF
        if (laserObject != null) laserObject.SetActive(false);
        if (chargeUpEffect != null) chargeUpEffect.SetActive(false);
    }

    // ── Dash — Single Charge ─────────────────────────────────────────────────
    IEnumerator DashAttack()
    {
        if (player == null) yield break;

        isDashing = true;
        damagedThisDash = false;
       

        // Aim once, then charge in a straight line (1 dash only)
        Vector2 dir = ((Vector2)(player.position - transform.position)).normalized;
        float elapsed = 0f;

        while (elapsed < attackDuration)
        {
            transform.position += (Vector3)(dir * dashSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        
        isDashing = false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  COLLISION: Dash Damage
    // ═════════════════════════════════════════════════════════════════════════

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isDashing || damagedThisDash) return;

        if (other.CompareTag("Player"))
        {
            PlayerStats ps = other.GetComponent<PlayerStats>();
            if (ps != null)
            {
                ps.TakeDamage(dashDamage);
                damagedThisDash = true;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  DEBUG: Draw aim direction ray in Scene view
    // ═════════════════════════════════════════════════════════════════════════

    void OnDrawGizmos()
    {
        // Only draw when not in body
        if (currentState == State.In_The_Body) return;

        // Green ray = actual aim direction (with offset applied)
        Gizmos.color = Color.green;
        Vector3 aimDir = transform.rotation * Vector3.right;
        Gizmos.DrawRay(transform.position, aimDir * aimGizmoLength);

        // Red ray = raw direction to player (no offset)
        if (player != null)
        {
            Gizmos.color = Color.red;
            Vector3 toPlayer = (player.position - transform.position).normalized;
            Gizmos.DrawRay(transform.position, toPlayer * aimGizmoLength);
        }
    }
}
