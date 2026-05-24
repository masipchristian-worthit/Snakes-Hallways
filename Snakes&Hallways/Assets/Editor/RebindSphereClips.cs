#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Clona los clips Sphere|* del FBX GuanteBaked re-bindeando sus curvas al path raíz ("")
/// para que un Animator colocado EN el GameObject Sphere.001 los pueda aplicar.
///
/// Uso: Menú "Tools/Player/Rebind Sphere Clips → Root".
/// Luego abre AC_PlayerEye y en cada estado (Draw, Idle, ReverseDraw) reemplaza el Motion
/// por la versión "_Root" generada en Assets/Animations/Player/Generated/.
/// </summary>
public static class RebindSphereClips
{
    const string FbxPath    = "Assets/Models/Characters/GuanteBaked.fbx";
    const string OutputDir  = "Assets/Animations/Player/Generated";
    const string NewPath    = ""; // raíz = mismo GO donde está el Animator (Sphere.001)
    const string Prefix     = "Sphere"; // clip names empiezan con "Sphere|..." o "Sphere_..."

    [MenuItem("Tools/Player/Rebind Sphere Clips → Root")]
    public static void Run()
    {
        if (!File.Exists(FbxPath))
        {
            Debug.LogError($"[RebindSphereClips] No se encuentra el FBX en: {FbxPath}");
            return;
        }
        if (!Directory.Exists(OutputDir))
        {
            Directory.CreateDirectory(OutputDir);
            AssetDatabase.Refresh();
        }

        var assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        var created = new List<string>();
        var skipped = new List<string>();

        foreach (var asset in assets)
        {
            var clip = asset as AnimationClip;
            if (clip == null) continue;
            // El nombre real del clip puede ser "Sphere|DRAW", "Sphere_DRAW", etc. Filtramos por prefijo.
            if (!clip.name.StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase)) continue;

            // Saltar clips de preview internos (terminan en " Avatar" o son __preview__)
            if (clip.name.Contains("__preview__")) { skipped.Add(clip.name); continue; }

            string safeName = clip.name.Replace("|", "_").Replace(":", "_").Replace("/", "_");
            string outPath = $"{OutputDir}/{safeName}_Root.anim";

            var newClip = new AnimationClip
            {
                name      = safeName + "_Root",
                frameRate = clip.frameRate,
                wrapMode  = clip.wrapMode,
                legacy    = clip.legacy,
            };

            // Curvas float (Transform.position, rotation, scale, blendshapes, etc.)
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                var nb = new EditorCurveBinding
                {
                    path         = NewPath,
                    type         = b.type,
                    propertyName = b.propertyName,
                };
                AnimationUtility.SetEditorCurve(newClip, nb, curve);
            }

            // Curvas de referencia (sprites, materiales) por si acaso
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var curve = AnimationUtility.GetObjectReferenceCurve(clip, b);
                var nb = new EditorCurveBinding
                {
                    path         = NewPath,
                    type         = b.type,
                    propertyName = b.propertyName,
                };
                AnimationUtility.SetObjectReferenceCurve(newClip, nb, curve);
            }

            // Conservar settings (loop time, etc.)
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            AnimationUtility.SetAnimationClipSettings(newClip, settings);

            // Borrar si ya existía para regenerar limpio
            if (File.Exists(outPath)) AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(newClip, outPath);
            created.Add(outPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (created.Count == 0)
            Debug.LogWarning($"[RebindSphereClips] No se encontraron clips con prefijo '{Prefix}' en {FbxPath}. ¿Cambió el nombre?");
        else
            Debug.Log($"[RebindSphereClips] {created.Count} clips creados:\n - " + string.Join("\n - ", created)
                      + "\n\nAhora asígnalos en AC_PlayerEye reemplazando los Motion de los estados Draw/Idle/ReverseDraw.");
    }
}
#endif
