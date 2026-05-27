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

    void Awake()
    {
        anim = GetComponent<Animator>();
        aiCached = GetComponent<EnemyAIBase>();
    }

    public void SetPatrolling(bool v) => anim.SetBool(HashPatrolling, v);
    public void SetChasing(bool v) => anim.SetBool(HashChasing, v);

    public void TriggerAttack() { OnAttack?.Invoke(); }
    public void TriggerAlert()
    {
        AudioManager.Instance?.PlaySFX(SFXId.MinotaurNeigh, transform.position);
        OnAlert?.Invoke();
    }

    public float GetGradualTurnSpeed() => gradualTurnSpeed;

    /// <summary>
    /// Cambia <see cref="Animator.speed"/> directamente. Se usa para PAUSAR la animación
    /// durante el stun por linterna (speed = 0) y restaurarla (speed = 1) al salir.
    /// </summary>
    public void SetAnimatorSpeed(float s)
    {
        if (anim != null) anim.speed = Mathf.Max(0f, s);
    }

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

    [Header("Debug / Fallback footsteps")]
    [Tooltip("Loguea en consola cada vez que FootstepWalk/FootstepRun se invocan. Útil para verificar que los AnimationEvents están bien colocados en los clips del minotauro.")]
    [SerializeField] bool debugFootsteps;
    [Tooltip("Si está activo y los AnimationEvents 'FootstepWalk'/'FootstepRun' NO se llaman mientras el mino se mueve, dispara la pisada nosotros mismos por tiempo. Es un PARCHE: lo limpio es añadir los Animation Events al .anim del mino — pero esto evita que el juego se quede sin pasos hasta entonces.")]
    [SerializeField] bool fallbackFootstepsByTimer = true;
    [Tooltip("Intervalo (s) entre pisadas fallback caminando.")]
    [SerializeField] float fallbackWalkInterval = 0.55f;
    [Tooltip("Intervalo (s) entre pisadas fallback corriendo.")]
    [SerializeField] float fallbackRunInterval = 0.32f;
    [Tooltip("Velocidad mínima (m/s) del NavMeshAgent para considerar que el mino se mueve y disparar el fallback.")]
    [SerializeField] float fallbackMoveVelocityThreshold = 0.4f;

    float lastFootstepEventTime = -1f; // -1 = aún no se ha llamado nunca
    float fallbackTimer;
    bool warnedNoEvents;
    EnemyAIBase aiCached;

    [Tooltip("Se invoca al pisar caminando (Animation Event 'FootstepWalk'). Engánchalo a SFX, partículas, etc.")]
    public UnityEngine.Events.UnityEvent OnFootstepWalk;
    [Tooltip("Se invoca al pisar corriendo (Animation Event 'FootstepRun'). Engánchalo a SFX, partículas, etc.")]
    public UnityEngine.Events.UnityEvent OnFootstepRun;

    void Update()
    {
        if (!fallbackFootstepsByTimer) return;
        if (aiCached == null) aiCached = GetComponent<EnemyAIBase>();
        if (aiCached == null || aiCached.Agent == null) return;

        // Si los AnimationEvents están disparando recientemente, el fallback se mantiene apagado
        // para no duplicar pisadas. Margen de 1.5s desde el último evento real.
        if (lastFootstepEventTime > 0f && Time.time - lastFootstepEventTime < 1.5f)
        {
            fallbackTimer = 0f;
            return;
        }

        bool moving = aiCached.Agent.velocity.sqrMagnitude > fallbackMoveVelocityThreshold * fallbackMoveVelocityThreshold;
        if (!moving) { fallbackTimer = 0f; return; }

        // Watchdog (una sola vez): si han pasado >4s en juego sin recibir ni un solo evento,
        // avisamos en consola que falta configurar los AnimationEvents.
        if (!warnedNoEvents && lastFootstepEventTime < 0f && Time.time > 4f)
        {
            Debug.LogWarning("[EnemyAnimator] Los AnimationEvents 'FootstepWalk'/'FootstepRun' no se están disparando en los clips del minotauro. " +
                             "Abre el .anim del mino, añade un AnimationEvent llamado 'FootstepWalk' (en walk) y 'FootstepRun' (en run) en los frames de pisada. " +
                             "Mientras tanto se usa el fallback por timer.", this);
            warnedNoEvents = true;
        }

        bool running = aiCached.CurrentState == EnemyAIBase.State.Chase
                    || aiCached.CurrentState == EnemyAIBase.State.Attacking;
        float interval = running ? fallbackRunInterval : fallbackWalkInterval;
        fallbackTimer += Time.deltaTime;
        if (fallbackTimer >= interval)
        {
            fallbackTimer = 0f;
            // Llamamos a los mismos métodos públicos para que la lógica (shake + sfx + UnityEvent) sea idéntica.
            // OJO: NO actualizamos lastFootstepEventTime aquí, así si los AnimationEvents empezasen a llegar
            // el flag lastFootstepEventTime se mantendría >0 sólo cuando los EVENTOS reales disparen.
            if (running) FootstepRunInternal(fromAnimationEvent: false);
            else FootstepWalkInternal(fromAnimationEvent: false);
        }
    }

    // Animation Event called from the attack animation at the swing/charge frame.
    public void AttackFrame()
    {
        var ai = GetComponent<EnemyAIBase>();
        ai?.OnAttackFrame();
    }

    /// <summary>Animation Event: pisada caminando. Dispara shake + UnityEvent.</summary>
    public void FootstepWalk() => FootstepWalkInternal(fromAnimationEvent: true);

    /// <summary>Animation Event: pisada corriendo. Dispara shake + UnityEvent + SFX.</summary>
    public void FootstepRun() => FootstepRunInternal(fromAnimationEvent: true);

    void FootstepWalkInternal(bool fromAnimationEvent)
    {
        if (fromAnimationEvent) lastFootstepEventTime = Time.time;
        if (debugFootsteps) Debug.Log($"[Mino] FootstepWalk fired (fromEvent={fromAnimationEvent})", this);
        CameraShake.Shake(transform.position, walkStepShake, stepShakeDuration);
        if (walkStepSfx != SFXId.None)
            AudioManager.Instance?.PlaySFX(walkStepSfx, transform.position);
        OnFootstepWalk?.Invoke();
    }

    void FootstepRunInternal(bool fromAnimationEvent)
    {
        if (fromAnimationEvent) lastFootstepEventTime = Time.time;
        if (debugFootsteps) Debug.Log($"[Mino] FootstepRun fired (fromEvent={fromAnimationEvent})", this);
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
