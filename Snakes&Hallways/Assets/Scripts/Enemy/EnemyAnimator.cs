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
    [Tooltip("Se invoca al entrar en estado Attacking. NOTA: la animación de ataque ya se fuerza por código vía PlayAttackAnimation() — NO enganches aquí Animator.Play ni SFX. Déjalo vacío o úsalo solo para VFX (partículas, decals). Si pones aquí un PlaySFX, sonará INCLUSO si la animación no se reproduce.")]
    public UnityEvent OnAttack;
    [Tooltip("Se invoca al entrar en estado Alert.")]
    public UnityEvent OnAlert;

    [Header("Attack animation (forced)")]
    [Tooltip("Nombre EXACTO del estado del Animator que contiene la animación de ataque. Se reproduce con Animator.CrossFadeInFixedTime para que SIEMPRE se ejecute, independientemente de los bools Chasing/Patrolling. Esto desacopla el ataque del estado de movimiento — el mino puede atacar desde Idle / Patrol / Chase indistintamente.")]
    [SerializeField] string attackStateName = "Attack";
    [Tooltip("Layer del Animator donde está el estado Attack (normalmente 0 = Base Layer).")]
    [SerializeField] int attackStateLayer = 0;
    [Tooltip("Tiempo (s) de crossfade al entrar a la animación de Attack. 0 = corte duro, 0.05-0.15 = transición suave.")]
    [SerializeField] float attackCrossFade = 0.06f;

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

    /// <summary>
    /// Fuerza la animación de ataque mediante <see cref="Animator.CrossFadeInFixedTime"/> al
    /// estado configurado en <see cref="attackStateName"/>. Se ignoran las transiciones del
    /// controller (bools Chasing/Patrolling) — esto permite atacar desde Idle, Patrol o Chase.
    ///
    /// FIX: antes el ataque solo se reproducía cuando la transición Run→Attack se disparaba
    /// (es decir, había que estar en estado Chasing). Si el mino estaba quieto / patrullando,
    /// no había animación y por tanto no había AttackFrame ni daño consistente.
    /// </summary>
    public void PlayAttackAnimation()
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;
        if (string.IsNullOrEmpty(attackStateName)) return;
        // CrossFadeInFixedTime usa el StateName y reproduce SIN depender de bools/triggers.
        // El '0f' del último parámetro fuerza arrancar desde el frame 0 del estado.
        anim.CrossFadeInFixedTime(attackStateName, Mathf.Max(0f, attackCrossFade), attackStateLayer, 0f);
    }

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
    [Tooltip("Intensidad base del shake al pisar caminando (se atenúa por distancia en CameraShake). Subido considerablemente — el shake del legacy era invisible por culpa del head-bob del player escribiendo sobre la misma transform que el shake.")]
    [SerializeField] float walkStepShake = 0.5f;
    [Tooltip("Intensidad base del shake al pisar corriendo.")]
    [SerializeField] float runStepShake = 1.1f;
    [SerializeField] float stepShakeDuration = 0.22f;

    [Header("Footstep SFX")]
    [Tooltip("ID del sonido reproducido en pisadas de Walk.")]
    [SerializeField] SFXId walkStepSfx = SFXId.MinotaurStep;
    [Tooltip("ID del sonido reproducido en pisadas de Run.")]
    [SerializeField] SFXId runStepSfx = SFXId.MinotaurStep;
    [Tooltip("Multiplicador global del volumen de las pisadas. Lo setea EnemyAIBase desde su inspector — NO lo cambies aquí salvo para testing.")]
    [Range(0f, 8f)][SerializeField] float footstepVolumeMul = 2.5f;

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
        if (debugFootsteps) Debug.Log($"[Mino] FootstepWalk fired (fromEvent={fromAnimationEvent}) vol={footstepVolumeMul}", this);
        CameraShake.Shake(transform.position, walkStepShake, stepShakeDuration);
        if (walkStepSfx != SFXId.None)
            AudioManager.Instance?.PlaySFX(walkStepSfx, transform.position, Mathf.Max(0f, footstepVolumeMul));
        OnFootstepWalk?.Invoke();
    }

    void FootstepRunInternal(bool fromAnimationEvent)
    {
        if (fromAnimationEvent) lastFootstepEventTime = Time.time;
        if (debugFootsteps) Debug.Log($"[Mino] FootstepRun fired (fromEvent={fromAnimationEvent}) vol={footstepVolumeMul}", this);
        CameraShake.Shake(transform.position, runStepShake, stepShakeDuration);
        if (runStepSfx != SFXId.None)
            AudioManager.Instance?.PlaySFX(runStepSfx, transform.position, Mathf.Max(0f, footstepVolumeMul));
        OnFootstepRun?.Invoke();
    }

    /// <summary>API pública para que EnemyAIBase ajuste el volumen de las pisadas desde su inspector.</summary>
    public void SetFootstepVolume(float v) => footstepVolumeMul = Mathf.Max(0f, v);

    /// <summary>Animation Event: swing/impacto del ataque. Reproduce el SFX de ataque
    /// (independiente del AttackFrame, que sólo aplica daño).</summary>
    public void AttackSfx()
    {
        if (attackSfx != SFXId.None)
            AudioManager.Instance?.PlaySFX(attackSfx, transform.position, attackSfxVolume);
    }
}
