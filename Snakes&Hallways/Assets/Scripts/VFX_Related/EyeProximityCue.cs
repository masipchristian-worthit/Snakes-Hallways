using UnityEngine;

/// <summary>
/// Reproduce sonidos de "ojo" (EyeProximity / EyeViscous / EyeZoom) en el AudioSource
/// dedicado mano/ojo del jugador, basándose en la cercanía y línea de visión hacia el
/// enemigo. Pensado para colgarlo del Player.
/// </summary>
public class EyeProximityCue : MonoBehaviour
{
    [SerializeField] AudioSource handEyeSource;
    [SerializeField] EnemyAIBase enemy;
    [SerializeField] EnemyDetection enemyDetection;

    [Header("Distancias")]
    [SerializeField] float proximityNear = 8f;
    [SerializeField] float proximityFar  = 25f;

    [Header("Intervalos (s)")]
    [SerializeField] float minInterval = 2.5f;
    [SerializeField] float maxInterval = 7f;

    [Header("Clips locales")]
    [SerializeField] AudioClip[] proximityClips;
    [SerializeField] AudioClip[] viscousClips;
    [SerializeField] AudioClip[] zoomClips;

    float timer;

    void Start()
    {
        if (!enemy) enemy = FindFirstObjectByType<EnemyAIBase>();
        if (!enemyDetection && enemy) enemyDetection = enemy.GetComponent<EnemyDetection>();
        if (!handEyeSource)
        {
            var pc = GetComponent<PlayerController>();
            if (pc) handEyeSource = transform.Find("AS_HandEye")?.GetComponent<AudioSource>();
        }
        timer = Random.Range(minInterval, maxInterval);
    }

    void Update()
    {
        if (!enemy || handEyeSource == null) return;
        float dist = Vector3.Distance(transform.position, enemy.transform.position);
        if (dist > proximityFar) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        float closeness = Mathf.InverseLerp(proximityFar, proximityNear, dist); // 0..1
        timer = Mathf.Lerp(maxInterval, minInterval, closeness);

        AudioClip[] pool;
        if (enemyDetection != null && enemyDetection.HasLineOfSight && zoomClips != null && zoomClips.Length > 0)
            pool = zoomClips;
        else if (closeness > 0.6f && viscousClips != null && viscousClips.Length > 0)
            pool = viscousClips;
        else if (proximityClips != null && proximityClips.Length > 0)
            pool = proximityClips;
        else
            return;

        var clip = pool[Random.Range(0, pool.Length)];
        if (clip == null) return;
        handEyeSource.pitch = Random.Range(0.95f, 1.05f);
        handEyeSource.PlayOneShot(clip, 0.6f + 0.4f * closeness);
    }
}
