using UnityEngine;

/// <summary>
/// Handles Catfish attack system where damage scales directly with Dash Force.
/// </summary>
public class CatFishAttackSystem : MonoBehaviour
{
    public static CatFishAttackSystem instance;

    [Header("Damage Settings")]
    [Tooltip("Multiplier applied to Dash Force to calculate final damage.")]
    public float damageMultiplier = 2f;

    [Tooltip("Base damage if attack is triggered manually without dash.")]
    public float baseDamage = 10f;

    [Tooltip("Duration (in seconds) that attack mode remains active after dashing.")]
    public float attackDuration = 0.5f;

    [Header("State (Read Only)")]
    public bool isAttacking;
    public float currentDamage;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // Right-Click legacy manual attack trigger
        if (Input.GetMouseButtonDown(1))
        {
            Attack();
        }
    }

    /// <summary>
    /// Triggers an attack scaling damage directly with the executed Dash Force.
    /// </summary>
    public void AttackWithForce(float dashForce)
    {
        currentDamage = dashForce * damageMultiplier;
        isAttacking = true;

        // Auto-reset attack state after attackDuration
        CancelInvoke(nameof(ResetIsAttacking));
        Invoke(nameof(ResetIsAttacking), attackDuration);

        Debug.Log($"[CatFishAttackSystem] Attack activated! Dash Force: {dashForce:F1} -> Damage: {currentDamage:F1}");
    }

    /// <summary>
    /// Default attack with base damage.
    /// </summary>
    public void Attack()
    {
        currentDamage = baseDamage;
        isAttacking = true;

        CancelInvoke(nameof(ResetIsAttacking));
        Invoke(nameof(ResetIsAttacking), attackDuration);
    }

    public void ResetIsAttacking()
    {
        isAttacking = false;
        currentDamage = 0f;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        HandleHit(collider.gameObject);
    }

    private void HandleHit(GameObject hitObject)
    {
        if (!isAttacking) return;

        bool hitAny = false;

        if (hitObject.TryGetComponent<BambooHatJohnnyEasyMode>(out BambooHatJohnnyEasyMode bambooJohnny))
        {
            bambooJohnny.TakeDamage(currentDamage);
            hitAny = true;
        }
        else if (hitObject.TryGetComponent<EnemyHealthTest>(out EnemyHealthTest enemy))
        {
            enemy.TakeDamage(currentDamage);
            hitAny = true;
        }
        else if (hitObject.TryGetComponent<JohnnyEasyMode>(out JohnnyEasyMode easyJohnny))
        {
            easyJohnny.TakeDamage(currentDamage);
            hitAny = true;
        }
        else if (hitObject.TryGetComponent<JohnnyHardMode>(out JohnnyHardMode hardJohnny))
        {
            hardJohnny.TakeDamage(currentDamage);
            hitAny = true;
        }

        if (hitAny)
        {
            Debug.Log($"[CatFishAttackSystem] HIT! Dealt {currentDamage:F1} damage to {hitObject.name}!", hitObject);
            ResetIsAttacking();
        }
    }
}