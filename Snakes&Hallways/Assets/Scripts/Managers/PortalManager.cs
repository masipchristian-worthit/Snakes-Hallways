using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalManager : MonoBehaviour
{
    public static PortalManager Instance { get; private set; }

    [Header("Run")]
    [Tooltip("Solo elige portal cuando esta escena está activa.")]
    [SerializeField] string gameplaySceneName = "SCN_Labe";

    [Header("Portales")]
    [SerializeField] List<Portal> portalCandidates = new();

    Portal selected;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()  => SceneManager.sceneLoaded += HandleSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    void Start()
    {
        if (SceneManager.GetActiveScene().name == gameplaySceneName) BeginRun();
        else ResetRun();
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == gameplaySceneName) BeginRun();
        else ResetRun();
    }

    public void BeginRun()
    {
        if (portalCandidates.Count == 0) return;
        int idx = Random.Range(0, portalCandidates.Count);
        selected = null;
        for (int i = 0; i < portalCandidates.Count; i++)
        {
            if (portalCandidates[i] == null) continue;
            bool sel = i == idx;
            portalCandidates[i].gameObject.SetActive(sel);
            portalCandidates[i].SetSelected(sel);
            if (sel) selected = portalCandidates[i];
        }
    }

    public void ResetRun()
    {
        selected = null;
        for (int i = 0; i < portalCandidates.Count; i++)
        {
            if (portalCandidates[i] == null) continue;
            portalCandidates[i].SetSelected(false);
            portalCandidates[i].gameObject.SetActive(false);
        }
    }

    public void ActivatePortal() => selected?.Activate();

    public Vector3 GetSelectedPosition() => selected ? selected.transform.position : Vector3.zero;

    /// <summary>Portal seleccionado para esta partida (puede estar inactivo aún si no se ha activado).</summary>
    public Portal Selected => selected;

    /// <summary>True si hay portal seleccionado y ya está activo (jugable). Lo usa la IA para
    /// crear una zona segura alrededor en la que NO puede teleportarse el minotauro.</summary>
    public bool IsSelectedActive => selected != null && selected.IsActive;
}
