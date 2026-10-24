using UnityEngine;

public class doubleclick : MonoBehaviour
{
    public int clicked;
    public GameObject pad1,pad2;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pad2.SetActive(false);
        
    }

    // Update is called once per frame
    public void Buttonclicked()
    {
        clicked++;
        if (clicked >= 2)
        {
            pad1.SetActive(false);
            pad2.SetActive(true);
        }
    }
}
