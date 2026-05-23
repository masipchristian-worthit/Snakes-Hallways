using UnityEngine;

/// <summary>
/// Marca un punto de aparición en la escena. SceneWarp puede apuntar a uno de estos
/// por nombre para colocar al jugador al cargar la escena.
/// Colócalo en un GameObject vacío en la posición y rotación correctas.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Identificador único dentro de la escena. Debe coincidir con el spawnId de SceneWarp.")]
    [SerializeField] string spawnId = "Default";
    public string SpawnId => spawnId;

    /// <summary>Registra este SpawnPoint en SceneTransition al activarse.</summary>
    void OnEnable()  => SceneTransition.RegisterSpawnPoint(this);
    void OnDisable() => SceneTransition.UnregisterSpawnPoint(this);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.2f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, $"[Spawn] {spawnId}");
#endif
    }
}
