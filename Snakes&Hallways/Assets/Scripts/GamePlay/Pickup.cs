using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Pickup : MonoBehaviour
{
    [SerializeField] GameObject visualRoot;
    [SerializeField] AudioSource loopSource;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Collect();
    }

    /// <summary>Llamado por PlayerController.TryInteract cuando el SphereCast del Interact apunta al pickup.</summary>
    public void InteractPickup() => Collect();

    void Collect()
    {
        if (!gameObject.activeSelf) return;       // evita doble registro (trigger + interact).
        GameManager.Instance?.RegisterPickup();
        if (loopSource) loopSource.Stop();
        gameObject.SetActive(false);
    }

    public void SetActiveCandidate(bool active)
    {
        gameObject.SetActive(active);
    }
}
