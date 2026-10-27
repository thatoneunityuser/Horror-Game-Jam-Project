using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;


[RequireComponent(typeof(Collider2D))]
public class FRISTEVENTGHOST : MonoBehaviour
{
    public enum GhostState { IDLE, CHASE, JUMP, DEAD }

    [Header("State")]
    public GhostState currentState = GhostState.IDLE;
    public bool died = false;

    [Header("Speed Settings")]
    public float chaseSpeed = 10f;
    public float jumpSpeedMultiplier = 2.5f;
    [Tooltip("If ghost gets within this distance to player, contact is registered even if colliders miss")]
    public float hitDistance = 1.5f;
    private float currentSpeed;

    [Header("Room Triggers")]
    public int chaseStartRoom = 31;
    public int jumpRoom = 34;

    [Header("Death Movement & Gravity")]
    [Tooltip("Meters the ghost slides to the right when it misses/dies")]
    public float slideDistance = 5f;
    [Tooltip("How many seconds the horizontal slide takes")]
    public float slideDuration = 0.8f;
    [Tooltip("Height in meters the ghost rises on death before falling (default: 2m)")]
    public float riseHeight = 2f;
    [Tooltip("Duration in seconds for the initial rise (default: 0.5s)")]
    public float riseDuration = 0.5f;
    [Tooltip("Downward gravity acceleration when falling")]
    public float fallGravity = 15f;
    [Tooltip("Time to wait after sliding before destroying the ghost")]
    public float destroyDelayAfterSlide = 2f;

    [Header("Timeline / Death Effects")]
    public PlayableDirector TimelineWhenDead;
    [Tooltip("If true, hide sprite/mesh renderers at the end when ghost is destroyed")]
    public bool hideRenderersOnDeath = false;

    [Header("References")]
    public Transform playerTransform;
    public Hided hideStatus;
    public Collider2D ghostCollider;
    private Rigidbody2D rb;
    private Vector2 lastMoveDirection = Vector2.right;

    private bool hasTriggeredJump = false;

    void Awake()
    {
        // Ensure a Kinematic Rigidbody2D exists so 2D trigger events ALWAYS fire in Unity
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.simulated = true;
    }

    void Start()
    {
        FindPlayer();

        if (hideStatus == null)
        {
            hideStatus = FindObjectOfType<Hided>();
        }

        if (ghostCollider == null)
        {
            ghostCollider = GetComponent<Collider2D>();
        }
        if (ghostCollider != null)
        {
            ghostCollider.isTrigger = true;
        }

        currentSpeed = chaseSpeed;
    }

    void FindPlayer()
    {
        if (died || currentState == GhostState.DEAD) return;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
            else
            {
                SideScrollMovement movement = FindObjectOfType<SideScrollMovement>();
                if (movement != null) playerTransform = movement.transform;
            }
        }
    }

    void Update()
    {
        if (TimelineWhenDead == null)
        {
            TimelineWhenDead = GetComponent<PlayableDirector>();
            if (TimelineWhenDead == null)
            {
                TimelineWhenDead = FindObjectOfType<PlayableDirector>();
            }
        }

        // Completely stop all chase & trigger logic when dead
        if (died || currentState == GhostState.DEAD) return;

        if (playerTransform == null)
        {
            FindPlayer();
        }

        // 1. Room progression from RoomCounter
        if (RoomCounter.instance != null)
        {
            int room = RoomCounter.instance.PassedRoomCount;

            // Start chasing when room 31 is reached
            if (currentState == GhostState.IDLE && room >= chaseStartRoom)
            {
                currentState = GhostState.CHASE;
                Debug.Log("[FRISTEVENTGHOST] Started chasing at room " + room);
            }

            // Jump / Lunge when room 34 is reached
            if (!hasTriggeredJump && room >= jumpRoom)
            {
                TriggerJump();
            }
        }
        else
        {
            // If RoomCounter is not in scene (test scene), chase directly
            if (currentState == GhostState.IDLE)
            {
                currentState = GhostState.CHASE;
            }
        }

        // 2. Proximity check (failsafe in case Unity physics trigger does not fire)
        if (playerTransform != null && currentState != GhostState.IDLE)
        {
            float dist = Vector2.Distance(transform.position, playerTransform.position);
            if (dist <= hitDistance)
            {
                ResolveContact();
            }
        }
    }

    void FixedUpdate()
    {
        // Absolutely no chasing player when dead or idle
        if (died || currentState == GhostState.DEAD || currentState == GhostState.IDLE) return;

        // When player is dying: stop tracking player and keep moving forward
        if (isKillingPlayer)
        {
            Vector2 forwardDir = lastMoveDirection.sqrMagnitude > 0.001f ? lastMoveDirection : Vector2.right;
            transform.position += (Vector3)(forwardDir * currentSpeed * Time.fixedDeltaTime);
            return;
        }

        if (playerTransform == null) return;

        // Track last moving direction so slide knows which way ghost was lunging
        Vector2 diff = (Vector2)playerTransform.position - (Vector2)transform.position;
        if (diff.sqrMagnitude > 0.001f)
        {
            lastMoveDirection = diff.normalized;
        }

        // Move towards player
        transform.position = Vector2.MoveTowards(
            transform.position,
            playerTransform.position,
            currentSpeed * Time.fixedDeltaTime
        );
    }

    [ContextMenu("Trigger Jump")]
    public void TriggerJump()
    {
        if (died || currentState == GhostState.DEAD) return;
        hasTriggeredJump = true;
        currentState = GhostState.JUMP;
        currentSpeed = chaseSpeed * jumpSpeedMultiplier;
        Debug.Log("[FRISTEVENTGHOST] Jump triggered! Speed increased to " + currentSpeed);
    }

    // Trigger & Collision hooks for maximum compatibility
    void OnTriggerEnter2D(Collider2D collision) { CheckCollision(collision.gameObject); }
    void OnTriggerStay2D(Collider2D collision) { CheckCollision(collision.gameObject); }
    void OnCollisionEnter2D(Collision2D collision) { CheckCollision(collision.gameObject); }
    void OnCollisionStay2D(Collision2D collision) { CheckCollision(collision.gameObject); }

    void CheckCollision(GameObject hitObj)
    {
        if (died || currentState == GhostState.DEAD) return;

        // Check if hit object is the player, player's child, or a locker
        bool isPlayer = hitObj.CompareTag("Player") ||
                        (hitObj.transform.root != null && hitObj.transform.root.CompareTag("Player")) ||
                        hitObj.GetComponentInParent<SideScrollMovement>() != null;

        bool isLocker = hitObj.GetComponent<Locker>() != null || hitObj.GetComponentInParent<Locker>() != null;

        if (isPlayer || isLocker)
        {
            ResolveContact();
        }
    }

    void ResolveContact()
    {
        if (died || currentState == GhostState.DEAD) return;

        if (hideStatus == null)
        {
            hideStatus = FindObjectOfType<Hided>();
        }

        // Check if player is hiding via Hided script or inside a Locker
        bool isHiding = (hideStatus != null && hideStatus.IsPlayerHide);

        if (isHiding)
        {
            // Player successfully hid -> Ghost dies!
            Debug.Log("[FRISTEVENTGHOST] Player is hiding! Ghost missed and dies.");
            Die();
        }
        else
        {
            // Player caught while not hiding -> Play death routine and restart scene
            Debug.Log("[FRISTEVENTGHOST] Player caught while NOT hiding! Starting death routine...");
            StartCoroutine(PlayerDieCoroutine());
        }
    }

    private bool isKillingPlayer = false;

    IEnumerator PlayerDieCoroutine()
    {
        if (isKillingPlayer) yield break;
        isKillingPlayer = true;

        // Stop tracking player
        playerTransform = null;

        // If on jump state, back to chase state and restore chase speed
        currentState = GhostState.CHASE;
        currentSpeed = chaseSpeed;

        // Disable colliders so it passes smoothly without re-triggering
        if (ghostCollider != null) ghostCollider.enabled = false;
        Collider2D[] allCols = GetComponentsInChildren<Collider2D>();
        foreach (var c in allCols) if (c != null) c.enabled = false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Animator anim = player.GetComponent<Animator>();
            if (anim == null) anim = player.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                if (anim.HasState(0, Animator.StringToHash("Die"))) anim.Play("Die");
                else if (anim.HasState(0, Animator.StringToHash("Dead"))) anim.Play("Dead");
                else anim.Play("Die");
            }

            SideScrollMovement movement = player.GetComponent<SideScrollMovement>();
            if (movement != null) movement.enabled = false;

            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
            if (playerRb != null) playerRb.linearVelocity = Vector2.zero;
        }

        if (hideStatus != null) hideStatus.enabled = false;

        yield return new WaitForSeconds(2f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    [ContextMenu("Force Kill Ghost")]
    public void Die()
    {
        if (died) return;
        died = true;
        currentState = GhostState.DEAD;
        currentSpeed = 0f;
        playerTransform = null; // Sever player tracking immediately

        // Disable all ghost colliders immediately so it cannot touch, block, or stick to the player or locker
        if (ghostCollider != null)
        {
            ghostCollider.enabled = false;
        }
        Collider2D[] allCols = GetComponentsInChildren<Collider2D>();
        foreach (var col in allCols)
        {
            if (col != null) col.enabled = false;
        }

        // Keep Rigidbody as Kinematic so physics contacts never freeze or clamp position
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        StartCoroutine(DiesRoutine());
    }

    IEnumerator DiesRoutine()
    {
        currentSpeed = 0f;
        playerTransform = null;

        // Ensure colliders stay completely disabled
        if (ghostCollider != null) ghostCollider.enabled = false;
        Collider2D[] allCols = GetComponentsInChildren<Collider2D>();
        foreach (var col in allCols)
        {
            if (col != null) col.enabled = false;
        }

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        Debug.Log("[FRISTEVENTGHOST] Ghost died! Colliders disabled. Rising 2m, sliding right, then falling down.");

        // Play Timeline if assigned
        if (TimelineWhenDead != null)
        {
            TimelineWhenDead.Play();
        }

        // 1. Setup positions: slide RIGHT on X (+slideDistance), rise 2m on Y (+riseHeight)
        float startX = transform.position.x;
        float startY = transform.position.y;
        float targetX = startX + slideDistance; // Guaranteed move to the RIGHT!
        float peakY = startY + riseHeight;      // Rise up 2 meters!

        float elapsed = 0f;
        float currentY = startY;
        float fallSpeed = 0f;

        float totalMotionDuration = slideDuration + destroyDelayAfterSlide;
        if (TimelineWhenDead != null)
        {
            totalMotionDuration = Mathf.Max(totalMotionDuration, (float)TimelineWhenDead.duration);
        }

        while (elapsed < totalMotionDuration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            // --- Horizontal (X) Motion: Slide to the RIGHT ---
            float currentX;
            if (elapsed <= slideDuration)
            {
                float tX = Mathf.Clamp01(elapsed / slideDuration);
                float easeX = 1f - (1f - tX) * (1f - tX); // Ease-out quad
                currentX = Mathf.Lerp(startX, targetX, easeX);
            }
            else
            {
                currentX = targetX;
            }

            // --- Vertical (Y) Motion: Rise 2m in 0.5s, then keep falling down ---
            if (elapsed <= riseDuration)
            {
                // Rise up 2 meters in riseDuration (0.5s)
                float tY = Mathf.Clamp01(elapsed / riseDuration);
                float easeY = Mathf.Sin(tY * Mathf.PI * 0.5f); // Smooth curve to peak
                currentY = Mathf.Lerp(startY, peakY, easeY);
                fallSpeed = 0f;
            }
            else
            {
                // Keep falling down! Accelerate downward like gravity
                fallSpeed += fallGravity * dt;
                currentY -= fallSpeed * dt;
            }

            // Directly move transform and Rigidbody position
            transform.position = new Vector3(currentX, currentY, transform.position.z);
            if (rb != null)
            {
                rb.position = new Vector2(currentX, currentY);
            }

            yield return null;
        }

        // Hide renderers and destroy
        if (hideRenderersOnDeath)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                r.enabled = false;
            }
        }

        Destroy(gameObject);
    }
}


