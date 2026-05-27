using UnityEngine;

/// <summary>
/// Singleton de shake. Aplica un offset posicional y rotacional (Perlin noise) sobre un
/// <c>Transform</c> "anchor" que NADIE MÁS modifique por frame.
///
/// ⚠️ Bug histórico: cuando este script aplicaba el shake directamente sobre
/// <c>Camera.transform.localPosition</c>, el head-bob del PlayerController (que escribe
/// localPosition cada Update con un Lerp hacia su rest pose) "se comía" el offset del shake.
/// Resultado: ningún shake visible aunque los Shake() se llamasen correctamente.
///
/// Fix: aplicamos el offset al <b>camHolder</b> (parent de la cámara) — el camHolder solo
/// recibe rotación de look, nunca posición. Así el bob (en la cámara) y el shake (en su
/// parent) coexisten sin pisarse.
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
    [SerializeField] float maxDistance = 30f;
    [Tooltip("Distancia (m) por debajo de la cual la intensidad se considera máxima.")]
    [SerializeField] float minDistance = 1.5f;
    [Tooltip("Potencia de la caída con la distancia (1 = lineal, 2 = cuadrática). Bajar = el shake se nota más a distancias medias.")]
    [SerializeField] float falloffPower = 1.4f;

    [Header("Tracked anchor (opcional)")]
    [Tooltip("Transform al que se aplica el shake (offset posicional + rotacional). Lo IDEAL es asignar aquí el 'camHolder' del player — un Transform que NO reciba localPosition de ningún otro script. Si se deja vacío, autoresuelve a parent(Camera.main).")]
    [SerializeField] Transform explicitShakeAnchor;

    [Header("Shake parameters")]
    [Tooltip("Multiplicador del offset posicional (metros).")]
    [SerializeField] float positionAmplitude = 0.6f;
    [Tooltip("Multiplicador del offset rotacional (grados).")]
    [SerializeField] float rotationAmplitude = 6f;
    [Tooltip("Frecuencia del ruido (Hz).")]
    [SerializeField] float frequency = 22f;
    [Tooltip("Decaimiento extra del trauma. >0 = el trauma cae más rápido (shake corto). Para shakes de pisadas un valor entre 4 y 8 da un 'thump' breve.")]
    [SerializeField] float traumaFloorDecay = 0f;

    [Header("Debug")]
    [Tooltip("Loguea cada Shake recibido con la intensidad efectiva calculada (útil para diagnosticar pasos invisibles).")]
    [SerializeField] bool debugShake;

    float trauma;            // 0..1, decae con el tiempo
    float traumaDecay = 1.6f;
    Vector2 noiseSeed;

    Transform trackedAnchor;
    Camera referenceCamera;     // Cámara usada SOLO para el cálculo de distancia (no recibe shake)
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

    /// <summary>Shake con intensidad calculada por distancia (inversamente proporcional).</summary>
    public static void Shake(Vector3 sourcePosition, float baseIntensity, float duration = 0.25f)
    {
        var cs = EnsureInstance();
        var refCam = cs.GetReferenceCamera();
        // La distancia se mide a la cámara, no al anchor (la cámara puede no ser hija del anchor).
        Vector3 referencePos = refCam != null ? refCam.transform.position : (cs.trackedAnchor != null ? cs.trackedAnchor.position : cs.transform.position);
        float dist = Vector3.Distance(sourcePosition, referencePos);
        float t = Mathf.InverseLerp(cs.maxDistance, cs.minDistance, dist); // 0 lejos → 1 cerca
        t = Mathf.Pow(Mathf.Clamp01(t), cs.falloffPower);
        float effective = baseIntensity * t;
        if (cs.debugShake) Debug.Log($"[CameraShake] Shake src={sourcePosition} base={baseIntensity:F2} dist={dist:F1} → effective={effective:F3}, anchor={(cs.trackedAnchor != null ? cs.trackedAnchor.name : "<null>")}", cs);
        cs.AddTrauma(effective, duration);
    }

    /// <summary>Shake con intensidad fija, sin atenuación por distancia.</summary>
    public static void ShakeUniform(float intensity, float duration = 0.25f)
    {
        var cs = EnsureInstance();
        if (cs.debugShake) Debug.Log($"[CameraShake] ShakeUniform intensity={intensity:F2}", cs);
        cs.AddTrauma(intensity, duration);
    }

    void AddTrauma(float amount, float duration)
    {
        if (amount <= 0f) return;
        trauma = Mathf.Clamp01(trauma + amount);
        if (duration > 0f) traumaDecay = 1f / duration + traumaFloorDecay;
    }

    Camera GetReferenceCamera()
    {
        if (referenceCamera != null && referenceCamera.isActiveAndEnabled) return referenceCamera;
        referenceCamera = Camera.main;
        if (referenceCamera == null || !referenceCamera.isActiveAndEnabled)
        {
            foreach (var c in Camera.allCameras)
            {
                if (!c || !c.isActiveAndEnabled) continue;
                if (referenceCamera == null || c.depth > referenceCamera.depth) referenceCamera = c;
            }
        }
        return referenceCamera;
    }

    Transform GetActiveAnchor()
    {
        // 1) Usuario asigna explícitamente — confiamos.
        if (explicitShakeAnchor != null && explicitShakeAnchor.gameObject.activeInHierarchy)
        {
            if (trackedAnchor != explicitShakeAnchor) AdoptAnchor(explicitShakeAnchor);
            return trackedAnchor;
        }
        // 2) Cache válido.
        if (trackedAnchor != null && trackedAnchor.gameObject.activeInHierarchy) return trackedAnchor;
        // 3) Autoresolución: parent de Camera.main (típicamente camHolder).
        //    Si la cámara no tiene padre, fallback a la cámara misma — peor caso (revertimos al
        //    comportamiento legacy con potential head-bob conflict).
        var cam = GetReferenceCamera();
        if (cam == null) return null;
        Transform anchor = cam.transform.parent != null ? cam.transform.parent : cam.transform;
        if (anchor != trackedAnchor) AdoptAnchor(anchor);
        return trackedAnchor;
    }

    void AdoptAnchor(Transform t)
    {
        RestoreBaseline();
        trackedAnchor = t;
        if (trackedAnchor != null)
        {
            baseLocalPos = trackedAnchor.localPosition;
            baseLocalRot = trackedAnchor.localRotation;
            lastPosOffset = Vector3.zero;
            lastRotOffset = Quaternion.identity;
        }
    }

    void RestoreBaseline()
    {
        if (trackedAnchor == null) return;
        trackedAnchor.localPosition = baseLocalPos;
        trackedAnchor.localRotation = baseLocalRot;
    }

    void LateUpdate()
    {
        var anchor = GetActiveAnchor();
        if (anchor == null) return;

        // baseline = estado actual del anchor restando el offset que aplicamos el frame anterior.
        // Como NADIE escribe sobre el anchor (camHolder), baseLocalPos será estable.
        baseLocalPos = anchor.localPosition - lastPosOffset;
        baseLocalRot = anchor.localRotation * Quaternion.Inverse(lastRotOffset);

        if (trauma <= 0f)
        {
            anchor.localPosition = baseLocalPos;
            anchor.localRotation = baseLocalRot;
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
        anchor.localPosition = baseLocalPos + posOffset;
        anchor.localRotation = baseLocalRot * rotQ;
        lastPosOffset = posOffset;
        lastRotOffset = rotQ;

        trauma = Mathf.Max(0f, trauma - traumaDecay * Time.deltaTime);
    }
}
