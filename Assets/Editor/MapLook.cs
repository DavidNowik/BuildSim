using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Wires the generated "Ink &amp; Candy" map art into a level scene: the Outlines tilemap with its TerrainOutliner,
/// the standalone tree/rock/bush tile lists on the MapGenerator. Used by the Level scene builder and by the upgrade menu
/// (BuildSim/Setup/6 Upgrade Level Map Look) which only ADDS to the existing Level scene, so your hand edits stay.
/// </summary>
public static class MapLook
{
    [MenuItem("BuildSim/Setup/6 Upgrade Level Map Look")]
    public static void MenuUpgrade()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Upgrade();
    }

    [MenuItem("BuildSim/Dev/Upgrade Level Map Look (no prompt)")]
    public static void SilentUpgrade()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Dev] Map look upgrade aborted: the open scene has unsaved changes."); return; }
        Upgrade();
    }

    private static void Upgrade()
    {
        ArtGenerator.Run();
        AssetDatabase.Refresh();

        var scene = EditorSceneManager.OpenScene(LevelSceneBuilder.ScenePath, OpenSceneMode.Single);
        var gen = Object.FindObjectOfType<MapGenerator>();
        var grid = Object.FindObjectOfType<GridManager>();
        if (gen == null || grid == null || gen.ground == null) { Debug.LogError("MapLook: Level scene has no MapGenerator / GridManager / Ground tilemap."); return; }

        Setup(gen, grid, gen.ground.transform.parent);
        gen.Generate();
        EditorUtility.SetDirty(gen);
        EditorUtility.SetDirty(grid);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Dev] MapLook: Level map upgraded (" + grid.width + "x" + grid.height + ", seed " + gen.settings.seed + ")");
    }

    /// <summary>Creates (or reuses) the Outlines tilemap under the grid and assigns every tile list.</summary>
    public static void Setup(MapGenerator gen, GridManager grid, Transform gridTransform)
    {
        // ---- outlines tilemap: above the ground, below decor and deposits
        Transform t = gridTransform.Find("Outlines");
        Tilemap outlines;
        if (t == null)
        {
            var go = new GameObject("Outlines");
            go.transform.SetParent(gridTransform, false);
            outlines = go.AddComponent<Tilemap>();
            var tr = go.AddComponent<TilemapRenderer>();
            tr.sortingLayerName = Layers.Ground;
            tr.sortingOrder = -9950;
            var lit = PrefabSetup.LitMaterial;
            if (lit != null) tr.sharedMaterial = lit;
        }
        else outlines = t.GetComponent<Tilemap>();

        var outliner = outlines.GetComponent<TerrainOutliner>();
        if (outliner == null) outliner = outlines.gameObject.AddComponent<TerrainOutliner>();
        outliner.map = outlines;
        outliner.coastTiles = EdgeSet("Coast");
        outliner.borderTiles = EdgeSet("Border");
        outliner.dirtTiles = EdgeSet("Dirt");
        EditorUtility.SetDirty(outliner);

        gen.outliner = outliner;
        grid.outliner = outliner;

        // deposits are drawn one tilemap per row so lower rows always overlap higher ones
        if (gen.resources != null && gen.resources.GetComponent<DepositRows>() == null) gen.resources.gameObject.AddComponent<DepositRows>();

        // ---- standalone deposits + border bushes
        gen.stoneTiles = Tiles("Rock_", 4);
        gen.woodTiles = Tiles("Tree_", 4);
        gen.bush = Tiles("Bush_", 3);
        gen.grassDecor = Tiles("Grass_Decor_", 5);
        gen.decorChance = 0.10f;
        EditorUtility.SetDirty(gen);
    }

    private static TileBase[] EdgeSet(string set)
    {
        var arr = new TileBase[256];
        for (int e = 0; e < 16; e++)
            for (int c = 0; c < 16; c++)
                if (MapArt.ValidEdge(e, c)) arr[MapArt.EdgeIndex(e, c)] = ArtGenerator.LoadTile("Edge_" + set + "_" + e + "_" + c);
        return arr;
    }

    private static TileBase[] Tiles(string prefix, int count)
    {
        var list = new TileBase[count];
        for (int i = 0; i < count; i++) list[i] = ArtGenerator.LoadTile(prefix + i);
        return list;
    }
}
