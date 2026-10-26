using UnityEngine;

public class FLASHLIGHT : MonoBehaviour
{
    public GameObject Flashlight;
    public KeyCode toggleKey = KeyCode.F;
    public bool startOn = false;

    private bool isOn;

    void Start()
    {
        isOn = startOn;
        if (Flashlight != null)
        {
            Flashlight.SetActive(isOn);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isOn = !isOn;
            if (Flashlight != null)
            {
                Flashlight.SetActive(isOn);
            }
        }
    }
}
