using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================
// WorleyNoiseGenerator
// Generates a tileable Worley/Voronoi grayscale texture for MainShader's
// _NoiseTexture slot. Output: T_NoiseTile_Worley_01.png at 512x512, R8,
// sRGB OFF, Wrap=Repeat. Tileable by construction (lattice wraps with
// proper world-space offset, so distances are correct across edges).
//
// Menu: Tools > MainShader > Generate Worley Noise (512)
// =====================================================================
public static class WorleyNoiseGenerator
{
    private const string OutputFolder = "Assets/Shader/MainShader/Noise";
    private const string FileName     = "T_NoiseTile_Worley_01.png";

    [MenuItem("Tools/MainShader/Generate Worley Noise (512)")]
    public static void Generate()
    {
        const int size  = 512;
        const int cells = 12;          // 12x12 feature points across the tile
        const int seed  = 1337;

        // ---- 1. Place one feature point per cell (deterministic via seed) ----
        Random.State prevState = Random.state;
        Random.InitState(seed);

        float cellSize = (float)size / cells;
        Vector2[,] points = new Vector2[cells, cells];
        for (int cy = 0; cy < cells; cy++)
        for (int cx = 0; cx < cells; cx++)
        {
            points[cx, cy] = new Vector2(
                cx * cellSize + Random.value * cellSize,
                cy * cellSize + Random.value * cellSize);
        }
        Random.state = prevState;

        // ---- 2. For each pixel, distance to nearest feature point (with wrap) ----
        Color[] pixels = new Color[size * size];
        float maxDist = cellSize * 1.7f;   // approx upper bound for F1 in a grid

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int cx = Mathf.Min((int)(x / cellSize), cells - 1);
            int cy = Mathf.Min((int)(y / cellSize), cells - 1);

            float minD = float.MaxValue;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int rcx = cx + ox;
                int rcy = cy + oy;
                int wcx = ((rcx % cells) + cells) % cells;
                int wcy = ((rcy % cells) + cells) % cells;
                Vector2 p = points[wcx, wcy];
                // Offset to account for the wrap so the distance is geometric.
                p.x += (rcx - wcx) * cellSize;
                p.y += (rcy - wcy) * cellSize;

                float dx = x - p.x;
                float dy = y - p.y;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < minD) minD = d;
            }

            float v = Mathf.Clamp01(minD / maxDist);
            // Slight contrast bump so cells read clearly.
            v = Mathf.SmoothStep(0.0f, 1.0f, v);
            pixels[y * size + x] = new Color(v, v, v, 1f);
        }

        // ---- 3. Encode and write ----
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.SetPixels(pixels);
        tex.Apply(false, false);

        if (!Directory.Exists(OutputFolder))
            Directory.CreateDirectory(OutputFolder);

        string fullPath = Path.Combine(OutputFolder, FileName);
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(fullPath, ImportAssetOptions.ForceUpdate);
        ApplyImportSettings(fullPath);

        Debug.Log($"[WorleyNoiseGenerator] Wrote {fullPath} ({size}x{size}, tileable, sRGB off, Wrap Repeat).");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath));
    }

    private static void ApplyImportSettings(string assetPath)
    {
        TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti == null) return;

        ti.textureType         = TextureImporterType.Default;
        ti.sRGBTexture         = false;        // it's a mask, not color
        ti.alphaSource         = TextureImporterAlphaSource.None;
        ti.alphaIsTransparency = false;
        ti.wrapMode            = TextureWrapMode.Repeat;
        ti.filterMode          = FilterMode.Bilinear;
        ti.mipmapEnabled       = true;
        ti.isReadable          = false;

        TextureImporterPlatformSettings ps = ti.GetDefaultPlatformTextureSettings();
        ps.format                = TextureImporterFormat.Automatic;
        ps.textureCompression    = TextureImporterCompression.CompressedLQ;
        ti.SetPlatformTextureSettings(ps);

        ti.SaveAndReimport();
    }
}
