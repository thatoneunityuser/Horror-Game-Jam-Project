using UnityEngine;
using UnityEngine.SceneManagement;
public class ChangeScenePlus : MonoBehaviour
{

    public ClickToChooseScene clickToChooseScene;
    public string NextScene;
  

    public void Start()
    {
        clickToChooseScene = FindObjectOfType<ClickToChooseScene>();
    }
    public void ChangeScene()
    {
        SceneManager.LoadScene(NextScene);

    }
    public void ChangeScenePlusPlus()
    {
        
        clickToChooseScene.LoadScene();
    }

 
}
