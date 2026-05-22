using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// =====================================================================
// CreateHorrorVolume
// Builds a URP Volume Profile asset configured for medieval horror mood
// (warm tonemap, subtle bloom, vignette, slight desaturation + warmth)
// and spawns a Global Volume GameObject in the current scene wired to it.
//
// Menu: Tools > MainShader > Create Horror Global Volume
// =====================================================================
public static class CreateHorrorVolume
{
    private const string ProfileFolder = "Assets/Shader/MainShader/Volume";
    private const string ProfileName   = "VP_Horror_Default.asset";
    private const string GoName        = "GlobalVolume_Horror";

    [MenuItem("Tools/MainShader/Create Horror Global Volume")]
    public static void Create()
    {
        // ---- 1. Ensure folder ----
        if (!Directory.Exists(ProfileFolder))
            Directory.CreateDirectory(ProfileFolder);
        string profilePath = Path.Combine(ProfileFolder, ProfileName);

        // ---- 2. Create or load the profile ----
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        bool created = false;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            created = true;
        }
        else
        {
            // Clear existing components so we get a clean profile
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(profile.components[i], true);
            }
            profile.components.Clear();
        }

        // ---- 3. Configure horror components ----
        var tonemap = profile.Add<Tonemapping>(true);
        tonemap.mode.overrideState = true;
        tonemap.mode.value         = TonemappingMode.Neutral;

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.overrideState = true;  bloom.threshold.value = 0.9f;
        bloom.intensity.overrideState = true;  bloom.intensity.value = 0.35f;
        bloom.scatter.overrideState   = true;  bloom.scatter.value   = 0.75f;
        bloom.tint.overrideState      = true;  bloom.tint.value      = new Color(1f, 0.86f, 0.70f);

        var vignette = profile.Add<Vignette>(true);
        vignette.color.overrideState     = true; vignette.color.value     = Color.black;
        vignette.intensity.overrideState = true; vignette.intensity.value = 0.35f;
        vignette.smoothness.overrideState= true; vignette.smoothness.value= 0.42f;
        vignette.rounded.overrideState   = true; vignette.rounded.value   = false;

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.overrideState = true; color.postExposure.value = 0.0f;
        color.contrast.overrideState     = true; color.contrast.value     = 12f;
        color.colorFilter.overrideState  = true; color.colorFilter.value  = new Color(1f, 0.96f, 0.88f);
        color.hueShift.overrideState     = true; color.hueShift.value     = 0f;
        color.saturation.overrideState   = true; color.saturation.value   = -8f;

        var wb = profile.Add<WhiteBalance>(true);
        wb.temperature.overrideState = true; wb.temperature.value = 8f;
        wb.tint.overrideState        = true; wb.tint.value        = -4f;

        var smh = profile.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.overrideState    = true; smh.shadows.value    = new Vector4(0.90f, 0.85f, 0.78f, 0f);
        smh.midtones.overrideState   = true; smh.midtones.value   = new Vector4(1.00f, 0.98f, 0.94f, 0f);
        smh.highlights.overrideState = true; smh.highlights.value = new Vector4(1.00f, 0.97f, 0.92f, 0f);

        var grain = profile.Add<FilmGrain>(true);
        grain.type.overrideState      = true; grain.type.value      = FilmGrainLookup.Thin1;
        grain.intensity.overrideState = true; grain.intensity.value = 0.18f;
        grain.response.overrideState  = true; grain.response.value  = 0.75f;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ---- 4. Spawn / update the Global Volume GameObject in the scene ----
        GameObject go = GameObject.Find(GoName);
        if (go == null)
        {
            go = new GameObject(GoName);
            Undo.RegisterCreatedObjectUndo(go, "Create Horror Global Volume");
        }

        Volume vol = go.GetComponent<Volume>();
        if (vol == null) vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 0f;
        vol.weight   = 1f;
        vol.profile  = profile;

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(profile);

        string msg = (created ? "Created" : "Updated") +
                     $" {profilePath}\nAttached to GameObject '{GoName}'.\n" +
                     "Don't forget to enable Post Processing on the Camera (URP > Camera > Rendering > Post Processing)\n" +
                     "and in the URP Renderer Asset (Post-processing tickbox).";
        Debug.Log("[CreateHorrorVolume] " + msg);
        EditorUtility.DisplayDialog("Horror Volume ready", msg, "OK");
    }
}
