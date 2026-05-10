using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Pause Panel (slides from left)")]
    [SerializeField] RectTransform pausePanel;
    [SerializeField] Button resumeBtn;
    [SerializeField] Button optionsBtn;
    [SerializeField] Button mainMenuBtn;
    [SerializeField] Button quitBtn;
    [SerializeField] string mainMenuScene = "MainMenu";

    [Header("HUD Panel (slides from left)")]
    [SerializeField] RectTransform hudPanel;
    [SerializeField] TMP_Text pickupCountText;
    [SerializeField] TMP_Text timerText;
    [SerializeField] RawImage minimapImage;

    [Header("Slide Tween")]
    [SerializeField] float slideTime = 0.3f;
    [SerializeField] float panelOffscreenX = -600f;
    [SerializeField] float panelOnscreenX = 0f;

    bool isPaused;
    Coroutine pauseTween;
    Coroutine hudTween;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (resumeBtn) resumeBtn.onClick.AddListener(() => { TogglePause(false); });
        if (optionsBtn) optionsBtn.onClick.AddListener(() => { /* hook options */ AudioManager.Instance?.PlaySFX2D(SFXId.UISelect); });
        if (mainMenuBtn) mainMenuBtn.onClick.AddListener(() => { Time.timeScale = 1f; SceneManager.LoadScene(mainMenuScene); });
        if (quitBtn) quitBtn.onClick.AddListener(QuitGame);

        HookHover(resumeBtn); HookHover(optionsBtn); HookHover(mainMenuBtn); HookHover(quitBtn);

        if (pausePanel) pausePanel.anchoredPosition = new Vector2(panelOffscreenX, pausePanel.anchoredPosition.y);
        if (hudPanel) hudPanel.anchoredPosition = new Vector2(panelOffscreenX, hudPanel.anchoredPosition.y);
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPickupCountChanged += UpdatePickups;
            GameManager.Instance.OnTimerChanged += UpdateTimer;
            UpdatePickups(GameManager.Instance.PickupsCollected, GameManager.Instance.PickupsRequired);
            UpdateTimer(GameManager.Instance.TimeRemaining);
        }
        ShowHUD(true);
    }

    void HookHover(Button b)
    {
        if (!b) return;
        var trig = b.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => AudioManager.Instance?.PlaySFX2D(SFXId.UIHover));
        trig.triggers.Add(entry);
    }

    public void TogglePause()
    {
        TogglePause(!isPaused);
    }

    public void TogglePause(bool paused)
    {
        isPaused = paused;
        GameManager.Instance?.Pause(paused);
        if (pauseTween != null) StopCoroutine(pauseTween);
        if (pausePanel) pauseTween = StartCoroutine(SlidePanel(pausePanel, paused ? panelOnscreenX : panelOffscreenX));
        Cursor.visible = paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public void ShowHUD(bool show)
    {
        if (hudTween != null) StopCoroutine(hudTween);
        if (hudPanel) hudTween = StartCoroutine(SlidePanel(hudPanel, show ? panelOnscreenX : panelOffscreenX));
    }

    IEnumerator SlidePanel(RectTransform panel, float targetX)
    {
        float startX = panel.anchoredPosition.x;
        float t = 0f;
        while (t < slideTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / slideTime);
            panel.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, k), panel.anchoredPosition.y);
            yield return null;
        }
        panel.anchoredPosition = new Vector2(targetX, panel.anchoredPosition.y);
    }

    void UpdatePickups(int collected, int required)
    {
        if (pickupCountText) pickupCountText.text = $"{collected}/{required}";
    }

    void UpdateTimer(float remaining)
    {
        if (!timerText) return;
        int m = Mathf.FloorToInt(remaining / 60f);
        int s = Mathf.FloorToInt(remaining % 60f);
        timerText.text = $"{m:00}:{s:00}";
    }

    void QuitGame()
    {
        AudioManager.Instance?.PlaySFX2D(SFXId.UICancel);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPickupCountChanged -= UpdatePickups;
            GameManager.Instance.OnTimerChanged -= UpdateTimer;
        }
    }
}
