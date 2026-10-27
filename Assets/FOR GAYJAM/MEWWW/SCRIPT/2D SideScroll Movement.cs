using UnityEngine;
using System.Collections;

public class SideScrollMovement : MonoBehaviour
{
    [Header("Movement")]
    public float maxSpeed = 5.0f;           // top horizontal speed
    public float acceleration = 40.0f;      // how fast speed builds up (units/s²)
    public float deceleration = 60.0f; 
    [HideInInspector] public bool isMoving;
    public Animator animator;

    [Header("Audio")]
    public AudioClip Walk;
    public AudioClip Sprint;
    [Range(0f, 1f)] public float walkVolume = 0.8f;
    [Range(0f, 1f)] public float sprintVolume = 1.0f;
    [Tooltip("Optional: dedicated AudioSource for footsteps. If unassigned, one will be found or added automatically.")]
    public AudioSource audioSource;

    [Header("Sprint")]
    public float sprintMultiplier = 2.0f;   // max speed multiplier while sprinting
    public float maxStamina = 100.0f;
    public float stamina = 100.0f;
    public float staminaDrainRate = 10.0f;
    public float staminaRegenRate = 5.0f;
    [Tooltip("If stamina hits 0, you cannot sprint until stamina recovers back to this threshold")]
    public float minStaminaToSprintAfterExhaustion = 20.0f;
    [HideInInspector] public bool isExhausted = false;

    [Header("Crawl")]
    public float crawlSpeed = 3f;
    [HideInInspector] public bool isCrawling = false;

    [Header("Walk To Stair")]
    public bool canWalkUpStair = true;
    public Collider2D PlayerCollider;
    [Tooltip("Maximum height of a step the player can automatically climb (default 0.85m)")]
    public float limitHeight = 0.85f;
    [Tooltip("Base lift speed modifier for climbing steps")]
    public float speedWalkToStair = 6.0f;
    [Tooltip("Forward push multiplier onto the step")]
    public float autoPushMultiplier = 1.0f;
    [Tooltip("Enable smooth descent when walking down stairs")]
    public bool smoothStepDown = true;
    [Tooltip("Optional: Layer mask for obstacles/stairs (defaults to solid non-trigger geometry)")]
    public LayerMask stairLayerMask;

    [Header("Fly")]
    public bool flyable = true;
    public float flyforce = 15.0f;
    public float dropForce = 15.0f;
    public float maxFlyMeter = 100.0f;
    public float flyMeter = 100.0f;
    public float flyMeterDrainRate = 10.0f;
    public float flyMeterRegenRate = 5.0f;

    [Header("Dash")]
    public bool dashable = true;
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 2f;
    public float dashStaminaCost = 20f;
    public AudioClip dashSound;

    // Public so PlayerStats can read it for damage immunity
    [HideInInspector] public bool isDashing = false;

    private float dashCooldownTimer = 0f;
    private float currentSpeedX = 0f;       // tracked horizontal speed (Terraria-style)
    private Rigidbody2D rb;
    private float originalGravity = 1f;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];
    private Coroutine dashCoroutine;

    // Input cached in Update, consumed in FixedUpdate
    private float moveInput = 0f;
    private bool isFlyingInput = false;

    [Header("Anti-Snag")]
    [Tooltip("Auto-recover when the collider catches on seams between ground colliders (tilemap / stacked boxes)")]
    public bool antiSnag = true;
    [Tooltip("Tiny upward nudge applied when snagged on a seam")]
    public float snagNudge = 0.03f;

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f; // 2D playback so distance to camera doesn't attenuate footstep audio

#if UNITY_EDITOR
        if (Walk == null)
        {
            Walk = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FOR GAYJAM/MEWWW/AUDIOOOO/walk.wav");
        }
        if (Sprint == null)
        {
            Sprint = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FOR GAYJAM/MEWWW/AUDIOOOO/run.wav");
        }
#endif

        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            originalGravity = rb.gravityScale;
        }

        if (PlayerCollider == null)
        {
            PlayerCollider = GetComponent<Collider2D>();
        }

        // Zero friction so the player never sticks to ground/walls (only if no material assigned)
        if (PlayerCollider != null && PlayerCollider.sharedMaterial == null)
        {
            PhysicsMaterial2D noFriction = new PhysicsMaterial2D("PlayerNoFriction");
            noFriction.friction = 0f;
            noFriction.bounciness = 0f;
            PlayerCollider.sharedMaterial = noFriction;
        }
    }

    void OnDisable()
    {
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }
        if (rb != null && isDashing)
        {
            rb.gravityScale = originalGravity;
        }
        isDashing = false;
        isMoving = false;
        currentSpeedX = 0f;
        StopMovementAudio();
    }

    void Update()
    {
        // Tick dash cooldown
        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        // Skip normal movement input while dashing
        if (isDashing)
        {
            isMoving = true;
            StopMovementAudio();
            return;
        }

        // ── Input ─────────────────────────────────────────────────────────────
        moveInput = Input.GetAxisRaw("Horizontal"); // -1, 0, or 1

        // ── Sprint & Stamina ──────────────────────────────────────────────────
        // If stamina reaches 0, player becomes exhausted and cannot sprint until stamina >= 20
        if (stamina <= 0f)
        {
            isExhausted = true;
        }
        else if (isExhausted && stamina >= minStaminaToSprintAfterExhaustion)
        {
            isExhausted = false;
        }

        bool wantsToCrawl = Input.GetKey(KeyCode.LeftControl);
        isCrawling = wantsToCrawl && moveInput != 0f;

        bool wantsToSprint = Input.GetKey(KeyCode.LeftShift) && moveInput != 0f && !wantsToCrawl;
        bool isSprinting = wantsToSprint && !isExhausted && stamina > 0f;
        float cappedSpeed = maxSpeed;

        if (isCrawling)
        {
            cappedSpeed = crawlSpeed;
            if (!isDashing)
            {
                stamina += staminaRegenRate * Time.deltaTime;
            }
        }
        else if (isSprinting)
        {
            cappedSpeed = maxSpeed * sprintMultiplier;
            stamina -= staminaDrainRate * Time.deltaTime;
            // Regen is completely disabled while stamina is actively in use
        }
        else if (!isDashing)
        {
            // Only regenerate stamina when stamina is not in use (not sprinting, not dashing)
            stamina += staminaRegenRate * Time.deltaTime;
        }
        stamina = Mathf.Clamp(stamina, 0, maxStamina);

        // ── Terraria-style horizontal movement ────────────────────────────────
        if (moveInput != 0f)
        {
            if(animator != null)
            {
                animator.SetBool("isMoving", true);
            }
            float targetX = moveInput * cappedSpeed;
            currentSpeedX = Mathf.MoveTowards(currentSpeedX, targetX, acceleration * Time.deltaTime);
        }
        else
        {
            if(animator != null)
            {
                animator.SetBool("isMoving", false);
            }
            currentSpeedX = Mathf.MoveTowards(currentSpeedX, 0f, deceleration * Time.deltaTime);
        }

        isMoving = (moveInput != 0f) || Mathf.Abs(currentSpeedX) > 0.05f || Mathf.Abs(rb.linearVelocity.y) > 0.1f;

        // ── Flip sprite ───────────────────────────────────────────────────────
        if (moveInput > 0)
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else if (moveInput < 0)
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        // ── Fly / Drop ────────────────────────────────────────────────────────
        isFlyingInput = false;
        if (flyable)
        {
            bool isFlying = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Space)) && flyMeter > 0;
            isFlyingInput = isFlying;
            if (isFlying)
            {
                flyMeter -= flyMeterDrainRate * Time.deltaTime;
            }
            else
            {
                flyMeter += flyMeterRegenRate * Time.deltaTime;
            }
            flyMeter = Mathf.Clamp(flyMeter, 0, maxFlyMeter);

            if (Input.GetKeyDown(KeyCode.S))
            {
                rb.AddForce(Vector2.down * dropForce, ForceMode2D.Impulse);
            }
        }

        // ── Dash ──────────────────────────────────────────────────────────────
        if (dashable)
        {
            if (Input.GetKeyDown(KeyCode.F) && stamina >= dashStaminaCost && dashCooldownTimer <= 0f)
            {
                dashCoroutine = StartCoroutine(DashRoutine(moveInput));
            }
        }

        // ── Footstep Audio ────────────────────────────────────────────────────
        HandleMovementAudio(isSprinting, isCrawling);
    }

    void FixedUpdate()
    {
        if (rb == null || isDashing) return;

        // ── Anti-snag: physics stopped us but there's no real wall → seam ghost collision ──
        if (antiSnag && moveInput != 0f && Mathf.Abs(currentSpeedX) > 0.5f
            && Mathf.Abs(rb.linearVelocity.x) < Mathf.Abs(currentSpeedX) * 0.2f)
        {
            TryUnsnag(Mathf.Sign(currentSpeedX));
        }

        rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);

        // ── Smooth Stairs Navigation (Velocity-Proportional Up & Down) ────────
        if (canWalkUpStair)
        {
            HandleStairs(moveInput);
        }

        // ── Fly force (physics, so applied at fixed rate) ─────────────────────
        if (isFlyingInput)
        {
            rb.AddForce(Vector2.up * flyforce, ForceMode2D.Force);
        }
    }

    void TryUnsnag(float dir)
    {
        if (PlayerCollider == null) return;

        Bounds b = PlayerCollider.bounds;
        int mask = stairLayerMask.value != 0 ? stairLayerMask.value : ~LayerMask.GetMask("Ignore Raycast");

        // Check for a real wall at mid-body height. If there is one, the stop is legit.
        Vector2 midOrigin = new Vector2(b.center.x + dir * (b.extents.x + 0.02f), b.center.y);
        RaycastHit2D wall = Physics2D.Raycast(midOrigin, Vector2.right * dir, 0.1f, mask);
        if (wall.collider != null && !wall.collider.isTrigger && wall.collider != PlayerCollider)
            return;

        // No real wall → caught on a seam. Pop up slightly and keep going.
        rb.position += new Vector2(dir * 0.01f, snagNudge);
    }

    void HandleStairs(float moveInput)
    {
        if (PlayerCollider == null || moveInput == 0f || isDashing) return;

        float dir = Mathf.Sign(moveInput);
        Bounds b = PlayerCollider.bounds;
        float feetY = b.min.y;
        float speedX = Mathf.Abs(currentSpeedX);
        if (speedX < 0.1f) return;

        int mask = stairLayerMask.value != 0 ? stairLayerMask.value : ~LayerMask.GetMask("Ignore Raycast");

        // ══════════════════════════════════════════════════════════════════════
        // 1. STEP UP: Natural velocity-scaled climb onto next step
        // ══════════════════════════════════════════════════════════════════════
        float forwardCheckDist = 0.35f;
        Vector2 lowerOrigin = new Vector2(b.center.x + dir * (b.extents.x + 0.02f), feetY + 0.08f);
        RaycastHit2D hitLower = Physics2D.Raycast(lowerOrigin, Vector2.right * dir, forwardCheckDist, mask);

        if (hitLower.collider != null && !hitLower.collider.isTrigger && hitLower.collider != PlayerCollider && !hitLower.collider.CompareTag("Enemy"))
        {
            // Verify there is clearance above at limitHeight
            Vector2 upperOrigin = new Vector2(b.center.x + dir * (b.extents.x + 0.02f), feetY + limitHeight);
            RaycastHit2D hitUpper = Physics2D.Raycast(upperOrigin, Vector2.right * dir, forwardCheckDist, mask);

            if (hitUpper.collider == null || hitUpper.collider.isTrigger)
            {
                // Find top surface of this step
                Vector2 surfaceRayOrigin = new Vector2(hitLower.point.x + dir * 0.12f, feetY + limitHeight + 0.1f);
                RaycastHit2D surfaceHit = Physics2D.Raycast(surfaceRayOrigin, Vector2.down, limitHeight + 0.2f, mask);

                if (surfaceHit.collider != null && !surfaceHit.collider.isTrigger)
                {
                    float stepHeight = surfaceHit.point.y - feetY;
                    if (stepHeight > 0.02f && stepHeight <= limitHeight)
                    {
                        // Check head clearance
                        float playerHeight = b.size.y;
                        RaycastHit2D ceilingHit = Physics2D.Raycast(new Vector2(b.center.x + dir * 0.2f, surfaceHit.point.y), Vector2.up, playerHeight, mask);

                        if (ceilingHit.collider == null || ceilingHit.collider.isTrigger || ceilingHit.collider == PlayerCollider)
                        {
                            // Lift speed scales directly with current horizontal speed:
                            // Faster sprint = faster climb; slower walk = smooth natural climb
                            float liftSpeed = Mathf.Max(speedX * (stepHeight / 0.35f), speedWalkToStair);
                            float targetY = surfaceHit.point.y + b.extents.y + 0.01f;

                            float newY = Mathf.MoveTowards(rb.position.y, targetY, liftSpeed * Time.deltaTime);

                            // Push forward proportionally so horizontal velocity doesn't halt at the step edge
                            float forwardPush = dir * speedX * autoPushMultiplier * Time.deltaTime;
                            rb.position = new Vector2(rb.position.x + forwardPush, newY);

                            // Keep clean horizontal velocity and prevent vertical momentum from launching off the top
                            rb.linearVelocity = new Vector2(currentSpeedX, 0f);
                            return;
                        }
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. STEP DOWN: Grounded descent so player doesn't float into the air
        // ══════════════════════════════════════════════════════════════════════
        if (smoothStepDown && rb.linearVelocity.y <= 0.1f)
        {
            Vector2 stepDownOrigin = new Vector2(b.center.x + dir * (b.extents.x + 0.1f), feetY + 0.1f);
            RaycastHit2D downHit = Physics2D.Raycast(stepDownOrigin, Vector2.down, limitHeight + 0.2f, mask);

            if (downHit.collider != null && !downHit.collider.isTrigger && downHit.collider != PlayerCollider && !downHit.collider.CompareTag("Enemy"))
            {
                float dropDist = feetY - downHit.point.y;
                if (dropDist > 0.05f && dropDist <= limitHeight)
                {
                    float targetY = downHit.point.y + b.extents.y;
                    float snapSpeed = Mathf.Max(speedX * 2.5f, 10f);
                    float newY = Mathf.MoveTowards(rb.position.y, targetY, snapSpeed * Time.deltaTime);
                    rb.position = new Vector2(rb.position.x, newY);
                    rb.linearVelocity = new Vector2(currentSpeedX, 0f);
                }
            }
        }
    }

    IEnumerator DashRoutine(float inputDir)
    {
        isDashing = true;
        stamina -= dashStaminaCost;
        StopMovementAudio();

        if (audioSource != null && dashSound != null)
        {
            audioSource.PlayOneShot(dashSound);
        }

        // Determine dash direction: prefer active input direction, fallback to sprite facing
        float facingDir = (inputDir != 0f) ? Mathf.Sign(inputDir) : Mathf.Sign(transform.localScale.x);

        // Turn character to dash direction if input was given
        if (inputDir != 0f)
        {
            transform.localScale = new Vector3(facingDir * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }

        // Zero out vertical velocity and freeze gravity so the dash travels straight horizontally
        if (rb != null)
        {
            originalGravity = rb.gravityScale;
            rb.gravityScale = 0f;
        }

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            currentSpeedX = facingDir * dashSpeed;
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(currentSpeedX, 0f);
            }
            yield return null;
        }

        // Restore gravity and start cooldown
        if (rb != null)
        {
            rb.gravityScale = originalGravity;
            rb.linearVelocity = new Vector2(0f, 0f);
        }
        currentSpeedX = 0f;
        isDashing = false;
        dashCooldownTimer = dashCooldown;
        dashCoroutine = null;
    }

    void OnDrawGizmosSelected()
    {
        if (PlayerCollider == null)
            PlayerCollider = GetComponent<Collider2D>();
        if (PlayerCollider == null) return;

        Bounds b = PlayerCollider.bounds;
        float feetY = b.min.y;
        float dir = Mathf.Sign(transform.localScale.x);

        // Lower ray (green)
        Gizmos.color = Color.green;
        Vector2 lowerOrigin = new Vector2(b.center.x + dir * (b.extents.x + 0.02f), feetY + 0.08f);
        Gizmos.DrawLine(lowerOrigin, lowerOrigin + Vector2.right * dir * 0.35f);

        // Upper ray (cyan)
        Gizmos.color = Color.cyan;
        Vector2 upperOrigin = new Vector2(b.center.x + dir * (b.extents.x + 0.02f), feetY + limitHeight);
        Gizmos.DrawLine(upperOrigin, upperOrigin + Vector2.right * dir * 0.35f);

        // Step down ray (orange)
        if (smoothStepDown)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Vector2 stepDownOrigin = new Vector2(b.center.x + dir * (b.extents.x + 0.1f), feetY + 0.1f);
            Gizmos.DrawLine(stepDownOrigin, stepDownOrigin + Vector2.down * (limitHeight + 0.2f));
        }

        // Ground check box (yellow)
        Gizmos.color = Color.yellow;
        Vector2 groundBoxOrigin = new Vector2(b.center.x, feetY + 0.05f);
        Vector2 groundBoxSize = new Vector2(Mathf.Max(0.1f, b.size.x * 0.7f), 0.05f);
        Gizmos.DrawWireCube(groundBoxOrigin + Vector2.down * 0.1f, groundBoxSize);
    }

    private void HandleMovementAudio(bool isSprinting, bool isCrawling)
    {
        if (audioSource == null) return;

        // Crawling is completely silent (no footstep sounds)
        if (isCrawling)
        {
            StopMovementAudio();
            return;
        }

        bool hasInput = moveInput != 0f;
        bool isMovingHorizontally = hasInput && (Mathf.Abs(currentSpeedX) > 0.1f || (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f));
        bool isMovingOnFoot = isMovingHorizontally && IsGrounded() && !isDashing && !isFlyingInput;

        if (isMovingOnFoot)
        {
            AudioClip targetClip = isSprinting ? (Sprint != null ? Sprint : Walk) : Walk;
            float targetPitch = (isSprinting && Sprint == null) ? 1.25f : 1.0f;
            float targetVolume = isSprinting ? sprintVolume : walkVolume;

            if (targetClip != null)
            {
                audioSource.volume = targetVolume;
                audioSource.pitch = targetPitch;

                if (audioSource.clip != targetClip || !audioSource.isPlaying)
                {
                    audioSource.clip = targetClip;
                    audioSource.loop = true;
                    audioSource.Play();
                }
            }
            else
            {
                StopMovementAudio();
            }
        }
        else
        {
            StopMovementAudio();
        }
    }

    private void StopMovementAudio()
    {
        if (audioSource != null && audioSource.isPlaying && (audioSource.clip == Walk || audioSource.clip == Sprint))
        {
            audioSource.Stop();
        }
    }

    public bool IsGrounded()
    {
        // 1. BoxCast check: check all solid layers (including Default layer 0 and stairs layer 6)
        int mask = ~LayerMask.GetMask("Ignore Raycast", "UI");

        if (PlayerCollider != null)
        {
            Bounds b = PlayerCollider.bounds;
            Vector2 origin = new Vector2(b.center.x, b.min.y + 0.1f);
            Vector2 size = new Vector2(Mathf.Max(0.1f, b.size.x * 0.7f), 0.1f);
            int hitCount = Physics2D.BoxCastNonAlloc(origin, size, 0f, Vector2.down, groundHits, 0.3f, mask);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D col = groundHits[i].collider;
                if (col != null && !col.isTrigger && col.transform != transform && !col.CompareTag("Enemy"))
                {
                    return true;
                }
            }
        }

        // 2. Physics check: if vertical velocity is near zero and player is not pressing fly, they are grounded
        if (rb != null && Mathf.Abs(rb.linearVelocity.y) < 0.25f && !isFlyingInput)
        {
            return true;
        }

        return false;
    }
}
