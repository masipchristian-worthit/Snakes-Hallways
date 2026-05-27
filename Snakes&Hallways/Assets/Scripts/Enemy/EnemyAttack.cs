using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyAttack : MonoBehaviour
{
    [Header("Daño")]
    [SerializeField] float windowDuration = 0.35f;
    [Tooltip("Daño aplicado al jugador en cada golpe. Si su PlayerHealth llega a 0 muere.")]
    [SerializeField] float damage = 35f;
    [Tooltip("Si está activo y no se encuentra PlayerHealth en el target, mata al jugador instantáneamente (comportamiento original).")]
    [SerializeField] bool fallbackToInstantKill = true;

    [Header("Knockback")]
    [Tooltip("Fuerza HORIZONTAL del empujón al jugador en la dirección mino→player. Es el componente principal del knockback.")]
    [SerializeField] float knockbackForce = 14f;
    [Tooltip("Componente vertical (MUY pequeño) del knockback. SOLO se aplica si el player está grounded (no saltando/en el aire) — apenas un toquecito para vender el golpe sin que el player salga volando hacia arriba.")]
    [SerializeField] float knockbackUpGrounded = 0.4f;
    [Tooltip("Si está activo, el knockback se aplica con ForceMode.VelocityChange (más consistente sin importar la masa del jugador).")]
    [SerializeField] bool knockbackUseVelocityChange = true;
    [Tooltip("Si está activo, antes de aplicar el impulso se resetea la velocidad horizontal del jugador para que el knockback no sume sobre el movimiento previo (sensación más limpia).")]
    [SerializeField] bool knockbackResetHorizontal = true;

    [Header("Feedback")]
    [Tooltip("Intensidad del CameraShake al CONECTAR el golpe (no al swing). Fuerte por defecto. 0 = desactivado.")]
    [Range(0f, 2f)][SerializeField] float hitShakeIntensity = 0.85f;
    [Tooltip("Duración del shake al conectar.")]
    [SerializeField] float hitShakeDuration = 0.35f;

    Collider col;

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        col.enabled = false;
        if (gameObject.tag != "EnemyAttack") gameObject.tag = "EnemyAttack";
    }

    public void OpenWindow()
    {
        // SFX del swing va EXCLUSIVAMENTE en el AnimationEvent 'AttackSfx' del clip Attack
        // (EnemyAnimator.AttackSfx). Si lo reprodujéramos aquí, el fallback de TickAttack
        // sonaría también cuando el mino "intenta atacar" sin que la animación se ejecute,
        // dando la sensación fantasma de un golpe inexistente.
        StopAllCoroutines();
        StartCoroutine(WindowCo());
    }

    IEnumerator WindowCo()
    {
        col.enabled = true;
        yield return new WaitForSeconds(windowDuration);
        col.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Aplicar daño
        var hp = other.GetComponentInParent<PlayerHealth>();
        if (hp != null)
            hp.TakeDamage(damage);
        else if (fallbackToInstantKill)
            GameManager.Instance?.TriggerGameOver();

        // ── Knockback al jugador ─────────────────────────────────────────────
        // Empuje en la dirección mino→player (XZ) + componente vertical opcional.
        // Usa el Rigidbody del player en su jerarquía padre.
        var rb = other.GetComponentInParent<Rigidbody>();
        if (rb != null && knockbackForce > 0f)
        {
            // Origen del impulso = posición del minotauro (parent del attack collider).
            // Si el collider está montado en un anchor distinto, usamos transform.position del propio collider como fallback.
            Vector3 origin = transform.position;
            var ai = GetComponentInParent<EnemyAIBase>();
            if (ai != null) origin = ai.transform.position;

            Vector3 to = rb.worldCenterOfMass - origin;
            to.y = 0f;
            Vector3 dir = to.sqrMagnitude > 0.001f ? to.normalized : transform.forward;

            // Vertical SOLO si el player está grounded — si está en el aire/saltando, no añadimos
            // bump vertical para no joder la caída ni convertir el golpe en un mini-salto.
            var pc = rb.GetComponentInParent<PlayerController>();
            bool playerGrounded = pc != null ? pc.IsGrounded : true; // si no hay PC, conservador
            float upComponent = playerGrounded ? knockbackUpGrounded : 0f;
            Vector3 impulse = dir * knockbackForce + Vector3.up * upComponent;

            if (knockbackResetHorizontal)
            {
                var v = rb.linearVelocity;
                v.x = 0f; v.z = 0f;
                // Si está en el aire, NO tocamos el componente vertical — preservar caída.
                if (!playerGrounded)
                {
                    // mantener v.y intacto
                }
                rb.linearVelocity = v;
            }
            rb.AddForce(impulse, knockbackUseVelocityChange ? ForceMode.VelocityChange : ForceMode.Impulse);
        }

        // ── Camera shake al CONECTAR el golpe ────────────────────────────────
        // Distinto al shake de pisadas: es uniforme (no depende de distancia, el jugador
        // ya está literalmente encima del mino) y fuerte para vender el impacto.
        if (hitShakeIntensity > 0f)
            CameraShake.ShakeUniform(hitShakeIntensity, hitShakeDuration);

        // Notificamos al cerebro del enemigo: entra en cooldown + walk forzado.
        var aiBase = GetComponentInParent<EnemyAIBase>();
        aiBase?.NotifyAttackLanded();
        col.enabled = false; // cierra la ventana inmediatamente — un golpe por ventana.
    }
}
