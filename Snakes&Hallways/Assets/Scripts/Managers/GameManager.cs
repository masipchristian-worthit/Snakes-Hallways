using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Playing, Paused, GameOver, Win }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Timer")]
    [SerializeField] float matchTime = 600f;
    [SerializeField] string gameOverScene = "GameOver";
    [SerializeField] string winScene = "Win";

    [Header("Refs")]
    [SerializeField] Transform player;
    public Transform Player => player;

    public GameState State { get; private set; } = GameState.Playing;
    public float TimeRemaining { get; private set; }
    public int PickupsCollected { get; private set; }
    public int PickupsRequired { get; private set; }

    public event Action<int, int> OnPickupCountChanged;
    public event Action<float> OnTimerChanged;
    public event Action<GameState> OnStateChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        TimeRemaining = matchTime;
        PickupsRequired = DifficultyManager.Instance ? DifficultyManager.Instance.GetSettings().pickupsRequired : 6;
        OnPickupCountChanged?.Invoke(PickupsCollected, PickupsRequired);
        OnTimerChanged?.Invoke(TimeRemaining);
    }

    void Update()
    {
        if (State != GameState.Playing) return;
        TimeRemaining -= Time.deltaTime;
        OnTimerChanged?.Invoke(TimeRemaining);
        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;
            TriggerGameOver();
        }
    }

    public void RegisterPickup()
    {
        if (State != GameState.Playing) return;
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
        SceneTransition.Instance?.FadeAndLoad(gameOverScene, 1.5f);
    }

    public void TriggerWin()
    {
        if (State == GameState.Win) return;
        State = GameState.Win;
        AudioManager.Instance?.PlayMusic(MusicId.Win);
        OnStateChanged?.Invoke(State);
        SceneTransition.Instance?.FadeAndLoad(winScene, 2f);
    }
}
