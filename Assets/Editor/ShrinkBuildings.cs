using UnityEditor;
using UnityEngine;

/// <summary>
/// One-off: shrinks the hut prefabs (half-resolution tents, 2 tiles wide instead of ~3.3) so layouts can be built tile by tile.
/// Menu: BuildSim/Dev/Shrink Buildings. Re-runs PrefabSetup afterwards so shadows / lights / door glow match the new scale.
/// Edit the numbers below (or the prefab scale / footprint directly) to tune.
/// </summary>
public static class ShrinkBuildings
{
    // prefab path, new root scale, new footprint
    private static readonly object[][] Table =
    {
        new object[] { "Assets/Prefabs/Buildings/StoneTent.prefab", 0.5f, new Vector2Int(2, 1) },
        new object[] { "Assets/Prefabs/Buildings/WoodTent.prefab", 0.5f, new Vector2Int(2, 1) },
        new object[] { "Assets/Prefabs/Buildings/StorageTent.prefab", 0.5f, new Vector2Int(2, 1) },
    };

    [MenuItem("BuildSim/Dev/Shrink Buildings")]
    public static void Run()
    {
        foreach (var row in Table)
        {
            string path = (string)row[0];
            var root = PrefabUtility.LoadPrefabContents(path);
            float s = (float)row[1];
            root.transform.localScale = new Vector3(s, s, 1f);
            root.GetComponent<Building>().footprint = (Vector2Int)row[2];
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        PrefabSetup.Run();
        Debug.Log("[Dev] ShrinkBuildings: tents scaled to 0.5, storage footprint 2x1");
    }
}
