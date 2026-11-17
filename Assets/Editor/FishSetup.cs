using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: BuildSim/Setup/9 Add Fisher Hut And Fish.
/// One worker per hut (Miner Camp, Woodcutter Camp, Fisher Hut), the Fisher Hut prefab, fish icon for the Worker prefab,
/// the fish counter / Fisher hut / Recruit Fisher buttons on the HUD. NOTE: this rebuilds the HUD hierarchy (like the
/// "Rebuild UI" button), so hand edits to the HUD are lost; the crate panel is re-added afterwards.
/// </summary>
public static class FishSetup
{
    private const string HutPrefab = "Assets/Prefabs/Buildings/FisherHut.prefab";

    [MenuItem("BuildSim/Setup/9 Add Fisher Hut And Fish")]
    public static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
    }

    [MenuItem("BuildSim/Dev/Add Fisher Hut And Fish (no prompt)")]
    public static void Silent()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Dev] Fish setup aborted: the open scene has unsaved changes."); return; }
        Run();
    }

    private static void Run()
    {
        ArtGenerator.Run();
        AssetDatabase.Refresh();

        // ---- one worker per hut
        foreach (string p in new[] { "Assets/Prefabs/Buildings/StoneTent.prefab", "Assets/Prefabs/Buildings/WoodTent.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(p);
            root.GetComponent<Building>().maxWorkers = 1;
            PrefabUtility.SaveAsPrefabAsset(root, p);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ---- fisher hut = copy of the woodcutter camp with its own look
        if (!File.Exists(HutPrefab)) AssetDatabase.CopyAsset("Assets/Prefabs/Buildings/WoodTent.prefab", HutPrefab);
        var hut = PrefabUtility.LoadPrefabContents(HutPrefab);
        var b = hut.GetComponent<Building>();
        b.displayName = "Fisher Hut";
        b.role = BuildingRole.FishCamp;
        b.footprint = new Vector2Int(2, 1);
        b.costWood = 12;
        b.costStone = 0;
        b.maxWorkers = 1;
        var sr = hut.GetComponent<SpriteRenderer>();
        sr.sprite = ArtGenerator.LoadSprite("hut_fisher");
        var extra = hut.transform.Find("WorkerCamps_2");
        if (extra != null) Object.DestroyImmediate(extra.gameObject);
        var anim = hut.GetComponent<Animator>();
        if (anim != null) Object.DestroyImmediate(anim);
        PrefabUtility.SaveAsPrefabAsset(hut, HutPrefab);
        PrefabUtility.UnloadPrefabContents(hut);

        // ---- worker prefab: fish icon
        var wroot = PrefabUtility.LoadPrefabContents(PrefabSetup.WorkerPrefab);
        wroot.GetComponent<Worker>().fishIcon = ArtGenerator.LoadSprite("icon_fish");
        PrefabUtility.SaveAsPrefabAsset(wroot, PrefabSetup.WorkerPrefab);
        PrefabUtility.UnloadPrefabContents(wroot);
        AssetDatabase.SaveAssets();

        // ---- scene: catalog + HUD
        var scene = EditorSceneManager.OpenScene(LevelSceneBuilder.ScenePath, OpenSceneMode.Single);
        var colony = Object.FindObjectOfType<Colony>();
        var hud = Object.FindObjectOfType<HUD>();
        if (colony == null || hud == null) { Debug.LogError("FishSetup: Level scene needs Colony and HUD."); return; }

        var hutB = AssetDatabase.LoadAssetAtPath<GameObject>(HutPrefab).GetComponent<Building>();
        if (!colony.catalog.Contains(hutB))
        {
            colony.catalog.Add(hutB);
            colony.catalog.Sort((x, y) =>
            {
                int c = ((int)x.role).CompareTo((int)y.role);
                return c != 0 ? c : string.CompareOrdinal(x.displayName, y.displayName);
            });
            EditorUtility.SetDirty(colony);
        }

        var hso = new SerializedObject(hud);
        hso.FindProperty("fishSprite").objectReferenceValue = ArtGenerator.LoadSprite("icon_fish");
        hso.ApplyModifiedPropertiesWithoutUndo();
        hud.BuildUI(colony);
        CrateSetup.BuildPanel(hud);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Dev] FishSetup: Fisher Hut + fish added (catalog " + colony.catalog.Count + " buildings)");
    }
}
// touch
