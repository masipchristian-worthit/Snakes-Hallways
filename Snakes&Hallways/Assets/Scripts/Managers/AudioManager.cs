using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum SFXId
{
    None = 0,
    PlayerStepStone,
    PlayerBreath,
    PlayerBreathHard,
    PlayerFall,
    PlayerCrouch,
    EyeViscous,
    HandDraw,
    HandStore,
    EyeZoom,
    EyeProximity,
    MinotaurStep,
    MinotaurNeigh,
    MinotaurCharge,
    MinotaurDetect,
    MinotaurCause,
    MinotaurIdleBreath,
    Torch,
    DoorVariant,
    Pickup,
    PortalActive,
    PortalIdle,
    PortalCross,
    UIHover,
    UISelect,
    UICancel,
    UITextVoice,
    UIPause,
    UIUnpause
}

public enum MusicId
{
    None = 0,
    AmbienceCalm,
    AmbienceHorror,
    Chase,
    Silent,
    GameOver,
    Win
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

    [Header("Mixer")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioMixerGroup musicGroup;

    [Header("Library")]
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

    public void SetSfxScale(float s) { sfxScale = Mathf.Clamp01(s); }
    public void SetMusicScale(float s)
    {
        musicScale = Mathf.Clamp01(s);
        if (musicSourceA != null) musicSourceA.volume = (musicSourceA.isPlaying ? currentMusicBase * musicScale : 0f);
        if (musicSourceB != null) musicSourceB.volume = (musicSourceB.isPlaying ? currentMusicBase * musicScale : 0f);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

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
    }

    public void PlaySFX(SFXId id, Vector3 position, float volumeMultiplier = 1f)
    {
        if (id == SFXId.None || !sfxMap.TryGetValue(id, out var e) || e.variants == null || e.variants.Length == 0) return;
        var clip = e.variants[Random.Range(0, e.variants.Length)];
        var src = sfxPool[sfxPoolIndex];
        sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Length;
        src.transform.position = position;
        src.spatialBlend = 1f;
        src.clip = clip;
        src.volume = e.volume * volumeMultiplier * sfxScale;
        src.pitch = Random.Range(e.minPitch, e.maxPitch);
        src.Play();
    }

    public void PlaySFX2D(SFXId id, float volumeMultiplier = 1f)
    {
        if (id == SFXId.None || !sfxMap.TryGetValue(id, out var e) || e.variants == null || e.variants.Length == 0) return;
        var clip = e.variants[Random.Range(0, e.variants.Length)];
        var src = sfxPool[sfxPoolIndex];
        sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Length;
        src.spatialBlend = 0f;
        src.clip = clip;
        src.volume = e.volume * volumeMultiplier * sfxScale;
        src.pitch = Random.Range(e.minPitch, e.maxPitch);
        src.Play();
    }

    public void PlayMusic(MusicId id, float fadeTime = 1.5f)
    {
        if (!musicMap.TryGetValue(id, out var e)) return;
        var fadeIn = useA ? musicSourceB : musicSourceA;
        var fadeOut = useA ? musicSourceA : musicSourceB;
        fadeIn.clip = e.clip;
        fadeIn.volume = 0f;
        fadeIn.Play();
        currentMusicBase = e.volume;
        StartCoroutine(FadeMusic(fadeIn, fadeOut, e.volume * musicScale, fadeTime));
        useA = !useA;
    }

    System.Collections.IEnumerator FadeMusic(AudioSource fadeIn, AudioSource fadeOut, float targetVol, float t)
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
}
