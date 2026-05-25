using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Pause Panel (dissolve)")]
    [Tooltip("CanvasGroup raíz del menú de pausa. Se anima alpha + interactable.")]
    [SerializeField] CanvasGroup pauseGroup;
    [Tooltip("Image de fondo del pause que usa el shader UI/Dissolve. Si está vacío se autocrea como hijo de pauseGroup.")]
    [SerializeField] Image pauseDissolveImage;
    [SerializeField] Button resumeBtn;
    [SerializeField] Button optionsBtn;
    [SerializeField] Button mainMenuBtn;
    [SerializeField] Button quitBtn;
    [Tooltip("Botón 'Close' del canvas — cierra la pausa (mismo efecto que ESC).")]
    [SerializeField] Button closeBtn;
    [SerializeField] string mainMenuScene = "MainMenu";

    [Header("Dissolve material")]
    [SerializeField] Shader dissolveShader;
    [SerializeField] Texture2D noiseTexture;
    [SerializeField] int proceduralNoiseSize = 256;
    [SerializeField] Color dissolveTint = new Color(0f, 0f, 0f, 0.85f);
    [SerializeField] Color edgeColor = new Color(1f, 0.45f, 0.1f, 1f);
    [Range(0f, 0.5f)][SerializeField] float edgeWidth = 0.08f;
    [Range(0f, 4f)][SerializeField] float edgeIntensity = 1.8f;
    [Tooltip("Duración del disolverse al abrir/cerrar la pausa (segundos).")]
    [SerializeField] float dissolveTime = 0.45f;

    [Header("HUD Panel (slides from left)")]
    [SerializeField] RectTransform hudPanel;
    [SerializeField] TMP_Text pickupCountText;
    [SerializeField] TMP_Text timerText;
    [SerializeField] RawImage minimapImage;

    [Header("HUD Slide Tween")]
    [SerializeField] float slideTime = 0.3f;
    [SerializeField] float panelOffscreenX = -600f;
    [SerializeField] float panelOnscreenX = 0f;

    bool isPaused;
    Coroutine pauseTween;
    Coroutine hudTween;

    Material dissolveMat;
    static readonly int IdCutoff        = Shader.PropertyToID("_Cutoff");
    static readonly int IdColor         = Shader.PropertyToID("_Color");
    static readonly int IdNoiseTex      = Shader.PropertyToID("_NoiseTex");
    static readonly int IdEdgeColor     = Shader.PropertyToID("_EdgeColor");
    static readonly int IdEdgeWidth     = Shader.PropertyToID("_EdgeWidth");
    static readonly int IdEdgeIntensity = Shader.PropertyToID("_EdgeIntensity");

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (resumeBtn) resumeBtn.onClick.AddListener(() => { TogglePause(false); });
        if (closeBtn)  closeBtn.onClick.AddListener(()  => { TogglePause(false); });
        if (optionsBtn) optionsBtn.onClick.AddListener(() => { AudioManager.Instance?.PlaySFX2D(SFXId.UISelect); });
        if (mainMenuBtn) mainMenuBtn.onClick.AddListener(() => { Time.timeScale = 1f; SceneManager.LoadScene(mainMenuScene); });
        if (quitBtn) quitBtn.onClick.AddListener(QuitGame);

        HookHover(resumeBtn); HookHover(optionsBtn); HookHover(mainMenuBtn); HookHover(quitBtn); HookHover(closeBtn);

        SetupPauseDissolve();
        SetPauseGroupHidden();

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
        // En dificultad Fácil NO hay límite de tiempo (matchTimeSeconds = 0 → UnlimitedTime),
        // por tanto el marcador del timer no aporta nada → lo ocultamos.
        if (timerText != null)
        {
            bool showTimer = GameManager.Instance == null || !GameManager.Instance.UnlimitedTime;
            timerText.gameObject.SetActive(showTimer);
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

    // ── Dissolve setup ────────────────────────────────────────────────────────
    void SetupPauseDissolve()
    {
        if (pauseGroup == null) return;

        if (pauseDissolveImage == null)
        {
            var go = new GameObject("PauseDissolveBG");
            go.transform.SetParent(pauseGroup.transform, false);
            go.transform.SetAsFirstSibling();
            pauseDissolveImage = go.AddComponent<Image>();
            var rt = pauseDissolveImage.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            pauseDissolveImage.raycastTarget = true; // bloquea clics al gameplay detrás
        }

        if (dissolveShader == null) dissolveShader = Shader.Find("UI/Dissolve");
        if (dissolveShader == null || !dissolveShader.isSupported)
        {
            Debug.LogWarning("[UIManager] Shader 'UI/Dissolve' no encontrado o no soportado. El pause usará fade alpha plano.", this);
            pauseDissolveImage.material = null;
            pauseDissolveImage.color = dissolveTint;
            return;
        }

        if (noiseTexture == null) noiseTexture = GenerateNoiseTexture(proceduralNoiseSize);

        dissolveMat = new Material(dissolveShader) { hideFlags = HideFlags.HideAndDontSave };
        dissolveMat.SetTexture(IdNoiseTex, noiseTexture);
        dissolveMat.SetColor(IdColor, dissolveTint);
        dissolveMat.SetColor(IdEdgeColor, edgeColor);
        dissolveMat.SetFloat(IdEdgeWidth, edgeWidth);
        dissolveMat.SetFloat(IdEdgeIntensity, edgeIntensity);
        dissolveMat.SetFloat(IdCutoff, 0f);

        pauseDissolveImage.material = dissolveMat;
        pauseDissolveImage.color = Color.white;
    }

    static Texture2D GenerateNoiseTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.R8, false, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
            name = "UIManager_PauseNoise"
        };
        var pixels = new Color32[size * size];
        var rng = new System.Random(20260524);
        for (int i = 0; i < pixels.Length; i++)
        {
            byte v = (byte)rng.Next(0, 256);
            pixels[i] = new Color32(v, v, v, 255);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    void SetPauseProgress(float t01)
    {
        // 0 = oculto / 1 = visible
        t01 = Mathf.Clamp01(t01);
        if (pauseGroup)
        {
            pauseGroup.alpha = t01;
            pauseGroup.interactable = t01 > 0.99f;
            pauseGroup.blocksRaycasts = t01 > 0.01f;
        }
        if (dissolveMat != null && pauseDissolveImage != null)
        {
            dissolveMat.SetFloat(IdCutoff, t01);
        }
        else if (pauseDissolveImage != null)
        {
            var c = dissolveTint; c.a = dissolveTint.a * t01; pauseDissolveImage.color = c;
        }
    }

    void SetPauseGroupHidden()
    {
        if (pauseGroup)
        {
            pauseGroup.alpha = 0f;
            pauseGroup.interactable = false;
            pauseGroup.blocksRaycasts = false;
            pauseGroup.gameObject.SetActive(true); // mantenemos activo para que la coroutine pueda animar
        }
        if (dissolveMat != null) dissolveMat.SetFloat(IdCutoff, 0f);
    }

    // ── API pública ───────────────────────────────────────────────────────────
    public void TogglePause()
    {
        TogglePause(!isPaused);
    }

    public void TogglePause(bool paused)
    {
        isPaused = paused;
        GameManager.Instance?.Pause(paused);

        if (pauseTween != null) StopCoroutine(pauseTween);
        pauseTween = StartCoroutine(DissolvePause(paused));

        Cursor.visible = paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
    }

    IEnumerator DissolvePause(bool show)
    {
        float from = pauseGroup ? pauseGroup.alpha : (show ? 0f : 1f);
        float to = show ? 1f : 0f;
        if (dissolveTime <= 0f) { SetPauseProgress(to); yield break; }

        float t = 0f;
        while (t < dissolveTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / dissolveTime);
            SetPauseProgress(Mathf.Lerp(from, to, k));
            yield return null;
        }
        SetPauseProgress(to);
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
