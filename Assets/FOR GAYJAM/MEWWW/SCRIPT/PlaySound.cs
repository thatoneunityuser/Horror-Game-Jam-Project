using UnityEngine;

public class PlaySound : MonoBehaviour
{
    public AudioSource a;
    public AudioClip sound;
    [Range(0f, 1f)] public float volume = 1.0f;

    [Header("3D Audio Distance Settings")]
    [Range(0f, 1f)] public float spatialBlend = 1.0f;
    public float minDistance = 10f;
    public float maxDistance = 35f;

    private float nextParticlePlayTime = 0f;

    void Awake()
    {
        EnsureAudioSource();
    }

    public void PlayTheSound()
    {
        EnsureAudioSource();
        if (a != null && sound != null)
        {
            a.PlayOneShot(sound, volume);
        }
    }

    void OnParticleCollision(GameObject other)
    {
        if (Time.time < nextParticlePlayTime) return;
        nextParticlePlayTime = Time.time + 0.08f;
        PlayTheSound();
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
            if (spatialBlend > 0f)
            {
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = minDistance;
                a.maxDistance = maxDistance;
                a.dopplerLevel = 0f;
            }
        }
    }
}
