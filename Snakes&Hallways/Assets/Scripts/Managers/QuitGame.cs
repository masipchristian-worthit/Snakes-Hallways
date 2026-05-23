using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class QuitGame : MonoBehaviour
{
    /// <summary>
    /// Asigna este método al OnClick() del botón de salir.
    /// En el editor detiene el Play Mode; en build cierra la aplicación.
    /// </summary>
    public void Quit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
