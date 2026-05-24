using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel de UI que controla todos los settings del SettingsManager.
/// Conecta Sliders, Toggles y Dropdowns con los métodos públicos del SettingsManager.
/// </summary>
public class SettingsUIPanel : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] Slider masterVolumeSlider;
    [SerializeField] Slider sfxVolumeSlider;
    [SerializeField] Slider musicVolumeSlider;
    [SerializeField] Slider uiVolumeSlider;

    [Header("Gameplay")]
    [SerializeField] Slider mouseSensitivitySlider;

    [Header("Graphics")]
    [SerializeField] Toggle fullscreenToggle;
    [SerializeField] Toggle vsyncToggle;
    [SerializeField] TMP_Dropdown resolutionDropdown;

    static readonly Vector2Int[] kResolutionOptions = new[]
    {
        new Vector2Int(1280, 720),   // 720p
        new Vector2Int(1600, 900),   // 900p
        new Vector2Int(1920, 1080),  // 1080p (Full HD)
        new Vector2Int(2560, 1440),  // 1440p (2K)
    };

    [Header("Buttons")]
    [SerializeField] Button resetButton;
    [SerializeField] Button closeButton;

    void Start()
    {
        if (SettingsManager.Instance == null) return;

        InitializeAudioSliders();
        InitializeGameplaySliders();
        InitializeGraphicsSettings();
        SetupButtons();

        SettingsManager.Instance.OnSettingsChanged += RefreshUI;
    }

    void InitializeAudioSliders()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = SettingsManager.Instance.Master;
            masterVolumeSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetMaster(v));
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = SettingsManager.Instance.Sfx;
            sfxVolumeSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetSfx(v));
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = SettingsManager.Instance.Music;
            musicVolumeSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetMusic(v));
        }

        if (uiVolumeSlider != null)
        {
            uiVolumeSlider.value = SettingsManager.Instance.Ui;
            uiVolumeSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetUi(v));
        }
    }

    void InitializeGameplaySliders()
    {
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = 0.01f;
            mouseSensitivitySlider.maxValue = 2f;
            mouseSensitivitySlider.value = SettingsManager.Instance.Sensitivity;
            mouseSensitivitySlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetSensitivity(v));
        }
    }

    void InitializeGraphicsSettings()
    {
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = SettingsManager.Instance.Fullscreen;
            fullscreenToggle.onValueChanged.AddListener(on => SettingsManager.Instance.SetFullscreen(on));
        }

        if (vsyncToggle != null)
        {
            vsyncToggle.isOn = SettingsManager.Instance.VSync;
            vsyncToggle.onValueChanged.AddListener(on => SettingsManager.Instance.SetVSync(on));
        }

        if (resolutionDropdown != null)
        {
            SetupResolutionDropdown();
        }
    }

    void SetupResolutionDropdown()
    {
        resolutionDropdown.ClearOptions();

        int currentIndex = 0;
        for (int i = 0; i < kResolutionOptions.Length; i++)
        {
            var r = kResolutionOptions[i];
            string label = $"{r.x} x {r.y}";
            if (r.x == 1920) label += "  (Full HD)";
            else if (r.x == 2560) label += "  (2K)";
            resolutionDropdown.options.Add(new TMP_Dropdown.OptionData(label));

            if (r.x == Screen.width && r.y == Screen.height)
                currentIndex = i;
        }

        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.SetValueWithoutNotify(currentIndex);
        resolutionDropdown.onValueChanged.AddListener(index =>
        {
            var r = kResolutionOptions[index];
            SettingsManager.Instance.SetResolution(r.x, r.y);
        });
    }

    void SetupButtons()
    {
        if (resetButton != null)
            resetButton.onClick.AddListener(() => SettingsManager.Instance.ResetAllToDefaults());

        if (closeButton != null)
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
    }

    void RefreshUI()
    {
        // Se llama cuando cambian los settings en otra parte del código
        // Actualizar valores sin disparar eventos
        if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(SettingsManager.Instance.Master);
        if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(SettingsManager.Instance.Sfx);
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(SettingsManager.Instance.Music);
        if (uiVolumeSlider != null) uiVolumeSlider.SetValueWithoutNotify(SettingsManager.Instance.Ui);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.SetValueWithoutNotify(SettingsManager.Instance.Sensitivity);
    }

    void OnDestroy()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnSettingsChanged -= RefreshUI;
    }
}
