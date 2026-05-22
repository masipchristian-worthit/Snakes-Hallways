using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class SensitivitySlider : MonoBehaviour
{
    [SerializeField] float min = 0.05f;
    [SerializeField] float max = 1.5f;

    Slider slider;

    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
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
