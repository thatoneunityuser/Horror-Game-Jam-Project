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
    }

    void CheckPlayerVerticalDirection()
    {
        float currentY = player.position.y;
        float deltaY = currentY - previousPlayerY;

        // Prefer Rigidbody2D velocity if moving, otherwise fallback to position delta
        if (playerRb != null && Mathf.Abs(playerRb.velocity.y) > 0.05f)
        {
            isGoingUp = playerRb.velocity.y > 0.05f;
            isGoingDown = playerRb.velocity.y < -0.05f;
        }
        else
        {
            isGoingUp = deltaY > 0.002f;
            isGoingDown = deltaY < -0.002f;
        }

        previousPlayerY = currentY;

        // Check whether player's feet are above the platform top surface
        float platformTop = (platformCollider != null) ? platformCollider.bounds.max.y : transform.position.y;
        float playerBottom = (playerCollider != null) ? playerCollider.bounds.min.y : player.position.y;

        isPlayerAbovePlatform = (playerBottom >= platformTop - surfaceOffset);
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
}
