using UnityEngine;
using UnityEngine.SceneManagement;

public class RushEntity : MonoBehaviour
{
    public float Speed = 10f;
    public float lifetime = 15f;

    private Hided hideStatus;

    void Start()
    {
        hideStatus = FindObjectOfType<Hided>();
        Destroy(gameObject, lifetime);
    }

    void LateUpdate()
    {
        transform.Translate(Vector2.right * Speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (hideStatus == null)
            {
                hideStatus = FindObjectOfType<Hided>();
            }

            if (hideStatus == null || !hideStatus.IsPlayerHide)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
        Door door = other.GetComponent<Door>();
        if (door != null)
        {
            Debug.Log("I'm in range of a door");
            door.UnlockDoor();
            door.needKey = false;
        }
    }
}


