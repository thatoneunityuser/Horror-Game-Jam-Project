using UnityEngine;

public class ChickenDrum : MonoBehaviour
{
    public float DamageAmount = 20f;
    public float speed = 20f;
    public float lifetime = 5f;
    public GameObject effect;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        Destroy(gameObject, lifetime);
    }

    public void Launch(Vector2 dir)
    {
        rb.linearVelocity = dir * speed;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            bool hit = false;

            BambooHatJohnnyEasyMode bambooJohnny = other.GetComponent<BambooHatJohnnyEasyMode>();
            if (bambooJohnny != null)
            {
                bambooJohnny.TakeDamage(DamageAmount);
                hit = true;
            }
            else
            {
                JohnnyEasyMode easy = other.GetComponent<JohnnyEasyMode>();
                if (easy != null)
                {
                    easy.TakeDamage(DamageAmount);
                    hit = true;
                }
                else
                {
                    JohnnyHardMode hard = other.GetComponent<JohnnyHardMode>();
                    if (hard != null)
                    {
                        hard.TakeDamage(DamageAmount);
                        hit = true;
                    }
                    else
                    {
                        EnemyHealthTest enemy = other.GetComponent<EnemyHealthTest>();
                        if (enemy != null)
                        {
                            enemy.TakeDamage(DamageAmount);
                            hit = true;
                        }
                    }
                }
            }

            if (hit)
            {
                if (effect != null)
                    Instantiate(effect, transform.position, transform.rotation);
                Destroy(gameObject);
            }
        }
    }
}
