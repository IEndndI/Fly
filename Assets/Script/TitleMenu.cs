using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement; 

public class TitleMenu : MonoBehaviour
{
    public string gameSceneName = "SampleScene";

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);    // 空2：加载场景
    }

    public void QuitGame()
    {
        Application.Quit();                  // 空3：退出游戏
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}