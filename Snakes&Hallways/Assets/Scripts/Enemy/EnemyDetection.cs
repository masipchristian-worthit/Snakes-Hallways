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
    [SerializeField] Transform eye;
    [SerializeField] float viewDistance = 18f;
    [SerializeField, Range(0f, 180f)] float viewHalfAngle = 60f;
    [SerializeField] LayerMask occluderMask;
    [SerializeField] string playerTag = "Player";

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
        if (!eye) eye = transform;
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
}
