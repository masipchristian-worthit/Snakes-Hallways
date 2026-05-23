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

    [Header("Distance Driven Intensity")]
    [Tooltip("Below this distance the AO is at near strength (default 0 = invisible).")]
    public float viewMinDistance = 2.0f;
    [Tooltip("At this distance the AO reaches far strength (full darkening). Lower = AO kicks in sooner.")]
    public float viewMaxDistance = 18.0f;
    [Range(0f, 2f)] public float aoNearStrength = 0.0f;
    [Range(0f, 2f)] public float aoFarStrength  = 1.0f;
    [Tooltip("Power curve for the distance ramp. Higher = AO stays invisible longer, kicks in sharply at max distance.")]
    [Range(0.1f, 8f)] public float aoFalloffPower = 2.5f;

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
    private static readonly int ID_ViewMinDistance          = Shader.PropertyToID("_ViewMinDistance");
    private static readonly int ID_ViewMaxDistance          = Shader.PropertyToID("_ViewMaxDistance");
    private static readonly int ID_AONearStrength           = Shader.PropertyToID("_AONearStrength");
    private static readonly int ID_AOFarStrength            = Shader.PropertyToID("_AOFarStrength");
    private static readonly int ID_AOFalloffPower           = Shader.PropertyToID("_AOFalloffPower");
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
            m.SetFloat(ID_ViewMinDistance,         viewMinDistance);
            m.SetFloat(ID_ViewMaxDistance,         viewMaxDistance);
            m.SetFloat(ID_AONearStrength,          aoNearStrength);
            m.SetFloat(ID_AOFarStrength,           aoFarStrength);
            m.SetFloat(ID_AOFalloffPower,          aoFalloffPower);
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
        EditorGUILayout.LabelField("Quick Visibility Presets", EditorStyles.boldLabel);
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
