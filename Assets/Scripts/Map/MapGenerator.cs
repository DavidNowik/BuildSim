using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Scene component that turns MapGen output into Tilemaps. Works in the editor too: use the buttons in the
/// Inspector (Generate / Random Seed / Clear). The painted result is saved with the scene.
/// </summary>
public class MapGenerator : MonoBehaviour
{
    public MapSettings settings = new MapSettings();

    [Header("Scene references")]
    public GridManager grid;
    public Tilemap ground;
    public Tilemap decor;
    public Tilemap resources;
    public Tilemap overlay;

    [Header("Ground tiles (plain variants per tone)")]
    public TileBase[] grassLight;
    public TileBase[] grassMid;
    public TileBase[] grassDark;
    [Tooltip("Grass with tufts/flowers, sprinkled over the plain grass")]
    public TileBase[] grassDecor;
    [Range(0f, 0.5f)] public float decorChance = 0.14f;
    public TileBase[] dirt;
    public TileBase[] water;
    [Tooltip("Bushes placed in the border around the map")]
    public TileBase[] bush;

    [Header("Deposit tiles (every cell is its own standalone sprite; names must start with 'Rock' / 'Tree')")]
    public TileBase[] stoneTiles;
    public TileBase[] woodTiles;

    [Header("Pearl crates")]
    public CrateManager crates;

    [Header("Terrain outlines")]
    public TerrainOutliner outliner;

    [Header("Overlay")]
    public TileBase gridLine;

    [Header("Play mode")]
    [Tooltip("If the map in the scene is empty when playing and there is no save, generate one.")]
    public bool generateOnPlayIfEmpty = true;
    [Tooltip("Testing: when there is no save to load, generate a fresh map from the settings above every time you press Play (instead of using the map painted in the scene).")]
    public bool regenerateOnPlay = false;

    /// <summary>The map that is currently painted (last Generate/Apply).</summary>
    public MapData Current { get; private set; }

    public bool IsEmpty { get { return ground == null || ground.GetUsedTilesCount() == 0; } }

    // ------------------------------------------------------------ actions
    public void Generate()
    {
        Apply(MapGen.Generate(settings));
    }

    public void RandomSeedAndGenerate()
    {
        settings.seed = Random.Range(1, 999999);
        Generate();
    }

    public void Clear()
    {
        if (ground != null) ground.ClearAllTiles();
        if (decor != null) decor.ClearAllTiles();
        if (resources != null) { resources.ClearAllTiles(); var rw = resources.GetComponent<DepositRows>(); if (rw != null) rw.ClearAll(); }
        if (overlay != null) overlay.ClearAllTiles();
        if (outliner != null) outliner.Clear();
        if (crates != null) crates.Clear();
        Current = null;
    }

    /// <summary>Paints a map into the scene tilemaps and configures the GridManager for its size.</summary>
    public void Apply(MapData d)
    {
        Clear();
        Current = d;
        settings.seed = d.seed;
        settings.tier = d.tier;

        if (grid != null)
        {
            grid.width = d.width;
            grid.height = d.height;
            grid.startCell = d.start;
        }

        int m = Mathf.Max(0, settings.margin);
        int w2 = d.width + m * 2, h2 = d.height + m * 2;
        var region = new BoundsInt(-m, -m, 0, w2, h2, 1);
        var tiles = new TileBase[w2 * h2];

        for (int gy = -m; gy < d.height + m; gy++)
        {
            for (int gx = -m; gx < d.width + m; gx++)
            {
                TileBase t;
                if (d.InBounds(gx, gy)) t = TileForTerrain(d, gx, gy);
                else t = Pick(grassDark, gx, gy, 5);
                tiles[(gy + m) * w2 + (gx + m)] = t;
            }
        }
        if (ground != null) ground.SetTilesBlock(region, tiles);

        // ink outlines between terrain types (also around the playable map's border)
        if (outliner != null)
            outliner.Paint((x, y) => ClassAt(d, x, y), -m, -m, d.width + m - 1, d.height + m - 1);

        // bushes in the border ring: a few clumps, never touching the playable map
        if (decor != null && bush != null && bush.Length > 0)
        {
            for (int gy = -m; gy < d.height + m; gy++)
                for (int gx = -m; gx < d.width + m; gx++)
                {
                    if (gx >= -1 && gy >= -1 && gx <= d.width && gy <= d.height) continue;       // keep a clean strip around the map
                    if (MapGen.Hash01(Mathf.FloorToInt(gx / 3f), Mathf.FloorToInt(gy / 3f), d.seed + 301) > 0.32f) continue;
                    if (MapGen.Hash01(gx, gy, d.seed + 302) > 0.78f) continue;
                    decor.SetTile(new Vector3Int(gx, gy, 0), Pick(bush, gx, gy, 6));
                }
        }

        // deposits
        if (resources != null)
        {
            for (int y = 0; y < d.height; y++)
                for (int x = 0; x < d.width; x++)
                {
                    ResourceKind k = d.GetResource(x, y);
                    if (k == ResourceKind.None) continue;
                    resources.SetTile(new Vector3Int(x, y, 0), Pick(k == ResourceKind.Stone ? stoneTiles : woodTiles, x, y, 8));
                }
        }

        if (resources != null) { var rw = resources.GetComponent<DepositRows>(); if (rw != null) rw.Rebuild(); }

        if (crates != null) crates.Spawn(d);

        // build-mode grid lines
        if (overlay != null && gridLine != null)
        {
            var lines = new TileBase[d.width * d.height];
            for (int i = 0; i < lines.Length; i++) lines[i] = gridLine;
            overlay.SetTilesBlock(new BoundsInt(0, 0, 0, d.width, d.height, 1), lines);
        }
    }

    /// <summary>Terrain class used for outlines (cells outside the map count as the border).</summary>
    private static int ClassAt(MapData d, int x, int y)
    {
        if (!d.InBounds(x, y)) return TerrainOutliner.Border;
        switch (d.GetTerrain(x, y))
        {
            case TerrainKind.Water: return TerrainOutliner.Water;
            case TerrainKind.Dirt: return TerrainOutliner.Dirt;
            default: return TerrainOutliner.Grass;
        }
    }

    private TileBase TileForTerrain(MapData d, int x, int y)
    {
        switch (d.GetTerrain(x, y))
        {
            case TerrainKind.Water: return Pick(water, x, y, 3);
            case TerrainKind.Dirt: return Pick(dirt, x, y, 4);
        }

        // grass: low-frequency tone picks light / mid / dark patches, rare decorated tiles on top
        if (grassDecor != null && grassDecor.Length > 0 && MapGen.Hash01(x, y, d.seed + 211) < decorChance)
            return Pick(grassDecor, x, y, 7);

        float tone = MapGen.Tone(x, y, d.seed);
        TileBase[] set = tone < 0.36f ? grassDark : (tone > 0.64f ? grassLight : grassMid);
        if (set == null || set.Length == 0) set = grassMid;
        return Pick(set, x, y, 2);
    }

    private TileBase Pick(TileBase[] set, int x, int y, int salt)
    {
        if (set == null || set.Length == 0) return null;
        int i = Mathf.FloorToInt(MapGen.Hash01(x, y, salt * 17 + settings.seed) * set.Length);
        return set[Mathf.Clamp(i, 0, set.Length - 1)];
    }

    // ------------------------------------------------------------ gizmos
    private void OnDrawGizmosSelected()
    {
        if (grid == null || Current == null) return;
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
        Rect b = grid.WorldBounds;
        Gizmos.DrawWireCube(b.center, new Vector3(b.width, b.height, 0f));
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
        Vector3 s = grid.CellToWorld(Current.start);
        Gizmos.DrawWireCube(s, new Vector3(9f, 7f, 0f));
    }
}
