using UnityEngine;

public class CooldownDecreaser : MonoBehaviour
{
    private SummonTheEntityByRandom[] summoners;
    private bool hasTriggered = false;

    void Start()
    {
        summoners = FindObjectsOfType<SummonTheEntityByRandom>();
    }
    public void DecreaseCountDown()
    {
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
        if (hasTriggered) return;

        if (collision.CompareTag("Player"))
        {
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
    }
}
