using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RushEntity : MonoBehaviour
{
    public float Speed = 10f;
    public float lifetime = 15f;
    public bool IsReversal = false;

    [Header("Audio Panning")]
    public AudioSource audioSource;
    [Tooltip("Horizontal distance where sound pans fully to Left (-1) or Right (+1)")]
    public float panDistance = 15f;
    [Tooltip("Toggle this ON if your sound is coming from the opposite ear")]
    public bool invertPan = false;
    [Tooltip("Smoothly fade volume when far away from the player")]
    public bool useDistanceVolume = true;
    public float maxAudioDistance = 25f;
    [Range(0f, 1f)] public float maxVolume = 1f;
    [Range(0f, 1f)] public float minVolume = 0.05f;

    private Hided hideStatus;
    private Transform playerTransform;
    private float currentLifeTimer;

    void Start()
    {
        currentLifeTimer = lifetime;
        hideStatus = FindObjectOfType<Hided>();
        
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Force 2D spatial blend so Unity's 3D AudioListener camera bugs don't invert the audio
        if (audioSource != null)
        {
            audioSource.spatialBlend = 0f;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    void LateUpdate()
    {
        // When killing the player, halt movement and pause despawn timer so the scene reset coroutine is never cancelled
        if (isKillingPlayer) return;

        currentLifeTimer -= Time.deltaTime;
        if (currentLifeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if(IsReversal)
        {
            transform.Translate(Vector2.left * Speed * Time.deltaTime);
        }
        else
        {
            transform.Translate(Vector2.right * Speed * Time.deltaTime);
        }

        UpdateAudioPanning();
    }

    void UpdateAudioPanning()
    {
        if (audioSource == null) return;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
            else return;
        }

        // Calculate horizontal direction: 
        // dx > 0 = Entity is to the RIGHT of player -> pan should be positive (Right ear)
        // dx < 0 = Entity is to the LEFT of player -> pan should be negative (Left ear)
        float dx = transform.position.x - playerTransform.position.x;
        float pan = Mathf.Clamp(dx / (panDistance > 0f ? panDistance : 15f), -1f, 1f);

        if (invertPan)
        {
            pan = -pan;
        }

        audioSource.panStereo = pan;

        if (useDistanceVolume)
        {
            float dist = Mathf.Abs(dx);
            float t = Mathf.Clamp01(dist / (maxAudioDistance > 0f ? maxAudioDistance : 25f));
            audioSource.volume = Mathf.Lerp(maxVolume, minVolume, t);
        }
    }

    private bool isKillingPlayer = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        bool isPlayer = other.CompareTag("Player") || (other.transform.root != null && other.transform.root.CompareTag("Player"));
        if (isPlayer)
        {
            if (hideStatus == null)
            {
                hideStatus = FindObjectOfType<Hided>();
            }

            if (hideStatus == null || !hideStatus.IsPlayerHide)
            {
                if (!isKillingPlayer)
                {
                    StartCoroutine(PlayerDieCoroutine());
                }
            }
        }
        Door door = other.GetComponent<Door>();
        if (door != null)
        {
            Debug.Log("I'm in range of a door");
            door.UnlockDoor();
            door.needKey = false;
        }
    }

    private IEnumerator PlayerDieCoroutine()
    {
        isKillingPlayer = true;

        // Disable all colliders on this entity so it doesn't trigger multiple times
        Collider2D[] allCols = GetComponentsInChildren<Collider2D>();
        foreach (var c in allCols)
        {
            if (c != null) c.enabled = false;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            SideScrollMovement movement = player.GetComponent<SideScrollMovement>();
            if (movement != null)
            {
                // Delegate guaranteed death handling to player so despawning of the entity cannot stop the reset
                movement.KillPlayer(2f);
            }
            else
            {
                Animator anim = player.GetComponent<Animator>();
                if (anim == null) anim = player.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    if (anim.HasState(0, Animator.StringToHash("Die"))) anim.Play("Die");
                    else if (anim.HasState(0, Animator.StringToHash("Dead"))) anim.Play("Dead");
                    else anim.Play("Die");
                }

                Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
                if (playerRb != null) playerRb.linearVelocity = Vector2.zero;
            }
        }

        if (hideStatus != null) hideStatus.enabled = false;

        yield return new WaitForSecondsRealtime(2f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}


