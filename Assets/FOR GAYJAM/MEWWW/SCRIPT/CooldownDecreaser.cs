using UnityEngine;

public class CooldownDecreaser : MonoBehaviour
{
    private SummonTheEntityByRandom[] summoners;
    private bool hasTriggered = false;

    void Start()
    {
        summoners = FindObjectsOfType<SummonTheEntityByRandom>();
    }
    // Can be called from a UnityEvent (e.g. Door onOpen) OR by the trigger.
    // Either way it only counts ONCE per CooldownDecreaser.
    public void DecreaseCountDown()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        if (summoners == null || summoners.Length == 0)
        {
            summoners = FindObjectsOfType<SummonTheEntityByRandom>();
        }

        if (summoners != null)
        {
            foreach (var summoner in summoners)
            {
                if (summoner != null)
                {
                    summoner.DecreaseCountDown();
                }
            }
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // Unity sends trigger messages to disabled scripts too
        if (!enabled || hasTriggered) return;

        if (collision.CompareTag("Player"))
        {
            DecreaseCountDown();
        }
    }
}
