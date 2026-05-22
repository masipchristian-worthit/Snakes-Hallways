using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

/// <summary>
/// Persistencia + aplicación de:
///  - Volúmenes: Master, SFX, Music, UI (con AudioMixer si se asigna, fallback a AudioListener+AudioManager).
///  - Sensibilidad de ratón (lee PlayerController por reflexión-segura: nuevo campo público MouseSensitivity).
///  - Rebinding del Input System (overrides JSON en PlayerPrefs).
///  - Dificultad (delegada a DifficultyManager).
/// Se guarda automáticamente en PlayerPrefs y se restaura en Awake.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("AudioMixer (opcional)")]
    [Tooltip("Si lo asignas, los volúmenes escriben los parámetros expuestos por nombre (en dB).")]
    [SerializeField] AudioMixer mixer;
    [SerializeField] string masterParam = "MasterVolume";
    [SerializeField] string sfxParam = "SFXVolume";
    [SerializeField] string musicParam = "MusicVolume";
    [SerializeField] string uiParam = "UIVolume";

    [Header("Input")]
    [Tooltip("InputActionAsset del juego — usado para guardar/cargar overrides de bindings.")]
    [SerializeField] InputActionAsset inputActions;
    public InputActionAsset InputActions => inputActions;

    [Header("Defaults")]
    [Range(0f, 1f)] [SerializeField] float defaultMaster = 1f;
    [Range(0f, 1f)] [SerializeField] float defaultSfx = 1f;
    [Range(0f, 1f)] [SerializeField] float defaultMusic = 0.8f;
    [Range(0f, 1f)] [SerializeField] float defaultUi = 1f;
    [Range(0.01f, 2f)] [SerializeField] float defaultSensitivity = 0.1f;

    const string K_Master = "SH_VolMaster";
    const string K_Sfx    = "SH_VolSfx";
    const string K_Music  = "SH_VolMusic";
    const string K_Ui     = "SH_VolUi";
    const string K_Sens   = "SH_Sensitivity";
    const string K_Binds  = "SH_BindingOverrides";
    const string K_Full   = "SH_Fullscreen";
    const string K_VSync  = "SH_VSync";
    const string K_ResW   = "SH_ResW";
    const string K_ResH   = "SH_ResH";

    public float Master   { get; private set; }
    public float Sfx      { get; private set; }
    public float Music    { get; private set; }
    public float Ui       { get; private set; }
    public float Sensitivity { get; private set; }

    public event Action OnSettingsChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAll();
    }

    void Start() => ApplyAll();

    void LoadAll()
    {
        Master = PlayerPrefs.GetFloat(K_Master, defaultMaster);
        Sfx    = PlayerPrefs.GetFloat(K_Sfx, defaultSfx);
        Music  = PlayerPrefs.GetFloat(K_Music, defaultMusic);
        Ui     = PlayerPrefs.GetFloat(K_Ui, defaultUi);
        Sensitivity = PlayerPrefs.GetFloat(K_Sens, defaultSensitivity);

        if (inputActions != null && PlayerPrefs.HasKey(K_Binds))
        {
            string json = PlayerPrefs.GetString(K_Binds);
            if (!string.IsNullOrEmpty(json))
                inputActions.LoadBindingOverridesFromJson(json);
        }
    }

    public void ApplyAll()
    {
        ApplyVolume(Master, Sfx, Music, Ui);
        ApplySensitivity(Sensitivity);
        OnSettingsChanged?.Invoke();
    }

    void ApplyVolume(float master, float sfx, float music, float ui)
    {
        // Mixer si existe: convertimos 0..1 a dB (con curva log para que sea suave).
        if (mixer != null)
        {
            SetMixer(masterParam, master);
            SetMixer(sfxParam, sfx);
            SetMixer(musicParam, music);
            SetMixer(uiParam, ui);
        }

        // Master global por AudioListener — funciona siempre.
        AudioListener.volume = master;

        // Fallback: escalado interno del AudioManager para diferenciar SFX/Música.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSfxScale(sfx);
            AudioManager.Instance.SetMusicScale(music);
        }
    }

    void SetMixer(string param, float v01)
    {
        if (string.IsNullOrEmpty(param)) return;
        float dB = v01 <= 0.0001f ? -80f : Mathf.Log10(v01) * 20f;
        mixer.SetFloat(param, dB);
    }

    void ApplySensitivity(float s)
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (!p) return;
        var pc = p.GetComponent<PlayerController>();
        if (pc != null) pc.MouseSensitivity = s;
    }

    // ───── API pública ─────
    public void SetMaster(float v) { Master = Mathf.Clamp01(v); PlayerPrefs.SetFloat(K_Master, Master); PlayerPrefs.Save(); ApplyAll(); }
    public void SetSfx(float v)    { Sfx = Mathf.Clamp01(v);    PlayerPrefs.SetFloat(K_Sfx, Sfx);       PlayerPrefs.Save(); ApplyAll(); }
    public void SetMusic(float v)  { Music = Mathf.Clamp01(v);  PlayerPrefs.SetFloat(K_Music, Music);   PlayerPrefs.Save(); ApplyAll(); }
    public void SetUi(float v)     { Ui = Mathf.Clamp01(v);     PlayerPrefs.SetFloat(K_Ui, Ui);         PlayerPrefs.Save(); ApplyAll(); }
    public void SetSensitivity(float v) { Sensitivity = Mathf.Clamp(v, 0.01f, 2f); PlayerPrefs.SetFloat(K_Sens, Sensitivity); PlayerPrefs.Save(); ApplyAll(); }

    public void PersistBindingOverrides()
    {
        if (inputActions == null) return;
        string json = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(K_Binds, json);
        PlayerPrefs.Save();
    }

    public void ResetBindings()
    {
        if (inputActions == null) return;
        foreach (var map in inputActions.actionMaps) map.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(K_Binds);
        PlayerPrefs.Save();
        OnSettingsChanged?.Invoke();
    }

    // ───── Gráficos ─────
    public bool Fullscreen => PlayerPrefs.GetInt(K_Full, Screen.fullScreen ? 1 : 0) == 1;
    public bool VSync => PlayerPrefs.GetInt(K_VSync, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;

    public void SetFullscreen(bool on)
    {
        Screen.fullScreen = on;
        PlayerPrefs.SetInt(K_Full, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetVSync(bool on)
    {
        QualitySettings.vSyncCount = on ? 1 : 0;
        PlayerPrefs.SetInt(K_VSync, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetResolution(int w, int h)
    {
        Screen.SetResolution(w, h, Screen.fullScreen);
        PlayerPrefs.SetInt(K_ResW, w);
        PlayerPrefs.SetInt(K_ResH, h);
        PlayerPrefs.Save();
    }

    public void ResetAllToDefaults()
    {
        Master = defaultMaster;
        Sfx = defaultSfx;
        Music = defaultMusic;
        Ui = defaultUi;
        Sensitivity = defaultSensitivity;
        PlayerPrefs.SetFloat(K_Master, Master);
        PlayerPrefs.SetFloat(K_Sfx, Sfx);
        PlayerPrefs.SetFloat(K_Music, Music);
        PlayerPrefs.SetFloat(K_Ui, Ui);
        PlayerPrefs.SetFloat(K_Sens, Sensitivity);
        PlayerPrefs.Save();
        ResetBindings();
        ApplyAll();
    }
}
