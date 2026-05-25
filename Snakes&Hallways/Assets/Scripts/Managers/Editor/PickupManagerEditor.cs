#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector personalizado para PickupManager. Añade un botón GRANDE de debug
/// "Collect All Pickups" que llama a <see cref="PickupManager.DebugCollectAllPickups"/>
/// para activar el portal sin tener que recorrer el mapa.
/// El botón solo es funcional en Play Mode (los pickups se activan al iniciar la run).
/// </summary>
[CustomEditor(typeof(PickupManager))]
public class PickupManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var pm = (PickupManager)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Debug / Testing", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button("Collect All Pickups (activa el portal)", GUILayout.Height(32)))
            {
                pm.DebugCollectAllPickups();
            }
            GUI.backgroundColor = Color.white;
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Solo en Play Mode.", MessageType.Info);
        else
            EditorGUILayout.HelpBox($"Active in scene: {pm.PickupsActiveInScene}", MessageType.None);
    }
}
#endif
