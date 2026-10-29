using UnityEngine;
using System.Collections.Generic;

public class AnomalyCheck : MonoBehaviour
{
    public static AnomalyCheck instance;

    [Header("Anomaly Settings")]
    public int anomalyLimit = 1;

    [Header("Timeline On First Entity Disappear")]
    [Tooltip("Condition 1: Timeline prefab instantiated when the very first entity that appears has disappeared")]
    public GameObject firstEntityDisappearTimelinePrefab;
    [Tooltip("Optional spawn point override for firstEntityDisappearTimelinePrefab (uses prefab's own position/rotation if null)")]
    public Transform firstEntityTimelineSpawnPoint;

    [SerializeField]
    private List<GameObject> AnomaliesInMap = new List<GameObject>();

    private bool hadAnomaliesLastFrame = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (RoomCounter.instance == null)
            {
                RoomCounter.ResetTimelineConditionTracking();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        AnomalyCap();
        RoomCounter.UpdateTimelineConditionWatchers();

        bool hasAnomalies = AnomaliesInMap != null && AnomaliesInMap.Count > 0;
        if (hadAnomaliesLastFrame && !hasAnomalies)
        {
            Door[] doors = FindObjectsOfType<Door>();
            foreach (Door door in doors)
            {
                if (door != null)
                {
                    door.UnlockDoor();
                }
            }
        }
        hadAnomaliesLastFrame = hasAnomalies;
    }

    void AnomalyCap()
    {
        // Clean up any destroyed anomalies from the list
        AnomaliesInMap.RemoveAll(a => a == null);//a คือตัวแปรที่ใช้ในการวนลูปเพื่อตรวจสอบว่ามีตัวไหนเป็น null หรือไม่

        while (AnomaliesInMap.Count > anomalyLimit)
        {
            if (AnomaliesInMap[0] != null)
            {
                Destroy(AnomaliesInMap[0]);
            }
            AnomaliesInMap.RemoveAt(0);
        }
    }

    public void AddedNewAnomaly(GameObject Anomaly)
    {
        if (Anomaly != null)
        {
            AnomaliesInMap.Add(Anomaly);
            RoomCounter.NotifyEntityAppeared(Anomaly, firstEntityDisappearTimelinePrefab, firstEntityTimelineSpawnPoint);
        }
    }

    public void CleanAllAnomaly()
    {
        foreach (GameObject anomaly in AnomaliesInMap)
        {
            if (anomaly != null)
            {
                Destroy(anomaly);
            }
        }
        AnomaliesInMap.Clear();
    }
}
