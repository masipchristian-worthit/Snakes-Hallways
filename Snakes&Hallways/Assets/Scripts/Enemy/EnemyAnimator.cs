using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Animator))]
public class EnemyAnimator : MonoBehaviour
{
    [Header("Turn settings")]
    [Tooltip("Degrees per second when turning while moving.")]
    [SerializeField] float gradualTurnSpeed = 180f;

    [Header("Eventos")]
    [Tooltip("Se invoca al entrar en estado Attacking. Engancha aquí la función que reproduce la animación de ataque (p.ej. Animator.Play en un estado, un AnimationClip, un SFX, etc.).")]
    public UnityEvent OnAttack;
    [Tooltip("Se invoca al entrar en estado Alert.")]
    public UnityEvent OnAlert;

    Animator anim;

    static readonly int HashPatrolling = Animator.StringToHash("Patrolling");
    static readonly int HashChasing    = Animator.StringToHash("Chasing");

    public bool LocomotionLocked => false;

    void Awake() { anim = GetComponent<Animator>(); }

    public void SetPatrolling(bool v) => anim.SetBool(HashPatrolling, v);
    public void SetChasing(bool v) => anim.SetBool(HashChasing, v);

    public void TriggerAttack() { OnAttack?.Invoke(); }
    public void TriggerAlert()
    {
        AudioManager.Instance?.PlaySFX(SFXId.MinotaurNeigh, transform.position);
        OnAlert?.Invoke();
    }

    public float GetGradualTurnSpeed() => gradualTurnSpeed;

    [Header("Footstep shake")]
    [Tooltip("Intensidad base del shake al pisar caminando (se atenúa por distancia en CameraShake).")]
    [SerializeField] float walkStepShake = 0.15f;
    [Tooltip("Intensidad base del shake al pisar corriendo.")]
    [SerializeField] float runStepShake = 0.45f;
    [SerializeField] float stepShakeDuration = 0.18f;

    [Header("Footstep SFX")]
    [Tooltip("ID del sonido reproducido en pisadas de Walk.")]
    [SerializeField] SFXId walkStepSfx = SFXId.MinotaurStep;
    [Tooltip("ID del sonido reproducido en pisadas de Run.")]
    [SerializeField] SFXId runStepSfx = SFXId.MinotaurStep;

    [Header("Attack SFX (animation event)")]
    [Tooltip("Sonido del swing del ataque del minotauro. Lo dispara el AnimationEvent 'AttackSfx' colocado en el frame de impacto.")]
    [SerializeField] SFXId attackSfx = SFXId.MinotaurCharge;
    [Range(0f, 1f)][SerializeField] float attackSfxVolume = 1f;

    [Tooltip("Se invoca al pisar caminando (Animation Event 'FootstepWalk'). Engánchalo a SFX, partículas, etc.")]
    public UnityEngine.Events.UnityEvent OnFootstepWalk;
    [Tooltip("Se invoca al pisar corriendo (Animation Event 'FootstepRun'). Engánchalo a SFX, partículas, etc.")]
    public UnityEngine.Events.UnityEvent OnFootstepRun;

    // Animation Event called from Run animation at the swing/charge frame.
    public void AttackFrame()
    {
        var ai = GetComponent<EnemyAIBase>();
        ai?.OnAttackFrame();
    }

    /// <summary>Animation Event: pisada caminando. Dispara shake + UnityEvent.</summary>
    public void FootstepWalk()
    {
        CameraShake.Shake(transform.position, walkStepShake, stepShakeDuration);
        if (walkStepSfx != SFXId.None)
            AudioManager.Instance?.PlaySFX(walkStepSfx, transform.position);
        OnFootstepWalk?.Invoke();
    }

    /// <summary>Animation Event: pisada corriendo. Dispara shake + UnityEvent + SFX.</summary>
    public void FootstepRun()
    {
        CameraShake.Shake(transform.position, runStepShake, stepShakeDuration);
        if (runStepSfx != SFXId.None)
            AudioManager.Instance?.PlaySFX(runStepSfx, transform.position);
        OnFootstepRun?.Invoke();
    }

    /// <summary>Animation Event: swing/impacto del ataque. Reproduce el SFX de ataque
    /// (independiente del AttackFrame, que sólo aplica daño).</summary>
    public void AttackSfx()
    {
        if (attackSfx != SFXId.None)
            AudioManager.Instance?.PlaySFX(attackSfx, transform.position, attackSfxVolume);
    }
}
