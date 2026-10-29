using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SecondEventGhost : MonoBehaviour
{
    public static readonly HashSet<SecondEventGhost> ActiveGhosts = new HashSet<SecondEventGhost>();

    public enum GHOST_STATE
    {
        IDLE,
        PATROL,
        SNIFF,
        CHASE
    }

    public enum WORKFLOW_STEP
    {
        WAITING_FOR_EVENT,
        CHOOSE_ROOM,
        CHOOSE_LOCKER,
        PATROL,
        SNIFF_CHOSEN_LOCKER,
        CHASE_INTERRUPT
    }

    [Header("Current State")]
    public GHOST_STATE ghost_State = GHOST_STATE.PATROL;
    [Tooltip("Live readout of the current step in: Choose Room -> Choose Locker -> Patrol -> Sniff Chosen Locker -> Loop")]
    public WORKFLOW_STEP currentWorkflowStep = WORKFLOW_STEP.WAITING_FOR_EVENT;

    [Header("Room Event Settings (Rooms 45 - 48)")]
    public int eventStartRoom = 45;
    public int eventEndRoom = 48;
    [Tooltip("If true, automatically destroys this ghost once the player moves past eventEndRoom")]
    public bool autoDespawnAfterEvent = true;

    [Header("Timeline On Disappear")]
    [Tooltip("Condition 3: Timeline prefab instantiated when SecondEventGhost disappears from the scene")]
    public GameObject timelinePrefabOnDisappear;
    [Tooltip("Optional spawn point override for timelinePrefabOnDisappear (uses prefab's own position/rotation if null)")]
    public Transform timelineSpawnPoint;

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
    [Tooltip("Legacy setting kept for serialization compatibility")]
    public int lockersPerRoom = 3;
    [Tooltip("Number of random lockers chosen right after choosing the room, to be sniffed after patrolling")]
    public int lockersToSniffPerCycle = 2;
    [Tooltip("Number of patrol points to visit in the chosen room before sniffing the pre-chosen lockers")]
    public int patrolSpotsBeforeNextSniff = 2;

    [Header("Patrol Idle Delay")]
    [Tooltip("Idle delay in seconds before continuing patrol after reaching/touching a patrol spot")]
    public float patrolSpotIdleDelay = 2.0f;
    private float patrolSpotIdleTimer = 0f;
    private bool isWaitingAtPatrolSpot = false;

    [Header("Locker Sniff Kill Chance")]
    [Range(0, 100)]
    [Tooltip("Percentage chance to kill the player when sniffing the locker the player is hiding inside (default: 80%)")]
    public int lockerKillChancePercentage = 80;

    [Header("References")]
    public Transform player_Target;
    public Animator animator;
    public AudioSource audioSource;
    public AudioSource ChaseTheme;
    public float chaseThemeTargetVolume = 1f;
    public float chaseThemeFadeSpeed = 6f;
    public AudioClip detectedSound;
    public AudioClip sniffSound;
    public AudioClip attackSound;

    [Header("Patrol Runtime Info")]
    public Transform[] PartrolSpot;
    public int currentPatrolSpot = 0;
    [Tooltip("Lockers pre-chosen in Step 2 (Choose Locker), waiting to be sniffed in Step 4 after patrolling")]
    public List<GameObject> lockerToSniff = new List<GameObject>();
    [Tooltip("The specific pre-chosen locker currently being walked to / sniffed in Step 4")]
    public GameObject currentLockerTarget;

    [Header("Room Tracking (Event Rooms 45 - 48)")]
    [Tooltip("The room GameObject the ghost is currently assigned to patrol")]
    public GameObject currentAssignedRoom;
    private float roomCheckTimer = 0f;

    [Header("Movement Sensing (Buff Settings)")]
    [Tooltip("If true, ghost detects player moving without crawling (player must crawl to stay silent)")]
    public bool detectPlayerMovement = true;
    [Tooltip("How long ghost investigates footstep noise if line of sight is lost")]
    public float noiseInvestigateDuration = 2.0f;
    private float noiseInvestigateTimer = 0f;
    private float loudMovementConfirmTimer = 0f;
    private int lastNoiseCheckFrame = -1;
    private bool cachedNoiseCheckResult = false;
    private Vector3 lastHeardNoisePosition;
    private bool isChasingFromNoise = false;
    private bool isChasingFromDoorOpen = false;
    private float doorChaseGraceTimer = 0f;
    private SideScrollMovement playerMovement;
    private Rigidbody2D playerRb;
    private Rigidbody2D selfRb;

    // Private runtime tracking
    private Hided Hide;
    private SpriteRenderer spriteRenderer;
    private HashSet<GameObject> sniffedLockers = new HashSet<GameObject>();
    private GameObject lastSniffedLocker;
    private List<Vector3> currentRoomPatrolPoints = new List<Vector3>();
    private int patrolPointsRemainingInCycle = 0;
    private bool isDoingOffRoomExcursion = false;
    private int excursionInitialRoomCount = 0;
    private float facingDirection = 1f; // 1 = right, -1 = left
    private bool isSniffing = false;
    private bool isRushingToKill = false;
    private bool isKillingPlayer = false;
    private Coroutine sniffingCoroutine;
    private Coroutine chaseThemeFadeCoroutine;
    private bool isFadingOutChaseTheme = false;
    private Vector3 lastSeenPlayerPosition;
    private float lostSightTimer = 0f;
    private int lastCheckedRoomCount = -1;
    private float cachedReferencePatrolY;
    private bool hasReferencePatrolY = false;

    private class CachedRoomBounds
    {
        public float rawMinX;
        public float rawMaxX;
        public float rawMinY;
        public float rawMaxY;
        public float centerX => 0.5f * (rawMinX + rawMaxX);
        public List<Door> doors = new List<Door>();
        public List<float> doorXs = new List<float>();
    }

    private readonly Dictionary<GameObject, CachedRoomBounds> roomBoundsCache = new Dictionary<GameObject, CachedRoomBounds>();

    [System.Serializable]
    public class EventRoomData
    {
        public GameObject roomObject;
        public int roomNumber = -1;
        public bool isSecondEventRoom = false;
        public List<Transform> patrolSpots = new List<Transform>();
        public List<Vector3> patrolPoints = new List<Vector3>();
        public List<GameObject> lockers = new List<GameObject>();
        public float minX = float.MaxValue;
        public float maxX = float.MinValue;

        public bool IsValid => roomObject != null && patrolPoints != null && patrolPoints.Count > 0;

        public bool ContainsX(float x, float margin = 0f)
        {
            return x >= (minX - margin) && x <= (maxX + margin);
        }

        public float DistanceToX(float x)
        {
            if (ContainsX(x, 0f)) return 0f;
            if (x < minX) return minX - x;
            return x - maxX;
        }
    }

    void OnEnable()
    {
        ActiveGhosts.Add(this);
        EnsureGhostPassThroughPhysics();
    }

    void OnDisable()
    {
        ActiveGhosts.Remove(this);
    }

    void OnDestroy()
    {
        ActiveGhosts.Remove(this);
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        cachedReferencePatrolY = transform.position.y;
        EnsureGhostPassThroughPhysics();
    }

    void Start()
    {
        ActiveGhosts.Add(this);
        EnsureGhostPassThroughPhysics();
        FindPlayerReferencesIfNeeded();

        // Initialize facing direction based on scale
        facingDirection = transform.localScale.x >= 0 ? 1f : -1f;

        RoomCounter.RegisterSecondEventGhost(gameObject, timelinePrefabOnDisappear, timelineSpawnPoint);

        if (RoomCounter.instance == null || IsWithinActiveEventLifecycle())
        {
            ChooseNextPatrolRoom(true);
        }
        else
        {
            ghost_State = GHOST_STATE.IDLE;
            currentWorkflowStep = WORKFLOW_STEP.WAITING_FOR_EVENT;
        }
    }

    /// <summary>
    /// Ensures the ghost's own colliders/rigidbody never get snagged on walls, doorframes, doors, or floor seams
    /// when patrolling between rooms via transform.position.
    /// </summary>
    private void EnsureGhostPassThroughPhysics()
    {
        if (selfRb == null)
        {
            selfRb = GetComponent<Rigidbody2D>();
        }
        if (selfRb != null)
        {
            selfRb.gravityScale = 0f;
            selfRb.linearVelocity = Vector2.zero;
            selfRb.angularVelocity = 0f;
            selfRb.bodyType = RigidbodyType2D.Kinematic;
        }

        Collider2D[] ownCols = GetComponentsInChildren<Collider2D>(true);
        if (ownCols != null)
        {
            for (int i = 0; i < ownCols.Length; i++)
            {
                if (ownCols[i] != null)
                {
                    ownCols[i].isTrigger = true;
                }
            }
        }
    }

    private bool IsEventRangeRoom(GameObject r)
    {
        if (r == null || IsStairRoom(r)) return false;

        if (RoomCounter.instance != null)
        {
            if (RoomCounter.instance.IsSecondEventRoom(r))
            {
                return true;
            }

            int roomNum = RoomCounter.instance.GetRoomNumber(r);
            if (roomNum >= eventStartRoom && roomNum <= eventEndRoom)
            {
                return true;
            }
        }

        // Also recognize rooms explicitly built with PartrolSpot markers once event has started
        if (RoomCounter.instance == null || RoomCounter.instance.PassedRoomCount >= eventStartRoom)
        {
            Transform[] children = r.GetComponentsInChildren<Transform>(true);
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i] != null && children[i].CompareTag("PartrolSpot"))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool HasUnfinishedEventRoom()
    {
        if (RoomCounter.instance == null || RoomCounter.instance.currentRooms == null) return false;

        for (int i = 0; i < RoomCounter.instance.currentRooms.Count; i++)
        {
            GameObject r = RoomCounter.instance.currentRooms[i];
            if (r == null || !IsEventRangeRoom(r)) continue;

            CachedRoomBounds b = GetOrComputeRoomBounds(r);
            if (b != null && b.doors.Count > 0)
            {
                bool anyDoorOpened = false;
                for (int d = 0; d < b.doors.Count; d++)
                {
                    if (b.doors[d] != null && b.doors[d].isOpened && !b.doors[d].IsOpenedByGhostWithoutEvent)
                    {
                        anyDoorOpened = true;
                        break;
                    }
                }
                if (!anyDoorOpened)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool IsWithinActiveEventLifecycle()
    {
        if (RoomCounter.instance == null) return true;

        int currentRoom = RoomCounter.instance.PassedRoomCount;
        if (currentRoom < eventStartRoom) return false;
        if (currentRoom <= eventEndRoom) return true;

        return HasUnfinishedEventRoom();
    }

    void Update()
    {
        if (doorChaseGraceTimer > 0f)
        {
            doorChaseGraceTimer -= Time.deltaTime;
        }

        // 1. Manage Lifecycle based on RoomCounter (Rooms 45 to 48)
        if (RoomCounter.instance != null)
        {
            int currentRoom = RoomCounter.instance.PassedRoomCount;

            // Destroy ghost after all event rooms are passed and their exit doors opened
            if (autoDespawnAfterEvent && currentRoom > eventEndRoom && !HasUnfinishedEventRoom())
            {
                Debug.Log($"[SecondEventGhost] Player passed second event rooms (count {currentRoom}). Despawning ghost.");
                RoomCounter.TriggerSecondEventGhostDisappearedTimeline(timelinePrefabOnDisappear, timelineSpawnPoint);
                Destroy(gameObject);
                return;
            }

            // If player hasn't reached eventStartRoom yet, keep ghost idle
            if (currentRoom < eventStartRoom)
            {
                if (ghost_State != GHOST_STATE.IDLE)
                {
                    ghost_State = GHOST_STATE.IDLE;
                }
                currentWorkflowStep = WORKFLOW_STEP.WAITING_FOR_EVENT;
                return;
            }

            // Wait to wake from IDLE until at least one non-stair eligible room exists (e.g. skip Stair Room #45 if before EventRooms2[0])
            if (ghost_State == GHOST_STATE.IDLE)
            {
                List<EventRoomData> readyRooms = GetEligibleRooms();
                if (readyRooms != null && readyRooms.Count > 0)
                {
                    ghost_State = GHOST_STATE.PATROL;
                    ChooseNextPatrolRoom(true);
                }
                else
                {
                    currentWorkflowStep = WORKFLOW_STEP.WAITING_FOR_EVENT;
                    return;
                }
            }

            // If room count changed, validate current assigned room (e.g. drop Room 44 fallback once 2+ event rooms exist)
            if (currentRoom != lastCheckedRoomCount)
            {
                lastCheckedRoomCount = currentRoom;
                ValidateAssignedRoom(true);
            }
        }

        FindPlayerReferencesIfNeeded();
        ValidateAssignedRoom(false);

        // When player is dead, ignore detection/chase and just keep patrolling like nothing happened
        if (isKillingPlayer)
        {
            if (ghost_State != GHOST_STATE.PATROL)
            {
                ghost_State = GHOST_STATE.PATROL;
            }
        }
        else
        {
            // 2. Detection checks (active in all event rooms 45 - 48)
            if (ghost_State != GHOST_STATE.CHASE && !isRushingToKill)
            {
                // A) Movement noise: player can ONLY crawl while moving; walking/running triggers chase
                if (CheckPlayerMovementNoise())
                {
                    isChasingFromNoise = true;
                    isChasingFromDoorOpen = false;
                    noiseInvestigateTimer = noiseInvestigateDuration;
                    lastHeardNoisePosition = GetPlayerPosition();
                    StartChase();
                }
                // B) Front detection rectangle (visual sight)
                else if (CheckPlayerInDetectionRange())
                {
                    isChasingFromNoise = false;
                    isChasingFromDoorOpen = false;
                    StartChase();
                }
            }
        }

        // 3. State Machine execution
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

        // When ghost is not chasing, ensure ChaseTheme fades out if it was playing
        if (ghost_State != GHOST_STATE.CHASE && ChaseTheme != null && (ChaseTheme.isPlaying || ChaseTheme.volume > 0f) && !isFadingOutChaseTheme)
        {
            StopChaseTheme();
        }

        UpdateAnimator();
    }

    // ==========================================
    // DOOR LOCK, GHOST DOOR-OPENING & PLAYER DOOR-OPEN CHASE HOOKS
    // ==========================================

    /// <summary>
    /// Called by Door.IsLockedByEntity().
    /// Locks a closed non-stair door ONLY while SecondEventGhost is physically inside that closed door's room.
    /// Once the ghost leaves that room, the player can open the door to the next room.
    /// </summary>
    public bool IsBlockingClosedDoor(Door door)
    {
        if (door == null || door.isOpened || door.IsStairDoor) return false;
        if (isKillingPlayer || ghost_State == GHOST_STATE.IDLE) return false;

        if (!IsWithinActiveEventLifecycle())
        {
            return false;
        }

        GameObject doorRoom = GetRoomForObject(door.gameObject);
        if (doorRoom == null) return false;

        GameObject ghostPhysicalRoom = GetPhysicalRoomAtPosition(transform.position);
        return ghostPhysicalRoom != null && ghostPhysicalRoom == doorRoom;
    }

    /// <summary>
    /// Called by Door.OpenDoorBySecondEventGhost(this).
    /// Returns true if and only if the player is currently staying in that door's room
    /// (either hiding inside a locker in that room, or standing/walking/crawling inside that room).
    /// </summary>
    public bool IsPlayerStayingInDoorRoom(Door door)
    {
        if (door == null) return false;

        GameObject doorRoom = GetRoomForObject(door.gameObject);
        if (doorRoom == null) return false;

        // 1. Exact hierarchy check when hiding inside a locker
        Locker[] lockers = FindObjectsOfType<Locker>();
        if (lockers != null)
        {
            for (int i = 0; i < lockers.Length; i++)
            {
                if (lockers[i] != null && lockers[i].IsPlayerInsideThisLocker)
                {
                    return GetRoomForObject(lockers[i].gameObject) == doorRoom;
                }
            }
        }

        // 2. Floor-aware + partitioned physical room check
        FindPlayerReferencesIfNeeded();
        if (!HasValidPlayerPosition()) return false;

        Vector3 playerPos = GetPlayerPosition();
        CachedRoomBounds doorRoomBounds = GetOrComputeRoomBounds(doorRoom);
        if (doorRoomBounds != null)
        {
            if (playerPos.y < doorRoomBounds.rawMinY - 1.5f || playerPos.y > doorRoomBounds.rawMaxY + 2.0f)
            {
                return false;
            }
        }

        if (RoomCounter.instance != null && RoomCounter.instance.currentRooms != null && RoomCounter.instance.currentRooms.Count > 0)
        {
            GameObject playerRoom = GetPhysicalRoomAtPosition(playerPos);
            return playerRoom != null && playerRoom == doorRoom;
        }

        if (doorRoomBounds != null)
        {
            return playerPos.x >= (doorRoomBounds.rawMinX - 0.5f) &&
                   playerPos.x <= (doorRoomBounds.rawMaxX + 0.5f);
        }

        return false;
    }

    /// <summary>
    /// Called by Door.OpenDoorBySecondEventGhost(this) right after the ghost opens a closed door.
    /// </summary>
    public void OnGhostOpenedDoor(Door door, bool invokedOnOpen)
    {
        roomBoundsCache.Clear();

        if (invokedOnOpen && door != null)
        {
            GameObject doorRoom = GetRoomForObject(door.gameObject);
            if (doorRoom != null)
            {
                // Safety fallback if the room's RandomRoomGenerate was trigger-only rather than wired to Door.onOpen
                RandomRoomGenerate pendingGen = doorRoom.GetComponentInChildren<RandomRoomGenerate>(true);
                if (pendingGen != null && !pendingGen.HasTriggered)
                {
                    pendingGen.ForceGenerateNextRoomIfPending();
                    roomBoundsCache.Clear();
                }
            }
        }

        // If the ghost walked out the exit door and opening it spawned a new event room ahead, enter it immediately!
        if (isDoingOffRoomExcursion && invokedOnOpen && !isKillingPlayer && ghost_State != GHOST_STATE.CHASE)
        {
            List<EventRoomData> eligible = GetEligibleRooms();
            if (eligible != null && eligible.Count > 0)
            {
                EventRoomData newlyOpened = null;
                if (RoomCounter.instance != null && RoomCounter.instance.latestGeneratedRoom != null)
                {
                    newlyOpened = eligible.FirstOrDefault(r =>
                        r.roomObject == RoomCounter.instance.latestGeneratedRoom &&
                        r.roomObject != currentAssignedRoom);
                }

                if (newlyOpened != null)
                {
                    isDoingOffRoomExcursion = false;
                    StartRoomRoutine(newlyOpened);
                }
                else if (eligible.Any(r => r.roomObject != currentAssignedRoom))
                {
                    isDoingOffRoomExcursion = false;
                    ChooseNextPatrolRoom(false);
                }
            }
        }
    }

    /// <summary>
    /// Called by Door.OpenDoor() whenever the player opens a door.
    /// </summary>
    public static void NotifyDoorOpened(Door door)
    {
        if (door == null || door.IsStairDoor || ActiveGhosts.Count == 0) return;

        List<SecondEventGhost> snapshot = new List<SecondEventGhost>(ActiveGhosts);
        for (int i = 0; i < snapshot.Count; i++)
        {
            SecondEventGhost ghost = snapshot[i];
            if (ghost != null && ghost.gameObject.activeInHierarchy)
            {
                ghost.OnDoorOpenedByPlayer(door);
            }
        }
    }

    private void OnDoorOpenedByPlayer(Door door)
    {
        if (door == null || isKillingPlayer) return;

        bool nextRoomInEventRange = false;

        if (RoomCounter.instance != null)
        {
            GameObject doorRoom = GetRoomForObject(door.gameObject);
            bool doorRoomIsEvent = doorRoom != null && IsEventRangeRoom(doorRoom);

            if (doorRoomIsEvent && RoomCounter.instance.currentRooms != null)
            {
                int idx = RoomCounter.instance.currentRooms.IndexOf(doorRoom);
                if (idx >= 0 && idx + 1 < RoomCounter.instance.currentRooms.Count)
                {
                    GameObject nextRoom = RoomCounter.instance.currentRooms[idx + 1];
                    nextRoomInEventRange = IsEventRangeRoom(nextRoom);
                }
                else
                {
                    int doorRoomNum = RoomCounter.instance.GetRoomNumber(doorRoom);
                    if (doorRoomNum != -1)
                    {
                        nextRoomInEventRange = (doorRoomNum >= eventStartRoom && (doorRoomNum + 1) <= eventEndRoom);
                    }
                }
            }
            else if (doorRoom != null)
            {
                int doorRoomNum = RoomCounter.instance.GetRoomNumber(doorRoom);
                if (doorRoomNum != -1)
                {
                    nextRoomInEventRange = (doorRoomNum >= eventStartRoom && (doorRoomNum + 1) <= eventEndRoom);
                }
            }
        }

        if (nextRoomInEventRange)
        {
            Debug.Log($"[SecondEventGhost] Player opened door '{door.name}' into next event room! Chasing player!");
            isChasingFromDoorOpen = true;
            isChasingFromNoise = false;
            noiseInvestigateTimer = 0f;
            doorChaseGraceTimer = 0.65f;
            isDoingOffRoomExcursion = false;
            StartChase();
        }
    }

    // ==========================================
    // WORKFLOW LOOP:
    // 1) CHOOSE ROOM -> 2) CHOOSE LOCKER -> 3) PATROL -> 4) FINALLY SNIFF CHOSEN LOCKER -> LOOP
    // ==========================================

    private Door FindRoomExitDoor(GameObject roomObj, out float doorX, out float outDir)
    {
        doorX = 0f;
        outDir = 1f;
        if (roomObj == null) return null;

        CachedRoomBounds b = GetOrComputeRoomBounds(roomObj);
        if (b == null || b.doors == null || b.doors.Count == 0) return null;

        Door bestClosed = null;
        float bestClosedX = float.MinValue;
        Door bestAny = null;
        float bestAnyX = float.MinValue;

        for (int i = 0; i < b.doors.Count; i++)
        {
            Door d = b.doors[i];
            if (d == null || d.IsStairDoor) continue;
            float dx = b.doorXs[i];

            if (!d.isOpened && dx >= bestClosedX)
            {
                bestClosed = d;
                bestClosedX = dx;
            }
            if (dx >= bestAnyX)
            {
                bestAny = d;
                bestAnyX = dx;
            }
        }

        Door targetDoor = bestClosed != null ? bestClosed : bestAny;
        if (targetDoor == null) return null;

        doorX = bestClosed != null ? bestClosedX : bestAnyX;
        outDir = (doorX >= b.centerX) ? 1f : -1f;
        return targetDoor;
    }

    private bool CanAdvanceThroughClosedEventDoor(EventRoomData room)
    {
        if (room == null || room.roomObject == null) return false;

        Door exitDoor = FindRoomExitDoor(room.roomObject, out _, out _);
        if (exitDoor == null || exitDoor.isOpened) return false;

        if (RoomCounter.instance != null)
        {
            return RoomCounter.instance.HasForcedRoom();
        }

        return true;
    }

    private void ChooseNextPatrolRoom(bool preferInitialEventRoom = false)
    {
        currentWorkflowStep = WORKFLOW_STEP.CHOOSE_ROOM;

        List<EventRoomData> eligibleRooms = GetEligibleRooms();
        if (eligibleRooms == null || eligibleRooms.Count == 0) return;

        EventRoomData chosen = null;

        if (preferInitialEventRoom)
        {
            isDoingOffRoomExcursion = false;
            float refX = HasValidPlayerPosition() ? GetPlayerPosition().x : transform.position.x;
            List<EventRoomData> strictEventRooms = eligibleRooms
                .Where(r => r.isSecondEventRoom)
                .ToList();

            if (strictEventRooms.Count > 0)
            {
                chosen = strictEventRooms.OrderBy(r => r.DistanceToX(refX)).FirstOrDefault();
            }
            else
            {
                chosen = eligibleRooms.OrderBy(r => r.DistanceToX(refX)).FirstOrDefault();
            }
        }
        else if (eligibleRooms.Count == 1)
        {
            // When only 1 event room exists so far, walk out through its exit door (opening it if closed,
            // and invoking Door.onOpen ONLY if the player stays in that room!)
            if (!isDoingOffRoomExcursion)
            {
                StartOffRoomExcursion(eligibleRooms[0]);
                return;
            }

            isDoingOffRoomExcursion = false;
            chosen = eligibleRooms[0];
        }
        else
        {
            bool wasExcursion = isDoingOffRoomExcursion;
            isDoingOffRoomExcursion = false;

            List<EventRoomData> otherRooms = eligibleRooms
                .Where(r => r.roomObject != currentAssignedRoom)
                .ToList();

            EventRoomData currentRoomData = eligibleRooms.FirstOrDefault(r => r.roomObject == currentAssignedRoom);

            // If finishing a room whose forward event door is still closed, ghost can also walk out through that closed door
            if (!wasExcursion && currentRoomData != null && CanAdvanceThroughClosedEventDoor(currentRoomData))
            {
                if (otherRooms.Count == 0 || Random.Range(0, otherRooms.Count + 1) == otherRooms.Count)
                {
                    StartOffRoomExcursion(currentRoomData);
                    return;
                }
            }

            if (otherRooms.Count > 0)
            {
                chosen = otherRooms[Random.Range(0, otherRooms.Count)];
            }
            else
            {
                chosen = eligibleRooms[Random.Range(0, eligibleRooms.Count)];
            }
        }

        if (chosen != null)
        {
            StartRoomRoutine(chosen);
        }
    }

    private void StartOffRoomExcursion(EventRoomData targetRoom)
    {
        if (targetRoom == null || targetRoom.roomObject == null) return;

        isDoingOffRoomExcursion = true;
        excursionInitialRoomCount = (RoomCounter.instance != null && RoomCounter.instance.currentRooms != null)
            ? RoomCounter.instance.currentRooms.Count
            : 1;
        PartrolSpot = new Transform[0];

        float refY = GetPatrolHeightY();
        Door exitDoor = FindRoomExitDoor(targetRoom.roomObject, out float doorX, out float outDir);

        currentRoomPatrolPoints = new List<Vector3>();
        if (exitDoor != null)
        {
            float outX = doorX + outDir * 3.0f;
            currentRoomPatrolPoints.Add(new Vector3(outX, refY, transform.position.z));
            patrolPointsRemainingInCycle = 1;
        }
        else
        {
            float outX1 = targetRoom.minX - 4f;
            float outX2 = targetRoom.minX - 8f;
            currentRoomPatrolPoints.Add(new Vector3(outX1, refY, transform.position.z));
            currentRoomPatrolPoints.Add(new Vector3(outX2, refY, transform.position.z));
            patrolPointsRemainingInCycle = 2;
        }

        if (sniffingCoroutine != null)
        {
            StopCoroutine(sniffingCoroutine);
            sniffingCoroutine = null;
        }
        isSniffing = false;
        isWaitingAtPatrolSpot = false;
        patrolSpotIdleTimer = 0f;
        currentLockerTarget = null;
        lockerToSniff.Clear();
        sniffedLockers.Clear();
        currentPatrolSpot = 0;
        currentWorkflowStep = WORKFLOW_STEP.PATROL;

        if (!isKillingPlayer && ghost_State != GHOST_STATE.CHASE && ghost_State != GHOST_STATE.IDLE)
        {
            ghost_State = GHOST_STATE.PATROL;
        }

        Debug.Log($"[SecondEventGhost] Walking out through doorway of '{targetRoom.roomObject.name}' (exitDoor={(exitDoor != null ? exitDoor.name : "none")}).");
    }

    private void StartRoomRoutine(EventRoomData roomData)
    {
        if (roomData == null || roomData.roomObject == null) return;

        // STEP 1: Room Chosen
        isDoingOffRoomExcursion = false;
        currentAssignedRoom = roomData.roomObject;
        PartrolSpot = roomData.patrolSpots != null ? roomData.patrolSpots.ToArray() : new Transform[0];
        currentRoomPatrolPoints = new List<Vector3>(roomData.patrolPoints);

        if (sniffingCoroutine != null)
        {
            StopCoroutine(sniffingCoroutine);
            sniffingCoroutine = null;
        }
        isSniffing = false;
        isWaitingAtPatrolSpot = false;
        patrolSpotIdleTimer = 0f;
        currentLockerTarget = null;
        lockerToSniff.Clear();
        sniffedLockers.Clear();

        // STEP 2: Next, Choose Locker(s) in the chosen room BEFORE patrolling!
        currentWorkflowStep = WORKFLOW_STEP.CHOOSE_LOCKER;
        ChooseLockersToSniff(roomData.lockers);

        // STEP 3: Then, Patrol the chosen room!
        StartPatrolPhase(roomData.roomNumber);
    }

    private void StartPatrolPhase(int roomNumber = -1)
    {
        isWaitingAtPatrolSpot = false;
        patrolSpotIdleTimer = 0f;

        int spotsToVisit = Mathf.Max(1, patrolSpotsBeforeNextSniff);
        if (currentRoomPatrolPoints.Count <= 1)
        {
            spotsToVisit = 1;
        }
        patrolPointsRemainingInCycle = spotsToVisit;

        currentPatrolSpot = -1;
        PickRandomPatrolSpot();

        currentWorkflowStep = WORKFLOW_STEP.PATROL;
        if (!isKillingPlayer && ghost_State != GHOST_STATE.CHASE && ghost_State != GHOST_STATE.IDLE)
        {
            ghost_State = GHOST_STATE.PATROL;
        }

        string chosenLockersSummary = lockerToSniff.Count > 0
            ? string.Join(" -> ", lockerToSniff.Where(l => l != null).Select(l => l.name))
            : "none";

        Debug.Log($"[SecondEventGhost] Workflow: (1) Chose room '{currentAssignedRoom.name}' (Room #{roomNumber}) -> (2) Chose {lockerToSniff.Count} locker(s) [{chosenLockersSummary}] -> (3) Patrolling {patrolPointsRemainingInCycle} spot(s) -> (4) Will sniff chosen locker(s).");
    }

    private void StartSniffChosenLockersPhase()
    {
        // Safety fallback ONLY if lockers inside the room registered a frame late and none were pre-chosen yet;
        // never overwrites lockers already chosen in Step 2!
        if ((lockerToSniff == null || lockerToSniff.Count == 0) && sniffedLockers.Count == 0)
        {
            ChooseLockersToSniff();
        }

        currentLockerTarget = GetRemainingQueuedLocker();

        if (currentLockerTarget != null)
        {
            currentWorkflowStep = WORKFLOW_STEP.SNIFF_CHOSEN_LOCKER;
            ghost_State = GHOST_STATE.SNIFF;
            Debug.Log($"[SecondEventGhost] Finished patrolling '{(currentAssignedRoom != null ? currentAssignedRoom.name : "room")}' -> (4) Finally sniffing pre-chosen locker '{currentLockerTarget.name}'!");
        }
        else
        {
            // Room has no lockers (e.g. temporary fallback Room 44) -> Loop directly to Step 1 (Choose Room)
            ChooseNextPatrolRoom(false);
        }
    }

    private void ValidateAssignedRoom(bool immediate)
    {
        if (!immediate)
        {
            roomCheckTimer -= Time.deltaTime;
            if (roomCheckTimer > 0f) return;
            roomCheckTimer = 0.35f;
        }

        if (ghost_State == GHOST_STATE.IDLE) return;

        List<EventRoomData> eligibleRooms = GetEligibleRooms();
        if (eligibleRooms == null || eligibleRooms.Count == 0) return;

        if (isDoingOffRoomExcursion)
        {
            int activeRoomCount = (RoomCounter.instance != null && RoomCounter.instance.currentRooms != null)
                ? RoomCounter.instance.currentRooms.Count
                : eligibleRooms.Count;

            if (activeRoomCount > excursionInitialRoomCount && eligibleRooms.Count >= 2 && ghost_State != GHOST_STATE.CHASE)
            {
                isDoingOffRoomExcursion = false;
                ChooseNextPatrolRoom(false);
            }
            return;
        }

        bool currentValid = currentAssignedRoom != null &&
                            eligibleRooms.Any(r => r.roomObject == currentAssignedRoom);

        if (!currentValid && ghost_State != GHOST_STATE.CHASE)
        {
            ChooseNextPatrolRoom(currentAssignedRoom == null);
        }
    }

    // ==========================================
    // PATROL LOGIC (STEP 3)
    // ==========================================
    private void Patrol()
    {
        if ((currentAssignedRoom == null && !isDoingOffRoomExcursion) || currentRoomPatrolPoints == null || currentRoomPatrolPoints.Count == 0)
        {
            ChooseNextPatrolRoom(currentAssignedRoom == null);
            if (currentRoomPatrolPoints == null || currentRoomPatrolPoints.Count == 0) return;
        }

        // If currently waiting in idle delay at a patrol spot
        if (isWaitingAtPatrolSpot)
        {
            patrolSpotIdleTimer -= Time.deltaTime;
            if (patrolSpotIdleTimer > 0f)
            {
                return; // Remain idle for 2 seconds!
            }

            // 2-second idle delay finished -> continue patrol!
            isWaitingAtPatrolSpot = false;
            patrolSpotIdleTimer = 0f;

            if (isKillingPlayer)
            {
                PickRandomPatrolSpot();
                return;
            }

            patrolPointsRemainingInCycle--;

            if (patrolPointsRemainingInCycle > 0)
            {
                PickRandomPatrolSpot();
            }
            else if (isDoingOffRoomExcursion)
            {
                // Finished temporary excursion outside single room -> loop back into Step 1 (Choose Room)
                ChooseNextPatrolRoom(false);
            }
            else
            {
                // STEP 4: Finished patrolling -> Now finally sniff the locker(s) chosen back in Step 2!
                StartSniffChosenLockersPhase();
            }
            return;
        }

        if (currentPatrolSpot < 0 || currentPatrolSpot >= currentRoomPatrolPoints.Count)
        {
            PickRandomPatrolSpot();
            if (currentPatrolSpot < 0 || currentPatrolSpot >= currentRoomPatrolPoints.Count) return;
        }

        // Keep patrol point synced with Transform position if backed by real PartrolSpot transforms
        if (!isDoingOffRoomExcursion && PartrolSpot != null && currentPatrolSpot < PartrolSpot.Length && PartrolSpot[currentPatrolSpot] != null)
        {
            currentRoomPatrolPoints[currentPatrolSpot] = PartrolSpot[currentPatrolSpot].position;
        }

        Vector3 targetPos = currentRoomPatrolPoints[currentPatrolSpot];

        // Face towards patrol spot
        float diffX = targetPos.x - transform.position.x;
        if (Mathf.Abs(diffX) > 0.05f)
        {
            SetFacing(Mathf.Sign(diffX));
        }

        // Move horizontally and smoothly align Y towards patrol spot (WALK, NO TELEPORT)
        float newX = Mathf.MoveTowards(transform.position.x, targetPos.x, moveSpeed * Time.deltaTime);
        float newY = Mathf.MoveTowards(transform.position.y, targetPos.y, moveSpeed * Time.deltaTime);
        transform.position = new Vector3(newX, newY, transform.position.z);

        // Check if reached current patrol spot horizontally
        if (Mathf.Abs(transform.position.x - targetPos.x) <= reachPointDistance)
        {
            TriggerPatrolSpotReached();
        }
    }

    private void TriggerPatrolSpotReached()
    {
        if (isWaitingAtPatrolSpot) return;

        isWaitingAtPatrolSpot = true;
        patrolSpotIdleTimer = Mathf.Max(0.01f, patrolSpotIdleDelay);
        Debug.Log($"[SecondEventGhost] Touched patrol spot #{currentPatrolSpot} in '{(currentAssignedRoom != null ? currentAssignedRoom.name : "room")}'. Idling for {patrolSpotIdleTimer:F1}s before continuing patrol.");
    }

    private void PickRandomPatrolSpot()
    {
        if (currentRoomPatrolPoints == null || currentRoomPatrolPoints.Count == 0) return;

        if (currentRoomPatrolPoints.Count == 1)
        {
            currentPatrolSpot = 0;
            return;
        }

        List<int> candidates = new List<int>();
        for (int i = 0; i < currentRoomPatrolPoints.Count; i++)
        {
            if (i != currentPatrolSpot && Mathf.Abs(currentRoomPatrolPoints[i].x - transform.position.x) > reachPointDistance * 1.5f)
            {
                candidates.Add(i);
            }
        }

        if (candidates.Count > 0)
        {
            currentPatrolSpot = candidates[Random.Range(0, candidates.Count)];
            return;
        }

        int newIndex = Random.Range(0, currentRoomPatrolPoints.Count);
        if (newIndex == currentPatrolSpot)
        {
            newIndex = (newIndex + 1) % currentRoomPatrolPoints.Count;
        }
        currentPatrolSpot = newIndex;
    }

    private GameObject GetRemainingQueuedLocker()
    {
        if (lockerToSniff == null || lockerToSniff.Count == 0) return null;

        lockerToSniff.RemoveAll(l => l == null);

        for (int i = 0; i < lockerToSniff.Count; i++)
        {
            GameObject locker = lockerToSniff[i];
            if (locker != null && !sniffedLockers.Contains(locker))
            {
                return locker;
            }
        }

        return null;
    }

    private float GetPatrolHeightY()
    {
        if (PartrolSpot != null && PartrolSpot.Length > 0)
        {
            Transform bestSpot = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < PartrolSpot.Length; i++)
            {
                if (PartrolSpot[i] == null) continue;
                float d = Mathf.Abs(PartrolSpot[i].position.x - transform.position.x);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestSpot = PartrolSpot[i];
                }
            }

            if (bestSpot != null)
            {
                cachedReferencePatrolY = bestSpot.position.y;
                hasReferencePatrolY = true;
                return bestSpot.position.y;
            }
        }

        if (currentRoomPatrolPoints != null && currentRoomPatrolPoints.Count > 0)
        {
            return currentRoomPatrolPoints[0].y;
        }

        return GetReferencePatrolY();
    }

    // ==========================================
    // SNIFF LOCKER LOGIC (STEP 4: SNIFF PRE-CHOSEN LOCKER(S))
    // ==========================================
    private void Sniff()
    {
        currentWorkflowStep = WORKFLOW_STEP.SNIFF_CHOSEN_LOCKER;

        // If already performing the sniffing coroutine or rushing, stay in routine
        if (isSniffing || isRushingToKill) return;

        if (currentAssignedRoom == null)
        {
            ChooseNextPatrolRoom(false);
            return;
        }

        // If target locker was destroyed or missing, check next pre-chosen locker in queue or loop to Step 1 (Choose Room)
        if (currentLockerTarget == null)
        {
            currentLockerTarget = GetRemainingQueuedLocker();
            if (currentLockerTarget == null)
            {
                ChooseNextPatrolRoom(false);
                return;
            }
        }

        // Face towards the locker while walking to it
        float diffX = currentLockerTarget.transform.position.x - transform.position.x;
        if (Mathf.Abs(diffX) > 0.05f)
        {
            SetFacing(Mathf.Sign(diffX));
        }

        // Move horizontally directly towards the pre-chosen locker (ignoring other lockers passed along the way)
        // while keeping Y smoothly aligned with the room's patrol height
        float targetY = GetPatrolHeightY();
        float newX = Mathf.MoveTowards(transform.position.x, currentLockerTarget.transform.position.x, moveSpeed * Time.deltaTime);
        float newY = Mathf.MoveTowards(transform.position.y, targetY, moveSpeed * Time.deltaTime);
        transform.position = new Vector3(newX, newY, transform.position.z);

        // Check if reached target locker horizontally
        if (Mathf.Abs(transform.position.x - currentLockerTarget.transform.position.x) <= reachPointDistance)
        {
            sniffingCoroutine = StartCoroutine(SniffLockerRoutine(currentLockerTarget));
        }
    }

    private IEnumerator SniffLockerRoutine(GameObject locker)
    {
        isSniffing = true;
        currentWorkflowStep = WORKFLOW_STEP.SNIFF_CHOSEN_LOCKER;

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
            // If interrupted by chase or player death, cancel sniff
            if (ghost_State == GHOST_STATE.CHASE || isKillingPlayer)
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
            if (lockerScript == null) lockerScript = locker.GetComponentInParent<Locker>();
            if (lockerScript == null) lockerScript = locker.GetComponentInChildren<Locker>();
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
            // 80% chance to kill the player when sniffing the locker the player is inside!
            int roll = Random.Range(0, 100);
            bool shouldKill = (roll < lockerKillChancePercentage);
            Debug.Log($"[SecondEventGhost] Player is inside sniffed locker '{(locker != null ? locker.name : "null")}'! Roll: {roll} < {lockerKillChancePercentage}% -> {(shouldKill ? "KILL" : "SPARE")}");

            if (shouldKill)
            {
                isSniffing = false;
                sniffingCoroutine = null;
                yield return StartCoroutine(RushAndKillPlayer(locker));
                yield break;
            }
            else
            {
                Debug.Log($"[SecondEventGhost] Player was inside sniffed locker '{(locker != null ? locker.name : "null")}', but 80% kill roll failed (20% spare chance)! Ghost spares player.");
            }
        }

        // Player was not inside (or was spared by the 20% roll) -> Mark this locker as sniffed
        if (locker != null)
        {
            sniffedLockers.Add(locker);
            lastSniffedLocker = locker;
        }

        isSniffing = false;
        sniffingCoroutine = null;
        currentLockerTarget = null;

        if (isKillingPlayer)
        {
            ghost_State = GHOST_STATE.PATROL;
            currentWorkflowStep = WORKFLOW_STEP.PATROL;
            PickRandomPatrolSpot();
            yield break;
        }

        // Advance to the next pre-chosen locker in this room's queue, OR loop back to Step 1 (Choose Room)!
        currentLockerTarget = GetRemainingQueuedLocker();
        if (currentLockerTarget != null)
        {
            currentWorkflowStep = WORKFLOW_STEP.SNIFF_CHOSEN_LOCKER;
            ghost_State = GHOST_STATE.SNIFF;
        }
        else
        {
            Debug.Log($"[SecondEventGhost] Finished sniffing {sniffedLockers.Count} pre-chosen locker(s) in '{(currentAssignedRoom != null ? currentAssignedRoom.name : "room")}'. Looping workflow -> Choose next room!");
            ChooseNextPatrolRoom(false);
        }
    }

    private IEnumerator RushAndKillPlayer(GameObject lockerObj)
    {
        isRushingToKill = true;
        ghost_State = GHOST_STATE.CHASE;
        currentWorkflowStep = WORKFLOW_STEP.CHASE_INTERRUPT;

        Debug.Log("[SecondEventGhost] Sniffing time is up and player is inside locker! (80% kill roll succeeded) -> Interacting with locker and rushing to kill!");

        // 1. Call animation of that locker ("Locker Interacted")
        if (lockerObj != null)
        {
            Locker lockerScript = lockerObj.GetComponent<Locker>();
            if (lockerScript == null) lockerScript = lockerObj.GetComponentInParent<Locker>();
            if (lockerScript == null) lockerScript = lockerObj.GetComponentInChildren<Locker>();

            if (lockerScript != null && lockerScript.animator != null)
            {
                lockerScript.animator.Play("Locker Interacted");
            }
            else
            {
                Animator anim = lockerObj.GetComponent<Animator>();
                if (anim == null) anim = lockerObj.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.Play("Locker Interacted");
                }
            }
        }

        // 2. Trigger Hided.cs to make hided false
        if (Hide == null)
        {
            Hide = FindObjectOfType<Hided>();
        }
        if (Hide != null)
        {
            Hide.IsPlayerHide = false;
        }

        if (audioSource != null && attackSound != null)
        {
            audioSource.PlayOneShot(attackSound);
        }

        Vector3 targetPos = (lockerObj != null) ? lockerObj.transform.position : transform.position;
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

        KillPlayer("Player caught hiding inside the sniffed locker (80% chance succeeded)!");
    }

    // ==========================================
    // CHASE LOGIC
    // ==========================================
    private void StartChase()
    {
        isWaitingAtPatrolSpot = false;
        patrolSpotIdleTimer = 0f;

        if (sniffingCoroutine != null)
        {
            StopCoroutine(sniffingCoroutine);
            sniffingCoroutine = null;
            isSniffing = false;
        }
        currentLockerTarget = null;

        bool alreadyChasing = (ghost_State == GHOST_STATE.CHASE);
        ghost_State = GHOST_STATE.CHASE;
        currentWorkflowStep = WORKFLOW_STEP.CHASE_INTERRUPT;

        if (!alreadyChasing && audioSource != null && detectedSound != null)
        {
            audioSource.PlayOneShot(detectedSound);
        }
        if (ChaseTheme != null)
        {
            PlayChaseTheme();
        }
    }

    public void PlayChaseTheme()
    {
        if (ChaseTheme == null) return;
        isFadingOutChaseTheme = false;
        if (chaseThemeFadeCoroutine != null)
        {
            StopCoroutine(chaseThemeFadeCoroutine);
        }
        chaseThemeFadeCoroutine = StartCoroutine(ChaseThemeFadeIn());
    }

    public void StopChaseTheme()
    {
        if (ChaseTheme == null) return;
        if (isFadingOutChaseTheme) return;
        isFadingOutChaseTheme = true;
        if (chaseThemeFadeCoroutine != null)
        {
            StopCoroutine(chaseThemeFadeCoroutine);
        }
        chaseThemeFadeCoroutine = StartCoroutine(ChaseThemeFadeOut());
    }

    IEnumerator ChaseThemeFadeOut()
    {
        if (ChaseTheme == null) yield break;

        while (ChaseTheme != null && ChaseTheme.volume > 0f)
        {
            ChaseTheme.volume = Mathf.MoveTowards(ChaseTheme.volume, 0f, chaseThemeFadeSpeed * Time.deltaTime);
            yield return null;
        }

        if (ChaseTheme != null)
        {
            ChaseTheme.volume = 0f;
            ChaseTheme.Stop();
        }
        isFadingOutChaseTheme = false;
        chaseThemeFadeCoroutine = null;
    }

    IEnumerator ChaseThemeFadeIn()
    {
        if (ChaseTheme == null) yield break;

        if (!ChaseTheme.isPlaying)
        {
            ChaseTheme.Play();
        }

        while (ChaseTheme != null && ChaseTheme.volume < chaseThemeTargetVolume)
        {
            ChaseTheme.volume = Mathf.MoveTowards(ChaseTheme.volume, chaseThemeTargetVolume, chaseThemeFadeSpeed * Time.deltaTime);
            yield return null;
        }

        if (ChaseTheme != null)
        {
            ChaseTheme.volume = chaseThemeTargetVolume;
        }
        chaseThemeFadeCoroutine = null;
    }

    private void Chase()
    {
        if (isRushingToKill) return;
        currentWorkflowStep = WORKFLOW_STEP.CHASE_INTERRUPT;

        // If player hides during chase (including door-open chase in rooms 45 - 48),
        // stop chasing and run the room workflow (choose room -> choose locker -> patrol -> sniff chosen locker -> loop) in that room!
        if (Hide != null && Hide.IsPlayerHide)
        {
            isChasingFromDoorOpen = false;
            isChasingFromNoise = false;
            noiseInvestigateTimer = 0f;
            doorChaseGraceTimer = 0f;
            StopChaseTheme();
            StartPatrolRoutineInPlayerRoom();
            return;
        }

        if (!HasValidPlayerPosition())
        {
            FindPlayerReferencesIfNeeded();
            if (!HasValidPlayerPosition())
            {
                isChasingFromDoorOpen = false;
                isChasingFromNoise = false;
                noiseInvestigateTimer = 0f;
                StopChaseTheme();
                ChooseNextPatrolRoom(false);
                return;
            }
        }

        Vector3 actualPlayerPos = GetPlayerPosition();

        // Check if player is directly seen inside the front detection box
        bool seesPlayer = CheckPlayerInDetectionRange();
        bool stillMakingNoise = false;

        if (seesPlayer && !isChasingFromDoorOpen)
        {
            isChasingFromNoise = false; // Visually spotted inside detection range -> full chase
            lastSeenPlayerPosition = actualPlayerPos;
            lostSightTimer = noiseInvestigateDuration;
        }
        else if (isChasingFromNoise && !isChasingFromDoorOpen)
        {
            // Chasing from footsteps while player is OUTSIDE visual detection range
            stillMakingNoise = CheckPlayerMovementNoise();
            if (stillMakingNoise)
            {
                noiseInvestigateTimer = noiseInvestigateDuration;
                lastHeardNoisePosition = actualPlayerPos;
            }
            else
            {
                noiseInvestigateTimer -= Time.deltaTime;
                bool reachedLastNoiseSpot = Mathf.Abs(transform.position.x - lastHeardNoisePosition.x) <= reachPointDistance;

                if (noiseInvestigateTimer <= 0f || reachedLastNoiseSpot)
                {
                    // Player stopped walking or started crawling outside detection range -> resume workflow!
                    isChasingFromNoise = false;
                    StopChaseTheme();
                    ResumeRoutineAfterLostNoise();
                    return;
                }
            }
        }
        else if (!isChasingFromDoorOpen)
        {
            // Player lost from front visual detection range
            stillMakingNoise = CheckPlayerMovementNoise();
            if (stillMakingNoise)
            {
                isChasingFromNoise = true;
                noiseInvestigateTimer = noiseInvestigateDuration;
                lastHeardNoisePosition = actualPlayerPos;
            }
            else
            {
                lostSightTimer -= Time.deltaTime;
                bool reachedLastSeenSpot = Mathf.Abs(transform.position.x - lastSeenPlayerPosition.x) <= reachPointDistance;

                if (lostSightTimer <= 0f || reachedLastSeenSpot)
                {
                    StopChaseTheme();
                    ResumeRoutineAfterLostNoise();
                    return;
                }
            }
        }

        // Only home onto live player position when visually seen, door-open chasing, or actively making loud footsteps;
        // if player switched to crawling outside detection range, only investigate lastHeardNoisePosition / lastSeenPlayerPosition!
        Vector3 targetDestination = actualPlayerPos;
        if (!isChasingFromDoorOpen && !seesPlayer && !stillMakingNoise)
        {
            targetDestination = isChasingFromNoise ? lastHeardNoisePosition : lastSeenPlayerPosition;
        }

        // Face target horizontally while chasing
        float diffX = targetDestination.x - transform.position.x;
        if (Mathf.Abs(diffX) > 0.05f)
        {
            SetFacing(Mathf.Sign(diffX));
        }

        // Move horizontally towards target at chase speed while keeping Y aligned to patrol height
        float chaseSpeed = moveSpeed * chaseSpeedMultiplier;
        float targetY = GetPatrolHeightY();
        float newX = Mathf.MoveTowards(transform.position.x, targetDestination.x, chaseSpeed * Time.deltaTime);
        float newY = Mathf.MoveTowards(transform.position.y, targetY, moveSpeed * Time.deltaTime);
        transform.position = new Vector3(newX, newY, transform.position.z);

        // Check kill distance against the actual player (never kill a player crawling outside detection range!)
        if (doorChaseGraceTimer <= 0f)
        {
            bool crawlingOutsideDetection = isChasingFromNoise && !isChasingFromDoorOpen && !seesPlayer && IsPlayerCrawling();
            if (!crawlingOutsideDetection)
            {
                float dist = Vector2.Distance(transform.position, actualPlayerPos);
                if (dist <= killDistance || Mathf.Abs(transform.position.x - actualPlayerPos.x) <= (killDistance * 0.7f))
                {
                    KillPlayer("Player caught by ghost during chase!");
                }
            }
        }
    }

    private void StartPatrolRoutineInPlayerRoom()
    {
        List<EventRoomData> eligibleRooms = GetEligibleRooms();
        if (eligibleRooms == null || eligibleRooms.Count == 0)
        {
            ghost_State = GHOST_STATE.PATROL;
            currentWorkflowStep = WORKFLOW_STEP.PATROL;
            return;
        }

        GameObject targetRoomObj = null;

        // 1. Check exact locker the player is currently hiding inside
        Locker[] allLockers = FindObjectsOfType<Locker>();
        if (allLockers != null)
        {
            for (int i = 0; i < allLockers.Length; i++)
            {
                if (allLockers[i] != null && allLockers[i].IsPlayerInsideThisLocker)
                {
                    targetRoomObj = GetRoomForObject(allLockers[i].gameObject);
                    break;
                }
            }
        }

        // 2. Fallback to physical room containing the player
        if (targetRoomObj == null && HasValidPlayerPosition())
        {
            targetRoomObj = GetPhysicalRoomAtPosition(GetPlayerPosition());
        }

        EventRoomData match = targetRoomObj != null
            ? eligibleRooms.FirstOrDefault(r => r.roomObject == targetRoomObj)
            : null;

        if (match == null)
        {
            float refX = HasValidPlayerPosition() ? GetPlayerPosition().x : transform.position.x;
            match = eligibleRooms.OrderBy(r => r.DistanceToX(refX)).FirstOrDefault();
        }

        if (match != null)
        {
            Debug.Log($"[SecondEventGhost] Player hid during chase -> starting room workflow (choose locker -> patrol -> sniff) in '{match.roomObject.name}'!");
            ghost_State = GHOST_STATE.PATROL;
            StartRoomRoutine(match);
        }
        else
        {
            ghost_State = GHOST_STATE.PATROL;
            ChooseNextPatrolRoom(false);
        }
    }

    private void ResumeRoutineAfterLostNoise()
    {
        List<EventRoomData> eligibleRooms = GetEligibleRooms();
        bool assignedStillValid = currentAssignedRoom != null &&
                                  eligibleRooms != null &&
                                  eligibleRooms.Any(r => r.roomObject == currentAssignedRoom);

        // Continue existing room progress rather than resetting from scratch (preserving pre-chosen lockers!)
        if (assignedStillValid)
        {
            // If Step 3 (Patrol) still has points left, finish patrolling first before sniffing the pre-chosen lockers
            if (patrolPointsRemainingInCycle > 0)
            {
                currentWorkflowStep = WORKFLOW_STEP.PATROL;
                ghost_State = GHOST_STATE.PATROL;
                PickRandomPatrolSpot();
                return;
            }

            // If Step 3 (Patrol) already finished, finish Step 4 (Sniff pre-chosen lockers)
            GameObject nextQueued = GetRemainingQueuedLocker();
            if (nextQueued != null)
            {
                currentLockerTarget = nextQueued;
                currentWorkflowStep = WORKFLOW_STEP.SNIFF_CHOSEN_LOCKER;
                ghost_State = GHOST_STATE.SNIFF;
                return;
            }
        }

        ghost_State = GHOST_STATE.PATROL;
        ChooseNextPatrolRoom(false);
    }

    private void Idle()
    {
        currentWorkflowStep = WORKFLOW_STEP.WAITING_FOR_EVENT;
        // Do nothing while idle waiting for room 45
    }

    public bool IsInLastRoom()
    {
        if (RoomCounter.instance != null)
        {
            return RoomCounter.instance.PassedRoomCount == eventEndRoom;
        }
        return false;
    }

    // ==========================================
    // DETECTION & FLIPPING HELPERS
    // ==========================================
    private bool HasEnabledNonTriggerCollider(GameObject go)
    {
        if (go == null) return false;
        Collider2D[] cols = go.GetComponents<Collider2D>();
        if (cols == null) return false;

        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && cols[i].enabled && !cols[i].isTrigger)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsPlayerBodyCollider(Collider2D hit)
    {
        if (hit == null || !hit.enabled) return false;

        bool isDirectPlayerObject =
            hit.CompareTag("Player") ||
            (playerMovement != null && hit.gameObject == playerMovement.gameObject) ||
            (player_Target != null && hit.gameObject == player_Target.gameObject);

        if (!isDirectPlayerObject)
        {
            return false;
        }

        // Ignore auxiliary trigger colliders on the Player if the Player has an active solid body collider
        if (hit.isTrigger && HasEnabledNonTriggerCollider(hit.gameObject))
        {
            return false;
        }

        return true;
    }

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
            if (IsPlayerBodyCollider(hit))
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

    private bool IsPlayerCrawling()
    {
        if (playerMovement != null && playerMovement.isCrawling)
        {
            return true;
        }

        if (playerMovement == null)
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.C))
            {
                return true;
            }
        }

        return false;
    }

    private bool CheckPlayerMovementNoise()
    {
        if (lastNoiseCheckFrame == Time.frameCount)
        {
            return cachedNoiseCheckResult;
        }

        lastNoiseCheckFrame = Time.frameCount;
        cachedNoiseCheckResult = EvaluatePlayerMovementNoiseInternal();
        return cachedNoiseCheckResult;
    }

    private bool EvaluatePlayerMovementNoiseInternal()
    {
        if (!detectPlayerMovement || isKillingPlayer)
        {
            loudMovementConfirmTimer = 0f;
            return false;
        }

        // Hidden in locker = silent
        if (Hide != null && Hide.IsPlayerHide)
        {
            loudMovementConfirmTimer = 0f;
            return false;
        }

        FindPlayerReferencesIfNeeded();
        if (!HasValidPlayerPosition())
        {
            loudMovementConfirmTimer = 0f;
            return false;
        }

        if (playerMovement != null && !playerMovement.enabled)
        {
            loudMovementConfirmTimer = 0f;
            return false;
        }

        // Crawling is unconditionally silent (cannot die by crawling when outside detection range)
        if (IsPlayerCrawling())
        {
            loudMovementConfirmTimer = 0f;
            return false;
        }

        bool isLoudMoving;
        if (playerMovement != null)
        {
            bool hasHorizontalInput = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.05f;
            bool hasHorizontalVelocity = playerRb == null || Mathf.Abs(playerRb.linearVelocity.x) > 0.1f;
            isLoudMoving = (playerMovement.isMoving && (hasHorizontalInput || hasHorizontalVelocity)) ||
                           (playerMovement.isDashing && hasHorizontalVelocity);
        }
        else
        {
            bool hasHorizontalInput = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.05f;
            bool hasHorizontalVelocity = playerRb == null || Mathf.Abs(playerRb.linearVelocity.x) > 0.15f;
            isLoudMoving = hasHorizontalInput && hasHorizontalVelocity;
        }

        if (!isLoudMoving)
        {
            loudMovementConfirmTimer = 0f;
            return false;
        }

        // Confirm sustained non-crawling movement (~5 frames) to prevent 1-frame script execution order races when pressing Crawl + Move
        loudMovementConfirmTimer += Time.deltaTime;
        if (loudMovementConfirmTimer < 0.08f)
        {
            return false;
        }

        lastHeardNoisePosition = GetPlayerPosition();
        return true;
    }

    // ==========================================
    // ROOM BOUNDS, PHYSICAL PARTITIONING & ELIGIBLE ROOMS (45 - 48)
    // ==========================================
    private bool IsStairRoom(GameObject room)
    {
        if (room == null) return false;
        return room.GetComponentInChildren<StairColliderLogic>() != null ||
               room.name.ToLower().Contains("stair");
    }

    private CachedRoomBounds GetOrComputeRoomBounds(GameObject room)
    {
        if (room == null) return null;

        if (roomBoundsCache.TryGetValue(room, out CachedRoomBounds cached) && cached != null)
        {
            return cached;
        }

        // Prune destroyed entries occasionally
        if (roomBoundsCache.Count > 16)
        {
            List<GameObject> deadKeys = roomBoundsCache.Keys.Where(k => k == null).ToList();
            for (int i = 0; i < deadKeys.Count; i++)
            {
                roomBoundsCache.Remove(deadKeys[i]);
            }
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        Renderer[] renderers = room.GetComponentsInChildren<Renderer>(true);
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled) continue;
                Bounds b = r.bounds;
                if (b.size.sqrMagnitude < 0.001f) continue;
                if (b.min.x < minX) minX = b.min.x;
                if (b.max.x > maxX) maxX = b.max.x;
                if (b.min.y < minY) minY = b.min.y;
                if (b.max.y > maxY) maxY = b.max.y;
            }
        }

        Collider2D[] colliders = room.GetComponentsInChildren<Collider2D>(true);
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D c = colliders[i];
                if (c == null || !c.enabled) continue;
                Bounds b = c.bounds;
                if (b.size.sqrMagnitude < 0.001f) continue;
                if (b.min.x < minX) minX = b.min.x;
                if (b.max.x > maxX) maxX = b.max.x;
                if (b.min.y < minY) minY = b.min.y;
                if (b.max.y > maxY) maxY = b.max.y;
            }
        }

        if (minX == float.MaxValue || maxX == float.MinValue)
        {
            minX = room.transform.position.x - 8f;
            maxX = room.transform.position.x + 8f;
        }
        if (minY == float.MaxValue || maxY == float.MinValue)
        {
            minY = room.transform.position.y - 5f;
            maxY = room.transform.position.y + 5f;
        }

        CachedRoomBounds result = new CachedRoomBounds
        {
            rawMinX = minX,
            rawMaxX = maxX,
            rawMinY = minY,
            rawMaxY = maxY
        };

        Door[] doors = room.GetComponentsInChildren<Door>(true);
        if (doors != null)
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null && !doors[i].IsStairDoor)
                {
                    float doorX = doors[i].doorCollider != null
                        ? doors[i].doorCollider.bounds.center.x
                        : doors[i].transform.position.x;
                    result.doors.Add(doors[i]);
                    result.doorXs.Add(doorX);
                }
            }
        }

        roomBoundsCache[room] = result;
        return result;
    }

    private struct PartitionedRoomSpan
    {
        public GameObject room;
        public float leftX;
        public float rightX;
    }

    private List<PartitionedRoomSpan> BuildPartitionedActiveRooms(float? filterRefY = null)
    {
        List<PartitionedRoomSpan> spans = new List<PartitionedRoomSpan>();
        if (RoomCounter.instance == null || RoomCounter.instance.currentRooms == null)
        {
            return spans;
        }

        List<GameObject> activeRooms = new List<GameObject>();
        for (int i = 0; i < RoomCounter.instance.currentRooms.Count; i++)
        {
            GameObject r = RoomCounter.instance.currentRooms[i];
            if (r == null || activeRooms.Contains(r)) continue;

            if (filterRefY.HasValue)
            {
                CachedRoomBounds rb = GetOrComputeRoomBounds(r);
                if (rb != null && (filterRefY.Value < rb.rawMinY - 2.0f || filterRefY.Value > rb.rawMaxY + 2.5f))
                {
                    continue;
                }
            }

            activeRooms.Add(r);
        }

        if (activeRooms.Count == 0 && filterRefY.HasValue)
        {
            for (int i = 0; i < RoomCounter.instance.currentRooms.Count; i++)
            {
                GameObject r = RoomCounter.instance.currentRooms[i];
                if (r != null && !activeRooms.Contains(r))
                {
                    activeRooms.Add(r);
                }
            }
        }

        if (activeRooms.Count == 0) return spans;

        activeRooms.Sort((a, b) =>
        {
            CachedRoomBounds ba = GetOrComputeRoomBounds(a);
            CachedRoomBounds bb = GetOrComputeRoomBounds(b);
            return ba.centerX.CompareTo(bb.centerX);
        });

        float[] splits = new float[Mathf.Max(0, activeRooms.Count - 1)];
        for (int i = 0; i < activeRooms.Count - 1; i++)
        {
            CachedRoomBounds leftB = GetOrComputeRoomBounds(activeRooms[i]);
            CachedRoomBounds rightB = GetOrComputeRoomBounds(activeRooms[i + 1]);
            float seamMid = 0.5f * (leftB.rawMaxX + rightB.rawMinX);

            // Prefer exact connecting Door X between leftRoom.centerX and rightRoom.centerX
            float bestDoorX = float.NaN;
            float bestDoorDist = float.MaxValue;

            for (int d = 0; d < leftB.doorXs.Count; d++)
            {
                float dx = leftB.doorXs[d];
                if (dx >= leftB.centerX && dx <= rightB.centerX)
                {
                    float dist = Mathf.Abs(dx - seamMid);
                    if (dist < bestDoorDist)
                    {
                        bestDoorDist = dist;
                        bestDoorX = dx;
                    }
                }
            }
            for (int d = 0; d < rightB.doorXs.Count; d++)
            {
                float dx = rightB.doorXs[d];
                if (dx >= leftB.centerX && dx <= rightB.centerX)
                {
                    float dist = Mathf.Abs(dx - seamMid);
                    if (dist < bestDoorDist)
                    {
                        bestDoorDist = dist;
                        bestDoorX = dx;
                    }
                }
            }

            splits[i] = !float.IsNaN(bestDoorX) ? bestDoorX : seamMid;
        }

        for (int i = 0; i < activeRooms.Count; i++)
        {
            CachedRoomBounds b = GetOrComputeRoomBounds(activeRooms[i]);
            float leftX = (i == 0) ? (b.rawMinX - 0.5f) : splits[i - 1];
            float rightX = (i == activeRooms.Count - 1) ? (b.rawMaxX + 0.5f) : splits[i];
            if (rightX < leftX)
            {
                float mid = 0.5f * (leftX + rightX);
                leftX = mid - 0.5f;
                rightX = mid + 0.5f;
            }

            spans.Add(new PartitionedRoomSpan
            {
                room = activeRooms[i],
                leftX = leftX,
                rightX = rightX
            });
        }

        return spans;
    }

    private GameObject GetPhysicalRoomAtPosition(Vector3 pos)
    {
        List<PartitionedRoomSpan> spans = BuildPartitionedActiveRooms(pos.y);
        if (spans.Count > 0)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                if (pos.x >= spans[i].leftX && pos.x <= spans[i].rightX)
                {
                    return spans[i].room;
                }
            }

            return null;
        }

        if (isDoingOffRoomExcursion)
        {
            return null;
        }

        return currentAssignedRoom;
    }

    private float GetReferencePatrolY()
    {
        GameObject[] spots = GameObject.FindGameObjectsWithTag("PartrolSpot");
        if (spots != null && spots.Length > 0)
        {
            Transform best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < spots.Length; i++)
            {
                if (spots[i] == null) continue;
                float d = Mathf.Abs(spots[i].transform.position.x - transform.position.x);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = spots[i].transform;
                }
            }

            if (best != null)
            {
                cachedReferencePatrolY = best.position.y;
                hasReferencePatrolY = true;
                return best.position.y;
            }
        }

        if (hasReferencePatrolY)
        {
            return cachedReferencePatrolY;
        }

        return transform.position.y;
    }

    private List<EventRoomData> GetEligibleRooms()
    {
        List<EventRoomData> result = new List<EventRoomData>();

        if (RoomCounter.instance != null && RoomCounter.instance.currentRooms != null && RoomCounter.instance.currentRooms.Count > 0)
        {
            float refY = GetReferencePatrolY();
            List<PartitionedRoomSpan> spans = BuildPartitionedActiveRooms(refY);
            Dictionary<GameObject, PartitionedRoomSpan> spanMap = new Dictionary<GameObject, PartitionedRoomSpan>();
            for (int i = 0; i < spans.Count; i++)
            {
                spanMap[spans[i].room] = spans[i];
            }

            List<GameObject> candidateRooms = new List<GameObject>();
            List<GameObject> activeRooms = RoomCounter.instance.currentRooms;

            // Collect all spawned Second Event rooms (45 - 48)
            for (int i = 0; i < activeRooms.Count; i++)
            {
                GameObject r = activeRooms[i];
                if (r == null || IsStairRoom(r)) continue;

                if (IsEventRangeRoom(r) && !candidateRooms.Contains(r))
                {
                    candidateRooms.Add(r);
                }
            }

            for (int i = 0; i < candidateRooms.Count; i++)
            {
                GameObject room = candidateRooms[i];
                EventRoomData data = BuildEventRoomData(room, spanMap, refY);
                if (data != null && data.IsValid)
                {
                    result.Add(data);
                }
            }

            return result;
        }

        // Standalone scene fallback when RoomCounter is absent
        GameObject[] spots = GameObject.FindGameObjectsWithTag("PartrolSpot");
        Dictionary<GameObject, List<Transform>> fallbackMap = new Dictionary<GameObject, List<Transform>>();
        if (spots != null)
        {
            foreach (GameObject spot in spots)
            {
                if (spot == null) continue;
                GameObject room = GetRoomForObject(spot);
                if (room == null) continue;
                if (!fallbackMap.TryGetValue(room, out List<Transform> list))
                {
                    list = new List<Transform>();
                    fallbackMap[room] = list;
                }
                list.Add(spot.transform);
            }
        }

        float fallbackRefY = GetReferencePatrolY();
        foreach (var kvp in fallbackMap)
        {
            EventRoomData data = BuildEventRoomData(kvp.Key, null, fallbackRefY);
            if (data != null && data.IsValid)
            {
                result.Add(data);
            }
        }

        return result;
    }

    private EventRoomData BuildEventRoomData(
        GameObject room,
        Dictionary<GameObject, PartitionedRoomSpan> spanMap,
        float referencePatrolY)
    {
        if (room == null) return null;

        CachedRoomBounds rawBounds = GetOrComputeRoomBounds(room);
        float leftX = rawBounds.rawMinX;
        float rightX = rawBounds.rawMaxX;

        if (spanMap != null && spanMap.TryGetValue(room, out PartitionedRoomSpan span))
        {
            leftX = span.leftX;
            rightX = span.rightX;
        }

        EventRoomData data = new EventRoomData
        {
            roomObject = room,
            roomNumber = RoomCounter.instance != null ? RoomCounter.instance.GetRoomNumber(room) : -1,
            isSecondEventRoom = IsEventRangeRoom(room),
            minX = leftX,
            maxX = rightX
        };

        // 1. Discover real PartrolSpot transforms inside this room
        Transform[] allTransforms = room.GetComponentsInChildren<Transform>(false);
        if (allTransforms != null)
        {
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t != null && t.CompareTag("PartrolSpot"))
                {
                    data.patrolSpots.Add(t);
                }
            }
        }
        data.patrolSpots = data.patrolSpots.OrderBy(t => t.name).ToList();

        if (data.patrolSpots.Count > 0)
        {
            for (int i = 0; i < data.patrolSpots.Count; i++)
            {
                data.patrolPoints.Add(data.patrolSpots[i].position);
            }
        }
        else
        {
            // Synthesize 2 interior patrol points inside room bounds (e.g. for normal Room 44)
            float margin = Mathf.Min(2.5f, Mathf.Max(0.5f, (rightX - leftX) * 0.25f));
            float pLeft = leftX + margin;
            float pRight = rightX - margin;
            if (pLeft > pRight)
            {
                float mid = 0.5f * (leftX + rightX);
                pLeft = mid;
                pRight = mid;
            }

            data.patrolPoints.Add(new Vector3(pLeft, referencePatrolY, transform.position.z));
            if (Mathf.Abs(pRight - pLeft) > 0.2f)
            {
                data.patrolPoints.Add(new Vector3(pRight, referencePatrolY, transform.position.z));
            }
        }

        // 2. Discover lockers strictly inside this room
        Locker[] childLockers = room.GetComponentsInChildren<Locker>(false);
        if (childLockers != null)
        {
            for (int i = 0; i < childLockers.Length; i++)
            {
                if (childLockers[i] != null && !data.lockers.Contains(childLockers[i].gameObject))
                {
                    data.lockers.Add(childLockers[i].gameObject);
                }
            }
        }

        if (data.lockers.Count == 0)
        {
            GameObject[] taggedLockers = GameObject.FindGameObjectsWithTag("Locker");
            if (taggedLockers != null)
            {
                for (int i = 0; i < taggedLockers.Length; i++)
                {
                    GameObject l = taggedLockers[i];
                    if (l != null && GetRoomForObject(l) == room && !data.lockers.Contains(l))
                    {
                        data.lockers.Add(l);
                    }
                }
            }
        }

        return data;
    }

    private GameObject GetRoomForObject(GameObject obj)
    {
        if (obj == null) return null;

        if (RoomCounter.instance != null)
        {
            return RoomCounter.instance.GetRoomForTransform(obj.transform);
        }

        return obj.transform.root != null ? obj.transform.root.gameObject : obj;
    }

    // ==========================================
    // LOCKER SELECTION HELPERS (CHOOSE 2 RANDOM LOCKERS IN CURRENT ROOM)
    // ==========================================
    private void ChooseLockersToSniff(List<GameObject> roomLockersOverride = null)
    {
        sniffedLockers.Clear();
        lockerToSniff.Clear();

        List<GameObject> availableLockers = new List<GameObject>();

        if (roomLockersOverride != null && roomLockersOverride.Count > 0)
        {
            for (int i = 0; i < roomLockersOverride.Count; i++)
            {
                GameObject l = roomLockersOverride[i];
                if (l != null && !availableLockers.Contains(l))
                {
                    availableLockers.Add(l);
                }
            }
        }
        else if (currentAssignedRoom != null)
        {
            Locker[] roomLockers = currentAssignedRoom.GetComponentsInChildren<Locker>(false);
            if (roomLockers != null)
            {
                for (int i = 0; i < roomLockers.Length; i++)
                {
                    if (roomLockers[i] != null && !availableLockers.Contains(roomLockers[i].gameObject))
                    {
                        availableLockers.Add(roomLockers[i].gameObject);
                    }
                }
            }

            if (availableLockers.Count == 0)
            {
                GameObject[] taggedInScene = GameObject.FindGameObjectsWithTag("Locker");
                if (taggedInScene != null)
                {
                    for (int i = 0; i < taggedInScene.Length; i++)
                    {
                        GameObject l = taggedInScene[i];
                        if (l != null && GetRoomForObject(l) == currentAssignedRoom && !availableLockers.Contains(l))
                        {
                            availableLockers.Add(l);
                        }
                    }
                }
            }
        }

        if (availableLockers.Count == 0)
        {
            return;
        }

        List<GameObject> sortedByX = availableLockers.OrderBy(l => l.transform.position.x).ToList();
        List<GameObject> shuffled = new List<GameObject>(availableLockers);
        int targetCount = Mathf.Clamp(lockersToSniffPerCycle, 1, shuffled.Count);
        int maxAttempts = 12;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Fisher-Yates shuffle
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                GameObject temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            // Avoid immediately re-sniffing the exact same locker that just finished the previous cycle
            if (shuffled.Count >= 2 && lastSniffedLocker != null && shuffled[0] == lastSniffedLocker)
            {
                continue;
            }

            break;
        }

        for (int i = 0; i < targetCount; i++)
        {
            lockerToSniff.Add(shuffled[i]);
        }

        string orderStr = string.Join(" -> ", lockerToSniff.Select(l => $"#{sortedByX.IndexOf(l) + 1} ({l.name})"));
        Debug.Log($"[SecondEventGhost] Chosen {lockerToSniff.Count}/{availableLockers.Count} random locker(s) in '{(currentAssignedRoom != null ? currentAssignedRoom.name : "room")}': {orderStr}");
    }

    // ==========================================
    // DEATH & COLLISION HANDLING
    // ==========================================
    private void KillPlayer(string reason)
    {
        if (isKillingPlayer) return;
        isKillingPlayer = true;

        StopChaseTheme();

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
        isChasingFromDoorOpen = false;
        isChasingFromNoise = false;
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
            if (movement == null) movement = playerMovement;
            if (movement != null)
            {
                movement.KillPlayer(2f);
            }

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb == null) rb = playerRb;
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        if (Hide != null) Hide.enabled = false;

        yield return new WaitForSecondsRealtime(2f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision != null && collision.CompareTag("PartrolSpot") && ghost_State == GHOST_STATE.PATROL && !isWaitingAtPatrolSpot)
        {
            TriggerPatrolSpotReached();
        }
        CheckContactKillCollider(collision);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        CheckContactKillCollider(collision != null ? collision.collider : null);
    }

    private void CheckContactKillCollider(Collider2D col)
    {
        if (col == null || isKillingPlayer || ghost_State != GHOST_STATE.CHASE || doorChaseGraceTimer > 0f) return;
        if (Hide != null && Hide.IsPlayerHide) return;

        if (!IsPlayerBodyCollider(col)) return;

        // Never kill a crawling player who is outside the detection range during a noise investigation
        if (isChasingFromNoise && !isChasingFromDoorOpen && IsPlayerCrawling() && !CheckPlayerInDetectionRange())
        {
            return;
        }

        Vector3 actualPlayerPos = GetPlayerPosition();
        float dist = Vector2.Distance(transform.position, actualPlayerPos);
        if (dist <= killDistance || Mathf.Abs(transform.position.x - actualPlayerPos.x) <= (killDistance * 0.7f))
        {
            KillPlayer("Player touched ghost while chasing!");
        }
    }

    // ==========================================
    // UTILITIES
    // ==========================================
    private Transform GetActivePlayerTransform()
    {
        if (playerMovement != null) return playerMovement.transform;
        if (player_Target != null) return player_Target;
        return null;
    }

    private bool HasValidPlayerPosition()
    {
        return GetActivePlayerTransform() != null;
    }

    private Vector3 GetPlayerPosition()
    {
        Transform t = GetActivePlayerTransform();
        return t != null ? t.position : transform.position;
    }

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

        if (playerMovement == null)
        {
            if (player_Target != null)
            {
                playerMovement = player_Target.GetComponent<SideScrollMovement>();
                if (playerMovement == null) playerMovement = player_Target.GetComponentInParent<SideScrollMovement>();
                if (playerMovement == null) playerMovement = player_Target.GetComponentInChildren<SideScrollMovement>(true);
            }

            if (playerMovement == null)
            {
                playerMovement = FindObjectOfType<SideScrollMovement>();
                if (playerMovement == null)
                {
                    SideScrollMovement[] found = FindObjectsOfType<SideScrollMovement>(true);
                    if (found != null && found.Length > 0)
                    {
                        playerMovement = found[0];
                    }
                }
            }
        }

        if (player_Target == null && playerMovement != null)
        {
            player_Target = playerMovement.transform;
        }

        if (playerRb == null)
        {
            if (playerMovement != null)
            {
                playerRb = playerMovement.GetComponent<Rigidbody2D>();
                if (playerRb == null) playerRb = playerMovement.GetComponentInParent<Rigidbody2D>();
                if (playerRb == null) playerRb = playerMovement.GetComponentInChildren<Rigidbody2D>(true);
            }
            if (playerRb == null && player_Target != null)
            {
                playerRb = player_Target.GetComponent<Rigidbody2D>();
                if (playerRb == null) playerRb = player_Target.GetComponentInParent<Rigidbody2D>();
                if (playerRb == null) playerRb = player_Target.GetComponentInChildren<Rigidbody2D>(true);
            }
        }

        if (Hide == null)
        {
            Hide = FindObjectOfType<Hided>();
            if (Hide == null)
            {
                Hided[] found = FindObjectsOfType<Hided>(true);
                if (found != null && found.Length > 0)
                {
                    Hide = found[0];
                }
            }
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        bool isMoving = (ghost_State == GHOST_STATE.PATROL && !isWaitingAtPatrolSpot) ||
                        (ghost_State == GHOST_STATE.CHASE) ||
                        (ghost_State == GHOST_STATE.SNIFF && !isSniffing);
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
