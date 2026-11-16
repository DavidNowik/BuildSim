using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Inspector for the MapGenerator: generate / re-roll / clear the map directly in the scene.</summary>
[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var gen = (MapGenerator)target;
        DrawDefaultInspector();

        EditorGUILayout.Space();
        Vector2Int size = gen.settings.Size;
        EditorGUILayout.HelpBox(
            "Map size: " + size.x + " x " + size.y + (gen.settings.sizeFromTier ? "  (tier " + gen.settings.tier + ")" : "  (custom)") +
            "\nSame seed + same settings always gives the same map.", MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate", GUILayout.Height(28))) Do(gen, "Generate Map", () => gen.Generate());
            if (GUILayout.Button("Random Seed + Generate", GUILayout.Height(28))) Do(gen, "Generate Map (random seed)", () => gen.RandomSeedAndGenerate());
        }
        if (GUILayout.Button("Clear Map")) Do(gen, "Clear Map", () => gen.Clear());

        if (gen.Current != null)
        {
            var d = gen.Current;
            EditorGUILayout.LabelField("Stone deposits", d.CountResource(ResourceKind.Stone).ToString());
            EditorGUILayout.LabelField("Wood deposits", d.CountResource(ResourceKind.Wood).ToString());
            EditorGUILayout.LabelField("Start cell", d.start.ToString());
        }
    }

    private static void Do(MapGenerator gen, string undoName, System.Action action)
    {
        Undo.RegisterFullObjectHierarchyUndo(gen.transform.root.gameObject, undoName);
        action();
        if (gen.grid != null) EditorUtility.SetDirty(gen.grid);
        EditorUtility.SetDirty(gen);
        EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
    }
}

/// <summary>Inspector for the LevelSession: inspect / delete the save of this map.</summary>
[CustomEditor(typeof(LevelSession))]
public class LevelSessionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (LevelSession)target;
        DrawDefaultInspector();
        EditorGUILayout.Space();

        bool exists = MapStore.Exists(s.mapId);
        EditorGUILayout.HelpBox((exists ? "A save exists for '" : "No save yet for '") + s.mapId + "'.\n" + MapStore.PathFor(s.mapId), exists ? MessageType.Info : MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
                if (GUILayout.Button("Save now")) s.Save(true);
            using (new EditorGUI.DisabledScope(!exists))
                if (GUILayout.Button("Delete this map's save"))
                {
                    if (EditorUtility.DisplayDialog("Delete save", "Delete the save of '" + s.mapId + "'? The map starts fresh next time.", "Delete", "Cancel"))
                        s.DeleteSave();
                }
        }
        if (GUILayout.Button("Show saves folder")) EditorUtility.RevealInFinder(System.IO.Directory.Exists(MapStore.Dir) ? MapStore.Dir : Application.persistentDataPath);
    }
}

/// <summary>Inspector for the HUD: rebuild the UI hierarchy from the Colony catalog.</summary>
[CustomEditor(typeof(HUD))]
public class HUDEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var hud = (HUD)target;
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("The UI below this object is real GameObjects you can edit by hand. 'Rebuild UI' deletes and recreates ALL of them (use it after changing the Colony catalog or to reset the layout).", MessageType.Info);
        if (GUILayout.Button("Rebuild UI", GUILayout.Height(28)))
        {
            var colony = FindObjectOfType<Colony>();
            if (colony == null) { EditorUtility.DisplayDialog("Rebuild UI", "No Colony object in the scene.", "OK"); return; }
            if (!EditorUtility.DisplayDialog("Rebuild UI", "Delete and recreate the whole HUD hierarchy?", "Rebuild", "Cancel")) return;
            Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Rebuild UI");
            hud.BuildUI(colony);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        }
    }
}
