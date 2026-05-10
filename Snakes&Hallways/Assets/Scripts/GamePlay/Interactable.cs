using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    [SerializeField] string prompt = "Interact";
    [SerializeField] UnityEvent onInteract;

    public string Prompt => prompt;

    public void Interact()
    {
        onInteract?.Invoke();
    }
}
