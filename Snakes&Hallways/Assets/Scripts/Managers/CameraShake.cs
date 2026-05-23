using UnityEngine;

/// <summary>
/// Singleton de shake de cámara. Aplica un offset posicional y rotacional
/// (Perlin noise) sobre la cámara que esté ENABLED en cada momento.
///
/// Uso desde código:
///   CameraShake.Shake(sourcePosition, baseIntensity, duration);
///   CameraShake.ShakeUniform(intensity, duration);
///
/// Si no hay instancia en escena, se crea una automáticamente la primera vez.
/// </summary>
[DefaultExecutionOrder(10000)]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Defaults")]
    [Tooltip("Distancia (m) a partir de la cual la intensidad cae a 0.")]
    [SerializeField] float maxDistance = 25f;
    [Tooltip("Distancia (m) por debajo de la cual la intensidad se considera máxima.")]
    [SerializeField] float minDistance = 1.5f;
    [Tooltip("Potencia de la caída con la distancia (1 = lineal, 2 = cuadrática).")]
    [SerializeField] float falloffPower = 2f;

    [Header("Shake parameters")]
    [Tooltip("Multiplicador del offset posicional (metros).")]
    [SerializeField] float positionAmplitude = 0.12f;
    [Tooltip("Multiplicador del offset rotacional (grados).")]
    [SerializeField] float rotationAmplitude = 1.5f;
    [Tooltip("Frecuencia del ruido (Hz).")]
    [SerializeField] float frequency = 22f;

    float trauma;            // 0..1, decae con el tiempo
    float traumaDecay = 1.6f;
    Vector2 noiseSeed;

    Camera trackedCamera;
    Vector3 baseLocalPos;
    Quaternion baseLocalRot;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
        noiseSeed = new Vector2(Random.value * 1000f, Random.value * 1000f);
    }

    static CameraShake EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("CameraShake (auto)");
        Instance = go.AddComponent<CameraShake>();
        return Instance;
    }

    /// <summary>Shake posicional con intensidad calculada por distancia (inversamente proporcional).</summary>
    public static void Shake(Vector3 sourcePosition, float baseIntensity, float duration = 0.25f)
    {
        var cs = EnsureInstance();
        var cam = cs.GetActiveCamera();
        if (!cam) return;
        float dist = Vector3.Distance(sourcePosition, cam.transform.position);
        float t = Mathf.InverseLerp(cs.maxDistance, cs.minDistance, dist); // 0 lejos → 1 cerca
        t = Mathf.Pow(Mathf.Clamp01(t), cs.falloffPower);
        cs.AddTrauma(baseIntensity * t, duration);
    }

    /// <summary>Shake con intensidad fija, sin atenuación por distancia.</summary>
    public static void ShakeUniform(float intensity, float duration = 0.25f)
    {
        var cs = EnsureInstance();
        cs.AddTrauma(intensity, duration);
    }

    void AddTrauma(float amount, float duration)
    {
        if (amount <= 0f) return;
        trauma = Mathf.Clamp01(trauma + amount);
        if (duration > 0f) traumaDecay = 1f / duration;
    }

    Camera GetActiveCamera()
    {
        // Si la cámara cacheada sigue activa, úsala; si no, busca otra entre todas las habilitadas.
        if (trackedCamera && trackedCamera.isActiveAndEnabled) return trackedCamera;

        Camera best = null;
        foreach (var c in Camera.allCameras)
        {
            if (!c || !c.isActiveAndEnabled) continue;
            if (best == null || c.depth > best.depth) best = c;
        }
        if (best != trackedCamera) AdoptCamera(best);
        return trackedCamera;
    }

    void AdoptCamera(Camera cam)
    {
        // Restauramos baseline de la cámara anterior antes de cambiar.
        RestoreBaseline();
        trackedCamera = cam;
        if (trackedCamera)
        {
            baseLocalPos = trackedCamera.transform.localPosition;
            baseLocalRot = trackedCamera.transform.localRotation;
        }
    }

    void RestoreBaseline()
    {
        if (!trackedCamera) return;
        trackedCamera.transform.localPosition = baseLocalPos;
        trackedCamera.transform.localRotation = baseLocalRot;
    }

    void LateUpdate()
    {
        // Validar cámara activa y refrescar baseline si el padre la mueve cada frame
        // (p.ej. el PlayerController coloca la cámara en su sitio antes de LateUpdate del shake).
        var cam = GetActiveCamera();
        if (!cam) return;

        // Capturamos baseline tal y como esté ahora (después de que el resto de scripts la posicione).
        baseLocalPos = cam.transform.localPosition;
        baseLocalRot = cam.transform.localRotation;

        if (trauma <= 0f) return;

        float t = Time.time * frequency;
        float shake = trauma * trauma; // curva cuadrática típica
        float nx = (Mathf.PerlinNoise(noiseSeed.x, t) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(noiseSeed.y, t) - 0.5f) * 2f;
        float nz = (Mathf.PerlinNoise(noiseSeed.x + 13.7f, t) - 0.5f) * 2f;
        float rx = (Mathf.PerlinNoise(noiseSeed.x + 37.1f, t) - 0.5f) * 2f;
        float ry = (Mathf.PerlinNoise(noiseSeed.y + 71.3f, t) - 0.5f) * 2f;
        float rz = (Mathf.PerlinNoise(noiseSeed.x + 91.9f, t) - 0.5f) * 2f;

        Vector3 posOffset = new Vector3(nx, ny, nz) * (positionAmplitude * shake);
        Vector3 rotOffset = new Vector3(rx, ry, rz) * (rotationAmplitude * shake);

        cam.transform.localPosition = baseLocalPos + posOffset;
        cam.transform.localRotation = baseLocalRot * Quaternion.Euler(rotOffset);

        trauma = Mathf.Max(0f, trauma - traumaDecay * Time.deltaTime);
    }
}
