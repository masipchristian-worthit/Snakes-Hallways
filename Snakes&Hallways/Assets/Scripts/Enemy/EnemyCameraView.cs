using UnityEngine;

/// <summary>
/// Coloca esto en el enemigo (mismo GameObject que EnemyAIBase / EnemyDetection).
/// Crea una Camera hija si no se asigna manualmente, posicionada como "ojo" del enemigo,
/// y la expone para que SpyCamController la encienda/apague.
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

    public Camera SpyCamera => spyCamera;

    void Awake()
    {
        EnsureCamera();
        SetActive(false);
    }

    void EnsureCamera()
    {
        if (spyCamera != null) return;

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
        spyCamera.enabled = false;
    }

    public void SetActive(bool active)
    {
        if (spyCamera == null) return;
        spyCamera.enabled = active;
    }
}
