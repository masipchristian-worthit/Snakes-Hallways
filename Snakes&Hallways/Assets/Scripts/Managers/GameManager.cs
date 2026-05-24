using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Playing, Paused, GameOver, Win }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Timer")]
    [Tooltip("Tiempo inicial de partida (segundos). Se copia a 'currentTime' al empezar la run.")]
    [SerializeField] float matchTime = 600f;
    [Tooltip("Tiempo actual restante (segundos). Editable en runtime desde el inspector.")]
    [SerializeField] float currentTime;
    [SerializeField] string winScene = "Win";

    [Header("Run")]
    [Tooltip("Nombre de la escena de gameplay. Al cargarla se arranca la run; al salir se resetea.")]
    [SerializeField] string gameplaySceneName = "SCN_Labe";

    [Header("Refs")]
    [SerializeField] Transform player;
    public Transform Player => player;

    public GameState State { get; private set; } = GameState.Playing;
    public float TimeRemaining { get => currentTime; private set => currentTime = value; }
    public int PickupsCollected { get; private set; }
    public int PickupsRequired { get; private set; }
    public bool RunActive { get; private set; }
    public bool UnlimitedTime { get; private set; }

    public event Action<int, int> OnPickupCountChanged;
    public event Action<float> OnTimerChanged;
    public event Action<GameState> OnStateChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()  => SceneManager.sceneLoaded += HandleSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    void Start()
    {
        // Si arrancamos directamente en la escena de gameplay (sin pasar por intro), inicia la run.
        if (SceneManager.GetActiveScene().name == gameplaySceneName) BeginRun();
        else ResetRun();
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == gameplaySceneName) BeginRun();
        else ResetRun();
    }

    void Update()
    {
        if (!RunActive || State != GameState.Playing) return;
        if (UnlimitedTime) return;
        TimeRemaining -= Time.deltaTime;
        OnTimerChanged?.Invoke(TimeRemaining);
        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;
            TriggerGameOver();
        }
    }

    // ── Run lifecycle ─────────────────────────────────────────────────────
    public void BeginRun()
    {
        RunActive = true;
        State = GameState.Playing;
        Time.timeScale = 1f;

        var diff = DifficultyManager.Instance ? DifficultyManager.Instance.GetSettings() : null;
        float configured = diff != null ? diff.matchTimeSeconds : matchTime;
        UnlimitedTime = configured <= 0f;
        TimeRemaining = UnlimitedTime ? 0f : configured;
        PickupsCollected = 0;
        PickupsRequired = diff != null ? diff.pickupsRequired : 6;

        // Re-busca el player en la nueva escena.
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }
        var playerHealth = player != null
            ? player.GetComponentInChildren<PlayerHealth>()
            : FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.OnDied -= TriggerGameOver;
            playerHealth.OnDied += TriggerGameOver;
        }

        OnPickupCountChanged?.Invoke(PickupsCollected, PickupsRequired);
        OnTimerChanged?.Invoke(TimeRemaining);
        OnStateChanged?.Invoke(State);
    }

    public void ResetRun()
    {
        RunActive = false;
        State = GameState.Playing;
        Time.timeScale = 1f;
        TimeRemaining = matchTime;
        PickupsCollected = 0;
        player = null;
        OnPickupCountChanged?.Invoke(PickupsCollected, PickupsRequired);
        OnTimerChanged?.Invoke(TimeRemaining);
        OnStateChanged?.Invoke(State);
    }

    public void RegisterPickup()
    {
        if (!RunActive || State != GameState.Playing) return;
        PickupsCollected++;
        OnPickupCountChanged?.Invoke(PickupsCollected, PickupsRequired);
        AudioManager.Instance?.PlaySFX2D(SFXId.Pickup, Mathf.Lerp(0.8f, 1.2f, (float)PickupsCollected / Mathf.Max(1, PickupsRequired)));
        if (PickupsCollected >= PickupsRequired) PortalManager.Instance?.ActivatePortal();
    }

    public void Pause(bool paused)
    {
        if (State == GameState.GameOver || State == GameState.Win) return;
        State = paused ? GameState.Paused : GameState.Playing;
        Time.timeScale = paused ? 0f : 1f;
        AudioManager.Instance?.PlaySFX2D(paused ? SFXId.UIPause : SFXId.UIUnpause);
        OnStateChanged?.Invoke(State);
    }

    public void TriggerGameOver()
    {
        if (State == GameState.GameOver) return;
        State = GameState.GameOver;
        AudioManager.Instance?.PlayMusic(MusicId.GameOver);
        OnStateChanged?.Invoke(State);
        // La carga de SCN_DeathScene es responsabilidad EXCLUSIVA de DefeatManager.
        // Si no existe, no se carga nada (evita rutas duplicadas a la escena de muerte).
        DefeatManager.Instance?.TriggerDefeat();
    }

    public void TriggerWin()
    {
        if (State == GameState.Win) return;
        State = GameState.Win;
        AudioManager.Instance?.PlayMusic(MusicId.Win);
        OnStateChanged?.Invoke(State);
        if (VictoryManager.Instance == null)
            SceneTransition.Instance?.FadeAndLoad(winScene, 2f);
    }
}
