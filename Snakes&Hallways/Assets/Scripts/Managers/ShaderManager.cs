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

    [Header("Stylized Triplanar AO")]
    [Range(0f, 1f)] public float stylizedAOStrength = 0f;

    [Header("Scene Extremes AO (distance darken)")]
    [Range(0f, 1f)] public float aoExtremesStrength = 0.45f;
    public float aoStartDistance = 4f;
    public float aoEndDistance   = 14f;

    [Header("Highlight Softness (Reinhard rolloff)")]
    [Range(0f, 1f)] public float highlightSoftness  = 0.15f;

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
    private static readonly int ID_StylizedAOStrength = Shader.PropertyToID("_StylizedAOStrength");
    private static readonly int ID_AOExtremesStrength = Shader.PropertyToID("_AOExtremesStrength");
    private static readonly int ID_AOStartDistance    = Shader.PropertyToID("_AOStartDistance");
    private static readonly int ID_AOEndDistance      = Shader.PropertyToID("_AOEndDistance");
    private static readonly int ID_HighlightSoftness  = Shader.PropertyToID("_HighlightSoftness");
    private static readonly int ID_NormalStrength    = Shader.PropertyToID("_NormalStrength");
    private static readonly int ID_OcclusionStrength = Shader.PropertyToID("_OcclusionStrength");
    private static readonly int ID_CurvatureStrength = Shader.PropertyToID("_CurvatureStrength");

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
    public void ApplyToAll()
    {
        for (int i = 0; i < targetMaterials.Count; i++)
        {
            var m = targetMaterials[i];
            if (m == null) continue;
            m.SetFloat(ID_StylizedAOStrength, stylizedAOStrength);
            m.SetFloat(ID_AOExtremesStrength, aoExtremesStrength);
            m.SetFloat(ID_AOStartDistance,    aoStartDistance);
            m.SetFloat(ID_AOEndDistance,      aoEndDistance);
            m.SetFloat(ID_HighlightSoftness,  highlightSoftness);
            m.SetFloat(ID_NormalStrength,     normalStrength);
            m.SetFloat(ID_OcclusionStrength,  occlusionStrength);
            m.SetFloat(ID_CurvatureStrength,  curvatureStrength);
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
    }
}
#endif
