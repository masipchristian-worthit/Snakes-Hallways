using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// AUTORIDAD ÚNICA de la derrota. GameManager solo cambia GameState a GameOver
/// y emite OnStateChanged — DefeatManager lo escucha y se encarga de TODO lo demás:
///  - música de muerte (PlayMusic GameOver)
///  - fade-to-black + carga de SCN_DeathScene
///  - liberar el cursor
///
/// También se suscribe directamente a PlayerHealth.OnDied como atajo idempotente
/// (el flag isDefeated evita doble disparo).
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

    [Header("Audio")]
    [Tooltip("Música que se reproduce al disparar la derrota. Default: GameOver. Si pones None no toca la música.")]
    [SerializeField] MusicId deathMusic = MusicId.GameOver;
    [Tooltip("Crossfade hacia la pista de muerte (segundos).")]
    [SerializeField] float deathMusicFade = 0.8f;

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

    void HandlePlayerDeath()
    {
        // PlayerHealth murió directamente: empujamos el estado y dejamos que el chain habitual
        // (OnStateChanged → TriggerDefeat) lo procese. Si GameManager no existe, hacemos
        // TriggerDefeat directamente para no perder la derrota.
        if (GameManager.Instance != null) GameManager.Instance.TriggerGameOver();
        else TriggerDefeat();
    }

    void HandleStateChanged(GameState s)
    {
        if (s == GameState.GameOver) TriggerDefeat();
    }

    /// <summary>Carga SCN_DeathScene con fade y arranca la música de muerte. Idempotente.</summary>
    public void TriggerDefeat()
    {
        if (isDefeated || !armed) return;
        isDefeated = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        if (deathMusic != MusicId.None)
            AudioManager.Instance?.PlayMusic(deathMusic, deathMusicFade);

        SceneTransition.EnsureInstance()?.FadeAndLoad(deathSceneName, fadeTime);
    }
}
