using TMPro;
using UnityEngine;

/// <summary>
/// Muestra el tiempo restante del GameManager en formato mm:ss.
/// Se suscribe a GameManager.OnTimerChanged para refrescar sin polling.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TimerDisplay : MonoBehaviour
{
    [Tooltip("Texto que se muestra cuando el GameManager no existe todavía.")]
    [SerializeField] string fallbackText = "--:--";
    [Tooltip("Color cuando quedan menos de 'lowTimeThreshold' segundos.")]
    [SerializeField] Color lowTimeColor = new Color(1f, 0.3f, 0.3f, 1f);
    [Tooltip("Segundos por debajo de los cuales el texto cambia a lowTimeColor.")]
    [SerializeField] float lowTimeThreshold = 30f;

    TMP_Text text;
    Color baseColor;
    bool subscribed;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        baseColor = text.color;
        text.text = fallbackText;
    }

    void OnEnable() { TrySubscribe(); }
    void OnDisable() { Unsubscribe(); }

    void Update()
    {
        if (!subscribed) TrySubscribe();
        // Si no hay GameManager aún o no nos suscribimos, mantén el fallback.
        if (!subscribed && GameManager.Instance != null)
            UpdateText(GameManager.Instance.TimeRemaining);
    }

    void TrySubscribe()
    {
        if (subscribed) return;
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnTimerChanged += UpdateText;
        UpdateText(GameManager.Instance.TimeRemaining);
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed) return;
        if (GameManager.Instance != null) GameManager.Instance.OnTimerChanged -= UpdateText;
        subscribed = false;
    }

    void UpdateText(float secondsRemaining)
    {
        secondsRemaining = Mathf.Max(0f, secondsRemaining);
        int totalSec = Mathf.CeilToInt(secondsRemaining);
        int minutes = totalSec / 60;
        int seconds = totalSec % 60;
        text.text = $"{minutes:00}:{seconds:00}";
        text.color = secondsRemaining <= lowTimeThreshold ? lowTimeColor : baseColor;
    }
}
