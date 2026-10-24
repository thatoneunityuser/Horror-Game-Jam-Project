using UnityEngine;

public class SnapShotSafeArea : MonoBehaviour
{
    [Header("Safe Area Settings")]
    [Tooltip("Radius around this object considered safe if no collider is attached")]
    public float safeRadius = 3f;

    [Tooltip("True when player is currently inside this safe area")]
    public bool isPlayerInside = false;

    private Collider2D areaCollider;

    void Awake()
    {
        areaCollider = GetComponent<Collider2D>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
        }
    }

    /// <summary>
    /// Checks if a position is inside this safe area by collider, trigger state, or radius distance.
    /// </summary>
    public bool IsInside(Vector3 position)
    {
        if (isPlayerInside) return true;

        if (areaCollider != null)
        {
            return areaCollider.OverlapPoint(position);
        }

        return Vector2.Distance(transform.position, position) <= safeRadius;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
        if (areaCollider == null)
        {
            Gizmos.DrawWireSphere(transform.position, safeRadius);
        }
    }
}
