using System.Collections;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace HorrorJam.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class FlashlightController : MonoBehaviour
    {
        [Header("Flashlight Beam Settings")]
        [Tooltip("Diameter of the illuminated circle in screen pixels (560px as requested)")]
        [Range(100f, 1500f)]
        public float diameter = 560f;

        [Tooltip("Softness/feather of the light border in pixels")]
        [Range(10f, 300f)]
        public float softness = 90f;

        [Tooltip("Ambient brightness outside the circle (0 = pitch black)")]
        [Range(0f, 1f)]
        public float ambientDarkness = 0.0f;

        [Header("Full Screen Flicker Event")]
        [Tooltip("If true, the light periodically surges and flickers to reveal the whole screen")]
        public bool enableFullScreenFlicker = true;

        [Tooltip("Min seconds between random full-screen flickers")]
        public float minFlickerInterval = 5f;

        [Tooltip("Max seconds between random full-screen flickers")]
        public float maxFlickerInterval = 12f;

        [Tooltip("How long the whole screen stays fully illuminated during the main burst (seconds)")]
        public float fullLightDuration = 0.5f;

        [Header("Flicker Audio")]
        public AudioClip flickerSfx;
        public AudioSource audioSource;
        [Range(0f, 1f)]
        public float flickerVolume = 0.85f;

        [Header("Motion & Smoothing")]
        [Tooltip("Smoothly lags the flashlight slightly behind mouse for realistic feel")]
        public bool smoothFollow = true;

        [Tooltip("Speed of the smooth lag (higher = faster)")]
        public float followSpeed = 28f;

        [Header("Shader Reference")]
        public Shader flashlightShader;

        private Image targetImage;
        private Material runtimeMaterial;
        private Vector2 currentPos;
        private bool hasInitializedPos = false;
        private float currentGlobalFlash = 0.0f;
        private Coroutine flickerCoroutine;
        private Coroutine loopCoroutine;

        private static readonly int ShaderProp_MousePixelPos = Shader.PropertyToID("_MousePixelPos");
        private static readonly int ShaderProp_Radius = Shader.PropertyToID("_Radius");
        private static readonly int ShaderProp_Softness = Shader.PropertyToID("_Softness");
        private static readonly int ShaderProp_AmbientDarkness = Shader.PropertyToID("_AmbientDarkness");
        private static readonly int ShaderProp_GlobalFlash = Shader.PropertyToID("_GlobalFlash");

        private void Awake()
        {
            SetupMaterial();
            SetupAudio();
        }

        private void Start()
        {
            if (Application.isPlaying && enableFullScreenFlicker)
            {
                loopCoroutine = StartCoroutine(PeriodicFlickerLoop());
            }
        }

        private void OnEnable()
        {
            SetupMaterial();
            hasInitializedPos = false;
            currentGlobalFlash = 0.0f;

            if (Application.isPlaying && enableFullScreenFlicker && loopCoroutine == null)
            {
                loopCoroutine = StartCoroutine(PeriodicFlickerLoop());
            }
        }

        private void OnDisable()
        {
            if (loopCoroutine != null)
            {
                StopCoroutine(loopCoroutine);
                loopCoroutine = null;
            }
            if (flickerCoroutine != null)
            {
                StopCoroutine(flickerCoroutine);
                flickerCoroutine = null;
            }
            currentGlobalFlash = 0.0f;
            CleanupMaterial();
        }

        private void OnDestroy()
        {
            CleanupMaterial();
        }

        private void SetupMaterial()
        {
            if (targetImage == null)
            {
                targetImage = GetComponent<Image>();
            }

            if (flashlightShader == null)
            {
                flashlightShader = Shader.Find("UI/HorrorFlashlight");
            }

            if (flashlightShader != null && runtimeMaterial == null)
            {
                runtimeMaterial = new Material(flashlightShader);
                runtimeMaterial.name = "Flashlight_Runtime_Mat";
                runtimeMaterial.hideFlags = HideFlags.DontSave;
                if (targetImage != null)
                {
                    targetImage.material = runtimeMaterial;
                }
            }
        }

        private void SetupAudio()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    MainMenuController mm = Object.FindFirstObjectByType<MainMenuController>();
                    if (mm != null && mm.audioSource != null)
                    {
                        audioSource = mm.audioSource;
                    }
                }
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                }
            }

            if (flickerSfx == null)
            {
#if UNITY_EDITOR
                flickerSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FOR GAYJAM/NON/Audio/Light_Flicker.wav");
#endif
                if (flickerSfx == null)
                {
                    flickerSfx = CreateProceduralFlickerClip();
                }
            }
        }

        private void CleanupMaterial()
        {
            if (targetImage != null && targetImage.material == runtimeMaterial)
            {
                targetImage.material = null;
            }

            if (runtimeMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(runtimeMaterial);
                else
                    DestroyImmediate(runtimeMaterial);

                runtimeMaterial = null;
            }
        }

        private void Update()
        {
            if (runtimeMaterial == null)
            {
                SetupMaterial();
                if (runtimeMaterial == null) return;
            }

            Vector2 targetPos = GetMouseScreenPosition();

            if (!hasInitializedPos)
            {
                currentPos = targetPos;
                hasInitializedPos = true;
            }
            else
            {
                if (smoothFollow && Application.isPlaying)
                {
                    currentPos = Vector2.Lerp(currentPos, targetPos, Time.unscaledDeltaTime * followSpeed);
                }
                else
                {
                    currentPos = targetPos;
                }
            }

            // Radius is half diameter (560px / 2 = 280px)
            float radius = diameter * 0.5f;

            runtimeMaterial.SetVector(ShaderProp_MousePixelPos, new Vector4(currentPos.x, currentPos.y, 0f, 0f));
            runtimeMaterial.SetFloat(ShaderProp_Radius, radius);
            runtimeMaterial.SetFloat(ShaderProp_Softness, softness);
            runtimeMaterial.SetFloat(ShaderProp_AmbientDarkness, ambientDarkness);
            runtimeMaterial.SetFloat(ShaderProp_GlobalFlash, currentGlobalFlash);
        }

        private Vector2 GetMouseScreenPosition()
        {
            Vector2 mousePos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Mouse.current != null)
            {
                mousePos = Mouse.current.position.ReadValue();
            }
            else
            {
                mousePos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
#else
            mousePos = Input.mousePosition;
#endif

            // Fallback if mouse hasn't entered window yet
            if (mousePos.x <= 0f && mousePos.y <= 0f)
            {
                mousePos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            return mousePos;
        }

        private IEnumerator PeriodicFlickerLoop()
        {
            while (true)
            {
                float waitTime = Random.Range(minFlickerInterval, maxFlickerInterval);
                yield return new WaitForSecondsRealtime(waitTime);

                yield return StartCoroutine(DoFullFlickerRoutine());
            }
        }

        [ContextMenu("Trigger Full Flicker Now")]
        public void TriggerFullFlicker()
        {
            if (flickerCoroutine != null)
                StopCoroutine(flickerCoroutine);

            if (gameObject.activeInHierarchy)
            {
                flickerCoroutine = StartCoroutine(DoFullFlickerRoutine());
            }
        }

        private IEnumerator DoFullFlickerRoutine()
        {
            if (audioSource != null && flickerSfx != null)
            {
                audioSource.PlayOneShot(flickerSfx, flickerVolume);
            }

            // 1. Initial subtle electrical spark & flash
            currentGlobalFlash = 0.55f;
            yield return new WaitForSecondsRealtime(0.06f);

            // 2. Dip back to dark
            currentGlobalFlash = 0.0f;
            yield return new WaitForSecondsRealtime(0.06f);

            // 3. Second sharper pre-ignition spark
            currentGlobalFlash = 0.85f;
            yield return new WaitForSecondsRealtime(0.08f);

            // 4. Quick cut
            currentGlobalFlash = 0.0f;
            yield return new WaitForSecondsRealtime(0.05f);

            // 5. MAIN BURST: Entire screen becomes fully visible for fullLightDuration!
            float elapsed = 0f;
            while (elapsed < fullLightDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                // Realistic fluorescent tube buzzing micro-jitter
                float microJitter = (Mathf.PerlinNoise(Time.unscaledTime * 40f, 0f) - 0.5f) * 0.12f;
                currentGlobalFlash = Mathf.Clamp(1.0f + microJitter, 0.85f, 1.15f);
                yield return null;
            }

            // 6. Dying electrical crackle & dip
            currentGlobalFlash = 0.2f;
            yield return new WaitForSecondsRealtime(0.04f);

            currentGlobalFlash = 0.65f;
            yield return new WaitForSecondsRealtime(0.04f);

            // 7. Light dies out, returning completely to mouse flashlight
            currentGlobalFlash = 0.0f;
        }

        private static AudioClip CreateProceduralFlickerClip()
        {
            int sampleRate = 44100;
            float duration = 1.3f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            System.Random rand = new System.Random(99);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.3f;

                float burst = 0f;
                if (t >= 0.04f && t < 0.14f) burst = 0.7f;
                else if (t >= 0.20f && t < 0.34f) burst = 0.8f;
                else if (t >= 0.40f && t < 1.0f) burst = 0.95f;
                else if (t >= 1.0f && t < 1.2f) burst = 0.3f * Mathf.Exp(-(t - 1.0f) * 12f);

                float noise = ((float)rand.NextDouble() * 2f - 1f) * 0.25f * burst;
                float spark = 0f;
                if ((t >= 0.04f && t < 0.07f) || (t >= 0.20f && t < 0.23f) || (t >= 0.40f && t < 0.44f))
                {
                    spark = ((float)rand.NextDouble() * 2f - 1f) * 0.7f;
                }

                samples[i] = Mathf.Clamp((hum * burst * 0.6f + noise + spark) * 0.85f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("UI_Flicker_Procedural", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (runtimeMaterial != null)
            {
                runtimeMaterial.SetFloat(ShaderProp_Radius, diameter * 0.5f);
                runtimeMaterial.SetFloat(ShaderProp_Softness, softness);
                runtimeMaterial.SetFloat(ShaderProp_AmbientDarkness, ambientDarkness);
                runtimeMaterial.SetFloat(ShaderProp_GlobalFlash, currentGlobalFlash);
            }
        }
#endif
    }
}
