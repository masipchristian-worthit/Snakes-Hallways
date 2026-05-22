using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DefeatManager : MonoBehaviour
{
    public static DefeatManager Instance { get; private set; }

    [Header("Refs de escena")]
    [SerializeField] PlayerHealth playerHealth;

    [Header("Pantalla de derrota")]
    [SerializeField] CanvasGroup defeatCanvasGroup;
    [SerializeField] TMP_Text defeatTitleLabel;
    [SerializeField] TMP_Text defeatSubtitleLabel;
    [SerializeField] Button mainMenuButton;

    [Header("Textos")]
    [SerializeField] string titleText = "DEFEAT";
    [SerializeField] string subtitleDeathText = "Has caído.";
    [SerializeField] string subtitleTimerText = "Se ha acabado el tiempo.";

    [Header("Transición")]
    [SerializeField] float fadeInTime = 1.2f;
    [SerializeField] string mainMenuSceneName = "MainMenu";

    bool isDefeated;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (defeatCanvasGroup)
        {
            defeatCanvasGroup.alpha = 0f;
            defeatCanvasGroup.blocksRaycasts = false;
            defeatCanvasGroup.interactable = false;
        }

        if (mainMenuButton)
        {
            mainMenuButton.gameObject.SetActive(false);
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        }
    }

    void Start()
    {
        if (!playerHealth)
        {
            var p = GameObject.FindWithTag("Player");
            if (p) playerHealth = p.GetComponentInChildren<PlayerHealth>();
        }
        if (playerHealth != null) playerHealth.OnDied += HandlePlayerDeath;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDeath;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    void HandlePlayerDeath() => TriggerDefeat(subtitleDeathText);

    void HandleStateChanged(GameState s)
    {
        // GameManager dispara GameOver cuando el temporizador llega a 0.
        if (s == GameState.GameOver) TriggerDefeat(subtitleTimerText);
    }

    public void TriggerDefeat(string reason = "")
    {
        if (isDefeated) return;
        isDefeated = true;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        AudioManager.Instance?.PlayMusic(MusicId.GameOver);

        if (defeatTitleLabel)    defeatTitleLabel.text = titleText;
        if (defeatSubtitleLabel) defeatSubtitleLabel.text = reason;
        if (mainMenuButton)      mainMenuButton.gameObject.SetActive(true);

        if (defeatCanvasGroup)
        {
            defeatCanvasGroup.blocksRaycasts = true;
            defeatCanvasGroup.interactable = true;
            StartCoroutine(FadeInGroup(defeatCanvasGroup, fadeInTime));
        }
    }

    IEnumerator FadeInGroup(CanvasGroup g, float t)
    {
        float e = 0f;
        while (e < t)
        {
            e += Time.unscaledDeltaTime;
            g.alpha = Mathf.Clamp01(e / t);
            yield return null;
        }
        g.alpha = 1f;
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
