using UnityEngine;

/// <summary>
/// Bloquea el AnimatorController de una puerta en el frame 0 del estado por defecto
/// hasta que se llame a Open(). Diseñado para esta jerarquía típica:
///
///   Door (PADRE)            ← aquí va Animator + Door.cs (este script)
///   └── InteractZone (HIJO) ← aquí va Collider + tag "Interactable" + Interactable.cs
///
/// Door.cs auto-resuelve el Animator buscando primero en sí mismo, luego en hijos y
/// finalmente en padres, y al iniciar busca un Interactable en sus hijos y se
/// auto-suscribe a su evento OnInteract → Open(). Así NO tienes que cablear la
/// UnityEvent a mano en el Inspector.
///
/// Si tu AC_Door tiene un solo estado "Scene" con la animación de apertura como Default,
/// este script impide que se reproduzca al cargar la escena.
/// </summary>
public class Door : MonoBehaviour
{
    [Header("Animator")]
    [Tooltip("Animator de la puerta. Si se deja vacío, se busca en este GO → hijos → padres.")]
    [SerializeField] Animator anim;
    [Tooltip("Nombre del estado del AnimatorController que reproduce la apertura. En la captura del usuario se llama 'Scene'.")]
    [SerializeField] string openStateName = "Scene";
    [Tooltip("Capa del Animator donde vive openStateName.")]
    [SerializeField] int animatorLayer = 0;
    [Tooltip("Si está activo, una segunda llamada a Open() reinicia la animación desde el frame 0.")]
    [SerializeField] bool allowReopen = false;

    [Header("Interactable wiring")]
    [Tooltip("Si está activo, se busca un Interactable en este GO o en hijos y se auto-suscribe Open() a su evento OnInteract. Si lo dejas en false, conecta el UnityEvent a mano en el Inspector.")]
    [SerializeField] bool autoWireChildInteractable = true;
    [Tooltip("Interactable concreto al que suscribirse. Si se deja vacío y autoWireChildInteractable está ON, se hace GetComponentInChildren<Interactable>().")]
    [SerializeField] Interactable interactable;

    [Header("Audio (opcional)")]
    [SerializeField] AudioSource doorAudio;
    [SerializeField] AudioClip openSfx;
    [Range(0f, 1f)][SerializeField] float openSfxVolume = 1f;

    [Header("Debug")]
    [Tooltip("Solo lectura — true cuando la puerta ya ha sido abierta.")]
    [SerializeField] bool opened;
    public bool IsOpen => opened;

    // Listener generado para poder Desuscribirnos limpio en OnDestroy.
    UnityEngine.Events.UnityAction wiredListener;

    void Awake()
    {
        ResolveAnimator();
        ResolveInteractable();
    }

    void OnEnable()
    {
        if (autoWireChildInteractable && interactable != null && wiredListener == null)
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
            Debug.LogWarning($"[Door] No se encontró Animator en '{name}' ni en hijos/padres. " +
                             "Asigna uno o pon Door.cs en el GameObject que tiene el Animator.", this);
    }

    void ResolveInteractable()
    {
        if (interactable != null) return;
        if (!autoWireChildInteractable) return;
        interactable = GetComponentInChildren<Interactable>(true);
        if (interactable == null)
            interactable = GetComponentInParent<Interactable>();
    }

    // ── Estado ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Coloca el Animator en el frame 0 del estado openStateName y lo pausa con speed=0.
    /// Así, aunque el AnimatorController tenga Entry → Scene auto-transition, la animación
    /// no avanza hasta que se llame a Open().
    /// </summary>
    void LockClosed()
    {
        if (anim == null) return;
        anim.Play(openStateName, animatorLayer, 0f);
        anim.speed = 0f;
        anim.Update(0f); // fuerza una evaluación para fijar la pose del primer frame
        opened = false;
    }

    /// <summary>
    /// Llamado desde Interactable.OnInteract (UnityEvent) o desde otro script.
    /// Reproduce la animación de apertura una sola vez (a menos que allowReopen).
    /// </summary>
    public void Open()
    {
        if (anim == null) return;
        if (opened && !allowReopen) return;

        opened = true;
        anim.speed = 1f;
        anim.Play(openStateName, animatorLayer, 0f);

        if (doorAudio != null && openSfx != null)
            doorAudio.PlayOneShot(openSfx, openSfxVolume);
    }

    /// <summary>
    /// Vuelve a bloquear la puerta en su frame 0 (útil para puertas reseteables
    /// entre rondas / debug).
    /// </summary>
    public void ResetDoor() => LockClosed();
}
