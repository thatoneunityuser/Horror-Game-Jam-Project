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
    [Tooltip("Condition 2: Timeline prefab instantiated when this ghost dies and DiesRoutine() ends")]
    public GameObject timelinePrefabWhenDeadEnd;
    [Tooltip("Optional spawn point override for timelinePrefabWhenDeadEnd (uses prefab's own position/rotation if null)")]
    public Transform timelinePrefabSpawnPoint;
    [Tooltip("If true, hide sprite/mesh renderers at the end when ghost is destroyed")]
    public bool hideRenderersOnDeath = false;

    [Header("References")]
    public Transform playerTransform;
    public Hided hideStatus;
    public Collider2D ghostCollider;
    private Rigidbody2D rb;
    private Vector2 lastMoveDirection = Vector2.right;

    [Header("ChaseTheme")]
    public AudioSource chasethemeonly;

    [Header("Jump Settings")]
    [Tooltip("Seconds ghost pauses still before launching the high-speed jump")]
    public float jumpPauseDuration = 0.5f;
    private bool isPreparingJump = false;
    private float jumpStartY;
    private float jumpTargetY;

    private bool hasTriggeredJump = false;
    private float lastTimelineSearchTime = -10f;

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
        FindTimelineDirector(true);

        // Attach collision forwarders to all child colliders so events on child objects (e.g. Square) reach this script
        Collider2D[] allChildCols = GetComponentsInChildren<Collider2D>(true);
        foreach (var col in allChildCols)
        {
            if (col != null)
            {
                col.isTrigger = true;
                GhostCollisionForwarder2D forwarder = col.GetComponent<GhostCollisionForwarder2D>();
                if (forwarder == null)
                {
                    forwarder = col.gameObject.AddComponent<GhostCollisionForwarder2D>();
                }
                forwarder.owner = this;
            }
        }
    }

    void FindPlayer()
    {
        if (died || currentState == GhostState.DEAD || isKillingPlayer) return;

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

    public void FindTimelineDirector(bool force = false)
    {
        if (TimelineWhenDead != null) return;

        // Rate-limit scene lookups to at most once every 0.5s unless forced
        if (!force && Time.time - lastTimelineSearchTime < 0.5f) return;
        lastTimelineSearchTime = Time.time;

      
        // 2. Safely find GameObject tagged "CORRIDOR END" (avoids crash if tag is not defined in Tag Manager)
        GameObject corridorObj = null;
        try
        {
            corridorObj = GameObject.FindGameObjectWithTag("CORRIDOR END");
        }
        catch (System.Exception)
        {
            // Tag might not be defined in Tag Manager yet
        }

        // 3. Fallback: Search by GameObject name in case it was named "CORRIDOR END" instead of tagged
        if (corridorObj == null)
        {
            corridorObj = GameObject.Find("CORRIDOR END");
        }

        if (corridorObj != null)
        {
            TimelineWhenDead = corridorObj.GetComponent<PlayableDirector>();
            if (TimelineWhenDead == null)
            {
                TimelineWhenDead = corridorObj.GetComponentInChildren<PlayableDirector>();
            }
        }

      
    }

    void Update()
    {
        if (TimelineWhenDead == null)
        {
            FindTimelineDirector();
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
                if (chasethemeonly != null && !chasethemeonly.isPlaying)
                {
                    chasethemeonly.Play();
                }
                Debug.Log("[FRISTEVENTGHOST] Started chasing at room " + room);
            }

            // Jump / Lunge when room jumpRoom
            if (!hasTriggeredJump && room == jumpRoom)
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
                if (chasethemeonly != null && !chasethemeonly.isPlaying)
                {
                    chasethemeonly.Play();
                }
            }
        }

        // 2. Proximity and overlap check (ensures reliable contact in both Chase and Jump states)
        CheckProximityAndOverlap();
    }

    void FixedUpdate()
    {
        // Absolutely no chasing player when dead, idle, or preparing jump
        if (died || currentState == GhostState.DEAD || currentState == GhostState.IDLE || isPreparingJump) return;

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

        if (currentState == GhostState.JUMP)
        {
            // Move horizontally towards player at multiplied jump speed
            float newX = Mathf.MoveTowards(transform.position.x, playerTransform.position.x, currentSpeed * Time.fixedDeltaTime);

            // Getting higher with delta time up to riseHeight (high as rise height when it dies)
            float riseSpeed = (riseDuration > 0f) ? (riseHeight / riseDuration) : 4f;
            float newY = Mathf.MoveTowards(transform.position.y, jumpTargetY, riseSpeed * Time.fixedDeltaTime);

            transform.position = new Vector3(newX, newY, transform.position.z);
            if (rb != null)
            {
                rb.position = new Vector2(newX, newY);
            }
        }
        else
        {
            // Move towards player normally
            transform.position = Vector2.MoveTowards(
                transform.position,
                playerTransform.position,
                currentSpeed * Time.fixedDeltaTime
            );
            if (rb != null)
            {
                rb.position = transform.position;
            }
        }

        CheckProximityAndOverlap();
    }

    public void CheckProximityAndOverlap()
    {
        if (died || currentState == GhostState.DEAD || currentState == GhostState.IDLE || isPreparingJump || isKillingPlayer) return;

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        // 1. Direct Collider Bounds Intersection
        if (ghostCollider == null)
        {
            ghostCollider = GetComponent<Collider2D>();
            if (ghostCollider == null) ghostCollider = GetComponentInChildren<Collider2D>();
        }

        if (ghostCollider != null)
        {
            Collider2D playerCol = playerTransform.GetComponent<Collider2D>();
            if (playerCol == null) playerCol = playerTransform.GetComponentInChildren<Collider2D>();

            if (playerCol != null && ghostCollider.bounds.Intersects(playerCol.bounds))
            {
                ResolveContact();
                return;
            }
        }

        // 2. Bounding distance check (horizontal reach & vertical reach)
        // FAT RAT's collider is ~10m wide, so 4.5m represents its front body reach
        float dx = Mathf.Abs(transform.position.x - playerTransform.position.x);
        float dy = Mathf.Abs(transform.position.y - playerTransform.position.y);

        float reachX = Mathf.Max(hitDistance, 4.5f);
        float reachY = (currentState == GhostState.JUMP) ? (riseHeight + 4.0f) : 4.0f;

        if (dx <= reachX && dy <= reachY)
        {
            ResolveContact();
        }
    }

    [ContextMenu("Trigger Jump")]
    public void TriggerJump()
    {
        if (died || currentState == GhostState.DEAD || hasTriggeredJump) return;
        hasTriggeredJump = true;
        StartCoroutine(JumpRoutine());
    }

    private IEnumerator JumpRoutine()
    {
        isPreparingJump = true;
        currentSpeed = 0f; // Wait still for 0.5s!
        Debug.Log("[FRISTEVENTGHOST] Preparing jump: waiting still for " + jumpPauseDuration + "s...");

        yield return new WaitForSeconds(jumpPauseDuration);

        if (died || currentState == GhostState.DEAD) yield break;

        isPreparingJump = false;
        currentState = GhostState.JUMP;
        currentSpeed = chaseSpeed * jumpSpeedMultiplier; // Multiply speed!

        jumpStartY = transform.position.y;
        jumpTargetY = jumpStartY + riseHeight; // Getting higher as high as rise height when it dies!

        if (chasethemeonly != null && !chasethemeonly.isPlaying)
        {
            chasethemeonly.Play();
        }

        Debug.Log("[FRISTEVENTGHOST] Jump launched! Speed multiplied to " + currentSpeed + ", rising Y towards " + jumpTargetY);
    }

    // Trigger & Collision hooks for maximum compatibility
    void OnTriggerEnter2D(Collider2D collision) { CheckCollision(collision.gameObject); }
    void OnTriggerStay2D(Collider2D collision) { CheckCollision(collision.gameObject); }
    void OnCollisionEnter2D(Collision2D collision) { CheckCollision(collision.gameObject); }
    void OnCollisionStay2D(Collision2D collision) { CheckCollision(collision.gameObject); }

    public void CheckCollision(GameObject hitObj)
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

        // Direct zero-delay check: player is hiding via Hided script or inside a Locker
        bool isHiding = (hideStatus != null && hideStatus.IsPlayerHide);

        if (!isHiding)
        {
            Locker[] allLockers = FindObjectsOfType<Locker>();
            if (allLockers != null)
            {
                for (int i = 0; i < allLockers.Length; i++)
                {
                    if (allLockers[i] != null && allLockers[i].IsPlayerInsideThisLocker)
                    {
                        isHiding = true;
                        break;
                    }
                }
            }
        }

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

        if (chasethemeonly != null && chasethemeonly.isPlaying)
        {
            chasethemeonly.Stop();
        }

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
            if (movement != null)
            {
                movement.KillPlayer(2f);
            }

            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
            if (playerRb != null) playerRb.linearVelocity = Vector2.zero;
        }

        if (hideStatus != null) hideStatus.enabled = false;

        yield return new WaitForSecondsRealtime(2f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    [ContextMenu("Force Kill Ghost")]
    public void Die()
    {
        if (died) return;
        died = true;

        if (chasethemeonly != null && chasethemeonly.isPlaying)
        {
            chasethemeonly.Stop();
        }

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
        float Volume =chasethemeonly.volume;
        while(Volume > 0)
        {
            Volume -= Time.deltaTime * 1f;
            chasethemeonly.volume = Volume;
            yield return null;
        }

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
        if (TimelineWhenDead == null)
        {
            FindTimelineDirector(true);
        }

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

        // Condition 2: First event ghost died and die enumerator ended -> instantiate Timeline prefab!
        RoomCounter.TriggerFirstEventGhostDeadTimeline(timelinePrefabWhenDeadEnd, timelinePrefabSpawnPoint);

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

public class GhostCollisionForwarder2D : MonoBehaviour
{
    public FRISTEVENTGHOST owner;
    void OnTriggerEnter2D(Collider2D c) { if (owner != null && c != null) owner.CheckCollision(c.gameObject); }
    void OnTriggerStay2D(Collider2D c) { if (owner != null && c != null) owner.CheckCollision(c.gameObject); }
    void OnCollisionEnter2D(Collision2D c) { if (owner != null && c != null && c.gameObject != null) owner.CheckCollision(c.gameObject); }
    void OnCollisionStay2D(Collision2D c) { if (owner != null && c != null && c.gameObject != null) owner.CheckCollision(c.gameObject); }
}
