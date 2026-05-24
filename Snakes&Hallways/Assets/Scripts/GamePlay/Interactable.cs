using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    [SerializeField] string prompt = "Interact";
    [SerializeField] UnityEvent onInteract = new();

    public string Prompt => prompt;
    /// <summary>
    /// Acceso al UnityEvent para suscribirse desde código (ej: Door.Awake hace
    /// interactable.OnInteract.AddListener(Open) si no quieres cablearlo en el Inspector).
    /// </summary>
    public UnityEvent OnInteract => onInteract;

    public void Interact()
    {
        onInteract?.Invoke();
    }
}
