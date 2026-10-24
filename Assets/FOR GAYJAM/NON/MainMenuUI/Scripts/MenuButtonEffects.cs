using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace HorrorJam.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class MenuButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
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

        [Header("Components (Auto-detected if empty)")]
        public Image borderImage;
        public TMP_Text buttonText;

        private RectTransform rectTransform;
        private Vector3 originalScale;
        private Vector2 originalAnchoredPos;
        private bool isHovered = false;
        private bool isPressed = false;
        private MainMenuController controller;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            originalScale = rectTransform.localScale;
            originalAnchoredPos = rectTransform.anchoredPosition;

            if (borderImage == null)
                borderImage = GetComponent<Image>();

            if (buttonText == null)
                buttonText = GetComponentInChildren<TMP_Text>();

            controller = GetComponentInParent<MainMenuController>();

            ApplyVisuals(false, false);
        }

        private void OnEnable()
        {
            isHovered = false;
            isPressed = false;
            if (rectTransform != null)
            {
                rectTransform.localScale = originalScale;
                rectTransform.anchoredPosition = originalAnchoredPos;
            }
            ApplyVisuals(false, false);
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

            // Subtle horror micro-jitter on hover
            if (isHovered && enableJitter && !isPressed)
            {
                float jx = (Mathf.PerlinNoise(Time.unscaledTime * 25f, 0f) - 0.5f) * 2f * jitterIntensity;
                float jy = (Mathf.PerlinNoise(0f, Time.unscaledTime * 25f) - 0.5f) * 2f * jitterIntensity;
                rectTransform.anchoredPosition = originalAnchoredPos + new Vector2(jx, jy);
            }
            else
            {
                rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, originalAnchoredPos, Time.unscaledDeltaTime * animSpeed);
            }
        }

        private void ApplyVisuals(bool hovered, bool pressed)
        {
            Color borderCol = hovered ? highlightBorderColor : normalBorderColor;
            Color textCol = hovered ? highlightTextColor : normalTextColor;

            if (borderImage != null)
                borderImage.color = borderCol;

            if (buttonText != null)
                buttonText.color = textCol;
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

        private void SetHovered(bool hovered)
        {
            if (isHovered == hovered) return;
            isHovered = hovered;
            ApplyVisuals(isHovered, isPressed);

            if (isHovered && controller != null)
            {
                controller.PlayHoverSfx();
            }
        }
    }
}
