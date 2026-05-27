using UnityEngine;
using DG.Tweening;

/// <summary>
/// Animación de lámpara de aceite con DOTween: encendido y apagado con parpadeos —
/// simula la inestabilidad del fuego cuando una mecha prende o se extingue.
///
/// Cómo usar:
///   1. Pon este componente en el GameObject que contiene la <see cref="Light"/> de la lámpara
///      (o en un parent — autoresuelve buscando un Light en hijos).
///   2. PlayerController.ToggleLamp lo detecta automáticamente: en vez de hacer SetActive
///      al lampObject, llama a <see cref="Ignite"/> y <see cref="Extinguish"/> respectivamente.
///   3. La intensidad full se captura en Awake del Light.intensity actual — ajusta esa
///      intensidad en la Light component como siempre.
///
/// Parámetros regulables en el inspector: duración, curva de envolvente, amplitud del flicker
/// y frecuencia del flicker — por separado para encender y apagar.
/// </summary>
[DisallowMultipleComponent]
public class OilLampIgnition : MonoBehaviour
{
    [Header("Light target")]
    [Tooltip("Light que se anima. Si está vacío, se autoresuelve buscando el primer Light en hijos (incluso inactivos).")]
    [SerializeField] Light targetLight;

    [Header("Intensidad base")]
    [Tooltip("Intensidad 'encendida' (full). Si <0 (default), se captura del Light.intensity inicial en Awake. Si quieres forzar un valor concreto, pon aquí >0.")]
    [SerializeField] float fullIntensity = -1f;
    [Tooltip("Si está activo, al arrancar la escena la luz comienza apagada (intensity=0, Light.enabled=false) hasta que se llame a Ignite.")]
    [SerializeField] bool startOff = true;

    [Header("Ignite (encender)")]
    [Tooltip("Duración total de la animación de encendido (s).")]
    [SerializeField] float igniteDuration = 0.9f;
    [Tooltip("Curva envolvente del encendido. La intensidad va de 0→full siguiendo esta curva (con flicker por encima).")]
    [SerializeField] AnimationCurve igniteEnvelope = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.15f, 0.3f),
        new Keyframe(0.25f, 0.1f),
        new Keyframe(0.45f, 0.7f),
        new Keyframe(0.6f, 0.4f),
        new Keyframe(0.85f, 1.05f),
        new Keyframe(1f, 1f)
    );
    [Tooltip("Amplitud extra del flicker durante el encendido (multiplicador random sobre la curva). 0 = sin flicker, 1 = flicker fuerte.")]
    [Range(0f, 1f)][SerializeField] float igniteFlickerAmp = 0.55f;
    [Tooltip("Frecuencia del flicker (Hz aproximada).")]
    [SerializeField] float igniteFlickerFreq = 18f;

    [Header("Extinguish (apagar)")]
    [Tooltip("Duración total de la animación de apagado (s).")]
    [SerializeField] float extinguishDuration = 0.7f;
    [Tooltip("Curva envolvente del apagado. La intensidad va de full→0 siguiendo esta curva (con flicker por encima — la mecha lucha por sobrevivir antes de morir).")]
    [SerializeField] AnimationCurve extinguishEnvelope = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.2f, 0.5f),
        new Keyframe(0.35f, 0.8f),
        new Keyframe(0.55f, 0.2f),
        new Keyframe(0.7f, 0.4f),
        new Keyframe(0.9f, 0.05f),
        new Keyframe(1f, 0f)
    );
    [Range(0f, 1f)][SerializeField] float extinguishFlickerAmp = 0.45f;
    [SerializeField] float extinguishFlickerFreq = 14f;

    [Header("Audio (opcional)")]
    [Tooltip("Si se asigna, se reproduce al iniciar Ignite. Útil para un 'puff' de encendido.")]
    [SerializeField] AudioClip igniteSfx;
    [Tooltip("Si se asigna, se reproduce al iniciar Extinguish (apagado / soplido).")]
    [SerializeField] AudioClip extinguishSfx;
    [SerializeField] AudioSource sfxSource;

    Sequence currentTween;
    bool isOn;
    float perlinSeed;

    public bool IsOn => isOn;

    void Awake()
    {
        if (targetLight == null) targetLight = GetComponentInChildren<Light>(true);
        if (targetLight != null && fullIntensity < 0f) fullIntensity = targetLight.intensity;
        perlinSeed = Random.value * 1000f;

        if (startOff && targetLight != null)
        {
            targetLight.intensity = 0f;
            targetLight.enabled = false;
        }
    }

    /// <summary>Enciende la lámpara con parpadeos de ignición. Idempotente: si ya está encendida, no hace nada.</summary>
    public void Ignite()
    {
        if (targetLight == null) return;
        if (isOn) return;
        isOn = true;
        KillCurrent();
        targetLight.enabled = true;
        if (igniteSfx != null) PlayClip(igniteSfx);
        float fromI = Mathf.Max(0f, targetLight.intensity);
        currentTween = AnimateIntensity(fromI, fullIntensity, igniteDuration,
                                        igniteEnvelope, igniteFlickerAmp, igniteFlickerFreq,
                                        finalEnabled: true, finalIntensity: fullIntensity);
    }

    /// <summary>Apaga la lámpara con parpadeos de extinción. Idempotente.</summary>
    public void Extinguish()
    {
        if (targetLight == null) return;
        if (!isOn) return;
        isOn = false;
        KillCurrent();
        if (extinguishSfx != null) PlayClip(extinguishSfx);
        float fromI = targetLight.intensity;
        currentTween = AnimateIntensity(fromI, 0f, extinguishDuration,
                                        extinguishEnvelope, extinguishFlickerAmp, extinguishFlickerFreq,
                                        finalEnabled: false, finalIntensity: 0f);
    }

    public void Toggle()
    {
        if (isOn) Extinguish();
        else Ignite();
    }

    void PlayClip(AudioClip c)
    {
        if (sfxSource == null) return;
        sfxSource.PlayOneShot(c);
    }

    Sequence AnimateIntensity(float from, float to, float dur, AnimationCurve env,
                              float flickerAmp, float flickerFreq,
                              bool finalEnabled, float finalIntensity)
    {
        var seq = DOTween.Sequence();
        float t = 0f;
        seq.Append(DOTween.To(() => t, x =>
        {
            t = x;
            // Curva envolvente: 0..1 → intensidad base interpolada.
            float k = Mathf.Clamp01(env.Evaluate(x));
            float baseV = Mathf.Lerp(from, to, k);
            // Flicker con Perlin a la frecuencia objetivo. Bias 0.5 → centrado.
            float n = Mathf.PerlinNoise(perlinSeed + Time.time * flickerFreq, perlinSeed * 0.37f);
            float flicker = (n - 0.5f) * 2f;
            float val = baseV * (1f + flicker * flickerAmp);
            // No bajamos por debajo de 0.
            targetLight.intensity = Mathf.Max(0f, val);
        }, 1f, dur).SetEase(Ease.Linear));
        seq.OnComplete(() =>
        {
            targetLight.intensity = finalIntensity;
            targetLight.enabled = finalEnabled;
        });
        // Si DOTween se kill-ea a mitad (otra acción de toggle), no garantizamos estado final
        // — pero el siguiente Ignite/Extinguish lo establece correctamente al arrancar.
        return seq;
    }

    void KillCurrent()
    {
        if (currentTween != null && currentTween.IsActive())
            currentTween.Kill(false);
        currentTween = null;
    }

    void OnDestroy() => KillCurrent();
}
