using TMPro;
using UnityEngine;

/// <summary>
/// Muestra los pickups recogidos en formato "X/Y", donde Y viene de la dificultad
/// activa (GameManager.PickupsRequired). Se suscribe a OnPickupCountChanged.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class PickupCountDisplay : MonoBehaviour
{
    [Tooltip("Texto cuando el GameManager todavía no existe.")]
    [SerializeField] string fallbackText = "0/-";
    [Tooltip("Color cuando se han recogido todos los pickups requeridos.")]
    [SerializeField] Color completeColor = new Color(0.4f, 1f, 0.4f, 1f);
    [Tooltip("Formato. {0}=recogidos, {1}=requeridos.")]
    [SerializeField] string format = "{0}/{1}";

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
    }

    void TrySubscribe()
    {
        if (subscribed) return;
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnPickupCountChanged += UpdateText;
        UpdateText(GameManager.Instance.PickupsCollected, GameManager.Instance.PickupsRequired);
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed) return;
        if (GameManager.Instance != null) GameManager.Instance.OnPickupCountChanged -= UpdateText;
        subscribed = false;
    }

    void UpdateText(int collected, int required)
    {
        text.text = string.Format(format, collected, required);
        text.color = (required > 0 && collected >= required) ? completeColor : baseColor;
    }
}
