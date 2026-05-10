using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EnemyAnimator : MonoBehaviour
{
    [Header("Turn settings")]
    [Tooltip("Below this angle the enemy turns gradually while moving (no animation).")]
    [SerializeField] float gradualTurnLimit = 90f;
    [Tooltip("Degrees per second when turning gradually while moving.")]
    [SerializeField] float gradualTurnSpeed = 60f;
    [Tooltip("Lock locomotion this long while playing 90-deg turn.")]
    [SerializeField] float turn90Lock = 0.6f;
    [Tooltip("Lock locomotion this long while playing 180-deg turn.")]
    [SerializeField] float turn180Lock = 1.0f;

    Animator anim;

    static readonly int HashPatrolling = Animator.StringToHash("Patrolling");
    static readonly int HashChasing    = Animator.StringToHash("Chasing");
    static readonly int HashAttack     = Animator.StringToHash("Attack");
    static readonly int HashAlert      = Animator.StringToHash("Alert");
    static readonly int HashTurn90     = Animator.StringToHash("Turn 90");
    static readonly int HashTurnNeg90  = Animator.StringToHash("Turn -90");
    static readonly int HashTurn180    = Animator.StringToHash("Turn 180");

    public bool LocomotionLocked { get; private set; }

    void Awake() { anim = GetComponent<Animator>(); }

    public void SetPatrolling(bool v) => anim.SetBool(HashPatrolling, v);
    public void SetChasing(bool v) => anim.SetBool(HashChasing, v);

    public void TriggerAttack() { StartCoroutine(Fire(HashAttack)); }
    public void TriggerAlert() { StartCoroutine(Fire(HashAlert)); AudioManager.Instance?.PlaySFX(SFXId.MinotaurNeigh, transform.position); }

    IEnumerator Fire(int hash)
    {
        anim.ResetTrigger(hash);
        anim.SetTrigger(hash);
        yield return null;
    }

    /// <summary>
    /// Decide whether to turn gradually, play 90/-90 anim, or 180 anim, based on signed angle.
    /// Returns true if locomotion should be locked (turn animation playing).
    /// </summary>
    public bool RequestTurn(float signedAngle, bool isMoving)
    {
        float abs = Mathf.Abs(signedAngle);
        if (abs <= gradualTurnLimit && isMoving)
        {
            // Gradual rotation handled by AI; no animation, no lock.
            return false;
        }

        if (abs >= 150f)
        {
            StartCoroutine(LockFor(turn180Lock));
            StartCoroutine(Fire(HashTurn180));
            return true;
        }
        // 90-ish
        StartCoroutine(LockFor(turn90Lock));
        StartCoroutine(Fire(signedAngle > 0f ? HashTurn90 : HashTurnNeg90));
        return true;
    }

    public float GetGradualTurnSpeed() => gradualTurnSpeed;
    public float GetGradualLimit() => gradualTurnLimit;

    IEnumerator LockFor(float t)
    {
        LocomotionLocked = true;
        yield return new WaitForSeconds(t);
        LocomotionLocked = false;
    }

    // Animation Event called from Run animation at the swing/charge frame.
    public void AttackFrame()
    {
        var ai = GetComponent<EnemyAIBase>();
        ai?.OnAttackFrame();
    }
}
