using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Reproduce una secuencia de líneas con efecto typewriter, beeps por carácter,
/// pausas en signos de puntuación, skip/avance por click o tecla, y un UnityEvent
/// final pensado para enganchar a SceneWarp.Warp().
///
/// Uso típico:
///   1. GameObject con Canvas + TMP_Text + (opcional) CanvasGroup raíz.
///   2. Añade este script, asigna `label` (TMP_Text) y rellena `lines`.
///   3. En `onDialogueEnd` engancha SceneWarp.Warp() de un GameObject con SceneWarp.
///
/// Hay dos "iframes" (ventanas de bloqueo de input) para que no se salte sin querer:
///   • startInputLock  – al empezar una línea (evita que el click anterior cuente como skip).
///   • completeInputLock – al completar la línea con click (evita auto-avanzar instantáneo).
/// </summary>
[DisallowMultipleComponent]
public class DialogueSequence : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] TMP_Text label;
    [Tooltip("Indicador opcional de 'pulsa para continuar'. Se activa al terminar una línea.")]
    [SerializeField] GameObject continueIndicator;
    [Tooltip("CanvasGroup opcional para fade-in al arrancar.")]
    [SerializeField] CanvasGroup canvasGroup;

    [Header("Líneas")]
    [TextArea(2, 6)]
    [SerializeField] string[] lines;

    [Header("Typewriter")]
    [Tooltip("Caracteres por segundo durante el typewriter.")]
    [SerializeField] float charsPerSecond = 35f;
    [Tooltip("Multiplicador de delay al encontrar coma, punto y coma o dos puntos.")]
    [SerializeField] float softPauseMultiplier = 6f;
    [Tooltip("Multiplicador de delay al encontrar punto, exclamación o interrogación.")]
    [SerializeField] float hardPauseMultiplier = 14f;
    [Tooltip("Caracteres tratados como pausa suave.")]
    [SerializeField] string softPauseChars = ",;:";
    [Tooltip("Caracteres tratados como pausa fuerte.")]
    [SerializeField] string hardPauseChars = ".!?…";

    [Header("Avance")]
    [Tooltip("Si está activo, las líneas avanzan solas tras autoAdvanceDelay.")]
    [SerializeField] bool autoAdvance = false;
    [SerializeField] float autoAdvanceDelay = 1.5f;
    [Tooltip("Tiempo (s) al iniciar línea en el que se ignora input (anti double-click).")]
    [SerializeField] float startInputLock = 0.15f;
    [Tooltip("Tiempo (s) tras completar una línea con skip en el que se ignora input.")]
    [SerializeField] float completeInputLock = 0.25f;

    [Header("Beeps")]
    [SerializeField] SFXId beepSfx = SFXId.UITextVoice;
    [Tooltip("Reproduce un beep cada N caracteres impresos.")]
    [SerializeField, Min(1)] int beepEvery = 2;
    [Tooltip("No suena beep en espacios y signos de puntuación.")]
    [SerializeField] bool skipBeepOnPunctuation = true;
    [SerializeField, Range(0f, 1f)] float beepVolume = 0.6f;

    [Header("SFX avance")]
    [Tooltip("SFX al hacer click/skip para completar una línea o avanzar a la siguiente.")]
    [SerializeField] SFXId advanceSfx = SFXId.UISelect;
    [SerializeField, Range(0f, 1f)] float advanceVolume = 1f;

    [Header("Inicio")]
    [SerializeField] bool playOnStart = true;
    [SerializeField] float startDelay = 0.25f;
    [SerializeField] float fadeInTime = 0.4f;

    [Header("Eventos")]
    public UnityEvent onDialogueStart;
    public UnityEvent onLineStart;
    public UnityEvent onLineComplete;
    [Tooltip("Engancha aquí SceneWarp.Warp() para encadenar fade + carga de escena.")]
    public UnityEvent onDialogueEnd;

    int currentLine;
    bool isTyping;
    bool lineComplete;
    bool sequenceFinished;
    float inputUnlockTime;
    Coroutine typeRoutine;
    string fullLineText;

    void Reset()
    {
        label = GetComponentInChildren<TMP_Text>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Start()
    {
        if (continueIndicator != null) continueIndicator.SetActive(false);
        if (label != null) label.text = string.Empty;
        if (playOnStart) StartCoroutine(StartRoutine());
    }

    void Update()
    {
        if (sequenceFinished) return;
        if (Time.unscaledTime < inputUnlockTime) return;
        if (!ConsumeAdvanceInput()) return;

        if (isTyping)
        {
            // Skip de la línea actual: NO suena accept (solo se está acelerando el typewriter).
            CompleteLineInstantly();
        }
        else
        {
            // Avance real a la siguiente línea: aquí sí suena accept.
            AudioManager.Instance?.PlaySFX2D(advanceSfx, advanceVolume);
            AdvanceLine();
        }
    }

    bool ConsumeAdvanceInput()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame
                        || kb.enterKey.wasPressedThisFrame
                        || kb.numpadEnterKey.wasPressedThisFrame
                        || kb.eKey.wasPressedThisFrame))
            return true;

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;

        var gp = Gamepad.current;
        if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame))
            return true;

        return false;
    }

    IEnumerator StartRoutine()
    {
        if (canvasGroup != null && fadeInTime > 0f)
        {
            canvasGroup.alpha = 0f;
            float t = 0f;
            while (t < fadeInTime)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(t / fadeInTime);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

        onDialogueStart?.Invoke();
        currentLine = 0;
        PlayLine(currentLine);
    }

    public void PlayLine(int index)
    {
        if (lines == null || lines.Length == 0 || index < 0 || index >= lines.Length)
        {
            FinishSequence();
            return;
        }

        currentLine = index;
        fullLineText = lines[index] ?? string.Empty;
        if (typeRoutine != null) StopCoroutine(typeRoutine);
        typeRoutine = StartCoroutine(TypeLineRoutine(fullLineText));
    }

    IEnumerator TypeLineRoutine(string text)
    {
        if (continueIndicator != null) continueIndicator.SetActive(false);
        isTyping = true;
        lineComplete = false;
        inputUnlockTime = Time.unscaledTime + startInputLock;
        onLineStart?.Invoke();

        if (label != null) label.text = string.Empty;
        float baseDelay = charsPerSecond > 0f ? 1f / charsPerSecond : 0f;
        int printedSinceBeep = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // Skip TMP rich-text tags (<color=...>, <b>, etc.) sin imprimir letra a letra.
            if (c == '<')
            {
                int close = text.IndexOf('>', i);
                if (close > i)
                {
                    if (label != null) label.text += text.Substring(i, close - i + 1);
                    i = close;
                    continue;
                }
            }

            if (label != null) label.text += c;

            bool isPunct = IsPunctuation(c);
            if (!char.IsWhiteSpace(c) && !(skipBeepOnPunctuation && isPunct))
            {
                printedSinceBeep++;
                if (printedSinceBeep >= beepEvery)
                {
                    AudioManager.Instance?.PlaySFX2D(beepSfx, beepVolume);
                    printedSinceBeep = 0;
                }
            }

            float delay = baseDelay;
            if (hardPauseChars.IndexOf(c) >= 0) delay *= hardPauseMultiplier;
            else if (softPauseChars.IndexOf(c) >= 0) delay *= softPauseMultiplier;

            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        }

        isTyping = false;
        lineComplete = true;
        if (continueIndicator != null) continueIndicator.SetActive(true);
        onLineComplete?.Invoke();

        if (autoAdvance)
        {
            yield return new WaitForSecondsRealtime(autoAdvanceDelay);
            if (lineComplete) AdvanceLine();
        }
    }

    void CompleteLineInstantly()
    {
        if (typeRoutine != null) StopCoroutine(typeRoutine);
        if (label != null) label.text = fullLineText;
        isTyping = false;
        lineComplete = true;
        inputUnlockTime = Time.unscaledTime + completeInputLock;
        if (continueIndicator != null) continueIndicator.SetActive(true);
        onLineComplete?.Invoke();
    }

    void AdvanceLine()
    {
        int next = currentLine + 1;
        if (next >= (lines?.Length ?? 0)) FinishSequence();
        else PlayLine(next);
    }

    void FinishSequence()
    {
        if (sequenceFinished) return;
        sequenceFinished = true;
        if (continueIndicator != null) continueIndicator.SetActive(false);
        onDialogueEnd?.Invoke();
    }

    bool IsPunctuation(char c)
    {
        return softPauseChars.IndexOf(c) >= 0 || hardPauseChars.IndexOf(c) >= 0;
    }

    // ── API pública por si quieres dispararlo desde otro script ────────────
    public void PlayFromStart()
    {
        StopAllCoroutines();
        sequenceFinished = false;
        StartCoroutine(StartRoutine());
    }

    public void SkipToEnd()
    {
        StopAllCoroutines();
        FinishSequence();
    }
}
