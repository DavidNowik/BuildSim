using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Inspector of the OverworldController: test buttons for the tide cycle (Play Mode) and the world save.</summary>
[CustomEditor(typeof(OverworldController))]
public class OverworldControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var c = (OverworldController)target;
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("The world (lands, discoveries, moon, pearls) is saved in\n" + WorldStore.PathFile, MessageType.None);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.LabelField("Play Mode tests", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New world")) c.DebugNewWorld();
                if (GUILayout.Button("Discover 5 random lands")) c.DebugDiscover(5);
                if (GUILayout.Button("+1 moon")) c.DebugAddMoon(1);
                if (GUILayout.Button("+3 moons")) c.DebugAddMoon(3);
                if (GUILayout.Button("Trigger flood now")) c.DebugFlood();
            }
        }
        using (new EditorGUI.DisabledScope(!WorldStore.Exists()))
            if (GUILayout.Button("Delete world save (new world next Play)"))
                if (EditorUtility.DisplayDialog("Delete world save", "Delete the overworld save? Pearls and all discoveries are lost (level saves stay until the next tide).", "Delete", "Cancel"))
                    WorldStore.Delete();
        if (GUILayout.Button("Show saves folder")) EditorUtility.RevealInFinder(System.IO.Directory.Exists(MapStore.Dir) ? MapStore.Dir : Application.persistentDataPath);
    }
}

/// <summary>Inspector of the overworld HUD: rebuild the UI hierarchy.</summary>
[CustomEditor(typeof(OverworldHUD))]
public class OverworldHUDEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var hud = (OverworldHUD)target;
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("The UI below this object is real GameObjects you can edit by hand. 'Rebuild UI' deletes and recreates ALL of them.", MessageType.Info);
        if (GUILayout.Button("Rebuild UI", GUILayout.Height(28)))
        {
            var ctl = FindObjectOfType<OverworldController>();
            if (!EditorUtility.DisplayDialog("Rebuild UI", "Delete and recreate the whole overworld HUD?", "Rebuild", "Cancel")) return;
            Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Rebuild UI");
            hud.BuildUI(ctl != null ? ctl.modifierIcons : null);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        }
    }
}
