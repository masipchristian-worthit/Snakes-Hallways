using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Engancha automáticamente UIHover (PointerEnter) y UISelect (onClick) a TODOS los
/// componentes Button de la escena. Se ejecuta al cargar cada escena y vuelve a
/// escanear cuando aparecen botones nuevos (p.ej. menús dinámicos) si lo llamas
/// manualmente con RescanCurrentScene().
///
/// Coloca este componente en el mismo GameObject persistente que el AudioManager.
/// </summary>
[DefaultExecutionOrder(100)]
public class UIClickSounds : MonoBehaviour
{
    public static UIClickSounds Instance { get; private set; }

    [SerializeField] SFXId clickSfx = SFXId.UISelect;
    [SerializeField] SFXId hoverSfx = SFXId.UIHover;
    [Tooltip("Botones con este nombre (case-insensitive) usan UICancel en vez de UISelect.")]
    [SerializeField] string[] cancelButtonNames = { "back", "cancel", "close", "exit", "quit", "atras", "atrás", "volver" };

    // Marcador por instancia para no enganchar dos veces el mismo botón.
    readonly HashSet<int> wired = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()  { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void Start() { RescanCurrentScene(); }

    void OnSceneLoaded(Scene s, LoadSceneMode mode) { RescanCurrentScene(); }

    /// <summary>Vuelve a escanear todos los Button activos en la escena.</summary>
    public void RescanCurrentScene()
    {
        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in buttons) Wire(b);
    }

    /// <summary>Engancha un único botón. Idempotente.</summary>
    public void Wire(Button b)
    {
        if (b == null) return;
        int id = b.GetInstanceID();
        if (wired.Contains(id)) return;
        wired.Add(id);

        // Click → UISelect (o UICancel si el nombre encaja).
        SFXId clickId = IsCancelButton(b.name) ? SFXId.UICancel : clickSfx;
        b.onClick.AddListener(() => AudioManager.Instance?.PlaySFX2D(clickId));

        // Hover → UIHover.
        var trig = b.GetComponent<EventTrigger>();
        if (trig == null) trig = b.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => AudioManager.Instance?.PlaySFX2D(hoverSfx));
        trig.triggers.Add(entry);
    }

    bool IsCancelButton(string name)
    {
        if (string.IsNullOrEmpty(name) || cancelButtonNames == null) return false;
        string lower = name.ToLowerInvariant();
        foreach (var n in cancelButtonNames)
            if (!string.IsNullOrEmpty(n) && lower.Contains(n.ToLowerInvariant())) return true;
        return false;
    }
}
