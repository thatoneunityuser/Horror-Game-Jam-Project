using System.Collections;
using UnityEngine;
using TMPro;

public class TypeWriter : MonoBehaviour
{
    #region Inspector
    [Header("Text")]
    [Tooltip("Leave empty to use TextMeshPro on this object.")]
    public TextMeshProUGUI text;
    [TextArea(2, 5)]
    public string textToType;

    [Header("Typing")]
    [Min(0f)]
    public float typeSpeed = 0.05f;
    [Tooltip("Ignore Time.timeScale (works while paused / time-stop).")]
    public bool useUnscaledTime = false;

    [Header("Audio")]
    [Tooltip("AudioSource to play typing blip sound. Auto found or assigned from DialogSystem.")]
    public AudioSource audioSource;
    [Tooltip("Audio clip to play per character.")]
    public AudioClip typingClip;
    [Tooltip("If true, play sound on character typed.")]
    public bool playAudioOnType = false;
    #endregion

    #region Runtime
    public bool isTyping { get; private set; }
    private Coroutine typingCoroutine;
    private object cachedWait;
    private float cachedWaitSpeed = -1f;
    private bool cachedWaitUnscaled;
    private AudioClip defaultTypingClip;
    private bool defaultPlayAudioOnType;
    #endregion

    #region Unity
    void Awake()
    {
        defaultTypingClip = typingClip;
        defaultPlayAudioOnType = playAudioOnType;
    }

    void OnEnable()
    {
        if (text == null)
        {
            text = GetComponent<TextMeshProUGUI>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (!string.IsNullOrEmpty(textToType))
        {
            StartTyping(textToType, typeSpeed);
        }
        else
        {
            ResetVisibleText();
        }
    }

    void OnDisable()
    {
        StopTypingCoroutine();
        ClearTypingAudio();
        isTyping = false;
    }
    #endregion

    #region Public Functions
    public void StartTyping(string textToType, float speed = -1f)
    {
        if (speed > 0f)
        {
            typeSpeed = speed;
        }

        this.textToType = ThaiFontFixer.Adjust(textToType);
        StopTypingCoroutine();
        typingCoroutine = StartCoroutine(TypeText());
    }

    public void SkipTyping(string fullText)
    {
        StopTypingCoroutine();
        ClearTypingAudio();

        if (text != null)
        {
            string adjusted = ThaiFontFixer.Adjust(fullText);
            text.text = adjusted;
            text.maxVisibleCharacters = 99999;
        }

        isTyping = false;
    }

    public void SetTypingAudio(AudioSource source, AudioClip clip)
    {
        if (source != null)
        {
            audioSource = source;
        }
        typingClip = clip;
        playAudioOnType = (clip != null && audioSource != null);
    }

    public void ClearTypingAudio()
    {
        typingClip = defaultTypingClip;
        playAudioOnType = defaultPlayAudioOnType;
    }
    #endregion

    #region Private Functions
    void StopTypingCoroutine()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
    }

    void ResetVisibleText()
    {
        if (text == null)
        {
            return;
        }

        text.maxVisibleCharacters = 0;
    }

    object GetWait()
    {
        if (cachedWait == null || cachedWaitSpeed != typeSpeed || cachedWaitUnscaled != useUnscaledTime)
        {
            cachedWaitSpeed = typeSpeed;
            cachedWaitUnscaled = useUnscaledTime;
            cachedWait = useUnscaledTime
                ? (object)new WaitForSecondsRealtime(typeSpeed)
                : new WaitForSeconds(typeSpeed);
        }

        return cachedWait;
    }
    #endregion

    #region Coroutines
    IEnumerator TypeText()
    {
        isTyping = true;

        if (text != null)
        {
            text.text = textToType;
            text.maxVisibleCharacters = 0;
            text.ForceMeshUpdate();

            int totalLength = text.textInfo != null && text.textInfo.characterCount > 0
                ? text.textInfo.characterCount
                : textToType.Length;

            object wait = GetWait();

            for (int i = 0; i <= totalLength; i++)
            {
                text.maxVisibleCharacters = i;

                if (i < totalLength)
                {
                    char c = ' ';
                    bool validChar = false;
                    if (text.textInfo != null && i < text.textInfo.characterInfo.Length)
                    {
                        c = text.textInfo.characterInfo[i].character;
                        validChar = true;
                    }
                    else if (i < textToType.Length)
                    {
                        c = textToType[i];
                        validChar = true;
                    }

                    if (validChar)
                    {
                        if (c == '\u200B')
                        {
                            continue;
                        }

                        if (playAudioOnType && audioSource != null && typingClip != null && !char.IsWhiteSpace(c))
                        {
                            audioSource.PlayOneShot(typingClip);
                        }
                    }
                }

                yield return wait;
            }

            text.maxVisibleCharacters = totalLength;
        }

        isTyping = false;
        typingCoroutine = null;
        ClearTypingAudio();
    }
    #endregion
}
