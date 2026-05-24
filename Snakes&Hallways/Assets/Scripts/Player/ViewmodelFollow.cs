using UnityEngine;

/// <summary>
/// Mantiene el view-model (GuanteBaked) a una distancia/orientación fija respecto a la cámara,
/// con suavizado exponencial frame-rate-independent para que el seguimiento sea fluido
/// y no de tirones cuando el ratón mueve la cámara rápido.
///
/// Setup recomendado:
///   1. Colócalo en el GameObject 'GuanteBaked'.
///   2. Asigna 'Target Camera' = transform de Main Camera.
///   3. Coloca el GuanteBaked en pantalla manualmente donde quieras que se vea.
///   4. Activa 'Capture Offset From Current' en el Inspector → al salir del checkbox
///      se rellena 'Position Offset' y 'Rotation Offset Euler' con la posición actual
///      relativa a la cámara. Ya no toques esos campos.
///   5. Ejecuta el juego: el GuanteBaked se quedará pegado a esa posición/rotación
///      relativas a la cámara, suavizando la rotación según los Follow Speed.
///
/// Notas:
///   • Corre en LateUpdate y con DefaultExecutionOrder alto para escribir DESPUÉS del
///     CameraLook del PlayerController (que vive en LateUpdate también).
///   • No interfiere con ArmSway (que modifica armRoot, un hijo del GuanteBaked).
/// </summary>
[DefaultExecutionOrder(500)]
public class ViewmodelFollow : MonoBehaviour
{
    [Header("Target Camera")]
    [Tooltip("Transform de la cámara a la que se ancla. Si se deja vacío, se usa Camera.main.")]
    [SerializeField] Transform targetCamera;

    [Header("Offset (en espacio local de la cámara)")]
    [Tooltip("Posición del view-model relativa a la cámara. Usa 'Capture Offset From Current' para autollenarlo.")]
    [SerializeField] Vector3 positionOffset = new Vector3(0.309f, 0.2f, 1.043f);
    [Tooltip("Rotación del view-model relativa a la cámara, en euler.")]
    [SerializeField] Vector3 rotationOffsetEuler = Vector3.zero;

    [Header("Smoothing (mayor = menos lag, más responsivo)")]
    [Tooltip("Velocidad de seguimiento posicional. 0 = no se mueve, 999 = instantáneo. Recomendado 25-40 para sentirse 'fijo'.")]
    [Range(0f, 999f)]
    [SerializeField] float positionFollowSpeed = 40f;
    [Tooltip("Velocidad de seguimiento rotacional. Bajar (8-15) para un 'sway' más perezoso y cinematográfico; subir (25-40) para responder pegado.")]
    [Range(0f, 999f)]
    [SerializeField] float rotationFollowSpeed = 22f;

    [Header("Captura del offset actual (Edit Mode)")]
    [Tooltip("Marca esta casilla para auto-rellenar Position/Rotation Offset con la pose actual del GuanteBaked respecto a la cámara. Se desmarcará sola al aplicarse.")]
    [SerializeField] bool captureOffsetFromCurrent = false;

    Vector3 currentPos;
    Quaternion currentRot;

    void Awake()
    {
        if (targetCamera == null && Camera.main != null) targetCamera = Camera.main.transform;
        if (targetCamera == null)
        {
            Debug.LogWarning("ViewmodelFollow: no se encontró Target Camera ni Camera.main. El view-model no seguirá nada.", this);
            enabled = false;
            return;
        }
        currentPos = transform.position;
        currentRot = transform.rotation;
    }

    void OnValidate()
    {
        if (captureOffsetFromCurrent)
        {
            captureOffsetFromCurrent = false;
            var cam = targetCamera != null ? targetCamera : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null)
            {
                Debug.LogWarning("ViewmodelFollow: no hay Target Camera asignada y Camera.main es null en Edit Mode. Asigna la cámara primero.", this);
                return;
            }
            positionOffset = cam.InverseTransformPoint(transform.position);
            rotationOffsetEuler = (Quaternion.Inverse(cam.rotation) * transform.rotation).eulerAngles;
        }
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 targetPos = targetCamera.TransformPoint(positionOffset);
        Quaternion targetRot = targetCamera.rotation * Quaternion.Euler(rotationOffsetEuler);

        // Suavizado exponencial frame-rate-independent: t = 1 - e^(-k * dt)
        float dt = Time.deltaTime;
        float posT = positionFollowSpeed >= 998f ? 1f : 1f - Mathf.Exp(-positionFollowSpeed * dt);
        float rotT = rotationFollowSpeed >= 998f ? 1f : 1f - Mathf.Exp(-rotationFollowSpeed * dt);

        currentPos = Vector3.Lerp(currentPos, targetPos, posT);
        currentRot = Quaternion.Slerp(currentRot, targetRot, rotT);

        transform.SetPositionAndRotation(currentPos, currentRot);
    }
}
