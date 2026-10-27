using System.Collections.Generic;
using UnityEngine;

public class RoomCounter : MonoBehaviour
{
    public static RoomCounter instance;

    [Header("Room Settings")]
    public int maxRooms = 5;
    public int staireveryXroom = 5;
    public int generatedRoomCount = 0;
    public int PassedRoomCount = 0;
    public int StairRoomCounter = 0;

    [Header("Entity Spawner")]
    public SummonTheEntityByRandom entitySpawner;
    [Tooltip("If true, every generated room ALSO decreases the entity countdown. Leave OFF when using CooldownDecreaser, otherwise one room counts 2x.")]
    public bool decreaseCountdownOnRoomGenerate = false;

    [Header("Event 1 / Forced Rooms")]
    [Tooltip("Event rooms that will force spawn in sequential order by index")]
    public GameObject[] EventRooms1;
    [Tooltip("Starting PassedRoomCount for event rooms (inclusive)")]
    public int eventStartRoom = 30;
    [Tooltip("Ending PassedRoomCount for event rooms (inclusive)")]
    public int eventEndRoom = 34;

    [Header("Event 2 / Forced Rooms (Second Ghost Event)")]
    [Tooltip("Event rooms for the second ghost event in sequential order")]
    public GameObject[] EventRooms2;
    [Tooltip("Starting PassedRoomCount for second event rooms (inclusive)")]
    public int event2StartRoom = 45;
    [Tooltip("Ending PassedRoomCount for second event rooms (inclusive)")]
    public int event2EndRoom = 48;

    [Header("First Event Room Override")]
    [Tooltip("If true, changes ONLY the first forced event room (index 0 / room 30) to alternateFirstEventRoom")]
    public bool changeFirstEventRoom = false;
    [Tooltip("Alternative prefab for the first forced event room")]
    public GameObject alternateFirstEventRoom;

    [Header("Room Tracking")]
    [Tooltip("The most recently generated room. This room is explicitly protected from ever being treated as a previous room.")]
    public GameObject latestGeneratedRoom;

    public List<GameObject> currentRooms = new List<GameObject>();
    public List<GameObject> PreviousRooms = new List<GameObject>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    void Start()
    {
        if (entitySpawner == null)
        {
            entitySpawner = FindObjectOfType<SummonTheEntityByRandom>();
        }
    }

    void Update()
    {
        // Remove any destroyed references
        currentRooms.RemoveAll(r => r == null);
        PreviousRooms.RemoveAll(r => r == null);

        // Safeguard: The latest generated room must NEVER be in PreviousRooms
        if (latestGeneratedRoom != null)
        {
            PreviousRooms.Remove(latestGeneratedRoom);
        }

        while (currentRooms.Count > maxRooms)
        {
            // Do not destroy the latest generated room when enforcing maxRooms
            if (currentRooms[0] != null && currentRooms[0] != latestGeneratedRoom)
            {
                Destroy(currentRooms[0]);
            }
            currentRooms.RemoveAt(0);
        }
        generatedRoomCount = currentRooms.Count;
    }

    public void GenerateRoom(GameObject room)
    {
        if (room == null) return;

        // Clean up any destroyed references first
        currentRooms.RemoveAll(r => r == null);
        PreviousRooms.RemoveAll(r => r == null);

        // Guard against duplicate calls with the exact same room instance
        // (prevents room from matching currentRooms.Count - 2 and being added to PreviousRooms)
        if (currentRooms.Contains(room))
        {
            Debug.LogWarning($"[RoomCounter] Room '{room.name}' has already been registered. Duplicate GenerateRoom call ignored.");
            return;
        }

        currentRooms.Add(room);
        latestGeneratedRoom = room;

        // Ensure newly generated room is NEVER in PreviousRooms
        PreviousRooms.Remove(room);

        // When a new room is added, the room right before it becomes a previous room candidate
        if (currentRooms.Count > 1)
        {
            GameObject prev = currentRooms[currentRooms.Count - 2];
            // Only add if prev is valid, not the newly generated room, not latestGeneratedRoom, and not already added
            if (prev != null && prev != room && prev != latestGeneratedRoom && !PreviousRooms.Contains(prev))
            {
                PreviousRooms.Add(prev);
            }
        }

        // Final safeguard: latestGeneratedRoom must never be in PreviousRooms
        if (latestGeneratedRoom != null)
        {
            PreviousRooms.Remove(latestGeneratedRoom);
        }

        PassedRoomCount++;
        generatedRoomCount = currentRooms.Count;

        // Check if THIS newly generated room is a stair room
        latestGeneratedRoomIsStair = (room.GetComponentInChildren<StairColliderLogic>() != null ||
                                      room.name.ToLower().Contains("stair"));
        if(latestGeneratedRoomIsStair)
        {
            StairRoomCounter++;
        }

        Debug.Log($"[RoomCounter] Added room '{room.name}'. Active rooms: {generatedRoomCount}/{maxRooms}. IsStair: {latestGeneratedRoomIsStair}");

        // Optional: only when enabled, otherwise CooldownDecreaser handles the countdown
        if (decreaseCountdownOnRoomGenerate && entitySpawner != null)
        {
            entitySpawner.DecreaseCountDown();
        }
    }

    // --- Forced Event Room Logic ---
    public bool HasForcedRoom()
    {
        bool hasEvent1 = EventRooms1 != null && EventRooms1.Length > 0 &&
                         PassedRoomCount >= eventStartRoom && PassedRoomCount <= eventEndRoom;
        bool hasEvent2 = EventRooms2 != null && EventRooms2.Length > 0 &&
                         PassedRoomCount >= event2StartRoom && PassedRoomCount <= event2EndRoom;
        return hasEvent1 || hasEvent2;
    }

    public GameObject GetForcedRoomPrefab()
    {
        if (!HasForcedRoom()) return null;

        // Event 1 check (rooms 30-34)
        if (EventRooms1 != null && EventRooms1.Length > 0 &&
            PassedRoomCount >= eventStartRoom && PassedRoomCount <= eventEndRoom)
        {
            int index = PassedRoomCount - eventStartRoom;

            // ONLY the first room (index 0 / room 30) can be changed by the bool
            if (index == 0 && changeFirstEventRoom && alternateFirstEventRoom != null)
            {
                return alternateFirstEventRoom;
            }

            if (index >= 0 && index < EventRooms1.Length)
            {
                return EventRooms1[index];
            }
            else if (EventRooms1.Length > 0)
            {
                return EventRooms1[index % EventRooms1.Length];
            }
        }

        // Event 2 check (rooms 45-48)
        if (EventRooms2 != null && EventRooms2.Length > 0 &&
            PassedRoomCount >= event2StartRoom && PassedRoomCount <= event2EndRoom)
        {
            int index = PassedRoomCount - event2StartRoom;
            if (index >= 0 && index < EventRooms2.Length)
            {
                return EventRooms2[index];
            }
            else if (EventRooms2.Length > 0)
            {
                return EventRooms2[index % EventRooms2.Length];
            }
        }

        return null;
    }

    public GameObject SpawnForcedRoom(Transform spawnPoint, GameObject customFirstRoom = null)
    {
        int currentCount = PassedRoomCount;
        bool isEvent1 = (currentCount >= eventStartRoom && currentCount <= eventEndRoom);
        int eventIndex = isEvent1 ? (currentCount - eventStartRoom) : (currentCount - event2StartRoom);
        string eventName = isEvent1 ? "Event 1" : "Event 2";

        GameObject forcedPrefab = GetForcedRoomPrefab();

        // If an explicit custom room was passed for the first event room (index 0)
        if (isEvent1 && eventIndex == 0 && customFirstRoom != null)
        {
            forcedPrefab = customFirstRoom;
        }

        if (forcedPrefab != null && spawnPoint != null)
        {
            string overrideTag = (isEvent1 && eventIndex == 0 && (changeFirstEventRoom || customFirstRoom != null))
                ? " [First Room Changed/Overridden]"
                : "";

            GameObject newRoom = Instantiate(forcedPrefab, spawnPoint.position, Quaternion.identity);
            GenerateRoom(newRoom);
            Debug.Log($"[RoomCounter] Force-spawned {eventName} Room [{eventIndex}]{overrideTag} ('{newRoom.name}') for room count #{currentCount}");
            return newRoom;
        }
        return null;
    }

    /// <summary>
    /// Call this from any script or UnityEvent to toggle changing the first forced room at runtime.
    /// </summary>
    public void SetChangeFirstEventRoom(bool enable, GameObject customAltRoom = null)
    {
        changeFirstEventRoom = enable;
        if (customAltRoom != null)
        {
            alternateFirstEventRoom = customAltRoom;
        }
    }

    [Header("Stair Room Tracking")]
    public bool latestGeneratedRoomIsStair = false;

    public bool ShouldSpawnStairRoom()
    {
        // Event rooms force-spawn from PassedRoomCount 30 to 34 and 45 to 48, so stair room is suppressed
        if ((PassedRoomCount >= eventStartRoom && PassedRoomCount <= eventEndRoom) ||
            (PassedRoomCount >= event2StartRoom && PassedRoomCount <= event2EndRoom) ||
            HasForcedRoom()) return false;
        if (staireveryXroom <= 0) return false;
        int nextRoomNumber = PassedRoomCount + 1;
        return nextRoomNumber % staireveryXroom == 0;
    }

    public bool IsNextRoomStairRoom()
    {
        // 1. Next room scheduled to be generated is a stair room
        if (ShouldSpawnStairRoom())
        {
            return true;
        }

        // 2. Room that was just generated (ahead of player) is a stair room
        if (latestGeneratedRoomIsStair)
        {
            return true;
        }

        // 3. Inspect newest active room in currentRooms list (the room ahead of the player)
        if (currentRooms != null && currentRooms.Count > 0)
        {
            GameObject newestRoom = currentRooms[currentRooms.Count - 1];
            if (newestRoom != null)
            {
                if (newestRoom.GetComponentInChildren<StairColliderLogic>() != null ||
                    newestRoom.name.ToLower().Contains("stair"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void DestroyAllPreviousRoom()
    {
        currentRooms.RemoveAll(r => r == null);
        PreviousRooms.RemoveAll(r => r == null);

        // Safeguard: The latest generated room must NEVER be destroyed as a previous room
        if (latestGeneratedRoom != null)
        {
            PreviousRooms.Remove(latestGeneratedRoom);
        }

        // 1. Destroy all rooms marked in PreviousRooms (strictly excluding latestGeneratedRoom)
        foreach (GameObject room in PreviousRooms)
        {
            if (room != null && room != latestGeneratedRoom)
            {
                Destroy(room);
            }
        }
        PreviousRooms.Clear();

        // 2. Keep only the latest active room, destroying older ones
        while (currentRooms.Count > 1)
        {
            if (currentRooms[0] != null && currentRooms[0] != latestGeneratedRoom)
            {
                Destroy(currentRooms[0]);
            }
            currentRooms.RemoveAt(0);
        }

        currentRooms.RemoveAll(r => r == null);
        generatedRoomCount = currentRooms.Count;
        Debug.Log($"[RoomCounter] Cleaned previous rooms. Remaining active room count: {generatedRoomCount}");
    }
}
