using TMPro;
using UnityEngine;

/// <summary>
/// Muestra un texto "[E] Prompt" cuando el SphereCast del jugador detecta un Interactable.
/// Colócalo en un GameObject UI con un TMP_Text.
/// </summary>
public class InteractionPrompt : MonoBehaviour
{
    [SerializeField] TMP_Text promptLabel;
    [SerializeField] string keyHint = "[E]";

    [Header("Raycast (replica el del player)")]
    [SerializeField] Transform rayOrigin;
    [SerializeField] float range = 2.5f;
    [SerializeField] float radius = 0.25f;
    [SerializeField] LayerMask mask = ~0;

    void Start()
    {
        if (!rayOrigin)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p)
            {
                var cam = p.GetComponentInChildren<Camera>();
                if (cam) rayOrigin = cam.transform;
            }
        }
        if (promptLabel) promptLabel.text = "";
    }

    void Update()
    {
        if (!rayOrigin || promptLabel == null) { return; }

        if (Physics.SphereCast(rayOrigin.position, radius, rayOrigin.forward, out var hit, range, mask, QueryTriggerInteraction.Collide))
        {
            var inter = hit.collider.GetComponentInParent<Interactable>();
            if (inter != null)
            {
                promptLabel.text = $"{keyHint} {inter.Prompt}";
                return;
            }
        }
        promptLabel.text = "";
    }
}
