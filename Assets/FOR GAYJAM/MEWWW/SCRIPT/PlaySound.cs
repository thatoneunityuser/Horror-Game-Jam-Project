using UnityEngine;

public class PlaySound : MonoBehaviour
{
    public AudioSource a;
    public AudioClip sound;
    [Range(0f, 1f)] public float volume = 1.0f;

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
            a.spatialBlend = 1f; // 2D playback so camera distance does not mute it
        }
    }
}
