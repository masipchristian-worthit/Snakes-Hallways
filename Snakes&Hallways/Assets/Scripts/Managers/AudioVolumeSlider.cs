using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class AudioVolumeSlider : MonoBehaviour
{
    public enum Channel { Master, SFX, Music, UI }

    [SerializeField] Channel channel = Channel.Master;
    [Tooltip("Valor inicial del slider si SettingsManager aún no está disponible. 0.5 = 50%, punto medio.")]
    [Range(0f, 1f)][SerializeField] float defaultValue = 0.5f;

    Slider slider;

    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        // Default sano por si SettingsManager no está aún disponible o no tiene PlayerPrefs guardados.
        slider.SetValueWithoutNotify(defaultValue);
        slider.onValueChanged.AddListener(OnChanged);
    }

    void OnEnable()
    {
        if (SettingsManager.Instance == null) return;
        slider.SetValueWithoutNotify(GetCurrent());
    }

    float GetCurrent()
    {
        return channel switch
        {
            Channel.Master => SettingsManager.Instance.Master,
            Channel.SFX    => SettingsManager.Instance.Sfx,
            Channel.Music  => SettingsManager.Instance.Music,
            _              => SettingsManager.Instance.Ui,
        };
    }

    void OnChanged(float v)
    {
        if (SettingsManager.Instance == null) return;
        switch (channel)
        {
            case Channel.Master: SettingsManager.Instance.SetMaster(v); break;
            case Channel.SFX:    SettingsManager.Instance.SetSfx(v);    break;
            case Channel.Music:  SettingsManager.Instance.SetMusic(v);  break;
            case Channel.UI:     SettingsManager.Instance.SetUi(v);     break;
        }
    }
}
