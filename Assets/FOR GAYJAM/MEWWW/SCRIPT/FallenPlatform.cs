using UnityEngine;
using System.Collections;

public class FallenPlatform : MonoBehaviour
{
    [Header("Colliders")]
    [Tooltip("Main platform collider. If unassigned, automatically gets GetComponent<Collider2D>()")]
    public Collider2D platformCollider;

    [Tooltip("Optional: additional colliders to enable/disable together with the platform")]
    public Collider2D[] extraColliders;

    [Header("Player Tracking")]
    public Transform player;

    [Header("Platform Settings")]
    [Tooltip("Threshold offset below the platform top where feet are considered above platform")]
    public float surfaceOffset = 0.15f;

    [Tooltip("Allow player to drop through platform by pressing S or Down Arrow")]
    public bool allowDropThrough = true;
    public float dropThroughDuration = 0.35f;

    [Header("Stand Above Check (Gizmo Box)")]
    [Tooltip("Size of the detection box. If set (>0), this box determines if player is standing above platform.")]
    public Vector2 PlayerStandAboveCheckRange;
    [Tooltip("Offset of the detection box from this GameObject's position.")]
    public Vector2 PlayerStandAboveCheckOffset;

    [Header("Stair Reference")]
    [Tooltip("Stair to destroy once player stands above platform. If unassigned, automatically finds one in scene.")]
    public StairColliderLogic stairtodelete;

    [Header("Debug Status (Read-Only)")]
    public bool isGoingUp;
    public bool isGoingDown;
    public bool isPlayerAbovePlatform;
    public bool isPlatformSolid = true;

    private Collider2D playerCollider;
    private Rigidbody2D playerRb;
    private float previousPlayerY;
    private bool isDroppingThrough = false;

    void Start()
    {
        if (stairtodelete == null)
        {
            stairtodelete = FindObjectOfType<StairColliderLogic>();
        }

        if (platformCollider == null)
        {
            platformCollider = GetComponent<Collider2D>();
        }

        FindPlayer();

        if (player != null)
        {
            previousPlayerY = player.position.y;
        }
    }

    void FindPlayer()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        if (player != null)
        {
            if (playerCollider == null) playerCollider = player.GetComponent<Collider2D>();
            if (playerRb == null) playerRb = player.GetComponent<Rigidbody2D>();
        }
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        CheckPlayerVerticalDirection();
        CheckDropThroughInput();
        UpdatePlatformColliderState();

        if (isPlayerAbovePlatform && stairtodelete != null)
        {
            Destroy(stairtodelete.gameObject, 0.1f);
            stairtodelete = null;
        }
    }

    void CheckPlayerVerticalDirection()
    {
        float currentY = player.position.y;
        float deltaY = currentY - previousPlayerY;

        // Prefer Rigidbody2D velocity if moving, otherwise fallback to position delta
        if (playerRb != null && Mathf.Abs(playerRb.linearVelocity.y) > 0.05f)
        {
            isGoingUp = playerRb.linearVelocity.y > 0.05f;
            isGoingDown = playerRb.linearVelocity.y < -0.05f;
        }
        else
        {
            isGoingUp = deltaY > 0.002f;
            isGoingDown = deltaY < -0.002f;
        }

        previousPlayerY = currentY;

        // Check if player is standing above platform using the Gizmo box if configured
        if (PlayerStandAboveCheckRange.x > 0f && PlayerStandAboveCheckRange.y > 0f)
        {
            isPlayerAbovePlatform = CheckPlayerInStandArea();
        }
        else
        {
            // Fallback: check whether player's feet are above the platform top surface
            float platformTop = (platformCollider != null) ? platformCollider.bounds.max.y : transform.position.y;
            float playerBottom = (playerCollider != null) ? playerCollider.bounds.min.y : player.position.y;

            isPlayerAbovePlatform = (playerBottom >= platformTop - surfaceOffset);
        }
    }

    bool CheckPlayerInStandArea()
    {
        Vector2 boxCenter = (Vector2)transform.position + PlayerStandAboveCheckOffset;

        // 1. Physics2D OverlapBox check (matches triggers and solid colliders)
        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, PlayerStandAboveCheckRange, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && (hits[i].CompareTag("Player") || hits[i].transform == player || (playerCollider != null && hits[i] == playerCollider)))
            {
                return true;
            }
        }

        // 2. Direct bounds intersection fallback (ensures detection even across different collision layers)
        if (playerCollider != null)
        {
            Bounds boxBounds = new Bounds(new Vector3(boxCenter.x, boxCenter.y, 0f), new Vector3(PlayerStandAboveCheckRange.x, PlayerStandAboveCheckRange.y, 100f));
            if (boxBounds.Intersects(playerCollider.bounds))
            {
                return true;
            }
        }

        // 3. Transform position check fallback
        if (player != null)
        {
            Vector2 halfSize = PlayerStandAboveCheckRange * 0.5f;
            if (Mathf.Abs(player.position.x - boxCenter.x) <= halfSize.x &&
                Mathf.Abs(player.position.y - boxCenter.y) <= halfSize.y)
            {
                return true;
            }
        }

        return false;
    }

    void CheckDropThroughInput()
    {
        if (!allowDropThrough || isDroppingThrough) return;

        // Press S or Down Arrow to drop down through the platform
        if (isPlayerAbovePlatform && (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)))
        {
            StartCoroutine(DropThroughRoutine());
        }
    }

    IEnumerator DropThroughRoutine()
    {
        isDroppingThrough = true;
        SetColliderEnabled(false);

        yield return new WaitForSeconds(dropThroughDuration);

        isDroppingThrough = false;
    }

    void UpdatePlatformColliderState()
    {
        if (isDroppingThrough) return;

        // LOGIC:
        // 1. If player is moving UP from below: Disable collider so player jumps through smoothly.
        // 2. If player is ABOVE platform and falling or standing: Enable collider so player lands on top.
        // 3. If player is below the platform: Keep disabled so they can jump up freely.
        bool shouldBeSolid;

        if (isPlayerAbovePlatform)
        {
            // When above platform, be solid unless jumping upward away from it
            shouldBeSolid = !isGoingUp;
        }
        else
        {
            // Below platform: pass through (disabled)
            shouldBeSolid = false;
        }

        SetColliderEnabled(shouldBeSolid);
    }

    void SetColliderEnabled(bool solid)
    {
        if (isPlatformSolid == solid) return;
        isPlatformSolid = solid;

        if (platformCollider != null)
        {
            platformCollider.enabled = solid;
        }

        if (extraColliders != null)
        {
            foreach (var col in extraColliders)
            {
                if (col != null)
                {
                    col.enabled = solid;
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 boxCenter = transform.position + (Vector3)PlayerStandAboveCheckOffset;
        Gizmos.DrawWireCube(boxCenter, new Vector3(PlayerStandAboveCheckRange.x, PlayerStandAboveCheckRange.y, 0.1f));
    }
}
