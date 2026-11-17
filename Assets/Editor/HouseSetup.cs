using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Menu: BuildSim/Dev/Add House (no prompt). Creates the House prefab (1 tile, 3 beds), adds it to the Colony catalog and rebuilds the HUD PoC so it shows in build tab E2.</summary>
public static class HouseSetup
{
    private const string Path = "Assets/Prefabs/Buildings/House.prefab";

    [MenuItem("BuildSim/Dev/Add House (no prompt)")]
    public static void Run()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Dev] House setup aborted: the open scene has unsaved changes."); return; }

        ArtGenerator.Run();
        AssetDatabase.Refresh();

        if (!File.Exists(Path))
        {
            var go = new GameObject("House");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ArtGenerator.LoadSprite("house");
            sr.sortingLayerName = Layers.Objects;
            var b = go.AddComponent<Building>();
            b.displayName = "House";
            b.role = BuildingRole.House;
            b.category = BuildCategory.Food;
            b.footprint = new Vector2Int(1, 1);
            b.costWood = 10;
            b.costStone = 0;
            b.maxWorkers = 3;
            PrefabUtility.SaveAsPrefabAsset(go, Path);
            Object.DestroyImmediate(go);
        }
        PrefabSetup.SetupBuilding(Path);
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.OpenScene(LevelSceneBuilder.ScenePath, OpenSceneMode.Single);
        var colony = Object.FindObjectOfType<Colony>();
        if (colony == null) { Debug.LogError("HouseSetup: Level scene needs a Colony."); return; }
        var house = AssetDatabase.LoadAssetAtPath<GameObject>(Path).GetComponent<Building>();
        if (!colony.catalog.Contains(house))
        {
            colony.catalog.Add(house);
            colony.catalog.Sort((x, y) =>
            {
                int c = ((int)x.role).CompareTo((int)y.role);
                return c != 0 ? c : string.CompareOrdinal(x.displayName, y.displayName);
            });
            EditorUtility.SetDirty(colony);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log("[Dev] House added to the catalog (" + colony.catalog.Count + " buildings)");
        HudPocBuilder.Silent();
    }
}
