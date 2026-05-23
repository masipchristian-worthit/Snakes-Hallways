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

    [Header("Buttons")]
    [SerializeField] Button resetButton;
    [SerializeField] Button closeButton;

    [Header("Close Behavior")]
    [SerializeField] bool useSceneWarpOnClose = false;
    [SerializeField] SceneWarp sceneWarpOnClose;

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
        Resolution[] resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        int currentIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = $"{resolutions[i].width}x{resolutions[i].height}@{resolutions[i].refreshRateRatio.numerator}Hz";
            resolutionDropdown.options.Add(new TMPro.TMP_Dropdown.OptionData(option));

            if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                currentIndex = i;
        }

        resolutionDropdown.value = currentIndex;
        resolutionDropdown.onValueChanged.AddListener(index =>
        {
            Resolution res = resolutions[index];
            SettingsManager.Instance.SetResolution(res.width, res.height);
        });
    }

    void SetupButtons()
    {
        if (resetButton != null)
            resetButton.onClick.AddListener(() => SettingsManager.Instance.ResetAllToDefaults());

        if (closeButton != null)
        {
            if (useSceneWarpOnClose && sceneWarpOnClose != null)
            {
                // Usa SceneWarp con transición (fade, cargar escena, etc.)
                closeButton.onClick.AddListener(() => sceneWarpOnClose.Warp());
            }
            else
            {
                // Solo desactiva el panel
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
        }
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
