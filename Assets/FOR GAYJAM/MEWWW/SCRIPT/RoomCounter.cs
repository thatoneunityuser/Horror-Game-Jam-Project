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

    [Header("Stair Room Tracking")]
    public bool latestGeneratedRoomIsStair = false;

    public bool ShouldSpawnStairRoom()
    {
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
