using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Único responsable de qué pickups están activos durante una run.
///
/// Política (un solo path para evitar duplicaciones con GameManager):
///   - Al cargar la escena de gameplay busca TODOS los Pickup en escena (incluso inactivos).
///   - Activa el <see cref="mandatoryPickup"/> SIEMPRE (cuenta como uno de los requeridos).
///   - Del resto, baraja y activa los necesarios para llegar a PickupsRequired (dificultad).
///   - El resto quedan SetActive(false).
///
/// GameManager solo lleva el contador (PickupsCollected/PickupsRequired) y emite eventos.
/// </summary>
public class PickupManager : MonoBehaviour
{
    public static PickupManager Instance { get; private set; }

    [Header("Mandatory pickup")]
    [Tooltip("Pickup OBLIGATORIO. Nunca se apaga; SIEMPRE aparece en la run y cuenta dentro de PickupsRequired.")]
    [SerializeField] Pickup mandatoryPickup;

    readonly List<Pickup> allScenePickups = new();
    public IReadOnlyList<Pickup> AllScenePickups => allScenePickups;
    public int PickupsActiveInScene { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // NOTA: ya no nos suscribimos a SceneManager.sceneLoaded. El GameManager nos llama
    // directamente desde su BeginRun() para garantizar el orden (PickupsRequired se
    // ajusta DESPUÉS de activar pickups). Mantenemos BeginRun/ResetRun públicos.

    public void BeginRun()
    {
        int required = DifficultyManager.Instance ? DifficultyManager.Instance.GetSettings().pickupsRequired : 6;
        SetupScenePickups(required);
    }

    public void ResetRun()
    {
        allScenePickups.Clear();
        PickupsActiveInScene = 0;
    }

    /// <summary>
    /// Activa exactamente <paramref name="required"/> pickups en la escena:
    /// el mandatorio siempre + (required-1) aleatorios del resto. El resto se apagan.
    /// </summary>
    void SetupScenePickups(int required)
    {
        allScenePickups.Clear();

        var all = FindObjectsByType<Pickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0)
        {
            PickupsActiveInScene = 0;
            return;
        }
        allScenePickups.AddRange(all);

        bool hasMandatory = mandatoryPickup != null && allScenePickups.Contains(mandatoryPickup);
        if (mandatoryPickup != null && !hasMandatory)
            Debug.LogWarning($"[PickupManager] mandatoryPickup '{mandatoryPickup.name}' no está en la escena activa. Se ignora.", this);

        var candidates = new List<Pickup>(allScenePickups.Count);
        for (int i = 0; i < allScenePickups.Count; i++)
        {
            if (allScenePickups[i] == null) continue;
            if (hasMandatory && allScenePickups[i] == mandatoryPickup) continue;
            candidates.Add(allScenePickups[i]);
        }

        Shuffle(candidates);

        int reserved = hasMandatory ? 1 : 0;
        int activateFromCandidates = Mathf.Clamp(required - reserved, 0, candidates.Count);

        if (hasMandatory) mandatoryPickup.gameObject.SetActive(true);
        for (int i = 0; i < candidates.Count; i++)
            candidates[i].gameObject.SetActive(i < activateFromCandidates);

        PickupsActiveInScene = activateFromCandidates + reserved;

        if (PickupsActiveInScene < required)
            Debug.LogWarning($"[PickupManager] Escena con {allScenePickups.Count} pickups, dificultad pide {required}. Activados {PickupsActiveInScene}.", this);

        // Comunicar a GameManager el número REAL de pickups activos para que PickupsRequired
        // coincida (si la escena tiene menos pickups que la dificultad, sin esto el portal
        // no se encendía nunca porque collected nunca llegaba al required teórico).
        if (GameManager.Instance != null)
            GameManager.Instance.SetPickupsRequired(PickupsActiveInScene);
    }

    // ── DEBUG / TESTING ─────────────────────────────────────────────────────
    /// <summary>
    /// Recoge instantáneamente TODOS los pickups activos de la escena. Útil para
    /// testear el portal sin recorrer el mapa.
    /// Cómo usar: en el inspector, click derecho sobre el componente PickupManager →
    /// "Collect All Pickups (debug)".
    /// </summary>
    [ContextMenu("Collect All Pickups (debug)")]
    public void DebugCollectAllPickups()
    {
        if (allScenePickups == null || allScenePickups.Count == 0)
        {
            Debug.LogWarning("[PickupManager] No hay pickups registrados aún. Inicia la run primero.", this);
            return;
        }
        int collected = 0;
        for (int i = 0; i < allScenePickups.Count; i++)
        {
            var p = allScenePickups[i];
            if (p == null || !p.gameObject.activeInHierarchy) continue;
            // Llama al mismo flujo que un OnTriggerEnter: registra en GameManager + desactiva.
            p.InteractPickup();
            collected++;
        }
        Debug.Log($"[PickupManager] DEBUG: recogidos {collected} pickups. PickupsCollected={GameManager.Instance?.PickupsCollected}, Required={GameManager.Instance?.PickupsRequired}.", this);
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
