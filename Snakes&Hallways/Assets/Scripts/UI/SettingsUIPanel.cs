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

        // ORDEN IMPORTANTE:
        // 1. Desactivar wrappers redundantes (AudioVolumeSlider, SensitivitySlider) que
        //    puedan estar también en los GameObjects de los sliders — si tienen channel
        //    mal configurado los 4 podrían modificar el mismo valor (síntoma: "actúan como uno").
        DisableRedundantSliderWrappers();

        InitializeAudioSliders();
        InitializeGameplaySliders();
        InitializeGraphicsSettings();
        SetupButtons();
        SetupNavigation();

        SettingsManager.Instance.OnSettingsChanged += RefreshUI;
    }

    /// <summary>
    /// Algunos sliders ya tienen un componente AudioVolumeSlider o SensitivitySlider
    /// (autobinding directo al SettingsManager). Si ESE Slider además está referenciado en
    /// SettingsUIPanel, hay DOBLE binding — y peor: si todos los AudioVolumeSlider tienen el
    /// mismo Channel (ej. todos = Master), TODOS modifican el mismo valor y los 4 parecen
    /// el mismo. Para evitar el conflicto, desactivamos esos wrappers en runtime.
    /// </summary>
    void DisableRedundantSliderWrappers()
    {
        var sliders = new Slider[] { masterVolumeSlider, sfxVolumeSlider, musicVolumeSlider, uiVolumeSlider, mouseSensitivitySlider };
        foreach (var s in sliders)
        {
            if (s == null) continue;
            var avs = s.GetComponent<AudioVolumeSlider>();
            if (avs != null && avs.enabled)
            {
                Debug.LogWarning($"[SettingsUIPanel] Slider '{s.name}' tiene AudioVolumeSlider Y está bindeado por SettingsUIPanel → doble binding. Desactivando AudioVolumeSlider (su 'channel' puede estar mal configurado y modificar el canal incorrecto).", avs);
                avs.enabled = false;
            }
            var sens = s.GetComponent<SensitivitySlider>();
            if (sens != null && sens.enabled)
            {
                Debug.LogWarning($"[SettingsUIPanel] Slider '{s.name}' tiene SensitivitySlider Y está bindeado por SettingsUIPanel → doble binding. Desactivando SensitivitySlider.", sens);
                sens.enabled = false;
            }
        }
    }

    /// <summary>
    /// Configura Navigation EXPLICIT + ejecuta diagnóstico para detectar problemas comunes:
    ///
    ///   1. DUPLICADOS: si varios slots del inspector apuntan AL MISMO Slider (error típico
    ///      "arrastré el mismo prefab 4 veces"), los 4 'sliders' son uno solo → se comportan
    ///      como uno. Detecta esto comparando instanceIDs.
    ///
    ///   2. CanvasGroup BLOQUEANTE: un CanvasGroup en el padre con interactable=false o
    ///      blocksRaycasts=false bloquea TODA la jerarquía hija — pero a veces solo el primer
    ///      Selectable consigue foco por orden de jerarquía. Se autocura forzando ambos true.
    ///
    ///   3. Selectable.interactable = false: idem, lo forzamos a true.
    ///
    ///   4. Slider.direction != Horizontal: si está como Vertical, ↑↓ ajustan valor y ←→
    ///      navegan — opuesto a lo que el layout requiere. Se fuerza LeftToRight.
    ///
    /// Si los logs muestran DUPLICADOS, el problema NO es el código sino las referencias del
    /// inspector — abre SettingsUIPanel en el inspector y verifica que cada slot apunta a un
    /// Slider/Toggle/Dropdown DIFERENTE en la jerarquía.
    /// </summary>
    void SetupNavigation()
    {
        // Columna izquierda (top→bottom).
        var left = new Selectable[] { masterVolumeSlider, sfxVolumeSlider, musicVolumeSlider, uiVolumeSlider };
        // Columna derecha (top→bottom).
        var right = new Selectable[] { vsyncToggle, fullscreenToggle, resolutionDropdown, mouseSensitivitySlider };

        // Aseguramos que TODOS los sliders horizontales lo están de verdad.
        SetSlidersHorizontal(masterVolumeSlider, sfxVolumeSlider, musicVolumeSlider, uiVolumeSlider, mouseSensitivitySlider);

        // Diagnóstico: detectar duplicados y bloqueos.
        var allSelectables = new System.Collections.Generic.List<Selectable>();
        foreach (var s in left)  if (s != null) allSelectables.Add(s);
        foreach (var s in right) if (s != null) allSelectables.Add(s);
        DiagnoseAndAutoCure(allSelectables);

        ChainVertical(left);
        ChainVertical(right);
    }

    void DiagnoseAndAutoCure(System.Collections.Generic.List<Selectable> all)
    {
        // 1) Detectar referencias duplicadas (mismo Selectable asignado a más de un slot).
        var seen = new System.Collections.Generic.Dictionary<int, Selectable>();
        bool anyDup = false;
        foreach (var s in all)
        {
            int id = s.GetInstanceID();
            if (seen.ContainsKey(id))
            {
                Debug.LogError($"[SettingsUIPanel] ❌ DUPLICADO DETECTADO: '{s.name}' (instanceID={id}) está asignado a MÁS DE UN slot del inspector del SettingsUIPanel. Por eso varios sliders 'actúan como uno' — porque LITERALMENTE son el mismo. Abre el SettingsUIPanel en el inspector y arrastra el Slider/Toggle correcto a cada slot.", s);
                anyDup = true;
            }
            else
            {
                seen[id] = s;
            }
        }
        if (!anyDup)
        {
            // Log informativo de qué quedó registrado — útil para verificar visualmente que
            // cada slot apunta a un GameObject distinto.
            string list = "";
            foreach (var s in all) list += $"\n  • {s.GetType().Name} '{s.name}' (id={s.GetInstanceID()}) interactable={s.interactable}";
            Debug.Log($"[SettingsUIPanel] Diagnóstico OK — {all.Count} selectables únicos registrados:{list}", this);
        }

        // 2) Forzar interactable + comprobar CanvasGroup en parents.
        foreach (var s in all)
        {
            if (!s.interactable)
            {
                Debug.LogWarning($"[SettingsUIPanel] '{s.name}' tenía interactable=false — forzando a true.", s);
                s.interactable = true;
            }
            var cg = s.GetComponentInParent<CanvasGroup>(includeInactive: false);
            if (cg != null && (!cg.interactable || !cg.blocksRaycasts))
            {
                Debug.LogWarning($"[SettingsUIPanel] CanvasGroup '{cg.name}' en parent de '{s.name}' tenía interactable={cg.interactable} blocksRaycasts={cg.blocksRaycasts} — bloqueaba la interacción. Activando ambos.", cg);
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }
    }

    static void SetSlidersHorizontal(params Slider[] sliders)
    {
        foreach (var s in sliders)
        {
            if (s == null) continue;
            // Si el slider está como Vertical, ↑↓ se usarían para ajustar el valor y ←→ navegar
            // — lo contrario a lo que queremos en este panel. Lo forzamos a horizontal.
            if (s.direction != Slider.Direction.LeftToRight && s.direction != Slider.Direction.RightToLeft)
                s.direction = Slider.Direction.LeftToRight;
        }
    }

    static void ChainVertical(Selectable[] list)
    {
        // Filtramos nulls Y también duplicados (instanceID ya visto en esta misma columna).
        // Si hay duplicados, encadenarlos rompe la navegación — mejor saltarlos.
        var clean = new System.Collections.Generic.List<Selectable>();
        var seen = new System.Collections.Generic.HashSet<int>();
        foreach (var s in list)
        {
            if (s == null) continue;
            if (!seen.Add(s.GetInstanceID())) continue;
            clean.Add(s);
        }
        for (int i = 0; i < clean.Count; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            if (i > 0)                  nav.selectOnUp   = clean[i - 1];
            if (i < clean.Count - 1)    nav.selectOnDown = clean[i + 1];
            // selectOnLeft / selectOnRight se quedan null → la selección NO cambia con ←/→.
            // Esto deja libres esas teclas para que el slider las consuma y ajuste su valor.
            clean[i].navigation = nav;
        }
    }

    void InitializeAudioSliders()
    {
        // RemoveAllListeners antes de añadir el nuestro: garantiza que NO hay listeners
        // configurados en el prefab del slider apuntando a sitios incorrectos (causa común
        // de "el slider X modifica el canal Y").
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            masterVolumeSlider.value = SettingsManager.Instance.Master;
            masterVolumeSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetMaster(v));
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.value = SettingsManager.Instance.Sfx;
            sfxVolumeSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetSfx(v));
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            musicVolumeSlider.value = SettingsManager.Instance.Music;
            musicVolumeSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetMusic(v));
        }

        if (uiVolumeSlider != null)
        {
            uiVolumeSlider.onValueChanged.RemoveAllListeners();
            uiVolumeSlider.value = SettingsManager.Instance.Ui;
            uiVolumeSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetUi(v));
        }
    }

    void InitializeGameplaySliders()
    {
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
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
