using UnityEngine;

public class PlaySoundOnParticleCollide : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("AudioSource to play sound from. If unassigned, one will be found or added automatically.")]
    public AudioSource a;
    [Tooltip("Audio clip to play when a particle collides.")]
    public AudioClip sound;
    [Range(0f, 1f)]
    public float volume = 1.0f;

    [Header("Rate Limit & Variety")]
    [Tooltip("Minimum time (in seconds) between sounds to prevent ear-rape spam from multiple particles colliding at once.")]
    public float minInterval = 0.08f;
    [Tooltip("Add slight random pitch variation to make repeated collisions sound natural.")]
    public bool randomizePitch = true;
    [Range(0.5f, 1.5f)]
    public float minPitch = 0.9f;
    [Range(0.5f, 1.5f)]
    public float maxPitch = 1.1f;

    private float nextPlayTime = 0f;

    void Awake()
    {
        EnsureAudioSource();
    }

    void Start()
    {
        EnsureAudioSource();

        // If attached to a ParticleSystem, check and configure collision settings
        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var collision = ps.collision;
            if (!collision.enabled)
            {
                Debug.LogWarning($"[PlaySoundOnParticleCollide] on '{gameObject.name}': The Particle System's 'Collision' module is DISABLED in the Inspector! Please enable 'Collision' and set Mode to '2D' for collisions to work.", this);
            }
            else if (!collision.sendCollisionMessages)
            {
                // Auto-enable Send Collision Messages so Unity sends OnParticleCollision callbacks
                collision.sendCollisionMessages = true;
            }
        }
    }

    void OnParticleCollision(GameObject other)
    {
        if (Time.time < nextPlayTime) return;
        if (sound == null) return;

        EnsureAudioSource();
        if (a == null) return;

        nextPlayTime = Time.time + minInterval;

        if (randomizePitch)
        {
            a.pitch = Random.Range(minPitch, maxPitch);
        }
        else
        {
            a.pitch = 1.0f;
        }

        a.PlayOneShot(sound, volume);
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
            a.spatialBlend = 1f; // 2D sound so camera distance does not mute it
        }
    }
}
