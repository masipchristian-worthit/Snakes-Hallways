using UnityEngine;

public class Billboard : MonoBehaviour
{
    public enum Axis { Full, YOnly }

    [SerializeField] private Transform target;
    [SerializeField] private Axis lockAxis = Axis.YOnly;
    [SerializeField] private bool flip180 = true;

    private void LateUpdate()
    {
        if (target == null)
        {
            if (Camera.main != null) target = Camera.main.transform;
            else return;
        }

        Vector3 dir = target.position - transform.position;
        if (lockAxis == Axis.YOnly) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(dir.normalized);
        if (flip180) look *= Quaternion.Euler(0f, 180f, 0f);
        transform.rotation = look;
    }
}
