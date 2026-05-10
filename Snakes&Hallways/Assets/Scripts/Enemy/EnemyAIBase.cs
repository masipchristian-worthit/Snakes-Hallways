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
    [SerializeField] List<Transform> patrolPoints = new();
    [SerializeField] float patrolWaitTime = 2.5f;
    [SerializeField] float arriveDistance = 1.2f;

    [Header("Speeds")]
    [SerializeField] float patrolSpeed = 2.2f;
    [SerializeField] float chaseSpeed = 5.2f;
    [SerializeField] float investigateSpeed = 3f;

    [Header("Detection thresholds")]
    [SerializeField, Range(0f, 1f)] float alertThreshold = 0.45f;
    [SerializeField, Range(0f, 1f)] float chaseThreshold = 0.85f;

    [Header("Attack")]
    [SerializeField] float attackRange = 2.5f;
    [SerializeField] EnemyAttack attackCollider;

    [Header("Front-detection behavior (back-detection branch)")]
    [Tooltip("If the enemy first sees the player from behind, chance to charge directly without alerting.")]
    [SerializeField, Range(0f, 1f)] float chargeFromBackChance = 0.6f;

    [Header("Refs")]
    [SerializeField] EnemyAnimator enemyAnim;
    [SerializeField] EnemyDetection detection;

    public State CurrentState { get; private set; } = State.Patrol;
    public NavMeshAgent Agent { get; private set; }
    public Vector3? KnownPlayerPos { get; private set; }
    public float DifficultyEscalation { get; private set; }

    Transform player;
    int patrolIdx;
    float patrolWaitTimer;
    Vector3? investigatePoint;
    float investigateTimer;
    float idleSfxTimer;

    void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        if (!enemyAnim) enemyAnim = GetComponent<EnemyAnimator>();
        if (!detection) detection = GetComponent<EnemyDetection>();
    }

    void OnEnable() { EnemyDetection.NoiseHeard += OnNoise; }
    void OnDisable() { EnemyDetection.NoiseHeard -= OnNoise; }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
        SetState(patrolPoints.Count > 0 ? State.Patrol : State.Idle);
    }

    void Update()
    {
        DifficultyEscalation = ResolveEscalation();

        // Vision -> Alert/Chase escalation
        if (detection && detection.HasLineOfSight)
        {
            KnownPlayerPos = player ? player.position : KnownPlayerPos;
            if (CurrentState != State.Chase && CurrentState != State.Attacking)
            {
                if (detection.Score >= chaseThreshold)
                {
                    BeginChase(detection.Visibility);
                }
                else if (detection.Score >= alertThreshold && CurrentState != State.Alert)
                {
                    if (detection.Visibility == DetectionVisibility.Frontside)
                        SetState(State.Alert);
                    else if (detection.Visibility == DetectionVisibility.Backside)
                        BeginChase(DetectionVisibility.Backside);
                }
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
        CurrentState = s;
        switch (s)
        {
            case State.Patrol: Agent.speed = patrolSpeed; Agent.isStopped = false; break;
            case State.Investigate: Agent.speed = investigateSpeed; Agent.isStopped = false; break;
            case State.Alert: Agent.isStopped = true; enemyAnim?.TriggerAlert(); break;
            case State.Chase:
                {
                    var diff = DifficultyManager.Instance ? DifficultyManager.Instance.GetSettings().chaseSpeedMul : 1f;
                    Agent.speed = chaseSpeed * diff;
                    Agent.isStopped = false;
                    AudioManager.Instance?.PlaySFX(SFXId.MinotaurDetect, transform.position);
                    AudioManager.Instance?.PlayMusic(MusicId.Chase);
                    break;
                }
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
        idleSfxTimer -= Time.deltaTime;
        if (idleSfxTimer <= 0f)
        {
            idleSfxTimer = Random.Range(6f, 12f);
            if (CurrentState == State.Patrol || CurrentState == State.Idle)
                AudioManager.Instance?.PlaySFX(SFXId.MinotaurIdleBreath, transform.position);
        }
    }

    void TickIdle()
    {
        if (patrolPoints.Count > 0) SetState(State.Patrol);
    }

    void TickPatrol()
    {
        if (patrolPoints.Count == 0) return;
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
        // Use known pos (refreshed every frame if visible).
        Vector3 dest = KnownPlayerPos ?? player.position;
        Agent.SetDestination(dest);
        // Detect attack range — animation event 'AttackFrame' will fire actual hit.
        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            // Don't stop — the charge keeps moving. Just open the attack window via anim event.
            // We let TickAttack handle this transient state.
            SetState(State.Attacking);
        }
    }

    float attackTimer;
    void TickAttack()
    {
        attackTimer += Time.deltaTime;
        if (player) Agent.SetDestination(player.position); // keep charging through.
        if (attackTimer >= 0.8f)
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
    #endregion

    void UpdateAnimatorBools()
    {
        if (!enemyAnim) return;
        bool patrolling = CurrentState == State.Patrol || CurrentState == State.Investigate;
        bool chasing = CurrentState == State.Chase || CurrentState == State.Attacking;
        enemyAnim.SetPatrolling(patrolling);
        enemyAnim.SetChasing(chasing);
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

        if (enemyAnim.LocomotionLocked)
        {
            Agent.isStopped = true;
            return;
        }

        bool playedTurn = enemyAnim.RequestTurn(signed, isMoving);
        if (playedTurn)
        {
            Agent.isStopped = true;
        }
        else
        {
            // Gradual turn while moving (<90)
            if (isMoving)
            {
                float maxStep = enemyAnim.GetGradualTurnSpeed() * Time.deltaTime;
                float step = Mathf.Clamp(signed, -maxStep, maxStep);
                transform.Rotate(0f, step, 0f, Space.World);
            }
            Agent.isStopped = (CurrentState == State.Alert || CurrentState == State.Idle);
        }
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
