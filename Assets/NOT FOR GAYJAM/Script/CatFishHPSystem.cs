using UnityEngine;
using System.Collections;

public class CatFishHPSystem : MonoBehaviour
{
    [Header("base HP of Catfish")]
    public float hp = 20000;
    public float maxHp = 20000;

    [Header("Regeneration system")]
    public bool EnableRegeneration;
    public float regenerationRate;

    [Header("Invincibility Frames after taking damage")]
    public float invincibilityFrames = 0.5f;
    private SpriteRenderer spriteRenderer;
    private bool ONInvincibilityFraming;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    void Update()
    {
        if (EnableRegeneration && hp < maxHp && hp > 0)
        {
            hp += regenerationRate * Time.deltaTime;
            if (hp > maxHp)
            {
                hp = maxHp;
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (ONInvincibilityFraming) return;
        if (hp <= 0) return;

        hp -= damage;
        if (invincibilityFrames > 0 && hp > 0)
        {
            StartCoroutine(InvincibilityFrames());
        }

        if (hp <= 0)
        {
            hp = 0;
            Die();
        }
    }

    IEnumerator InvincibilityFrames()
    {
        ONInvincibilityFraming = true;
        Color originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);
        }

        yield return new WaitForSeconds(invincibilityFrames);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        ONInvincibilityFraming = false;
    }

    void Die()
    {
        Destroy(gameObject);
    }
}
