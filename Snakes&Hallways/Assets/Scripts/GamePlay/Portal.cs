using UnityEngine;

public class Portal : MonoBehaviour
{
    [SerializeField] GameObject frame;
    [SerializeField] GameObject activeVfxAndCollider;
    [Tooltip("AudioSource del portal. Si está vacío se crea uno propio. Reproducirá PortalIdle en loop al seleccionarse y PortalActivate (one-shot) + sigue loopeando al activarse.")]
    [SerializeField] AudioSource portalSource;

    public bool IsActive { get; private set; }

    void Awake()
    {
        if (activeVfxAndCollider) activeVfxAndCollider.SetActive(false);
        if (!portalSource)
        {
            portalSource = GetComponent<AudioSource>();
            if (!portalSource)
            {
                var go = new GameObject("AS_Portal");
                go.transform.SetParent(transform, false);
                portalSource = go.AddComponent<AudioSource>();
                portalSource.playOnAwake = false;
                portalSource.spatialBlend = 1f;
                portalSource.loop = true;
            }
        }
    }

    public void SetSelected(bool selected)
    {
        if (frame) frame.SetActive(selected);
        if (selected)
            AudioManager.Instance?.StartLoop(SFXId.PortalIdle, portalSource, 0.3f, 1f, spatial: true);
        else
            AudioManager.Instance?.StopLoop(portalSource, 0.3f);
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        if (activeVfxAndCollider) activeVfxAndCollider.SetActive(true);
        AudioManager.Instance?.PlaySFX(SFXId.PortalActivate, transform.position);
        // El loop sigue activo (PortalIdle), pero si quieres parar al activarse, descomenta:
        // AudioManager.Instance?.StopLoop(portalSource, 0.3f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsActive) return;
        if (!other.CompareTag("Player")) return;
        AudioManager.Instance?.PlaySFX(SFXId.PortalCross, transform.position);
        GameManager.Instance?.TriggerWin();
    }
}
