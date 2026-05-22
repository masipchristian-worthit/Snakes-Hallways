using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VictoryManager : MonoBehaviour
{
    public static VictoryManager Instance { get; private set; }

    [Header("Pantalla de victoria")]
    [SerializeField] CanvasGroup victoryCanvasGroup;
    [SerializeField] TMP_Text titleLabel;
    [SerializeField] TMP_Text subtitleLabel;
    [SerializeField] Button mainMenuButton;

    [Header("Textos")]
    [SerializeField] string titleText = "ESCAPE";
    [SerializeField] string subtitleText = "Has cruzado el portal.";

    [Header("Transición")]
    [SerializeField] float fadeInTime = 1.2f;
    [SerializeField] string mainMenuSceneName = "MainMenu";

    bool isWon;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (victoryCanvasGroup)
        {
            victoryCanvasGroup.alpha = 0f;
            victoryCanvasGroup.blocksRaycasts = false;
            victoryCanvasGroup.interactable = false;
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
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    void HandleStateChanged(GameState s)
    {
        if (s == GameState.Win) TriggerVictory();
    }

    public void TriggerVictory()
    {
        if (isWon) return;
        isWon = true;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        AudioManager.Instance?.PlayMusic(MusicId.Win);

        if (titleLabel)    titleLabel.text = titleText;
        if (subtitleLabel) subtitleLabel.text = subtitleText;
        if (mainMenuButton) mainMenuButton.gameObject.SetActive(true);

        if (victoryCanvasGroup)
        {
            victoryCanvasGroup.blocksRaycasts = true;
            victoryCanvasGroup.interactable = true;
            StartCoroutine(FadeInGroup(victoryCanvasGroup, fadeInTime));
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
