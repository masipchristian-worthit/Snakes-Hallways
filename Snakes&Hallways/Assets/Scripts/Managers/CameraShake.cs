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

    [Header("Tracked camera (opcional)")]
    [Tooltip("Cámara que recibirá el shake. Si se deja vacío se autoresuelve a Camera.main / Camera más activa con mayor depth. Asígnala explícitamente si tu cámara no es la 'main'.")]
    [SerializeField] Camera explicitCamera;

    [Header("Shake parameters")]
    [Tooltip("Multiplicador del offset posicional (metros). Subido: la antigua 0.12 con trauma^2 daba <3mm en pisadas.")]
    [SerializeField] float positionAmplitude = 0.45f;
    [Tooltip("Multiplicador del offset rotacional (grados).")]
    [SerializeField] float rotationAmplitude = 5f;
    [Tooltip("Frecuencia del ruido (Hz).")]
    [SerializeField] float frequency = 22f;

    float trauma;            // 0..1, decae con el tiempo
    float traumaDecay = 1.6f;
    Vector2 noiseSeed;

    Camera trackedCamera;
    Vector3 baseLocalPos;
    Quaternion baseLocalRot;
    Vector3 lastPosOffset;
    Quaternion lastRotOffset = Quaternion.identity;

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
        // 1) Si el usuario ha asignado una cámara explícita en el inspector, esa manda.
        if (explicitCamera && explicitCamera.isActiveAndEnabled)
        {
            if (trackedCamera != explicitCamera) AdoptCamera(explicitCamera);
            return trackedCamera;
        }
        // 2) Cámara cacheada si sigue válida.
        if (trackedCamera && trackedCamera.isActiveAndEnabled) return trackedCamera;
        // 3) Autoresolución: Camera.main primero, luego la mayor 'depth' activa.
        Camera best = Camera.main;
        if (best == null || !best.isActiveAndEnabled)
        {
            foreach (var c in Camera.allCameras)
            {
                if (!c || !c.isActiveAndEnabled) continue;
                if (best == null || c.depth > best.depth) best = c;
            }
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

        // Capturamos baseline RESTANDO el offset que aplicamos el frame anterior, para que
        // el shake no se absorba en la baseline y derive. Si el PlayerController re-posicionó
        // la cámara este frame antes de nosotros, el offset previo ya no estaría aplicado
        // pero restarlo es seguro (compensa después al sumar el nuevo offset).
        baseLocalPos = cam.transform.localPosition - lastPosOffset;
        baseLocalRot = cam.transform.localRotation * Quaternion.Inverse(lastRotOffset);

        if (trauma <= 0f)
        {
            // Sin trauma: dejamos la cámara en su baseline limpia y limpiamos offsets.
            cam.transform.localPosition = baseLocalPos;
            cam.transform.localRotation = baseLocalRot;
            lastPosOffset = Vector3.zero;
            lastRotOffset = Quaternion.identity;
            return;
        }

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

        Quaternion rotQ = Quaternion.Euler(rotOffset);
        cam.transform.localPosition = baseLocalPos + posOffset;
        cam.transform.localRotation = baseLocalRot * rotQ;
        lastPosOffset = posOffset;
        lastRotOffset = rotQ;

        trauma = Mathf.Max(0f, trauma - traumaDecay * Time.deltaTime);
    }
}
