using UnityEngine;

public class FLASHLIGHT : MonoBehaviour
{
    public GameObject Flashlight;
    public KeyCode toggleKey = KeyCode.F;
    public bool startOn = false;
    public AudioSource a;

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
            a.Play();
            isOn = !isOn;
            if (Flashlight != null)
            {
                Flashlight.SetActive(isOn);
            }
        }
    }
}
