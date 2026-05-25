using UnityEngine;

/// <summary>
/// Bloquea el AnimatorController de una puerta en el frame 0 del estado por defecto
/// hasta que se llame a Open(). 
/// 
/// Diseñado para esta jerarquía:
///   Door (PADRE)            ← aquí va Animator + Door.cs (este script)
///   └── InteractZone (HIJO) ← asignado en el Inspector en 'interactionNode'
/// </summary>
public class Door : MonoBehaviour
{
    [Header("Animator")]
    [Tooltip("Animator de la puerta. Si se deja vacío, se busca en este GO → hijos → padres.")]
    [SerializeField] Animator anim;
    [Tooltip("Nombre del estado del AnimatorController que reproduce la apertura.")]
    [SerializeField] string openStateName = "Scene";
    [Tooltip("Capa del Animator donde vive openStateName.")]
    [SerializeField] int animatorLayer = 0;
    [Tooltip("Si está activo, una segunda llamada a Open() reinicia la animación desde el frame 0.")]
    [SerializeField] bool allowReopen = false;

    [Header("Interactable wiring")]
    [Tooltip("GameObject hijo que contiene el BoxCollider para la interacción. Asignar en Inspector.")]
    [SerializeField] GameObject interactionNode;

    // Referencia interna al componente Interactable una vez resuelto.
    private Interactable interactable;

    [Header("Audio (opcional)")]
    [Tooltip("Si está ON, reproduce vía AudioManager. Si está OFF, usa doorAudio + openSfx locales.")]
    [SerializeField] bool useAudioManagerSfx = true;
    [SerializeField] AudioSource doorAudio;
    [SerializeField] AudioClip openSfx;
    [Range(0f, 1f)][SerializeField] float openSfxVolume = 1f;

    [Header("Debug")]
    [Tooltip("Solo lectura — true cuando la puerta ya ha sido abierta.")]
    [SerializeField] bool opened;
    public bool IsOpen => opened;

    // Listener generado para poder Desuscribirnos limpio en OnDestroy.
    private UnityEngine.Events.UnityAction wiredListener;

    // Pose original colocada en la escena. Se cachea ANTES de que el Animator pueda
    // aplicar el frame 0 (que podría mover/rotar el root si la anim tiene curves de
    // posición). Sirve para restaurar la puerta a su sitio en LockClosed.
    Vector3 cachedLocalPos;
    Quaternion cachedLocalRot;
    Transform animTarget;

    void Awake()
    {
        ResolveAnimator();
        // Si el Animator está en el propio GameObject, los curves de posición/rotación
        // del clip tocan ESTE transform → cacheamos su pose local.
        animTarget = anim != null ? anim.transform : transform;
        cachedLocalPos = animTarget.localPosition;
        cachedLocalRot = animTarget.localRotation;
        ResolveInteractable();
    }

    void OnEnable()
    {
        if (interactable != null && wiredListener == null)
        {
            wiredListener = Open;
            interactable.OnInteract.AddListener(wiredListener);
        }
    }

    void OnDisable()
    {
        if (interactable != null && wiredListener != null)
        {
            interactable.OnInteract.RemoveListener(wiredListener);
        }
        wiredListener = null;
    }

    void Start()
    {
        LockClosed();
    }

    // ── Resolución ───────────────────────────────────────────────────────────
    void ResolveAnimator()
    {
        if (anim != null) return;

        anim = GetComponent<Animator>();
        if (anim != null) return;

        anim = GetComponentInChildren<Animator>(true);
        if (anim != null) return;

        anim = GetComponentInParent<Animator>();
        if (anim == null)
        {
            Debug.LogWarning($"[Door] No se encontró Animator en '{name}' ni en hijos/padres.", this);
        }
    }

    void ResolveInteractable()
    {
        if (interactionNode == null)
        {
            Debug.LogWarning($"[Door] '{name}': El campo 'interactionNode' no está asignado en el Inspector. La puerta no será interactuable.", this);
            return;
        }

        // 1. Validar o añadir BoxCollider en el nodo referenciado
        BoxCollider box = interactionNode.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = interactionNode.AddComponent<BoxCollider>();
            // Valores por defecto lógicos para un trigger de interacción
            box.size = Vector3.one;
        }

        // Forzamos que sea un trigger
        if (!box.isTrigger)
        {
            box.isTrigger = true;
        }

        // 2. Validar o añadir el script Interactable
        interactable = interactionNode.GetComponent<Interactable>();
        if (interactable == null)
        {
            interactable = interactionNode.AddComponent<Interactable>();
        }
    }

    // ── Estado ───────────────────────────────────────────────────────────────
    void LockClosed()
    {
        // APAÑO: el Animator escribe el transform del root cada frame mientras esté
        // habilitado, así que aunque restauremos pos/rot tras Update(0) él vuelve a
        // pisarlas. Solución cruda y efectiva: lo DESHABILITAMOS por completo hasta
        // que se llame a Open(). Mientras esté cerrada, la puerta se queda EXACTAMENTE
        // en la pose colocada en escena.
        if (anim != null) anim.enabled = false;
        if (animTarget != null)
        {
            animTarget.localPosition = cachedLocalPos;
            animTarget.localRotation = cachedLocalRot;
        }
        opened = false;
    }

    public void Open()
    {
        if (anim == null) return;
        if (opened && !allowReopen) return;

        opened = true;
        anim.enabled = true;       // re-habilita el Animator (lo dejó apagado LockClosed)
        anim.speed = 1f;
        anim.Play(openStateName, animatorLayer, 0f);

        // Lógica de audio: Delegación prioritaria al AudioManager
        if (useAudioManagerSfx)
        {
            if (AudioManager.Instance != null)
            {
                // Emite el sonido en la posición de la puerta para mantener coherencia espacial
                AudioManager.Instance.PlaySFX(SFXId.Door, transform.position, openSfxVolume);
            }
            else
            {
                Debug.LogWarning($"[Door] '{name}' intentó usar AudioManager para SFXId.Door, pero la instancia es nula.", this);
            }
        }
        // Fallback a componentes locales si el AudioManager está deshabilitado por Inspector
        else if (doorAudio != null && openSfx != null)
        {
            doorAudio.PlayOneShot(openSfx, openSfxVolume);
        }
    }

    public void ResetDoor() => LockClosed();
}