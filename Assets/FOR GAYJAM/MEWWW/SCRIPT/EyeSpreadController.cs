using UnityEngine;

/// <summary>
/// Lets each SpriteRenderer pick its own eye texture / spread settings
/// while sharing ONE material (Custom/SpriteEyeSpread).
/// The sprite on the SpriteRenderer is never changed - it is only used as the area.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class EyeSpreadController : MonoBehaviour
{
    [Header("Eye")]
    public Texture2D eyeTexture;
    public Color eyeColor = Color.white;
    [Range(0f, 5f)] public float eyeGlow = 0f;

    [Header("Voronoi")]
    [Range(1f, 30f)] public float density = 6f;
    public float angleOffset = 2f;
    [Range(0f, 5f)] public float voronoiSpeed = 0.5f;
    public float seed = 0f;
    public Color edgeColor = new Color(0f, 0f, 0f, 0f);
    [Range(0f, 0.5f)] public float edgeWidth = 0.05f;

    [Header("Spread")]
    [Range(0f, 1f)] public float coverage = 0.7f;
    [Range(0.05f, 1.5f)] public float scaleMin = 0.5f;
    [Range(0.05f, 1.5f)] public float scaleMax = 0.9f;
    [Range(0f, 180f)] public float randomRotation = 15f;

    [Header("Area Limit")]
    [Range(0.01f, 1f)] public float maskThreshold = 0.5f;
    public bool clipToMask = true;
    public bool showSprite = true;

    [Header("Blink")]
    [Range(0f, 2f)] public float blinkSpeed = 0.3f;

    private SpriteRenderer sr;
    private MaterialPropertyBlock block;

    void OnEnable() { Apply(); }
    void OnValidate() { Apply(); }

    public void SetEyeTexture(Texture2D tex)
    {
        eyeTexture = tex;
        Apply();
    }

    public void Apply()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        sr.GetPropertyBlock(block);
        if (eyeTexture != null) block.SetTexture("_EyeTex", eyeTexture);
        block.SetColor("_EyeColor", eyeColor);
        block.SetFloat("_EyeGlow", eyeGlow);
        block.SetFloat("_Density", density);
        block.SetFloat("_AngleOffset", angleOffset);
        block.SetFloat("_VoronoiSpeed", voronoiSpeed);
        block.SetFloat("_Seed", seed);
        block.SetColor("_EdgeColor", edgeColor);
        block.SetFloat("_EdgeWidth", edgeWidth);
        block.SetFloat("_Coverage", coverage);
        block.SetFloat("_ScaleMin", scaleMin);
        block.SetFloat("_ScaleMax", scaleMax);
        block.SetFloat("_RandomRotation", randomRotation);
        block.SetFloat("_MaskThreshold", maskThreshold);
        block.SetFloat("_ClipToMask", clipToMask ? 1f : 0f);
        block.SetFloat("_ShowSprite", showSprite ? 1f : 0f);
        block.SetFloat("_BlinkSpeed", blinkSpeed);
        sr.SetPropertyBlock(block);
    }
}
