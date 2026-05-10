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
        GameManager.Instance?.RegisterPickup();
        Collect();
    }

    void Collect()
    {
        if (loopSource) loopSource.Stop();
        gameObject.SetActive(false);
    }

    public void SetActiveCandidate(bool active)
    {
        gameObject.SetActive(active);
    }
}
