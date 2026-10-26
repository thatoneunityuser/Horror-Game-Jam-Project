using UnityEngine;
using UnityEngine.Events;

public class StairRoomCountEvent : MonoBehaviour
{
    public UnityEvent OnStairRoomReached;
    public int ReachedReQuire;
    [Tooltip("If true, the event only fires once when the requirement is reached")]
    public bool triggerOnce = true;

    private RoomCounter roomCounter;
    private bool hasTriggered = false;

    void Start()
    {
        roomCounter = FindObjectOfType<RoomCounter>();
        if (roomCounter == null)
        {
            Debug.LogError("RoomCounter not found!");
        }
    }

    void Update()
    {
        if (hasTriggered && triggerOnce) return;

        if (roomCounter != null && roomCounter.StairRoomCounter >= ReachedReQuire)
        {
            hasTriggered = true;
            OnStairRoomReached?.Invoke();
        }
    }
}
