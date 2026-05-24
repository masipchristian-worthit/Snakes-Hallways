using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

// ─────────────────────────────────────────────────────────────────────────────
// IDs canónicos. Los nombres COINCIDEN con los archivos en Assets/Audio/SFX y
// Assets/Audio/Music (sin contar sufijos numéricos: PlayerStepStone/2, MinotaurNeigh1/2).
//
// IMPORTANTE: los valores numéricos se mantienen EXACTAMENTE como en la versión
// original del enum. Unity serializa los enums por su int, por lo que reordenar
// los valores rompería todo lo ya asignado en .prefab / .unity. Los huecos
// corresponden a IDs descartados (PlayerBreathHard, PlayerCrouch, HandDraw,
// HandStore, EyeProximity, MinotaurDetect, MinotaurCause).
//
// Renombrados (mismo int):
//   DoorVariant   → Door            (= 18)
//   PortalActive  → PortalActivate  (= 20)
// ─────────────────────────────────────────────────────────────────────────────
public enum SFXId
{
    None               = 0,
    PlayerStepStone    = 1,    // variantes: PlayerStepStone, PlayerStepStone2
    PlayerBreath       = 2,    // loop con fade in/out al esprintar
    // 3 = PlayerBreathHard (descartado)
    PlayerFall         = 4,
    // 5 = PlayerCrouch (descartado)
    EyeViscous         = 6,    // bajo volumen, AS mano
    // 7 = HandDraw (descartado)
    // 8 = HandStore (descartado)
    EyeZoom            = 9,
    // 10 = EyeProximity (descartado)
    MinotaurStep       = 11,
    MinotaurNeigh      = 12,   // variantes: MinotaurNeigh1, MinotaurNeigh2
    MinotaurCharge     = 13,
    // 14 = MinotaurDetect (descartado)
    // 15 = MinotaurCause (descartado)
    MinotaurIdleBreath = 16,   // loop con fade in/out en Idle/Walk
    Torch              = 17,   // loop sincronizado en todos los Fire
    Door               = 18,   // (antes DoorVariant)
    Pickup             = 19,
    PortalActivate     = 20,   // (antes PortalActive)
    PortalIdle         = 21,   // loop del portal
    PortalCross        = 22,
    UIHover            = 23,
    UISelect           = 24,
    UICancel           = 25,
    UITextVoice        = 26,
    UIPause            = 27,
    UIUnpause          = 28,
}

public enum MusicId
{
    None           = 0,
    AmbienceCalm   = 1,
    AmbienceHorror = 2,
    Chase          = 3,
    // 4 = Silent (descartado)
    GameOver       = 5,
    Win            = 6,
}

[System.Serializable]
public class SFXEntry
{
    public SFXId id;
    public AudioClip[] variants;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.5f, 1.5f)] public float minPitch = 0.95f;
    [Range(0.5f, 1.5f)] public float maxPitch = 1.05f;
}

[System.Serializable]
public class MusicEntry
{
    public MusicId id;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.7f;
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Bootstrap")]
    [Tooltip("Si está activo y arrancas una escena sin AudioManager, se intentará cargar un prefab 'AudioManager' desde Resources/ para que SFX/Music funcionen igualmente.")]
    [SerializeField] bool autoBootstrapFromResources = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BootstrapFromResources()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>("AudioManager");
        if (prefab == null)
        {
            Debug.LogWarning("[AudioManager] No se encontró 'Resources/AudioManager.prefab'. " +
                             "Crea el prefab (Assets/Resources/AudioManager.prefab) o coloca el GameObject AudioManager " +
                             "en la primera escena que cargues, o no habrá SFX/Música en escenas que arranques directamente.");
            return;
        }
        var go = Instantiate(prefab);
        go.name = "AudioManager(Auto)";
    }

    [Header("Mixer")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioMixerGroup musicGroup;

    [Header("Auto-load (Editor)")]
    [Tooltip("Carpeta (relativa a Assets/) que contiene los SFX. Cada AudioClip se mapea al SFXId con el mismo nombre. Sufijo numérico (PlayerStepStone2, MinotaurNeigh1) = variante del mismo id.")]
    [SerializeField] string sfxFolder = "Audio/SFX";
    [Tooltip("Carpeta (relativa a Assets/) que contiene la música. Cada AudioClip se mapea al MusicId con el mismo nombre.")]
    [SerializeField] string musicFolder = "Audio/Music";
    [Tooltip("Si está activo, re-escanea las carpetas en cada OnValidate del editor para mantener las listas siempre al día.")]
    [SerializeField] bool autoScanInEditor = true;

    [Header("Library (auto-poblada)")]
    [SerializeField] List<SFXEntry> sfxLibrary = new();
    [SerializeField] List<MusicEntry> musicLibrary = new();

    [Header("Pool")]
    [SerializeField] int sfxPoolSize = 16;

    Dictionary<SFXId, SFXEntry> sfxMap;
    Dictionary<MusicId, MusicEntry> musicMap;
    AudioSource[] sfxPool;
    int sfxPoolIndex;
    AudioSource musicSourceA;
    AudioSource musicSourceB;
    bool useA = true;

    // Escalas globales aplicadas por SettingsManager.
    [System.NonSerialized] public float sfxScale = 1f;
    [System.NonSerialized] public float musicScale = 1f;
    float currentMusicBase = 0.7f;
    MusicId currentMusicId = MusicId.None;
    public MusicId CurrentMusicId => currentMusicId;

    // Sincronización de antorchas (Torch). Todas las Fire arrancan referenciadas
    // al mismo punto temporal del clip para no producir comb-filtering.
    double torchStartDsp;
    readonly List<AudioSource> torchSources = new();

    public void SetSfxScale(float s) { sfxScale = Mathf.Clamp01(s); }
    public void SetMusicScale(float s)
    {
        musicScale = Mathf.Clamp01(s);
        if (musicSourceA != null && musicSourceA.isPlaying) musicSourceA.volume = currentMusicBase * musicScale;
        if (musicSourceB != null && musicSourceB.isPlaying) musicSourceB.volume = currentMusicBase * musicScale;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
        // Asegura que las listas reflejen los archivos en disco antes de construir mapas.
        if (autoScanInEditor) RebuildLibrariesFromFolders();
#endif

        sfxMap = new Dictionary<SFXId, SFXEntry>();
        foreach (var e in sfxLibrary) if (e != null && !sfxMap.ContainsKey(e.id)) sfxMap.Add(e.id, e);

        musicMap = new Dictionary<MusicId, MusicEntry>();
        foreach (var e in musicLibrary) if (e != null && !musicMap.ContainsKey(e.id)) musicMap.Add(e.id, e);

        sfxPool = new AudioSource[sfxPoolSize];
        for (int i = 0; i < sfxPoolSize; i++)
        {
            var go = new GameObject($"SFX_{i}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.outputAudioMixerGroup = sfxGroup;
            src.playOnAwake = false;
            sfxPool[i] = src;
        }

        var mA = new GameObject("Music_A"); mA.transform.SetParent(transform);
        musicSourceA = mA.AddComponent<AudioSource>();
        musicSourceA.outputAudioMixerGroup = musicGroup;
        musicSourceA.loop = true;

        var mB = new GameObject("Music_B"); mB.transform.SetParent(transform);
        musicSourceB = mB.AddComponent<AudioSource>();
        musicSourceB.outputAudioMixerGroup = musicGroup;
        musicSourceB.loop = true;

        torchStartDsp = AudioSettings.dspTime + 0.1d;

        // ── Fallback AudioListener ──────────────────────────────────────────
        // Si la escena no tiene ningún AudioListener activo (típico cuando
        // alguien borra el de la Main Camera), añadimos uno aquí para que NO
        // haya silencio total.
        EnsureAudioListenerExists();
        SceneManager.sceneLoaded += (_, __) => EnsureAudioListenerExists();

        // Diagnóstico inicial: alerta si las librerías están vacías.
        if (sfxLibrary == null || sfxLibrary.Count == 0)
            Debug.LogWarning("[AudioManager] Sfx Library vacía. Revisa que los .mp3 estén en Assets/" + sfxFolder + " y pulsa 'Rebuild Libraries From Folders' en el inspector.", this);
        if (musicLibrary == null || musicLibrary.Count == 0)
            Debug.LogWarning("[AudioManager] Music Library vacía. Revisa que los .mp3 estén en Assets/" + musicFolder + " y pulsa 'Rebuild Libraries From Folders' en el inspector.", this);
    }

    AudioListener fallbackListener;
    void EnsureAudioListenerExists()
    {
        var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        bool anyOther = false;
        foreach (var l in listeners) if (l != fallbackListener && l.enabled) { anyOther = true; break; }
        if (anyOther)
        {
            if (fallbackListener != null) fallbackListener.enabled = false;
        }
        else
        {
            if (fallbackListener == null) fallbackListener = gameObject.AddComponent<AudioListener>();
            fallbackListener.enabled = true;
            Debug.LogWarning("[AudioManager] La escena no tiene AudioListener — uso uno fallback en el propio AudioManager. " +
                             "Asegúrate de que la Main Camera de cada escena tenga AudioListener para mejor espacialización.");
        }
    }

    // ── Reproducción puntual ────────────────────────────────────────────────
    public void PlaySFX(SFXId id, Vector3 position, float volumeMultiplier = 1f)
    {
        var clip = PickVariant(id);
        if (clip == null) return;
        var e = sfxMap[id];
        var src = NextSource();
        src.transform.position = position;
        src.spatialBlend = 1f;
        src.loop = false;
        src.clip = clip;
        src.outputAudioMixerGroup = sfxGroup;
        src.volume = e.volume * volumeMultiplier * sfxScale;
        src.pitch = Random.Range(e.minPitch, e.maxPitch);
        src.Play();
    }

    public void PlaySFX2D(SFXId id, float volumeMultiplier = 1f)
    {
        var clip = PickVariant(id);
        if (clip == null) return;
        var e = sfxMap[id];
        var src = NextSource();
        src.spatialBlend = 0f;
        src.loop = false;
        src.clip = clip;
        src.outputAudioMixerGroup = sfxGroup;
        src.volume = e.volume * volumeMultiplier * sfxScale;
        src.pitch = Random.Range(e.minPitch, e.maxPitch);
        src.Play();
    }

    /// <summary>Reproduce una variante específica (por índice). Útil para alternar pasos.</summary>
    public void PlaySFXVariant(SFXId id, int variantIndex, Vector3 position, float volumeMultiplier = 1f, AudioSource source = null)
    {
        if (id == SFXId.None || !sfxMap.TryGetValue(id, out var e) || e.variants == null || e.variants.Length == 0) return;
        var clip = e.variants[Mathf.Abs(variantIndex) % e.variants.Length];
        if (clip == null) return;
        if (source != null)
        {
            source.pitch = Random.Range(e.minPitch, e.maxPitch);
            source.PlayOneShot(clip, e.volume * volumeMultiplier * sfxScale);
        }
        else
        {
            var src = NextSource();
            src.transform.position = position;
            src.spatialBlend = 1f;
            src.loop = false;
            src.clip = clip;
            src.outputAudioMixerGroup = sfxGroup;
            src.volume = e.volume * volumeMultiplier * sfxScale;
            src.pitch = Random.Range(e.minPitch, e.maxPitch);
            src.Play();
        }
    }

    public AudioClip GetClip(SFXId id)
    {
        if (sfxMap != null && sfxMap.TryGetValue(id, out var e) && e.variants != null && e.variants.Length > 0)
            return e.variants[0];
        return null;
    }

    public float GetVolume(SFXId id) => sfxMap != null && sfxMap.TryGetValue(id, out var e) ? e.volume : 1f;

    AudioClip PickVariant(SFXId id)
    {
        if (id == SFXId.None || sfxMap == null || !sfxMap.TryGetValue(id, out var e)) return null;
        if (e.variants == null || e.variants.Length == 0) return null;
        return e.variants[Random.Range(0, e.variants.Length)];
    }

    AudioSource NextSource()
    {
        var src = sfxPool[sfxPoolIndex];
        sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Length;
        return src;
    }

    // ── Loops con fade in/out sobre un AudioSource externo ──────────────────
    /// <summary>
    /// Arranca un loop con fade-in en el <paramref name="source"/> indicado, usando el clip
    /// asociado al SFXId. Si ya estaba sonando ese mismo clip se respeta su volumen actual
    /// y solo se hace fade hasta el objetivo (no reinicia).
    /// </summary>
    public Coroutine StartLoop(SFXId id, AudioSource source, float fadeIn = 0.6f, float volumeMultiplier = 1f, bool spatial = true)
    {
        if (source == null) return null;
        var clip = GetClip(id);
        if (clip == null) return null;
        var entry = sfxMap[id];
        float target = entry.volume * volumeMultiplier * sfxScale;

        bool sameClip = source.clip == clip && source.isPlaying;
        source.outputAudioMixerGroup = sfxGroup;
        source.loop = true;
        source.spatialBlend = spatial ? 1f : 0f;
        if (!sameClip)
        {
            source.clip = clip;
            source.volume = 0f;
            source.Play();
        }
        return StartCoroutine(FadeSource(source, target, fadeIn));
    }

    /// <summary>Para un loop con fade-out. No destruye el AudioSource.</summary>
    public Coroutine StopLoop(AudioSource source, float fadeOut = 0.6f)
    {
        if (source == null || !source.isPlaying) return null;
        return StartCoroutine(FadeAndStop(source, fadeOut));
    }

    IEnumerator FadeSource(AudioSource s, float target, float t)
    {
        if (t <= 0f) { s.volume = target; yield break; }
        float start = s.volume;
        float elapsed = 0f;
        while (elapsed < t && s != null)
        {
            elapsed += Time.unscaledDeltaTime;
            s.volume = Mathf.Lerp(start, target, elapsed / t);
            yield return null;
        }
        if (s != null) s.volume = target;
    }

    IEnumerator FadeAndStop(AudioSource s, float t)
    {
        float start = s.volume;
        float elapsed = 0f;
        while (elapsed < t && s != null && s.isPlaying)
        {
            elapsed += Time.unscaledDeltaTime;
            s.volume = Mathf.Lerp(start, 0f, elapsed / t);
            yield return null;
        }
        if (s != null) { s.Stop(); s.volume = 0f; }
    }

    // ── Música ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Reproduce música con crossfade. Si la pista solicitada ya está sonando NO se reinicia
    /// (esto permite mantener AmbienceHorror sonando al pasar de SCN_Introduction a SCN_Labe).
    /// </summary>
    public void PlayMusic(MusicId id, float fadeTime = 1.5f)
    {
        if (id == currentMusicId) return;
        if (musicMap == null || !musicMap.TryGetValue(id, out var e) || e.clip == null) return;

        var fadeIn  = useA ? musicSourceB : musicSourceA;
        var fadeOut = useA ? musicSourceA : musicSourceB;
        fadeIn.clip = e.clip;
        fadeIn.loop = true;
        fadeIn.volume = 0f;
        fadeIn.Play();
        currentMusicBase = e.volume;
        currentMusicId = id;
        StartCoroutine(FadeMusic(fadeIn, fadeOut, e.volume * musicScale, fadeTime));
        useA = !useA;
    }

    public void StopMusic(float fadeTime = 1f)
    {
        if (musicSourceA != null && musicSourceA.isPlaying) StartCoroutine(FadeAndStop(musicSourceA, fadeTime));
        if (musicSourceB != null && musicSourceB.isPlaying) StartCoroutine(FadeAndStop(musicSourceB, fadeTime));
        currentMusicId = MusicId.None;
    }

    IEnumerator FadeMusic(AudioSource fadeIn, AudioSource fadeOut, float targetVol, float t)
    {
        float startOut = fadeOut.volume;
        float elapsed = 0f;
        while (elapsed < t)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = elapsed / t;
            fadeIn.volume = Mathf.Lerp(0f, targetVol, k);
            fadeOut.volume = Mathf.Lerp(startOut, 0f, k);
            yield return null;
        }
        fadeOut.Stop();
    }

    // ── Antorchas (Torch) sincronizadas ─────────────────────────────────────
    /// <summary>
    /// Asigna el clip de Torch a un AudioSource y lo arranca sincronizado con el resto
    /// (mismo offset dentro del bucle), evitando comb-filtering entre antorchas próximas.
    /// Llamado desde Fire.cs.
    /// </summary>
    public void RegisterTorch(AudioSource src)
    {
        if (src == null) return;
        var clip = GetClip(SFXId.Torch);
        if (clip == null) return;
        var entry = sfxMap[SFXId.Torch];

        src.outputAudioMixerGroup = sfxGroup;
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 1f;
        src.volume = entry.volume * sfxScale;
        src.pitch = 1f;

        // Compensa la fase: si ya hay antorchas sonando, alineamos al mismo punto del clip.
        double elapsed = AudioSettings.dspTime - torchStartDsp;
        if (elapsed < 0d) elapsed = 0d;
        double clipLen = (double)clip.samples / clip.frequency;
        if (clipLen > 0d)
        {
            double offset = elapsed % clipLen;
            src.timeSamples = (int)(offset * clip.frequency);
        }
        src.Play();
        if (!torchSources.Contains(src)) torchSources.Add(src);
    }

    public void UnregisterTorch(AudioSource src)
    {
        if (src == null) return;
        torchSources.Remove(src);
        if (src.isPlaying) src.Stop();
    }

    // ── Auto-load desde carpetas (solo Editor) ──────────────────────────────
#if UNITY_EDITOR
    void OnValidate()
    {
        if (!autoScanInEditor) return;
        // OnValidate puede llamarse durante la deserialización: aplazamos para evitar warnings.
        UnityEditor.EditorApplication.delayCall -= RebuildLibrariesFromFolders;
        UnityEditor.EditorApplication.delayCall += RebuildLibrariesFromFolders;
    }

    [ContextMenu("Rebuild Libraries From Folders")]
    public void RebuildLibrariesFromFolders()
    {
        if (this == null) return;
        sfxLibrary = ScanSfx(sfxFolder, sfxLibrary);
        musicLibrary = ScanMusic(musicFolder, musicLibrary);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    static List<SFXEntry> ScanSfx(string folder, List<SFXEntry> existing)
    {
        var byId = new Dictionary<SFXId, SFXEntry>();
        if (existing != null)
            foreach (var e in existing) if (e != null && !byId.ContainsKey(e.id)) byId[e.id] = new SFXEntry { id = e.id, volume = e.volume, minPitch = e.minPitch, maxPitch = e.maxPitch };

        // Agrupa por id, recolectando variantes (sufijo numérico final).
        var groups = new Dictionary<SFXId, List<AudioClip>>();
        foreach (var clip in LoadClips(folder))
        {
            if (!TryParseSfxId(clip.name, out var id)) continue;
            if (!groups.TryGetValue(id, out var list)) { list = new List<AudioClip>(); groups[id] = list; }
            list.Add(clip);
        }

        var result = new List<SFXEntry>();
        foreach (var kv in groups)
        {
            // Ordena variantes por nombre para que PlayerStepStone < PlayerStepStone2.
            kv.Value.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            if (!byId.TryGetValue(kv.Key, out var entry))
                entry = new SFXEntry { id = kv.Key };
            entry.variants = kv.Value.ToArray();
            result.Add(entry);
        }
        // Orden estable por enum.
        result.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));
        return result;
    }

    static List<MusicEntry> ScanMusic(string folder, List<MusicEntry> existing)
    {
        var byId = new Dictionary<MusicId, MusicEntry>();
        if (existing != null)
            foreach (var e in existing) if (e != null && !byId.ContainsKey(e.id)) byId[e.id] = new MusicEntry { id = e.id, volume = e.volume };

        var result = new List<MusicEntry>();
        foreach (var clip in LoadClips(folder))
        {
            if (!System.Enum.TryParse<MusicId>(clip.name, out var id) || id == MusicId.None) continue;
            if (!byId.TryGetValue(id, out var entry))
                entry = new MusicEntry { id = id, volume = 0.7f };
            entry.clip = clip;
            result.Add(entry);
        }
        result.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));
        return result;
    }

    static IEnumerable<AudioClip> LoadClips(string folderUnderAssets)
    {
        string assetsRel = "Assets/" + folderUnderAssets.TrimStart('/');
        if (!AssetDatabase.IsValidFolder(assetsRel)) yield break;
        var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { assetsRel });
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) yield return clip;
        }
    }

    /// <summary>
    /// "PlayerStepStone" → PlayerStepStone | "PlayerStepStone2" → PlayerStepStone
    /// "MinotaurNeigh1" → MinotaurNeigh.
    /// </summary>
    static bool TryParseSfxId(string clipName, out SFXId id)
    {
        if (System.Enum.TryParse<SFXId>(clipName, out id) && id != SFXId.None) return true;
        // Quita sufijo numérico final
        int cut = clipName.Length;
        while (cut > 0 && char.IsDigit(clipName[cut - 1])) cut--;
        if (cut == clipName.Length) { id = SFXId.None; return false; }
        var trimmed = clipName.Substring(0, cut);
        return System.Enum.TryParse<SFXId>(trimmed, out id) && id != SFXId.None;
    }
#endif
}
