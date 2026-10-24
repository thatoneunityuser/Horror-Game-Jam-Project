using UnityEngine;

public class DamageAndHitbox : MonoBehaviour
{
    public float Damage;
    public GameObject hiteffect;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            bool hit = false;

            BambooHatJohnnyEasyMode bambooJohnny = other.GetComponent<BambooHatJohnnyEasyMode>();
            if (bambooJohnny != null)
            {
                bambooJohnny.TakeDamage(Damage);
                hit = true;
            }
            else
            {
                JohnnyEasyMode easy = other.GetComponent<JohnnyEasyMode>();
                if (easy != null)
                {
                    easy.TakeDamage(Damage);
                    hit = true;
                }
                else
                {
                    JohnnyHardMode hard = other.GetComponent<JohnnyHardMode>();
                    if (hard != null)
                    {
                        hard.TakeDamage(Damage);
                        hit = true;
                    }
                }
            }

            if (hit && hiteffect != null)
            {
                Instantiate(hiteffect, transform.position, Quaternion.identity);
            }
        }
    }
}
