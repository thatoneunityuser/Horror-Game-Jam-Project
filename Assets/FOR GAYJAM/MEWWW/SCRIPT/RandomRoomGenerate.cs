using System.Collections.Generic;
using UnityEngine;

public class RandomRoomGenerate : MonoBehaviour
{
    [Header("Room Settings")]
    public GameObject[] roomPrefabs;
    public GameObject[] snapshotroomprefabs;
    public GameObject[] stairroomprefab;
    public GameObject[] EventRooms1;
    public GameObject[] EventRooms2;
    
    [Header("First Event Room Override")]
    [Tooltip("If true, changes ONLY the first forced event room (room 30) spawned by this trigger")]
    public bool changeFirstEventRoom = false;
    [Tooltip("Alternative prefab for the first forced event room (if null, uses RoomCounter's alternate prefab)")]
    public GameObject alternateFirstEventRoom;
    
    [Range(0, 100)]
    public int snapshotroomchace = 10;
    [Tooltip("Stair room will spawn every X rooms (overridden by RoomCounter.staireveryXroom if assigned)")]
    public int randomstairstairroom = 5;
    public int normalroomchace;
    public GameObject LastRoom;
    public Transform spawnPoint;

    [Header("Spawn & Chain Prevention")]
    [Tooltip("Global cooldown (seconds) before ANY room generator can spawn a new room")]
    public float chainCooldown = 0.5f;
    [Tooltip("A newly instantiated room generator cannot trigger for this many seconds after spawning")]
    public float spawnCooldown = 0.3f;

    private static float lastGlobalRoomGeneratedTime = -999f;
    private float spawnTime;
    private RoomCounter roomCounter;
    private bool hasTriggered = false;

    public bool HasTriggered => hasTriggered;

    void Awake()
    {
        spawnTime = Time.time;
    }

    void Start()
    {
        roomCounter = FindObjectOfType<RoomCounter>();
        normalroomchace = 100 - snapshotroomchace;
        if (roomCounter != null && roomCounter.staireveryXroom > 0)
        {
            randomstairstairroom = roomCounter.staireveryXroom;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleTrigger(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        HandleTrigger(other);
    }

    private void HandleTrigger(Collider2D other)
    {
        // NOTE: Unity still sends trigger messages to DISABLED scripts, so check 'enabled' manually
        if (!enabled || hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            if (TryGenerateRoom())
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Generates the next room. Runs ONLY ONCE per instance, no matter how many times
    /// it is called (door onOpen event, trigger, etc.).
    /// </summary>
    public void GenerateRoom()
    {
        TryGenerateRoom();
    }

    /// <summary>
    /// Used by SecondEventGhost when only 1 event room exists so the next event room can pre-spawn
    /// ahead of the closed door (runs at most once per generator instance).
    /// </summary>
    public bool ForceGenerateNextRoomIfPending()
    {
        if (hasTriggered || spawnPoint == null) return false;

        hasTriggered = true;
        lastGlobalRoomGeneratedTime = Time.time;
        SpawnNextRoom();
        return true;
    }

    private bool TryGenerateRoom()
    {
        if (hasTriggered) return false;

        if (spawnPoint == null)
            return false;

        // Prevent newly spawned rooms from triggering immediately on spawn (e.g. if spawnPoint overlaps player collider)
        if (Time.time - spawnTime < spawnCooldown)
        {
            return false;
        }

        // Prevent rapid chain room generation (back-to-back spawns within cooldown)
        if (Time.time - lastGlobalRoomGeneratedTime < chainCooldown)
        {
            return false;
        }

        hasTriggered = true;
        lastGlobalRoomGeneratedTime = Time.time;
        SpawnNextRoom();
        return true;
    }

    private void SpawnNextRoom()
    {
        if (roomCounter == null)
        {
            roomCounter = RoomCounter.instance != null ? RoomCounter.instance : FindObjectOfType<RoomCounter>();
        }

        // 1. FORCED EVENT ROOMS: Handled centrally by RoomCounter (30 -> 34 and 45 -> 48 by index)
        if (roomCounter != null)
        {
            // If this trigger enables changeFirstEventRoom, apply it
            if (changeFirstEventRoom)
            {
                roomCounter.changeFirstEventRoom = true;
                if (alternateFirstEventRoom != null)
                {
                    roomCounter.alternateFirstEventRoom = alternateFirstEventRoom;
                }
            }

            if (roomCounter.HasForcedRoom())
            {
                GameObject customRoom = (changeFirstEventRoom && alternateFirstEventRoom != null)
                    ? alternateFirstEventRoom
                    : null;

                roomCounter.SpawnForcedRoom(spawnPoint, customRoom);
                return;
            }
            // Fallback: If EventRooms1 was configured on this prefab rather than RoomCounter
            else if (EventRooms1 != null && EventRooms1.Length > 0 && roomCounter.PassedRoomCount >= 30 && roomCounter.PassedRoomCount <= 34)
            {
                int eventIndex = roomCounter.PassedRoomCount - 30;
                GameObject fallbackPrefab;

                if (eventIndex == 0 && (changeFirstEventRoom || roomCounter.changeFirstEventRoom))
                {
                    fallbackPrefab = (alternateFirstEventRoom != null)
                        ? alternateFirstEventRoom
                        : roomCounter.alternateFirstEventRoom;

                    if (fallbackPrefab == null)
                    {
                        fallbackPrefab = EventRooms1[0];
                    }
                }
                else
                {
                    fallbackPrefab = (eventIndex >= 0 && eventIndex < EventRooms1.Length)
                        ? EventRooms1[eventIndex]
                        : EventRooms1[eventIndex % EventRooms1.Length];
                }

                if (fallbackPrefab != null)
                {
                    GameObject spawnedRoom = Instantiate(fallbackPrefab, spawnPoint.position, Quaternion.identity);
                    roomCounter.GenerateRoom(spawnedRoom);
                    return;
                }
            }
            // Fallback: If EventRooms2 was configured on this prefab rather than RoomCounter
            else if (EventRooms2 != null && EventRooms2.Length > 0 && roomCounter.PassedRoomCount >= roomCounter.event2StartRoom && roomCounter.PassedRoomCount <= roomCounter.event2EndRoom)
            {
                int eventIndex = roomCounter.PassedRoomCount - roomCounter.event2StartRoom;
                GameObject fallbackPrefab = (eventIndex >= 0 && eventIndex < EventRooms2.Length)
                    ? EventRooms2[eventIndex]
                    : EventRooms2[eventIndex % EventRooms2.Length];

                if (fallbackPrefab != null)
                {
                    GameObject spawnedRoom = Instantiate(fallbackPrefab, spawnPoint.position, Quaternion.identity);
                    roomCounter.GenerateRoom(spawnedRoom);
                    return;
                }
            }
        }

        // 2. Final Room check (e.g. at 50 rooms passed)
        if (roomCounter != null && roomCounter.PassedRoomCount >= 50 && LastRoom != null)
        {
            GameObject finalRoom = Instantiate(LastRoom, spawnPoint.position, Quaternion.identity);
            roomCounter.GenerateRoom(finalRoom);
            return;
        }

        // Calculate next room index (e.g. room 1, 2, 3...)
        int nextRoomNumber = (roomCounter != null) ? roomCounter.PassedRoomCount + 1 : 1;
        int stairInterval = (roomCounter != null && roomCounter.staireveryXroom > 0)
            ? roomCounter.staireveryXroom
            : (randomstairstairroom > 0 ? randomstairstairroom : 5);

        bool isStairRoomTime = (stairInterval > 0) && (nextRoomNumber % stairInterval == 0);
        bool hasStairRooms = stairroomprefab != null && stairroomprefab.Length > 0;
        bool hasSnapshotRooms = snapshotroomprefabs != null && snapshotroomprefabs.Length > 0;
        bool hasNormalRooms = roomPrefabs != null && roomPrefabs.Length > 0;

        int random = Random.Range(0, 100);
        GameObject prefabToSpawn = null;

        // TOP PRIORITY: Stair room condition (e.g. every 5th room)
        if (isStairRoomTime && hasStairRooms)
        {
            int randomIndex = Random.Range(0, stairroomprefab.Length);
            prefabToSpawn = stairroomprefab[randomIndex];
            Debug.Log($"[RandomRoomGenerate] Spawning Stair Room for room #{nextRoomNumber} (every {stairInterval} rooms)");
        }
        // SECOND PRIORITY: Snapshot room by percentage chance
        else if (hasSnapshotRooms && random < snapshotroomchace)
        {
            int randomIndex = Random.Range(0, snapshotroomprefabs.Length);
            prefabToSpawn = snapshotroomprefabs[randomIndex];
        }
        // DEFAULT: Normal room
        else if (hasNormalRooms)
        {
            int randomIndex = Random.Range(0, roomPrefabs.Length);
            prefabToSpawn = roomPrefabs[randomIndex];
        }

        // Safety fallback: If selected slot was null or no stair prefab, pick any available room
        if (prefabToSpawn == null)
        {
            if (hasNormalRooms)
            {
                prefabToSpawn = roomPrefabs[Random.Range(0, roomPrefabs.Length)];
            }
            else if (hasSnapshotRooms)
            {
                prefabToSpawn = snapshotroomprefabs[Random.Range(0, snapshotroomprefabs.Length)];
            }
            else if (hasStairRooms)
            {
                prefabToSpawn = stairroomprefab[Random.Range(0, stairroomprefab.Length)];
            }
        }

        GameObject newRoom = null;
        if (prefabToSpawn != null)
        {
            newRoom = Instantiate(prefabToSpawn, spawnPoint.position, Quaternion.identity);
        }

        if (roomCounter != null && newRoom != null)
        {
            roomCounter.GenerateRoom(newRoom);
        }
    }
}
