using System.Collections;
using UnityEngine;

public class Hided : MonoBehaviour
{
    [SerializeField]
    private bool isPlayerHide = false;

    public bool IsPlayerHide
    {
        get => isPlayerHide;
        set => SetHide(value);
    }


    public SpriteRenderer HiderSprite;
    public GameObject Hider;
    public float maxHideDuration = 10f;

    public float lastHideTime { get; private set; } = -999f;
    public bool JustHid => (Time.time - lastHideTime) <= 0.5f;

    private Coroutine hideTimerCoroutine;

    void Start()
    {
        if (Hider == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Hider = player;
            }
        }
        if (HiderSprite == null && Hider != null)
        {
            HiderSprite = Hider.GetComponent<SpriteRenderer>();
        }

        SetHide(isPlayerHide);
    }

    public void SetHide(bool hide)
    {
        isPlayerHide = hide;
        lastHideTime = Time.time;

        if (HiderSprite == null && Hider != null)
        {
            HiderSprite = Hider.GetComponent<SpriteRenderer>();
        }

        if (HiderSprite != null)
        {
            HiderSprite.sortingOrder = isPlayerHide ? 2 : 5;
        }

        // Cancel previous timer
        if (hideTimerCoroutine != null)
        {
            StopCoroutine(hideTimerCoroutine);
            hideTimerCoroutine = null;
        }

        // Start countdown to unhide if player is hiding
        if (isPlayerHide)
        {
            hideTimerCoroutine = StartCoroutine(HideLimitTime());
        }
    }

    IEnumerator HideLimitTime()
    {
        yield return new WaitForSeconds(maxHideDuration);
        SetHide(false);
    }
}