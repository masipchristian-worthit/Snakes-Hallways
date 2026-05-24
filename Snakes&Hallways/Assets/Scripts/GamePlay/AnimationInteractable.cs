using System.Collections;
using UnityEngine;

/// <summary>
/// Collider interactuable (via PlayerController raycast) que dispara una animación
/// en el Animator del objeto padre y deja el personaje/objeto congelado en el último frame.
///
/// Uso:
/// - Coloca este componente en el GameObject hijo que tiene el Collider con tag "Interactable".
/// - Junto a este componente debe haber un componente Interactable (se añade automáticamente).
/// - Asigna el Animator del padre (o se autocaptura con GetComponentInParent en Awake).
/// - Indica el nombre del state o el trigger a disparar.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Interactable))]
public class AnimationInteractable : MonoBehaviour
{
    [Header("Animator (en padre)")]
    [Tooltip("Animator a controlar. Si está vacío se busca con GetComponentInParent en Awake.")]
    [SerializeField] Animator targetAnimator;
    [SerializeField] int animatorLayer = 0;

    [Header("Disparo")]
    [Tooltip("Si está activo, se llama Animator.Play con stateName. Si no, se dispara el trigger.")]
    [SerializeField] bool useStateName = true;
    [SerializeField] string stateName = "Open";
    [SerializeField] string triggerName = "Open";

    [Header("Comportamiento")]
    [Tooltip("Si está activo, solo puede interactuarse una vez.")]
    [SerializeField] bool oneShot = true;
    [Tooltip("Pequeño margen al final de la animación antes de congelar (segundos).")]
    [SerializeField] float endMargin = 0.02f;
    [Tooltip("Si está activo, deshabilita el collider tras interactuar para evitar nuevos raycasts.")]
    [SerializeField] bool disableColliderAfter = true;

    Interactable interactable;
    Collider col;
    bool played;

    void Awake()
    {
        interactable = GetComponent<Interactable>();
        col = GetComponent<Collider>();
        if (targetAnimator == null) targetAnimator = GetComponentInParent<Animator>();
        if (targetAnimator == null)
            Debug.LogWarning($"[AnimationInteractable] No se encontró Animator en padres de {name}.", this);

        interactable.GetType(); // ensure component exists
        // Hookea el UnityEvent de Interactable por código (también puedes hacerlo desde el inspector).
        var so = new UnityEngine.Events.UnityAction(PlayAndFreeze);
        // Nota: si ya tienes listeners en el inspector, este se añade además, no reemplaza.
        var ev = typeof(Interactable).GetField("onInteract",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (ev != null)
        {
            var unityEvent = ev.GetValue(interactable) as UnityEngine.Events.UnityEvent;
            unityEvent?.AddListener(so);
        }
    }

    /// <summary>Reproduce la animación y la congela en el último frame.</summary>
    public void PlayAndFreeze()
    {
        if (oneShot && played) return;
        if (targetAnimator == null) return;
        played = true;

        // Asegura velocidad normal antes de lanzarla (por si quedó congelado de antes).
        targetAnimator.speed = 1f;

        if (useStateName && !string.IsNullOrEmpty(stateName))
            targetAnimator.Play(stateName, animatorLayer, 0f);
        else if (!string.IsNullOrEmpty(triggerName))
            targetAnimator.SetTrigger(triggerName);

        StopAllCoroutines();
        StartCoroutine(FreezeAtEndCo());
    }

    IEnumerator FreezeAtEndCo()
    {
        // Espera un frame para que Animator entre en el nuevo state.
        yield return null;
        yield return null;

        var info = targetAnimator.GetCurrentAnimatorStateInfo(animatorLayer);
        float clipLen = info.length;
        // Si la duración es ridículamente pequeña, intenta del próximo state (en caso de transición).
        if (clipLen <= 0.01f)
        {
            yield return null;
            info = targetAnimator.GetCurrentAnimatorStateInfo(animatorLayer);
            clipLen = info.length;
        }

        float wait = Mathf.Max(0f, clipLen - endMargin);
        if (wait > 0f) yield return new WaitForSeconds(wait);

        // Fuerza la posición final del clip y para el animator.
        var finalInfo = targetAnimator.GetCurrentAnimatorStateInfo(animatorLayer);
        targetAnimator.Play(finalInfo.fullPathHash, animatorLayer, 1f);
        targetAnimator.Update(0f);
        targetAnimator.speed = 0f;

        if (disableColliderAfter && col) col.enabled = false;
    }
}
