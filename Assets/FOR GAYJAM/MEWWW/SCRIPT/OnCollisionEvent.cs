using UnityEngine;
using UnityEngine.Events;
public class OnCollisionEvent : MonoBehaviour
{
    public UnityEvent OnCollisionEnter;
    public UnityEvent OnCollisionExit;
    public UnityEvent OnCollisionStay;
    private void OnCollisionEnter2D(Collision2D collision)
    {
        OnCollisionEnter?.Invoke();
    }
    private void OnCollisionExit2D(Collision2D collision)
    {
        OnCollisionExit?.Invoke();
    }
    private void OnCollisionStay2D(Collision2D collision)
    {
        OnCollisionStay?.Invoke();
    }
}
