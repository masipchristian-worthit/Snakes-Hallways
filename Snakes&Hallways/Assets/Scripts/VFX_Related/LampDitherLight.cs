using UnityEngine;

/// <summary>
/// Genera una light cookie procedural con dithering / granulado para una <see cref="Light"/>
/// (típicamente la linterna del jugador). Visualmente produce el mismo efecto que un shader
/// de granulado medieval sobre la luz proyectada, pero sin necesidad de tocar shader graph
/// — todo se configura desde el inspector.
///
/// Cómo usar:
///   1. Añade este componente al GameObject de la Light (la linterna).
///   2. Configura los parámetros (resolución, pattern, falloff, tint).
///   3. La cookie se genera en Awake y se asigna automáticamente a la Light.
///   4. Cualquier cambio del inspector en runtime regenera la cookie al instante (OnValidate).
///
/// Compatibilidad URP:
///   - Spot / Directional / Area: usa Light.cookie (Texture2D). ✓
///   - Point: requiere Cubemap, este componente NO lo soporta directamente.
///
/// IMPORTANTE: En el URP Asset (Render Pipeline Asset → Lighting), asegúrate de que
/// "Light Cookies" esté activado, o las cookies no se renderizarán.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Light))]
public class LampDitherLight : MonoBehaviour
{
    public enum DitherPattern
    {
        Bayer,     // Matriz Bayer 8×8 — clásico retro 1-bit
        Noise,     // Hash determinista — granulado orgánico
        Halftone,  // Puntos circulares — look impresión periódico
        FilmGrain  // Random + temporal seed — más caótico
    }

    [Header("Cookie texture")]
    [Tooltip("Resolución de la cookie (px). 256 es un buen punto. Subir si se nota pixelado al apuntar de cerca.")]
    [SerializeField] int resolution = 256;
    [Tooltip("Patrón base del dithering.")]
    [SerializeField] DitherPattern pattern = DitherPattern.Bayer;
    [Tooltip("Escala del patrón (px). Más alto = grano más grueso. 1 = 1 píxel del patrón por píxel de cookie.")]
    [Range(1, 16)][SerializeField] int patternScale = 3;

    [Header("Apariencia")]
    [Tooltip("Color tintado de la cookie (se multiplica sobre el patrón).")]
    [SerializeField] Color tint = new Color(1f, 0.86f, 0.55f, 1f);
    [Tooltip("Fuerza del dithering. 0 = luz uniforme (sin grano). 1 = grano máximo.")]
    [Range(0f, 1f)][SerializeField] float ditherStrength = 0.55f;
    [Tooltip("Falloff radial: cuánto se atenúa el cookie hacia los bordes. 0 = cookie cuadrada (sin atenuación). 1 = cookie circular suave.")]
    [Range(0f, 1f)][SerializeField] float radialFalloff = 0.9f;
    [Tooltip("Suavidad del borde radial. 0 = borde duro. 1 = borde muy suave (gradual).")]
    [Range(0f, 1f)][SerializeField] float edgeSoftness = 0.4f;
    [Tooltip("Brillo mínimo (0..1). Útil para que ningún píxel del cookie sea totalmente negro y la luz se vea apagada en zonas oscuras del patrón.")]
    [Range(0f, 1f)][SerializeField] float floor = 0.15f;

    [Header("Animación (granulado vivo)")]
    [Tooltip("Si está activo, la cookie se regenera cada animateInterval para que el grano 'baile' como un proyector viejo.")]
    [SerializeField] bool animateInRuntime = false;
    [Tooltip("Segundos entre regeneraciones. Bajo = grano frenético. Subir para look más calmado.")]
    [SerializeField] float animateInterval = 0.08f;

    [Header("Filtering")]
    [Tooltip("Filtro de la textura. Point preserva el look pixelado retro. Bilinear suaviza el grano.")]
    [SerializeField] FilterMode filterMode = FilterMode.Point;

    Light lampLight;
    Texture2D cookieTex;
    float animTimer;
    int frameSeed;

    static readonly int[,] BayerMatrix8 = new int[8, 8]
    {
        {  0, 32,  8, 40,  2, 34, 10, 42 },
        { 48, 16, 56, 24, 50, 18, 58, 26 },
        { 12, 44,  4, 36, 14, 46,  6, 38 },
        { 60, 28, 52, 20, 62, 30, 54, 22 },
        {  3, 35, 11, 43,  1, 33,  9, 41 },
        { 51, 19, 59, 27, 49, 17, 57, 25 },
        { 15, 47,  7, 39, 13, 45,  5, 37 },
        { 63, 31, 55, 23, 61, 29, 53, 21 }
    };

    void OnEnable()
    {
        lampLight = GetComponent<Light>();
        GenerateCookie();
    }

    void OnDisable()
    {
        // Limpiar la cookie cuando se desactiva — evita que quede una textura "estática"
        // si el script se quita. Si quieres conservarla, comenta esto.
        if (lampLight != null && lampLight.cookie == cookieTex)
            lampLight.cookie = null;
        DestroyCookie();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        if (lampLight == null) lampLight = GetComponent<Light>();
        // Inspector cambia en runtime → regenerar.
        GenerateCookie();
    }

    void Update()
    {
        if (!animateInRuntime) return;
        animTimer += Time.unscaledDeltaTime;
        if (animTimer >= Mathf.Max(0.01f, animateInterval))
        {
            animTimer = 0f;
            frameSeed++;
            GenerateCookie();
        }
    }

    /// <summary>
    /// Regenera la cookie y la asigna al Light. Llámalo desde código si cambias parámetros
    /// que no disparan OnValidate (raro). También está expuesto como ContextMenu para tirar
    /// "Regenerate Cookie" desde el inspector.
    /// </summary>
    [ContextMenu("Regenerate Cookie")]
    public void GenerateCookie()
    {
        if (lampLight == null) lampLight = GetComponent<Light>();
        if (lampLight == null) return;

        int res = Mathf.Clamp(resolution, 16, 2048);
        if (cookieTex == null || cookieTex.width != res || cookieTex.height != res)
        {
            DestroyCookie();
            cookieTex = new Texture2D(res, res, TextureFormat.RGBA32, false, true);
            cookieTex.wrapMode = TextureWrapMode.Clamp;
            cookieTex.name = "LampDitherCookie";
        }
        cookieTex.filterMode = filterMode;

        Color32[] pixels = new Color32[res * res];
        Vector2 center = new Vector2(res * 0.5f, res * 0.5f);
        float maxDist = res * 0.5f;
        int ps = Mathf.Max(1, patternScale);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                // 1) Valor del patrón en [0..1].
                float v = SamplePattern(x / ps, y / ps);

                // 2) Mezcla con blanco según ditherStrength: más strength → más grano visible.
                v = Mathf.Lerp(1f, v, ditherStrength);

                // 3) Floor: nunca llegamos a cero (controla el "negro" del cookie).
                v = Mathf.Max(floor, v);

                // 4) Radial falloff: atenúa hacia los bordes para una linterna "circular".
                float distNorm = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                // edgeSoftness controla la suavidad de la caída radial.
                float startFade = Mathf.Lerp(1f, 0.4f, edgeSoftness);
                float radialMask = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((distNorm - startFade * (1f - radialFalloff)) / Mathf.Max(0.001f, 1f - startFade * (1f - radialFalloff))));
                v *= Mathf.Lerp(1f, radialMask, radialFalloff);

                // 5) Tint.
                Color c = tint * v;
                c.a = v;
                pixels[y * res + x] = c;
            }
        }

        cookieTex.SetPixels32(pixels);
        cookieTex.Apply(false, false);

        lampLight.cookie = cookieTex;
    }

    float SamplePattern(int x, int y)
    {
        switch (pattern)
        {
            case DitherPattern.Bayer:
                return (BayerMatrix8[y & 7, x & 7] + 0.5f) / 64f;
            case DitherPattern.Noise:
                return Hash01(x, y, frameSeed);
            case DitherPattern.Halftone:
                return Halftone(x, y);
            case DitherPattern.FilmGrain:
                // Combina hash con seed temporal — varía cada regeneración.
                return Hash01(x * 73 + 17, y * 19 + 31, frameSeed);
        }
        return 1f;
    }

    static float Hash01(int x, int y, int seed)
    {
        // Hash determinista, rápido y "barato" — calidad suficiente para grano.
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            h = h ^ (h >> 16);
            return (h & 0xFFFFu) / 65535f;
        }
    }

    float Halftone(int x, int y)
    {
        int s = 8; // tamaño del "punto" del halftone, en unidades del patrón
        int cx = (x % s) - s / 2;
        int cy = (y % s) - s / 2;
        float d = Mathf.Sqrt(cx * cx + cy * cy) / (s * 0.5f);
        // 1 en los bordes, 0 en el centro de cada punto → invertimos para tener puntos brillantes.
        return Mathf.Clamp01(d);
    }

    void DestroyCookie()
    {
        if (cookieTex == null) return;
        if (Application.isPlaying) Destroy(cookieTex);
        else DestroyImmediate(cookieTex);
        cookieTex = null;
    }

#if UNITY_EDITOR
    void Reset()
    {
        // Buenos defaults: spotlight cálido con un toque de grano.
        var l = GetComponent<Light>();
        if (l != null && l.type == LightType.Spot)
        {
            // No tocamos el rango / ángulo — eso lo decide el usuario en el Light component.
        }
    }
#endif
}
