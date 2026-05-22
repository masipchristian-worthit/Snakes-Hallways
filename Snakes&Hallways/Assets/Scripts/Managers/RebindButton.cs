using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Botón UI para rebindear UNA binding concreta de una acción del InputActionAsset
/// configurado en el SettingsManager.
/// - actionName: nombre exacto de la action (p.ej. "Interact", "Jump").
/// - bindingIndex: índice de la binding dentro de la action (0 = primero).
/// - Pulsa el botón → escucha la siguiente tecla/pad y la asigna.
/// </summary>
[RequireComponent(typeof(Button))]
public class RebindButton : MonoBehaviour
{
    [Header("Acción a rebindear")]
    [SerializeField] string actionName = "Interact";
    [SerializeField] int bindingIndex = 0;

    [Header("UI")]
    [SerializeField] TMP_Text label;
    [SerializeField] string waitingText = "Pulsa una tecla…";

    [Header("Filtros (opcional)")]
    [Tooltip("Excluir <Mouse> en este rebind (útil para no asignar movimiento del ratón).")]
    [SerializeField] bool excludeMouse = false;
    [Tooltip("Excluir <Gamepad> en este rebind (rebinds sólo de teclado).")]
    [SerializeField] bool excludeGamepad = false;

    Button btn;
    InputActionRebindingExtensions.RebindingOperation rebindOp;

    void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(StartRebind);
    }

    void OnEnable() => RefreshLabel();

    void RefreshLabel()
    {
        if (label == null) return;
        var action = ResolveAction();
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            label.text = "—";
            return;
        }
        label.text = InputControlPath.ToHumanReadableString(
            action.bindings[bindingIndex].effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);
    }

    InputAction ResolveAction()
    {
        var sm = SettingsManager.Instance;
        return sm?.InputActions?.FindAction(actionName, throwIfNotFound: false);
    }

    void StartRebind()
    {
        var action = ResolveAction();
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return;

        if (label != null) label.text = waitingText;
        action.Disable();

        rebindOp?.Dispose();
        rebindOp = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(op => CompleteRebind(op, action))
            .OnCancel(op => CompleteRebind(op, action));

        if (excludeMouse)   rebindOp.WithControlsExcluding("<Mouse>");
        if (excludeGamepad) rebindOp.WithControlsExcluding("<Gamepad>");

        rebindOp.Start();
    }

    void CompleteRebind(InputActionRebindingExtensions.RebindingOperation op, InputAction action)
    {
        op.Dispose();
        rebindOp = null;
        action.Enable();
        SettingsManager.Instance?.PersistBindingOverrides();
        RefreshLabel();
    }

    void OnDestroy()
    {
        rebindOp?.Dispose();
        rebindOp = null;
    }
}
