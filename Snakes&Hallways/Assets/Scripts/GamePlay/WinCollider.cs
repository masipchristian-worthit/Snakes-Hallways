using UnityEngine;

/// <summary>
/// Portal de victoria. Está APAGADO hasta que el jugador haya recogido todos los pickups
/// requeridos por la dificultad; entonces se activa con SFX PortalActivate y loop PortalIdle.
/// Al entrar el Player dispara GameManager.TriggerWin() + SceneTransition a la escena fin.
///
/// Setup:
///   1. GameObject con BoxCollider/SphereCollider en modo trigger (Is Trigger ON).
///   2. Tag del Player = "Player".
///   3. Arrastra el visual del portal (mesh + VFX + colliders bloqueantes si los hay) a
///      <see cref="portalVisual"/>. Mientras el portal está apagado ese root estará SetActive(false).
///   4. Asigna el nombre exacto de la escena fin (default "SCN_EndScene").
/// </summary>
[RequireComponent(typeof(Collider))]
public class WinCollider : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Nombre exacto de la escena fin a cargar (debe estar añadida en Build Settings).")]
    [SerializeField] string endSceneName = "SCN_EndScene";
    [Tooltip("Duración del fade a negro antes de cargar la escena fin.")]
    [SerializeField] float fadeTime = 1.5f;

    [Header("Behaviour")]
    [Tooltip("Si está activo, además del fade dispara GameManager.TriggerWin() para que se ejecute la limpieza (EndRun) y se emita el estado Win.")]
    [SerializeField] bool triggerWinState = true;
    [Tooltip("Si está activo, exige que el jugador haya recogido todos los pickups (PickupsCollected >= PickupsRequired de la dificultad) antes de poder ganar. ON por defecto.")]
    [SerializeField] bool requireAllPickups = true;

    [Header("Portal Visual / Audio")]
    [Tooltip("Root del visual del portal (mesh + VFX). Se mantiene SetActive(false) hasta que se completen todos los pickups. Si está vacío se usa este GameObject como visual (no recomendado: deshabilita también este script).")]
    [SerializeField] GameObject portalVisual;
    [Tooltip("AudioSource dedicado del portal para el loop PortalIdle. Si está vacío se crea uno en runtime.")]
    [SerializeField] AudioSource portalLoopSource;
    [Tooltip("Fade-in del loop PortalIdle al activarse el portal.")]
    [SerializeField] float portalIdleFadeIn = 0.8f;
    [Tooltip("Volumen del loop PortalIdle.")]
    [Range(0f, 1f)][SerializeField] float portalIdleVolume = 0.8f;

    bool fired;
    bool portalActivated;
    Collider triggerCol;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Awake()
    {
        triggerCol = GetComponent<Collider>();
        if (triggerCol != null) triggerCol.isTrigger = true;

        if (!portalLoopSource)
        {
            var go = new GameObject("AS_PortalLoop");
            go.transform.SetParent(transform, false);
            portalLoopSource = go.AddComponent<AudioSource>();
            portalLoopSource.playOnAwake = false;
            portalLoopSource.spatialBlend = 1f;
        }
    }

    bool subscribed;

    void OnEnable()
    {
        // Estado inicial: portal apagado, trigger inactivo, escucha cambios de pickups.
        SetPortalActiveImmediate(false);
        TrySubscribe();
    }

    void OnDisable()
    {
        if (subscribed && GameManager.Instance != null)
            GameManager.Instance.OnPickupCountChanged -= HandlePickupCountChanged;
        subscribed = false;
    }

    void Update()
    {
        // Si GameManager.Instance no existía en OnEnable (orden de Awake), reintentamos.
        if (!subscribed) TrySubscribe();
    }

    void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null || subscribed) return;
        gm.OnPickupCountChanged += HandlePickupCountChanged;
        subscribed = true;
        // Estado actual: por si la run ya está en marcha.
        HandlePickupCountChanged(gm.PickupsCollected, gm.PickupsRequired);
    }

    void HandlePickupCountChanged(int collected, int required)
    {
        if (portalActivated) return;
        if (!requireAllPickups || (required > 0 && collected >= required))
            ActivatePortal();
    }

    void SetPortalActiveImmediate(bool on)
    {
        portalActivated = on;
        ApplyPortalVisibility(on);
        if (triggerCol != null) triggerCol.enabled = on;
    }

    /// <summary>
    /// Hace visible/invisible el portal SIN tocar el SetActive del GameObject raíz.
    /// Si portalVisual apunta a un HIJO distinto del WinCollider, lo activa/desactiva.
    /// Si portalVisual es null o apunta a sí mismo (self-reference), alternamos solo los
    /// Renderers + ParticleSystems + Lights para que el script siga corriendo.
    /// </summary>
    void ApplyPortalVisibility(bool on)
    {
        if (portalVisual != null && portalVisual != gameObject)
        {
            portalVisual.SetActive(on);
            return;
        }

        // portalVisual == null o == self. Toggle a nivel de componentes para no
        // desactivarnos a nosotros mismos (Awake/OnEnable dejaría de correr).
        Transform root = portalVisual != null ? portalVisual.transform : transform;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = on;
        var ps = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < ps.Length; i++)
        {
            if (on) ps[i].Play(true);
            else ps[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        var lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++) lights[i].enabled = on;
    }

    /// <summary>
    /// Activa el portal. PUBLIC: lo puede llamar GameManager directamente cuando
    /// detecta que se han recogido todos los pickups, evitando dependencias del
    /// evento OnPickupCountChanged (que no llega si el GameObject está inactivo).
    /// También se asegura de que el propio GameObject esté activo.
    /// </summary>
    public void ActivatePortal()
    {
        // Si el GO del WinCollider estaba apagado, lo encendemos. Si no, Awake/OnEnable
        // nunca habrían corrido y triggerCol seguiría sin resolver.
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (triggerCol == null) triggerCol = GetComponent<Collider>();

        if (portalActivated) return;
        portalActivated = true;

        ApplyPortalVisibility(true);
        if (triggerCol != null) triggerCol.enabled = true;

        // SFX one-shot de activación + loop ambiente del portal.
        AudioManager.Instance?.PlaySFX(SFXId.PortalActivate, transform.position);
        AudioManager.Instance?.StartLoop(SFXId.PortalIdle, portalLoopSource, portalIdleFadeIn, portalIdleVolume, spatial: true);
    }

    void OnTriggerEnter(Collider other)
    {
        if (fired || !portalActivated) return;
        if (!other.CompareTag("Player")) return;

        if (requireAllPickups)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.PickupsCollected < gm.PickupsRequired) return;
        }

        fired = true;

        // SFX de cruce del portal.
        AudioManager.Instance?.PlaySFX(SFXId.PortalCross, transform.position);

        // 1) Dispara el estado WIN (esto llama internamente a EndRun para limpiar
        //    pickups, parar música, deshabilitar el inteligente, etc.).
        if (triggerWinState) GameManager.Instance?.TriggerWin();

        // 2) Carga la escena fin con fade (usa el mismo overlay/dissolve que el resto).
        var st = SceneTransition.EnsureInstance();
        if (st != null) st.FadeAndLoad(endSceneName, fadeTime);
        else UnityEngine.SceneManagement.SceneManager.LoadScene(endSceneName);
    }
}
