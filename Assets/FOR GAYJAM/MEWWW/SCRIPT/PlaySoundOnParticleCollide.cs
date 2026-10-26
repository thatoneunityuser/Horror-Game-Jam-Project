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

    [Header("3D Sound Settings")]
    [Range(0f, 1f)]
    [Tooltip("1 = Full 3D spatial sound with panning and distance attenuation.")]
    public float spatialBlend = 1.0f;
    [Tooltip("Distance below which audio is at 100% volume. Since the 2D Camera is at Z = -10, this should be at least 10 so it's full volume when the camera is nearby.")]
    public float minDistance = 10f;
    [Tooltip("Distance at which the 3D sound fades out.")]
    public float maxDistance = 35f;
    [Tooltip("Linear rolloff provides smooth, natural 3D distance falloff.")]
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;

    [Header("Rate Limit & Variety")]
    [Tooltip("Minimum time (in seconds) between sounds to prevent spam from multiple particles colliding at once.")]
    public float minInterval = 0.08f;
    [Tooltip("Add slight random pitch variation to make repeated collisions sound natural.")]
    public bool randomizePitch = true;
    [Range(0.5f, 1.5f)]
    public float minPitch = 0.9f;
    [Range(0.5f, 1.5f)]
    public float maxPitch = 1.1f;

    [Header("Debug")]
    [Tooltip("Enable to print debug messages to Console whenever a particle collision occurs.")]
    public bool debug = false;

    private float nextPlayTime = 0f;
    private ParticleSystem cachedPs;

    void Awake()
    {
        EnsureAudioSource();
    }

    void Start()
    {
        EnsureAudioSource();

        if (sound == null)
        {
            Debug.LogWarning($"[PlaySoundOnParticleCollide] on '{gameObject.name}': No AudioClip assigned in 'Sound'!", this);
        }

        // If attached to a ParticleSystem (or parent/child), check and configure collision settings
        cachedPs = GetComponent<ParticleSystem>();
        if (cachedPs == null) cachedPs = GetComponentInChildren<ParticleSystem>();
        if (cachedPs != null)
        {
            var collision = cachedPs.collision;
            if (!collision.enabled)
            {
                Debug.LogWarning($"[PlaySoundOnParticleCollide] on '{gameObject.name}': Particle Collision module was disabled. Enabling it automatically.", this);
                collision.enabled = true;
            }
            if (!collision.sendCollisionMessages)
            {
                collision.sendCollisionMessages = true;
            }
        }
    }

    void OnParticleCollision(GameObject other)
    {
        if (debug)
        {
            Debug.Log($"[PlaySoundOnParticleCollide] Particle collision with '{other?.name}' at time {Time.time}", this);
        }

        if (Time.time < nextPlayTime) return;
        if (sound == null)
        {
            if (debug) Debug.LogWarning($"[PlaySoundOnParticleCollide] on '{gameObject.name}': Sound is null, cannot play sound!", this);
            return;
        }

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
            a.spatialBlend = spatialBlend;

            // Apply 3D distance settings so Z = -10 camera doesn't mute the sound
            if (spatialBlend > 0f)
            {
                a.rolloffMode = rolloffMode;
                a.minDistance = minDistance;
                a.maxDistance = maxDistance;
                a.dopplerLevel = 0f; // Prevent unnatural pitch bends on static water drips
            }
        }
    }
}
