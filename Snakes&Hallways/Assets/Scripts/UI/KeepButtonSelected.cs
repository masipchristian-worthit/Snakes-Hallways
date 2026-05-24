using UnityEngine;
using UnityEngine.EventSystems;

public class KeepButtonSelected : MonoBehaviour
{
    [Tooltip("Botón al que se vuelve si el usuario clickea fuera o deselecciona.")]
    [SerializeField] GameObject defaultSelected;

    GameObject lastSelected;

    void OnEnable()
    {
        if (defaultSelected != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(defaultSelected);
            lastSelected = defaultSelected;
        }
    }

    void Update()
    {
        var es = EventSystem.current;
        if (es == null) return;

        var current = es.currentSelectedGameObject;

        if (current != null && current.activeInHierarchy)
        {
            lastSelected = current;
            return;
        }

        var fallback = (lastSelected != null && lastSelected.activeInHierarchy) ? lastSelected : defaultSelected;
        if (fallback != null && fallback.activeInHierarchy)
            es.SetSelectedGameObject(fallback);
    }
}
