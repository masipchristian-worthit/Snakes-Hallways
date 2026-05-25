using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resaltado tipo "rayos X" para pickups (activos), portal (cuando se enciende)
/// y enemigo. Se llama a <see cref="Toggle"/> desde el PlayerController al pulsar
/// la acción Highlight (ç por defecto).
///
/// Implementación:
///   - Mantiene una lista de "snapshots" (Renderer + array de materiales originales).
///   - Al activar, escanea targets y swappea sus materiales por uno con el shader
///     "SH/Highlight" (ZTest Always → atraviesa paredes).
///   - Al desactivar, restaura los materiales originales.
///
/// El material por categoría se crea en runtime (no hace falta asset en disco) si
/// no se asigna uno en el inspector.
/// </summary>
public class HighlightController : MonoBehaviour
{
    public static HighlightController Instance { get; private set; }

    [Header("Highlight materials (opcional). Si vacíos, se crean en runtime con SH/Highlight.")]
    [SerializeField] Material pickupMat;
    [SerializeField] Material portalMat;
    [SerializeField] Material enemyMat;

    [Header("Colores por defecto (si los materiales se autocrean)")]
    [SerializeField] Color pickupColor = new Color(1f, 0.85f, 0.2f, 0.55f);
    [SerializeField] Color portalColor = new Color(0.2f, 1f, 0.6f, 0.6f);
    [SerializeField] Color enemyColor  = new Color(1f, 0.25f, 0.25f, 0.6f);

    struct Snapshot { public Renderer r; public Material[] originals; }
    readonly List<Snapshot> snapshots = new();
    bool active;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsureMaterials();
    }

    static HighlightController EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("HighlightController (auto)");
        Instance = go.AddComponent<HighlightController>();
        return Instance;
    }

    void EnsureMaterials()
    {
        var sh = Shader.Find("SH/Highlight");
        if (sh == null) { Debug.LogWarning("[HighlightController] Shader 'SH/Highlight' no encontrado."); return; }
        if (pickupMat == null) { pickupMat = new Material(sh); pickupMat.SetColor("_Color", pickupColor); pickupMat.SetColor("_OutlineColor", new Color(1f,1f,0.5f,1f)); }
        if (portalMat == null) { portalMat = new Material(sh); portalMat.SetColor("_Color", portalColor); portalMat.SetColor("_OutlineColor", new Color(0.5f,1f,0.7f,1f)); }
        if (enemyMat  == null) { enemyMat  = new Material(sh); enemyMat .SetColor("_Color", enemyColor ); enemyMat .SetColor("_OutlineColor", new Color(1f,0.5f,0.5f,1f)); }
    }

    public static void Toggle()
    {
        var c = EnsureInstance();
        if (c.active) c.Deactivate();
        else c.Activate();
    }

    void Activate()
    {
        if (active) return;
        EnsureMaterials();
        snapshots.Clear();

        // Pickups: solo los activos en escena.
        if (PickupManager.Instance != null)
        {
            foreach (var p in PickupManager.Instance.AllScenePickups)
            {
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                ApplyToHierarchy(p.transform, pickupMat);
            }
        }

        // Portal: si hay un WinCollider con el portal activado.
        var wins = FindObjectsByType<WinCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var w in wins)
        {
            if (w == null) continue;
            ApplyToHierarchy(w.transform, portalMat);
        }

        // Enemigo: cualquier EnemyAIBase activo.
        var enemies = FindObjectsByType<EnemyAIBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (e == null) continue;
            ApplyToHierarchy(e.transform, enemyMat);
        }

        active = true;
    }

    void Deactivate()
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            var s = snapshots[i];
            if (s.r != null) s.r.sharedMaterials = s.originals;
        }
        snapshots.Clear();
        active = false;
    }

    void ApplyToHierarchy(Transform root, Material highlightMat)
    {
        if (root == null || highlightMat == null) return;
        var renderers = root.GetComponentsInChildren<Renderer>(false);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            // Saltar particles/trail (no tienen sentido pintarlos de color macizo).
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            var orig = r.sharedMaterials;
            snapshots.Add(new Snapshot { r = r, originals = orig });
            var arr = new Material[orig.Length];
            for (int i = 0; i < arr.Length; i++) arr[i] = highlightMat;
            r.sharedMaterials = arr;
        }
    }
}
