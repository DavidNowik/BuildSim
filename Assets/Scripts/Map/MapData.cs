using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Ground type of a cell. Water is neither walkable nor buildable.</summary>
public enum TerrainKind : byte { Grass = 0, Dirt = 1, Water = 2 }

/// <summary>Tunable parameters of the procedural map generator (shown in the MapGenerator inspector).</summary>
[Serializable]
public class MapSettings
{
    public int seed = 1234;

    [Tooltip("Map tier. Higher tiers produce bigger maps (see SizeForTier).")]
    public int tier = 0;
    public bool sizeFromTier = true;
    public Vector2Int customSize = new Vector2Int(32, 24);

    [Header("Terrain")]
    [Range(4f, 24f)] public float terrainScale = 10f;
    [Range(0f, 0.4f)] public float waterAmount = 0.14f;
    [Range(0f, 0.4f)] public float dirtAmount = 0.10f;

    [Header("Resources (1 = default density)")]
    [Range(0f, 3f)] public float stoneAmount = 1f;
    [Range(0f, 3f)] public float forestAmount = 1f;
    public int stoneYield = 100;
    public int woodYield = 50;

    [Header("Border")]
    public int margin = 8;

    public Vector2Int Size { get { return sizeFromTier ? SizeForTier(tier) : customSize; } }

    /// <summary>Maps grow with every tier (capped so they stay playable).</summary>
    public static Vector2Int SizeForTier(int tier)
    {
        tier = Mathf.Max(0, tier);
        return new Vector2Int(Mathf.Min(28 + 8 * tier, 120), Mathf.Min(20 + 6 * tier, 90));
    }
}

/// <summary>A pearl crate on a 2x2 footprint (anchor = bottom-left cell). Opened by paying products with the right tag.</summary>
[Serializable]
public class CrateData
{
    public Vector2Int anchor;
    public int variant;      // look (0 wood, 1 iron-bound, 2 gold)
    public int tag;          // ProductTag needed to open it
    public int amount;       // how many products of that tag
    public int pearls;       // reward
    public bool opened;
}

/// <summary>Plain-data description of a generated (or saved) map. Serialised to JSON in save files.</summary>
[Serializable]
public class MapData
{
    public int version = 1;
    public int seed;
    public int tier;
    public int width;
    public int height;
    public Vector2Int start;
    public byte[] terrain;    // TerrainKind per cell
    public byte[] resource;   // ResourceKind per cell
    public int[] yield;       // remaining amount per cell (0 = none)
    public List<CrateData> crates = new List<CrateData>();

    public MapData() { }

    public MapData(int width, int height)
    {
        this.width = width;
        this.height = height;
        terrain = new byte[width * height];
        resource = new byte[width * height];
        yield = new int[width * height];
    }

    public int Index(int x, int y) { return y * width + x; }
    public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < width && y < height; }

    public TerrainKind GetTerrain(int x, int y) { return (TerrainKind)terrain[Index(x, y)]; }
    public ResourceKind GetResource(int x, int y) { return (ResourceKind)resource[Index(x, y)]; }

    public bool IsLand(int x, int y) { return InBounds(x, y) && GetTerrain(x, y) != TerrainKind.Water; }

    public int CountResource(ResourceKind kind)
    {
        int n = 0;
        for (int i = 0; i < resource.Length; i++) if (resource[i] == (byte)kind) n++;
        return n;
    }
}
