using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class AudioVolumeSlider : MonoBehaviour
{
    public enum Channel { Master, SFX, Music, UI }

    [SerializeField] Channel channel = Channel.Master;

    Slider slider;

    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
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
