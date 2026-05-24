using UnityEngine;

/// <summary>
/// Coloca este script en un GameObject con un Collider en modo trigger.
/// Cuando el Player entre, dispara GameManager.TriggerWin() (que a su vez
/// llama a EndRun → limpieza de managers + corte de música) y warpea a
/// SCN_EndScene usando SceneTransition con fade.
///
/// Setup:
///   1. GameObject vacío con un BoxCollider/SphereCollider, marca "Is Trigger".
///   2. Tag del Player debe ser "Player".
///   3. Asigna nombre exacto de la escena fin (default "SCN_EndScene") en el Inspector.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WinCollider : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Nombre exacto de la escena fin a cargar (debe estar añadida en Build Settings).")]
    [SerializeField] string endSceneName = "SCN_EndScene";
    [Tooltip("Duración del fade a negro antes de cargar la escena fin.")]
    [SerializeField] float fadeTime = 1.5f;

    [Header("Behaviour")]
    [Tooltip("Si está activo, además del fade dispara GameManager.TriggerWin() para que se ejecute la limpieza (EndRun) y se reproduzca la música de victoria.")]
    [SerializeField] bool triggerWinState = true;
    [Tooltip("Si está activo, exige haber recogido todos los pickups antes de poder ganar. Útil si el collider está siempre presente.")]
    [SerializeField] bool requireAllPickups = false;

    bool fired;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (fired) return;
        if (!other.CompareTag("Player")) return;

        if (requireAllPickups)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.PickupsCollected < gm.PickupsRequired) return;
        }

        fired = true;

        // 1) Dispara el estado WIN (esto llama internamente a EndRun para limpiar
        //    pickups, parar música, deshabilitar el inteligente, etc.).
        if (triggerWinState) GameManager.Instance?.TriggerWin();

        // 2) Carga la escena fin con fade (usa el mismo overlay/dissolve que el resto).
        var st = SceneTransition.EnsureInstance();
        if (st != null) st.FadeAndLoad(endSceneName, fadeTime);
        else UnityEngine.SceneManagement.SceneManager.LoadScene(endSceneName);
    }
}
