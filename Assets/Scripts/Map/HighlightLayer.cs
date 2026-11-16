using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum HighlightShape { Fill, Outline, Dot }

/// <summary>
/// Paints temporary highlights (placement footprint, entrance marker, selection box) on the scene's
/// "Highlight" Tilemap. Usage per frame: Begin() -> Paint(...) any number of times -> End().
/// Cells painted last frame but not this frame are cleared automatically.
/// </summary>
public class HighlightLayer : MonoBehaviour
{
    [Header("Scene references")]
    public Tilemap map;
    public TileBase fillTile;
    public TileBase outlineTile;
    public TileBase dotTile;

    private HashSet<Vector3Int> painted = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> frame = new HashSet<Vector3Int>();

    public void Begin() { frame.Clear(); }

    public void Paint(Vector2Int cell, HighlightShape shape, Color color)
    {
        if (map == null || GridManager.instance == null) return;
        Vector3Int p = map.WorldToCell(GridManager.instance.CellToWorld(cell));
        TileBase t = shape == HighlightShape.Fill ? fillTile : (shape == HighlightShape.Outline ? outlineTile : dotTile);
        map.SetTile(p, t);
        map.SetTileFlags(p, TileFlags.None);
        map.SetColor(p, color);
        frame.Add(p);
    }

    public void End()
    {
        if (map == null) return;
        foreach (var p in painted)
            if (!frame.Contains(p)) map.SetTile(p, null);
        var swap = painted; painted = frame; frame = swap;
    }

    /// <summary>Removes everything this layer painted.</summary>
    public void Clear()
    {
        Begin();
        End();
    }
}
