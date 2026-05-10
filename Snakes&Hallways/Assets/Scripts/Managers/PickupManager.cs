using System.Collections.Generic;
using UnityEngine;

public class PickupManager : MonoBehaviour
{
    public static PickupManager Instance { get; private set; }

    [Header("Candidate spawn points (transforms in scene).")]
    [SerializeField] List<Transform> candidatePoints = new();
    [Header("Pickup prefab (instantiated at chosen points).")]
    [SerializeField] Pickup pickupPrefab;

    [Header("If true, treat candidatePoints as already-placed Pickup GameObjects and just enable a subset.")]
    [SerializeField] bool useExistingPickups = false;
    [SerializeField] List<Pickup> existingPickups = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        int required = DifficultyManager.Instance ? DifficultyManager.Instance.GetSettings().pickupsRequired : 6;
        SpawnPickups(required);
    }

    void SpawnPickups(int amount)
    {
        if (useExistingPickups)
        {
            Shuffle(existingPickups);
            for (int i = 0; i < existingPickups.Count; i++)
                existingPickups[i].SetActiveCandidate(i < amount);
            return;
        }

        if (pickupPrefab == null || candidatePoints.Count == 0) return;
        var picked = new List<Transform>(candidatePoints);
        Shuffle(picked);
        amount = Mathf.Min(amount, picked.Count);
        for (int i = 0; i < amount; i++)
        {
            Instantiate(pickupPrefab, picked[i].position, picked[i].rotation);
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
