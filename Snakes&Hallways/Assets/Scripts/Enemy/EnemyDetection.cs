using UnityEngine;

public enum DetectionVisibility { None, Backside, Frontside }

/// <summary>
/// Computes a 0..1 detection score for the player given player state, lamp, sound, view-cone.
/// Also a static noise broadcast channel so PlayerController and others can ping noises.
/// </summary>
[RequireComponent(typeof(EnemyAIBase))]
public class EnemyDetection : MonoBehaviour
{
    public static System.Action<Vector3, float> NoiseHeard;

    [Header("Vision")]
    [Tooltip("Transform desde el que parte el cono de visión. Si lo dejas vacío se crea un hijo 'Eye' automáticamente para que puedas moverlo y rotarlo en la escena.")]
    [SerializeField] Transform eye;
    [SerializeField] Vector3 autoEyeLocalPos = new Vector3(0f, 1.6f, 0.2f);
    [SerializeField] float viewDistance = 18f;
    [SerializeField, Range(0f, 180f)] float viewHalfAngle = 60f;
    [SerializeField] LayerMask occluderMask;
    [SerializeField] string playerTag = "Player";

    [Header("Proximity (omnidireccional)")]
    [Tooltip("Radio base de detección OMNIDIRECCIONAL (sin cono, sin LoS). Si el jugador entra → se considera detectado. Escalado por dificultad/pickups vía DifficultyManager. 0 = desactivado.")]
    [SerializeField] float baseProximityRadius = 3f;
    [Tooltip("Si está activo, la detección por proximidad respeta occluders (raycast). Si false, atraviesa paredes (recomendado false: es un 'sexto sentido').")]
    [SerializeField] bool proximityRequiresLoS = false;

    public Transform Eye => eye;

    [Header("Detection scoring")]
    [SerializeField] float crouchMul = 0.4f;
    [SerializeField] float walkingMul = 0.7f;
    [SerializeField] float sprintingMul = 1.3f;
    [SerializeField] float idleMul = 0.5f;
    [SerializeField] float lampMul = 1.6f;
    [SerializeField] float baseGainPerSec = 1.0f;
    [SerializeField] float decayPerSec = 0.5f;

    public float Score { get; private set; }
    public DetectionVisibility Visibility { get; private set; }
    public bool HasLineOfSight { get; private set; }

    Transform player;
    PlayerController pc;

    public static void NotifyNoise(Vector3 position, float intensity)
    {
        NoiseHeard?.Invoke(position, intensity);
    }

    void Awake()
    {
        EnsureEye();
    }

    void OnValidate()
    {
        // En editor: si no hay eye intentamos encontrar un hijo llamado "Eye".
        if (!eye)
        {
            var t = transform.Find("Eye");
            if (t) eye = t;
        }
    }

    void EnsureEye()
    {
        if (eye) return;
        var existing = transform.Find("Eye");
        if (existing) { eye = existing; return; }
        var go = new GameObject("Eye");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = autoEyeLocalPos;
        go.transform.localRotation = Quaternion.identity; // mira hacia +Z local del enemigo
        eye = go.transform;
    }

    void Update()
    {
        if (!ResolvePlayer()) return;
        ComputeVisibility();
        UpdateScore();
    }

    bool ResolvePlayer()
    {
        if (player) return true;
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (!p) return false;
        player = p.transform;
        pc = p.GetComponent<PlayerController>();
        return true;
    }

    void ComputeVisibility()
    {
        Visibility = DetectionVisibility.None;
        HasLineOfSight = false;
        Vector3 to = player.position + Vector3.up * 1.4f - eye.position;
        float dist = to.magnitude;

        // ── Detección omnidireccional por proximidad ───────────────────────
        // Radio efectivo = base * multiplicador de dificultad/pickups.
        float proximity = baseProximityRadius * ResolveProximityMul();
        if (proximity > 0f && dist <= proximity)
        {
            bool blocked = false;
            if (proximityRequiresLoS && dist > 0.01f)
            {
                Vector3 dir2 = to / dist;
                blocked = Physics.Raycast(eye.position, dir2, dist, occluderMask, QueryTriggerInteraction.Ignore);
            }
            if (!blocked)
            {
                HasLineOfSight = true;
                Visibility = DetectionVisibility.Frontside;
                return;
            }
        }

        // ── Cono de visión clásico ──────────────────────────────────────────
        if (dist > viewDistance) return;
        Vector3 dir = to / dist;
        float angle = Vector3.Angle(eye.forward, dir);
        if (angle > viewHalfAngle) return;
        if (Physics.Raycast(eye.position, dir, out var hit, dist, occluderMask, QueryTriggerInteraction.Ignore))
            return; // occluded
        HasLineOfSight = true;

        // Front vs back: compare enemy-forward with player-forward.
        float facing = Vector3.Dot(eye.forward, player.forward);
        // facing > 0 → player and enemy look the same direction → enemy sees player's BACK.
        Visibility = facing > 0.2f ? DetectionVisibility.Backside : DetectionVisibility.Frontside;
    }

    /// <summary>Mul (escala el radio base) según dificultad + pickups recogidos.</summary>
    float ResolveProximityMul()
    {
        var dm = DifficultyManager.Instance;
        if (dm == null) return 1f;
        var diff = dm.GetRuntimeSettings();
        return diff != null && diff.proximityRadiusMul > 0f ? diff.proximityRadiusMul : 1f;
    }

    void UpdateScore()
    {
        float gain = 0f;
        if (HasLineOfSight)
        {
            float mul = idleMul;
            if (pc)
            {
                if (pc.IsMoving)
                    mul = pc.IsCrouching ? crouchMul : (pc.IsSprinting ? sprintingMul : walkingMul);
                else
                    mul = pc.IsCrouching ? crouchMul * 0.5f : idleMul;
                if (pc.LampOn) mul *= lampMul;
            }
            gain = baseGainPerSec * mul;
        }

        if (gain > 0f) Score = Mathf.Min(1f, Score + gain * Time.deltaTime);
        else Score = Mathf.Max(0f, Score - decayPerSec * Time.deltaTime);
    }

    void OnDrawGizmos()
    {
        // Versión más tenue siempre visible para localizar el ojo en escena.
        Transform e = eye ? eye : transform;
        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(e.position, 0.08f);
        Gizmos.DrawRay(e.position, e.forward * 0.6f);
    }

    void OnDrawGizmosSelected()
    {
        Transform e = eye ? eye : transform;
        Gizmos.color = HasLineOfSight ? new Color(1f, 0.2f, 0.2f, 0.9f) : new Color(1f, 1f, 0.2f, 0.7f);
        // Cono de visión — rotación alrededor del UP del ojo (sigue su pitch/roll).
        Vector3 axis = e.up;
        Vector3 fwd = e.forward * viewDistance;
        Quaternion qL = Quaternion.AngleAxis(-viewHalfAngle, axis);
        Quaternion qR = Quaternion.AngleAxis( viewHalfAngle, axis);
        Vector3 left  = qL * fwd;
        Vector3 right = qR * fwd;
        Gizmos.DrawLine(e.position, e.position + left);
        Gizmos.DrawLine(e.position, e.position + right);
        Gizmos.DrawLine(e.position, e.position + fwd);
        const int seg = 24;
        Vector3 prev = e.position + left;
        for (int i = 1; i <= seg; i++)
        {
            float t = (float)i / seg;
            Quaternion q = Quaternion.AngleAxis(Mathf.Lerp(-viewHalfAngle, viewHalfAngle, t), axis);
            Vector3 p = e.position + q * fwd;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
        // Esfera de alcance máximo
        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
        Gizmos.DrawWireSphere(e.position, viewDistance);
    }
}
