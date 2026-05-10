using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Room : MonoBehaviour
{
    [Header("Spawn points inside the room (out of player sight ideally).")]
    [SerializeField] Transform[] interiorSpawnPoints;

    public Transform[] InteriorSpawnPoints => interiorSpawnPoints;
    public Bounds Area => GetComponent<Collider>().bounds;

    void Reset() { GetComponent<Collider>().isTrigger = true; gameObject.tag = "Room"; }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        EnemyAIInteligent.Instance?.NotifyPlayerEnteredRoom(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        EnemyAIInteligent.Instance?.NotifyPlayerExitedRoom(this);
    }
}
