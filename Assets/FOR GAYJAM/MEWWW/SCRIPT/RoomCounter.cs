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
    [Tooltip("Amount to increase entity summon rate (spawn chance percentage) each time a room is passed")]
    public int summonRateIncreasePerRoom = 2;

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

    private Dictionary<GameObject, int> roomNumbers = new Dictionary<GameObject, int>();
    private HashSet<GameObject> secondEventRooms = new HashSet<GameObject>();

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
        secondEventRooms.RemoveWhere(r => r == null);

        // Safeguard: The latest generated room must NEVER be in PreviousRooms
        if (latestGeneratedRoom != null)
        {
            PreviousRooms.Remove(latestGeneratedRoom);
        }

        int effectiveMaxRooms = IsSecondEventCurrentlyActive() ? Mathf.Max(maxRooms, 6) : maxRooms;
        GameObject playerRoom = currentRooms.Count > effectiveMaxRooms ? GetRoomContainingPlayer() : null;
        int safety = 0;
        while (currentRooms.Count > effectiveMaxRooms && safety < 20)
        {
            safety++;
            int removeIdx = -1;
            bool protectEvent2 = IsSecondEventCurrentlyActive();
            for (int i = 0; i < currentRooms.Count; i++)
            {
                GameObject candidate = currentRooms[i];
                if (candidate == null)
                {
                    removeIdx = i;
                    break;
                }
                if (candidate == latestGeneratedRoom || candidate == playerRoom)
                {
                    continue;
                }
                if (protectEvent2 && secondEventRooms.Contains(candidate))
                {
                    continue;
                }
                removeIdx = i;
                break;
            }
            if (removeIdx == -1) break;

            // Do not destroy the latest generated room or player's current room when enforcing maxRooms
            if (currentRooms[removeIdx] != null && currentRooms[removeIdx] != latestGeneratedRoom && currentRooms[removeIdx] != playerRoom)
            {
                roomNumbers.Remove(currentRooms[removeIdx]);
                secondEventRooms.Remove(currentRooms[removeIdx]);
                Destroy(currentRooms[removeIdx]);
            }
            currentRooms.RemoveAt(removeIdx);
        }
        generatedRoomCount = currentRooms.Count;
    }

    public bool IsSecondEventRoom(GameObject room)
    {
        return room != null && secondEventRooms.Contains(room);
    }

    public bool IsSecondEventCurrentlyActive()
    {
        if (latestGeneratedRoom != null && secondEventRooms.Contains(latestGeneratedRoom))
        {
            return true;
        }
        if (SecondEventGhost.ActiveGhosts.Count > 0 && PassedRoomCount >= event2StartRoom)
        {
            return true;
        }
        return PassedRoomCount >= event2StartRoom && PassedRoomCount <= event2EndRoom;
    }

    private GameObject GetRoomContainingPlayer()
    {
        Locker[] lockers = FindObjectsOfType<Locker>();
        if (lockers != null)
        {
            for (int i = 0; i < lockers.Length; i++)
            {
                if (lockers[i] != null && lockers[i].IsPlayerInsideThisLocker)
                {
                    return GetRoomForTransform(lockers[i].transform);
                }
            }
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;

        float px = player.transform.position.x;
        float py = player.transform.position.y;
        GameObject bestRoom = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < currentRooms.Count; i++)
        {
            GameObject r = currentRooms[i];
            if (r == null) continue;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            Renderer[] rends = r.GetComponentsInChildren<Renderer>(true);
            for (int j = 0; j < rends.Length; j++)
            {
                if (rends[j] == null || !rends[j].enabled) continue;
                Bounds b = rends[j].bounds;
                if (b.size.sqrMagnitude < 0.001f) continue;
                if (b.min.x < minX) minX = b.min.x;
                if (b.max.x > maxX) maxX = b.max.x;
                if (b.min.y < minY) minY = b.min.y;
                if (b.max.y > maxY) maxY = b.max.y;
            }
            if (minX == float.MaxValue) continue;

            float dx = (px >= minX && px <= maxX) ? 0f : (px < minX ? minX - px : px - maxX);
            float dy = (py >= minY && py <= maxY) ? 0f : (py < minY ? minY - py : py - maxY);
            float distSq = dx * dx + dy * dy;
            if (distSq <= bestDistSq)
            {
                bestDistSq = distSq;
                bestRoom = r;
            }
        }

        return bestRoom;
    }

    public int GetRoomNumber(GameObject room)
    {
        if (room == null) return -1;
        if (roomNumbers.TryGetValue(room, out int num))
        {
            return num;
        }
        return -1;
    }

    public GameObject GetRoomForTransform(Transform t)
    {
        if (t == null) return null;
        for (int i = 0; i < currentRooms.Count; i++)
        {
            GameObject r = currentRooms[i];
            if (r != null && (t == r.transform || t.IsChildOf(r.transform)))
            {
                return r;
            }
        }
        return t.root != null ? t.root.gameObject : t.gameObject;
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
        roomNumbers[room] = PassedRoomCount;
        generatedRoomCount = currentRooms.Count;

        // Track rooms that contain SecondEventGhost patrol spots
        Transform[] childTransforms = room.GetComponentsInChildren<Transform>(true);
        if (childTransforms != null)
        {
            for (int i = 0; i < childTransforms.Length; i++)
            {
                if (childTransforms[i] != null && childTransforms[i].CompareTag("PartrolSpot"))
                {
                    secondEventRooms.Add(room);
                    break;
                }
            }
        }

        // When passed room count increases, increase summon rate by 2 on all spawners
        SummonTheEntityByRandom[] allSpawners = FindObjectsOfType<SummonTheEntityByRandom>();
        if (allSpawners != null)
        {
            foreach (var spawner in allSpawners)
            {
                if (spawner != null)
                {
                    spawner.IncreaseSummonRate(summonRateIncreasePerRoom);
                }
            }
        }

        // Check if THIS newly generated room is a stair room
        latestGeneratedRoomIsStair = (room.GetComponentInChildren<StairColliderLogic>() != null ||
                                      room.name.ToLower().Contains("stair"));
        if(latestGeneratedRoomIsStair)
        {
            StairRoomCounter++;
        }

        Debug.Log($"[RoomCounter] Added room '{room.name}' (#{PassedRoomCount}). Active rooms: {generatedRoomCount}/{maxRooms}. IsStair: {latestGeneratedRoomIsStair}");

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
            if (!isEvent1)
            {
                secondEventRooms.Add(newRoom);
            }
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

        bool keepEvent2Rooms = IsSecondEventCurrentlyActive();
        GameObject playerRoom = GetRoomContainingPlayer();

        // 1. Destroy all rooms marked in PreviousRooms (strictly excluding latestGeneratedRoom, playerRoom, and active Event 2 rooms)
        for (int i = PreviousRooms.Count - 1; i >= 0; i--)
        {
            GameObject room = PreviousRooms[i];
            if (room == null)
            {
                PreviousRooms.RemoveAt(i);
                continue;
            }
            if (room == latestGeneratedRoom || room == playerRoom || (keepEvent2Rooms && secondEventRooms.Contains(room)))
            {
                continue;
            }
            roomNumbers.Remove(room);
            secondEventRooms.Remove(room);
            Destroy(room);
            PreviousRooms.RemoveAt(i);
        }
        if (!keepEvent2Rooms && playerRoom == null)
        {
            PreviousRooms.Clear();
        }

        // 2. Keep only the latest active room (plus playerRoom and active Event 2 rooms during Event 2), destroying older ones
        for (int i = currentRooms.Count - 1; i >= 0; i--)
        {
            GameObject room = currentRooms[i];
            if (room == null)
            {
                currentRooms.RemoveAt(i);
                continue;
            }
            if (room == latestGeneratedRoom || room == playerRoom || (keepEvent2Rooms && secondEventRooms.Contains(room)))
            {
                continue;
            }
            roomNumbers.Remove(room);
            secondEventRooms.Remove(room);
            Destroy(room);
            currentRooms.RemoveAt(i);
        }

        currentRooms.RemoveAll(r => r == null);
        generatedRoomCount = currentRooms.Count;
        Debug.Log($"[RoomCounter] Cleaned previous rooms. Remaining active room count: {generatedRoomCount}");
    }
}
