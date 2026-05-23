using UnityEngine;

/// <summary>
/// Componente de teletransporte. Ponlo en un GameObject con un Collider marcado como Trigger.
///
/// Modos:
///   • SameScene  – teleporta al jugador al SpawnPoint con el targetSpawnId dentro de la escena actual.
///   • LoadScene  – fade a negro y carga otra escena, colocando al jugador en targetSpawnId (opcional).
///
/// Uso:
///   1. Añade este script + un Collider (IsTrigger = true) al portal / puerta / zona.
///   2. Configura el modo y rellena targetSceneName / targetSpawnId.
///   3. Para SameScene: crea GameObjects con SpawnPoint.cs en los destinos de la escena.
///   4. Para LoadScene: crea un SpawnPoint en la escena destino con el mismo targetSpawnId.
///
/// También puedes llamar a Warp() por código (desde un botón, PortalManager, etc.).
/// </summary>
public class SceneWarp : MonoBehaviour
{
    public enum WarpMode { SameScene, LoadScene }

    [Header("Modo")]
    [SerializeField] WarpMode mode = WarpMode.LoadScene;

    [Header("Destino")]
    [Tooltip("Nombre de la escena destino (solo para modo LoadScene).")]
    [SerializeField] string targetSceneName = "";
    [Tooltip("Id del SpawnPoint donde aparecerá el jugador. OPCIONAL en LoadScene: si está vacío se carga la escena tal cual y el jugador aparece donde lo coloque la propia escena. OBLIGATORIO en SameScene.")]
    [SerializeField] string targetSpawnId = "";

    [Header("Transición")]
    [SerializeField] float fadeTime = 0.5f;

    [Header("Trigger")]
    [Tooltip("Si está activo, el warp se dispara cuando el Player entra al trigger.")]
    [SerializeField] bool triggerOnEnter = true;
    [Tooltip("Bloquea el warp si el jugador ya está muriéndose/game over.")]
    [SerializeField] bool requirePlaying = true;

    bool used; // evita disparar múltiples veces en el mismo frame

    // ── Trigger automático ────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!triggerOnEnter) return;
        if (!other.CompareTag("Player")) return;
        Warp();
    }

    // ── API pública ───────────────────────────────────────────────────────────
    /// <summary>Ejecuta el warp manualmente (p.ej. desde PortalManager o un botón UI).</summary>
    public void Warp()
    {
        if (used) return;
        if (requirePlaying && GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

        used = true;

        switch (mode)
        {
            case WarpMode.SameScene:
                if (string.IsNullOrEmpty(targetSpawnId))
                {
                    Debug.LogWarning($"[SceneWarp] SameScene: targetSpawnId vacío en '{name}'.");
                    used = false;
                    return;
                }
                SceneTransition.Instance?.WarpInScene(targetSpawnId, fadeTime);
                break;

            case WarpMode.LoadScene:
                if (string.IsNullOrEmpty(targetSceneName))
                {
                    Debug.LogWarning($"[SceneWarp] LoadScene: targetSceneName vacío en '{name}'.");
                    used = false;
                    return;
                }
                if (!string.IsNullOrEmpty(targetSpawnId))
                    SceneTransition.Instance?.FadeAndLoadWithSpawn(targetSceneName, targetSpawnId, fadeTime);
                else
                    SceneTransition.Instance?.FadeAndLoad(targetSceneName, fadeTime);
                break;
        }
    }

    /// <summary>Permite reusar el warp (p.ej. si la puerta se cierra y vuelve a abrirse).</summary>
    public void ResetWarp() => used = false;

    void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;
        Gizmos.color = new Color(0.8f, 0.3f, 1f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is BoxCollider box)
            Gizmos.DrawCube(box.center, box.size);
        else if (col is SphereCollider sphere)
            Gizmos.DrawSphere(sphere.center, sphere.radius);
    }
}
