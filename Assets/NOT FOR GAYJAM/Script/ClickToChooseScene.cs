using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ClickToChooseScene : MonoBehaviour
{
    public string sceneName;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void ChooseSceneName(string SceneName)
    {
        sceneName = SceneName;
    }

    public void LoadScene()
    {
        SceneManager.LoadScene(sceneName);
    }

    

  
}
