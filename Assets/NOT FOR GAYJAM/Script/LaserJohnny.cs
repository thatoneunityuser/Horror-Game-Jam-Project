using UnityEngine;

public class LaserJohnny : MonoBehaviour
{
    public float damage;
    public float laserSpeed;
    public bool hasParried = false;
    private Vector2 dir; // set once in Start, never updated (no homing)

    void Start()
    {
        Transform player = GameObject.FindGameObjectWithTag("Player").transform;
        dir = ((Vector2)(player.position - transform.position)).normalized;

        // Rotate to face direction on spawn
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    void Update()
    {
        transform.position += (Vector3)(dir * laserSpeed * Time.deltaTime);
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        // ── Hit player ────────────────────────────────────────────────────────
        if (other.CompareTag("Player"))
        {
            // Already parried — ignore further player collisions
            if (hasParried) return;

            PlayerStats stats = other.GetComponent<PlayerStats>();
            if (stats == null) return;

            if (!stats.isParrying)
            {
                // Normal hit: deal damage and destroy
                stats.TakeDamage(damage);
                Destroy(gameObject);
            }
            else
            {
                // Parry: redirect the laser toward the enemy — let it keep flying
                hasParried = true;
                // Ignore player collider from now on so we don't re-trigger
                Physics2D.IgnoreCollision(GetComponent<Collider2D>(), other, true);

                GameObject enemy = GameObject.FindGameObjectWithTag("Enemy");
                if (enemy != null)
                {
                    dir = ((Vector2)(enemy.transform.position - transform.position)).normalized;
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                }
                // Don't destroy — laser continues flying toward the enemy
            }
        }

        // ── Hit enemy (reflected laser arrives) ───────────────────────────────
        else if (hasParried)
        {
            // Debug: log what we actually hit so you can verify the tag in the Inspector
            Debug.Log($"[LaserJohnny] Reflected laser hit: {other.name} (tag: {other.tag})");

            bool hit = false;

            JohnnyEasyMode easy = other.GetComponent<JohnnyEasyMode>();
            if (easy != null)
            {
                easy.TakeDamage(damage);
                hit = true;
            }
            else
            {
                JohnnyHardMode hard = other.GetComponent<JohnnyHardMode>();
                if (hard != null)
                {
                    hard.TakeDamage(damage * 200.67f);
                    hit = true;
                }
            }

            if (hit)
            {
                Destroy(gameObject);
            }
        }
    }
}
