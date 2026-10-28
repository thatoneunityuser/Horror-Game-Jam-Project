using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SummonTheEntityByRandom : MonoBehaviour
{
    [Header("Entity Prefabs & Spawning")]
    public GameObject[] Entity;
    public Transform spawnPoint;
    [Tooltip("Delay in seconds before spawning the entity once triggered")]
    public float spawnDelay = 0.1f;

    [Header("Spawn Chance & Cooldown")]
    public bool Spawnable;
    [Range(0, 100)]
    public int spawnChancePercentage = 50;
    public int minRooms = 3;
    public int maxRooms = 5;
    public int CountDown;
    public bool CantChange = false;
    [Tooltip("If true, this spawner will summon an entity only once, then deactivate/destroy itself")]
    public bool SummonOnce;
    private bool hasSpawned = false;

    [Header("Stair Room Prevention")]
    [Tooltip("If true, BlindedEntity will never spawn when the next room is a stair room")]
    public bool preventBlindedEntityOnStairRoom = true;
    [Tooltip("If true and BlindedEntity was blocked by a stair room with no other entity to spawn, delay the spawn to the room after rather than resetting full countdown")]
    public bool delaySpawnIfBlockedByStair = false;

    private int lastDecreaseFrame = -1;
    private bool initialized = false;
    private bool isSpawningDelayed = false;
    private RoomCounter roomCounter;

    void Awake()
    {
        // Init in Awake (not Start) so CountDown is never 0 when something decreases it early
        EnsureRoomCounter();
        InitCountdown();
    }

    void Start()
    {
        EnsureRoomCounter();
        InitCountdown();
    }

    void OnDisable()
    {
        isSpawningDelayed = false;
    }

    private void EnsureRoomCounter()
    {
        if (roomCounter == null)
        {
            roomCounter = RoomCounter.instance != null ? RoomCounter.instance : FindObjectOfType<RoomCounter>();
        }
    }

    private void InitCountdown()
    {
        if (initialized) return;
        initialized = true;

        EnsureRoomCounter();

        // Apply +2% summon rate per passed room count
        if (!SummonOnce && RoomCounter.instance != null && RoomCounter.instance.PassedRoomCount > 0)
        {
            spawnChancePercentage = Mathf.Clamp(spawnChancePercentage + (RoomCounter.instance.PassedRoomCount * 2), 0, 100);
        }

        RollChance();
        ResetCountdown();
    }

    public void IncreaseSummonRate(int amount = 2)
    {
        if (SummonOnce) return;
        spawnChancePercentage = Mathf.Clamp(spawnChancePercentage + amount, 0, 100);
        Debug.Log($"[SummonTheEntityByRandom] Summon rate increased on '{gameObject.name}' -> {spawnChancePercentage}%");
    }

    public void DecreaseCountDown()
    {
        if (SummonOnce && hasSpawned) return;
        if (isSpawningDelayed) return;

        InitCountdown();

        // Ignore duplicate calls in the same frame (door event + trigger + room generate)
        if (lastDecreaseFrame == Time.frameCount) return;
        lastDecreaseFrame = Time.frameCount;

        CountDown--;
        Debug.Log($"[SummonTheEntityByRandom] CountDown -> {CountDown}");

        if (CountDown <= 0)
        {
            if (spawnDelay > 0f && isActiveAndEnabled && gameObject.activeInHierarchy)
            {
                StartCoroutine(DelayedCountdownSpawnRoutine(spawnDelay));
            }
            else
            {
                ResolveCountdownSpawn();
            }
        }
    }

    private IEnumerator DelayedCountdownSpawnRoutine(float delay)
    {
        isSpawningDelayed = true;
        yield return new WaitForSeconds(delay);
        isSpawningDelayed = false;

        ResolveCountdownSpawn();
    }

    private void ResolveCountdownSpawn()
    {
        if (SummonOnce && hasSpawned) return;

        bool spawned = Spawn();
        if (spawned)
        {
            if (SummonOnce)
            {
                // Spawn() already handled hasSpawned and cleanup
                return;
            }
            ResetCountdown();
        }
        else
        {
            if (delaySpawnIfBlockedByStair && IsNextRoomStairRoom())
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

    public void SpawnWithDelay()
    {
        if (SummonOnce && hasSpawned) return;
        if (isSpawningDelayed) return;

        if (spawnDelay > 0f && isActiveAndEnabled && gameObject.activeInHierarchy)
        {
            StartCoroutine(DelayedDirectSpawnRoutine(spawnDelay));
        }
        else
        {
            Spawn();
        }
    }

    private IEnumerator DelayedDirectSpawnRoutine(float delay)
    {
        isSpawningDelayed = true;
        yield return new WaitForSeconds(delay);
        isSpawningDelayed = false;

        Spawn();
    }

    [ContextMenu("Spawn Entity")]
    public bool Spawn()
    {
        if (SummonOnce && hasSpawned) return false;

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

        if (SummonOnce)
        {
            hasSpawned = true;
            enabled = false;
            CleanupAfterSummonOnce();
        }

        return true;
    }

    private void CleanupAfterSummonOnce()
    {
        // If this component is attached to a GameObject with other core scripts (like RoomCounter or Camera),
        // only destroy this script component so we don't accidentally destroy the entire manager GameObject!
        if (GetComponent<RoomCounter>() != null || GetComponent<Camera>() != null || CompareTag("Player"))
        {
            Destroy(this);
        }
        else
        {
            Destroy(gameObject);
        }
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
        if (SummonOnce)
        {
            // If SummonOnce is enabled, guarantee the spawn when countdown expires
            Spawnable = true;
            return;
        }
        int chance = Random.Range(0, 100);
        Spawnable = (chance < spawnChancePercentage);
    }

    private void ResetCountdown()
    {
        // If CantChange is enabled, preserve the inspector countdown
        if (CantChange)
        {
            CountDown = Mathf.Max(1, CountDown);
            return;
        }

        EnsureRoomCounter();
        int passedRooms = (roomCounter != null) ? roomCounter.PassedRoomCount : 0;

        if (passedRooms <= 10)
        {
            CountDown = maxRooms;
        }
        else if (passedRooms <= 20)
        {
            // Random.Range with integers is max-exclusive, so add 1 to include maxRooms
            CountDown = Random.Range(minRooms, maxRooms + 1);
        }
        else
        {
            CountDown = minRooms;
        }

        CountDown = Mathf.Max(1, CountDown);
    }
}
