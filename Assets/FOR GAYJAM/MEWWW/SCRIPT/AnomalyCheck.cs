using UnityEngine;
using System.Collections.Generic;

public class AnomalyCheck : MonoBehaviour
{
    public static AnomalyCheck instance;

    [Header("Anomaly Settings")]
    public int anomalyLimit = 1;

    [SerializeField]
    private List<GameObject> AnomaliesInMap = new List<GameObject>();

    private bool hadAnomaliesLastFrame = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        AnomalyCap();

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
