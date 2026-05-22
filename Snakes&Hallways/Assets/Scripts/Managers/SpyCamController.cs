using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton que gestiona la "cámara espía" del enemigo.
/// - Tecla C: si el enemigo NO te ve y NO estás demasiado cerca, fade a negro y cambia
///   la cámara activa del player a la cámara del enemigo (EnemyCameraView).
/// - Otra vez C: vuelve a la cámara del player con otro fade.
/// - Si durante el modo espía el enemigo ve al player o se le acerca demasiado,
///   se sale automáticamente como si se pulsara C.
/// El bloqueo de input del player se gestiona vía IsActive (lo lee PlayerController).
/// </summary>
public class SpyCamController : MonoBehaviour
{
    public static SpyCamController Instance { get; private set; }

    [Header("Cámaras")]
    [Tooltip("Cámara principal del jugador. Si está vacía se busca por Camera.main en Start.")]
    [SerializeField] Camera playerCamera;
    [Tooltip("EnemyCameraView. Si está vacío se busca el primero en escena.")]
    [SerializeField] EnemyCameraView enemyView;

    [Header("Refs detección")]
    [Tooltip("Detección del enemigo, usada para abortar si te ve. Si está vacía se busca en EnemyCameraView.transform.")]
    [SerializeField] EnemyDetection enemyDetection;

    [Header("Fade UI")]
    [Tooltip("Image de pantalla completa (negra) en un Canvas Screen Space - Overlay con sorting alto.")]
    [SerializeField] Image fadeImage;
    [SerializeField] Color fadeColor = Color.black;
    [SerializeField] float fadeInTime = 0.35f;
    [SerializeField] float fadeOutTime = 0.45f;
    [Tooltip("Tiempo (segundos) que la pantalla se mantiene en negro antes del fade out.")]
    [SerializeField] float holdBlackTime = 0.08f;

    [Header("Reglas de uso")]
    [Tooltip("Si la distancia player↔enemigo es menor que esto, NO se puede entrar al modo espía.")]
    [SerializeField] float minSafeDistance = 12f;
    [Tooltip("Si la distancia cae por debajo de este valor con el modo espía activo, se sale automáticamente.")]
    [SerializeField] float autoExitDistance = 9f;

    [Header("Audio")]
    [SerializeField] string denySfx = "ui_deny";

    public bool IsActive { get; private set; }
    public bool IsTransitioning { get; private set; }

    Transform player;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (!playerCamera) playerCamera = Camera.main;
        if (!enemyView) enemyView = FindFirstObjectByType<EnemyCameraView>();
        if (!enemyDetection && enemyView) enemyDetection = enemyView.GetComponent<EnemyDetection>();
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;

        // Estado inicial: cámara del player ON, cámara enemigo OFF, fade limpio
        if (playerCamera) playerCamera.enabled = true;
        if (enemyView) enemyView.SetActive(false);
        SetFadeAlpha(0f);
    }

    void Update()
    {
        if (!IsActive || IsTransitioning) return;

        // Auto-exit si te ven o te acercas demasiado.
        bool seen = enemyDetection != null && enemyDetection.HasLineOfSight;
        bool tooClose = false;
        if (player && enemyView)
            tooClose = Vector3.Distance(player.position, enemyView.transform.position) < autoExitDistance;

        if (seen || tooClose)
            Exit();
    }

    public void Toggle()
    {
        if (IsTransitioning) return;
        if (IsActive) Exit();
        else TryEnter();
    }

    public void TryEnter()
    {
        if (IsActive || IsTransitioning) return;
        if (!CanEnter())
        {
            PlayUI(denySfx);
            return;
        }
        StartCoroutine(EnterRoutine());
    }

    public void Exit()
    {
        if (!IsActive || IsTransitioning) return;
        StartCoroutine(ExitRoutine());
    }

    bool CanEnter()
    {
        if (!enemyView || !playerCamera || !player) return false;
        if (enemyDetection != null && enemyDetection.HasLineOfSight) return false;
        float d = Vector3.Distance(player.position, enemyView.transform.position);
        if (d < minSafeDistance) return false;
        return true;
    }

    IEnumerator EnterRoutine()
    {
        IsTransitioning = true;
        yield return FadeTo(1f, fadeInTime);
        if (playerCamera) playerCamera.enabled = false;
        if (enemyView) enemyView.SetActive(true);
        yield return new WaitForSecondsRealtime(holdBlackTime);
        IsActive = true;
        yield return FadeTo(0f, fadeOutTime);
        IsTransitioning = false;
    }

    IEnumerator ExitRoutine()
    {
        IsTransitioning = true;
        yield return FadeTo(1f, fadeInTime);
        if (enemyView) enemyView.SetActive(false);
        if (playerCamera) playerCamera.enabled = true;
        yield return new WaitForSecondsRealtime(holdBlackTime);
        IsActive = false;
        yield return FadeTo(0f, fadeOutTime);
        IsTransitioning = false;
    }

    IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeImage == null)
        {
            yield return new WaitForSecondsRealtime(duration);
            yield break;
        }
        Color c = fadeColor;
        float startA = fadeImage.color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(startA, targetAlpha, Mathf.Clamp01(t / duration));
            fadeImage.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        fadeImage.color = c;
    }

    void SetFadeAlpha(float a)
    {
        if (fadeImage == null) return;
        Color c = fadeColor; c.a = a; fadeImage.color = c;
    }

    void PlayUI(string id)
    {
        // Hook simple por si tienes un SFXId.UICancel o similar.
        AudioManager.Instance?.PlaySFX2D(SFXId.UICancel);
    }
}
