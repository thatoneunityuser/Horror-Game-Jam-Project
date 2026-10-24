using UnityEngine;
using System.Collections;

public class ThemeController : MonoBehaviour
{
    public AudioClip[] Theme;

    [Tooltip("How long the crossfade between tracks takes in seconds")]
    public float crossfadeDuration = 1.5f;

    // Two AudioSources — one plays, one fades in
    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;   // currently audible source
    private AudioSource inactiveSource; // used for the incoming track

    // Cache the last known phase so we only react when it actually changes
    private JohnnyHardMode.phasestate lastPhase = (JohnnyHardMode.phasestate)(-1);

    private Coroutine crossfadeCoroutine;

    void Awake()
    {
        // Create two AudioSources on this GameObject
        sourceA = gameObject.AddComponent<AudioSource>();
        sourceB = gameObject.AddComponent<AudioSource>();

        sourceA.loop = true;
        sourceB.loop = true;
        sourceA.playOnAwake = false;
        sourceB.playOnAwake = false;
        sourceA.volume = 0f;
        sourceB.volume = 0f;

        activeSource   = sourceA;
        inactiveSource = sourceB;
    }

    void Start()
    {
        // Pre-warm every clip so audio data is decoded/buffered BEFORE any phase transition.
        // We do this by briefly playing each clip at volume 0 and pausing it immediately.
        // This forces Unity to load the audio data on Start, not mid-game.
        if (Theme != null)
        {
            foreach (AudioClip clip in Theme)
            {
                if (clip == null) continue;
                // Play silent on the inactive source to trigger Unity's audio warm-up
                inactiveSource.clip = clip;
                inactiveSource.volume = 0f;
                inactiveSource.Play();
                inactiveSource.Pause();
            }
            inactiveSource.clip = null;
            inactiveSource.volume = 0f;
        }

        // Start Phase1 music immediately (phase is set to Spawn at first, 
        // so we manually kick off index 0 without waiting for a phase check)
        if (Theme != null && Theme.Length > 0 && Theme[0] != null)
        {
            activeSource.clip = Theme[0];
            activeSource.volume = 0.25f;
            activeSource.Play();
            lastPhase = JohnnyHardMode.phasestate.Spawn; // mark so first real change triggers correctly
        }
    }

    void Update()
    {
        if (JohnnyHardMode.Instance == null) return;

        JohnnyHardMode.phasestate currentPhase = JohnnyHardMode.Instance.currentPhase;

        // Only act when the phase has actually changed
        if (currentPhase == lastPhase) return;
        lastPhase = currentPhase;

        AudioClip targetClip = GetClipForPhase(currentPhase);
        if (targetClip == null) return;

        // Don't restart if already playing the same clip
        if (activeSource.clip == targetClip && activeSource.isPlaying) return;

        if (crossfadeCoroutine != null)
            StopCoroutine(crossfadeCoroutine);

        crossfadeCoroutine = StartCoroutine(CrossfadeTo(targetClip));
    }

    AudioClip GetClipForPhase(JohnnyHardMode.phasestate phase)
    {
        if (Theme == null || Theme.Length == 0) return null;

        switch (phase)
        {
            case JohnnyHardMode.phasestate.Phase2:
                return Theme.Length > 1 ? Theme[1] : null;

            case JohnnyHardMode.phasestate.Phase3:
                if (Theme.Length > 2) return Theme[2];
                if (Theme.Length > 1) return Theme[1];
                return Theme[0];

            default: // Spawn, Phase1
                return Theme[0];
        }
    }

    IEnumerator CrossfadeTo(AudioClip newClip)
    {
        // Set up the incoming source (already pre-warmed, no load stall)
        inactiveSource.clip = newClip;
        inactiveSource.volume = 0f;
        inactiveSource.Play();

        float elapsed = 0f;
        float startVolume = activeSource.volume;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / crossfadeDuration);

            inactiveSource.volume = t;
            activeSource.volume   = startVolume * (1f - t);

            yield return null;
        }

        // Snap to final state
        inactiveSource.volume = 0.5f;
        activeSource.volume   = 0f;
        activeSource.Stop();

        // Swap roles
        (activeSource, inactiveSource) = (inactiveSource, activeSource);
        crossfadeCoroutine = null;
    }
}