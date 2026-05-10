using UnityEngine;

public class Portal : MonoBehaviour
{
    [SerializeField] GameObject frame;
    [SerializeField] GameObject activeVfxAndCollider;
    [SerializeField] AudioSource idleLoop;
    [SerializeField] AudioSource activeLoop;

    public bool IsActive { get; private set; }

    void Awake()
    {
        if (activeVfxAndCollider) activeVfxAndCollider.SetActive(false);
    }

    public void SetSelected(bool selected)
    {
        if (frame) frame.SetActive(selected);
        if (idleLoop) { if (selected) idleLoop.Play(); else idleLoop.Stop(); }
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        if (activeVfxAndCollider) activeVfxAndCollider.SetActive(true);
        AudioManager.Instance?.PlaySFX(SFXId.PortalActive, transform.position);
        if (idleLoop) idleLoop.Stop();
        if (activeLoop) activeLoop.Play();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsActive) return;
        if (!other.CompareTag("Player")) return;
        AudioManager.Instance?.PlaySFX(SFXId.PortalCross, transform.position);
        GameManager.Instance?.TriggerWin();
    }
}
