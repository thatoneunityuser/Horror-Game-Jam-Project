using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace HorrorJam.UI
{
    public class PauseMenuController : MonoBehaviour
    {
        public static PauseMenuController Instance { get; private set; }

        [Header("Scene Navigation")]
        [Tooltip("Name of the Main Menu scene to return to")]
        public string mainMenuSceneName = "Menu GayJam";

        [Header("UI Panels & Container")]
        [Tooltip("The main pause menu overlay object")]
        public GameObject pausePanel;

        [Header("Buttons")]
        public Button btnResume;
        public Button btnMainMenu;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip hoverSfx;
        public AudioClip clickSfx;
        public AudioClip pauseOpenSfx;
        [Range(0f, 1f)] public float hoverVolume = 0.6f;
        [Range(0f, 1f)] public float clickVolume = 0.85f;

        [Header("Transition Screen Fader")]
        public CanvasGroup screenFader;
        public float fadeDuration = 0.4f;

        [Header("State")]
        public bool allowTogglePause = true;

        public bool IsPaused { get; private set; } = false;

        private float lastHoverTime = -1f;
        private float lastClickTime = -1f;
        private bool isTransitioning = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                // If another instance already exists in scene, destroy this duplicate
                Destroy(gameObject);
                return;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.ignoreListenerPause = true;
            }

            // Fallback procedural UI clips if audio files are unassigned
            if (hoverSfx == null)
            {
                hoverSfx = CreateProceduralHoverClip();
            }
            if (clickSfx == null)
            {
                clickSfx = CreateProceduralClickClip();
            }

            if (screenFader != null)
            {
                screenFader.alpha = 0f;
                screenFader.blocksRaycasts = false;
            }

            SetupButtons();
        }

        private void OnEnable()
        {
            SetupButtons();
        }

        private void SetupButtons()
        {
            if (btnResume != null)
            {
                btnResume.onClick.RemoveListener(ResumeGame);
                btnResume.onClick.AddListener(ResumeGame);
            }

            if (btnMainMenu != null)
            {
                btnMainMenu.onClick.RemoveListener(ReturnToMainMenu);
                btnMainMenu.onClick.AddListener(ReturnToMainMenu);
            }
        }

        private void Start()
        {
            SetupButtons();

            // Ensure game starts unpaused and panel is hidden
            if (pausePanel != null && pausePanel != gameObject)
            {
                pausePanel.SetActive(false);
            }
            else
            {
                // If pausePanel is this root GameObject, hide child canvas/panel instead
                SetPauseVisualsActive(false);
            }

            Time.timeScale = 1f;
            IsPaused = false;
        }

        private void Update()
        {
            if (!allowTogglePause || isTransitioning) return;

            if (WasEscapePressed())
            {
                TogglePause();
            }
        }

        public static bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;
#else
            if (Input.GetKeyDown(KeyCode.Escape))
                return true;
#endif
            return false;
        }

        private int lastToggleFrame = -1;

        public void TogglePause()
        {
            if (Time.frameCount == lastToggleFrame) return;
            lastToggleFrame = Time.frameCount;

            if (IsPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        public void PauseGame()
        {
            if (IsPaused || isTransitioning) return;

            IsPaused = true;
            Time.timeScale = 0f;

            SetPauseVisualsActive(true);

            // Make cursor visible for menu interaction
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (pauseOpenSfx != null && audioSource != null)
            {
                audioSource.PlayOneShot(pauseOpenSfx, 0.7f);
            }
            else
            {
                PlayClickSfx();
            }

            // Focus on Resume button for controller/keyboard support
            StartCoroutine(SelectDefaultButtonRoutine());
        }

        public void ResumeGame()
        {
            if (!IsPaused || isTransitioning) return;

            PlayClickSfx();

            IsPaused = false;
            Time.timeScale = 1f;

            SetPauseVisualsActive(false);

            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void ReturnToMainMenu()
        {
            if (isTransitioning) return;

            PlayClickSfx();
            isTransitioning = true;

            // CRITICAL: Always reset timeScale back to 1 before changing scenes!
            Time.timeScale = 1f;

            StartCoroutine(FadeOutAndLoad(mainMenuSceneName));
        }

        private void SetPauseVisualsActive(bool active)
        {
            if (pausePanel != null && pausePanel != gameObject)
            {
                pausePanel.SetActive(active);
            }
            else
            {
                // If attached to root, toggle CanvasGroup or Image/Children
                CanvasGroup group = GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = active ? 1f : 0f;
                    group.interactable = active;
                    group.blocksRaycasts = active;
                }
                else
                {
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        Transform child = transform.GetChild(i);
                        // Don't disable screen fader
                        if (screenFader == null || child != screenFader.transform)
                        {
                            child.gameObject.SetActive(active);
                        }
                    }
                }
            }
        }

        private System.Collections.IEnumerator SelectDefaultButtonRoutine()
        {
            yield return null;
            if (UnityEngine.EventSystems.EventSystem.current != null && btnResume != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(btnResume.gameObject);
            }
        }

        private System.Collections.IEnumerator FadeOutAndLoad(string sceneName)
        {
            if (screenFader != null)
            {
                screenFader.blocksRaycasts = true;
                float t = 0f;
                while (t < fadeDuration)
                {
                    t += Time.unscaledDeltaTime;
                    screenFader.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
                    yield return null;
                }
                screenFader.alpha = 1f;
            }

            yield return new WaitForSecondsRealtime(0.08f);
            SceneManager.LoadScene(sceneName);
        }

        public void PlayHoverSfx()
        {
            if (Time.unscaledTime - lastHoverTime < 0.04f) return;
            lastHoverTime = Time.unscaledTime;

            if (audioSource != null && hoverSfx != null)
                audioSource.PlayOneShot(hoverSfx, hoverVolume);
        }

        public void PlayClickSfx()
        {
            if (Time.unscaledTime - lastClickTime < 0.04f) return;
            lastClickTime = Time.unscaledTime;

            if (audioSource != null && clickSfx != null)
                audioSource.PlayOneShot(clickSfx, clickVolume);
        }

        private static AudioClip CreateProceduralHoverClip()
        {
            int sampleRate = 44100;
            float duration = 0.08f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 40f);
                float freq = Mathf.Lerp(260f, 130f, t / duration);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                samples[i] = wave * env * 0.6f;
            }
            AudioClip clip = AudioClip.Create("UI_Hover_Procedural", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateProceduralClickClip()
        {
            int sampleRate = 44100;
            float duration = 0.07f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            System.Random rand = new System.Random(42);
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 55f);
                float freq = Mathf.Lerp(520f, 150f, t / duration);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = ((float)rand.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 100f) * 0.35f;
                samples[i] = (wave * 0.7f + noise) * env * 0.85f;
            }
            AudioClip clip = AudioClip.Create("UI_Click_Procedural", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
