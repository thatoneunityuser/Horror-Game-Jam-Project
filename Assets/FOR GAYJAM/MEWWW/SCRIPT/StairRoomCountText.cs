using UnityEngine;
using TMPro;
public class StairRoomCountText : MonoBehaviour
{
    public TextMeshProUGUI text;
    private RoomCounter roomCounter;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        roomCounter = FindObjectOfType<RoomCounter>();
        if (roomCounter == null)
        {
            Debug.LogError("RoomCounter not found!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (text == null) return;

        if (roomCounter != null && roomCounter.StairRoomCounter > 0)
        {
            text.text = roomCounter.StairRoomCounter.ToString();
        }
        else
        {
            text.text = "0";
        }
    }
}