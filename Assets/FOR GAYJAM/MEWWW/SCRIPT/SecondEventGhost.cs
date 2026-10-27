using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SecondEventGhost : MonoBehaviour
{
    public enum GHOST_STATE
    {
        IDLE,
        PATROL,
        SNIFF,
        CHASE
    }

    [Header("Current State")]
    public GHOST_STATE ghost_State = GHOST_STATE.PATROL;

    [Header("Room Event Settings (Rooms 45 - 48)")]
    public int eventStartRoom = 45;
    public int eventEndRoom = 48;
    [Tooltip("If true, automatically destroys this ghost once the player moves past eventEndRoom")]
    public bool autoDespawnAfterEvent = true;

    [Header("Movement & Speed")]
    public float moveSpeed = 3f;
    [Tooltip("Chase speed multiplier (2x-3x faster by default)")]
    public float chaseSpeedMultiplier = 3f;
    [Tooltip("Horizontal distance threshold to consider a patrol spot or locker reached")]
    public float reachPointDistance = 0.5f;
    [Tooltip("Distance at which the ghost kills the player during chase")]
    public float killDistance = 1.2f;

    [Header("Facing & Flipping")]
    [Tooltip("If true, inverts sprite facing direction if the sprite natively faces left")]
    public bool invertFacing = false;

    [Header("Detection Range (Front Rectangle)")]
    [Tooltip("Size (width, height) of the detection box")]
    public Vector2 detectionSize = new Vector2(6f, 3f);
    [Tooltip("Offset of the detection box from the ghost center (x is multiplied by facing direction)")]
    public Vector2 detectionOffset = new Vector2(3f, 0f);

    [Header("Locker Detection Range")]
    [Tooltip("Horizontal distance to detect a targeted locker while moving between patrol spots")]
    public float lockerDetectRange = 4f;

    [Header("Sniff Locker Settings")]
    [Tooltip("Seconds the ghost spends sniffing each locker")]
    public float sniffTime = 2.5f;
    [Tooltip("Lockers to choose per second-event room that has appeared (3 * rooms)")]
    public int lockersPerRoom = 3;
    [Tooltip("Legacy setting kept for serialization compatibility")]
    public int patrolSpotsBeforeNextSniff = 2;

    [Header("References")]
    public Transform player_Target;
    public Animator animator;
    public AudioSource audioSource;
    public AudioClip detectedSound;
    public AudioClip sniffSound;
    public AudioClip attackSound;

    [Header("Patrol Runtime Info")]
    public Transform[] PartrolSpot;
    public int currentPatrolSpot = 0;

    // Private runtime tracking
    private Hided Hide;
    private SpriteRenderer spriteRenderer;
    private List<GameObject> lockerToSniff = new List<GameObject>();
    private HashSet<GameObject> sniffedLockers = new HashSet<GameObject>();
    private GameObject currentLockerTarget;
    private float facingDirection = 1f; // 1 = right, -1 = left
    private bool isSniffing = false;
    private bool isRushingToKill = false;
    private Coroutine sniffingCoroutine;
    private float searchPatrolSpotsTimer = 0f;
    private int lastCheckedRoomCount = -1;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        FindPlayerReferencesIfNeeded();
        RefreshPatrolSpots();
        ChooseLockersToSniff();
        PickRandomPatrolSpot();

        // Initialize facing direction based on scale
        facingDirection = transform.localScale.x >= 0 ? 1f : -1f;
    }

    void Update()
    {
        // 1. Manage Lifecycle based on RoomCounter (Rooms 45 to 48)
        if (RoomCounter.instance != null)
        {
            int currentRoom = RoomCounter.instance.PassedRoomCount;

            // Destroy ghost after event rooms are passed
            if (autoDespawnAfterEvent && currentRoom > eventEndRoom)
            {
                Debug.Log($"[SecondEventGhost] Player passed room {eventEndRoom}. Despawning ghost.");
                Destroy(gameObject);
                return;
            }

            // If player hasn't reached room 45 yet, keep ghost idle
            if (currentRoom < eventStartRoom)
            {
                if (ghost_State != GHOST_STATE.IDLE)
                {
                    ghost_State = GHOST_STATE.IDLE;
                }
                return;
            }

            // If room count changed, refresh patrol spots and update locker pool
            if (currentRoom != lastCheckedRoomCount)
            {
                lastCheckedRoomCount = currentRoom;
                RefreshPatrolSpots();
                UpdateLockerPoolIfNeeded();
            }
        }

        // Periodically refresh patrol spots in case new rooms were generated
        searchPatrolSpotsTimer -= Time.deltaTime;
        if (searchPatrolSpotsTimer <= 0f)
        {
            searchPatrolSpotsTimer = 1.5f;
            RefreshPatrolSpots();
        }

        FindPlayerReferencesIfNeeded();

        // When player is dead, ignore detection/chase and just keep patrolling like nothing happened
        if (isKillingPlayer)
        {
            if (ghost_State != GHOST_STATE.PATROL && ghost_State != GHOST_STATE.SNIFF)
            {
                ghost_State = GHOST_STATE.PATROL;
            }
        }
        else
        {
            // 2. Final room check: In the last room, ghost is forced into CHASE mode permanently
            if (IsInLastRoom())
            {
                if (ghost_State != GHOST_STATE.CHASE)
                {
                    StartChase();
                }
            }
            else
            {
                // 3. Normal detection check: transitions into CHASE if player seen in front rectangle
                if (ghost_State != GHOST_STATE.CHASE && !isRushingToKill)
                {
                    if (CheckPlayerInDetectionRange())
                    {
                        StartChase();
                    }
                }
            }
        }

        // 4. State Machine execution
        switch (ghost_State)
        {
            case GHOST_STATE.IDLE:
                Idle();
                break;
            case GHOST_STATE.PATROL:
                Patrol();
                break;
            case GHOST_STATE.SNIFF:
                Sniff();
                break;
            case GHOST_STATE.CHASE:
                Chase();
                break;
        }

        UpdateAnimator();
    }

    // ==========================================
    // PATROL LOGIC
    // ==========================================
    private void Patrol()
    {
        if (PartrolSpot == null || PartrolSpot.Length == 0)
        {
            RefreshPatrolSpots();
            if (PartrolSpot == null || PartrolSpot.Length == 0) return;
        }

        // Ensure current patrol spot index is valid
        if (currentPatrolSpot < 0 || currentPatrolSpot >= PartrolSpot.Length)
        {
            PickRandomPatrolSpot();
            if (currentPatrolSpot < 0 || currentPatrolSpot >= PartrolSpot.Length) return;
        }

        Transform targetSpot = PartrolSpot[currentPatrolSpot];
        if (targetSpot == null)
        {
            RefreshPatrolSpots();
            PickRandomPatrolSpot();
            return;
        }

        // Check if a targeted locker is within detection range while moving between patrol spots
        GameObject nearbyLocker = FindNearbyTargetLocker();
        if (nearbyLocker != null)
        {
            currentLockerTarget = nearbyLocker;
            ghost_State = GHOST_STATE.SNIFF;
            return;
        }

        // Face towards patrol spot immediately
        float diffX = targetSpot.position.x - transform.position.x;
        if (Mathf.Abs(diffX) > 0.05f)
        {
            SetFacing(Mathf.Sign(diffX));
        }

        // Move horizontally towards patrol spot
        float newX = Mathf.MoveTowards(transform.position.x, targetSpot.position.x, moveSpeed * Time.deltaTime);
        transform.position = new Vector3(newX, transform.position.y, transform.position.z);

        // Check if reached current patrol spot horizontally
        if (Mathf.Abs(transform.position.x - targetSpot.position.x) <= reachPointDistance)
        {
            PickRandomPatrolSpot();
        }
    }

    private void PickRandomPatrolSpot()
    {
        if (PartrolSpot == null || PartrolSpot.Length == 0) return;

        if (PartrolSpot.Length == 1)
        {
            currentPatrolSpot = 0;
            return;
        }

        int newIndex = Random.Range(0, PartrolSpot.Length);
        if (newIndex == currentPatrolSpot)
        {
            newIndex = (newIndex + 1) % PartrolSpot.Length;
        }
        currentPatrolSpot = newIndex;
    }

    private GameObject FindNearbyTargetLocker()
    {
        if (lockerToSniff == null || lockerToSniff.Count == 0) return null;

        foreach (GameObject locker in lockerToSniff)
        {
            if (locker == null) continue;
            if (sniffedLockers.Contains(locker)) continue;

            float distX = Mathf.Abs(transform.position.x - locker.transform.position.x);
            float distY = Mathf.Abs(transform.position.y - locker.transform.position.y);

            // Within horizontal detection range and reasonable vertical height
            if (distX <= lockerDetectRange && distY <= 3.5f)
            {
                return locker;
            }
        }
        return null;
    }

    // ==========================================
    // SNIFF LOCKER LOGIC
    // ==========================================
    private void Sniff()
    {
        // If already performing the sniffing coroutine or rushing, stay in routine
        if (isSniffing || isRushingToKill) return;

        // If target locker was destroyed or missing, return to patrol
        if (currentLockerTarget == null)
        {
            ghost_State = GHOST_STATE.PATROL;
            PickRandomPatrolSpot();
            return;
        }

        // Face towards the locker while walking to it
        float diffX = currentLockerTarget.transform.position.x - transform.position.x;
        if (Mathf.Abs(diffX) > 0.05f)
        {
            SetFacing(Mathf.Sign(diffX));
        }

        // Move horizontally towards locker
        float newX = Mathf.MoveTowards(transform.position.x, currentLockerTarget.transform.position.x, moveSpeed * Time.deltaTime);
        transform.position = new Vector3(newX, transform.position.y, transform.position.z);

        // Check if reached locker horizontally
        if (Mathf.Abs(transform.position.x - currentLockerTarget.transform.position.x) <= reachPointDistance)
        {
            sniffingCoroutine = StartCoroutine(SniffLockerRoutine(currentLockerTarget));
        }
    }

    private IEnumerator SniffLockerRoutine(GameObject locker)
    {
        isSniffing = true;

        if (audioSource != null && sniffSound != null)
        {
            audioSource.PlayOneShot(sniffSound);
        }

        // Face the locker while sniffing
        if (locker != null)
        {
            float diffX = locker.transform.position.x - transform.position.x;
            if (Mathf.Abs(diffX) > 0.05f)
            {
                SetFacing(Mathf.Sign(diffX));
            }
        }

        float timer = 0f;
        while (timer < sniffTime)
        {
            // If forced to chase because player reached final room or was seen, cancel sniff
            if (ghost_State == GHOST_STATE.CHASE)
            {
                isSniffing = false;
                sniffingCoroutine = null;
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // SNIFFING TIME IS UP: Evaluate if player is inside this locker!
        bool playerInThisLocker = false;
        if (locker != null)
        {
            Locker lockerScript = locker.GetComponent<Locker>();
            playerInThisLocker = (lockerScript != null && lockerScript.IsPlayerInsideThisLocker);

            // Fallback check: player is hiding and located at this locker's position
            if (!playerInThisLocker && Hide != null && Hide.IsPlayerHide && player_Target != null)
            {
                if (Vector2.Distance(player_Target.position, locker.transform.position) < 1.5f)
                {
                    playerInThisLocker = true;
                }
            }
        }

        if (playerInThisLocker)
        {
            // Player is inside the locker when sniffing time expired -> rush to you and you die!
            isSniffing = false;
            sniffingCoroutine = null;
            yield return StartCoroutine(RushAndKillPlayer(locker.transform.position));
            yield break;
        }

        // Player was not inside -> Mark this locker as sniffed
        if (locker != null && !sniffedLockers.Contains(locker))
        {
            sniffedLockers.Add(locker);
        }

        isSniffing = false;
        sniffingCoroutine = null;
        currentLockerTarget = null;

        // Clean up destroyed lockers
        lockerToSniff.RemoveAll(l => l == null);

        // Once all targeted lockers have been sniffed, randomize a new pool of lockers to sniff
        bool allSniffed = (lockerToSniff.Count == 0 || lockerToSniff.All(l => l == null || sniffedLockers.Contains(l)));
        if (allSniffed)
        {
            Debug.Log("[SecondEventGhost] All targeted lockers have been sniffed! Rolling a new pool of lockers.");
            ChooseLockersToSniff();
        }

        // Resume patrol
        PickRandomPatrolSpot();
        ghost_State = GHOST_STATE.PATROL;
    }

    private IEnumerator RushAndKillPlayer(Vector3 targetPos)
    {
        isRushingToKill = true;
        ghost_State = GHOST_STATE.CHASE;

        Debug.Log("[SecondEventGhost] Sniffing time is up and player is inside locker! Rushing to kill!");

        if (audioSource != null && attackSound != null)
        {
            audioSource.PlayOneShot(attackSound);
        }

        float rushSpeed = moveSpeed * chaseSpeedMultiplier * 1.5f;
        float elapsed = 0f;
        float maxRushDuration = 0.4f;

        while (elapsed < maxRushDuration)
        {
            elapsed += Time.deltaTime;
            float newX = Mathf.MoveTowards(transform.position.x, targetPos.x, rushSpeed * Time.deltaTime);
            transform.position = new Vector3(newX, transform.position.y, transform.position.z);

            if (Mathf.Abs(transform.position.x - targetPos.x) <= 0.1f)
            {
                break;
            }
            yield return null;
        }

        KillPlayer("Player caught hiding inside the sniffed locker when time expired!");
    }

    // ==========================================
    // CHASE LOGIC
    // ==========================================
    private void StartChase()
    {
        if (sniffingCoroutine != null)
        {
            StopCoroutine(sniffingCoroutine);
            sniffingCoroutine = null;
            isSniffing = false;
        }

        ghost_State = GHOST_STATE.CHASE;

        if (audioSource != null && detectedSound != null)
        {
            audioSource.PlayOneShot(detectedSound);
        }
    }

    private void Chase()
    {
        bool isLastRoom = IsInLastRoom();

        // In normal rooms (45 - 47), if player successfully hides, ghost loses target and returns to patrol
        if (!isLastRoom && Hide != null && Hide.IsPlayerHide)
        {
            ghost_State = GHOST_STATE.PATROL;
            PickRandomPatrolSpot();
            return;
        }

        if (player_Target == null)
        {
            FindPlayerReferencesIfNeeded();
            if (player_Target == null)
            {
                if (!isLastRoom)
                {
                    ghost_State = GHOST_STATE.PATROL;
                    PickRandomPatrolSpot();
                }
                return;
            }
        }

        // In the last room, the ghost knows exactly where the player is even inside lockers (no hiding allowed)
        Vector3 targetDestination = player_Target.position;

        // Face player horizontally while chasing
        float diffX = targetDestination.x - transform.position.x;
        if (Mathf.Abs(diffX) > 0.05f)
        {
            SetFacing(Mathf.Sign(diffX));
        }

        // Walk horizontally towards player at chase speed
        float chaseSpeed = moveSpeed * chaseSpeedMultiplier;
        float newX = Mathf.MoveTowards(transform.position.x, targetDestination.x, chaseSpeed * Time.deltaTime);
        transform.position = new Vector3(newX, transform.position.y, transform.position.z);

        // Check kill distance
        float dist = Vector2.Distance(transform.position, targetDestination);
        if (dist <= killDistance || Mathf.Abs(transform.position.x - targetDestination.x) <= (killDistance * 0.7f))
        {
            KillPlayer(isLastRoom
                ? "Player caught by ghost in the final room (forced chase mode)!"
                : "Player caught by ghost during chase!");
        }
    }

    private void Idle()
    {
        // Do nothing while idle waiting for room 45
    }

    public bool IsInLastRoom()
    {
        if (RoomCounter.instance != null)
        {
            return RoomCounter.instance.PassedRoomCount >= eventEndRoom;
        }
        return false;
    }

    // ==========================================
    // DETECTION & FLIPPING HELPERS
    // ==========================================
    private bool CheckPlayerInDetectionRange()
    {
        if (Hide != null && Hide.IsPlayerHide)
        {
            return false;
        }

        Vector2 boxCenter = GetDetectionBoxCenter();
        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, detectionSize, 0f);

        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag("Player") || (hit.transform.root != null && hit.transform.root.CompareTag("Player")))
            {
                return true;
            }
        }
        return false;
    }

    public Vector2 GetDetectionBoxCenter()
    {
        Vector2 currentOffset = new Vector2(detectionOffset.x * facingDirection, detectionOffset.y);
        return (Vector2)transform.position + currentOffset;
    }

    private void SetFacing(float dir)
    {
        facingDirection = dir >= 0f ? 1f : -1f;

        Vector3 scale = transform.localScale;
        float sign = invertFacing ? -facingDirection : facingDirection;
        scale.x = Mathf.Abs(scale.x) * sign;
        transform.localScale = scale;
    }

    // ==========================================
    // LOCKER SELECTION HELPERS
    // ==========================================
    private void ChooseLockersToSniff()
    {
        sniffedLockers.Clear();

        int roomsAppeared = 1;
        if (RoomCounter.instance != null)
        {
            int currentRoom = RoomCounter.instance.PassedRoomCount;
            roomsAppeared = Mathf.Clamp(currentRoom - eventStartRoom + 1, 1, (eventEndRoom - eventStartRoom + 1));
        }

        int targetCount = lockersPerRoom * roomsAppeared;

        GameObject[] allLockers = GameObject.FindGameObjectsWithTag("Locker");
        if (allLockers == null || allLockers.Length == 0)
        {
            lockerToSniff.Clear();
            return;
        }

        // Shuffle all available lockers randomly
        List<GameObject> shuffled = allLockers.Where(l => l != null).OrderBy(x => Random.value).ToList();

        int countToPick = Mathf.Min(targetCount, shuffled.Count);
        lockerToSniff = shuffled.Take(countToPick).ToList();

        Debug.Log($"[SecondEventGhost] Chosen {lockerToSniff.Count} lockers to sniff for {roomsAppeared} event room(s).");
    }

    private void UpdateLockerPoolIfNeeded()
    {
        int roomsAppeared = 1;
        if (RoomCounter.instance != null)
        {
            roomsAppeared = Mathf.Clamp(RoomCounter.instance.PassedRoomCount - eventStartRoom + 1, 1, (eventEndRoom - eventStartRoom + 1));
        }

        int targetCount = lockersPerRoom * roomsAppeared;
        if (lockerToSniff.Count < targetCount)
        {
            GameObject[] allLockers = GameObject.FindGameObjectsWithTag("Locker");
            if (allLockers != null)
            {
                foreach (GameObject l in allLockers)
                {
                    if (l != null && !lockerToSniff.Contains(l))
                    {
                        lockerToSniff.Add(l);
                        if (lockerToSniff.Count >= targetCount) break;
                    }
                }
            }
        }
    }

    // ==========================================
    // PATROL SPOTS DISCOVERY
    // ==========================================
    private void RefreshPatrolSpots()
    {
        GameObject[] spots = GameObject.FindGameObjectsWithTag("PartrolSpot");
        if (spots != null && spots.Length > 0)
        {
            PartrolSpot = spots.OrderBy(x => x.name).Select(x => x.transform).ToArray();

            if (currentPatrolSpot >= PartrolSpot.Length)
            {
                currentPatrolSpot = Mathf.Max(0, PartrolSpot.Length - 1);
            }
        }
    }

    private bool isKillingPlayer = false;

    private void KillPlayer(string reason)
    {
        if (isKillingPlayer) return;
        isKillingPlayer = true;

        Debug.Log($"[SecondEventGhost] {reason} -> Starting death routine!");

        if (audioSource != null && attackSound != null)
        {
            audioSource.PlayOneShot(attackSound);
        }

        StartCoroutine(PlayerDieCoroutine());
    }

    private IEnumerator PlayerDieCoroutine()
    {
        // Resume patrolling like nothing happened!
        ghost_State = GHOST_STATE.PATROL;
        isSniffing = false;
        isRushingToKill = false;
        PickRandomPatrolSpot();

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

        if (Hide != null) Hide.enabled = false;

        yield return new WaitForSeconds(2f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        CheckContactKill(collision.gameObject);
        CheckLockerContactInLastRoom(collision.gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        CheckContactKill(collision.gameObject);
        CheckLockerContactInLastRoom(collision.gameObject);
    }

    private void CheckContactKill(GameObject obj)
    {
        if (isKillingPlayer || ghost_State != GHOST_STATE.CHASE) return;

        bool isPlayer = obj.CompareTag("Player") ||
                        (obj.transform.root != null && obj.transform.root.CompareTag("Player"));

        bool isLastRoom = IsInLastRoom();

        if (isPlayer && (isLastRoom || Hide == null || !Hide.IsPlayerHide))
        {
            KillPlayer(isLastRoom ? "Player touched ghost in the final room!" : "Player touched ghost while chasing!");
        }
    }

    private void CheckLockerContactInLastRoom(GameObject obj)
    {
        if (isKillingPlayer || !IsInLastRoom() || ghost_State != GHOST_STATE.CHASE) return;

        Locker locker = obj.GetComponent<Locker>();
        if (locker == null) locker = obj.GetComponentInParent<Locker>();

        if (locker != null && locker.IsPlayerInsideThisLocker)
        {
            KillPlayer("Player caught hiding in locker in the final room!");
        }
    }

    // ==========================================
    // UTILITIES
    // ==========================================
    private void FindPlayerReferencesIfNeeded()
    {
        if (player_Target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player_Target = player.transform;
            }
        }

        if (Hide == null)
        {
            Hide = FindObjectOfType<Hided>();
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        bool isMoving = (ghost_State == GHOST_STATE.PATROL || ghost_State == GHOST_STATE.CHASE || (ghost_State == GHOST_STATE.SNIFF && !isSniffing));
        animator.SetBool("IsMoving", isMoving);
        animator.SetBool("IsChasing", ghost_State == GHOST_STATE.CHASE);
        animator.SetBool("IsSniffing", isSniffing);
    }

    // ==========================================
    // GIZMOS (VISUAL DEBUGGING)
    // ==========================================
    void OnDrawGizmos()
    {
        // 1. Front-facing rectangle detection range
        Gizmos.color = (ghost_State == GHOST_STATE.CHASE) ? Color.red : Color.yellow;
        Vector2 center = GetDetectionBoxCenter();
        Gizmos.DrawWireCube(center, new Vector3(detectionSize.x, detectionSize.y, 0.1f));

        // 2. Kill range circle
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, killDistance);

        // 3. Locker detection area
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(lockerDetectRange * 2f, 4f, 0.1f));

        // 4. Targeted lockers
        if (lockerToSniff != null && lockerToSniff.Count > 0)
        {
            for (int i = 0; i < lockerToSniff.Count; i++)
            {
                if (lockerToSniff[i] != null)
                {
                    bool isSniffed = sniffedLockers != null && sniffedLockers.Contains(lockerToSniff[i]);
                    Gizmos.color = isSniffed ? Color.green : Color.magenta;
                    Gizmos.DrawWireSphere(lockerToSniff[i].transform.position, 0.5f);

                    if (lockerToSniff[i] == currentLockerTarget)
                    {
                        Gizmos.DrawLine(transform.position, lockerToSniff[i].transform.position);
                    }
                }
            }
        }
    }
}