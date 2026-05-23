using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [Header("Overlay")]
    [SerializeField] Image fadeImage;
    [SerializeField] Color fadeColor = Color.black;

    [Header("Dissolve")]
    [Tooltip("Si está activo, las transiciones usan un shader de disolución por ruido en vez de un fade alpha plano.")]
    [SerializeField] bool useDissolve = true;
    [Tooltip("Shader UI/Dissolve. Si está vacío se busca por nombre 'UI/Dissolve'.")]
    [SerializeField] Shader dissolveShader;
    [Tooltip("Textura de ruido para la disolución. Si está vacía se genera una procedural 256x256.")]
    [SerializeField] Texture2D noiseTexture;
    [SerializeField] int proceduralNoiseSize = 256;
    [SerializeField] Color edgeColor = new Color(1f, 0.45f, 0.1f, 1f);
    [Range(0f, 0.5f)][SerializeField] float edgeWidth = 0.08f;
    [Range(0f, 4f)][SerializeField] float edgeIntensity = 1.8f;

    [Header("Defaults")]
    [Tooltip("Duración de la disolución de entrada al cargar una escena (segundos).")]
    [SerializeField] float defaultDissolveInTime = 1.4f;

    // ── Spawn point registry ──────────────────────────────────────────────────
    static readonly List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
    static string pendingSpawnId;

    public static void RegisterSpawnPoint(SpawnPoint sp)
    {
        if (!spawnPoints.Contains(sp)) spawnPoints.Add(sp);
        TryApplyPendingSpawn();
    }

    public static void UnregisterSpawnPoint(SpawnPoint sp) => spawnPoints.Remove(sp);

    static void TryApplyPendingSpawn()
    {
        if (string.IsNullOrEmpty(pendingSpawnId)) return;
        foreach (var sp in spawnPoints)
        {
            if (sp.SpawnId == pendingSpawnId)
            {
                PlacePlayer(sp);
                pendingSpawnId = null;
                return;
            }
        }
    }

    static void PlacePlayer(SpawnPoint sp)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player) return;
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.transform.SetPositionAndRotation(sp.transform.position, sp.transform.rotation);
        if (cc) cc.enabled = true;
    }

    // ── Material / runtime state ──────────────────────────────────────────────
    Material dissolveMat;
    static readonly int IdCutoff       = Shader.PropertyToID("_Cutoff");
    static readonly int IdColor        = Shader.PropertyToID("_Color");
    static readonly int IdNoiseTex     = Shader.PropertyToID("_NoiseTex");
    static readonly int IdEdgeColor    = Shader.PropertyToID("_EdgeColor");
    static readonly int IdEdgeWidth    = Shader.PropertyToID("_EdgeWidth");
    static readonly int IdEdgeIntensity= Shader.PropertyToID("_EdgeIntensity");

    bool initialDissolvePlayed;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (fadeImage == null)
        {
            var go = new GameObject("FadeImage");
            go.transform.SetParent(transform, false);
            fadeImage = go.AddComponent<Image>();
            var rt = fadeImage.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        fadeImage.raycastTarget = false;

        SetupDissolveMaterial();

        // Start opaque so the player never sees the underlying scene before the dissolve plays.
        SetOverlayOpaque();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // Initial scene: sceneLoaded won't fire because we subscribed in Awake AFTER it had already loaded.
        // Trigger the entry dissolve here.
        if (!initialDissolvePlayed)
        {
            initialDissolvePlayed = true;
            StartCoroutine(FadeFromBlack(defaultDissolveInTime));
        }
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        spawnPoints.Clear();
        // SpawnPoints will re-register themselves via OnEnable.
        SetOverlayOpaque();                          // ensure no 1-frame peek of the new scene
        initialDissolvePlayed = true;                // prevent Start() doing it again on fresh instances
        StartCoroutine(FadeFromBlack(defaultDissolveInTime));
    }

    // ── Material setup ────────────────────────────────────────────────────────
    void SetupDissolveMaterial()
    {
        if (!useDissolve) { fadeImage.color = WithAlpha(fadeColor, 1f); return; }

        if (dissolveShader == null) dissolveShader = Shader.Find("UI/Dissolve");
        if (dissolveShader == null)
        {
            Debug.LogWarning("[SceneTransition] Shader 'UI/Dissolve' no encontrado. Cayendo a fade alpha plano.", this);
            useDissolve = false;
            return;
        }

        if (noiseTexture == null) noiseTexture = GenerateNoiseTexture(proceduralNoiseSize);

        dissolveMat = new Material(dissolveShader) { hideFlags = HideFlags.HideAndDontSave };
        dissolveMat.SetTexture(IdNoiseTex, noiseTexture);
        dissolveMat.SetColor(IdColor, fadeColor);
        dissolveMat.SetColor(IdEdgeColor, edgeColor);
        dissolveMat.SetFloat(IdEdgeWidth, edgeWidth);
        dissolveMat.SetFloat(IdEdgeIntensity, edgeIntensity);
        dissolveMat.SetFloat(IdCutoff, 0f);

        fadeImage.material = dissolveMat;
        fadeImage.color = Color.white; // material handles tint via _Color
    }

    static Texture2D GenerateNoiseTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.R8, false, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
            name = "SceneTransition_Noise"
        };
        var pixels = new Color32[size * size];
        var rng = new System.Random(20260523);
        for (int i = 0; i < pixels.Length; i++)
        {
            byte v = (byte)rng.Next(0, 256);
            pixels[i] = new Color32(v, v, v, 255);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    void SetOverlayOpaque()
    {
        if (useDissolve && dissolveMat != null)
        {
            dissolveMat.SetFloat(IdCutoff, 0f);
            fadeImage.color = Color.white;
        }
        else
        {
            fadeImage.color = WithAlpha(fadeColor, 1f);
        }
    }

    static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    void SetProgress(float t01)
    {
        // t01 = 0 → fully opaque (black covering screen).
        // t01 = 1 → fully transparent (scene visible).
        t01 = Mathf.Clamp01(t01);
        if (useDissolve && dissolveMat != null)
            dissolveMat.SetFloat(IdCutoff, t01);
        else
            fadeImage.color = WithAlpha(fadeColor, 1f - t01);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Fade a negro y carga la escena.</summary>
    public void FadeAndLoad(string sceneName, float fadeTime = 1.5f)
    {
        StartCoroutine(FadeAndLoadCo(sceneName, null, fadeTime));
    }

    /// <summary>Fade a negro, carga la escena y coloca al player en el SpawnPoint con el id dado.</summary>
    public void FadeAndLoadWithSpawn(string sceneName, string spawnId, float fadeTime = 1.5f)
    {
        pendingSpawnId = spawnId;
        StartCoroutine(FadeAndLoadCo(sceneName, spawnId, fadeTime));
    }

    /// <summary>
    /// Warp dentro de la misma escena: teleporta al jugador al SpawnPoint indicado
    /// con un fundido a negro y posterior fundido a transparente.
    /// </summary>
    public void WarpInScene(string spawnId, float fadeTime = 0.4f)
    {
        StartCoroutine(WarpInSceneCo(spawnId, fadeTime));
    }

    IEnumerator FadeAndLoadCo(string sceneName, string spawnId, float fadeTime)
    {
        yield return StartCoroutine(FadeToBlack(fadeTime));
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
        // OnSceneLoaded handles fade-in + spawn placement.
    }

    IEnumerator WarpInSceneCo(string spawnId, float fadeTime)
    {
        yield return StartCoroutine(FadeToBlack(fadeTime));

        SpawnPoint target = null;
        foreach (var sp in spawnPoints)
            if (sp.SpawnId == spawnId) { target = sp; break; }

        if (target != null) PlacePlayer(target);

        yield return StartCoroutine(FadeFromBlack(fadeTime));
    }

    // ── Fade / dissolve helpers (compatibles con la API previa) ───────────────
    public IEnumerator FadeToBlack(float fadeTime = 1f)
    {
        // progress 1 → 0 (visible → opaque)
        SetProgress(1f);
        if (fadeTime <= 0f) { SetProgress(0f); yield break; }
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            SetProgress(1f - Mathf.Clamp01(elapsed / fadeTime));
            yield return null;
        }
        SetProgress(0f);
    }

    public IEnumerator FadeFromBlack(float fadeTime = 1f)
    {
        // progress 0 → 1 (opaque → visible)
        SetProgress(0f);
        if (fadeTime <= 0f) { SetProgress(1f); yield break; }
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            SetProgress(Mathf.Clamp01(elapsed / fadeTime));
            yield return null;
        }
        SetProgress(1f);
    }
}
