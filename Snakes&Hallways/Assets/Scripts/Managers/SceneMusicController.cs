using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reproduce la música asociada a cada escena. Usa el crossfade del AudioManager y,
/// si la escena requiere la misma pista que ya está sonando, NO la reinicia (esto
/// permite que AmbienceHorror se mantenga entre SCN_Introduction y SCN_Labe).
///
/// Asignación por defecto:
///   - SCN_MainMenu, SCN_Settings, SCN_Difficulty      → AmbienceCalm
///   - SCN_Introduction, SCN_Labe                       → AmbienceHorror
///   - SCN_DeathScene                                   → GameOver
///   - SCN_EndingScene                                  → Win
///   - Resto                                            → sin cambios
/// </summary>
[DefaultExecutionOrder(50)]
public class SceneMusicController : MonoBehaviour
{
    public static SceneMusicController Instance { get; private set; }

    [System.Serializable]
    public class SceneMusic
    {
        public string sceneName;
        public MusicId music = MusicId.None;
    }

    [Header("Mapeo escena → música")]
    [SerializeField] List<SceneMusic> mapping = new()
    {
        new SceneMusic { sceneName = "SCN_MainMenu",     music = MusicId.AmbienceCalm },
        new SceneMusic { sceneName = "SCN_Settings",     music = MusicId.AmbienceCalm },
        new SceneMusic { sceneName = "SCN_Difficulty",   music = MusicId.AmbienceCalm },
        new SceneMusic { sceneName = "SCN_Introduction", music = MusicId.AmbienceHorror },
        new SceneMusic { sceneName = "SCN_Labe",         music = MusicId.AmbienceHorror },
        new SceneMusic { sceneName = "SCN_DeathScene",   music = MusicId.GameOver },
        new SceneMusic { sceneName = "SCN_EndingScene",  music = MusicId.Win },
    };

    [Header("Fades")]
    [SerializeField] float fadeIn = 1.5f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()  { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void Start()
    {
        // Música de la escena ya activa al boot.
        ApplyForScene(SceneManager.GetActiveScene().name);
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        ApplyForScene(s.name);
    }

    void ApplyForScene(string sceneName)
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        var entry = mapping.Find(m => m.sceneName == sceneName);
        if (entry == null) return; // Escena no mapeada: no toca nada (persiste lo que sonase).

        if (entry.music == MusicId.None)
        {
            am.StopMusic(fadeIn);
            return;
        }
        // PlayMusic ya hace early-return si la pista solicitada coincide con la actual.
        am.PlayMusic(entry.music, fadeIn);
    }
}
