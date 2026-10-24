using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SnapshotAnomaly : MonoBehaviour
{
    [Header("Snapshot Settings")]
    [Tooltip("Total countdown duration before snapshot triggers")]
    public float lifeTimeBeforeSnapShot = 5f;

    [Tooltip("Transform positions or objects designated as safe zones")]
    public Transform[] snapshotSafeArea;

    [Tooltip("Safe radius around each safe area transform if no collider is attached")]
    public float safeAreaRadius = 3f;

    [Tooltip("Number of safe areas to randomly destroy at the start")]
    public int safeAreasToDestroy = 2;

    [Tooltip("How long the blinding debuff takes to fully recover")]
    public float SnapShotDebufftime = 6f;

    [Header("Tick Per Sec (Wink)")]
    [Tooltip("Duration of each smooth volume wink")]
    public float tickduration = 0.3f;

    [Tooltip("Interval in seconds between each wink tick")]
    public float tickeverySecond = 1f;

    [Tooltip("Post exposure peak during each warning tick/wink")]
    public float winkPostExposure = 1.5f;

    [Tooltip("Post exposure applied when player fails to enter a safe zone")]
    public float debuffPostExposure = 6f;

    [Header("Volume Settings")]
    public Volume snapshotVolume;
    private ColorAdjustments colorAdjustments;

    private Coroutine anomalyCoroutine;

    void Awake()
    {
        InitVolume();
    }

    void Start()
    {
        if (tickeverySecond <= 0f) tickeverySecond = 1f;
        if (tickduration <= 0f) tickduration = 0.3f;

        if (colorAdjustments == null)
        {
            InitVolume();
        }

        anomalyCoroutine = StartCoroutine(SnapshotAnomalyRoutine());

        SetupAndRandomizeSafeAreas();
    }

    void SetupAndRandomizeSafeAreas()
    {
        if (snapshotSafeArea == null || snapshotSafeArea.Length == 0) return;

        // 1. Ensure each assigned safe area has the SnapShotSafeArea component
        List<Transform> availableAreas = new List<Transform>();
        foreach (Transform area in snapshotSafeArea)
        {
            if (area != null)
            {
                SnapShotSafeArea safeScript = area.GetComponent<SnapShotSafeArea>();
                if (safeScript == null)
                {
                    safeScript = area.gameObject.AddComponent<SnapShotSafeArea>();
                }
                safeScript.safeRadius = safeAreaRadius;
                availableAreas.Add(area);
            }
        }

        // 2. Randomly destroy safe areas without risks of infinite loops or duplicate picks
        int countToDestroy = Mathf.Min(safeAreasToDestroy, availableAreas.Count);
        for (int i = 0; i < countToDestroy; i++)
        {
            int randomIndex = Random.Range(0, availableAreas.Count);
            GameObject toDestroy = availableAreas[randomIndex].gameObject;
            availableAreas.RemoveAt(randomIndex);

            if (toDestroy != null)
            {
                toDestroy.SetActive(false);
                Destroy(toDestroy);
            }
        }
    }

    void InitVolume()
    {
        if (snapshotVolume == null)
        {
            snapshotVolume = GetComponent<Volume>();
            if (snapshotVolume == null)
            {
                snapshotVolume = FindObjectOfType<Volume>();
            }
        }

        if (snapshotVolume != null && snapshotVolume.profile != null)
        {
            if (!snapshotVolume.profile.TryGet(out colorAdjustments))
            {
                colorAdjustments = snapshotVolume.profile.Add<ColorAdjustments>(true);
            }

            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = 0f;
        }
    }

    IEnumerator SnapshotAnomalyRoutine()
    {
        float elapsed = 0f;
        float actualWinkDuration = Mathf.Clamp(tickduration, 0.05f, tickeverySecond * 0.9f);

        // Tick and wink every second with ColorAdjustments until countdown is up
        while (elapsed < lifeTimeBeforeSnapShot)
        {
            yield return StartCoroutine(WinkRoutine(winkPostExposure, actualWinkDuration));

            elapsed += tickeverySecond;

            float waitTime = tickeverySecond - actualWinkDuration;
            float remaining = lifeTimeBeforeSnapShot - elapsed;

            if (waitTime > 0f && remaining > 0f)
            {
                yield return new WaitForSeconds(Mathf.Min(waitTime, remaining));
            }
        }

        // Time's up: Check if player stayed inside a safe area
        bool isSafe = IsPlayerInSafeArea();

        if (!isSafe)
        {
            Debug.Log($"[SnapshotAnomaly] Times up! Player NOT in safe zone. Applying post exposure debuff ({debuffPostExposure}).");
            yield return StartCoroutine(ApplyDebuffRoutine());
        }
        else
        {
            Debug.Log("[SnapshotAnomaly] Times up! Player is SAFE inside the safe zone.");
            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.value = 0f;
            }
        }
    }

    /// <summary>
    /// Smoothly winks the volume's Post Exposure using Mathf.Lerp up and down.
    /// </summary>
    IEnumerator WinkRoutine(float targetExposure, float duration)
    {
        if (colorAdjustments == null) yield break;

        float halfDuration = Mathf.Max(0.01f, duration * 0.5f);
        float startExposure = colorAdjustments.postExposure.value;

        // Lerp up to wink exposure
        float timer = 0f;
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / halfDuration);
            colorAdjustments.postExposure.value = Mathf.Lerp(startExposure, targetExposure, t);
            yield return null;
        }

        // Lerp back down to 0
        timer = 0f;
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / halfDuration);
            colorAdjustments.postExposure.value = Mathf.Lerp(targetExposure, 0f, t);
            yield return null;
        }

        colorAdjustments.postExposure.value = 0f;
    }

    /// <summary>
    /// Applies Post Exposure = 6 and smoothly recovers to 0 using Lerp over SnapShotDebufftime.
    /// </summary>
    IEnumerator ApplyDebuffRoutine()
    {
        if (colorAdjustments == null) yield break;

        // Quick flash up to debuffPostExposure (6)
        float flashDuration = 0.08f;
        float timer = 0f;
        float startExposure = colorAdjustments.postExposure.value;
        while (timer < flashDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / flashDuration);
            colorAdjustments.postExposure.value = Mathf.Lerp(startExposure, debuffPostExposure, t);
            yield return null;
        }
        colorAdjustments.postExposure.value = debuffPostExposure;

        // Smoothly fade back down to 0 over SnapShotDebufftime
        timer = 0f;
        while (timer < SnapShotDebufftime)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / SnapShotDebufftime);
            colorAdjustments.postExposure.value = Mathf.Lerp(debuffPostExposure, 0f, t);
            yield return null;
        }

        colorAdjustments.postExposure.value = 0f;
    }

    /// <summary>
    /// Checks if player is within any assigned snapshot safe areas or SnapShotSafeArea components.
    /// </summary>
    public bool IsPlayerInSafeArea()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return false;

        Vector3 playerPos = player.transform.position;

        // 1. Check assigned safe area transforms
        if (snapshotSafeArea != null && snapshotSafeArea.Length > 0)
        {
            foreach (Transform safeZone in snapshotSafeArea)
            {
                if (safeZone == null) continue;

                // Check SnapShotSafeArea script
                SnapShotSafeArea safeScript = safeZone.GetComponent<SnapShotSafeArea>();
                if (safeScript != null && safeScript.IsInside(playerPos))
                {
                    return true;
                }

                // Check 2D Collider
                Collider2D col = safeZone.GetComponent<Collider2D>();
                if (col != null && col.OverlapPoint(playerPos))
                {
                    return true;
                }

                // Check radius distance
                if (Vector2.Distance(safeZone.position, playerPos) <= safeAreaRadius)
                {
                    return true;
                }
            }
        }

        // 2. Also check any SnapShotSafeArea components present in scene
        SnapShotSafeArea[] allSafeAreas = FindObjectsOfType<SnapShotSafeArea>();
        foreach (SnapShotSafeArea area in allSafeAreas)
        {
            if (area != null && area.IsInside(playerPos))
            {
                return true;
            }
        }

        return false;
    }

    void OnDisable()
    {
        ResetPostExposure();
    }

    void OnDestroy()
    {
        ResetPostExposure();
    }

    private void ResetPostExposure()
    {
        if (anomalyCoroutine != null)
        {
            StopCoroutine(anomalyCoroutine);
            anomalyCoroutine = null;
        }

        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = 0f;
        }
    }
}