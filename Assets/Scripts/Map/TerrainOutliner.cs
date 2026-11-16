using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Draws the ink outlines between terrain types on the scene's "Outlines" Tilemap.
/// Each boundary gets exactly ONE outline, drawn on the side of the LOWER-priority terrain
/// (water &lt; map border &lt; dirt &lt; grass). A cell looks at its 4 neighbours (edge bits) and at its diagonals
/// (inner-corner dots), and picks the matching generated tile from the arrays below
/// (index = edges &lt;&lt; 4 | corners; see MapArt.EdgeIndex). Lone water/dirt tiles therefore get a full outline
/// automatically (they stand alone), and so do grass islands in water.
/// </summary>
public class TerrainOutliner : MonoBehaviour
{
    // terrain classes, ordered by priority (higher = drawn "on top", gets no outline against lower ones)
    public const int Water = 0, Border = 1, Dirt = 2, Grass = 3;

    [Header("Scene references")]
    public Tilemap map;

    [Header("Outline tiles (256 slots each, index = edges<<4 | corners)")]
    public TileBase[] coastTiles = new TileBase[256];       // on water cells
    public TileBase[] borderTiles = new TileBase[256];      // on the map-border cells next to the playable map
    public TileBase[] dirtTiles = new TileBase[256];        // on dirt cells next to grass

    private static readonly Vector2Int[] Dir = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) }; // N E S W
    private static readonly Vector2Int[] Diag = { new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1) }; // NE SE SW NW

    public void Clear() { if (map != null) map.ClearAllTiles(); }

    /// <summary>Paints outlines for every cell in the inclusive rectangle. classAt must also answer for cells just outside it.</summary>
    public void Paint(Func<int, int, int> classAt, int x0, int y0, int x1, int y1)
    {
        if (map == null) return;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                map.SetTile(new Vector3Int(x, y, 0), TileFor(classAt, x, y));
    }

    /// <summary>Refreshes the 3x3 block around a cell that changed (e.g. a mined deposit became dirt).</summary>
    public void PaintAround(Vector2Int cell, Func<int, int, int> classAt)
    {
        Paint(classAt, cell.x - 1, cell.y - 1, cell.x + 1, cell.y + 1);
    }

    private TileBase TileFor(Func<int, int, int> classAt, int x, int y)
    {
        int k = classAt(x, y);
        TileBase[] set = k == Water ? coastTiles : (k == Border ? borderTiles : (k == Dirt ? dirtTiles : null));
        if (set == null) return null;

        int edges = 0;
        for (int i = 0; i < 4; i++)
            if (classAt(x + Dir[i].x, y + Dir[i].y) > k) edges |= 1 << i;

        int corners = 0;
        for (int i = 0; i < 4; i++)
        {
            // diagonal neighbour is higher, but neither neighbour next to it already draws an edge on this corner
            int adjA = 1 << i, adjB = 1 << ((i + 1) % 4);
            if ((edges & (adjA | adjB)) != 0) continue;
            if (classAt(x + Diag[i].x, y + Diag[i].y) > k) corners |= 1 << i;
        }

        int index = (edges << 4) | corners;
        return index == 0 ? null : set[index];
    }
}
