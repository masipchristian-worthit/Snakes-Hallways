using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] float maxHP = 100f;
    [SerializeField] float currentHP = 100f;

    [Header("Regeneración")]
    [Tooltip("Segundos sin recibir daño antes de empezar a regenerar.")]
    [SerializeField] float regenDelay = 60f;
    [Tooltip("HP por segundo durante la regeneración.")]
    [SerializeField] float regenRate = 2f;

    [Header("Invulnerabilidad")]
    [Tooltip("Tiempo en segundos tras un golpe en el que no se vuelve a recibir daño.")]
    [SerializeField] float iFrames = 0.3f;

    public float MaxHP => maxHP;
    public float CurrentHP => currentHP;
    public bool IsDead { get; private set; }

    public event Action<float, float> OnHealthChanged; // (current, max)
    public event Action<float> OnDamaged;              // amount
    public event Action OnDied;

    float lastDamageTime = -999f;
    float iFrameUntil = -1f;

    void Start()
    {
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    void Update()
    {
        if (IsDead) return;
        if (currentHP >= maxHP) return;
        if (Time.time - lastDamageTime < regenDelay) return;

        currentHP = Mathf.Min(maxHP, currentHP + regenRate * Time.deltaTime);
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;
        if (Time.time < iFrameUntil) return;

        iFrameUntil = Time.time + iFrames;
        lastDamageTime = Time.time;
        currentHP = Mathf.Max(0f, currentHP - amount);

        // SFX de daño — se reproduce en la posición del player (3D) para que pase por el
        // mixer y respete el volumen Master/SFX. Si no hay AudioManager o el clip no está
        // mapeado todavía, retorna silenciosamente.
        AudioManager.Instance?.PlaySFX(SFXId.PlayerDamage, transform.position);

        OnDamaged?.Invoke(amount);
        OnHealthChanged?.Invoke(currentHP, maxHP);

        if (currentHP <= 0f)
        {
            IsDead = true;
            OnDied?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }
}
