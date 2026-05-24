using UnityEngine;

/// <summary>
/// Coloca esto en el enemigo (mismo GameObject que EnemyAIBase / EnemyDetection).
/// Crea una Camera hija si no se asigna manualmente y la expone para que SpyCamController la encienda/apague.
/// El GameObject de la cámara puede estar apagado en escena (lo gestionamos aquí).
/// Si la cámara lleva AudioListener, también se enciende/apaga junto con la cámara.
/// </summary>
[DisallowMultipleComponent]
public class EnemyCameraView : MonoBehaviour
{
    [Header("Cámara")]
    [Tooltip("Si se deja vacía se autogenera una cámara hija en Awake.")]
    [SerializeField] Camera spyCamera;

    [Header("Auto-create defaults")]
    [SerializeField] Vector3 cameraLocalOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] float fieldOfView = 75f;
    [SerializeField] float nearPlane = 0.1f;
    [SerializeField] float farPlane = 80f;
    [Tooltip("Anchor opcional: si lo asignas (p.ej. cabeza del enemigo), la cámara se anida ahí.")]
    [SerializeField] Transform headAnchor;

    [Header("Audio")]
    [Tooltip("Si está activo, se añade/usa un AudioListener en la cámara espía y se enciende/apaga con ella.")]
    [SerializeField] bool manageAudioListener = true;

    AudioListener spyListener;

    public Camera SpyCamera => spyCamera;

    void Awake()
    {
        EnsureCamera();
        SetActive(false);
    }

    void EnsureCamera()
    {
        if (spyCamera == null)
        {
            var parent = headAnchor != null ? headAnchor : transform;
            var go = new GameObject("EnemySpyCamera");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = cameraLocalOffset;
            go.transform.localRotation = Quaternion.identity;

            spyCamera = go.AddComponent<Camera>();
            spyCamera.fieldOfView = fieldOfView;
            spyCamera.nearClipPlane = nearPlane;
            spyCamera.farClipPlane = farPlane;
            spyCamera.tag = "Untagged";
        }

        if (manageAudioListener)
        {
            spyListener = spyCamera.GetComponent<AudioListener>();
            if (!spyListener) spyListener = spyCamera.gameObject.AddComponent<AudioListener>();
        }
        else
        {
            spyListener = spyCamera.GetComponent<AudioListener>(); // úsalo si existe pero no lo creamos
        }
    }

    /// <summary>
    /// Activa/desactiva la cámara espía. Maneja:
    ///   - GameObject (puede estar apagado en escena).
    ///   - Componente Camera.
    ///   - AudioListener si existe.
    /// </summary>
    public void SetActive(bool active)
    {
        if (spyCamera == null) return;
        if (spyCamera.gameObject.activeSelf != active)
            spyCamera.gameObject.SetActive(active);
        spyCamera.enabled = active;
        if (spyListener) spyListener.enabled = active;
    }
}
