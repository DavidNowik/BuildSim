using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>One-off: moves the high-water mark in the existing Overworld scene above the start settlement (keeps hand edits).</summary>
public static class MoveHighWater
{
    [MenuItem("BuildSim/Dev/Move High-Water Mark Above Start (no prompt)")]
    public static void Run()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Dev] aborted: open scene has unsaved changes."); return; }
        var scene = EditorSceneManager.OpenScene(OverworldSceneBuilder.ScenePath, OpenSceneMode.Single);
        var c = Object.FindObjectOfType<OverworldController>();
        c.highWaterY = 0.62f;
        EditorUtility.SetDirty(c);
        foreach (var lr in Object.FindObjectsOfType<LineRenderer>(true))
            if (lr.name == "High-Water Mark") { lr.SetPosition(0, new Vector3(-9.5f, 0.62f, 0f)); lr.SetPosition(1, new Vector3(9.5f, 0.62f, 0f)); EditorUtility.SetDirty(lr); }
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
            if (t.name == "High-Water Label") { var p = t.localPosition; p.y = 0.83f; t.localPosition = p; EditorUtility.SetDirty(t); }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Dev] High-water mark moved to y=0.62");
    }
}
