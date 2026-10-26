using System.Collections.Generic;
using UnityEngine;

public class EYESMOVEMENT : MonoBehaviour
{
    // Global registry of all eyes for fast distance checks (no colliders needed)
    public static readonly List<EYESMOVEMENT> allEyes = new List<EYESMOVEMENT>();

    [Header("Target & Area")]
    public Transform playerTarget;
    public EYESAREA area;

    [Header("Follow Settings")]
    [Tooltip("Movement speed towards player")]
    public float followSpeed = 3f;
    [Tooltip("Stop following if closer than this to player")]
    public float stopDistance = 1.0f;

    [Header("Anti-Stacking (Separation)")]
    [Tooltip("Minimum distance kept between eyes")]
    public float minDistance = 1.0f;
    [Tooltip("How strongly eyes repel each other when too close")]
    public float separationStrength = 5f;

    [Header("Boundary")]
    [Tooltip("Keep eye this far inside the area edges")]
    public float eyeRadiusPadding = 0.3f;

    private void OnEnable()
    {
        if (!allEyes.Contains(this))
            allEyes.Add(this);
    }

    private void OnDisable()
    {
        allEyes.Remove(this);
    }

    private void Start()
    {
        // Auto-find player if not assigned
        if (playerTarget == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerTarget = playerObj.transform;
        }

        // Auto-find parent EYESAREA if not assigned
        if (area == null)
        {
            area = GetComponentInParent<EYESAREA>();
        }
    }

    private void Update()
    {
        Vector2 currentPos = transform.position;
        Vector2 targetVelocity = Vector2.zero;

        // 1. Follow Player
        if (playerTarget != null)
        {
            Vector2 toPlayer = (Vector2)playerTarget.position - currentPos;
            float distToPlayer = toPlayer.magnitude;

            if (distToPlayer > stopDistance)
            {
                targetVelocity += toPlayer.normalized * followSpeed;
            }
        }

        // 2. Anti-Stacking: Push away from neighboring eyes
        Vector2 separationForce = Vector2.zero;
        for (int i = 0; i < allEyes.Count; i++)
        {
            EYESMOVEMENT other = allEyes[i];
            if (other == null || other == this) continue;

            Vector2 diff = currentPos - (Vector2)other.transform.position;
            float dist = diff.magnitude;

            // Handle perfect overlap tie-breaker
            if (dist < 0.0001f)
            {
                diff = Random.insideUnitCircle.normalized * 0.01f;
                dist = diff.magnitude;
            }

            // If closer than minDistance, apply repulsive force
            if (dist < minDistance)
            {
                float strength = (minDistance - dist) / minDistance; // 1.0 when touching, 0.0 at edge
                separationForce += (diff / dist) * (strength * separationStrength);
            }
        }

        // 3. Combine follow movement + separation force
        Vector2 newPos = currentPos + (targetVelocity + separationForce) * Time.deltaTime;

        // 4. Clamp strictly inside area bounds
        if (area != null)
        {
            newPos = area.ClampToBounds(newPos, eyeRadiusPadding);
        }

        transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the personal space of each eye in Scene view
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, minDistance * 0.5f);
    }
}
