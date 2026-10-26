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

    void Start()
    {
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

        Destroy(gameObject, lifetime);
    }

    void LateUpdate()
    {
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

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (hideStatus == null)
            {
                hideStatus = FindObjectOfType<Hided>();
            }

            if (hideStatus == null || !hideStatus.IsPlayerHide)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
}


