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

    [Header("Death Slide & Gravity")]
    [Tooltip("Meters the ghost slides away when it misses/dies")]
    public float slideDistance = 5f;
    [Tooltip("How many seconds the slide takes")]
    public float slideDuration = 0.8f;
    [Tooltip("Gravity scale when sliding/dying")]
    public float deathGravityScale = 1f;
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
            // Player caught while not hiding -> Restart scene
            Debug.Log("[FRISTEVENTGHOST] Player caught while NOT hiding! Resetting scene...");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    [ContextMenu("Force Kill Ghost")]
    public void Die()
    {
        if (died) return;
        died = true;
        currentState = GhostState.DEAD;
        currentSpeed = 0f;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
        StartCoroutine(DiesRoutine());
    }

    IEnumerator DiesRoutine()
    {
        currentSpeed = 0f;

        // 1. Determine slide direction (continue moving past player or along last movement direction)
        float dirX = (lastMoveDirection.x >= 0f) ? 1f : -1f;

        // 2. Ignore collision with player so the sliding ghost doesn't hurt or push player
        Collider2D playerCol = null;
        if (playerTransform != null)
        {
            playerCol = playerTransform.GetComponent<Collider2D>();
            if (playerCol == null)
            {
                playerCol = playerTransform.GetComponentInChildren<Collider2D>();
            }
        }

        if (ghostCollider != null)
        {
            if (playerCol != null)
            {
                Physics2D.IgnoreCollision(ghostCollider, playerCol, true);
            }
            // Make collider solid so it lands on floors and platforms instead of falling into the void
            ghostCollider.isTrigger = false;
        }

        // 3. Enable physics gravity and dynamic body
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = deathGravityScale;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        // 4. Play Timeline if assigned
        if (TimelineWhenDead != null)
        {
            TimelineWhenDead.Play();
        }

        // 5. Slide horizontally over slideDuration using MovePosition for guaranteed 5 meter slide
        float startX = transform.position.x;
        float targetX = startX + (dirX * slideDistance);
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            // Ease-out quad deceleration
            float easeT = 1f - (1f - t) * (1f - t);
            float currentX = Mathf.Lerp(startX, targetX, easeT);

            if (rb != null)
            {
                rb.MovePosition(new Vector2(currentX, rb.position.y));
            }
            else
            {
                transform.position = new Vector3(currentX, transform.position.y, transform.position.z);
            }

            yield return new WaitForFixedUpdate();
        }

        // Ensure ghost stops completely on X after slide
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        // 6. Wait before destroying
        float waitRemaining = destroyDelayAfterSlide;
        if (TimelineWhenDead != null)
        {
            waitRemaining = Mathf.Max(waitRemaining, (float)TimelineWhenDead.duration - slideDuration);
        }

        if (waitRemaining > 0f)
        {
            yield return new WaitForSeconds(waitRemaining);
        }

        // 7. Hide renderers and destroy
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


