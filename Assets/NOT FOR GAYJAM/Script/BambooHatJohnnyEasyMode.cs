using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

[System.Serializable]
public class AttackPatternAndHitbox
{
    public enum The_Way_It_Call { Hitbox_Touched, Timing_Based };

    [Header("Information")]
    public string Name;
    public float DamageMultiplier = 1f;
    public float GetDamage(float baseDamage) => baseDamage * DamageMultiplier;
    public Vector2 HitboxSize = new Vector2(2f, 2f);
    public Vector2 HitboxOffset;
    public UnityEvent OnAttack;
    public GameObject Effect;
    public AudioClip SoundEffect;

    [Header("How They Call This Move")]
    public The_Way_It_Call the_Way_It_Call;
    [Range(0f, 30f)] public float MinTime = 1f;
    [Range(0f, 30f)] public float MaxTime = 3f;

    public bool needtoTeleportToPlayer;
}

public class BambooHatJohnnyEasyMode : MonoBehaviour
{
    [Header("Stats")]
    public float hp = 20000;
    public float maxHp = 20000;
    public float BaseDamage = 10f;
    public bool AvoidingDamage;

    [Header("Attack Patterns (List)")]
    public List<AttackPatternAndHitbox> attackPatterns = new List<AttackPatternAndHitbox>();

    [Header("Targeting Information")]
    public Transform Player;
    public float speed = 3f;
    public float distanceToPlayer;
    private Rigidbody2D rb;
    private Animator anim;
    private AudioSource audioSource;
    private bool isAttacking = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();

        if (Player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                Player = playerObj.transform;
            }
        }

        StartCoroutine(TimingBasedAttackRoutine());
    }

    void Update()
    {
        if (attackPatterns == null || attackPatterns.Count == 0 || isAttacking) return;

        // Check Hitbox-Touched patterns
        foreach (var pattern in attackPatterns)
        {
            if (pattern != null && pattern.the_Way_It_Call == AttackPatternAndHitbox.The_Way_It_Call.Hitbox_Touched)
            {
                if (IsPlayerInHitbox(pattern))
                {
                    ExecuteAttack(pattern);
                    break;
                }
            }
        }
    }

    IEnumerator TimingBasedAttackRoutine()
    {
        while (true)
        {
            List<AttackPatternAndHitbox> timingPatterns = GetTimingBasedPatterns();
            if (timingPatterns.Count > 0 && !isAttacking)
            {
                AttackPatternAndHitbox selectedPattern = timingPatterns[Random.Range(0, timingPatterns.Count)];
                float waitTime = Random.Range(selectedPattern.MinTime, selectedPattern.MaxTime);
                yield return new WaitForSeconds(Mathf.Max(0.1f, waitTime));

                if (!isAttacking && selectedPattern != null)
                {
                    ExecuteAttack(selectedPattern);
                }
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private List<AttackPatternAndHitbox> GetTimingBasedPatterns()
    {
        List<AttackPatternAndHitbox> list = new List<AttackPatternAndHitbox>();
        if (attackPatterns != null)
        {
            foreach (var pattern in attackPatterns)
            {
                if (pattern != null && pattern.the_Way_It_Call == AttackPatternAndHitbox.The_Way_It_Call.Timing_Based)
                {
                    list.Add(pattern);
                }
            }
        }
        return list;
    }

    public void ExecuteAttack(AttackPatternAndHitbox pattern)
    {
        if (pattern == null || isAttacking) return;

        if (pattern.the_Way_It_Call == AttackPatternAndHitbox.The_Way_It_Call.Hitbox_Touched)
        {
            if (!IsPlayerInHitbox(pattern)) return;
        }

        isAttacking = true;

        // Teleport near player if requested
        if (pattern.needtoTeleportToPlayer && Player != null)
        {
            transform.position = Player.position + new Vector3(-1f, 0f, 0f);
        }

        if (anim != null) anim.SetTrigger("Attack");

        if (pattern.SoundEffect != null && audioSource != null)
        {
            audioSource.PlayOneShot(pattern.SoundEffect);
        }

        if (pattern.Effect != null)
        {
            Instantiate(pattern.Effect, transform.position + (Vector3)pattern.HitboxOffset, Quaternion.identity);
        }

        pattern.OnAttack?.Invoke();

        // Deal damage to CatFish player if in hitbox
        DealDamageToPlayer(pattern);

        StartCoroutine(ResetAttackFlag());
    }
    public void Parry()
    {
       StartCoroutine(ParryCoutine());
    }
    IEnumerator ParryCoutine()
    {
        AvoidingDamage = true;
        yield return new WaitForSeconds(5);
        AvoidingDamage = false;
        yield return null;
    }

    private void DealDamageToPlayer(AttackPatternAndHitbox pattern)
    {
        if (Player == null) return;

        if (IsPlayerInHitbox(pattern))
        {
            float damage = pattern.GetDamage(BaseDamage);

            // Deal damage to CatFish PlayerStats
            PlayerStats stats = Player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.TakeDamage(damage);
                Debug.Log($"[BambooHatJohnny] Dealt {damage:F1} damage to PlayerStats!", this);
            }

            // Deal damage to CatFishHPSystem
            CatFishHPSystem hpSys = Player.GetComponent<CatFishHPSystem>();
            if (hpSys != null)
            {
                hpSys.TakeDamage(damage);
                Debug.Log($"[BambooHatJohnny] Dealt {damage:F1} damage to CatFishHPSystem!", this);
            }
        }
    }

    IEnumerator ResetAttackFlag()
    {
        yield return new WaitForSeconds(0.5f);
        isAttacking = false;
    }

    public bool IsPlayerInHitbox(AttackPatternAndHitbox pattern)
    {
        if (pattern == null) return false;
        Collider2D[] hitboxes = Physics2D.OverlapBoxAll(transform.position + (Vector3)pattern.HitboxOffset, pattern.HitboxSize, 0f);
        foreach (Collider2D hitbox in hitboxes)
        {
            if (hitbox.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (attackPatterns == null) return;
        Gizmos.color = Color.red;
        foreach (var pattern in attackPatterns)
        {
            if (pattern != null)
            {
                Gizmos.DrawWireCube(transform.position + (Vector3)pattern.HitboxOffset, pattern.HitboxSize);
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (AvoidingDamage) return;

        hp -= damage;
        Debug.Log($"[BambooHatJohnny] Took {damage:F1} damage! Remaining HP: {hp:F1}/{maxHp:F1}", this);

        if (hp <= 0)
        {
            hp = 0;
            Die();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckCatFishCollision(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        CheckCatFishCollision(collider.gameObject);
    }

    private void CheckCatFishCollision(GameObject obj)
    {
        if (obj.CompareTag("Player") || obj.GetComponent<CatFishMovement>() != null)
        {
            if (CatFishAttackSystem.instance != null && CatFishAttackSystem.instance.isAttacking)
            {
                TakeDamage(CatFishAttackSystem.instance.currentDamage);
                CatFishAttackSystem.instance.ResetIsAttacking();
            }
        }
    }

    void Die()
    {
        Destroy(gameObject);
    }
}
