using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class DamageVignette : MonoBehaviour
{
    [System.Serializable]
    public class Threshold
    {
        [Tooltip("Se activa cuando CurrentHP < maxHPTrigger.")]
        public float maxHPTrigger;
        public Color color = Color.yellow;
        [Range(0f, 1f)] public float baseAlpha = 0.4f;
        [Range(0f, 1f)] public float pulseAmount = 0.2f;
        public float pulseSpeed = 1f;
    }

    [Header("Referencias")]
    [SerializeField] PlayerHealth playerHealth;
    [SerializeField] Image vignetteImage;

    [Header("Thresholds (ordenados de mayor a menor HP)")]
    [SerializeField] Threshold yellowState = new Threshold { maxHPTrigger = 60f, color = new Color(1f, 0.85f, 0.2f), baseAlpha = 0.25f, pulseAmount = 0.15f, pulseSpeed = 0.6f };
    [SerializeField] Threshold orangeState = new Threshold { maxHPTrigger = 45f, color = new Color(1f, 0.5f, 0.1f), baseAlpha = 0.4f, pulseAmount = 0.25f, pulseSpeed = 1.2f };
    [SerializeField] Threshold redState   = new Threshold { maxHPTrigger = 30f, color = new Color(1f, 0.1f, 0.1f), baseAlpha = 0.6f, pulseAmount = 0.35f, pulseSpeed = 2.2f };

    [Header("Hit Flash")]
    [SerializeField] float hitFlashAlpha = 0.85f;
    [SerializeField] float hitFlashTime  = 0.18f;

    Threshold activeState;
    Coroutine flashCo;

    void Awake()
    {
        if (!vignetteImage) vignetteImage = GetComponent<Image>();
        var c = vignetteImage.color; c.a = 0f; vignetteImage.color = c;
        vignetteImage.enabled = false;

        if (!playerHealth)
        {
            var p = GameObject.FindWithTag("Player");
            if (p) playerHealth = p.GetComponentInChildren<PlayerHealth>();
        }
    }

    void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
            playerHealth.OnDamaged += HandleDamaged;
            HandleHealthChanged(playerHealth.CurrentHP, playerHealth.MaxHP);
        }
    }

    void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
            playerHealth.OnDamaged -= HandleDamaged;
        }
        if (flashCo != null) StopCoroutine(flashCo);
    }

    void Update()
    {
        if (vignetteImage == null || activeState == null || flashCo != null) return;
        float t = Time.time * activeState.pulseSpeed;
        float wave = (Mathf.Sin(t) * 0.5f + 0.5f) * activeState.pulseAmount;
        float a = Mathf.Clamp01(activeState.baseAlpha + wave - activeState.pulseAmount * 0.5f);
        var c = activeState.color; c.a = a; vignetteImage.color = c;
    }

    Threshold GetThreshold(float hp)
    {
        if (hp < redState.maxHPTrigger)    return redState;
        if (hp < orangeState.maxHPTrigger) return orangeState;
        if (hp < yellowState.maxHPTrigger) return yellowState;
        return null;
    }

    void HandleHealthChanged(float current, float max)
    {
        var next = GetThreshold(current);
        if (next == activeState) return;
        activeState = next;
        if (vignetteImage == null) return;

        if (activeState == null)
        {
            var c = vignetteImage.color; c.a = 0f; vignetteImage.color = c;
            vignetteImage.enabled = false;
        }
        else
        {
            vignetteImage.enabled = true;
            var c = activeState.color; c.a = activeState.baseAlpha; vignetteImage.color = c;
        }
    }

    void HandleDamaged(float amount)
    {
        if (vignetteImage == null) return;
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        vignetteImage.enabled = true;
        Color baseCol = activeState != null ? activeState.color : new Color(1f, 0.2f, 0.2f);
        float backAlpha = activeState != null ? activeState.baseAlpha : 0f;
        vignetteImage.color = new Color(baseCol.r, baseCol.g, baseCol.b, hitFlashAlpha);

        float t = 0f;
        while (t < hitFlashTime)
        {
            t += Time.deltaTime;
            float k = t / hitFlashTime;
            float a = Mathf.Lerp(hitFlashAlpha, backAlpha, k);
            vignetteImage.color = new Color(baseCol.r, baseCol.g, baseCol.b, a);
            yield return null;
        }
        if (activeState == null) vignetteImage.enabled = false;
        flashCo = null;
    }
}
