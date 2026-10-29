using UnityEngine;

public class FLASHLIGHT : MonoBehaviour
{
    public GameObject Flashlight;
    public KeyCode toggleKey = KeyCode.F;
    public bool startOn = false;
    public AudioSource a;

    private bool isOn;

    public bool IsOn
    {
        get => isOn;
        set => SetFlashlight(value, false);
    }

    void Start()
    {
        SetFlashlight(startOn, false);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleFlashlight();
        }
    }

    public void ToggleFlashlight()
    {
        SetFlashlight(!isOn, true);
    }

    public void SetFlashlight(bool state, bool playSound = true)
    {
        isOn = state;
        if (playSound && a != null)
        {
            a.Play();
        }

        ApplyLightState();

        if (FlashlightUI.Instance != null && FlashlightUI.Instance.isFlashlightOn != isOn)
        {
            FlashlightUI.Instance.SetFlashlight(isOn);
        }
    }

    public void SetLightStateDirect(bool state)
    {
        isOn = state;
        ApplyLightState();
    }

    public void ApplyLightState()
    {
        if (Flashlight != null)
        {
            Flashlight.SetActive(isOn);

            // Re-enable / disable any Light or Light2D components attached to Flashlight
            Behaviour[] lightBehaviours = Flashlight.GetComponentsInChildren<Behaviour>(true);
            foreach (var b in lightBehaviours)
            {
                if (b != null && (b.GetType().Name.Contains("Light2D") || b.GetType().Name == "Light"))
                {
                    b.enabled = isOn;
                }
            }
        }
    }
}
