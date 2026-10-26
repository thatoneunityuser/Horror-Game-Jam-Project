using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class Door : MonoBehaviour
{
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

    [Header("Position Validation")]
    [Tooltip("Maximum vertical distance between player and door for interaction (prevents tall colliders from triggering while on stairs below)")]
    public float maxVerticalDistance = 2.5f;

    void Awake()
    {
        ResetDoorState();
    }

    void Start()
    {
        // Double-check reset in Start so no cloned state persists after instantiation
        ResetDoorState();

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        // Check if this door belongs to a StairRoom
        bool isStairDoor = GetComponentInParent<StairColliderLogic>() != null ||
                           (transform.root != null && transform.root.name.ToLower().Contains("stair")) ||
                           gameObject.name.ToLower().Contains("stair");

        if (isStairDoor)
        {
            // StairRoom doors should never be locked by entities since entities cannot navigate stairs
            needKey = false;
        }
        else
        {
            BlindedEntity blindedEntity = FindObjectOfType<BlindedEntity>();
            RushEntity rushEntity = FindObjectOfType<RushEntity>();
      
            if (blindedEntity != null || rushEntity != null)
            {
                needKey = true;
            }
            else
            {
                needKey = false;
            }
        }
    }

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
        playerInRange = false;
        spawnTime = Time.time;

        if (doorCollider == null)
        {
            doorCollider = GetComponent<Collider2D>();
            if (doorCollider == null)
            {
                doorCollider = GetComponentInChildren<Collider2D>();
            }
        }

        // Force all colliders to be enabled and SOLID (not trigger) on spawn
        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>(true);
        foreach (var col in allColliders)
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
        if (isOpened) return;

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

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            playerTransform = collision.transform;
            if (IsPlayerAtDoorLevel(collision.transform))
            {
                playerInRange = true;
                if (openMode == OpenMode.OnTouch || openMode == OpenMode.Both)
                {
                    TryOpenDoor();
                }
            }
        }

        BlindedEntity blindedEntity = collision.gameObject.GetComponent<BlindedEntity>();
        RushEntity rushEntity = collision.gameObject.GetComponent<RushEntity>();
        if (blindedEntity != null || rushEntity != null)
        {
            UnlockDoor();
            needKey = false;
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
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
        if (other.CompareTag("Player"))
        {
            playerTransform = other.transform;
            if (IsPlayerAtDoorLevel(other.transform))
            {
                playerInRange = true;
                if (openMode == OpenMode.OnTouch || openMode == OpenMode.Both)
                {
                    TryOpenDoor();
                }
            }
        }
    }



    public void TryOpenDoor()
    {
        if (isOpened) return;

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

        // 3. Key check
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
    }
    public void ClearAllEntity()
    {
        if (AnomalyCheck.instance != null)
        {
            AnomalyCheck.instance.CleanAllAnomaly();
        }
    }

    public void UnlockDoor()
    {
        hasKey = true;
        needKey = false;
    }
}