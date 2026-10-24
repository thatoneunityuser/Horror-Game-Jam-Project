using UnityEngine;

public class StairColliderLogic : MonoBehaviour
{
    [Header("Stair Colliders")]
    public Collider2D[] collider2DsUp;
    public Collider2D[] collider2DsDown;

    [Header("Turning Point Settings")]
    public Collider2D turningPoint;
    public bool IsTurningPoint;
    public Vector2 turningpointScale;
    public Vector2 turningpointOffset;

    [Header("Player Reference")]
    public Transform player;

    [Header("State")]
    public bool isUpStarWalkable;
    public bool isDownStarWalkable;

    private Collider2D playerCollider;
    private Rigidbody2D playerRb;
    private bool lastUpState;
    private bool lastDownState;
    private float previousPlayerY;
    private bool wasSteppingOnTurningPoint;

    void Start()
    {
        FindPlayer();

        // If this object is marked as the turning point and no specific collider was dragged, use this object's collider
        if (turningPoint == null && IsTurningPoint)
        {
            turningPoint = GetComponent<Collider2D>();
        }

        if (player != null)
        {
            previousPlayerY = player.position.y;
            CalculatePlayerUpOrDown();
        }
        else
        {
            UpdateColliders(true);
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
                playerCollider = playerObj.GetComponent<Collider2D>();
                playerRb = playerObj.GetComponent<Rigidbody2D>();
            }
        }
        else
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

        // Only trigger turning point calculation when the player actually STEPS on top of it
        if (turningPoint != null && IsTurningPoint)
        {
            bool isStepping = IsPlayerSteppingOnTurningPoint();
            if (isStepping && !wasSteppingOnTurningPoint)
            {
                CalculatePlayerUpOrDown();
            }
            wasSteppingOnTurningPoint = isStepping;

            // Only update previousPlayerY when not stepping on the landing,
            // so we preserve the approach direction (from above or below)
            if (!isStepping)
            {
                previousPlayerY = player.position.y;
            }
        }
        else
        {
            previousPlayerY = player.position.y;
        }

        // Sync colliders if state variables were changed
        if (isUpStarWalkable != lastUpState || isDownStarWalkable != lastDownState)
        {
            UpdateColliders();
        }
    }

    public bool IsPlayerSteppingOnTurningPoint()
    {
        if (turningPoint == null || player == null) return false;
        if (playerCollider == null) FindPlayer();
        if (playerCollider == null) return false;

        Vector3 pPos = playerCollider.bounds.center;
        Vector3 pExtents = playerCollider.bounds.extents;
        float playerFeet = playerCollider.bounds.min.y;

        Vector3 tpPos = turningPoint.bounds.center + new Vector3(turningpointOffset.x, turningpointOffset.y, 0f);
        Vector3 tpSize = turningPoint.bounds.size;

        // Apply inspector scale override if specified
        if (turningpointScale.x > 0f) tpSize.x = turningpointScale.x;
        if (turningpointScale.y > 0f) tpSize.y = turningpointScale.y;

        float halfWidth = tpSize.x * 0.5f;
        float horzDist = Mathf.Abs(pPos.x - tpPos.x);

        // Player must be horizontally within the landing area (with half player-width tolerance)
        bool withinHorz = horzDist <= (halfWidth + pExtents.x * 0.5f);

        // Top surface of the turning point landing
        float tpTop = (turningpointScale.y > 0f) ? (tpPos.y + tpSize.y * 0.5f) : (turningPoint.bounds.max.y + turningpointOffset.y);

        // Player's feet must be on the top surface (stepping on top, not touching from below)
        bool feetOnTop = (playerFeet >= tpTop - 0.25f) && (playerFeet <= tpTop + 0.35f);

        // Ensure player is not jumping upward from below through the platform
        bool notJumpingUp = (playerRb == null) || (playerRb.velocity.y <= 0.2f);

        return withinHorz && feetOnTop && notJumpingUp;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && IsTurningPoint)
        {
            if (IsPlayerSteppingOnTurningPoint() && !wasSteppingOnTurningPoint)
            {
                CalculatePlayerUpOrDown();
                wasSteppingOnTurningPoint = true;
            }
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player") && IsTurningPoint)
        {
            if (IsPlayerSteppingOnTurningPoint() && !wasSteppingOnTurningPoint)
            {
                CalculatePlayerUpOrDown();
                wasSteppingOnTurningPoint = true;
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && IsTurningPoint)
        {
            if (IsPlayerSteppingOnTurningPoint() && !wasSteppingOnTurningPoint)
            {
                CalculatePlayerUpOrDown();
                wasSteppingOnTurningPoint = true;
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            wasSteppingOnTurningPoint = false;
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            wasSteppingOnTurningPoint = false;
        }
    }

    public void CalculatePlayerUpOrDown()
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        float referenceY = (turningPoint != null) ? turningPoint.bounds.center.y : transform.position.y;
        float turningTop = (turningPoint != null) ? turningPoint.bounds.max.y : referenceY;
        if (turningPoint != null && turningpointScale.y > 0f)
        {
            turningTop = turningPoint.bounds.center.y + (turningpointScale.y * 0.5f);
        }

        // If player arrived from below the turning point, they are moving UP -> enable upstairs
        if (previousPlayerY < turningTop - 0.1f)
        {
            isUpStarWalkable = false;
            isDownStarWalkable = true;
        }
        // If player arrived from above the turning point, they are moving DOWN -> enable downstairs
        else if (previousPlayerY > turningTop + 0.35f)
        {
            isUpStarWalkable = true;
            isDownStarWalkable = false;
        }
        // Fallback: check center Y relative to reference
        else
        {
            if (player.position.y > referenceY)
            {
                isUpStarWalkable = false;
                isDownStarWalkable = true;
            }
            else
            {
                isUpStarWalkable = true;
                isDownStarWalkable = false;
            }
        }

        UpdateColliders();
    }

    public void UpdateColliders(bool force = false)
    {
        if (!force && isUpStarWalkable == lastUpState && isDownStarWalkable == lastDownState)
            return;

        if (collider2DsUp != null)
        {
            foreach (Collider2D c in collider2DsUp)
            {
                if (c != null)
                {
                    c.enabled = isUpStarWalkable;
                }
            }
        }

        if (collider2DsDown != null)
        {
            foreach (Collider2D c in collider2DsDown)
            {
                if (c != null)
                {
                    c.enabled = isDownStarWalkable;
                }
            }
        }

        lastUpState = isUpStarWalkable;
        lastDownState = isDownStarWalkable;
    }

    void OnDrawGizmos()
    {
        if (turningPoint != null)
        {
            Vector3 center = turningPoint.bounds.center + new Vector3(turningpointOffset.x, turningpointOffset.y, 0f);
            Vector3 size = turningPoint.bounds.size;
            if (turningpointScale.x > 0f) size.x = turningpointScale.x;
            if (turningpointScale.y > 0f) size.y = turningpointScale.y;

            // Wire cube for the turning point footprint
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, size);

            // Green wire cube showing the exact "step on top" trigger zone
            float tpTop = (turningpointScale.y > 0f) ? (center.y + size.y * 0.5f) : (turningPoint.bounds.max.y + turningpointOffset.y);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3(center.x, tpTop + 0.05f, center.z), new Vector3(size.x, 0.4f, 0.1f));
        }
    }
}
