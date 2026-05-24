using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyAttack : MonoBehaviour
{
    [SerializeField] float windowDuration = 0.35f;
    [Tooltip("Daño aplicado al jugador en cada golpe. Si su PlayerHealth llega a 0 muere.")]
    [SerializeField] float damage = 35f;
    [Tooltip("Si está activo y no se encuentra PlayerHealth en el target, mata al jugador instantáneamente (comportamiento original).")]
    [SerializeField] bool fallbackToInstantKill = true;

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
        var hp = other.GetComponentInParent<PlayerHealth>();
        if (hp != null)
            hp.TakeDamage(damage);
        else if (fallbackToInstantKill)
            GameManager.Instance?.TriggerGameOver();

        // Notificamos al cerebro del enemigo: entra en cooldown + walk forzado.
        var ai = GetComponentInParent<EnemyAIBase>();
        ai?.NotifyAttackLanded();
        col.enabled = false; // cierra la ventana inmediatamente — un golpe por ventana.
    }
}
