using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class DifficultyButton : MonoBehaviour
{
    [Tooltip("Dificultad que aplica este botón al ser pulsado.")]
    [SerializeField] Difficulty targetDifficulty = Difficulty.Medium;

    [Header("Feedback visual (opcional)")]
    [SerializeField] Image highlight;
    [SerializeField] Color selectedColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] Color unselectedColor = new Color(1f, 1f, 1f, 0.3f);

    Button btn;

    void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClick);
    }

    void OnEnable() => RefreshVisual();

    void OnClick()
    {
        if (DifficultyManager.Instance == null) return;
        DifficultyManager.Instance.SetDifficulty(targetDifficulty);
        AudioManager.Instance?.PlaySFX2D(SFXId.UISelect);

        foreach (var b in FindObjectsByType<DifficultyButton>(FindObjectsSortMode.None))
            b.RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (highlight == null || DifficultyManager.Instance == null) return;
        bool isSelected = DifficultyManager.Instance.Current == targetDifficulty;
        highlight.color = isSelected ? selectedColor : unselectedColor;
    }
}
