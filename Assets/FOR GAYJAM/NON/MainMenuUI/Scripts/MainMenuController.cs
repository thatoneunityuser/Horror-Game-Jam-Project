using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace HorrorJam.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Navigation")]
        [Tooltip("Target scene to load when clicking play buttons")]
        public string gameplaySceneName = "MAIN GAMEPLAY";

        [Header("Main Buttons")]
        public Button btnLoadCheckpoint;
        public Button btnExplore;
        public Button btnRestart;

        [Header("Bottom Icon Buttons")]
        public Button btnSmiley;
        public Button btnSettings;
        public Button btnQuit;

        [Header("Panels")]
        public GameObject settingsPanel;
        public GameObject creditsPanel;
        public GameObject quitDialog;

        [Header("Settings Controls")]
        public Slider volumeSlider;
        public Toggle fullscreenToggle;
        public TMP_Dropdown qualityDropdown;
        public Button btnCloseSettings;

        [Header("Credits Controls")]
        public Button btnCloseCredits;

        [Header("Quit Controls")]
        public Button btnConfirmQuit;
        public Button btnCancelQuit;

        [Header("Transition & Audio")]
        public CanvasGroup screenFader;
        public float fadeDuration = 0.6f;
        public AudioSource audioSource;
        public AudioClip hoverSfx;
        public AudioClip clickSfx;

        private bool isLoadingScene = false;

        private void Awake()
        {
            if (screenFader != null)
            {
                screenFader.alpha = 1f;
                screenFader.blocksRaycasts = true;
            }
        }

        private void Start()
        {
            // Close all popups initially
            CloseAllPanels();

            // Setup button listeners
            if (btnLoadCheckpoint != null)
                btnLoadCheckpoint.onClick.AddListener(() => OnClickLoadCheckpoint());

            if (btnExplore != null)
                btnExplore.onClick.AddListener(() => OnClickExplore());

            if (btnRestart != null)
                btnRestart.onClick.AddListener(() => OnClickRestart());

            if (btnSmiley != null)
                btnSmiley.onClick.AddListener(() => OpenCredits());

            if (btnSettings != null)
                btnSettings.onClick.AddListener(() => OpenSettings());

            if (btnQuit != null)
                btnQuit.onClick.AddListener(() => OpenQuitDialog());

            // Dialog buttons
            if (btnCloseSettings != null)
                btnCloseSettings.onClick.AddListener(() => CloseAllPanels());

            if (btnCloseCredits != null)
                btnCloseCredits.onClick.AddListener(() => CloseAllPanels());

            if (btnConfirmQuit != null)
                btnConfirmQuit.onClick.AddListener(() => ConfirmQuit());

            if (btnCancelQuit != null)
                btnCancelQuit.onClick.AddListener(() => CloseAllPanels());

            // Settings initialize
            InitSettings();

            // Fade in from black
            StartCoroutine(FadeInRoutine());

            // Set default selected button for keyboard / controller
            StartCoroutine(SelectDefaultButtonRoutine());
        }

        private void Update()
        {
            // Handle Escape key to close popups or open quit dialog
            bool escPressed = false;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                escPressed = true;
#else
            if (Input.GetKeyDown(KeyCode.Escape))
                escPressed = true;
#endif
            if (escPressed)
            {
                if (IsAnyPanelOpen())
                {
                    CloseAllPanels();
                    PlayClickSfx();
                }
                else
                {
                    OpenQuitDialog();
                }
            }
        }

        private IEnumerator SelectDefaultButtonRoutine()
        {
            yield return null;
            if (UnityEngine.EventSystems.EventSystem.current != null && btnExplore != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(btnExplore.gameObject);
            }
        }

        private void InitSettings()
        {
            // Master volume
            float savedVol = PlayerPrefs.GetFloat("Horror_MasterVolume", 1f);
            AudioListener.volume = savedVol;
            if (volumeSlider != null)
            {
                volumeSlider.value = savedVol;
                volumeSlider.onValueChanged.AddListener((val) =>
                {
                    AudioListener.volume = val;
                    PlayerPrefs.SetFloat("Horror_MasterVolume", val);
                });
            }

            // Fullscreen
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                fullscreenToggle.onValueChanged.AddListener((isFull) =>
                {
                    Screen.fullScreen = isFull;
                    PlayClickSfx();
                });
            }

            // Quality
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                var options = new System.Collections.Generic.List<string>(QualitySettings.names);
                qualityDropdown.AddOptions(options);
                qualityDropdown.value = QualitySettings.GetQualityLevel();
                qualityDropdown.RefreshShownValue();
                qualityDropdown.onValueChanged.AddListener((lvl) =>
                {
                    QualitySettings.SetQualityLevel(lvl, true);
                    PlayClickSfx();
                });
            }
        }

        // --- Button Actions ---
        public void OnClickLoadCheckpoint()
        {
            PlayClickSfx();
            LoadScene(gameplaySceneName);
        }

        public void OnClickExplore()
        {
            PlayClickSfx();
            LoadScene(gameplaySceneName);
        }

        public void OnClickRestart()
        {
            PlayClickSfx();
            // Could reset checkpoint/save data here
            PlayerPrefs.DeleteKey("PlayerCheckpoint");
            LoadScene(gameplaySceneName);
        }

        public void OpenSettings()
        {
            PlayClickSfx();
            CloseAllPanels();
            if (settingsPanel != null)
                settingsPanel.SetActive(true);
            if (btnCloseSettings != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(btnCloseSettings.gameObject);
        }

        public void OpenCredits()
        {
            PlayClickSfx();
            CloseAllPanels();
            if (creditsPanel != null)
                creditsPanel.SetActive(true);
            if (btnCloseCredits != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(btnCloseCredits.gameObject);
        }

        public void OpenQuitDialog()
        {
            PlayClickSfx();
            CloseAllPanels();
            if (quitDialog != null)
                quitDialog.SetActive(true);
            if (btnCancelQuit != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(btnCancelQuit.gameObject);
        }

        public void CloseAllPanels()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (quitDialog != null) quitDialog.SetActive(false);

            if (UnityEngine.EventSystems.EventSystem.current != null && btnExplore != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(btnExplore.gameObject);
        }

        public bool IsAnyPanelOpen()
        {
            return (settingsPanel != null && settingsPanel.activeSelf) ||
                   (creditsPanel != null && creditsPanel.activeSelf) ||
                   (quitDialog != null && quitDialog.activeSelf);
        }

        public void ConfirmQuit()
        {
            PlayClickSfx();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void PlayHoverSfx()
        {
            if (audioSource != null && hoverSfx != null)
                audioSource.PlayOneShot(hoverSfx, 0.6f);
        }

        public void PlayClickSfx()
        {
            if (audioSource != null && clickSfx != null)
                audioSource.PlayOneShot(clickSfx, 0.8f);
        }

        public void LoadScene(string sceneName)
        {
            if (isLoadingScene) return;
            StartCoroutine(FadeOutAndLoad(sceneName));
        }

        private IEnumerator FadeInRoutine()
        {
            if (screenFader == null) yield break;
            screenFader.blocksRaycasts = true;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                screenFader.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
                yield return null;
            }
            screenFader.alpha = 0f;
            screenFader.blocksRaycasts = false;
        }

        private IEnumerator FadeOutAndLoad(string sceneName)
        {
            isLoadingScene = true;
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
            yield return new WaitForSecondsRealtime(0.1f);
            SceneManager.LoadScene(sceneName);
        }
    }
}
