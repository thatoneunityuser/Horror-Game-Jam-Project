using UnityEngine;

public class Locker : MonoBehaviour
{
    public Vector3 LockerUseRange = new Vector3(2f, 2f, 2f);
    public Transform PlayerPos;
    public Animator animator;

    private Hided hided;
    private bool isPlayerInsideThisLocker = false;
    private SideScrollMovement playerMovement;
    private Rigidbody2D rb;

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        hided = FindObjectOfType<Hided>();
        FindPlayerIfNeeded();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (hided == null)
            {
                hided = FindObjectOfType<Hided>();
            }

            FindPlayerIfNeeded();

            if (hided == null) return;

            // If player is already inside this locker, press E to exit
            if (isPlayerInsideThisLocker)
            {
                if (animator != null)
                {
                    animator.Play("Locker Interacted");
                }
                ExitLocker();
            }
            // If player is in range of this locker and not currently hiding, press E to enter
            else if (IsPlayerInRange() && !hided.IsPlayerHide)
            {
                if (animator != null)
                {
                    animator.Play("Locker Interacted");
                }
                EnterLocker();
            }
        }

        // If player was unhidden by time limit, reset local flag and restore player controls
        if (isPlayerInsideThisLocker && (hided != null && !hided.IsPlayerHide))
        {
            ExitLocker();
        }
    }

    void EnterLocker()
    {
        if (PlayerPos == null) return;

        if (hided != null)
        {
            hided.IsPlayerHide = true;
        }
        isPlayerInsideThisLocker = true;

        // Snap player position to locker
        PlayerPos.position = transform.position;

        if (playerMovement != null)
        {
            playerMovement.isMoving = false;
            playerMovement.enabled = false;
        }
        if (rb != null)
        {
            rb.gravityScale = 0;
            rb.linearVelocity = Vector2.zero;
        }
    }

    void ExitLocker()
    {
        if (hided != null)
        {
            hided.IsPlayerHide = false;
        }
        isPlayerInsideThisLocker = false;

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }
        if (rb != null)
        {
            rb.gravityScale = 1;
        }
    }

    void FindPlayerIfNeeded()
    {
        if (PlayerPos == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                PlayerPos = player.transform;
            }
        }

        if (PlayerPos != null)
        {
            if (playerMovement == null) playerMovement = PlayerPos.GetComponent<SideScrollMovement>();
            if (rb == null) rb = PlayerPos.GetComponent<Rigidbody2D>();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, LockerUseRange);
    }

    bool IsPlayerInRange()
    {
        if (PlayerPos == null) return false;
        return Vector3.Distance(PlayerPos.position, transform.position) < LockerUseRange.x;
    }
}
