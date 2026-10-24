using UnityEngine;

/// <summary>
/// Controls Catfish player movement: charged directional jumps (A/D) and straight-line click-to-dash towards mouse position.
/// Dash meter is earned by landing on the ground after executing an A/D jump (stronger impact/movement = more meter gained).
/// </summary>
public class CatFishMovement : MonoBehaviour
{
    [Header("Basic A/D Jump Movement")]
    [Tooltip("Minimum force applied on jump tap.")]
    public float minForce = 5f;

    [Tooltip("Maximum force applied at full jump charge.")]
    public float maxForce = 15f;

    [Tooltip("Maximum hold duration (in seconds) to reach full jump power.")]
    public float maxChargeTime = 1.2f;

    [Tooltip("Upward trajectory ratio (1 = 45-degree arc jump).")]
    [Range(0.1f, 3f)]
    public float upwardAngleRatio = 1f;

    [Header("Dash / Rush (Click & Charge towards Mouse)")]
    public float dashMeterMax = 100f;
    public float minDashPower = 8f;
    public float maxDashPower = 25f;
    public float maxDashChargeTime = 1f;
    [Tooltip("Dash meter cost required per dash.")]
    public float dashMeterCost = 20f;
    [Tooltip("Duration (in seconds) of the straight line dash.")]
    public float dashDuration = 0.22f;

    [Header("Ground Touch / Impact Meter Gain")]
    [Tooltip("Multiplier converting landing impact speed to Dash Meter.")]
    public float impactGainMultiplier = 4f;
    [Tooltip("Minimum impact speed needed to gain Dash Meter on landing.")]
    public float minImpactSpeed = 1.5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Visual Feedback (Juice)")]
    [Tooltip("Squash sprite downwards while charging jump or dash.")]
    public bool enableSquashEffect = true;
    public float maxSquashAmount = 0.35f;

    // References & Internal State
    private Rigidbody2D rb;
    private SpriteRenderer playerSprite;
    private Vector3 originalScale;
    private float originalGravityScale;

    private bool isGrounded;
    private Vector2 lastPhysicsVelocity;

    // Jump state
    private bool isChargingJump;
    private float jumpChargePower;
    private int facingDirection = 1; // 1 = Right, -1 = Left
    private bool pendingJump;
    private Vector2 pendingJumpDirection;
    private float pendingJumpForce;
    private bool hasADJumped;

    // Dash state
    private bool isChargingDash;
    private float dashChargeTimer;
    private float dashMeter;

    // Active Straight Dash state
    private bool isDashing;
    private float currentDashTimer;
    private Vector2 currentDashDirection;
    private float currentDashSpeed;

    // Public Getters for UI / External Scripts
    public float CurrentDashMeter => dashMeter;
    public bool IsGrounded => isGrounded;
    public bool IsChargingJump => isChargingJump;
    public bool IsChargingDash => isChargingDash;
    public bool IsDashing => isDashing;
    public float JumpChargeRatio => Mathf.Clamp01(jumpChargePower / Mathf.Max(0.01f, maxChargeTime));
    public float DashChargeRatio => Mathf.Clamp01(dashChargeTimer / Mathf.Max(0.01f, maxDashChargeTime));

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerSprite = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
        originalGravityScale = rb.gravityScale;

        dashMeter = dashMeterMax;

        if (groundCheck == null)
        {
            groundCheck = transform;
            Debug.LogWarning("[CatFishMovement] GroundCheck transform is unassigned. Using transform position instead.", this);
        }
    }

    void Update()
    {
        // 1. Check Ground Status & Handle Landing Meter Gain
        CheckGrounded();

        // 2. Handle Jump Input (A / D)
        HandleJumpInput();

        // 3. Handle Dash Input (Left Click towards Mouse)
        HandleDashInput();

        // 4. Update Visual Feedback (Squash & Stretch)
        UpdateVisuals();
    }

    void FixedUpdate()
    {
        // Record physics velocity before ground collision for impact meter calculation
        lastPhysicsVelocity = rb.linearVelocity;

        // Handle Active Straight Line Dash
        if (isDashing)
        {
            currentDashTimer -= Time.fixedDeltaTime;
            // Force 100% straight velocity ignoring gravity
            rb.linearVelocity = currentDashDirection * currentDashSpeed;

            if (currentDashTimer <= 0f)
            {
                EndDash();
            }
            return;
        }

        // Apply Pending Jump Physics Impulse
        if (pendingJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.2f, 0f);
            rb.AddForce(pendingJumpDirection * pendingJumpForce, ForceMode2D.Impulse);
            pendingJump = false;
        }
    }

    private void CheckGrounded()
    {
        if (groundCheck == null) return;

        bool wasGrounded = isGrounded;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // Detect landing transition from air to ground
        if (!wasGrounded && isGrounded)
        {
            OnLanded();
        }
    }

    private void OnLanded()
    {
        // Stop active dash if touching ground
        if (isDashing)
        {
            EndDash();
        }

        // Calculate impact speed based on how fast the catfish was moving right before landing
        float impactSpeed = lastPhysicsVelocity.magnitude;

        // Dash meter is only gained if landing after an A/D jump
        if (hasADJumped)
        {
            if (impactSpeed >= minImpactSpeed)
            {
                float gainedMeter = impactSpeed * impactGainMultiplier;
                dashMeter = Mathf.Min(dashMeterMax, dashMeter + gainedMeter);
                Debug.Log($"[CatFishMovement] Touched ground after A/D jump with impact speed {impactSpeed:F1}! Gained {gainedMeter:F1} Dash Meter ({dashMeter:F1}/{dashMeterMax})", this);
            }
            hasADJumped = false;
        }
        else
        {
            Debug.Log($"[CatFishMovement] Touched ground without prior A/D jump. Dash Meter not gained.", this);
        }
    }

    private void HandleJumpInput()
    {
        bool keyA = Input.GetKey(KeyCode.A);
        bool keyD = Input.GetKey(KeyCode.D);
        bool keyAUp = Input.GetKeyUp(KeyCode.A);
        bool keyDUp = Input.GetKeyUp(KeyCode.D);

        if (isGrounded)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                StartChargingJump(-1);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                StartChargingJump(1);
            }

            if (isChargingJump && (keyA || keyD))
            {
                jumpChargePower += Time.deltaTime;
                jumpChargePower = Mathf.Min(jumpChargePower, maxChargeTime);
            }

            if (isChargingJump && (keyAUp || keyDUp))
            {
                ExecuteJump();
            }
        }
        else
        {
            if (isChargingJump && !keyA && !keyD)
            {
                isChargingJump = false;
                jumpChargePower = 0f;
            }
        }
    }

    private void StartChargingJump(int dir)
    {
        if (isDashing) EndDash();

        isChargingJump = true;
        jumpChargePower = 0f;
        facingDirection = dir;

        if (playerSprite != null)
        {
            playerSprite.flipX = (dir == -1);
        }
    }

    private void ExecuteJump()
    {
        float ratio = JumpChargeRatio;
        float force = Mathf.Lerp(minForce, maxForce, ratio);

        Vector2 jumpVector = new Vector2(facingDirection, upwardAngleRatio).normalized;

        pendingJumpDirection = jumpVector;
        pendingJumpForce = force;
        pendingJump = true;
        hasADJumped = true;

        isChargingJump = false;
        jumpChargePower = 0f;
    }

    private void HandleDashInput()
    {
        // Start charging dash on Mouse 0 click (if enough dash meter)
        if (Input.GetMouseButtonDown(0))
        {
            if (dashMeter >= dashMeterCost)
            {
                isChargingDash = true;
                dashChargeTimer = 0f;
            }
            else
            {
                Debug.Log($"[CatFishMovement] Not enough Dash Meter! Need {dashMeterCost}, but have {dashMeter:F1}.", this);
            }
        }

        // Charge dash while holding mouse button
        if (Input.GetMouseButton(0) && isChargingDash)
        {
            dashChargeTimer += Time.deltaTime;
            dashChargeTimer = Mathf.Min(dashChargeTimer, maxDashChargeTime);

            // Orient sprite towards mouse target
            UpdateFacingFromMouse();
        }

        // Release mouse button to execute dash
        if (Input.GetMouseButtonUp(0) && isChargingDash)
        {
            ExecuteDash();
        }
    }

    private void ExecuteDash()
    {
        Camera mainCam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (mainCam != null)
        {
            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z = -mainCam.transform.position.z;
            Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(mouseScreenPos);
            Vector2 dashDirection = ((Vector2)mouseWorldPos - (Vector2)transform.position).normalized;

            float ratio = DashChargeRatio;
            float speed = Mathf.Lerp(minDashPower, maxDashPower, ratio);

            // Activate Straight Line Dash (Zero Gravity)
            isDashing = true;
            currentDashTimer = dashDuration;
            currentDashDirection = dashDirection;
            currentDashSpeed = speed;

            rb.gravityScale = 0f;
            rb.linearVelocity = dashDirection * speed;

            // Pass dash speed directly to attack system so damage scales dynamically
            if (CatFishAttackSystem.instance != null)
            {
                CatFishAttackSystem.instance.AttackWithForce(speed);
            }

            // Deduct dash meter cost
            dashMeter = Mathf.Max(0f, dashMeter - dashMeterCost);

            Debug.Log($"[CatFishMovement] Straight Dash executed towards {dashDirection} at speed {speed:F1}!", this);
        }

        isChargingDash = false;
        dashChargeTimer = 0f;
    }

    private void EndDash()
    {
        if (!isDashing) return;

        isDashing = false;
        rb.gravityScale = originalGravityScale;
        // Retain 25% coasting speed after dash ends for smooth movement feel
        rb.linearVelocity = currentDashDirection * (currentDashSpeed * 0.25f);
    }

    private void UpdateFacingFromMouse()
    {
        Camera mainCam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (mainCam != null && playerSprite != null)
        {
            Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
            if (mouseWorldPos.x < transform.position.x)
            {
                facingDirection = -1;
                playerSprite.flipX = true;
            }
            else
            {
                facingDirection = 1;
                playerSprite.flipX = false;
            }
        }
    }

    private void UpdateVisuals()
    {
        if (!enableSquashEffect) return;

        if ((isChargingJump && isGrounded) || isChargingDash)
        {
            float ratio = isChargingDash ? DashChargeRatio : JumpChargeRatio;
            float scaleY = Mathf.Lerp(1f, 1f - maxSquashAmount, ratio);
            float scaleX = Mathf.Lerp(1f, 1f + (maxSquashAmount * 0.5f), ratio);
            transform.localScale = new Vector3(originalScale.x * scaleX, originalScale.y * scaleY, originalScale.z);
        }
        else
        {
            transform.localScale = Vector3.Lerp(transform.localScale, originalScale, Time.deltaTime * 15f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
