using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PickupManager : MonoBehaviour
{
    public static PickupManager Instance { get; private set; }

    [Header("Run")]
    [Tooltip("Solo spawnea pickups cuando esta escena está activa.")]
    [SerializeField] string gameplaySceneName = "SCN_Labe";

    [Header("Candidate spawn points (transforms in scene).")]
    [SerializeField] List<Transform> candidatePoints = new();
    [Header("Pickup prefab (instantiated at chosen points).")]
    [SerializeField] Pickup pickupPrefab;

    [Header("If true, treat candidatePoints as already-placed Pickup GameObjects and just enable a subset.")]
    [SerializeField] bool useExistingPickups = false;
    [SerializeField] List<Pickup> existingPickups = new();

    readonly List<Pickup> spawned = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()  => SceneManager.sceneLoaded += HandleSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    void Start()
    {
        if (SceneManager.GetActiveScene().name == gameplaySceneName) BeginRun();
        else ResetRun();
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == gameplaySceneName) BeginRun();
        else ResetRun();
    }

    public void BeginRun()
    {
        ResetRun();
        // El número de pickups proviene SIEMPRE de la dificultad activa.
        int required = DifficultyManager.Instance ? DifficultyManager.Instance.GetSettings().pickupsRequired : 6;
        SpawnPickups(required);
    }

    public void ResetRun()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        spawned.Clear();

        if (useExistingPickups)
            for (int i = 0; i < existingPickups.Count; i++)
                if (existingPickups[i] != null) existingPickups[i].SetActiveCandidate(false);
    }

    void SpawnPickups(int amount)
    {
        if (useExistingPickups)
        {
            Shuffle(existingPickups);
            for (int i = 0; i < existingPickups.Count; i++)
                if (existingPickups[i] != null) existingPickups[i].SetActiveCandidate(i < amount);
            return;
        }

        if (pickupPrefab == null || candidatePoints.Count == 0) return;
        var picked = new List<Transform>(candidatePoints);
        Shuffle(picked);
        amount = Mathf.Min(amount, picked.Count);
        for (int i = 0; i < amount; i++)
        {
            var p = Instantiate(pickupPrefab, picked[i].position, picked[i].rotation);
            spawned.Add(p);
        }
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
