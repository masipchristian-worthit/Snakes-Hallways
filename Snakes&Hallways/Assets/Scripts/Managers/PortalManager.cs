using System.Collections.Generic;
using UnityEngine;

public class PortalManager : MonoBehaviour
{
    public static PortalManager Instance { get; private set; }

    [SerializeField] List<Portal> portalCandidates = new();

    Portal selected;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (portalCandidates.Count == 0) return;
        int idx = Random.Range(0, portalCandidates.Count);
        for (int i = 0; i < portalCandidates.Count; i++)
        {
            bool sel = i == idx;
            portalCandidates[i].gameObject.SetActive(sel);
            portalCandidates[i].SetSelected(sel);
            if (sel) selected = portalCandidates[i];
        }
    }

    public void ActivatePortal()
    {
        selected?.Activate();
    }

    public Vector3 GetSelectedPosition() => selected ? selected.transform.position : Vector3.zero;
}
