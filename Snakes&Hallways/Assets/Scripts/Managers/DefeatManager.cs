using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Único punto de entrada hacia SCN_DeathScene.
/// - DontDestroyOnLoad: persiste entre escenas como singleton.
/// - Solo arma listeners (PlayerHealth, GameManager state) en la escena de gameplay.
/// - Al disparar derrota, hace fade a negro y carga SCN_DeathScene.
///
/// IMPORTANTE: ningún otro script debe cargar SCN_DeathScene directamente.
/// GameManager.TriggerGameOver delega aquí.
/// </summary>
public class DefeatManager : MonoBehaviour
{
    public static DefeatManager Instance { get; private set; }

    [Header("Run")]
    [Tooltip("Escena en la que se arma este manager (solo escucha a PlayerHealth/GameManager aquí).")]
    [SerializeField] string gameplaySceneName = "SCN_Labe";

    [Header("Death scene")]
    [SerializeField] string deathSceneName = "SCN_DeathScene";
    [SerializeField] float fadeTime = 1.5f;

    PlayerHealth playerHealth;
    bool isDefeated;
    bool armed;

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
        if (SceneManager.GetActiveScene().name == gameplaySceneName) Arm();
        else Disarm();
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == gameplaySceneName) Arm();
        else Disarm();
    }

    void Arm()
    {
        // Resetea el flag al volver a entrar a gameplay (permite morir de nuevo en otra run).
        isDefeated = false;

        var p = GameObject.FindGameObjectWithTag("Player");
        playerHealth = p != null ? p.GetComponentInChildren<PlayerHealth>() : null;
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDeath;
            playerHealth.OnDied += HandlePlayerDeath;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
            GameManager.Instance.OnStateChanged += HandleStateChanged;
        }

        armed = true;
    }

    void Disarm()
    {
        if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDeath;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
        playerHealth = null;
        armed = false;
        isDefeated = false;
    }

    void HandlePlayerDeath() => TriggerDefeat();

    void HandleStateChanged(GameState s)
    {
        if (s == GameState.GameOver) TriggerDefeat();
    }

    /// <summary>Carga SCN_DeathScene con fade. Idempotente.</summary>
    public void TriggerDefeat()
    {
        if (isDefeated || !armed) return;
        isDefeated = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        AudioManager.Instance?.PlayMusic(MusicId.GameOver);
        SceneTransition.EnsureInstance()?.FadeAndLoad(deathSceneName, fadeTime);
    }
}
