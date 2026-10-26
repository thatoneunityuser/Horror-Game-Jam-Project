using System.Collections.Generic;
using UnityEngine;

public class SummonTheEntityByRandom : MonoBehaviour
{
    [Header("Entity Prefabs & Spawning")]
    public GameObject[] Entity;
    public Transform spawnPoint;

    [Header("Spawn Chance & Cooldown")]
    public bool Spawnable;
    [Range(0, 100)]
    public int spawnChancePercentage = 50;
    public int minRooms = 3;
    public int maxRooms = 5;
    public int CountDown;

    [Header("Stair Room Prevention")]
    [Tooltip("If true, BlindedEntity will never spawn when the next room is a stair room")]
    public bool preventBlindedEntityOnStairRoom = true;
    [Tooltip("If true and BlindedEntity was blocked by a stair room with no other entity to spawn, delay the spawn to the room after rather than resetting full countdown")]
    public bool delaySpawnIfBlockedByStair = false;

    private int lastDecreaseFrame = -1;
    private bool initialized = false;

    void Awake()
    {
        // Init in Awake (not Start) so CountDown is never 0 when something decreases it early
        InitCountdown();
    }

    void Start()
    {
        InitCountdown();
    }

    private void InitCountdown()
    {
        if (initialized) return;
        initialized = true;
        RollChance();
        ResetCountdown();
    }

    public void DecreaseCountDown()
    {
        InitCountdown();

        // Ignore duplicate calls in the same frame (door event + trigger + room generate)
        if (lastDecreaseFrame == Time.frameCount) return;
        lastDecreaseFrame = Time.frameCount;

        CountDown--;
        Debug.Log($"[SummonTheEntityByRandom] CountDown -> {CountDown}");

        if (CountDown <= 0)
        {
            bool spawned = Spawn();
            if (!spawned && delaySpawnIfBlockedByStair && IsNextRoomStairRoom())
            {
                // Delay attempt by 1 room until player exits the stair room
                CountDown = 1;
            }
            else
            {
                ResetCountdown();
            }
        }
    }

    public bool Spawn()
    {
        // Re-roll chance for this cycle
        RollChance();

        if (!Spawnable || Entity == null || Entity.Length == 0)
        {
            return false;
        }

        bool nextIsStair = preventBlindedEntityOnStairRoom && IsNextRoomStairRoom();

        // Build list of valid entity prefabs
        List<GameObject> validEntities = new List<GameObject>();
        bool hadBlindedEntity = false;

        foreach (GameObject enemyPrefab in Entity)
        {
            if (enemyPrefab == null) continue;

            bool isBlinded = IsBlindedEntity(enemyPrefab);
            if (isBlinded)
            {
                hadBlindedEntity = true;
            }

            // Exclude BlindedEntity if the next room is a stair room
            if (nextIsStair && isBlinded)
            {
                continue;
            }

            validEntities.Add(enemyPrefab);
        }

        // If next room is stair room and all available entities were BlindedEntity, do not spawn
        if (validEntities.Count == 0)
        {
            if (nextIsStair && hadBlindedEntity)
            {
                Debug.Log("[SummonTheEntityByRandom] BlindedEntity was prevented from spawning because the next room is a StairRoom.");
            }
            return false;
        }

        int randomIndex = Random.Range(0, validEntities.Count);
        GameObject chosenPrefab = validEntities[randomIndex];
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject newEnemy = Instantiate(chosenPrefab, spawnPos, Quaternion.identity);

        if (AnomalyCheck.instance != null)
        {
            AnomalyCheck.instance.AddedNewAnomaly(newEnemy);
        }

        return true;
    }

    public bool IsBlindedEntity(GameObject obj)
    {
        if (obj == null) return false;
        if (obj.GetComponent<BlindedEntity>() != null || obj.GetComponentInChildren<BlindedEntity>() != null)
            return true;
        if (obj.name.ToLower().Contains("blind"))
            return true;
        return false;
    }

    public bool IsNextRoomStairRoom()
    {
        if (RoomCounter.instance != null)
        {
            return RoomCounter.instance.IsNextRoomStairRoom();
        }

        RoomCounter rc = FindObjectOfType<RoomCounter>();
        if (rc != null)
        {
            return rc.IsNextRoomStairRoom();
        }

        return false;
    }

    private void RollChance()
    {
        int chance = Random.Range(0, 100);
        Spawnable = (chance < spawnChancePercentage);
    }

    private void ResetCountdown()
    {
        // Random.Range with integers is max-exclusive, so add 1
        CountDown = Mathf.Max(1, Random.Range(minRooms, maxRooms + 1));
    }
}
