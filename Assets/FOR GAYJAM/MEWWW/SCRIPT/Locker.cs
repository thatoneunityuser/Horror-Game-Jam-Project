using UnityEngine;

public class Locker : MonoBehaviour
{
    public Vector3 LockerUseRange = new Vector3(2f, 2f, 2f);
    public Transform PlayerPos;
    public Animator animator;
    public Transform HideSpot;

    [Header("Audio")]
    public AudioSource a;
    public AudioClip On;
    public AudioClip Off;

    private Hided hided;
    private bool isPlayerInsideThisLocker = false;
    public bool IsPlayerInsideThisLocker => isPlayerInsideThisLocker;
    private SideScrollMovement playerMovement;
    private Rigidbody2D rb;

    void Awake()
    {
        EnsureAudioSource();
#if UNITY_EDITOR
        if (On == null)
            On = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FOR GAYJAM/MEWWW/AUDIOOOO/door open.wav");
        if (Off == null)
            Off = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FOR GAYJAM/MEWWW/AUDIOOOO/door close.wav");
#endif
    }

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        EnsureAudioSource();
        hided = FindObjectOfType<Hided>();
        FindPlayerIfNeeded();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (hided == null)
            {
                hided = FindObjectOfType<Hided>();
            }

            FindPlayerIfNeeded();

            if (hided == null) return;

            // If player is already inside this locker, press E to exit
            if (isPlayerInsideThisLocker)
            {
                if (animator != null)
                {
                    animator.Play("Locker Interacted");
                }
                else
                {
                    close();
                }
                ExitLocker();
            }
            // If player is in range of this locker and not currently hiding, press E to enter
            else if (IsPlayerInRange() && !hided.IsPlayerHide)
            {
                if (animator != null)
                {
                    animator.Play("Locker Interacted");
                }
                else
                {
                    open();
                }
                EnterLocker();
            }
        }

        // If player was unhidden by time limit, reset local flag and restore player controls
        if (isPlayerInsideThisLocker && (hided != null && !hided.IsPlayerHide))
        {
            ExitLocker();
        }
    }

    void EnterLocker()
    {
        if (PlayerPos == null) return;

        if (hided != null)
        {
            hided.IsPlayerHide = true;
        }
        isPlayerInsideThisLocker = true;

        // Snap player position to locker
        PlayerPos.position = HideSpot.position;

        if (playerMovement != null)
        {
            playerMovement.isMoving = false;
            playerMovement.enabled = false;
        }
        if (rb != null)
        {
            rb.gravityScale = 0;
            rb.linearVelocity = Vector2.zero;
        }
    }

    void ExitLocker()
    {
        if (hided != null)
        {
            hided.IsPlayerHide = false;
        }
        isPlayerInsideThisLocker = false;

        if (playerMovement != null)
        {
            playerMovement.isMoving = false;
            playerMovement.enabled = true;
        }
        if (rb != null)
        {
            rb.gravityScale = 1;
            rb.linearVelocity = Vector2.zero;
        }
    }

    void FindPlayerIfNeeded()
    {
        if (PlayerPos == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                PlayerPos = player.transform;
            }
        }

        if (PlayerPos != null)
        {
            if (playerMovement == null) playerMovement = PlayerPos.GetComponent<SideScrollMovement>();
            if (rb == null) rb = PlayerPos.GetComponent<Rigidbody2D>();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, LockerUseRange);
    }

    bool IsPlayerInRange()
    {
        if (PlayerPos == null) return false;
        return Vector3.Distance(PlayerPos.position, transform.position) < LockerUseRange.x;
    }
    public void open()
    {
        EnsureAudioSource();
        if (a != null && On != null)
        {
            a.PlayOneShot(On);
        }
    }

    public void close()
    {
        EnsureAudioSource();
        if (a != null && Off != null)
        {
            a.PlayOneShot(Off);
        }
    }

    private void EnsureAudioSource()
    {
        if (a == null)
        {
            a = GetComponent<AudioSource>();
            if (a == null) a = GetComponentInChildren<AudioSource>();
            if (a == null) a = gameObject.AddComponent<AudioSource>();
        }
        if (a != null)
        {
            a.playOnAwake = false;
            a.spatialBlend = 0f; // 2D playback so distance to Main Camera doesn't mute it
        }
    }
}
