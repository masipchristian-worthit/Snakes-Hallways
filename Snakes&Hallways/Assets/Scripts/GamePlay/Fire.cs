using UnityEngine;

/// <summary>
/// Asocia el clip de SFXId.Torch al AudioSource de este GameObject y lo arranca
/// sincronizado con el resto de antorchas (mismo offset en el loop) para evitar
/// comb-filtering / fase rara cuando hay varias antorchas próximas.
///
/// Uso: pon este componente en cualquier antorcha. Si no hay AudioSource, se crea uno.
/// Solo se carga UN AudioClip (Torch) en memoria, referenciado por todas las Fire.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class Fire : MonoBehaviour
{
    [SerializeField] AudioSource source;
    [Tooltip("Si está activo, registra el AudioSource en el AudioManager para arrancar en sync con el resto de antorchas.")]
    [SerializeField] bool autoStart = true;

    void Reset()
    {
        source = GetComponent<AudioSource>();
        if (source)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 1f;
        }
    }

    void Awake()
    {
        if (!source) source = GetComponent<AudioSource>();
        if (!source) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 1f;
    }

    void OnEnable()
    {
        if (!autoStart) return;
        // Espera un frame para asegurar que AudioManager.Instance está disponible.
        if (AudioManager.Instance != null) AudioManager.Instance.RegisterTorch(source);
        else StartCoroutine(DelayedRegister());
    }

    System.Collections.IEnumerator DelayedRegister()
    {
        // Reintenta brevemente por si el AudioManager se inicializa después.
        for (int i = 0; i < 30; i++)
        {
            yield return null;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.RegisterTorch(source);
                yield break;
            }
        }
    }

    void OnDisable()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.UnregisterTorch(source);
        else if (source != null && source.isPlaying) source.Stop();
    }
}
