using UnityEngine;

/// <summary>
/// Reproduce indicios de "ojo" basados en la cercanía al enemigo:
///  - EyeViscous: a BAJO volumen, sobre el AudioSource de la mano del player.
///  - EyeZoom: cuando hay línea de visión directa con el enemigo.
/// EyeProximity ha sido descartado.
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

    [Header("Volumen EyeViscous")]
    [Range(0f, 1f)][SerializeField] float viscousBaseVolume = 0.25f;
    [Range(0f, 1f)][SerializeField] float viscousMaxVolume = 0.5f;

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

        bool hasLoS = enemyDetection != null && enemyDetection.HasLineOfSight;
        SFXId id = hasLoS ? SFXId.EyeZoom : SFXId.EyeViscous;
        float vol = hasLoS ? 1f : Mathf.Lerp(viscousBaseVolume, viscousMaxVolume, closeness);
        AudioManager.Instance?.PlaySFXVariant(id, 0, transform.position, vol, handEyeSource);
    }
}
