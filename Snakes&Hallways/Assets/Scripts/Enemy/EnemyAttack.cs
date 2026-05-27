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
    [Tooltip("Fuerza del empujón al jugador en la dirección mino→player al impactar. Exagerado por default para que el golpe se sienta.")]
    [SerializeField] float knockbackForce = 14f;
    [Tooltip("Componente vertical del knockback. >0 levanta al jugador (lo despega del suelo para que no frene de golpe).")]
    [SerializeField] float knockbackUp = 4f;
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
        StopAllCoroutines();
        StartCoroutine(WindowCo());
        AudioManager.Instance?.PlaySFX(SFXId.MinotaurCharge, transform.position);
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
            Vector3 impulse = dir * knockbackForce + Vector3.up * knockbackUp;

            if (knockbackResetHorizontal)
            {
                var v = rb.linearVelocity;
                v.x = 0f; v.z = 0f;
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
