using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// "Director" AI — knows player position at all times and feeds hints to EnemyAIBase
/// at a frequency proportional to difficulty/escalation.
/// Also handles room-spawn rules and post-room repositioning.
/// </summary>
public class EnemyAIInteligent : MonoBehaviour
{
    public static EnemyAIInteligent Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] EnemyAIBase blindAI;
    [SerializeField] Camera playerCamera;

    [Header("Hint pacing (overridden by difficulty)")]
    [SerializeField] float baseHintInterval = 30f;
    [SerializeField] float minHintInterval = 6f;
    [SerializeField] float hintRadius = 12f;

    [Header("Visibility checks")]
    [SerializeField] LayerMask occluderMask;
    [SerializeField] float visibilityDot = 0.4f;

    Transform player;
    float hintTimer;
    Room currentRoom;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
        if (!playerCamera) playerCamera = Camera.main;
    }

    void Update()
    {
        if (!blindAI || !player) return;
        if (!DifficultyManager.Instance || !GameManager.Instance) return;

        var diff = DifficultyManager.Instance.GetSettings();
        float esc = DifficultyManager.Instance.GetEscalation(GameManager.Instance.PickupsCollected, GameManager.Instance.PickupsRequired);

        // Omniscience: at impossible, always know exact position.
        if (Random.value < diff.omniscience * 0.05f)
        {
            blindAI.ForceKnownPlayer(player.position);
        }

        // Hints
        hintTimer -= Time.deltaTime * diff.hintFrequencyMul * (0.5f + esc);
        if (hintTimer <= 0f)
        {
            hintTimer = Mathf.Lerp(baseHintInterval, minHintInterval, esc);
            if (!blindAI.IsChasing)
            {
                blindAI.ReceiveHint(player.position, hintRadius);
            }
        }
    }

    public void NotifyPlayerEnteredRoom(Room room)
    {
        currentRoom = room;
        if (blindAI == null) return;
        var diff = DifficultyManager.Instance.GetSettings();

        if (blindAI.IsChasing)
        {
            // Impossible to spawn inside while chasing.
            return;
        }

        float roll = Random.value;
        float chance = Mathf.Lerp(diff.roomSpawnChance, diff.roomSpawnChance * 1.6f, GetEscalation());
        if (roll <= chance)
        {
            TryRoomSpawn(room);
        }
    }

    public void NotifyPlayerExitedRoom(Room room)
    {
        if (currentRoom == room) currentRoom = null;
        if (blindAI == null) return;
        var diff = DifficultyManager.Instance.GetSettings();
        StartCoroutine(PostRoomRespawn(diff.postRoomSpawnDistance));
    }

    IEnumerator PostRoomRespawn(float radius)
    {
        yield return new WaitForSeconds(Random.Range(2f, 5f));
        if (blindAI.IsChasing) yield break;
        Vector3 origin = player.position;
        Vector2 disc = Random.insideUnitCircle.normalized * radius;
        Vector3 candidate = origin + new Vector3(disc.x, 0f, disc.y);
        if (NavMesh.SamplePosition(candidate, out var hit, radius * 0.5f, NavMesh.AllAreas))
        {
            if (!IsVisibleToPlayer(hit.position))
                blindAI.Teleport(hit.position);
        }
    }

    void TryRoomSpawn(Room room)
    {
        var pts = room.InteriorSpawnPoints;
        if (pts == null || pts.Length == 0) return;
        var shuffled = new List<Transform>(pts);
        for (int i = shuffled.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]); }
        foreach (var t in shuffled)
        {
            if (!t) continue;
            if (IsVisibleToPlayer(t.position)) continue;
            blindAI.Teleport(t.position);
            return;
        }
    }

    bool IsVisibleToPlayer(Vector3 pos)
    {
        if (!playerCamera) return false;
        Vector3 to = (pos - playerCamera.transform.position);
        float dist = to.magnitude;
        Vector3 dir = to / dist;
        if (Vector3.Dot(playerCamera.transform.forward, dir) < visibilityDot) return false;
        if (Physics.Raycast(playerCamera.transform.position, dir, dist, occluderMask, QueryTriggerInteraction.Ignore)) return false;
        return true;
    }

    float GetEscalation()
    {
        if (!GameManager.Instance || !DifficultyManager.Instance) return 0.3f;
        return DifficultyManager.Instance.GetEscalation(GameManager.Instance.PickupsCollected, GameManager.Instance.PickupsRequired);
    }
}
