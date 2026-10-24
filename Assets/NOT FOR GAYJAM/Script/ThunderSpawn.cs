using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class ThunderSpawn : MonoBehaviour
{
    public Vector2 SpawnBox;
    public Vector2 Offset;

    [Header("Thunder")]
    public GameObject[] thunder;
    [Range(1,50)]public int thunderRandomSpawn;
    [Range(0.1f, 20.0f)] public float minThunderSpawnInterval = 1f;
    [Range(0.1f, 20.0f)] public float maxThunderSpawnInterval = 13f;

    [Header("Global volume")]
    public Volume globalVolume;
    private ColorAdjustments colorAdjustments;

    private float targetExposure = 0f;
    private float targetContrast = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Auto-find Volume if not assigned
        if (globalVolume == null)
        {
            globalVolume = FindFirstObjectByType<Volume>();
        }

        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out colorAdjustments);
        }

        if (colorAdjustments != null)
        {
            targetExposure = colorAdjustments.postExposure.value;
            targetContrast = colorAdjustments.contrast.value;
        }

        StartCoroutine(ThunderCycle());
    }

    IEnumerator ThunderCycle()
    {
        while (true)
        {
            // Randomly wait for a bit
            float waitTime = Random.Range(minThunderSpawnInterval, maxThunderSpawnInterval);
            yield return new WaitForSeconds(waitTime);

            // Start lightning sequence
            StartCoroutine(FlashAndStrike());

            // Wait for flash + rain duration before looping
            yield return new WaitForSeconds(1.5f + 0.5f);
        }
    }

    IEnumerator FlashAndStrike()
    {
        // 1. Make it brighter + change contrast
        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = targetExposure + 2.5f;
            colorAdjustments.contrast.value = targetContrast + 100f;
        }

        // Random count of thunder strikes to spawn (from 1 to thunderRandomSpawn)
        int spawnCount = Random.Range(1, Mathf.Max(2, thunderRandomSpawn + 1));

        for (int i = 0; i < spawnCount; i++)
        {
            // Spawn lightning randomly in the box
            Vector3 pos = transform.position + new Vector3(
                Random.Range(-SpawnBox.x, SpawnBox.x) + Offset.x,
                Random.Range(-SpawnBox.y, SpawnBox.y) + Offset.y,
                0
            );

            if (thunder.Length > 0)
            {
                int index = Random.Range(0, thunder.Length);
                GameObject spawned = Instantiate(thunder[index], pos, Quaternion.identity);
                StartCoroutine(FadeAndDestroyThunder(spawned));
            }
        }

        // Lightning flash (temporary)
        yield return new WaitForSeconds(0.2f);

        // 2. Go back to normal (or darker)
        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = targetExposure;
            colorAdjustments.contrast.value = targetContrast;
        }
    }

    IEnumerator FadeAndDestroyThunder(GameObject thunderObj)
    {
        if (thunderObj == null) yield break;

        // Get SpriteRenderer(s) on the object or its children
        SpriteRenderer[] renderers = thunderObj.GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0)
        {
            // Fallback: if no SpriteRenderer found, just destroy after 0.4s
            yield return new WaitForSeconds(0.4f);
            Destroy(thunderObj);
            yield break;
        }

        // Record initial colors and set alpha to 0
        Color[] initialColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            initialColors[i] = renderers[i].color;
            Color c = initialColors[i];
            c.a = 0f;
            renderers[i].color = c;
        }

        float elapsed = 0f;

        // Phase 1: 0.2 sec to fade alpha from 0 to 1 (0 to 255)
        while (elapsed < 0.2f)
        {
            if (thunderObj == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.2f);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = initialColors[i];
                c.a = t * initialColors[i].a; // Scale with initial opacity
                renderers[i].color = c;
            }
            yield return null;
        }

        elapsed = 0f;

        // Phase 2: 0.2 sec to fade alpha from 1 back to 0
        while (elapsed < 0.2f)
        {
            if (thunderObj == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(1f - (elapsed / 0.2f));

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = initialColors[i];
                c.a = t * initialColors[i].a; // Scale with initial opacity
                renderers[i].color = c;
            }
            yield return null;
        }

        Destroy(thunderObj);
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + (Vector3)Offset, SpawnBox * 2);
    }
}
