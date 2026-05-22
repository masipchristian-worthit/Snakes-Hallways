using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================
// GrimeTextureGenerator
// Generates a tileable grime/dirt grayscale texture for MainShader's
// _GrimeMap slot. Output: T_GrimeAtlas_01.png at 1024x1024, R8,
// sRGB OFF, Wrap=Repeat.
//
// Implementation: multi-octave tileable value noise (lattice wraps via
// modular indices). Result is biased with a smoothstep so most of the
// surface is clean and only ~30% reads as dirt patches.
//
// Menu: Tools > MainShader > Generate Grime Texture (1024)
// =====================================================================
public static class GrimeTextureGenerator
{
    private const string OutputFolder = "Assets/Shader/MainShader/Noise";
    private const string FileName     = "T_GrimeAtlas_01.png";

    [MenuItem("Tools/MainShader/Generate Grime Texture (1024)")]
    public static void Generate()
    {
        const int   size       = 1024;
        const int   seed       = 9133;
        const int   octaves    = 5;
        const float baseFreq   = 4.0f;        // period across the tile in lattice cells
        const float lacunarity = 2.0f;
        const float gain       = 0.55f;

        // Pre-seed a hash table for deterministic value noise
        Random.State prevState = Random.state;
        Random.InitState(seed);

        // Highest-frequency octave period (must divide size for tileability).
        int maxPeriod = Mathf.RoundToInt(baseFreq * Mathf.Pow(lacunarity, octaves - 1));
        float[,,] octaveValues = new float[octaves, maxPeriod, maxPeriod];
        int[] periods   = new int[octaves];
        float[] amps    = new float[octaves];

        float freq = baseFreq;
        float amp  = 1.0f;
        float totalAmp = 0f;
        for (int o = 0; o < octaves; o++)
        {
            int period = Mathf.Max(1, Mathf.RoundToInt(freq));
            periods[o] = period;
            amps[o]    = amp;
            totalAmp  += amp;
            for (int y = 0; y < period; y++)
            for (int x = 0; x < period; x++)
                octaveValues[o, x, y] = Random.value;

            freq *= lacunarity;
            amp  *= gain;
        }
        Random.state = prevState;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (float)x / size;
            float v = (float)y / size;

            float sum = 0f;
            for (int o = 0; o < octaves; o++)
            {
                int p = periods[o];
                float fx = u * p;
                float fy = v * p;
                int x0 = Mathf.FloorToInt(fx);
                int y0 = Mathf.FloorToInt(fy);
                float tx = fx - x0;
                float ty = fy - y0;
                int wx0 = ((x0 % p) + p) % p;
                int wy0 = ((y0 % p) + p) % p;
                int wx1 = (wx0 + 1) % p;
                int wy1 = (wy0 + 1) % p;

                float v00 = octaveValues[o, wx0, wy0];
                float v10 = octaveValues[o, wx1, wy0];
                float v01 = octaveValues[o, wx0, wy1];
                float v11 = octaveValues[o, wx1, wy1];

                float sx = tx * tx * (3f - 2f * tx);
                float sy = ty * ty * (3f - 2f * ty);
                float ix0 = Mathf.Lerp(v00, v10, sx);
                float ix1 = Mathf.Lerp(v01, v11, sx);
                float val = Mathf.Lerp(ix0, ix1, sy);

                sum += val * amps[o];
            }

            float n = sum / totalAmp;
            // Bias toward dark with bright patches => grime look.
            // smoothstep(0.45, 0.85) keeps clean surfaces dark, lets stains pop.
            n = Mathf.Clamp01((n - 0.45f) / 0.40f);
            n = n * n * (3f - 2f * n);                  // extra smoothstep
            n = Mathf.Pow(n, 1.3f);                     // contrast bump

            pixels[y * size + x] = new Color(n, n, n, 1f);
        }

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

        Debug.Log($"[GrimeTextureGenerator] Wrote {fullPath} ({size}x{size}, tileable, sRGB off, Wrap Repeat).");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath));
    }

    private static void ApplyImportSettings(string assetPath)
    {
        TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti == null) return;

        ti.textureType         = TextureImporterType.Default;
        ti.sRGBTexture         = false;
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
