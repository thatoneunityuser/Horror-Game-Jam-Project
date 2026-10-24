using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class NameCheck
{
 
    public List<string> namecheck;
    public List<string> certian_text1,certian_text2,certian_text3;
}

public class NameChecker : MonoBehaviour
{

    public TMP_InputField nameInput;
    public TextMeshProUGUI outputText,outputText1,outputText2;
    public NameCheck nameCheck;




    

    private void PerformNameCheck(TextMeshProUGUI output)
    {
        if (nameCheck == null) return;
        if (nameCheck.namecheck == null) nameCheck.namecheck = new List<string>();
        if (nameCheck.certian_text1 == null) nameCheck.certian_text1 = new List<string>();

        int index = nameCheck.namecheck.IndexOf(nameInput.text);
        if (index >= 0)
        {
            if (index < nameCheck.certian_text1.Count)
            {
                output.text = nameCheck.certian_text1[index];
            }
            else
            {
                output.text = "สวัสดีจร้า";
            }
        }
        else
        {
            nameCheck.namecheck.Add(nameInput.text);
            nameCheck.certian_text1.Add("สวัสดีจร้า " + nameInput.text);
            output.text = nameCheck.certian_text1[nameCheck.certian_text1.Count - 1];
        }
    }

    private void PerformNameCheck1(TextMeshProUGUI output)
    {
        if (nameCheck == null) return;
        if (nameCheck.namecheck == null) nameCheck.namecheck = new List<string>();
        if (nameCheck.certian_text2 == null) nameCheck.certian_text2 = new List<string>();

        int index = nameCheck.namecheck.IndexOf(nameInput.text);
        if (index >= 0)
        {
            if (index < nameCheck.certian_text2.Count)
            {
                output.text = nameCheck.certian_text2[index];
            }
            else
            {
                output.text = "อันนี้โหมดธรรมดา";
            }
        }
        else
        {
            nameCheck.namecheck.Add(nameInput.text);
            nameCheck.certian_text2.Add("Default text for " + nameInput.text);
            output.text = nameCheck.certian_text2[nameCheck.certian_text2.Count - 1];
        }
    }

    private void PerformNameCheck2(TextMeshProUGUI output)
    {
        if (nameCheck == null) return;
        if (nameCheck.namecheck == null) nameCheck.namecheck = new List<string>();
        if (nameCheck.certian_text3 == null) nameCheck.certian_text3 = new List<string>();

        int index = nameCheck.namecheck.IndexOf(nameInput.text);
        if (index >= 0)
        {
            if (index < nameCheck.certian_text3.Count)
            {
                output.text = nameCheck.certian_text3[index];
            }
            else
            {
                output.text = "อันนี้โหมดไม่ธรรมดา";
            }
        }
        else
        {
            nameCheck.namecheck.Add(nameInput.text);
            nameCheck.certian_text3.Add("Default text for " + nameInput.text);
            output.text = nameCheck.certian_text3[nameCheck.certian_text3.Count - 1];
        }
    }

    public void CheckName()
    {
        PerformNameCheck(outputText);
    }

    public void CheckName1()
    {
        PerformNameCheck1(outputText1);
    }

    public void CheckName2()
    {
        PerformNameCheck2(outputText2);
    }
}


