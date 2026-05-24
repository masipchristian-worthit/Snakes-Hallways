using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// "Blind" AI — does not know where the player is by default.
/// It patrols, investigates noises (depending on difficulty),
/// and accepts approximate hints from EnemyAIInteligent.
/// On line-of-sight, it transitions to chase. The intelligent layer can also
/// force chase / set known position when conditions warrant.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAIBase : MonoBehaviour
{
    public enum State { Idle, Patrol, Investigate, Alert, Chase, Attacking, Stunned }

    [Header("Patrol")]
    [Tooltip("Si está vacío, el enemigo patrullará escogiendo puntos aleatorios del NavMesh.")]
    [SerializeField] List<Transform> patrolPoints = new();
    [SerializeField] float patrolWaitTime = 2.5f;
    [SerializeField] float arriveDistance = 1.2f;

    [Header("Patrol — NavMesh wander (cuando no hay patrolPoints)")]
    [Tooltip("Radio alrededor del enemigo para muestrear puntos aleatorios del NavMesh.")]
    [SerializeField] float wanderRadius = 18f;
    [Tooltip("Nº de intentos por elección de punto. Se queda con el mejor candidato según escalación de dificultad.")]
    [SerializeField] int wanderSampleCount = 6;
    [Tooltip("Distancia mínima entre puntos consecutivos para no quedarse dando vueltas.")]
    [SerializeField] float wanderMinStep = 4f;

    [Header("Speeds")]
    [SerializeField] float patrolSpeed = 2.2f;
    [SerializeField] float chaseSpeed = 5.2f;
    [SerializeField] float investigateSpeed = 3f;

    [Header("Attack")]
    [SerializeField] float attackRange = 2.5f;
    [Tooltip("Duración (s) del estado Attacking. Debe ser >= a la duración de la animación de ataque.")]
    [SerializeField] float attackDuration = 1.2f;
    [SerializeField] EnemyAttack attackCollider;
    [Tooltip("Distancia mínima a la que el enemigo intentará quedarse del jugador para no clipear con él.")]
    [SerializeField] float safetyDistance = 1.6f;

    [Header("Memory by distance")]
    [Tooltip("Distancia ≤ a esta → memoria al máximo. Más lejos → menos memoria.")]
    [SerializeField] float closeMemoryDistance = 4f;
    [Tooltip("Multiplicador de memoria cuando el jugador está cerca.")]
    [SerializeField] float closeMemoryMultiplier = 1.6f;
    [Tooltip("Multiplicador de memoria cuando el jugador está al límite del cono.")]
    [SerializeField] float farMemoryMultiplier = 0.4f;
    [Tooltip("Distancia a la que se aplica el multiplicador 'far'. Por defecto = viewDistance del EnemyDetection.")]
    [SerializeField] float farMemoryDistance = 18f;

    [Header("Front-detection behavior (back-detection branch)")]
    [Tooltip("If the enemy first sees the player from behind, chance to charge directly without alerting.")]
    [SerializeField, Range(0f, 1f)] float chargeFromBackChance = 0.6f;

    [Header("Refs")]
    [SerializeField] EnemyAnimator enemyAnim;
    [SerializeField] EnemyDetection detection;

    [Header("Audio")]
    [Tooltip("AudioSource dedicado del minotauro (pisadas + idle breath). Si está vacío se intenta autoresolver.")]
    [SerializeField] AudioSource minotaurSource;
    [Tooltip("Fade-in del loop MinotaurIdleBreath al entrar a Idle/Patrol.")]
    [SerializeField] float idleBreathFadeIn = 0.6f;
    [Tooltip("Fade-out del loop MinotaurIdleBreath al salir a Chase/Attacking.")]
    [SerializeField] float idleBreathFadeOut = 0.6f;
    [Tooltip("Volumen del loop respiración idle.")]
    [Range(0f, 1f)][SerializeField] float idleBreathVolume = 1f;

    bool idleBreathActive;

    public State CurrentState { get; private set; } = State.Patrol;
    public NavMeshAgent Agent { get; private set; }
    public Vector3? KnownPlayerPos { get; private set; }
    public float DifficultyEscalation { get; private set; }

    Transform player;
    int patrolIdx;
    float patrolWaitTimer;
    Vector3? investigatePoint;
    float investigateTimer;
    Vector3 currentWanderTarget;
    bool hasWanderTarget;
    float chaseMemoryTimer;          // cuenta atrás de "recuerdo" tras perder LoS
    float attackCooldownTimer;       // cooldown global de ataque
    float postAttackWalkTimer;       // tiempo restante en modo Walk forzado tras impactar

    void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        if (!enemyAnim) enemyAnim = GetComponent<EnemyAnimator>();
        if (!detection) detection = GetComponent<EnemyDetection>();
        if (!minotaurSource)
        {
            // Busca un AudioSource propio o crea uno dedicado.
            minotaurSource = GetComponent<AudioSource>();
            if (!minotaurSource)
            {
                var go = new GameObject("AS_Minotaur");
                go.transform.SetParent(transform, false);
                minotaurSource = go.AddComponent<AudioSource>();
                minotaurSource.playOnAwake = false;
                minotaurSource.spatialBlend = 1f;
            }
        }
    }

    void OnEnable() { EnemyDetection.NoiseHeard += OnNoise; }
    void OnDisable() { EnemyDetection.NoiseHeard -= OnNoise; }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
        SetState(State.Patrol); // Patrol funciona también sin patrolPoints (wander por NavMesh).
    }

    void Update()
    {
        DifficultyEscalation = ResolveEscalation();

        if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;
        if (postAttackWalkTimer > 0f) postAttackWalkTimer -= Time.deltaTime;

        bool postAttackLocked = postAttackWalkTimer > 0f;

        // ── VISIÓN REALISTA ─────────────────────────────────────────────
        // Si el jugador está dentro del cono de visión con LoS clara, el enemigo
        // INTERRUMPE todo (alert, patrol, investigate…) y persigue inmediatamente.
        if (detection && detection.HasLineOfSight && player)
        {
            KnownPlayerPos = player.position;

            // Memoria proporcional a la distancia: cuanto más cerca, más recuerda.
            // currentDistance ≤ closeDistance → memoria ×1.5
            // currentDistance ≥ viewDistance  → memoria ×0.4
            float dist = Vector3.Distance(transform.position, player.position);
            chaseMemoryTimer = ComputeMemoryDuration(dist);

            if (!postAttackLocked)
            {
                if (CurrentState != State.Chase && CurrentState != State.Attacking)
                    BeginChase(detection.Visibility); // interrupción dura
            }
        }
        else if (CurrentState == State.Chase || CurrentState == State.Attacking)
        {
            // Sin LoS pero seguimos persiguiendo durante chaseMemoryTimer (escalado por dificultad + distancia).
            chaseMemoryTimer -= Time.deltaTime;
            if (chaseMemoryTimer <= 0f)
            {
                if (KnownPlayerPos.HasValue)
                {
                    investigatePoint = KnownPlayerPos;
                    investigateTimer = 0f;
                    Agent.SetDestination(KnownPlayerPos.Value);
                    SetState(State.Investigate);
                }
                else
                {
                    SetState(State.Patrol);
                }
                KnownPlayerPos = null;
            }
        }

        TickIdleSounds();

        switch (CurrentState)
        {
            case State.Idle: TickIdle(); break;
            case State.Patrol: TickPatrol(); break;
            case State.Investigate: TickInvestigate(); break;
            case State.Alert: TickAlert(); break;
            case State.Chase: TickChase(); break;
            case State.Attacking: TickAttack(); break;
        }

        UpdateAnimatorBools();
        HandleTurnAnimations();
    }

    float ResolveEscalation()
    {
        if (!GameManager.Instance || !DifficultyManager.Instance) return 0.3f;
        return DifficultyManager.Instance.GetEscalation(GameManager.Instance.PickupsCollected, GameManager.Instance.PickupsRequired);
    }

    void SetState(State s)
    {
        // Al salir de patrullaje invalidamos el wander target para que recalcule al volver.
        if (CurrentState == State.Patrol && s != State.Patrol) hasWanderTarget = false;
        CurrentState = s;
        switch (s)
        {
            case State.Patrol: Agent.speed = patrolSpeed; Agent.isStopped = false; hasWanderTarget = false; break;
            case State.Investigate: Agent.speed = investigateSpeed; Agent.isStopped = false; break;
            case State.Alert: Agent.isStopped = true; enemyAnim?.TriggerAlert(); break;
            case State.Chase:
                {
                    var diff = DifficultyManager.Instance ? DifficultyManager.Instance.GetSettings().chaseSpeedMul : 1f;
                    Agent.speed = chaseSpeed * diff;
                    Agent.isStopped = false;
                    // MinotaurDetect SFX descartado.
                    AudioManager.Instance?.PlayMusic(MusicId.Chase);
                    break;
                }
            case State.Attacking:
                Agent.isStopped = true;                 // se planta para golpear
                enemyAnim?.TriggerAttack();             // dispara el UnityEvent (Animator.Play "Attack", SFX, etc.)
                // OJO: el daño se aplica desde el AnimationEvent "AttackFrame" de la animación de ataque.
                // EnemyAnimator.AttackFrame() → EnemyAIBase.OnAttackFrame() → attackCollider.OpenWindow()
                break;
            case State.Idle: Agent.isStopped = true; break;
        }
    }

    void BeginChase(DetectionVisibility vis)
    {
        if (vis == DetectionVisibility.Backside)
        {
            // 60% directly charges, 40% roars first (Alert).
            if (Random.value < chargeFromBackChance) SetState(State.Chase);
            else SetState(State.Alert);
        }
        else
        {
            SetState(State.Chase);
        }
    }

    #region State Ticks
    void TickIdleSounds()
    {
        // MinotaurIdleBreath ahora es un LOOP gestionado por estado.
        // ON cuando está Idle / Patrol / Investigate (animaciones idle+walk).
        // OFF cuando entra a Alert / Chase / Attacking / Stunned.
        bool wantsIdleBreath =
            CurrentState == State.Idle ||
            CurrentState == State.Patrol ||
            CurrentState == State.Investigate;

        if (wantsIdleBreath && !idleBreathActive)
        {
            AudioManager.Instance?.StartLoop(SFXId.MinotaurIdleBreath, minotaurSource, idleBreathFadeIn, idleBreathVolume, spatial: true);
            idleBreathActive = true;
        }
        else if (!wantsIdleBreath && idleBreathActive)
        {
            AudioManager.Instance?.StopLoop(minotaurSource, idleBreathFadeOut);
            idleBreathActive = false;
        }
    }

    void TickIdle()
    {
        SetState(State.Patrol);
    }

    void TickPatrol()
    {
        if (patrolPoints.Count > 0) TickPatrolFixedPoints();
        else TickPatrolWander();
    }

    void TickPatrolFixedPoints()
    {
        if (Agent.pathPending) return;
        if (Agent.remainingDistance <= arriveDistance)
        {
            patrolWaitTimer += Time.deltaTime;
            if (patrolWaitTimer >= patrolWaitTime)
            {
                patrolWaitTimer = 0f;
                patrolIdx = (patrolIdx + 1) % patrolPoints.Count;
                Agent.SetDestination(patrolPoints[patrolIdx].position);
            }
        }
        else if (!Agent.hasPath)
        {
            Agent.SetDestination(patrolPoints[patrolIdx].position);
        }
    }

    void TickPatrolWander()
    {
        // Pedir un destino nuevo si no tenemos uno o si ya hemos llegado.
        bool needNewTarget =
            !hasWanderTarget ||
            (!Agent.pathPending && Agent.remainingDistance <= arriveDistance);

        if (needNewTarget)
        {
            patrolWaitTimer += Time.deltaTime;
            if (patrolWaitTimer < patrolWaitTime && hasWanderTarget) return;
            patrolWaitTimer = 0f;
            if (TryPickIntelligentWanderTarget(out var target))
            {
                currentWanderTarget = target;
                hasWanderTarget = true;
                Agent.SetDestination(currentWanderTarget);
            }
        }
        else if (!Agent.hasPath)
        {
            Agent.SetDestination(currentWanderTarget);
        }
    }

    /// <summary>
    /// Muestrea varios puntos aleatorios del NavMesh y elige uno ponderado por:
    ///   - distancia mínima respecto a la posición actual (evita rebotar en el sitio),
    ///   - sesgo hacia el jugador según escalación de dificultad (a más dificultad → más cerca del player).
    /// La IA inteligente puede ajustar el sesgo vía <see cref="WanderBiasToPlayer"/>.
    /// </summary>
    bool TryPickIntelligentWanderTarget(out Vector3 result)
    {
        result = transform.position;
        float bestScore = float.NegativeInfinity;
        bool found = false;

        float biasToPlayer = Mathf.Clamp01(EffectiveWanderBias);

        for (int i = 0; i < Mathf.Max(1, wanderSampleCount); i++)
        {
            Vector2 rnd = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = transform.position + new Vector3(rnd.x, 0f, rnd.y);

            if (!NavMesh.SamplePosition(candidate, out var hit, wanderRadius * 0.5f, NavMesh.AllAreas))
                continue;

            float distFromSelf = Vector3.Distance(hit.position, transform.position);
            if (distFromSelf < wanderMinStep) continue;

            float score = distFromSelf;

            if (player && biasToPlayer > 0f)
            {
                float distToPlayer = Vector3.Distance(hit.position, player.position);
                // A más biasToPlayer, más penalizamos quedarnos lejos del jugador.
                score -= biasToPlayer * distToPlayer * 1.5f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                result = hit.position;
                found = true;
            }
        }
        return found;
    }

    /// <summary>
    /// Sesgo 0..1 hacia el jugador a la hora de elegir destinos de wander.
    /// Por defecto se calcula desde la escalación de dificultad. EnemyAIInteligent puede sobreescribirlo.
    /// </summary>
    public float WanderBiasToPlayer { get; set; } = -1f; // -1 → derive from difficulty
    float EffectiveWanderBias =>
        WanderBiasToPlayer >= 0f ? WanderBiasToPlayer : Mathf.Clamp01(DifficultyEscalation);

    void TickInvestigate()
    {
        if (!investigatePoint.HasValue) { SetState(State.Patrol); return; }
        if (Agent.pathPending) return;
        if (Agent.remainingDistance <= arriveDistance)
        {
            investigateTimer += Time.deltaTime;
            if (investigateTimer >= 3f) { investigatePoint = null; investigateTimer = 0f; SetState(State.Patrol); }
        }
    }

    float alertTimer;
    void TickAlert()
    {
        Agent.isStopped = true;
        alertTimer += Time.deltaTime;
        if (alertTimer >= 1.2f) { alertTimer = 0f; BeginChase(DetectionVisibility.Frontside); }
    }

    void TickChase()
    {
        if (!player) return;

        Vector3 targetPoint = KnownPlayerPos ?? player.position;
        // Mantén una distancia de seguridad: no apuntes al pie del jugador, apunta a un punto
        // por delante a 'safetyDistance' de él, así nunca lo clipea.
        Vector3 toPlayer = targetPoint - transform.position;
        float distNow = toPlayer.magnitude;
        if (distNow > 0.001f)
        {
            Vector3 dir = toPlayer / distNow;
            float desiredDist = Mathf.Max(0f, distNow - safetyDistance);
            Vector3 stopAt = transform.position + dir * desiredDist;
            Agent.SetDestination(stopAt);
        }
        // Que el agent.stoppingDistance no sea menor que la safetyDistance.
        if (Agent.stoppingDistance < safetyDistance) Agent.stoppingDistance = safetyDistance;

        bool canAttack = attackCooldownTimer <= 0f && postAttackWalkTimer <= 0f;
        if (canAttack && Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            SetState(State.Attacking);
        }
    }

    float attackTimer;
    void TickAttack()
    {
        attackTimer += Time.deltaTime;
        // Mientras ataca se queda parado; encara al jugador.
        if (player)
        {
            Vector3 to = player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 360f * Time.deltaTime);
            }
        }
        if (attackTimer >= attackDuration)
        {
            attackTimer = 0f;
            SetState(State.Chase);
        }
    }

    /// <summary>Called by EnemyAnimator AnimationEvent on Run animation.</summary>
    public void OnAttackFrame()
    {
        if (!player) return;
        if (CurrentState != State.Chase && CurrentState != State.Attacking) return;
        if (Vector3.Distance(transform.position, player.position) <= attackRange + 0.5f)
        {
            attackCollider?.OpenWindow();
        }
    }

    /// <summary>Llámalo desde EnemyAttack cuando un golpe IMPACTA al jugador.
    /// Fuerza al enemigo a caminar (sin atacar) X segundos según dificultad y arranca cooldown.</summary>
    public void NotifyAttackLanded()
    {
        var diff = DifficultyManager.Instance != null ? DifficultyManager.Instance.GetSettings() : null;
        attackCooldownTimer = diff != null ? diff.attackCooldown : 2.5f;
        postAttackWalkTimer = diff != null ? diff.postAttackWalkSeconds : 4f;
        // Salimos inmediatamente del ataque al patrullaje (walk) — no persigue ni corre.
        if (CurrentState == State.Attacking || CurrentState == State.Chase)
            SetState(State.Patrol);
    }
    #endregion

    float GetChaseMemoryDuration()
    {
        var diff = DifficultyManager.Instance != null ? DifficultyManager.Instance.GetSettings() : null;
        return diff != null ? diff.chaseMemorySeconds : 3f;
    }

    /// <summary>Memoria de chase escalada por distancia: más cerca → recuerda más.</summary>
    float ComputeMemoryDuration(float currentDistance)
    {
        float baseMem = GetChaseMemoryDuration();
        float t = Mathf.InverseLerp(closeMemoryDistance, Mathf.Max(closeMemoryDistance + 0.01f, farMemoryDistance), currentDistance);
        float mul = Mathf.Lerp(closeMemoryMultiplier, farMemoryMultiplier, Mathf.Clamp01(t));
        return baseMem * mul;
    }

    void UpdateAnimatorBools()
    {
        if (!enemyAnim) return;
        // Solo reproducimos las animaciones de locomoción si el agente se mueve de verdad.
        bool actuallyMoving = !Agent.isStopped
                              && Agent.velocity.sqrMagnitude > 0.05f
                              && Agent.remainingDistance > Agent.stoppingDistance + 0.05f;

        bool inPatrolState = CurrentState == State.Patrol || CurrentState == State.Investigate;
        bool inChaseState  = CurrentState == State.Chase  || CurrentState == State.Attacking;

        enemyAnim.SetPatrolling(inPatrolState && actuallyMoving);
        enemyAnim.SetChasing(inChaseState && actuallyMoving);
    }

    void HandleTurnAnimations()
    {
        if (!enemyAnim) return;
        if (Agent.velocity.sqrMagnitude < 0.001f && !Agent.hasPath) return;

        Vector3 desired = (Agent.steeringTarget - transform.position);
        desired.y = 0f;
        if (desired.sqrMagnitude < 0.01f) return;
        desired.Normalize();
        float signed = Vector3.SignedAngle(transform.forward, desired, Vector3.up);
        bool isMoving = Agent.velocity.sqrMagnitude > 0.5f;

        if (isMoving)
        {
            float maxStep = enemyAnim.GetGradualTurnSpeed() * Time.deltaTime;
            float step = Mathf.Clamp(signed, -maxStep, maxStep);
            transform.Rotate(0f, step, 0f, Space.World);
        }
        Agent.isStopped = (CurrentState == State.Alert || CurrentState == State.Idle);
    }

    void OnNoise(Vector3 position, float intensity)
    {
        if (CurrentState == State.Chase || CurrentState == State.Attacking) return;
        if (!DifficultyManager.Instance) return;
        var diff = DifficultyManager.Instance.GetSettings();
        // soundReactivity 0 → ignore. 1 → directly chase.
        float roll = Random.value;
        float reactive = diff.soundReactivity * intensity;
        if (reactive < 0.05f) return;

        if (reactive >= 0.85f && roll < reactive)
        {
            // Higher difficulties — react aggressively.
            KnownPlayerPos = position;
            BeginChase(DetectionVisibility.Frontside);
        }
        else if (reactive >= 0.25f)
        {
            investigatePoint = position;
            investigateTimer = 0f;
            Agent.SetDestination(position);
            SetState(State.Investigate);
        }
    }

    #region API (used by EnemyAIInteligent)
    public void ReceiveHint(Vector3 approxPosition, float radius)
    {
        if (CurrentState == State.Chase || CurrentState == State.Attacking) return;

        // Probabilidad de ignorar (por dificultad).
        var diff = DifficultyManager.Instance != null ? DifficultyManager.Instance.GetSettings() : null;
        float ignore = diff != null ? diff.hintIgnoreChance : 0.4f;
        if (Random.value < ignore) return;

        Vector2 r = Random.insideUnitCircle * radius;
        Vector3 target = approxPosition + new Vector3(r.x, 0f, r.y);
        if (NavMesh.SamplePosition(target, out var hit, radius * 1.5f, NavMesh.AllAreas))
        {
            investigatePoint = hit.position;
            investigateTimer = 0f;
            Agent.SetDestination(hit.position);
            SetState(State.Investigate);
        }
    }

    public void ForceKnownPlayer(Vector3 pos)
    {
        KnownPlayerPos = pos;
        if (CurrentState != State.Chase) BeginChase(DetectionVisibility.Frontside);
    }

    public void Teleport(Vector3 pos)
    {
        Agent.Warp(pos);
    }

    public bool IsChasing => CurrentState == State.Chase || CurrentState == State.Attacking;
    public Transform Player => player;
    #endregion
}
