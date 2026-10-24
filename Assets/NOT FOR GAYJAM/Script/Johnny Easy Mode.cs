using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public class JohnnyEasyMode : MonoBehaviour
{
    [Header("Basic Stats")]
    public float Hp;
    public const int MAX_HEALTH = 6767;
    public TextMeshProUGUI healthText;
    public Slider hpbar;
    [Header("Hurt")]
    public GameObject DamageTextPrefab;   // assign your TMP text prefab here
    public Canvas DamageCanvas;           // assign the canvas to spawn text into
    public AnimationCurve textanimation;
    public Color Textalpha;

    public enum JohnnyState
    {
        Fly,
        DashToPlayer
    }
    public JohnnyState currentState;
    private Transform player;

    // ─── Fly ──────────────────────────────────────────────────────────────────
    [Header("Fly")]
    public float flySpeed = 3f;           // speed while hovering toward player
    public float flyStopDistance = 2f;    // stop this far away from player

    // ─── Laser Eye (fires only in Fly state) ─────────────────────────────────
    [Header("Laser Eye")]
    public GameObject[] laserShot;        // laser prefab(s)
    public Transform[] laserSpawn;        // eye/barrel spawn points
    public float shotPerSec = 1f;         // seconds between laser bursts

    // ─── Dash ─────────────────────────────────────────────────────────────────
    [Header("Dash To Player")]
    public float dashSpeed = 18f;         // movement speed during dash
    public float dashCoolDown = 0.5f;     // seconds per direction segment
    public float DamageOnDash = 20f;      // damage dealt on contact while dashing
    public AudioClip dashSound;
    private bool isDashing = false;
    private bool damagedThisDash = false;
    private bool touchedGround = false;
    private AudioSource audioSource;
 
              // disabled during dash so Johnny passes through

    // ─── State Switch Timer ───────────────────────────────────────────────────
    private float stateSwitchTimer;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        Hp = MAX_HEALTH;
        currentState = JohnnyState.Fly;
      

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
        else
            Debug.LogError("JohnnyEasyMode: No GameObject tagged 'Player' found!");

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        ScheduleNextSwitch();
        StartCoroutine(LaserFireLoop());
      
    }

    // ── Pick a new random switch delay ────────────────────────────────────────
    void ScheduleNextSwitch()
    {
        stateSwitchTimer = Random.Range(3f, 10f);
    }

    // ── Count down the state-switch timer ─────────────────────────────────────
    void Update()
    {
        healthText.text = Hp.ToString() + "/" + MAX_HEALTH.ToString();
        hpbar.value = Hp / MAX_HEALTH;
        if (player == null) return;

        stateSwitchTimer -= Time.deltaTime;
        if (stateSwitchTimer <= 0f)
        {
            SwitchState();
            ScheduleNextSwitch();
        }
    }

    void SwitchState()
    {
        if (isDashing) return;  // never interrupt an active dash burst

        if (currentState == JohnnyState.Fly)
        {
            currentState = JohnnyState.DashToPlayer;
            StartCoroutine(DashBurst());
        }
        else
        {
            currentState = JohnnyState.Fly;
        }
    }

    // ── Fly toward player (only while in Fly state, not during a dash) ─────────
    void FixedUpdate()
    {
        if (player == null || isDashing) return;

        if (currentState == JohnnyState.Fly)
        {
            float dist = Vector2.Distance(transform.position, player.position);
            if (dist > flyStopDistance)
            {
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    player.position,
                    flySpeed * Time.fixedDeltaTime
                );
            }

            // Flip sprite horizontally to always face the player
            Vector2 dir = player.position - transform.position;
            if (dir.x != 0f)
            {
                transform.localScale = new Vector3(
                    Mathf.Sign(dir.x) * Mathf.Abs(transform.localScale.x),
                    transform.localScale.y,
                    transform.localScale.z
                );
            }
        }
    }

    // ── Laser fires at shotPerSec rate, but ONLY during Fly state ─────────────
    IEnumerator LaserFireLoop()
    {
        while (true)
        {
            if (currentState == JohnnyState.Fly)
                FireLasers();

            yield return new WaitForSeconds(shotPerSec);
        }
    }

    void FireLasers()
    {
        if (laserShot == null || laserSpawn == null) return;

        int count = Mathf.Min(laserShot.Length, laserSpawn.Length);
        for (int i = 0; i < count; i++)
        {
            if (laserShot[i] != null && laserSpawn[i] != null)
                Instantiate(laserShot[i], laserSpawn[i].position, laserSpawn[i].rotation);
        }
    }

    // ── Bullet-style dash: moves in a direction for dashCoolDown seconds,
    //    then instantly re-snaps toward the player and repeats (dashCount times) ─
    IEnumerator DashBurst()
    {
        isDashing = true;
        yield return new WaitForSeconds(0.5f);

       
      
        int dashCount = Random.Range(1, 4);         // 1 to 3 segments

        // Snap initial direction toward player
        Vector2 dir = Vector2.zero;
        if (player != null)
            dir = ((Vector2)(player.position - transform.position)).normalized;

        for (int i = 0; i < dashCount; i++)
        {
            if (audioSource != null && dashSound != null)
            {
                audioSource.PlayOneShot(dashSound);
            }
            damagedThisDash = false; // reset per segment so each pass can hit
            touchedGround = false;
            float elapsed = 0f;

            // Fly in the locked direction for dashCoolDown seconds — no stopping
            while (elapsed < dashCoolDown)
            {
                if (touchedGround)
                {
                    break;
                }
                transform.position += (Vector3)(dir * dashSpeed * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Sharp turn: re-snap direction toward player's current position
            if (player != null)
                dir = ((Vector2)(player.position - transform.position)).normalized;
        }

        // Restore solid collision after all dash segments complete
      
       
        isDashing = false;
        currentState = JohnnyState.Fly;
        ScheduleNextSwitch();
    }

    // ── Deal dash damage when passing through the player ─────────────────────
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isDashing) return;

        if (other.CompareTag("Player") && !damagedThisDash)
        {
            PlayerStats ps = other.GetComponent<PlayerStats>();
            if (ps != null)
            {
                ps.TakeDamage(DamageOnDash);
                damagedThisDash = true; // one hit per dash segment
            }
        }

        if (other.CompareTag("Ground") || 
            other.gameObject.name.ToLower().Contains("ground") || 
            other.gameObject.name.ToLower().Contains("floor") || 
            other.gameObject.layer == LayerMask.NameToLayer("Ground") ||
            other.gameObject.layer == LayerMask.NameToLayer("Floor"))
        {
            touchedGround = true;
        }
    }

    // ── Take damage ───────────────────────────────────────────────────────────
    public void TakeDamage(float damageAmount)
    {
        Hp -= damageAmount;
        Hp = Mathf.Clamp(Hp, 0, MAX_HEALTH);

        // Spawn the TMP prefab as a child of the canvas
        if (DamageTextPrefab != null && DamageCanvas != null)
        {
            GameObject textObj = Instantiate(DamageTextPrefab, DamageCanvas.transform);

            // Position the text near Johnny's position in canvas space with a small offset
            RectTransform canvasRect = DamageCanvas.GetComponent<RectTransform>();
            RectTransform rt = textObj.GetComponent<RectTransform>();
            if (rt != null && canvasRect != null)
            {
                // Translate Johnny's position to screen and then canvas space
                Vector2 screenPos = Camera.main.WorldToScreenPoint(transform.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPos,
                    DamageCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
                    out Vector2 localPos
                );

                // Small random offset near Johnny (e.g. slightly above his head)
                float offsetX = Random.Range(-2f, 2f);
                float offsetY = Random.Range(0f, 2f);
                rt.localPosition = new Vector3(localPos.x + offsetX, localPos.y + offsetY, 0f);
            }

            TextMeshProUGUI tmp = textObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = "-" + (int)damageAmount;
                tmp.color = Textalpha;           // show immediately at full color
                StartCoroutine(ShowDamage(tmp, textObj));
            }
        }

        if (Hp <= 0)
            UnityEngine.SceneManagement.SceneManager.LoadScene("YouWin");
    }

    IEnumerator ShowDamage(TextMeshProUGUI tmp, GameObject textObj)
    {
        float t = 0f;
        while (t < 1f)
        {
            if (textObj == null) yield break;
            t += Time.deltaTime;
            // Fade alpha from 1 → 0 over 1 second (curve drives the falloff shape)
            Color c = Textalpha;
            c.a = Textalpha.a * (1f - t);   // linear fade; replace with textanimation.Evaluate(t) for custom curve
            tmp.color = c;
            yield return null;
        }
        if (textObj != null)
            Destroy(textObj);
    }
}
