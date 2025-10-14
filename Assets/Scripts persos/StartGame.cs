using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGame : MonoBehaviour
{
    [SerializeField] private string sceneName = "NomDeTaScene"; // ← à personnaliser dans l’inspecteur

    public void Launch()
    {
        SceneManager.LoadScene(sceneName);
    }
}
