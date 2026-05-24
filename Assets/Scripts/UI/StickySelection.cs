using UnityEngine;
using UnityEngine.EventSystems;

/// Attach to the same GameObject as the EventSystem (or any always-active object in the menu scene).
/// If the user clicks on empty space and the current selection becomes null,
/// it restores the last valid selection so the highlighted button stays highlighted.
public class StickySelection : MonoBehaviour
{
    [SerializeField] private GameObject defaultSelection;

    private GameObject lastSelected;

    private void Start()
    {
        if (defaultSelection != null)
        {
            EventSystem.current.SetSelectedGameObject(defaultSelection);
            lastSelected = defaultSelection;
        }
    }

    private void Update()
    {
        var es = EventSystem.current;
        if (es == null) return;

        GameObject current = es.currentSelectedGameObject;

        if (current != null && current.activeInHierarchy)
        {
            lastSelected = current;
            return;
        }

        if (lastSelected != null && lastSelected.activeInHierarchy)
        {
            es.SetSelectedGameObject(lastSelected);
        }
        else if (defaultSelection != null && defaultSelection.activeInHierarchy)
        {
            es.SetSelectedGameObject(defaultSelection);
            lastSelected = defaultSelection;
        }
    }
}
