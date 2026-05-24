using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// Singleton runtime controller for materials using Custom/MainShader.
/// Drop on a GameObject in the scene hierarchy. Survives scene loads.
[DisallowMultipleComponent]
public class ShaderManager : MonoBehaviour
{
    public static ShaderManager Instance { get; private set; }

    private const string TargetShaderName = "Custom/MainShader";

    [Header("Target Materials")]
    [Tooltip("Materials controlled by this manager. Use the Auto-Fill button to populate from the scene.")]
    public List<Material> targetMaterials = new List<Material>();

    // ============================================================
    // MASTER CONTROLS  (affect all materials, all features)
    // ============================================================
    [Header("Master Visibility")]
    [Tooltip("Global multiplier for all stylized effects: Fake AO, dither, palette, highlight granulation. 0 = pure URP/Lit, 1 = full effect.")]
    [Range(0f, 1f)] public float masterVisibility = 1f;

    [Header("Master Burn Control")]
    [Tooltip("Linear exposure multiplier applied right after PBR. <1 dims the whole scene, >1 brightens.")]
    [Range(0f, 3f)] public float exposure       = 1.0f;
    [Tooltip("Hard ceiling per RGB channel. Lower = less burn, more flat highlights. Default 8 = nearly no clamp.")]
    [Range(0.5f, 8f)] public float maxBrightness = 8.0f;

    [Header("Feature Toggles")]
    public bool fakeAOEnabled            = true;
    public bool ditherEnabled            = true;
    public bool paletteEnabled           = true;
    public bool highlightGranulateEnabled = true;
    public bool stylizedTriplanarEnabled  = false;

    // ============================================================
    // INDIVIDUAL CONTROLS  (used when their toggle is ON)
    // ============================================================
    [Header("Stylized Triplanar AO")]
    [Range(0f, 1f)] public float stylizedAOStrength = 0f;

    [Header("Screen-Space Fake AO")]
    [Range(0f, 3f)] public float fakeAOStrength          = 1.0f;
    [Range(0f, 10f)] public float fakeAONormalSensitivity = 2.5f;
    [Range(0f, 10f)] public float fakeAODepthSensitivity  = 1.0f;

    [Header("Three Level Distance System")]
    [Tooltip("End of Base level (0..L1). Inside this range AO is at Level 1 multiplier.")]
    public float level1Distance = 3.0f;
    [Tooltip("End of Intermediate level (L1..L2). After this, Level 3 (Total) takes over.")]
    public float level2Distance = 8.0f;
    [Tooltip("Softness of transitions between levels (meters). Larger = smoother gradients near boundary.")]
    [Range(0.1f, 5f)] public float levelBlend = 1.5f;

    [Header("AO Per Level Multipliers")]
    [Tooltip("AO multiplier at Base level (0..L1). Subtle.")]
    [Range(0f, 3f)] public float aoLevel1Mul = 0.6f;
    [Tooltip("AO multiplier at Intermediate level (L1..L2). Amplified.")]
    [Range(0f, 5f)] public float aoLevel2Mul = 1.8f;
    [Tooltip("AO multiplier at Total level (L2+). Heavy.")]
    [Range(0f, 8f)] public float aoLevel3Mul = 4.0f;

    [Header("Dither Per Level Multipliers")]
    [Range(0f, 3f)] public float ditherLevel1Mul = 1.0f;
    [Range(0f, 3f)] public float ditherLevel2Mul = 1.7f;
    [Range(0f, 4f)] public float ditherLevel3Mul = 2.6f;

    [Header("Level 3 Shader Fog")]
    [Tooltip("Strength of the fog that kicks in at the Total level. 1 = fully fades to fog color.")]
    [Range(0f, 1f)] public float level3FogStrength = 0.85f;
    public Color    level3FogColor   = new Color(0.02f, 0.02f, 0.02f, 1f);
    [Tooltip("Distance offset (m) past Level 2 where the fog actually starts ramping up.")]
    public float    level3FogStart   = 0.0f;

    [Header("Shadow Accumulation")]
    [Range(1f, 5f)] public float shadowAOBoost     = 1.5f;
    [Range(0f, 1f)] public float shadowAOThreshold = 0.35f;

    [Header("Dither + PSX Palette")]
    [Range(0f, 1f)] public float ditherStrength     = 0.15f;
    [Range(1f, 8f)] public float ditherScale        = 1.0f;
    [Range(0f, 1f)] public float highlightDither    = 0.15f;
    [Range(0f, 2f)] public float highlightThreshold = 0.85f;
    [Range(2f, 64f)] public float paletteSteps      = 48f;
    [Range(0f, 1.5f)] public float paletteSaturation = 1.00f;

    [Header("Highlight Softness (Reinhard rolloff)")]
    [Range(0f, 1f)] public float highlightSoftness  = 0.10f;

    [Header("Ambient Lift (unlit visibility floor) - OFF by default")]
    [Tooltip("Lifts unlit pixels back toward their own ALBEDO color (preserves texture identity, no gray wash). 0 = off, ~0.10 = subtle, ~0.30 = obvious. Use only if scene is too dark.")]
    [Range(0f, 1f)] public float ambientLift             = 0.0f;
    [Tooltip("Distance at which the lift fades to 0. Beyond this, unlit pixels can go fully black.")]
    public float ambientLiftFadeDistance                 = 8.0f;
    [Tooltip("Color tint of the ambient lift. Default white = neutral gray floor.")]
    public Color ambientLiftTint                         = Color.white;

    [Header("Global Surface Overrides")]
    [Range(0f, 2f)] public float normalStrength     = 1f;
    [Range(0f, 1f)] public float occlusionStrength  = 1f;
    [Range(0f, 1f)] public float curvatureStrength  = 0f;

    [Header("Behavior")]
    [Tooltip("Persist this manager across scene loads.")]
    public bool dontDestroyOnLoad = true;
    [Tooltip("Apply overrides automatically on Awake.")]
    public bool applyOnAwake = true;
    [Tooltip("Apply overrides every time a value changes in the inspector (editor only).")]
    public bool applyOnValidate = true;

    // Cached property IDs
    private static readonly int ID_StylizedAOStrength       = Shader.PropertyToID("_StylizedAOStrength");
    private static readonly int ID_FakeAOStrength           = Shader.PropertyToID("_FakeAOStrength");
    private static readonly int ID_FakeAONormalSensitivity  = Shader.PropertyToID("_FakeAONormalSensitivity");
    private static readonly int ID_FakeAODepthSensitivity   = Shader.PropertyToID("_FakeAODepthSensitivity");
    private static readonly int ID_Level1Distance           = Shader.PropertyToID("_Level1Distance");
    private static readonly int ID_Level2Distance           = Shader.PropertyToID("_Level2Distance");
    private static readonly int ID_LevelBlend               = Shader.PropertyToID("_LevelBlend");
    private static readonly int ID_AOLevel1Mul              = Shader.PropertyToID("_AOLevel1Mul");
    private static readonly int ID_AOLevel2Mul              = Shader.PropertyToID("_AOLevel2Mul");
    private static readonly int ID_AOLevel3Mul              = Shader.PropertyToID("_AOLevel3Mul");
    private static readonly int ID_DitherLevel1Mul          = Shader.PropertyToID("_DitherLevel1Mul");
    private static readonly int ID_DitherLevel2Mul          = Shader.PropertyToID("_DitherLevel2Mul");
    private static readonly int ID_DitherLevel3Mul          = Shader.PropertyToID("_DitherLevel3Mul");
    private static readonly int ID_Level3FogStrength        = Shader.PropertyToID("_Level3FogStrength");
    private static readonly int ID_Level3FogColor           = Shader.PropertyToID("_Level3FogColor");
    private static readonly int ID_Level3FogStart           = Shader.PropertyToID("_Level3FogStart");
    private static readonly int ID_ShadowAOBoost            = Shader.PropertyToID("_ShadowAOBoost");
    private static readonly int ID_ShadowAOThreshold        = Shader.PropertyToID("_ShadowAOThreshold");
    private static readonly int ID_DitherStrength           = Shader.PropertyToID("_DitherStrength");
    private static readonly int ID_DitherScale              = Shader.PropertyToID("_DitherScale");
    private static readonly int ID_HighlightDither          = Shader.PropertyToID("_HighlightDither");
    private static readonly int ID_HighlightThreshold       = Shader.PropertyToID("_HighlightThreshold");
    private static readonly int ID_PaletteSteps             = Shader.PropertyToID("_PaletteSteps");
    private static readonly int ID_PaletteSaturation        = Shader.PropertyToID("_PaletteSaturation");
    private static readonly int ID_HighlightSoftness        = Shader.PropertyToID("_HighlightSoftness");
    private static readonly int ID_AmbientLift              = Shader.PropertyToID("_AmbientLift");
    private static readonly int ID_AmbientLiftFadeDistance  = Shader.PropertyToID("_AmbientLiftFadeDistance");
    private static readonly int ID_AmbientLiftTint          = Shader.PropertyToID("_AmbientLiftTint");
    private static readonly int ID_Exposure                 = Shader.PropertyToID("_Exposure");
    private static readonly int ID_MaxBrightness            = Shader.PropertyToID("_MaxBrightness");
    private static readonly int ID_NormalStrength           = Shader.PropertyToID("_NormalStrength");
    private static readonly int ID_OcclusionStrength        = Shader.PropertyToID("_OcclusionStrength");
    private static readonly int ID_CurvatureStrength        = Shader.PropertyToID("_CurvatureStrength");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        if (applyOnAwake) ApplyToAll();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!applyOnValidate) return;
        // Defer to avoid SendMessage warnings during inspector edits.
        EditorApplication.delayCall += () => { if (this != null) ApplyToAll(); };
    }
#endif

    /// Push current settings to every target material.
    /// Honors master visibility multiplier and individual feature toggles.
    public void ApplyToAll()
    {
        float v = Mathf.Clamp01(masterVisibility);

        // Effective per-feature values, scaled by masterVisibility and toggles.
        float effStylizedAO    = stylizedTriplanarEnabled  ? stylizedAOStrength * v : 0f;
        float effFakeAO        = fakeAOEnabled             ? fakeAOStrength * v     : 0f;
        float effDither        = ditherEnabled             ? ditherStrength * v     : 0f;
        float effHLDither      = highlightGranulateEnabled ? highlightDither * v    : 0f;

        // Palette: when disabled or visibility=0, lerp toward "no quantization" (steps=64, sat=1).
        float effPaletteSteps  = paletteEnabled
            ? Mathf.Lerp(64f, paletteSteps, v)
            : 64f;
        float effPaletteSat    = paletteEnabled
            ? Mathf.Lerp(1f, paletteSaturation, v)
            : 1f;

        for (int i = 0; i < targetMaterials.Count; i++)
        {
            var m = targetMaterials[i];
            if (m == null) continue;

            // Stylized triplanar AO (gated by toggle + visibility)
            m.SetFloat(ID_StylizedAOStrength,      effStylizedAO);

            // Screen-space fake AO (gated by toggle + visibility)
            m.SetFloat(ID_FakeAOStrength,          effFakeAO);
            m.SetFloat(ID_FakeAONormalSensitivity, fakeAONormalSensitivity);
            m.SetFloat(ID_FakeAODepthSensitivity,  fakeAODepthSensitivity);
            // Three-level distance system (replaces old linear ramp)
            m.SetFloat(ID_Level1Distance,          level1Distance);
            m.SetFloat(ID_Level2Distance,          level2Distance);
            m.SetFloat(ID_LevelBlend,              levelBlend);
            m.SetFloat(ID_AOLevel1Mul,             aoLevel1Mul * v);
            m.SetFloat(ID_AOLevel2Mul,             aoLevel2Mul * v);
            m.SetFloat(ID_AOLevel3Mul,             aoLevel3Mul * v);
            m.SetFloat(ID_DitherLevel1Mul,         ditherLevel1Mul);
            m.SetFloat(ID_DitherLevel2Mul,         ditherLevel2Mul);
            m.SetFloat(ID_DitherLevel3Mul,         ditherLevel3Mul);
            m.SetFloat(ID_Level3FogStrength,       level3FogStrength * v);
            m.SetColor(ID_Level3FogColor,          level3FogColor);
            m.SetFloat(ID_Level3FogStart,          level3FogStart);
            m.SetFloat(ID_ShadowAOBoost,           shadowAOBoost);
            m.SetFloat(ID_ShadowAOThreshold,       shadowAOThreshold);

            // Dither + highlight granulation (gated by toggles + visibility)
            m.SetFloat(ID_DitherStrength,          effDither);
            m.SetFloat(ID_DitherScale,             ditherScale);
            m.SetFloat(ID_HighlightDither,         effHLDither);
            m.SetFloat(ID_HighlightThreshold,      highlightThreshold);

            // Palette quantization (gated by toggle + visibility)
            m.SetFloat(ID_PaletteSteps,            effPaletteSteps);
            m.SetFloat(ID_PaletteSaturation,       effPaletteSat);

            // Always-on tonemap controls
            m.SetFloat(ID_HighlightSoftness,       highlightSoftness);
            m.SetFloat(ID_Exposure,                exposure);
            m.SetFloat(ID_MaxBrightness,           maxBrightness);

            // Ambient lift (unlit visibility floor)
            m.SetFloat(ID_AmbientLift,             ambientLift);
            m.SetFloat(ID_AmbientLiftFadeDistance, ambientLiftFadeDistance);
            m.SetColor(ID_AmbientLiftTint,         ambientLiftTint);

            // Surface overrides (URP/Lit base)
            m.SetFloat(ID_NormalStrength,          normalStrength);
            m.SetFloat(ID_OcclusionStrength,       occlusionStrength);
            m.SetFloat(ID_CurvatureStrength,       curvatureStrength);
        }
    }

    /// Add a material at runtime and apply current settings to it.
    public void Register(Material m)
    {
        if (m == null || targetMaterials.Contains(m)) return;
        targetMaterials.Add(m);
        ApplyToAll();
    }

    /// Scan the active scene for renderers using Custom/MainShader and fill the list.
    [ContextMenu("Auto-Fill From Scene")]
    public void AutoFillFromScene()
    {
        var found = new HashSet<Material>();
        var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            var mats = Application.isPlaying ? r.materials : r.sharedMaterials;
            foreach (var m in mats)
            {
                if (m != null && m.shader != null && m.shader.name == TargetShaderName)
                    found.Add(m);
            }
        }

        foreach (var m in found)
            if (!targetMaterials.Contains(m))
                targetMaterials.Add(m);

        Debug.Log($"[ShaderManager] Auto-fill: {found.Count} MainShader materials found, {targetMaterials.Count} total in list.");
    }

    [ContextMenu("Clear List")]
    public void ClearList()
    {
        targetMaterials.Clear();
    }

    // ============================================================
    // SCENE PRESETS  (one-click configurations for common workflows)
    // ============================================================

    /// Build / Level editing mode: everything as visible as possible,
    /// AO and dither minimized so geometry is easy to read in scene view.
    /// Does NOT touch saved settings - just applies overrides to materials.
    [ContextMenu("Preset: Build Mode (Scene Edit)")]
    public void PresetBuildMode()
    {
        masterVisibility = 0f;                 // mute all stylized effects
        ambientLift      = 0.20f;              // floor of visibility on dark areas
        ambientLiftFadeDistance = 30f;
        level3FogStrength = 0f;                // no fog
        ApplyToAll();
        Debug.Log("[ShaderManager] Build Mode preset applied. Scene clean for editing.");
    }

    /// Default gameplay look: 3-level system active with balanced multipliers.
    [ContextMenu("Preset: Gameplay (Default)")]
    public void PresetGameplay()
    {
        masterVisibility = 1f;
        ambientLift      = 0f;
        ambientLiftFadeDistance = 8f;
        level1Distance   = 3f;
        level2Distance   = 8f;
        levelBlend       = 1.5f;
        aoLevel1Mul      = 0.6f;
        aoLevel2Mul      = 1.8f;
        aoLevel3Mul      = 4.0f;
        ditherLevel1Mul  = 1.0f;
        ditherLevel2Mul  = 1.7f;
        ditherLevel3Mul  = 2.6f;
        level3FogStrength = 0.85f;
        ApplyToAll();
        Debug.Log("[ShaderManager] Gameplay preset applied.");
    }

    /// Inscryption-style heavy: marked AO, palette compressed, fog very strong.
    [ContextMenu("Preset: Inscryption Heavy")]
    public void PresetInscryption()
    {
        masterVisibility = 1f;
        fakeAOStrength   = 1.5f;
        fakeAONormalSensitivity = 3.5f;
        fakeAODepthSensitivity  = 1.5f;
        shadowAOBoost    = 2.5f;
        shadowAOThreshold = 0.45f;
        ditherStrength   = 0.30f;
        highlightDither  = 0.30f;
        paletteSteps     = 24f;
        paletteSaturation = 0.85f;
        level1Distance   = 2.5f;
        level2Distance   = 7f;
        levelBlend       = 1.2f;
        aoLevel1Mul      = 0.8f;
        aoLevel2Mul      = 2.2f;
        aoLevel3Mul      = 5.0f;
        ditherLevel1Mul  = 1.2f;
        ditherLevel2Mul  = 2.0f;
        ditherLevel3Mul  = 3.0f;
        level3FogStrength = 0.95f;
        level3FogColor   = new Color(0.02f, 0.015f, 0.01f, 1f);
        ApplyToAll();
        Debug.Log("[ShaderManager] Inscryption preset applied.");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ShaderManager))]
public class ShaderManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var mgr = (ShaderManager)target;

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto-Fill From Scene"))
            {
                Undo.RecordObject(mgr, "Auto-Fill ShaderManager");
                mgr.AutoFillFromScene();
                EditorUtility.SetDirty(mgr);
            }
            if (GUILayout.Button("Apply To All"))
            {
                mgr.ApplyToAll();
            }
            if (GUILayout.Button("Clear"))
            {
                Undo.RecordObject(mgr, "Clear ShaderManager");
                mgr.ClearList();
                EditorUtility.SetDirty(mgr);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Workflow Presets", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button("BUILD MODE (Scene Edit)"))
            {
                Undo.RecordObject(mgr, "ShaderManager: Build Mode");
                mgr.PresetBuildMode();
                EditorUtility.SetDirty(mgr);
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Gameplay (Default)"))
            {
                Undo.RecordObject(mgr, "ShaderManager: Gameplay");
                mgr.PresetGameplay();
                EditorUtility.SetDirty(mgr);
            }
            GUI.backgroundColor = new Color(1f, 0.85f, 0.7f);
            if (GUILayout.Button("Inscryption Heavy"))
            {
                Undo.RecordObject(mgr, "ShaderManager: Inscryption");
                mgr.PresetInscryption();
                EditorUtility.SetDirty(mgr);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Master Visibility Quick", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Pure URP (visibility 0)"))
            {
                Undo.RecordObject(mgr, "ShaderManager: Pure URP");
                mgr.masterVisibility = 0f;
                mgr.ApplyToAll();
                EditorUtility.SetDirty(mgr);
            }
            if (GUILayout.Button("Subtle (0.4)"))
            {
                Undo.RecordObject(mgr, "ShaderManager: Subtle");
                mgr.masterVisibility = 0.4f;
                mgr.ApplyToAll();
                EditorUtility.SetDirty(mgr);
            }
            if (GUILayout.Button("Full (1.0)"))
            {
                Undo.RecordObject(mgr, "ShaderManager: Full Effect");
                mgr.masterVisibility = 1f;
                mgr.ApplyToAll();
                EditorUtility.SetDirty(mgr);
            }
        }
    }
}
#endif
