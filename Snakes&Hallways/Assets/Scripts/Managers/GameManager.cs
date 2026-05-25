using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Playing, Paused, GameOver, Win }

/// <summary>
/// Estado global de la run. Responsabilidades:
///   - Timer + estado (Playing/Paused/GameOver/Win) + eventos.
///   - Spawn de pickups: al iniciar la run busca TODOS los Pickup en escena y deja
///     activos solo los necesarios según la dificultad (apaga el resto al azar).
///   - Limpieza ligera al terminar la run (EndRun).
///
/// NO se encarga de:
///   - Cargar SCN_DeathScene ni reproducir música GameOver  → DefeatManager
///   - Cargar SCN_EndingScene ni reproducir música Win      → WinCollider + SceneMusicController
///   - Activar el portal                                     → eliminado (PortalManager obsoleto)
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Timer")]
    [Tooltip("Tiempo inicial de partida (segundos). Solo se usa como fallback si DifficultyManager no está presente.")]
    [SerializeField] float matchTime = 600f;
    [Tooltip("Tiempo actual restante (segundos). Editable en runtime desde el inspector.")]
    [SerializeField] float currentTime;

    [Header("Run")]
    [Tooltip("Nombre de la escena de gameplay. Al cargarla se arranca la run; al salir se resetea.")]
    [SerializeField] string gameplaySceneName = "SCN_Labe";

    [Header("Refs")]
    [SerializeField] Transform player;
    public Transform Player => player;

    // ── Pickups ────────────────────────────────────────────────────────────
    // Toda la lógica de activación/spawn de pickups vive ahora en PickupManager
    // (incluido el mandatoryPickup). GameManager solo cuenta y emite eventos.
    public IReadOnlyList<Pickup> AllScenePickups =>
        PickupManager.Instance != null ? PickupManager.Instance.AllScenePickups : System.Array.Empty<Pickup>();

    public GameState State { get; private set; } = GameState.Playing;
    public float TimeRemaining { get => currentTime; private set => currentTime = value; }
    public int PickupsCollected { get; private set; }
    public int PickupsRequired { get; private set; }
    public int PickupsActiveInScene => PickupManager.Instance != null ? PickupManager.Instance.PickupsActiveInScene : 0;
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
        // OJO: DefeatManager YA escucha PlayerHealth.OnDied por su lado.
        // No lo duplicamos aquí — GameManager solo emite OnStateChanged.

        // La activación de pickups en escena la hace PickupManager. La llamamos
        // DIRECTAMENTE aquí (en vez de depender de su propio sceneLoaded) para garantizar
        // el orden: primero GameManager fija PickupsRequired, luego PickupManager activa
        // pickups y reescribe PickupsRequired con el número REAL activado, todo en el
        // mismo frame. Sin esto, una race entre ambos handlers dejaba el portal sin
        // encenderse cuando collected != required teórico.
        if (PickupManager.Instance != null)
            PickupManager.Instance.BeginRun();

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

    /// <summary>
    /// Lo invoca <see cref="PickupManager"/> después de activar pickups en escena para
    /// AJUSTAR PickupsRequired al número de pickups REALMENTE activos. Sin esto, si la
    /// escena tiene menos pickups que los que pide la dificultad, el portal NUNCA se
    /// encendía (collected nunca llegaba a required).
    /// </summary>
    public void SetPickupsRequired(int actualActive)
    {
        if (actualActive <= 0) return;
        PickupsRequired = actualActive;
        OnPickupCountChanged?.Invoke(PickupsCollected, PickupsRequired);
    }

    public void RegisterPickup()
    {
        if (!RunActive || State != GameState.Playing) return;
        PickupsCollected++;
        OnPickupCountChanged?.Invoke(PickupsCollected, PickupsRequired);
        AudioManager.Instance?.PlaySFX2D(SFXId.Pickup, Mathf.Lerp(0.8f, 1.2f, (float)PickupsCollected / Mathf.Max(1, PickupsRequired)));

        // Activación DIRECTA del portal cuando se llega al objetivo. No dependemos del
        // evento OnPickupCountChanged (que WinCollider podría perderse si su GameObject
        // está SetActive(false) en escena hasta el momento de activarse).
        if (AllPickupsCollected) TryActivateAllPortals();
    }

    /// <summary>
    /// Busca TODOS los WinCollider (incluso los inactivos) y les pide que se enciendan.
    /// Robusto frente a portales que se mantienen inactivos hasta el final de la run.
    /// </summary>
    void TryActivateAllPortals()
    {
        var portals = FindObjectsByType<WinCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (portals == null || portals.Length == 0)
        {
            Debug.LogWarning("[GameManager] AllPickupsCollected pero no se encontró ningún WinCollider en escena.");
            return;
        }
        for (int i = 0; i < portals.Length; i++)
        {
            if (portals[i] != null) portals[i].ActivatePortal();
        }
    }

    public bool AllPickupsCollected => PickupsRequired > 0 && PickupsCollected >= PickupsRequired;

    public void Pause(bool paused)
    {
        if (State == GameState.GameOver || State == GameState.Win) return;
        State = paused ? GameState.Paused : GameState.Playing;
        Time.timeScale = paused ? 0f : 1f;
        AudioManager.Instance?.PlaySFX2D(paused ? SFXId.UIPause : SFXId.UIUnpause);
        OnStateChanged?.Invoke(State);
    }

    /// <summary>
    /// Solo cambia estado a GameOver y emite el evento. La gestión completa
    /// (música, fade, carga de SCN_DeathScene) la hace DefeatManager escuchando
    /// OnStateChanged o suscrito a PlayerHealth.OnDied directamente.
    /// </summary>
    public void TriggerGameOver()
    {
        if (State == GameState.GameOver) return;
        State = GameState.GameOver;
        EndRun(won: false);
        OnStateChanged?.Invoke(State);
    }

    /// <summary>
    /// Solo cambia estado a Win y emite el evento. El cambio de escena y el fade
    /// los dispara WinCollider. La música de la ending la pone SceneMusicController
    /// al cargar SCN_EndingScene.
    /// </summary>
    public void TriggerWin()
    {
        if (State == GameState.Win) return;
        State = GameState.Win;
        EndRun(won: true);
        OnStateChanged?.Invoke(State);
    }

    /// <summary>
    /// Limpieza ligera al terminar la run. Aborta hint loop del minotauro y resetea contadores.
    /// La música la matan DefeatManager / SceneMusicController según corresponda.
    /// </summary>
    public void EndRun(bool won)
    {
        RunActive = false;

        // Si el inteligente sigue suelto, deshabilítalo para que no siga lanzando
        // hints/teleports tras la pantalla de fin.
        var brain = EnemyAIInteligent.Instance;
        if (brain != null) brain.enabled = false;

        // Reset de contadores (no del player ref por si la escena se mantiene un instante).
        PickupsCollected = 0;
        OnPickupCountChanged?.Invoke(PickupsCollected, PickupsRequired);
    }
}
