using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>One logical grid cell. This is the single source of truth for the world state.</summary>
public class Cell
{
    public readonly Vector2Int pos;
    public TerrainKind terrain;
    public ResourceKind resource;
    public int yield;
    public int maxYield;
    public Building building;
    public Crate crate;
    public Worker reservedBy;
    public float moveCost = 1f;

    public Cell(Vector2Int pos) { this.pos = pos; }

    public bool HasNode { get { return resource != ResourceKind.None; } }
    public bool Walkable { get { return building == null && crate == null && resource == ResourceKind.None && terrain != TerrainKind.Water; } }
    public bool Buildable { get { return Walkable; } }
}

/// <summary>
/// Data-driven grid. The scene's Ground / Resources tilemaps are read once at startup to seed the cells
/// (water, dirt, deposits); afterwards the tilemaps are purely a view (a mined deposit is replaced by dirt).
/// Width/height/start are written by the MapGenerator in the scene.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GridManager : MonoBehaviour
{
    public static GridManager instance;

    [Header("Grid (written by the MapGenerator)")]
    public int width = 10;
    public int height = 10;
    public float tileSpacing = 1f;
    public Vector2Int startCell = new Vector2Int(5, 5);

    [Header("Scene references")]
    public Tilemap groundMap;
    public Tilemap resourceMap;
    [Tooltip("Tiles painted on the ground where a deposit was mined out")]
    public TileBase[] minedTiles;
    [Tooltip("Redraws the ink outlines around a cell that turned to dirt")]
    public TerrainOutliner outliner;

    [Header("Deposit sizes")]
    public int stoneYield = 100;
    public int woodYield = 50;

    public bool IsInitialized { get; private set; }
    public Vector3 Origin { get; private set; }

    private Cell[,] grid;
    private readonly List<Cell> nodes = new List<Cell>();
    private readonly List<Cell> candidateBuffer = new List<Cell>();

    public IReadOnlyList<Cell> Nodes { get { return nodes; } }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Multiple GridManagers found! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        instance = this;

        Vector3 o = transform.position;
        Origin = new Vector3(o.x, o.y, 0f);

        grid = new Cell[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = new Cell(new Vector2Int(x, y));

        ScanGround();
        ScanResources();
        IsInitialized = true;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ------------------------------------------------------------ seeding from the scene tilemaps
    private void ScanGround()
    {
        if (groundMap == null) return;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                TileBase tile = groundMap.GetTile(groundMap.WorldToCell(CellToWorld(new Vector2Int(x, y))));
                if (tile == null) continue;
                string n = tile.name;
                if (n.StartsWith("Water")) grid[x, y].terrain = TerrainKind.Water;
                else if (n.StartsWith("Dirt")) grid[x, y].terrain = TerrainKind.Dirt;
            }
    }

    private void ScanResources()
    {
        if (resourceMap == null)
        {
            Debug.LogWarning("GridManager: no resource Tilemap assigned, the map has no deposits.");
            return;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                TileBase tile = resourceMap.GetTile(resourceMap.WorldToCell(CellToWorld(new Vector2Int(x, y))));
                if (tile == null) continue;

                ResourceKind kind = ResourceKind.None;
                int yield = 0;
                string n = tile.name;
                if (n.StartsWith("Rock")) { kind = ResourceKind.Stone; yield = stoneYield; }
                else if (n.StartsWith("Tree")) { kind = ResourceKind.Wood; yield = woodYield; }
                if (kind == ResourceKind.None) continue;

                Cell c = grid[x, y];
                c.resource = kind;
                c.yield = yield;
                c.maxYield = yield;
                nodes.Add(c);
            }
        }
    }

    /// <summary>Restores remaining deposit amounts from a saved map (call after the grid was scanned).</summary>
    public void ApplyYields(MapData saved)
    {
        if (saved == null || saved.width != width || saved.height != height) return;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Cell c = grid[x, y];
                int y2 = saved.yield[saved.Index(x, y)];
                if (c.HasNode && y2 > 0) c.yield = y2;
            }
    }

    /// <summary>Snapshot of the current world (terrain, deposits, remaining amounts) for saving.</summary>
    public MapData Capture(int seed, int tier)
    {
        var d = new MapData(width, height) { seed = seed, tier = tier, start = startCell };
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Cell c = grid[x, y];
                int i = d.Index(x, y);
                d.terrain[i] = (byte)c.terrain;
                d.resource[i] = (byte)c.resource;
                d.yield[i] = c.yield;
            }
        return d;
    }

    // ------------------------------------------------------------ coordinates
    public Vector3 CellToWorld(Vector2Int c)
    {
        return new Vector3(Origin.x + c.x * tileSpacing, Origin.y + c.y * tileSpacing, 0f);
    }

    public Vector2Int WorldToCell(Vector3 w)
    {
        return new Vector2Int(
            Mathf.RoundToInt((w.x - Origin.x) / tileSpacing),
            Mathf.RoundToInt((w.y - Origin.y) / tileSpacing));
    }

    public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < width && y < height; }
    public bool InBounds(Vector2Int c) { return InBounds(c.x, c.y); }

    public Cell GetCell(int x, int y) { return InBounds(x, y) ? grid[x, y] : null; }
    public Cell GetCell(Vector2Int c) { return GetCell(c.x, c.y); }

    /// <summary>World-space rectangle covered by the playable grid.</summary>
    public Rect WorldBounds
    {
        get
        {
            float h = tileSpacing * 0.5f;
            return new Rect(Origin.x - h, Origin.y - h, width * tileSpacing, height * tileSpacing);
        }
    }

    // ------------------------------------------------------------ footprints
    public bool CanPlace(Vector2Int anchor, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                Cell c = GetCell(anchor.x + x, anchor.y + y);
                if (c == null || !c.Buildable) return false;
            }
        return true;
    }

    public void Occupy(Building b, Vector2Int anchor, Vector2Int size, List<Cell> outCells)
    {
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                Cell c = GetCell(anchor.x + x, anchor.y + y);
                if (c == null) continue;
                c.building = b;
                if (outCells != null) outCells.Add(c);
            }
    }

    public void Vacate(Building b)
    {
        foreach (Cell c in b.occupiedCells)
            if (c.building == b) c.building = null;
    }

    /// <summary>Terrain class of a cell for the outline tiles (outside the map = border).</summary>
    private int OutlineClassAt(int x, int y)
    {
        Cell c = GetCell(x, y);
        if (c == null) return TerrainOutliner.Border;
        return c.terrain == TerrainKind.Water ? TerrainOutliner.Water : (c.terrain == TerrainKind.Dirt ? TerrainOutliner.Dirt : TerrainOutliner.Grass);
    }

    // ------------------------------------------------------------ resource nodes
    /// <summary>
    /// Finds the nearest unreserved node of a kind that the worker can actually walk to.
    /// On success the node is reserved for the worker and the path (to a free neighbour cell) is returned.
    /// </summary>
    public bool TryClaimNode(Worker worker, ResourceKind kind, Vector2Int from, List<Vector2Int> pathOut, out Cell node)
    {
        node = null;
        candidateBuffer.Clear();
        for (int i = 0; i < nodes.Count; i++)
        {
            Cell c = nodes[i];
            if (c.resource != kind) continue;
            if (c.reservedBy != null && c.reservedBy != worker) continue;
            candidateBuffer.Add(c);
        }
        if (candidateBuffer.Count == 0) return false;

        candidateBuffer.Sort((a, b) =>
        {
            int da = Mathf.Abs(a.pos.x - from.x) + Mathf.Abs(a.pos.y - from.y);
            int db = Mathf.Abs(b.pos.x - from.x) + Mathf.Abs(b.pos.y - from.y);
            return da.CompareTo(db);
        });

        int tried = 0;
        for (int i = 0; i < candidateBuffer.Count && tried < 8; i++)
        {
            Cell c = candidateBuffer[i];
            if (!HasFreeNeighbor(c.pos)) continue; // enclosed deposits are skipped without counting
            tried++;
            if (TryPathToNeighbor(from, c.pos, pathOut))
            {
                c.reservedBy = worker;
                node = c;
                return true;
            }
        }
        return false;
    }

    private static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, 1)
    };

    public bool HasFreeNeighbor(Vector2Int p)
    {
        for (int i = 0; i < Dirs.Length; i++)
        {
            Cell n = GetCell(p + Dirs[i]);
            if (n != null && n.Walkable) return true;
        }
        return false;
    }

    private readonly List<Vector2Int> scratchPath = new List<Vector2Int>();

    /// <summary>Shortest path to any free neighbour of a cell.</summary>
    public bool TryPathToNeighbor(Vector2Int from, Vector2Int target, List<Vector2Int> pathOut)
    {
        int best = int.MaxValue;
        bool found = false;
        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2Int n = target + Dirs[i];
            Cell cell = GetCell(n);
            if (cell == null || !cell.Walkable) continue;
            if (PathFinder.FindPath(from, n, scratchPath) && scratchPath.Count < best)
            {
                best = scratchPath.Count;
                pathOut.Clear();
                pathOut.AddRange(scratchPath);
                found = true;
            }
        }
        return found;
    }

    public void ReleaseNode(Cell c, Worker worker)
    {
        if (c != null && c.reservedBy == worker) c.reservedBy = null;
    }

    /// <summary>Takes one unit from a node. Returns true if the node is now depleted.</summary>
    public bool HarvestOne(Cell c)
    {
        if (c == null || !c.HasNode) return true;
        c.yield--;
        if (c.yield > 0) return false;

        RemoveNode(c);
        return true;
    }

    public void RemoveNode(Cell c)
    {
        ResourceKind kind = c.resource;
        c.resource = ResourceKind.None;
        c.yield = 0;
        c.reservedBy = null;
        nodes.Remove(c);

        if (resourceMap != null)
        {
            var rc = resourceMap.WorldToCell(CellToWorld(c.pos));
            resourceMap.SetTile(rc, null);
            var rw = resourceMap.GetComponent<DepositRows>();
            if (rw != null) rw.Remove(rc);
        }

        // the ground under a mined deposit turns to dirt
        if (c.terrain != TerrainKind.Water) c.terrain = TerrainKind.Dirt;
        if (groundMap != null && minedTiles != null && minedTiles.Length > 0)
        {
            int pick = Mathf.FloorToInt(MapGen.Hash01(c.pos.x, c.pos.y, 91) * minedTiles.Length) % minedTiles.Length;
            groundMap.SetTile(groundMap.WorldToCell(CellToWorld(c.pos)), minedTiles[pick]);
            if (outliner != null) outliner.PaintAround(c.pos, OutlineClassAt);
        }

        if (FxManager.Instance != null)
            FxManager.Instance.Burst(CellToWorld(c.pos), kind == ResourceKind.Wood ? new Color(0.62f, 0.42f, 0.24f) : new Color(0.6f, 0.65f, 0.74f), 12, 2.2f);
    }
}
