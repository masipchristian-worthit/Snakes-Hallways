using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Singleton de shake. <b>V4</b> — la versión que por fin funciona.
///
/// Historia del fracaso de las versiones anteriores:
///   - V1: shake sobre Camera.localPosition → el HeadBob del PlayerController lo borraba.
///   - V2: shake sobre parent(Camera) = camHolder → el CameraLook escribe
///         camHolder.localEulerAngles cada LateUpdate y se cargaba la rotación del shake.
///   - V3 con DOTween multi-cam: mismo problema — los scripts del player escriben sobre
///         los Transforms que el shake intenta animar.
///
/// V4 (esta): para cada cámara, insertamos en RUNTIME un GameObject intermedio entre la
/// cámara y su padre original ("_ShakeAnchor"). La cámara se reparente con
/// <c>worldPositionStays=false</c> — su localPosition NO cambia, así HeadBob sigue funcionando.
/// El _ShakeAnchor queda con localPosition=0 y localRotation=identity y nadie más lo toca,
/// así DOShakePosition y DOShakeRotation se aplican LIMPIOS.
///
/// Uso (sin cambios):
///   CameraShake.Shake(sourcePosition, baseIntensity, duration);
///   CameraShake.ShakeUniform(intensity, duration);
/// </summary>
[DefaultExecutionOrder(10000)]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Atenuación por distancia")]
    [Tooltip("Distancia (m) a partir de la cual la intensidad cae a 0.")]
    [SerializeField] float maxDistance = 30f;
    [Tooltip("Distancia (m) por debajo de la cual la intensidad es máxima.")]
    [SerializeField] float minDistance = 1.5f;
    [Tooltip("Curva de caída. 1 = lineal, 2 = cuadrática.")]
    [SerializeField] float falloffPower = 1.4f;

    [Header("Cámaras objetivo")]
    [Tooltip("Si está activo, se descubren TODAS las cámaras activas (Main + ViewModel + cualquiera) y a cada una se le inserta un _ShakeAnchor intermedio para recibir el shake.")]
    [SerializeField] bool autoDiscoverCameras = true;
    [Tooltip("Re-escanea cámaras cada N segundos. Necesario para detectar cámaras que aparecen tarde (spy cam que se crea en runtime, viewmodel que se activa al sacar la mano, etc.).")]
    [SerializeField] float rediscoverInterval = 0.4f;

    [Header("Shake (DOTween)")]
    [Tooltip("Multiplicador del shake posicional (m).")]
    [SerializeField] float positionAmplitude = 0.4f;
    [Tooltip("Multiplicador del shake rotacional (°).")]
    [SerializeField] float rotationAmplitude = 9f;
    [Tooltip("Vibrato — número de 'sacudidas' por shake. 10-18 funciona para pasos/golpes.")]
    [SerializeField] int vibrato = 14;
    [Tooltip("Varianza angular de las sacudidas (0..180).")]
    [Range(0f, 180f)][SerializeField] float randomness = 90f;
    [Tooltip("Fade-out durante el shake: la sacudida pierde fuerza al final.")]
    [SerializeField] bool fadeOut = true;

    [Header("Debug")]
    [Tooltip("Loguea cada shake con intensidad efectiva y nº de anchors. Si esto loguea pero no ves shake, hay otro script escribiendo sobre el _ShakeAnchor.")]
    [SerializeField] bool debugShake;

    class AnchorState
    {
        public Transform anchor;              // _ShakeAnchor GameObject (insertado por nosotros)
        public Transform originalParent;      // padre original de la cámara antes del reparenting
        public Transform cameraTransform;     // referencia a la cámara reparentada
        public Tweener posTween;
        public Tweener rotTween;
    }

    // Map: Camera transform → estado. Una entrada por cada cámara descubierta.
    readonly Dictionary<Transform, AnchorState> states = new();
    float rediscoverTimer;
    Camera referenceCamera;

    const string AnchorNamePrefix = "_ShakeAnchor";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
        RefreshAnchors();
    }

    static CameraShake EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("CameraShake (auto)");
        Instance = go.AddComponent<CameraShake>();
        return Instance;
    }

    // ── API pública ─────────────────────────────────────────────────────────

    /// <summary>Shake con intensidad atenuada por distancia entre sourcePosition y la cámara de referencia.</summary>
    public static void Shake(Vector3 sourcePosition, float baseIntensity, float duration = 0.25f)
    {
        var cs = EnsureInstance();
        var refCam = cs.GetReferenceCamera();
        Vector3 referencePos = refCam != null ? refCam.transform.position : cs.transform.position;
        float dist = Vector3.Distance(sourcePosition, referencePos);
        float t = Mathf.InverseLerp(cs.maxDistance, cs.minDistance, dist);
        t = Mathf.Pow(Mathf.Clamp01(t), cs.falloffPower);
        float effective = baseIntensity * t;
        if (cs.debugShake)
            Debug.Log($"[CameraShake] Shake src={sourcePosition} base={baseIntensity:F2} dist={dist:F1} → eff={effective:F3} anchors={cs.states.Count}", cs);
        cs.ApplyShake(effective, duration);
    }

    /// <summary>Shake uniforme — sin atenuación por distancia. Ideal para "te golpean": el player no debe percibir distancia.</summary>
    public static void ShakeUniform(float intensity, float duration = 0.25f)
    {
        var cs = EnsureInstance();
        if (cs.debugShake)
            Debug.Log($"[CameraShake] ShakeUniform intensity={intensity:F2} anchors={cs.states.Count}", cs);
        cs.ApplyShake(intensity, duration);
    }

    // ── Implementación ──────────────────────────────────────────────────────

    void ApplyShake(float intensity, float duration)
    {
        if (intensity <= 0.001f || duration <= 0f) return;
        RefreshAnchors(); // por si alguna cámara apareció entre tics

        Vector3 posStrength = new Vector3(intensity, intensity * 0.6f, intensity) * positionAmplitude;
        Vector3 rotStrength = Vector3.one * intensity * rotationAmplitude;

        foreach (var kvp in states)
        {
            var st = kvp.Value;
            if (st.anchor == null) continue;
            // Matar tweens previos del anchor para evitar acumulación.
            if (st.posTween != null && st.posTween.IsActive()) st.posTween.Kill(complete: false);
            if (st.rotTween != null && st.rotTween.IsActive()) st.rotTween.Kill(complete: false);
            // Reset baseline del anchor — siempre arranca desde (0,0,0)/identity (nadie lo toca).
            st.anchor.localPosition = Vector3.zero;
            st.anchor.localRotation = Quaternion.identity;

            // DOShakePosition / DOShakeRotation: oscilación con fade-out. Auto-restauran al terminar.
            st.posTween = st.anchor.DOShakePosition(duration, posStrength, vibrato, randomness, snapping: false, fadeOut: fadeOut);
            st.rotTween = st.anchor.DOShakeRotation(duration, rotStrength, vibrato, randomness, fadeOut: fadeOut);
        }
    }

    void Update()
    {
        if (!autoDiscoverCameras) return;
        rediscoverTimer -= Time.unscaledDeltaTime;
        if (rediscoverTimer <= 0f)
        {
            rediscoverTimer = Mathf.Max(0.1f, rediscoverInterval);
            RefreshAnchors();
        }
    }

    /// <summary>
    /// Inserta un _ShakeAnchor entre cada cámara activa y su padre original. La cámara se
    /// reparenta con <c>worldPositionStays=false</c> → su localPosition NO cambia, así HeadBob
    /// (que lerpa cameraTransform.localPosition) sigue funcionando idéntico que antes.
    /// </summary>
    void RefreshAnchors()
    {
        if (!autoDiscoverCameras) return;

        var seenCams = new HashSet<Transform>();
        foreach (var c in Camera.allCameras)
        {
            if (c == null || !c.isActiveAndEnabled) continue;
            var camT = c.transform;
            seenCams.Add(camT);

            // Si la cámara ya es hija de un _ShakeAnchor que conocemos → nada que hacer.
            if (states.ContainsKey(camT)) continue;

            // Si la cámara YA está dentro de un _ShakeAnchor (de una instancia previa nuestra,
            // p.ej. tras un domain reload), reutilizarlo en vez de crear uno nuevo.
            Transform parent = camT.parent;
            if (parent != null && parent.name.StartsWith(AnchorNamePrefix))
            {
                states[camT] = new AnchorState
                {
                    anchor = parent,
                    originalParent = parent.parent,
                    cameraTransform = camT,
                };
                continue;
            }

            // Crear el anchor intermedio.
            var anchorGo = new GameObject(AnchorNamePrefix + "_" + c.name);
            anchorGo.transform.SetParent(parent, false); // anchor hijo del antiguo padre, en su origen
            anchorGo.transform.localPosition = Vector3.zero;
            anchorGo.transform.localRotation = Quaternion.identity;
            anchorGo.transform.localScale = Vector3.one;

            // Reparentar la cámara dentro del anchor. worldPositionStays=false → mantiene su
            // localPosition/Rotation (que era relativa al anterior padre, y ahora será relativa
            // al anchor que está en el mismo sitio que el anterior padre). Net: la cámara no
            // se mueve visualmente, pero ahora hay un nivel intermedio donde aplicar el shake.
            camT.SetParent(anchorGo.transform, worldPositionStays: false);

            states[camT] = new AnchorState
            {
                anchor = anchorGo.transform,
                originalParent = parent,
                cameraTransform = camT,
            };
        }

        // Limpieza: cámaras que ya no están activas → quitar el anchor (la cámara queda donde estaba).
        if (states.Count == 0) return;
        List<Transform> toRemove = null;
        foreach (var kvp in states)
        {
            if (!seenCams.Contains(kvp.Key))
            {
                toRemove ??= new List<Transform>();
                toRemove.Add(kvp.Key);
            }
        }
        if (toRemove != null)
            foreach (var k in toRemove) DisposeAnchor(k);
    }

    /// <summary>
    /// Vuelve a poner la cámara como hija de su padre original y destruye el _ShakeAnchor.
    /// Se llama al desactivarse una cámara o al destruir el CameraShake.
    /// </summary>
    void DisposeAnchor(Transform camT)
    {
        if (!states.TryGetValue(camT, out var st)) return;
        states.Remove(camT);

        if (st.posTween != null && st.posTween.IsActive()) st.posTween.Kill(complete: false);
        if (st.rotTween != null && st.rotTween.IsActive()) st.rotTween.Kill(complete: false);

        if (camT != null && st.anchor != null)
        {
            // Reset del anchor antes de devolver la cámara — para que su pose sea limpia.
            st.anchor.localPosition = Vector3.zero;
            st.anchor.localRotation = Quaternion.identity;
            // Devuelve la cámara al padre original manteniendo su localPos/Rot relativo al anchor
            // (anchor estaba en 0/identity → la cámara seguía en su localPos antiguo).
            if (st.originalParent != null)
                camT.SetParent(st.originalParent, worldPositionStays: false);
            // Destruye el anchor.
            if (Application.isPlaying) Destroy(st.anchor.gameObject);
            else DestroyImmediate(st.anchor.gameObject);
        }
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

    void OnDestroy()
    {
        // Limpieza completa: devolver cámaras a sus padres originales.
        var keys = new List<Transform>(states.Keys);
        foreach (var k in keys) DisposeAnchor(k);
    }
}
