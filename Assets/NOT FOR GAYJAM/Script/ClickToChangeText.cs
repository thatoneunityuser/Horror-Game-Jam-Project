using UnityEngine;
using TMPro;
public class ClickToChangeText : MonoBehaviour
{
    public TextMeshProUGUI text;
    public void ChangeText(string newText)
    {
        text.text = newText;
    }

}
