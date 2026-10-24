using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

public class PlayerStats : MonoBehaviour
{
    [Header("Hp/Regen")]
    public const int MAX_HEALTH = 400;
    public int health ;
    public float regenRate = 5f;
    public TextMeshProUGUI healthText;
    public Slider hpbar;

    [Header("Weapons")]
    public GameObject ChickenDrum, ChickenWing;
    public Transform ShotSpot;
    public float fireRate = 0.25f;      // seconds between shots (assault rifle feel)
    private float fireTimer = 0f;

    [Header("Parry")]
    public float Parrycooldown = 1f;
    public float parryDuration = 0.5f;    // how long parry window stays active
    public float fightBackDuration = 0.5f; // set this to match your FightBack animation length
    public bool IsParryOnCooldown;
    private Animator ChickenwingsAnimator;
    public bool isParrying = false;
    private float healthRegenAccumulator = 0f;
    public AudioSource source;
    public AudioClip ParrySound;


    void Start()
    {
        health = MAX_HEALTH;
        ChickenwingsAnimator = ChickenWing.GetComponent<Animator>();
    }

    void Update()
    {
        healthText.text = health.ToString() + "/" + MAX_HEALTH.ToString();
        AttackMechanics();
        hpbar.value = (float)health / MAX_HEALTH;
        RegenerateHealth();

    }
    void RegenerateHealth()
    {
        if (health < MAX_HEALTH)
        {
            healthRegenAccumulator += regenRate * Time.deltaTime;
            if (healthRegenAccumulator >= 1f)
            {
                int healthToAdd = Mathf.FloorToInt(healthRegenAccumulator);
                health += healthToAdd;
                health = Mathf.Clamp(health, 0, MAX_HEALTH);
                healthRegenAccumulator -= healthToAdd;
            }
        }
        else
        {
            healthRegenAccumulator = 0f;
        }
    }

    void AttackMechanics()
    {
        // Held left-click: shoot every fireRate seconds
        if (Input.GetMouseButton(0))
        {
            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f)
            {
                fireTimer = fireRate;
                FireDrum();
            }
        }
        else
        {
            fireTimer = 0f; // reset so first shot is instant on next press
        }

        // Gate on !isParrying AND !IsParryOnCooldown so cooldown is respected on input
        if (Input.GetMouseButtonDown(1) && !isParrying && !IsParryOnCooldown)
        {
            StartCoroutine(ParryRoutine());
        }
    }

    void FireDrum()
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Vector2 dir = ((Vector2)(mouseWorld - ShotSpot.position)).normalized;
        GameObject drum = Instantiate(ChickenDrum, ShotSpot.position, Quaternion.identity);
        drum.GetComponent<ChickenDrum>().Launch(dir);
    }

    // Called on right-click: opens the parry window, then closes it
    IEnumerator ParryRoutine()
    {
        isParrying = true;
        ChickenWing.SetActive(true);
        ChickenwingsAnimator.Play("Parry");

        // Wait for the parry window (set parryDuration to match your animation length)
        yield return new WaitForSeconds(parryDuration);

        isParrying = false;
        ChickenWing.SetActive(false);

        // Start cooldown so player can't spam parry
        parryCooldownCoroutine = StartCoroutine(ParryCooldown());
    }

    // Called when a hit lands during parry: plays the counter-attack animation
    IEnumerator ParrySuccess()
    {
        source.PlayOneShot(ParrySound);
        ChickenwingsAnimator.Play("FightBack");
        // Immediately cancel any pending cooldown so parry is ready again at once
        if (parryCooldownCoroutine != null)
        {
            StopCoroutine(parryCooldownCoroutine);
            parryCooldownCoroutine = null;
        }
        IsParryOnCooldown = false;
        yield return null;
    }

    // Store the cooldown coroutine so we can cancel it on parry success
    private Coroutine parryCooldownCoroutine;

    IEnumerator ParryCooldown()
    {
        IsParryOnCooldown = true;
        yield return new WaitForSeconds(Parrycooldown);
        IsParryOnCooldown = false;
        parryCooldownCoroutine = null;
    }

    public void TakeDamage(float DamageAmount)
    {
        // Immune while dashing
        SideScrollMovement movement = GetComponent<SideScrollMovement>();
        if (movement != null && movement.isDashing) return;

        if (isParrying)
        {
            Debug.Log("Parry success");
            StartCoroutine(ParrySuccess());
            return;
        }
        health -= (int)DamageAmount;
        hpbar.value = (float)health / MAX_HEALTH;
        healthText.text = health.ToString() + "/" + MAX_HEALTH.ToString();
        health = Mathf.Clamp(health, 0, MAX_HEALTH);
        if(health<=0)
        {   
            health = 0;
            if (JohnnyHardMode.Instance != null && JohnnyHardMode.Instance.currentPhase == JohnnyHardMode.phasestate.Phase3)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("CUTSCENE");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("GameOver");
            }
        }
    }
}
