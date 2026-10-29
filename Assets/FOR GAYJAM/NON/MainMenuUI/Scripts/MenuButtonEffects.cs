using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace HorrorJam.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class MenuButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Header("Colors")]
        public Color normalBorderColor = new Color(0.9f, 0.9f, 0.92f, 0.85f);
        public Color highlightBorderColor = new Color(1f, 1f, 1f, 1f);
        public Color normalTextColor = new Color(0.92f, 0.92f, 0.95f, 1f);
        public Color highlightTextColor = new Color(1f, 0.88f, 0.88f, 1f);

        [Header("Hover Animation")]
        public float hoverScale = 1.035f;
        public float animSpeed = 14f;
        public bool enableJitter = true;
        public float jitterIntensity = 0.75f;

        [Header("Progressive Reddening (Hover Over Time)")]
        [Tooltip("If enabled, hovering longer gradually turns the button blood red")]
        public bool enableProgressiveRed = true;

        [Tooltip("Seconds of continuous hover to reach maximum redness")]
        public float timeToMaxRed = 2.5f;

        [Tooltip("Maximum redness limit: 0 = none, 1.0 = full deep blood red")]
        [Range(0f, 1f)]
        public float maxRedLimit = 1.0f;

        [Tooltip("Target red color for the border when reaching limit")]
        public Color maxRedBorderColor = new Color(0.95f, 0.08f, 0.08f, 1f);

        [Tooltip("Target red color for the text when reaching limit")]
        public Color maxRedTextColor = new Color(1f, 0.18f, 0.18f, 1f);

        [Tooltip("How fast it cools down and returns to normal color when unhovered")]
        public float redCooldownSpeed = 3.5f;

        [Tooltip("Subtle horror pulse when reaching max redness")]
        public bool pulseAtMaxRed = true;

        [Header("Audio (Optional Overrides)")]
        public AudioClip customHoverSfx;
        public AudioClip customClickSfx;

        [Header("Components (Auto-detected if empty)")]
        public Image borderImage;
        public TMP_Text buttonText;

        private RectTransform rectTransform;
        private Vector3 originalScale;
        private Vector2 currentJitter = Vector2.zero;
        private bool isHovered = false;
        private bool isPressed = false;
        private MainMenuController controller;
        private PauseMenuController pauseController;
        private Button uiButton;
        private float hoverTimer = 0f;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            originalScale = rectTransform.localScale;

            if (borderImage == null)
                borderImage = GetComponent<Image>();

            if (buttonText == null)
                buttonText = GetComponentInChildren<TMP_Text>();

            controller = GetComponentInParent<MainMenuController>();
            if (controller == null)
                controller = Object.FindFirstObjectByType<MainMenuController>();

            pauseController = GetComponentInParent<PauseMenuController>();
            if (pauseController == null)
                pauseController = Object.FindFirstObjectByType<PauseMenuController>();

            uiButton = GetComponent<Button>();
            if (uiButton != null)
            {
                uiButton.onClick.AddListener(PlayClickSound);
            }

            ApplyVisuals(false, false);
        }

        private void OnEnable()
        {
            isHovered = false;
            isPressed = false;
            hoverTimer = 0f;
            currentJitter = Vector2.zero;
            if (rectTransform != null)
            {
                rectTransform.localScale = originalScale;
            }
            ApplyVisuals(false, false);
        }

        private void OnDisable()
        {
            if (rectTransform != null && currentJitter != Vector2.zero)
            {
                rectTransform.anchoredPosition -= currentJitter;
                currentJitter = Vector2.zero;
            }
        }

        private void Update()
        {
            float targetScaleMultiplier = 1f;
            if (isPressed)
                targetScaleMultiplier = 0.96f;
            else if (isHovered)
                targetScaleMultiplier = hoverScale;

            Vector3 targetScale = originalScale * targetScaleMultiplier;
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * animSpeed);

            // Progressive reddening progress calculation with limit
            if (isHovered && enableProgressiveRed)
            {
                hoverTimer += Time.unscaledDeltaTime;
            }
            else
            {
                hoverTimer = Mathf.Max(0f, hoverTimer - Time.unscaledDeltaTime * redCooldownSpeed);
            }

            float rawProgress = timeToMaxRed > 0.001f ? (hoverTimer / timeToMaxRed) : 1f;
            float redAmount = Mathf.Clamp(rawProgress, 0f, maxRedLimit);

            // Jitter as a delta so layout-assigned positions are never overridden
            Vector2 targetJitter = Vector2.zero;
            if (isHovered && enableJitter && !isPressed)
            {
                // Jitter intensifies slightly as the button gets red-hot
                float effectiveJitter = jitterIntensity * (1f + redAmount * 0.7f);
                float jx = (Mathf.PerlinNoise(Time.unscaledTime * 25f, 0f) - 0.5f) * 2f * effectiveJitter;
                float jy = (Mathf.PerlinNoise(0f, Time.unscaledTime * 25f) - 0.5f) * 2f * effectiveJitter;
                targetJitter = new Vector2(jx, jy);
            }
            else
            {
                targetJitter = Vector2.Lerp(currentJitter, Vector2.zero, Time.unscaledDeltaTime * animSpeed);
            }
            rectTransform.anchoredPosition += targetJitter - currentJitter;
            currentJitter = targetJitter;

            // Apply progressive color transition continuously
            UpdateColors(redAmount);
        }

        private void ApplyVisuals(bool hovered, bool pressed)
        {
            float rawProgress = timeToMaxRed > 0.001f ? (hoverTimer / timeToMaxRed) : 1f;
            float redAmount = Mathf.Clamp(rawProgress, 0f, maxRedLimit);
            UpdateColors(redAmount);
        }

        private void UpdateColors(float redAmount)
        {
            Color baseBorder = isHovered ? highlightBorderColor : normalBorderColor;
            Color baseText = isHovered ? highlightTextColor : normalTextColor;

            // Smoothly shift to blood red
            Color currentBorder = Color.Lerp(baseBorder, maxRedBorderColor, redAmount);
            Color currentText = Color.Lerp(baseText, maxRedTextColor, redAmount);

            // Subtle pulsing heartbeat effect when near or at max limit
            if (isHovered && pulseAtMaxRed && redAmount >= maxRedLimit * 0.95f)
            {
                float pulse = Mathf.Sin(Time.unscaledTime * 6f) * 0.06f;
                currentBorder += new Color(pulse, pulse * 0.5f, pulse * 0.5f, 0f);
                currentText += new Color(pulse, pulse * 0.5f, pulse * 0.5f, 0f);
            }

            if (isPressed)
            {
                currentBorder *= 0.88f;
                currentText *= 0.88f;
            }

            if (borderImage != null)
                borderImage.color = currentBorder;

            if (buttonText != null)
                buttonText.color = currentText;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHovered(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetHovered(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetHovered(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // If there's no UI Button component, handle the click audio directly here
            if (uiButton == null)
            {
                PlayClickSound();
            }
        }

        public void PlayHoverSound()
        {
            if (customHoverSfx != null)
            {
                AudioSource src = (controller != null) ? controller.audioSource : (pauseController != null ? pauseController.audioSource : null);
                if (src != null) src.PlayOneShot(customHoverSfx, 0.6f);
            }
            else if (controller != null)
            {
                controller.PlayHoverSfx();
            }
            else if (pauseController != null)
            {
                pauseController.PlayHoverSfx();
            }
        }

        public void PlayClickSound()
        {
            if (customClickSfx != null)
            {
                AudioSource src = (controller != null) ? controller.audioSource : (pauseController != null ? pauseController.audioSource : null);
                if (src != null) src.PlayOneShot(customClickSfx, 0.85f);
            }
            else if (controller != null)
            {
                controller.PlayClickSfx();
            }
            else if (pauseController != null)
            {
                pauseController.PlayClickSfx();
            }
        }

        private void SetHovered(bool hovered)
        {
            if (isHovered == hovered) return;
            isHovered = hovered;
            ApplyVisuals(isHovered, isPressed);

            if (isHovered)
            {
                PlayHoverSound();
            }
        }
    }
}
