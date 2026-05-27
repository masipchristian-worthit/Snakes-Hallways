using UnityEngine;

/// <summary>
/// Hace que un quad (típicamente planos de fuego) mire siempre a la cámara
/// que esté renderizando en ese momento — Camera.main por defecto, pero
/// también funciona con la spy cam del enemigo cuando se activa, porque
/// se reresuelve cada LateUpdate buscando la cámara activa con mayor depth.
///
/// Antes seguía el transform del player, lo que causaba que con la spy cam
/// los planos miraran "al cuerpo del jugador" en lugar de a la cámara que
/// se está renderizando — el quad parecía aplanado / mal orientado.
/// </summary>
[DisallowMultipleComponent]
public class Billboard : MonoBehaviour
{
    public enum Axis { FullFace, YOnly }

    [Tooltip("FullFace = rota en todos los ejes hacia la cámara. YOnly = solo gira en Y (recomendado para fuego/sprites verticales).")]
    [SerializeField] Axis axis = Axis.YOnly;

    [Tooltip("Si true, mira en sentido inverso (útil si el quad tiene la cara hacia -Z).")]
    [SerializeField] bool flip = true;

    [Tooltip("Opcional: cámara explícita. Si se deja vacío, se usa Camera.main o la cámara activa con mayor depth.")]
    [SerializeField] Camera explicitCamera;

    [Tooltip("Cada cuántos frames re-resolver la cámara activa cuando explicitCamera está vacío. 1 = cada frame (más caro), 5 = cada 5 frames (estable).")]
    [SerializeField] int rebindEveryFrames = 5;

    Camera cachedCamera;
    int frameCounter;

    void LateUpdate()
    {
        Camera cam = ResolveCamera();
        if (cam == null) return;

        Vector3 dir = cam.transform.position - transform.position;
        if (axis == Axis.YOnly) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        if (flip) dir = -dir;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    Camera ResolveCamera()
    {
        // 1) Cámara explícita del inspector si está asignada y activa.
        if (explicitCamera && explicitCamera.isActiveAndEnabled) return explicitCamera;

        // 2) Cámara cacheada si sigue siendo válida y no toca rebindear.
        frameCounter++;
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled && frameCounter < rebindEveryFrames)
            return cachedCamera;
        frameCounter = 0;

        // 3) Autoresolución: Camera.main primero, luego la cámara activa con mayor depth.
        //    Cubre el caso de SpyCamController: cuando la spy cam se enciende, Camera.main
        //    suele apagarse y la spy cam queda como la única activa.
        Camera best = Camera.main;
        if (best == null || !best.isActiveAndEnabled)
        {
            foreach (var c in Camera.allCameras)
            {
                if (!c || !c.isActiveAndEnabled) continue;
                if (best == null || c.depth > best.depth) best = c;
            }
        }
        cachedCamera = best;
        return cachedCamera;
    }
}
