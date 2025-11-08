using PurrNet;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuEndGame : MonoBehaviour
{
    [PurrScene, SerializeField] private string nextScene;
    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartGame()
    {
        SceneManager.LoadSceneAsync(nextScene);
    }
}
