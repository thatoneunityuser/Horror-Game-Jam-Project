using UnityEngine;

public class Johnnylonglaser : MonoBehaviour
{
    public float dmg;
    void OnTriggerStay2D(Collider2D other)
    {
        if(other.CompareTag("Player"))
        {
            other.GetComponent<PlayerStats>().TakeDamage(dmg);
        }
    }
   
}
