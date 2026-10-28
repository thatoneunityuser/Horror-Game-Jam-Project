using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class Door : MonoBehaviour
{
    public AudioSource a;
    public AudioClip open;

    public enum OpenMode { OnTouch, OnKeyPress, Both }

    [Header("Open Mode")]
    [Tooltip("OnTouch: Opens when player touches door. OnKeyPress: Player must press interactKey. Both: either works.")]
    public OpenMode openMode = OpenMode.OnTouch;
    public KeyCode interactKey = KeyCode.E;

    [Header("Key Settings")]
    public bool needKey = false;
    public bool hasKey = false;

    [Header("Door State")]
    [Tooltip("Whether this door is currently open. Automatically resets to false whenever cloned/instantiated.")]
    public bool isOpened = false;

    [Header("Chain Prevention")]
    [Tooltip("Global cooldown (seconds) before ANY door in the game can open after one opens")]
    public float chainCooldown = 0.5f;

    [Tooltip("A newly instantiated door cannot open for this many seconds after spawning")]
    public float spawnCooldown = 0.2f;

    [Header("Open Door Event")]
    public UnityEvent onOpen;

    [Header("Components & Colliders")]
    public Animator animator;
    public Collider2D doorCollider;

    [Tooltip("If true, all colliders on this door are forced to solid (isTrigger = false) when spawned/cloned")]
    public bool forceSolidOnSpawn = true;

    [Tooltip("When opened, set collider to isTrigger = true so the player can walk through")]
    public bool makeTriggerOnOpen = true;

    [Tooltip("When opened, completely disable the collider")]
    public bool disableColliderOnOpen = false;

    // Static timestamp shared across all doors to prevent chain reactions
    private static float lastGlobalDoorOpenedTime = -999f;
    private float spawnTime;
    private bool playerInRange = false;
    private Transform playerTransform;
    private bool isStairDoor = false;

    // Entity lock tracking (Rush / Blinded / SecondEventGhost)
    private readonly HashSet<GameObject> entitiesThatTouchedThisDoor = new HashSet<GameObject>();
    private readonly Dictionary<GameObject, float> lastEntityX = new Dictionary<GameObject, float>();
    private readonly HashSet<GameObject> ignoredCollisionEntities = new HashSet<GameObject>();
    private Collider2D[] cachedDoorColliders;
    private float lastLockLogTime = -999f;
    private bool openedByGhostWithoutEvent = false;
    public bool IsOpenedByGhostWithoutEvent => openedByGhostWithoutEvent;

    [Header("Position Validation")]
    [Tooltip("Maximum vertical distance between player and door for interaction (prevents tall colliders from triggering while on stairs below)")]
    public float maxVerticalDistance = 2.5f;

    void Awake()
    {
        ResetDoorState();
        CheckStairDoor();
    }

    void Start()
    {
        // Double-check reset in Start so no cloned state persists after instantiation
        ResetDoorState();
        CheckStairDoor();

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (isStairDoor)
        {
            // StairRoom doors should never be locked by entities since entities cannot navigate stairs
            needKey = false;
        }
    }

    private void CheckStairDoor()
    {
        isStairDoor = GetComponentInParent<StairColliderLogic>() != null ||
                      (transform.root != null && transform.root.name.ToLower().Contains("stair")) ||
                      gameObject.name.ToLower().Contains("stair");
    }

    public bool IsStairDoor => isStairDoor;

    void OnEnable()
    {
        // Reset when enabled/cloned
        if (!isOpened)
        {
            ResetDoorState();
        }
    }

    public void FakeDoor()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ResetDoorState()
    {
        // Force closed state so cloned/instantiated doors NEVER start open!
        isOpened = false;
        openedByGhostWithoutEvent = false;
        playerInRange = false;
        spawnTime = Time.time;

        entitiesThatTouchedThisDoor.Clear();
        lastEntityX.Clear();
        ignoredCollisionEntities.Clear();

        if (doorCollider == null)
        {
            doorCollider = GetComponent<Collider2D>();
            if (doorCollider == null)
            {
                doorCollider = GetComponentInChildren<Collider2D>();
            }
        }

        // Force all colliders to be enabled and SOLID (not trigger) on spawn
        cachedDoorColliders = GetComponentsInChildren<Collider2D>(true);
        foreach (var col in cachedDoorColliders)
        {
            if (col != null)
            {
                col.enabled = true;
                if (forceSolidOnSpawn)
                {
                    col.isTrigger = false;
                }
            }
        }

        if (doorCollider != null)
        {
            doorCollider.enabled = true;
            if (forceSolidOnSpawn)
            {
                doorCollider.isTrigger = false;
            }
        }

        // Always prioritize this door's OWN animator rather than a cross-referenced external one
        Animator ownAnimator = GetComponent<Animator>();
        if (ownAnimator == null) ownAnimator = GetComponentInChildren<Animator>();
        if (ownAnimator != null)
        {
            animator = ownAnimator;
        }

        if (animator != null)
        {
            // Rebind resets any animated properties (like animated isTrigger = true) back to default
            animator.Rebind();
            animator.Update(0f);

            // Explicitly force to closed animation state so it never starts in Open
            if (animator.HasState(0, Animator.StringToHash("Door UnOpen")))
            {
                animator.Play("Door UnOpen", 0, 0f);
            }
        }
    }

    public bool IsPlayerAtDoorLevel(Transform pTransform)
    {
        if (pTransform == null) return false;
        float yDiff = Mathf.Abs(pTransform.position.y - transform.position.y);
        return yDiff <= maxVerticalDistance;
    }

    void Update()
    {
        if (isOpened)
        {
            if (openedByGhostWithoutEvent)
            {
                CheckDeferredPlayerCrossedOpenDoor();
            }
            return;
        }

        UpdateEntityPassThroughAndTouches();

        // Auto-retry opening if player is touching the door and at door level
        if (playerInRange && (openMode == OpenMode.OnTouch || openMode == OpenMode.Both))
        {
            if (playerTransform != null && IsPlayerAtDoorLevel(playerTransform))
            {
                TryOpenDoor();
            }
        }

        // Check key press if mode allows it and player is in range and at door level
        if ((openMode == OpenMode.OnKeyPress || openMode == OpenMode.Both) && playerInRange)
        {
            if (playerTransform != null && IsPlayerAtDoorLevel(playerTransform))
            {
                if (Input.GetKeyDown(interactKey))
                {
                    TryOpenDoor();
                }
            }
        }
    }

    private void EnsureEntityIgnoresDoorColliders(GameObject entityObj)
    {
        if (entityObj == null || ignoredCollisionEntities.Contains(entityObj)) return;
        ignoredCollisionEntities.Add(entityObj);

        if (cachedDoorColliders == null || cachedDoorColliders.Length == 0)
        {
            cachedDoorColliders = GetComponentsInChildren<Collider2D>(true);
        }

        Collider2D[] entityCols = entityObj.GetComponentsInChildren<Collider2D>(true);
        if (entityCols == null || cachedDoorColliders == null) return;

        foreach (Collider2D dCol in cachedDoorColliders)
        {
            if (dCol == null) continue;
            foreach (Collider2D eCol in entityCols)
            {
                if (eCol != null)
                {
                    Physics2D.IgnoreCollision(dCol, eCol, true);
                }
            }
        }
    }

    private void UpdateEntityPassThroughAndTouches()
    {
        float doorX = doorCollider != null ? doorCollider.bounds.center.x : transform.position.x;
        float doorY = doorCollider != null ? doorCollider.bounds.center.y : transform.position.y;
        float halfWidth = doorCollider != null ? Mathf.Max(0.4f, doorCollider.bounds.extents.x) : 0.5f;
        float touchRangeX = halfWidth + 1.25f;
        float touchRangeY = maxVerticalDistance + 2.5f;

        // 1. RushEntity pass-through & touch unlock (does NOT trigger onOpen!)
        foreach (RushEntity rush in RushEntity.ActiveEntities)
        {
            if (rush == null || !rush.gameObject.activeInHierarchy) continue;
            GameObject obj = rush.gameObject;
            EnsureEntityIgnoresDoorColliders(obj);

            if (!entitiesThatTouchedThisDoor.Contains(obj))
            {
                Vector3 pos = rush.transform.position;
                bool inVerticalRange = Mathf.Abs(pos.y - doorY) <= touchRangeY;
                bool touchedX = Mathf.Abs(pos.x - doorX) <= touchRangeX;
                bool crossedX = lastEntityX.TryGetValue(obj, out float prevX) &&
                                ((prevX - doorX) * (pos.x - doorX) <= 0f);

                if (inVerticalRange && (touchedX || crossedX))
                {
                    NotifyEntityTouched(obj);
                }
                lastEntityX[obj] = pos.x;
            }
        }

        // 2. BlindedEntity pass-through & touch unlock (does NOT trigger onOpen!)
        foreach (BlindedEntity blinded in BlindedEntity.ActiveEntities)
        {
            if (blinded == null || !blinded.gameObject.activeInHierarchy) continue;
            GameObject obj = blinded.gameObject;
            EnsureEntityIgnoresDoorColliders(obj);

            if (!entitiesThatTouchedThisDoor.Contains(obj))
            {
                Vector3 pos = blinded.transform.position;
                bool inVerticalRange = Mathf.Abs(pos.y - doorY) <= touchRangeY;
                bool touchedX = Mathf.Abs(pos.x - doorX) <= touchRangeX;
                bool crossedX = lastEntityX.TryGetValue(obj, out float prevX) &&
                                ((prevX - doorX) * (pos.x - doorX) <= 0f);

                if (inVerticalRange && (touchedX || crossedX))
                {
                    NotifyEntityTouched(obj);
                }
                lastEntityX[obj] = pos.x;
            }
        }

        // 3. SecondEventGhost pass-through & door opening (opens closed door; invokes onOpen ONLY if player stays in that room)
        float ghostTouchRangeX = halfWidth + 0.4f;
        List<SecondEventGhost> ghostsSnapshot = SecondEventGhost.ActiveGhosts.Count > 0
            ? new List<SecondEventGhost>(SecondEventGhost.ActiveGhosts)
            : null;

        if (ghostsSnapshot != null)
        {
            for (int i = 0; i < ghostsSnapshot.Count; i++)
            {
                SecondEventGhost ghost = ghostsSnapshot[i];
                if (ghost == null || !ghost.gameObject.activeInHierarchy) continue;

                GameObject obj = ghost.gameObject;
                EnsureEntityIgnoresDoorColliders(obj);

                if (isStairDoor || ghost.ghost_State == SecondEventGhost.GHOST_STATE.IDLE)
                {
                    continue;
                }

                Vector3 pos = ghost.transform.position;
                bool inVerticalRange = Mathf.Abs(pos.y - doorY) <= touchRangeY;
                bool touchedX = Mathf.Abs(pos.x - doorX) <= ghostTouchRangeX;
                bool crossedX = lastEntityX.TryGetValue(obj, out float prevX) &&
                                ((prevX - doorX) * (pos.x - doorX) <= 0f);
                lastEntityX[obj] = pos.x;

                if (inVerticalRange && (touchedX || crossedX))
                {
                    OpenDoorBySecondEventGhost(ghost);
                    if (isOpened) return;
                }
            }
        }
    }

    /// <summary>
    /// Called when RushEntity or BlindedEntity touches or passes through this closed door.
    /// Unlocks this door for that entity WITHOUT opening the door and WITHOUT invoking onOpen.
    /// </summary>
    public void NotifyEntityTouched(GameObject entityObj)
    {
        if (entityObj == null) return;

        RushEntity rush = entityObj.GetComponent<RushEntity>();
        if (rush == null) rush = entityObj.GetComponentInParent<RushEntity>();
        if (rush != null) entityObj = rush.gameObject;
        else
        {
            BlindedEntity blinded = entityObj.GetComponent<BlindedEntity>();
            if (blinded == null) blinded = entityObj.GetComponentInParent<BlindedEntity>();
            if (blinded != null) entityObj = blinded.gameObject;
        }

        if (entitiesThatTouchedThisDoor.Add(entityObj))
        {
            needKey = false;
            Debug.Log($"[Door] '{entityObj.name}' touched/triggered door '{name}'. Door is now unlocked (without invoking onOpen).");
        }
    }

    public bool IsLockedByEntity()
    {
        if (isOpened || isStairDoor) return false;

        // 1. Locked if any active RushEntity in the scene has not touched/triggered this door yet
        foreach (RushEntity rush in RushEntity.ActiveEntities)
        {
            if (rush != null && rush.gameObject.activeInHierarchy && !entitiesThatTouchedThisDoor.Contains(rush.gameObject))
            {
                return true;
            }
        }

        // 2. Locked if any active BlindedEntity in the scene has not touched/triggered this door yet
        foreach (BlindedEntity blinded in BlindedEntity.ActiveEntities)
        {
            if (blinded != null && blinded.gameObject.activeInHierarchy && !entitiesThatTouchedThisDoor.Contains(blinded.gameObject))
            {
                return true;
            }
        }

        // 3. Locked if an active SecondEventGhost is currently staying in this closed door's room
        foreach (SecondEventGhost ghost in SecondEventGhost.ActiveGhosts)
        {
            if (ghost != null && ghost.gameObject.activeInHierarchy && ghost.IsBlockingClosedDoor(this))
            {
                return true;
            }
        }

        return false;
    }

    private bool HandleEntityContact(GameObject obj)
    {
        if (obj == null) return false;

        RushEntity rush = obj.GetComponent<RushEntity>();
        if (rush == null) rush = obj.GetComponentInParent<RushEntity>();
        if (rush != null)
        {
            NotifyEntityTouched(rush.gameObject);
            return true;
        }

        BlindedEntity blinded = obj.GetComponent<BlindedEntity>();
        if (blinded == null) blinded = obj.GetComponentInParent<BlindedEntity>();
        if (blinded != null)
        {
            NotifyEntityTouched(blinded.gameObject);
            return true;
        }

        SecondEventGhost ghost = obj.GetComponent<SecondEventGhost>();
        if (ghost == null) ghost = obj.GetComponentInParent<SecondEventGhost>();
        if (ghost != null)
        {
            if (!isOpened && !isStairDoor && ghost.ghost_State != SecondEventGhost.GHOST_STATE.IDLE)
            {
                OpenDoorBySecondEventGhost(ghost);
            }
            return true;
        }

        return false;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (HandleEntityContact(collision.gameObject))
        {
            return;
        }

        if (collision.gameObject.CompareTag("Player"))
        {
            playerTransform = collision.transform;
            if (IsPlayerAtDoorLevel(collision.transform))
            {
                if (isOpened && openedByGhostWithoutEvent)
                {
                    TriggerDeferredDoorOpenForPlayer();
                    return;
                }

                playerInRange = true;
                if (openMode == OpenMode.OnTouch || openMode == OpenMode.Both)
                {
                    TryOpenDoor();
                }
            }
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (HandleEntityContact(collision.gameObject))
        {
            return;
        }

        if (collision.gameObject.CompareTag("Player"))
        {
            playerTransform = collision.transform;
            if (IsPlayerAtDoorLevel(collision.transform))
            {
                playerInRange = true;
                if (!isOpened && (openMode == OpenMode.OnTouch || openMode == OpenMode.Both))
                {
                    TryOpenDoor();
                }
            }
            else
            {
                playerInRange = false;
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (HandleEntityContact(other.gameObject))
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            playerTransform = other.transform;
            if (IsPlayerAtDoorLevel(other.transform))
            {
                if (isOpened && openedByGhostWithoutEvent)
                {
                    TriggerDeferredDoorOpenForPlayer();
                    return;
                }

                playerInRange = true;
                if (openMode == OpenMode.OnTouch || openMode == OpenMode.Both)
                {
                    TryOpenDoor();
                }
            }
        }
    }

    private static bool HasPersistentMethod(UnityEvent evt, string methodName)
    {
        if (evt == null || string.IsNullOrEmpty(methodName)) return false;
        int count = evt.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (evt.GetPersistentMethodName(i) == methodName)
            {
                return true;
            }
        }
        return false;
    }

    private void CheckDeferredPlayerCrossedOpenDoor()
    {
        if (!openedByGhostWithoutEvent || isStairDoor) return;

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
        if (playerTransform == null || !IsPlayerAtDoorLevel(playerTransform)) return;

        float doorX = doorCollider != null ? doorCollider.bounds.center.x : transform.position.x;
        float halfWidth = doorCollider != null ? Mathf.Max(0.4f, doorCollider.bounds.extents.x) : 0.5f;
        if (Mathf.Abs(playerTransform.position.x - doorX) <= halfWidth + 0.6f)
        {
            TriggerDeferredDoorOpenForPlayer();
        }
    }

    private void TriggerDeferredDoorOpenForPlayer()
    {
        if (!openedByGhostWithoutEvent) return;
        openedByGhostWithoutEvent = false;

        if (HasPersistentMethod(onOpen, "FakeDoor")) return;

        if (cleanPreviousRoomsOnOpen)
        {
            CleanallPreviousRoom();
        }

        onOpen?.Invoke();
        SecondEventGhost.NotifyDoorOpened(this);
    }

    /// <summary>
    /// Opens this door when SecondEventGhost walks through/touches it.
    /// Invokes Door.onOpen ONLY if the player is currently staying in this door's room!
    /// </summary>
    public void OpenDoorBySecondEventGhost(SecondEventGhost ghost)
    {
        if (isOpened || isStairDoor || ghost == null) return;
        if (ghost.ghost_State == SecondEventGhost.GHOST_STATE.IDLE) return;

        bool playerInDoorRoom = ghost.IsPlayerStayingInDoorRoom(this);
        bool isFakeDoor = HasPersistentMethod(onOpen, "FakeDoor");
        bool invokeOnOpen = playerInDoorRoom && !isFakeDoor;

        isOpened = true;
        needKey = false;
        openedByGhostWithoutEvent = !invokeOnOpen && !isFakeDoor;
        lastGlobalDoorOpenedTime = Time.time;

        Debug.Log($"[Door] SecondEventGhost opened door '{name}' (playerInDoorRoom={playerInDoorRoom} -> trigger onOpen={invokeOnOpen}).");

        if (animator != null)
        {
            animator.Play("Open", -1, 0f);
        }
        else
        {
            PlayOpenSound();
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

        if (makeTriggerOnOpen)
        {
            if (doorCollider != null) doorCollider.isTrigger = true;
            foreach (var col in colliders)
            {
                if (col != null) col.isTrigger = true;
            }
        }

        if (disableColliderOnOpen)
        {
            if (doorCollider != null) doorCollider.enabled = false;
            foreach (var col in colliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        if (invokeOnOpen)
        {
            if (cleanPreviousRoomsOnOpen)
            {
                CleanallPreviousRoom();
            }

            onOpen?.Invoke();
        }

        ghost.OnGhostOpenedDoor(this, invokeOnOpen);
    }

    public void TryOpenDoor()
    {
        if (isOpened) return;

        UpdateEntityPassThroughAndTouches();

        // Verify player is vertically at door level
        if (playerTransform != null && !IsPlayerAtDoorLevel(playerTransform))
        {
            return;
        }

        // 1. Prevent chain reactions: Do not open if another door opened too recently
        if (Time.time - lastGlobalDoorOpenedTime < chainCooldown)
        {
            Debug.Log($"[Door] Blocked chain open: another door opened {Time.time - lastGlobalDoorOpenedTime:F2}s ago (cooldown: {chainCooldown}s).");
            return;
        }

        // 2. Prevent newly spawned doors from opening immediately on spawn
        if (Time.time - spawnTime < spawnCooldown)
        {
            Debug.Log($"[Door] Blocked spawn open: door spawned {Time.time - spawnTime:F2}s ago (spawn cooldown: {spawnCooldown}s).");
            return;
        }

        // 3. Entity lock check (Rush / Blinded must touch door first; SecondEventGhost must leave room first)
        if (IsLockedByEntity())
        {
            if (Time.time - lastLockLogTime > 1.0f)
            {
                lastLockLogTime = Time.time;
                Debug.Log($"[Door] '{name}' is locked by an active entity!");
            }
            return;
        }

        // 4. Key check
        if (needKey && !hasKey)
        {
            Debug.Log("[Door] Locked! Player needs a key to open this door.");
            return;
        }

        OpenDoor();
    }

    [Header("Room Cleanup")]
    [Tooltip("If true, automatically cleans previous rooms when this door opens")]
    public bool cleanPreviousRoomsOnOpen = false;

    public void CleanallPreviousRoom()
    {
        if (RoomCounter.instance != null)
        {
            RoomCounter.instance.DestroyAllPreviousRoom();
        }
        else
        {
            RoomCounter rc = FindObjectOfType<RoomCounter>();
            if (rc != null)
            {
                rc.DestroyAllPreviousRoom();
            }
            else
            {
                Debug.LogWarning("[Door] CleanallPreviousRoom: RoomCounter not found in scene!");
            }
        }
    }

    [ContextMenu("Open Door")]
    public void OpenDoor()
    {
        if (isOpened) return;
        isOpened = true;
        lastGlobalDoorOpenedTime = Time.time;

        Debug.Log("[Door] Door opened!");

        if (animator != null)
        {
            animator.Play("Open", -1, 0f);
        }
        else
        {
            PlayOpenSound();
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

        if (makeTriggerOnOpen)
        {
            if (doorCollider != null) doorCollider.isTrigger = true;
            foreach (var col in colliders)
            {
                if (col != null) col.isTrigger = true;
            }
        }

        if (disableColliderOnOpen)
        {
            if (doorCollider != null) doorCollider.enabled = false;
            foreach (var col in colliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        if (cleanPreviousRoomsOnOpen)
        {
            CleanallPreviousRoom();
        }

        onOpen?.Invoke();

        // Notify SecondEventGhost if player opened a door into the next event room (45 - 48)
        SecondEventGhost.NotifyDoorOpened(this);
    }

    public void ClearAllEntity()
    {
        if (AnomalyCheck.instance != null)
        {
            AnomalyCheck.instance.CleanAllAnomaly();
        }
    }

    private float lastOpenSoundTime = -1f;

    public void PlayOpenSound()
    {
        if (Time.time - lastOpenSoundTime < 0.2f) return;
        lastOpenSoundTime = Time.time;

        if (a == null)
        {
            a = GetComponent<AudioSource>();
            if (a == null) a = GetComponentInChildren<AudioSource>();
            if (a == null) a = gameObject.AddComponent<AudioSource>();
        }
        if (a != null)
        {
            a.playOnAwake = false;
            a.spatialBlend = 0f; // 2D playback so camera distance doesn't mute it

#if UNITY_EDITOR
            if (open == null)
            {
                open = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FOR GAYJAM/MEWWW/AUDIOOOO/door open.wav");
            }
#endif
            if (open != null)
            {
                a.PlayOneShot(open);
            }
        }
    }

    public void UnlockDoor()
    {
        hasKey = true;
        needKey = false;
    }

    public void Updateroomcoount()
    {
        RoomCounterUI.Instance.SyncWithRoomCounter();
    }
}
