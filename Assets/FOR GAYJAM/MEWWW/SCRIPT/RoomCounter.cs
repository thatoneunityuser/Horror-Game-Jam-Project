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

    [Header("Entity Spawner")]
    public SummonTheEntityByRandom entitySpawner;

    [Header("Event / Forced Rooms")]
    [Tooltip("Event rooms that will force spawn in sequential order by index")]
    public GameObject[] EventRooms1;
    [Tooltip("Starting PassedRoomCount for event rooms (inclusive)")]
    public int eventStartRoom = 30;
    [Tooltip("Ending PassedRoomCount for event rooms (inclusive)")]
    public int eventEndRoom = 34;

    [Header("First Event Room Override")]
    [Tooltip("If true, changes ONLY the first forced event room (index 0 / room 30) to alternateFirstEventRoom")]
    public bool changeFirstEventRoom = false;
    [Tooltip("Alternative prefab for the first forced event room")]
    public GameObject alternateFirstEventRoom;

    private List<GameObject> currentRooms = new List<GameObject>();
    private List<GameObject> PreviousRooms = new List<GameObject>();

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

        while (currentRooms.Count > maxRooms)
        {
            if (currentRooms[0] != null)
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

        currentRooms.Add(room);

        // When a new room is added, the room right before it becomes a previous room
        if (currentRooms.Count > 1)
        {
            GameObject prev = currentRooms[currentRooms.Count - 2];
            if (prev != null && !PreviousRooms.Contains(prev))
            {
                PreviousRooms.Add(prev);
            }
        }

        PassedRoomCount++;
        generatedRoomCount = currentRooms.Count;

        // Check if THIS newly generated room is a stair room
        latestGeneratedRoomIsStair = (room.GetComponentInChildren<StairColliderLogic>() != null ||
                                      room.name.ToLower().Contains("stair"));

        Debug.Log($"[RoomCounter] Added room '{room.name}'. Active rooms: {generatedRoomCount}/{maxRooms}. IsStair: {latestGeneratedRoomIsStair}");

        // Optional: decrease entity countdown if entitySpawner is linked directly
        if (entitySpawner != null)
        {
            entitySpawner.DecreaseCountDown();
        }
    }

    // --- Forced Event Room Logic ---
    public bool HasForcedRoom()
    {
        return EventRooms1 != null && EventRooms1.Length > 0 &&
               PassedRoomCount >= eventStartRoom && PassedRoomCount <= eventEndRoom;
    }

    public GameObject GetForcedRoomPrefab()
    {
        if (!HasForcedRoom()) return null;

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

        return null;
    }

    public GameObject SpawnForcedRoom(Transform spawnPoint, GameObject customFirstRoom = null)
    {
        int currentCount = PassedRoomCount;
        int eventIndex = currentCount - eventStartRoom;

        GameObject forcedPrefab = GetForcedRoomPrefab();

        // If an explicit custom room was passed for the first event room (index 0)
        if (eventIndex == 0 && customFirstRoom != null)
        {
            forcedPrefab = customFirstRoom;
        }

        if (forcedPrefab != null && spawnPoint != null)
        {
            string overrideTag = (eventIndex == 0 && (changeFirstEventRoom || customFirstRoom != null))
                ? " [First Room Changed/Overridden]"
                : "";

            GameObject newRoom = Instantiate(forcedPrefab, spawnPoint.position, Quaternion.identity);
            GenerateRoom(newRoom);
            Debug.Log($"[RoomCounter] Force-spawned Event Room [{eventIndex}]{overrideTag} ('{newRoom.name}') for room count #{currentCount}");
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
        // Event rooms force-spawn from PassedRoomCount 30 to 34, so stair room is suppressed
        if ((PassedRoomCount >= eventStartRoom && PassedRoomCount <= eventEndRoom) || HasForcedRoom()) return false;
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
        // 1. Destroy all rooms marked in PreviousRooms
        foreach (GameObject room in PreviousRooms)
        {
            if (room != null)
            {
                Destroy(room);
            }
        }
        PreviousRooms.Clear();

        // 2. Keep only the latest active room, destroying older ones
        while (currentRooms.Count > 1)
        {
            if (currentRooms[0] != null)
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
