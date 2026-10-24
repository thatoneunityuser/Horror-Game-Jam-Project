using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ImpactFrame : MonoBehaviour
{
    public Volume globalVolume;
    public float duration = 0.1f;
    public float vignetteduration;
    public bool IsVignetteIncreasing;
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    
    void Start()
    {
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out colorAdjustments);
            globalVolume.profile.TryGet(out vignette);
        }
    }

    public void ImpactFrameCall()
    {
        StartCoroutine(ImpactFrameCoroutine());
    }

    public void VignetteCall()
    {
        StartCoroutine(VignetteCoroutine());
    }

    IEnumerator VignetteCoroutine()
    {
        if (vignette == null) yield break;

        IsVignetteIncreasing = true;
        
        // Smoothly fade in vignette intensity to 1
        float elapsed = 0f;
        float startIntensity = vignette.intensity.value;
        while (elapsed < vignetteduration)
        {
            elapsed += Time.deltaTime;
            vignette.intensity.value = Mathf.Lerp(startIntensity, 1f, elapsed / vignetteduration);
            yield return null;
        }
        vignette.intensity.value = 1f;

        // Keep vignette at peak for the duration
        yield return new WaitForSeconds(vignetteduration);

        // Smoothly fade out vignette intensity back to 0
        elapsed = 0f;
        while (elapsed < vignetteduration)
        {
            elapsed += Time.deltaTime;
            vignette.intensity.value = Mathf.Lerp(1f, 0f, elapsed / vignetteduration);
            yield return null;
        }
        vignette.intensity.value = 0f;

        IsVignetteIncreasing = false;
    }

    IEnumerator ImpactFrameCoroutine()
    {
        if (colorAdjustments == null) yield break;

        colorAdjustments.postExposure.value = 66f;
        colorAdjustments.saturation.value = 100f;
        colorAdjustments.contrast.value = 20f;
        yield return new WaitForSeconds(duration / 2f);

        colorAdjustments.postExposure.value = 122f;
        colorAdjustments.saturation.value = 150f;
        colorAdjustments.contrast.value = 40f;
        yield return new WaitForSeconds(duration / 2f);

        // Reset all post processing overrides back to their defaults
        colorAdjustments.postExposure.value = 0f;
        colorAdjustments.saturation.value = 0f;
        colorAdjustments.contrast.value = 0f;
        yield return null;
    }



}
