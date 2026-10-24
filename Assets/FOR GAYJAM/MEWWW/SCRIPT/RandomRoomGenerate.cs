using System.Collections.Generic;
using UnityEngine;

public class RandomRoomGenerate : MonoBehaviour
{
    [Header("Room Settings")]
    public GameObject[] roomPrefabs;
    public GameObject[] snapshotroomprefabs;
    public GameObject[] stairroomprefab;
    [Range(0, 100)]
    public int snapshotroomchace = 10;
    [Tooltip("Stair room will spawn every X rooms (overridden by RoomCounter.staireveryXroom if assigned)")]
    public int randomstairstairroom = 5;
    public int normalroomchace;
    public GameObject LastRoom;
    public Transform spawnPoint;

    private RoomCounter roomCounter;
    private bool hasTriggered = false;

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
        if (!hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true;
            GenerateRoom();
            Destroy(gameObject);
        }
    }

    public void GenerateRoom()
    {
        if (spawnPoint == null)
            return;

        if (roomCounter == null)
        {
            roomCounter = FindObjectOfType<RoomCounter>();
        }

        // 1. Final Room check (e.g. at 50 rooms passed)
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

        // TOP PRIORITY: Stair room condition evaluated FIRST (e.g. every 5th room)
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

