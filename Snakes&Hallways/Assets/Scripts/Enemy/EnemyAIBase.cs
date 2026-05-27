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
    [Tooltip("Espera entre patrol points (s). Bajado por defecto para que patrulle de forma más constante.")]
    [SerializeField] float patrolWaitTime = 0.6f;
    [SerializeField] float arriveDistance = 1.2f;

    [Header("Patrol — NavMesh wander (cuando no hay patrolPoints)")]
    [Tooltip("Radio alrededor del enemigo para muestrear puntos aleatorios del NavMesh. Subido para que cubra distancias mucho más largas.")]
    [SerializeField] float wanderRadius = 35f;
    [Tooltip("Nº de intentos por elección de punto. Se queda con el mejor candidato según escalación de dificultad.")]
    [SerializeField] int wanderSampleCount = 8;
    [Tooltip("Distancia mínima entre puntos consecutivos para no quedarse dando vueltas. Subido para forzar saltos largos.")]
    [SerializeField] float wanderMinStep = 12f;

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

    [Header("Forced Respawn — sampling")]
    [Tooltip("Tolerancia (±m) sobre la distancia ideal al elegir el punto del NavMesh. Por debajo no se acepta el punto, por encima se aceptan los más cercanos.")]
    [SerializeField] float forcedRespawnTolerance = 6f;
    [Tooltip("Nº de candidatos a samplear en el NavMesh para elegir el mejor.")]
    [SerializeField] int forcedRespawnSamples = 12;
    [Tooltip("Si tras un respawn forzado el minotauro mira al jugador, pasa directo a Chase. Si false, vuelve a Patrol.")]
    [SerializeField] bool forcedRespawnChasesIfFacing = true;
    [Tooltip("Cooldown (s) entre dos respawns forzados, para evitar teleports consecutivos.")]
    [SerializeField] float forcedRespawnCooldown = 4f;

    [Header("Stuck respawn")]
    [Tooltip("Segundos sin moverse apreciablemente (no salir del mismo radio) antes de teleportarse forzosamente. 0 = desactivado.")]
    [SerializeField] float stuckRespawnSeconds = 6f;
    [Tooltip("Radio (m) que el minotauro debe abandonar para resetear el contador de stuck. Si se queda dentro X s, se considera atascado.")]
    [SerializeField] float stuckRadius = 3f;
    [Tooltip("Y forzada del respawn: tras samplear XZ alrededor del jugador, el Warp queda fijado a esta altura (suele coincidir con el navmesh superior del mapa).")]
    [SerializeField] float forcedRespawnY = 0f;
    [Tooltip("Tolerancia vertical al samplear NavMesh alrededor de (playerX, forcedRespawnY, playerZ). Subir si el navmesh superior no es perfectamente plano.")]
    [SerializeField] float forcedRespawnVerticalTolerance = 2f;
    [Tooltip("Radio MÍNIMO (m) alrededor del jugador en el que el minotauro NUNCA puede reaparecer. Cualquier candidato de respawn dentro de esta zona se descarta.")]
    [SerializeField] float minSafeRespawnRadius = 7f;

    [Header("Forced Respawn — Distancias por trigger")]
    [Tooltip("Si el TP lo dispara 'stuck' (mino atascado), distancia MÍNIMA al jugador. Random hasta 'stuckMaxDistance'.")]
    [SerializeField] float stuckMinDistance = 18f;
    [Tooltip("Si el TP lo dispara 'stuck', distancia MÁXIMA al jugador. El sampler escogerá aleatorio entre min y max.")]
    [SerializeField] float stuckMaxDistance = 32f;
    [Tooltip("Si el TP lo dispara 'noSight' (mucho tiempo sin verlo), distancia mínima.")]
    [SerializeField] float noSightMinDistance = 15f;
    [Tooltip("Si el TP lo dispara 'noSight', distancia máxima.")]
    [SerializeField] float noSightMaxDistance = 20f;
    [Tooltip("Si el TP lo dispara 'farFromPlayer' (lejos del jugador), distancia mínima.")]
    [SerializeField] float farMinDistance = 10f;
    [Tooltip("Si el TP lo dispara 'farFromPlayer', distancia máxima.")]
    [SerializeField] float farMaxDistance = 20f;

    [Header("Forced Respawn — Validación occluders (CRÍTICO)")]
    [Tooltip("Layers que cuentan como occluders válidos para tapar al minotauro tras un TP. Debe contener 'Scenario'. Si NO está marcado nada el TP NO valida occlusion (no recomendado).")]
    [SerializeField] LayerMask scenarioOccluderMask;
    [Tooltip("Si está activo, antes de aceptar un punto de TP se lanzan 8 raycasts desde la cámara del player a las 8 esquinas del bounds del minotauro. SOLO se acepta el punto si TODAS las esquinas quedan ocluidas por colliders del scenarioOccluderMask. Garantiza que el TP no caiga visible aunque sea parcialmente.")]
    [SerializeField] bool requireScenarioOcclusion = true;
    [Tooltip("Margen adicional (m) que se resta a la distancia de los raycasts para considerar que el occluder está 'antes' del minotauro. Sube si los raycasts colisionan con el propio mino.")]
    [SerializeField] float occluderMargin = 0.3f;
    [Tooltip("Si tras N samples no se encuentra ningún punto que cumpla la oclusión, el TP se ABORTA (no se hace) en lugar de aceptar un punto subóptimo. Si false, se acepta el menos malo.")]
    [SerializeField] bool abortIfNoOccludedPoint = true;
    [Tooltip("Tamaño extra (multiplicador) que se aplica al bounds del minotauro al lanzar los raycasts de validación. >1 es más exigente (mejor para evitar 'asomarse').")]
    [SerializeField] float occlusionBoundsExpand = 1.05f;

    [Header("Forced Respawn — Zona segura Portal")]
    [Tooltip("Una vez el portal de victoria está activo, cualquier candidato de TP dentro de este radio (m) del portal se descarta. 0 = desactivado.")]
    [SerializeField] float portalSafeRadius = 6f;
    [Tooltip("Cooldown forzado del TP cuando el jugador está MUY cerca del portal (a esta distancia o menos). Sube la presión final.")]
    [SerializeField] float portalProximityCooldownMul = 0.4f;
    [Tooltip("Distancia (m) al portal por debajo de la cual el cooldown se reduce por portalProximityCooldownMul.")]
    [SerializeField] float portalProximityDistance = 18f;

    [Header("Forced Respawn — Audio TP")]
    [Tooltip("Volumen del SFX MinotaurTP que se reproduce tras un TP. 0 = desactivado.")]
    [Range(0f, 2f)][SerializeField] float minotaurTpVolume = 1f;

    [Header("Stun (Linterna)")]
    [Tooltip("Tiempo (s) que dura el stun cuando la linterna lo activa durante un ataque.")]
    [SerializeField] float stunDuration = 2.5f;
    [Tooltip("Color del outline morado mostrado durante el stun (regulable).")]
    [SerializeField] Color stunOutlineColor = new Color(0.65f, 0.18f, 1f, 1f);
    [Tooltip("Intensidad del outline (multiplicador de emisión sobre stunOutlineColor).")]
    [Range(0f, 8f)][SerializeField] float stunOutlineIntensity = 2.5f;
    [Tooltip("Renderers del mino que reciben el outline durante el stun. Si está vacío se autollenan con todos los SkinnedMeshRenderer/MeshRenderer hijos.")]
    [SerializeField] Renderer[] stunRenderers;
    [Tooltip("Rango (m) jugador↔mino dentro del cual la linterna puede stunearlo. 0 = sin límite (solo importa el ataque).")]
    [SerializeField] float stunLampRange = 8f;

    [Header("Idle Attack")]
    [Tooltip("Si está activo, el mino puede atacar directamente desde Idle si el jugador entra en attackRange — sin pasar antes por Chase. Útil para spawns/posts/ambush.")]
    [SerializeField] bool allowAttackFromIdle = true;

    [Header("Chase — lead targeting (predicción)")]
    [Tooltip("Apunta a player.position + playerVelocity * leadTime. 0 = sin predicción (apunta al jugador exacto).")]
    [SerializeField] float chaseLeadTime = 0.35f;
    [Tooltip("Clamp del offset de predicción para no sobre-anticipar en sprints largos.")]
    [SerializeField] float chaseMaxLeadDistance = 4f;

    [Header("Search — espiral tras perder LoS")]
    [Tooltip("Nº de waypoints de la espiral de búsqueda tras agotar chaseMemory. 0 = desactivado (legacy: ir directo al last known).")]
    [SerializeField] int spiralSearchPoints = 4;
    [Tooltip("Radio mínimo del primer waypoint de la espiral (centrada en el last known position).")]
    [SerializeField] float spiralMinRadius = 3f;
    [Tooltip("Radio máximo del último waypoint de la espiral.")]
    [SerializeField] float spiralMaxRadius = 9f;

    [Header("Speed smoothing")]
    [Tooltip("Aceleración de Agent.speed (m/s²) al cambiar de estado. 0 = cambio instantáneo (legacy).")]
    [SerializeField] float speedAccel = 6f;

    [Header("Stalking audio — breath cerca sin LoS")]
    [Tooltip("Distancia (m) bajo la cual el breath se intensifica si el jugador NO tiene LoS. 0 = stalking audio desactivado.")]
    [SerializeField] float stalkAudioDistance = 8f;
    [Tooltip("Multiplicador máximo del volumen del breath cuando el jugador está pegado al monstruo y no lo ve.")]
    [SerializeField] float stalkAudioMaxBoost = 2.2f;

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

    // Trigger que disparó el TP forzado más reciente — define qué distancia se usa para muestrear el punto.
    enum ForcedRespawnTrigger { None, Stuck, NoSight, FarFromPlayer }

    // Forced respawn tracking
    float noSightTimer;              // segundos seguidos sin tener LoS con el jugador
    float farFromPlayerTimer;        // segundos seguidos por encima de farFromPlayerDistance
    float forcedRespawnCooldownTimer;

    // Stun (linterna)
    float stunTimer;
    bool isStunned;
    static readonly int HashEmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly int HashBaseColor = Shader.PropertyToID("_BaseColor");
    public bool IsStunned => isStunned;
    // Stuck detection: ancla la posición y mide cuánto tiempo lleva sin abandonar stuckRadius.
    Vector3 stuckAnchor;
    float stuckTimer;
    bool stuckInit;

    // Speed smoothing
    float targetSpeed;

    // Player velocity estimation (para lead targeting)
    Vector3 prevPlayerPos;
    Vector3 playerVelocity;
    bool prevPlayerInit;

    // Spiral search queue
    readonly Queue<Vector3> spiralQueue = new();
    bool inSpiralSearch;

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
        // Inicializa speed sin lerp para evitar arrancar a 0 si el inspector lo dejó así.
        if (Agent != null) Agent.speed = targetSpeed;
    }

    void Update()
    {
        DifficultyEscalation = ResolveEscalation();

        // ── STUN ───────────────────────────────────────────────────────────
        // Pausa total: la IA, el agent y el Animator quedan congelados. Solo
        // decrementa el timer del stun. El outline lo gestiona TickStun.
        if (isStunned)
        {
            TickStun();
            return;
        }

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
                    BeginSpiralSearch(KnownPlayerPos.Value);
                }
                else
                {
                    SetState(State.Patrol);
                }
                KnownPlayerPos = null;
            }
        }

        TickPlayerVelocityEstimate();
        TickSpeedSmoothing();
        TickIdleSounds();
        TickForcedRespawn();

        switch (CurrentState)
        {
            case State.Idle: TickIdle(); break;
            case State.Patrol: TickPatrol(); break;
            case State.Investigate: TickInvestigate(); break;
            case State.Alert: TickAlert(); break;
            case State.Chase: TickChase(); break;
            case State.Attacking: TickAttack(); break;
            case State.Stunned: TickStun(); break;
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
        // Al salir de Investigate también limpiamos la cola de espiral.
        if (CurrentState == State.Investigate && s != State.Investigate) { spiralQueue.Clear(); inSpiralSearch = false; }
        CurrentState = s;
        switch (s)
        {
            case State.Patrol: targetSpeed = patrolSpeed; Agent.isStopped = false; hasWanderTarget = false; break;
            case State.Investigate: targetSpeed = investigateSpeed; Agent.isStopped = false; break;
            case State.Alert: Agent.isStopped = true; enemyAnim?.TriggerAlert(); break;
            case State.Chase:
                {
                    var diff = DifficultyManager.Instance ? DifficultyManager.Instance.GetRuntimeSettings().chaseSpeedMul : 1f;
                    targetSpeed = chaseSpeed * diff;
                    Agent.isStopped = false;
                    // MinotaurDetect SFX descartado.
                    AudioManager.Instance?.PlayMusic(MusicId.Chase);
                    break;
                }
            case State.Attacking:
                Agent.isStopped = true;                 // se planta para golpear
                enemyAnim?.TriggerAttack();             // dispara el UnityEvent (Animator.Play "Attack", SFX, etc.)
                // El daño puede venir por dos vías:
                //   a) AnimationEvent "AttackFrame" → OnAttackFrame() (preferida, sincronizada al swing).
                //   b) Fallback temporal en TickAttack() si la anim no llega a reproducir (p.ej. el
                //      jugador se queda pegado y la transición Run→Attack no dispara el frame event).
                //      Sin esto, el minotauro entra a Attacking pero nunca aplica daño.
                attackDamageApplied = false;
                break;
            case State.Idle: Agent.isStopped = true; break;
            case State.Stunned:
                // El stun congela todo. Animator pausado y agent parado.
                Agent.isStopped = true;
                targetSpeed = 0f;
                if (enemyAnim != null)
                {
                    enemyAnim.SetPatrolling(false);
                    enemyAnim.SetChasing(false);
                    enemyAnim.SetAnimatorSpeed(0f);
                }
                break;
        }
    }

    /// <summary>
    /// API pública: el PlayerController la llama cuando el jugador ENCIENDE la linterna
    /// dentro del rango definido (stunLampRange) MIENTRAS el mino está atacando.
    /// Devuelve true si el stun se ha aplicado.
    /// </summary>
    public bool TryStunByLamp(Vector3 playerPosition)
    {
        // Solo se puede stunear si está en mitad del ataque.
        if (CurrentState != State.Attacking) return false;
        if (isStunned) return false;
        // Rango
        if (stunLampRange > 0f)
        {
            float dist = Vector3.Distance(transform.position, playerPosition);
            if (dist > stunLampRange) return false;
        }
        EnterStun();
        return true;
    }

    void EnterStun()
    {
        isStunned = true;
        stunTimer = stunDuration;
        // Cancelar también el possible damage que iba a aplicarse este ataque.
        attackDamageApplied = true;
        attackTimer = 0f;
        // Visual: outline morado.
        ApplyStunOutline(true);
        // Estado lógico: Stunned. Se restaurará el animator.speed en ExitStun.
        SetState(State.Stunned);
    }

    void ExitStun()
    {
        isStunned = false;
        stunTimer = 0f;
        ApplyStunOutline(false);
        if (enemyAnim != null) enemyAnim.SetAnimatorSpeed(1f);
        // Tras salir del stun, vuelve a Chase si conocemos al jugador, o Patrol si no.
        if (KnownPlayerPos.HasValue || (detection != null && detection.HasLineOfSight))
        {
            BeginChase(DetectionVisibility.Frontside);
        }
        else
        {
            SetState(State.Patrol);
        }
    }

    void TickStun()
    {
        // Decrementa con unscaledDeltaTime sería raro porque el resto usa deltaTime — aquí también.
        stunTimer -= Time.deltaTime;
        if (stunTimer <= 0f) ExitStun();
    }

    /// <summary>
    /// Activa/desactiva el outline morado del mino aplicando un MaterialPropertyBlock con
    /// _EmissionColor pumpado al stunOutlineColor * stunOutlineIntensity. Restaurar = limpiar.
    /// Usa los renderers asignados en inspector (stunRenderers) o autollena con todos los del mino.
    /// Si tu shader URP no tiene _EmissionColor (p.ej. Unlit), modifica _BaseColor en su lugar.
    /// </summary>
    void ApplyStunOutline(bool on)
    {
        EnsureStunRenderers();
        if (stunRenderers == null) return;
        for (int i = 0; i < stunRenderers.Length; i++)
        {
            var r = stunRenderers[i];
            if (r == null) continue;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            if (on)
            {
                Color emi = stunOutlineColor * Mathf.Max(0f, stunOutlineIntensity);
                // _EmissionColor (Lit/HDRP). Si tu shader no lo tiene, Unity lo ignora silenciosamente.
                mpb.SetColor(HashEmissionColor, emi);
                // Tinte de base como fallback visual si el shader no soporta emission.
                mpb.SetColor(HashBaseColor, Color.Lerp(Color.white, stunOutlineColor, 0.6f));
            }
            else
            {
                // Limpiar overrides para volver al material original.
                mpb.Clear();
            }
            r.SetPropertyBlock(mpb);
        }
    }

    void EnsureStunRenderers()
    {
        if (stunRenderers != null && stunRenderers.Length > 0) return;
        stunRenderers = GetComponentsInChildren<Renderer>();
    }

    /// <summary>
    /// Suaviza la transición de <see cref="NavMeshAgent.speed"/> hacia <see cref="targetSpeed"/>
    /// para evitar saltos bruscos entre patrol↔chase. Si <see cref="speedAccel"/> = 0, salta directo.
    /// </summary>
    void TickSpeedSmoothing()
    {
        if (Agent == null) return;
        if (speedAccel <= 0f) { Agent.speed = targetSpeed; return; }
        Agent.speed = Mathf.MoveTowards(Agent.speed, targetSpeed, speedAccel * Time.deltaTime);
    }

    /// <summary>
    /// Estima la velocidad del jugador a partir del delta de posición frame a frame.
    /// La usa <see cref="TickChase"/> para "lead targeting" (anticipar giros).
    /// </summary>
    void TickPlayerVelocityEstimate()
    {
        if (!player) { prevPlayerInit = false; playerVelocity = Vector3.zero; return; }
        if (!prevPlayerInit)
        {
            prevPlayerPos = player.position;
            prevPlayerInit = true;
            playerVelocity = Vector3.zero;
            return;
        }
        Vector3 delta = player.position - prevPlayerPos;
        if (Time.deltaTime > 0f)
            playerVelocity = delta / Time.deltaTime;
        prevPlayerPos = player.position;
    }

    void BeginChase(DetectionVisibility vis)
    {
        // Si ya tiene al jugador a tiro, NUNCA entra a Alert: ataque inmediato.
        if (player != null && Vector3.Distance(transform.position, player.position) <= attackRange + 0.5f
            && attackCooldownTimer <= 0f && postAttackWalkTimer <= 0f)
        {
            SetState(State.Attacking);
            return;
        }

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

        // ── Stalking proximity audio ────────────────────────────────────────
        // Si el breath está activo, modulamos su volumen por proximidad SIN LoS:
        // cuanto más cerca está el jugador y MENOS lo ve el minotauro, más fuerte
        // se escucha la respiración. Genera la tensión de "está aquí cerca pero no
        // lo vemos". Si el jugador SÍ tiene LoS, breath al volumen base.
        if (idleBreathActive && minotaurSource != null && stalkAudioDistance > 0f && player != null)
        {
            bool seesPlayer = detection != null && detection.HasLineOfSight;
            float dist = Vector3.Distance(transform.position, player.position);
            // proximity 0..1: 0 lejos / fuera de rango, 1 pegado.
            float proximity = 1f - Mathf.Clamp01(dist / stalkAudioDistance);
            // Solo aplica el boost si el monstruo NO está siendo visto (acechando).
            float boost = seesPlayer ? 1f : Mathf.Lerp(1f, Mathf.Max(1f, stalkAudioMaxBoost), proximity);
            float wanted = Mathf.Clamp01(idleBreathVolume * boost);
            // Smoothing exponencial para que no haya saltos audibles.
            minotaurSource.volume = Mathf.MoveTowards(minotaurSource.volume, wanted, 1.5f * Time.deltaTime);
        }
    }

    void TickIdle()
    {
        // Ataque directo desde Idle: si el jugador entra al attackRange estando el mino
        // parado (post-stun, post-warp con facing=false, etc.), entra a Attacking sin
        // pasar por Chase. Genera "ambush" cuando el player se acerca a un mino quieto.
        if (allowAttackFromIdle && player != null
            && attackCooldownTimer <= 0f && postAttackWalkTimer <= 0f
            && Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            SetState(State.Attacking);
            return;
        }
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
            // En espiral los waypoints son "fly-by": no se queda parado en cada uno,
            // sólo el waypoint final mantiene el dwell de 3s.
            float dwell = (inSpiralSearch && spiralQueue.Count > 0) ? 0.4f : 3f;
            if (investigateTimer >= dwell)
            {
                investigateTimer = 0f;
                if (inSpiralSearch && spiralQueue.Count > 0)
                {
                    Vector3 next = spiralQueue.Dequeue();
                    investigatePoint = next;
                    Agent.SetDestination(next);
                }
                else
                {
                    investigatePoint = null;
                    inSpiralSearch = false;
                    SetState(State.Patrol);
                }
            }
        }
    }

    /// <summary>
    /// Genera una secuencia de waypoints en espiral alrededor del último punto conocido
    /// del jugador y entra a Investigate consumiéndolos uno a uno. Cada waypoint se
    /// samplea contra el NavMesh; los que no caen en navmesh se descartan.
    /// </summary>
    void BeginSpiralSearch(Vector3 center)
    {
        spiralQueue.Clear();

        int n = Mathf.Max(0, spiralSearchPoints);
        if (n <= 0)
        {
            // Sin espiral: comportamiento legacy (ir directo al last known).
            investigatePoint = center;
            investigateTimer = 0f;
            Agent.SetDestination(center);
            inSpiralSearch = false;
            SetState(State.Investigate);
            return;
        }

        // Distribuye n puntos en una espiral logarítmica simple: ángulo crece
        // de forma escalonada (Phi golden-angle ≈ 137.5°) para evitar simetrías,
        // radio interpola lineal de min→max.
        const float goldenAngleDeg = 137.508f;
        float baseAngle = Random.value * 360f;
        for (int i = 0; i < n; i++)
        {
            float t = (n == 1) ? 0f : (float)i / (n - 1);
            float radius = Mathf.Lerp(spiralMinRadius, spiralMaxRadius, t);
            float angleDeg = baseAngle + i * goldenAngleDeg;
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 candidate = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);

            if (NavMesh.SamplePosition(candidate, out var hit, Mathf.Max(2f, spiralMinRadius), NavMesh.AllAreas))
                spiralQueue.Enqueue(hit.position);
        }

        if (spiralQueue.Count == 0)
        {
            // Ningún waypoint válido: ir al last known directamente.
            investigatePoint = center;
            investigateTimer = 0f;
            Agent.SetDestination(center);
            inSpiralSearch = false;
        }
        else
        {
            inSpiralSearch = true;
            Vector3 first = spiralQueue.Dequeue();
            investigatePoint = first;
            investigateTimer = 0f;
            Agent.SetDestination(first);
        }
        SetState(State.Investigate);
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

        // ── Lead targeting (predicción) ─────────────────────────────────────
        // Si tenemos LoS (KnownPlayerPos == player.position fresh) usamos predicción.
        // Si NO tenemos LoS pero KnownPlayerPos sigue válido, no predecimos (sería ruido).
        Vector3 basePoint = KnownPlayerPos ?? player.position;
        bool hasLoSNow = detection != null && detection.HasLineOfSight;
        if (hasLoSNow && chaseLeadTime > 0f)
        {
            Vector3 lead = playerVelocity * chaseLeadTime;
            // Solo aporta XZ (la altura la marca el navmesh).
            lead.y = 0f;
            if (lead.magnitude > chaseMaxLeadDistance)
                lead = lead.normalized * chaseMaxLeadDistance;
            basePoint += lead;
        }

        Vector3 targetPoint = basePoint;
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
    bool attackDamageApplied;
    [Tooltip("Fracción de attackDuration (0..1) a la que se aplica el daño si la animación no dispara AttackFrame.")]
    [SerializeField] float attackDamageFallbackT = 0.5f;
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
        // Fallback de daño: si pasada la mitad del ataque la anim no ha llamado a OnAttackFrame
        // (por ejemplo porque la transición Run→Attack no se reprodujo entera), aplicamos
        // el daño aquí mismo mientras el jugador siga en rango.
        if (!attackDamageApplied && attackTimer >= attackDuration * Mathf.Clamp01(attackDamageFallbackT))
        {
            if (player && Vector3.Distance(transform.position, player.position) <= attackRange + 0.5f)
                attackCollider?.OpenWindow();
            attackDamageApplied = true;
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
        attackDamageApplied = true; // evita doble daño con el fallback de TickAttack.
    }

    /// <summary>Llámalo desde EnemyAttack cuando un golpe IMPACTA al jugador.
    /// Si el jugador sigue dentro del attackRange, el minotauro REENCADENA otro ataque
    /// (no le da respiro). Si el jugador escapó, aplica el cooldown/walk normal.</summary>
    public void NotifyAttackLanded()
    {
        var diff = DifficultyManager.Instance != null ? DifficultyManager.Instance.GetRuntimeSettings() : null;

        bool playerStillClose = player != null &&
            Vector3.Distance(transform.position, player.position) <= attackRange + 0.5f;

        if (playerStillClose)
        {
            // El jugador no se ha movido del rango → encadenar otro golpe ya.
            // Cooldown mínimo para que la animación no se solape consigo misma.
            attackCooldownTimer = 0.1f;
            postAttackWalkTimer = 0f;
            // Si ya estamos atacando, dejamos que TickAttack termine y vuelva a Chase,
            // que volverá a entrar a Attacking inmediatamente al estar en rango.
            return;
        }

        // Comportamiento clásico: el jugador escapó → walk-back con cooldown.
        attackCooldownTimer = diff != null ? diff.attackCooldown : 2.5f;
        postAttackWalkTimer = diff != null ? diff.postAttackWalkSeconds : 4f;
        if (CurrentState == State.Attacking || CurrentState == State.Chase)
            SetState(State.Patrol);
    }
    #endregion

    float GetChaseMemoryDuration()
    {
        var diff = DifficultyManager.Instance != null ? DifficultyManager.Instance.GetRuntimeSettings() : null;
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

    #region Forced Respawn
    /// <summary>
    /// Mantiene dos timers (sin LoS y "demasiado lejos") y, si superan los thresholds
    /// definidos en DifficultyManager para la dificultad actual, teleporta al minotauro
    /// a un punto del NavMesh a ~forcedRespawnDistance del jugador.
    /// Sirve para evitar que el minotauro se pierda eternamente en mapas grandes
    /// o cuando el jugador rusha lejos de él.
    /// </summary>
    void TickForcedRespawn()
    {
        if (forcedRespawnCooldownTimer > 0f) forcedRespawnCooldownTimer -= Time.deltaTime;

        // No interferir con un ataque en curso ni con su "lock" post-impacto.
        if (CurrentState == State.Attacking) { noSightTimer = 0f; farFromPlayerTimer = 0f; return; }
        if (postAttackWalkTimer > 0f)        { noSightTimer = 0f; farFromPlayerTimer = 0f; return; }
        if (forcedRespawnCooldownTimer > 0f) return;
        if (DifficultyManager.Instance == null || player == null) return;

        var diff = DifficultyManager.Instance.GetRuntimeSettings();

        // ── Timer "sin LoS" ─────────────────────────────────────────────────
        // Solo cuenta cuando NO tenemos visión ni estamos persiguiendo activamente.
        bool hasLoSNow = detection != null && detection.HasLineOfSight;
        bool actingOnPlayer = CurrentState == State.Chase || CurrentState == State.Attacking;
        if (hasLoSNow || actingOnPlayer) noSightTimer = 0f;
        else                              noSightTimer += Time.deltaTime;

        // ── Timer "lejos del jugador" ───────────────────────────────────────
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        if (diff.farFromPlayerDistance > 0f && distToPlayer > diff.farFromPlayerDistance)
            farFromPlayerTimer += Time.deltaTime;
        else
            farFromPlayerTimer = 0f;

        // ── Timer "stuck" (lleva X s sin abandonar un radio pequeño) ───────
        // Solo cuenta si NO está atacando (ya validado arriba) y NO está pegado al jugador
        // (si está atacando o muy cerca, quedarse quieto es normal).
        if (!stuckInit) { stuckAnchor = transform.position; stuckInit = true; stuckTimer = 0f; }
        bool closeToPlayer = distToPlayer <= safetyDistance + 1f;
        if (closeToPlayer)
        {
            stuckTimer = 0f;
            stuckAnchor = transform.position;
        }
        else if (Vector3.Distance(transform.position, stuckAnchor) > stuckRadius)
        {
            stuckAnchor = transform.position;
            stuckTimer = 0f;
        }
        else
        {
            stuckTimer += Time.deltaTime;
        }

        // ── Triggers ────────────────────────────────────────────────────────
        bool noSightTrigger = diff.noSightRespawnSeconds > 0f && noSightTimer       >= diff.noSightRespawnSeconds;
        bool farTrigger     = diff.farFromPlayerSeconds  > 0f && farFromPlayerTimer >= diff.farFromPlayerSeconds;
        // Stuck: SIEMPRE activo (incluso en Easy) — su propósito es desencallar al bicho.
        bool stuckTrigger   = stuckRespawnSeconds > 0f && stuckTimer >= stuckRespawnSeconds;

        if (noSightTrigger || farTrigger || stuckTrigger)
        {
            // Prioridad: stuck > noSight > far. El stuck es el más "anómalo" (mino atascado),
            // y queremos resolverlo siempre con TP lejos para garantizar que respawnea en sitio nuevo.
            ForcedRespawnTrigger trigger;
            float desiredMin, desiredMax;
            if (stuckTrigger)
            {
                trigger = ForcedRespawnTrigger.Stuck;
                desiredMin = stuckMinDistance;
                desiredMax = Mathf.Max(stuckMaxDistance, stuckMinDistance + 1f);
            }
            else if (noSightTrigger)
            {
                trigger = ForcedRespawnTrigger.NoSight;
                desiredMin = noSightMinDistance;
                desiredMax = Mathf.Max(noSightMaxDistance, noSightMinDistance + 1f);
            }
            else
            {
                trigger = ForcedRespawnTrigger.FarFromPlayer;
                desiredMin = farMinDistance;
                desiredMax = Mathf.Max(farMaxDistance, farMinDistance + 1f);
            }
            // Garantiza que la distancia mínima respeta la zona prohibida del player.
            desiredMin = Mathf.Max(desiredMin, minSafeRespawnRadius + 1f);
            desiredMax = Mathf.Max(desiredMax, desiredMin + 1f);

            if (TryForcedRespawnNearPlayer(desiredMin, desiredMax, trigger))
            {
                noSightTimer = 0f;
                farFromPlayerTimer = 0f;
                stuckTimer = 0f;
                stuckAnchor = transform.position;
                forcedRespawnCooldownTimer = ResolveForcedRespawnCooldown();
            }
        }
    }

    /// <summary>
    /// Cooldown adaptativo: si el jugador está cerca del portal activo, se reduce para
    /// aumentar la presión en el tramo final del nivel.
    /// </summary>
    float ResolveForcedRespawnCooldown()
    {
        if (player == null) return forcedRespawnCooldown;
        if (PortalManager.Instance == null) return forcedRespawnCooldown;
        Vector3 portalPos = PortalManager.Instance.GetSelectedPosition();
        if (portalPos == Vector3.zero) return forcedRespawnCooldown;
        float distPortal = Vector3.Distance(player.position, portalPos);
        if (distPortal > portalProximityDistance) return forcedRespawnCooldown;
        // Mapeo lineal: distPortal=portalProximityDistance → cooldown normal; distPortal=0 → cooldown * portalProximityCooldownMul.
        float t = Mathf.InverseLerp(portalProximityDistance, 0f, distPortal);
        float mul = Mathf.Lerp(1f, Mathf.Clamp01(portalProximityCooldownMul), t);
        return forcedRespawnCooldown * mul;
    }

    /// <summary>
    /// Samplea N puntos XZ del NavMesh alrededor del jugador y se queda con el mejor según:
    ///   - distancia aleatoria en [minDist, maxDist] (depende del trigger),
    ///   - validación CRÍTICA de oclusión: 8 raycasts desde la cámara del player a las 8
    ///     esquinas del bounds del minotauro. Si CUALQUIER esquina queda visible (sin
    ///     occluder de layer 'Scenario' entre player y mino), el punto se descarta.
    ///   - zona segura del portal activo: descartar candidatos dentro de portalSafeRadius
    ///     del portal una vez Portal.IsActive.
    /// Tras escoger un punto, el Warp queda fijado a Y=forcedRespawnY.
    /// </summary>
    bool TryForcedRespawnNearPlayer(float minDist, float maxDist, ForcedRespawnTrigger trigger)
    {
        if (player == null) return false;

        Vector3 playerPos = player.position;
        Vector3 searchCenter = new Vector3(playerPos.x, forcedRespawnY, playerPos.z);

        Camera playerCam = Camera.main;
        float bestScore = float.NegativeInfinity;
        Vector3 bestPoint = transform.position;
        bool found = false;
        bool foundOccluded = false;

        int samples = Mathf.Max(4, forcedRespawnSamples);
        float sampleTol = Mathf.Max(forcedRespawnVerticalTolerance, forcedRespawnTolerance);
        float targetDist = (minDist + maxDist) * 0.5f;

        for (int i = 0; i < samples; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float radius = Random.Range(minDist, maxDist);
            Vector3 candidate = searchCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            if (!NavMesh.SamplePosition(candidate, out var hit, sampleTol, NavMesh.AllAreas))
                continue;

            Vector3 finalPoint = new Vector3(hit.position.x, forcedRespawnY, hit.position.z);

            Vector2 dXZ = new Vector2(finalPoint.x - playerPos.x, finalPoint.z - playerPos.z);
            float distXZ = dXZ.magnitude;
            // Zona prohibida alrededor del jugador.
            if (minSafeRespawnRadius > 0f && distXZ < minSafeRespawnRadius) continue;
            // Zona prohibida alrededor del portal (si activo).
            if (IsTooCloseToActivePortal(finalPoint)) continue;

            // CRÍTICO: validar que el punto queda detrás de occluders del scenarioOccluderMask.
            bool occluded = !requireScenarioOcclusion || IsFullyOccludedByScenario(finalPoint, playerCam);
            // Si requerimos oclusión y abortamos si no la hay, descarta directamente los no ocluidos.
            if (requireScenarioOcclusion && !occluded && abortIfNoOccludedPoint) continue;

            // Scoring: prioriza ocluidos. Dentro de cada categoría, prefiere puntos no visibles
            // y cercanos a la distancia media del rango.
            float score = 0f;
            if (occluded) score += 1000f;
            bool visible = playerCam != null && IsPointVisibleToCamera(finalPoint + Vector3.up * 1.5f, playerCam);
            if (!visible) score += 100f;
            score += -Mathf.Abs(distXZ - targetDist);

            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = finalPoint;
                found = true;
                foundOccluded = occluded;
            }
        }

        if (!found) return false;
        // Política estricta: si exigimos oclusión y no encontramos un punto ocluido, abortamos.
        // El TP se vuelve a intentar en el siguiente tick una vez expire el cooldown.
        if (requireScenarioOcclusion && abortIfNoOccludedPoint && !foundOccluded) return false;

        // Si la spy cam está activa, hacemos un blink (fade rápido) para enmascarar el warp.
        // Si no está activa, ApplyTeleport corre síncronamente.
        Vector3 warpPos = bestPoint;
        System.Action doWarp = () => ApplyTeleport(warpPos);

        if (SpyCamController.Instance != null)
            SpyCamController.Instance.BlinkForTeleport(doWarp);
        else
            doWarp();

        return true;
    }

    /// <summary>
    /// Aplica el warp físicamente + reproduce SFX MinotaurTP + decide estado post-warp.
    /// Se llama dentro del callback de BlinkForTeleport para que la spy cam tape el "pop".
    /// </summary>
    void ApplyTeleport(Vector3 bestPoint)
    {
        Agent.Warp(bestPoint);
        var pos = transform.position; pos.y = forcedRespawnY; transform.position = pos;
        KnownPlayerPos = null;
        chaseMemoryTimer = 0f;

        // SFX que telegrafía la reaparición — el player escucha "algo se ha movido".
        if (minotaurTpVolume > 0f)
            AudioManager.Instance?.PlaySFX(SFXId.MinotaurTP, transform.position, minotaurTpVolume);

        if (player == null) { SetState(State.Patrol); return; }

        Vector3 toPlayer = (player.position - bestPoint); toPlayer.y = 0f;
        bool facing = forcedRespawnChasesIfFacing && toPlayer.sqrMagnitude > 0.01f &&
                      Vector3.Dot(transform.forward, toPlayer.normalized) > 0.3f;
        if (facing)
        {
            KnownPlayerPos = player.position;
            BeginChase(DetectionVisibility.Frontside);
        }
        else
        {
            SetState(State.Patrol);
        }
    }

    /// <summary>Devuelve true si el portal activo existe Y el punto está dentro de su zona segura.</summary>
    bool IsTooCloseToActivePortal(Vector3 point)
    {
        if (portalSafeRadius <= 0f) return false;
        if (PortalManager.Instance == null || !PortalManager.Instance.IsSelectedActive) return false;
        Vector3 portalPos = PortalManager.Instance.GetSelectedPosition();
        if (portalPos == Vector3.zero) return false;
        Vector2 d = new Vector2(point.x - portalPos.x, point.z - portalPos.z);
        return d.magnitude < portalSafeRadius;
    }

    /// <summary>
    /// Validación crítica del TP: lanza 8 raycasts desde la cámara del player a las 8 esquinas
    /// del bounds del minotauro APLICADO al punto candidato. Devuelve true SOLO si TODOS los
    /// raycasts impactan en colliders del scenarioOccluderMask antes de llegar al mino.
    /// Esto garantiza que la malla del mino queda completamente tapada por geometría del nivel.
    /// </summary>
    bool IsFullyOccludedByScenario(Vector3 candidate, Camera playerCam)
    {
        if (playerCam == null) return true; // sin cámara no validamos — no rompemos el TP
        if (scenarioOccluderMask.value == 0)
        {
            // Sin layer configurado, no podemos validar. Avisamos una vez y aceptamos.
            if (!scenarioMaskWarned)
            {
                Debug.LogWarning("[EnemyAIBase] scenarioOccluderMask vacío. El TP no valida oclusión — asigna el layer 'Scenario' en el inspector del minotauro.", this);
                scenarioMaskWarned = true;
            }
            return true;
        }

        Bounds localBounds = GetMinotaurLocalBounds();
        Vector3 localExtents = localBounds.extents * occlusionBoundsExpand;
        Vector3 origin = playerCam.transform.position;

        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        for (int sz = -1; sz <= 1; sz += 2)
        {
            Vector3 corner = candidate + localBounds.center +
                             new Vector3(sx * localExtents.x, sy * localExtents.y, sz * localExtents.z);
            Vector3 dir = corner - origin;
            float dist = dir.magnitude;
            if (dist < 0.01f) return false; // candidato pegado al player
            dir /= dist;
            float maxDist = Mathf.Max(0.1f, dist - occluderMargin);
            // Si CUALQUIER esquina NO está ocluida → el mino se vería al menos parcialmente.
            if (!Physics.Raycast(origin, dir, maxDist, scenarioOccluderMask, QueryTriggerInteraction.Ignore))
                return false;
        }
        return true;
    }
    bool scenarioMaskWarned;

    // Cache de los bounds locales del mino, calculados desde renderers hijos al primer uso.
    Bounds cachedLocalBounds;
    bool cachedLocalBoundsInit;
    Bounds GetMinotaurLocalBounds()
    {
        if (cachedLocalBoundsInit) return cachedLocalBounds;
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            // Fallback razonable (humanoide ~2m altura).
            cachedLocalBounds = new Bounds(Vector3.up * 1f, new Vector3(1f, 2f, 1f));
        }
        else
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            cachedLocalBounds = new Bounds(b.center - transform.position, b.size);
        }
        cachedLocalBoundsInit = true;
        return cachedLocalBounds;
    }

    static bool IsPointVisibleToCamera(Vector3 worldPoint, Camera cam)
    {
        Vector3 vp = cam.WorldToViewportPoint(worldPoint);
        if (vp.z <= 0f) return false;
        return vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
    }
    #endregion

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
        var diff = DifficultyManager.Instance.GetRuntimeSettings();
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
        var diff = DifficultyManager.Instance != null ? DifficultyManager.Instance.GetRuntimeSettings() : null;
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
        // TP del minotauro: la Y se fuerza a forcedRespawnY para que SIEMPRE caiga en la
        // cota superior del mapa (suelo del navmesh elevado), nunca en partes bajas.
        pos.y = forcedRespawnY;
        // Zona prohibida: si el punto solicitado cae dentro del radio seguro del jugador,
        // lo empujamos hacia fuera en línea recta.
        if (player != null && minSafeRespawnRadius > 0f)
        {
            Vector3 fromPlayer = pos - player.position; fromPlayer.y = 0f;
            float d = fromPlayer.magnitude;
            if (d < minSafeRespawnRadius)
            {
                Vector3 dir = d > 0.01f ? fromPlayer / d : (transform.position - player.position).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                Vector3 pushed = player.position + dir * (minSafeRespawnRadius + 1f);
                pushed.y = forcedRespawnY;
                if (UnityEngine.AI.NavMesh.SamplePosition(pushed, out var hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                    pos = new Vector3(hit.position.x, forcedRespawnY, hit.position.z);
                else
                    pos = pushed;
            }
        }
        Agent.Warp(pos);
        var p = transform.position; p.y = forcedRespawnY; transform.position = p;
    }

    public bool IsChasing => CurrentState == State.Chase || CurrentState == State.Attacking;
    public Transform Player => player;
    #endregion

    #region Gizmos
    void OnDrawGizmosSelected()
    {
        // Esfera roja: rango de impacto del ataque del minotauro.
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        // Esfera amarilla tenue: safety distance (a la que se queda en chase para no clipear).
        Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, safetyDistance);
        // Esfera morada: rango de stun por linterna.
        if (stunLampRange > 0f)
        {
            Gizmos.color = new Color(0.65f, 0.18f, 1f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, stunLampRange);
        }
        // Zona segura del portal activo (sólo en play y con portal activo).
        if (Application.isPlaying && PortalManager.Instance != null
            && PortalManager.Instance.IsSelectedActive && portalSafeRadius > 0f)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(PortalManager.Instance.GetSelectedPosition(), portalSafeRadius);
        }
    }
    #endregion
}
