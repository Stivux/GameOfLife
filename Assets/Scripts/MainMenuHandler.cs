using UnityEngine;
using UnityEngine.SceneManagement; 

public class MainMenuHandler : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameOfLife";

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    
    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }
}