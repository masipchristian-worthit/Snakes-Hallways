using UnityEngine;

[DisallowMultipleComponent]
public class Billboard : MonoBehaviour
{
    public enum Axis { FullFace, YOnly }

    [Tooltip("Si está vacío, busca el objeto con tag 'Player' al iniciar.")]
    [SerializeField] Transform target;

    [Tooltip("FullFace = rota en todos los ejes hacia el player. YOnly = solo gira en Y (recomendado para fuego/sprites verticales).")]
    [SerializeField] Axis axis = Axis.YOnly;

    [Tooltip("Si true, mira en sentido inverso (útil si el quad tiene la cara hacia -Z).")]
    [SerializeField] bool flip = true;

    void Start()
    {
        if (target == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.Player != null)
                target = GameManager.Instance.Player;
            else
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) target = p.transform;
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 dir = target.position - transform.position;
        if (axis == Axis.YOnly) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        if (flip) dir = -dir;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}
