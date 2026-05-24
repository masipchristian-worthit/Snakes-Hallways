using UnityEngine;

public enum Difficulty { Easy, Medium, Hard, Impossible }

[System.Serializable]
public class DifficultySettings
{
    public Difficulty level;
    public int pickupsRequired = 6;
    [Tooltip("0..1 — base aggression of the intelligent AI feeding hints to the blind AI.")]
    [Range(0f, 1f)] public float baseAggression = 0.3f;
    [Tooltip("0..1 — chance the minotaur knows player position permanently. 1 = Impossible.")]
    [Range(0f, 1f)] public float omniscience = 0f;
    [Tooltip("Probability of room-spawn when player enters a Room collider and minotaur is NOT chasing.")]
    [Range(0f, 1f)] public float roomSpawnChance = 0.15f;
    [Tooltip("Distance around player at which minotaur respawns when player exits a Room. Lower = harder.")]
    public float postRoomSpawnDistance = 30f;
    [Tooltip("How much sound investigation matters: 0=ignore, 1=fully react.")]
    [Range(0f, 1f)] public float soundReactivity = 0.3f;
    [Tooltip("Multiplier applied to chase speed.")]
    public float chaseSpeedMul = 1f;
    [Tooltip("Multiplier on hint frequency from intelligent AI.")]
    public float hintFrequencyMul = 1f;
    [Tooltip("Segundos que el enemigo sigue persiguiendo al perder línea de visión antes de volver a patrullar/investigar.")]
    public float chaseMemorySeconds = 3f;
    [Tooltip("Probabilidad (0..1) de ignorar un hint recibido del Inteligente. Mayor en fácil.")]
    [Range(0f, 1f)] public float hintIgnoreChance = 0.5f;
    [Tooltip("Cooldown (s) entre ataques sucesivos.")]
    public float attackCooldown = 2.5f;
    [Tooltip("Segundos que el enemigo se queda en modo Walk (sin poder atacar/correr) tras impactar.")]
    public float postAttackWalkSeconds = 4f;
    [Tooltip("Duración de la partida en segundos. 0 o negativo = sin límite de tiempo.")]
    public float matchTimeSeconds = 1500f;
}

public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    [SerializeField] Difficulty current = Difficulty.Medium;
    [SerializeField] DifficultySettings easy       = new() { level = Difficulty.Easy,       pickupsRequired = 4,  baseAggression = 0.1f,  omniscience = 0f,    roomSpawnChance = 0.05f, postRoomSpawnDistance = 60f, soundReactivity = 0f,    chaseSpeedMul = 0.85f, hintFrequencyMul = 0.5f, chaseMemorySeconds = 1.5f, hintIgnoreChance = 0.7f, attackCooldown = 4f,   postAttackWalkSeconds = 6f, matchTimeSeconds = 0f };
    [SerializeField] DifficultySettings medium     = new() { level = Difficulty.Medium,     pickupsRequired = 6,  baseAggression = 0.35f, omniscience = 0.1f,  roomSpawnChance = 0.2f,  postRoomSpawnDistance = 35f, soundReactivity = 0.5f,  chaseSpeedMul = 1f,    hintFrequencyMul = 1f,   chaseMemorySeconds = 3f,   hintIgnoreChance = 0.4f, attackCooldown = 3f,   postAttackWalkSeconds = 4f, matchTimeSeconds = 1500f };
    [SerializeField] DifficultySettings hard       = new() { level = Difficulty.Hard,       pickupsRequired = 8,  baseAggression = 0.6f,  omniscience = 0.35f, roomSpawnChance = 0.35f, postRoomSpawnDistance = 18f, soundReactivity = 0.85f, chaseSpeedMul = 1.15f, hintFrequencyMul = 1.6f, chaseMemorySeconds = 5f,   hintIgnoreChance = 0.15f, attackCooldown = 2f,   postAttackWalkSeconds = 2f, matchTimeSeconds = 1200f };
    [SerializeField] DifficultySettings impossible = new() { level = Difficulty.Impossible, pickupsRequired = 10, baseAggression = 1f,    omniscience = 1f,    roomSpawnChance = 0.6f,  postRoomSpawnDistance = 8f,  soundReactivity = 1f,    chaseSpeedMul = 1.3f,  hintFrequencyMul = 2.5f, chaseMemorySeconds = 8f,   hintIgnoreChance = 0f,    attackCooldown = 1f,   postAttackWalkSeconds = 0.5f, matchTimeSeconds = 600f };

    public Difficulty Current => current;

    const string PrefKey = "SH_Difficulty";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (PlayerPrefs.HasKey(PrefKey))
        {
            int v = PlayerPrefs.GetInt(PrefKey);
            if (System.Enum.IsDefined(typeof(Difficulty), v))
                current = (Difficulty)v;
        }
    }

    public void SetDifficulty(Difficulty d)
    {
        current = d;
        PlayerPrefs.SetInt(PrefKey, (int)d);
        PlayerPrefs.Save();
    }

    public DifficultySettings GetSettings()
    {
        return current switch
        {
            Difficulty.Easy => easy,
            Difficulty.Hard => hard,
            Difficulty.Impossible => impossible,
            _ => medium
        };
    }

    /// <summary>Returns 0..1 value mixing base difficulty + pickup progress.</summary>
    public float GetEscalation(int pickupsCollected, int pickupsTotal)
    {
        var s = GetSettings();
        float progress = pickupsTotal <= 0 ? 0f : Mathf.Clamp01((float)pickupsCollected / pickupsTotal);
        return Mathf.Clamp01(s.baseAggression + progress * (1f - s.baseAggression));
    }
}
