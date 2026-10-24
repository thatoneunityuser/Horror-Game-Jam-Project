using UnityEngine;

public class CameraScriptWithLimitFollowArea : MonoBehaviour
{
    public static CameraScriptWithLimitFollowArea instance;
    public Transform player;
    private Vector3 LimitOffset;
    public float smoothSpeed = 1f;
    public float smoothTime = 0.15f;
    public Vector3 offset;
    public float zoom = 5f;

    [Header("Camera Limits")]
    public bool useBounds = false; // Toggle this in the Unity Inspector!
    public Vector3 minBounds;
    public Vector3 maxBounds;

    private Vector2 cameraVelocity2D = Vector2.zero;
    private Rigidbody2D playerRb;
    private Camera cam;

    void Awake()
    {
        if (instance == null)
            instance = this;

        cam = GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
    }

    void Start()
    {
        FindPlayer();
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
            playerRb = player.GetComponent<Rigidbody2D>();
            if (playerRb != null && playerRb.interpolation != RigidbodyInterpolation2D.Interpolate)
            {
                playerRb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
        }
    }

    void LateUpdate()
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        // Ensure Rigidbody2D interpolation is enabled on the player to prevent physics vs render frame stutter
        if (playerRb == null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
        }
        if (playerRb != null && playerRb.interpolation != RigidbodyInterpolation2D.Interpolate)
        {
            playerRb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        Vector2 currentPos = new Vector2(transform.position.x, transform.position.y);
        Vector2 targetPos = new Vector2(player.position.x + offset.x, player.position.y + offset.y);

        // Pre-clamp target position so camera doesn't build up excess velocity past bounds
        if (useBounds)
        {
            float realMinX = Mathf.Min(minBounds.x, maxBounds.x);
            float realMaxX = Mathf.Max(minBounds.x, maxBounds.x);
            float realMinY = Mathf.Min(minBounds.y, maxBounds.y);
            float realMaxY = Mathf.Max(minBounds.y, maxBounds.y);

            targetPos.x = Mathf.Clamp(targetPos.x, realMinX, realMaxX);
            targetPos.y = Mathf.Clamp(targetPos.y, realMinY, realMaxY);
        }

        Vector2 smoothedPos;
        if (smoothTime > 0f)
        {
            // Pure 2D damping eliminates Z-axis depth disparity jitter
            smoothedPos = Vector2.SmoothDamp(currentPos, targetPos, ref cameraVelocity2D, smoothTime);
        }
        else
        {
            float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            smoothedPos = Vector2.Lerp(currentPos, targetPos, t);
        }

        float finalX = smoothedPos.x;
        float finalY = smoothedPos.y;

        // Post-clamp to ensure boundaries are strictly enforced
        if (useBounds)
        {
            float realMinX = Mathf.Min(minBounds.x, maxBounds.x);
            float realMaxX = Mathf.Max(minBounds.x, maxBounds.x);
            float realMinY = Mathf.Min(minBounds.y, maxBounds.y);
            float realMaxY = Mathf.Max(minBounds.y, maxBounds.y);

            finalX = Mathf.Clamp(finalX, realMinX, realMaxX);
            finalY = Mathf.Clamp(finalY, realMinY, realMaxY);
        }

        // Apply 2D position with fixed -10f Z for 2D camera
        transform.position = new Vector3(finalX, finalY, -10f);
        LimitOffset = new Vector3(targetPos.x, targetPos.y, 0f) - transform.position;

        if (cam != null)
        {
            cam.orthographicSize = zoom;
        }
        else if (Camera.main != null)
        {
            Camera.main.orthographicSize = zoom;
        }
    }

    void OnDrawGizmos() 
    {
        if (useBounds)
        {
            Gizmos.color = Color.yellow;
            Vector3 center = (minBounds + maxBounds) / 2f;
            Vector3 size = maxBounds - minBounds;
            Gizmos.DrawWireCube(center, size);
        }
    }
    public void zoomIn()
    {
        zoom = 2.5f;
    }
    public void zoomOut()
    {
        zoom = 5f;
    }
}
