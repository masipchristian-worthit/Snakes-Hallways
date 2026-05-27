using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class SensitivitySlider : MonoBehaviour
{
    [SerializeField] float min = 0.05f;
    [SerializeField] float max = 1.5f;
    [Tooltip("Valor inicial del slider si SettingsManager aún no está disponible. 0.5 = sensibilidad similar a la de Windows por defecto.")]
    [SerializeField] float defaultValue = 0.5f;

    Slider slider;

    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        // Default sano por si SettingsManager no está aún disponible.
        slider.SetValueWithoutNotify(Mathf.Clamp(defaultValue, min, max));
        slider.onValueChanged.AddListener(OnChanged);
    }

    void OnEnable()
    {
        if (SettingsManager.Instance == null) return;
        slider.SetValueWithoutNotify(Mathf.Clamp(SettingsManager.Instance.Sensitivity, min, max));
    }

    void OnChanged(float v)
    {
        SettingsManager.Instance?.SetSensitivity(v);
    }
}
