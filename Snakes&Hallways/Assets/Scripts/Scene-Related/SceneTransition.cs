using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [SerializeField] Image fadeImage;
    [SerializeField] Color fadeColor = Color.black;

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
        var c = fadeColor; c.a = 0f; fadeImage.color = c;
        fadeImage.raycastTarget = false;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        spawnPoints.Clear();
        // SpawnPoints will re-register themselves via OnEnable.
        // TryApplyPendingSpawn is called there.
        // Fade-in after load:
        StartCoroutine(FadeFromBlack(1f));
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

        // Buscar spawn y teleportar.
        SpawnPoint target = null;
        foreach (var sp in spawnPoints)
            if (sp.SpawnId == spawnId) { target = sp; break; }

        if (target != null) PlacePlayer(target);

        yield return StartCoroutine(FadeFromBlack(fadeTime));
    }

    // ── Fade helpers ──────────────────────────────────────────────────────────
    public IEnumerator FadeToBlack(float fadeTime = 1f)
    {
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            var c = fadeColor; c.a = Mathf.Clamp01(elapsed / fadeTime); fadeImage.color = c;
            yield return null;
        }
    }

    public IEnumerator FadeFromBlack(float fadeTime = 1f)
    {
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            var c = fadeColor; c.a = 1f - Mathf.Clamp01(elapsed / fadeTime); fadeImage.color = c;
            yield return null;
        }
    }
}
