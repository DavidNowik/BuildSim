using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Draws the deposits (trees, rocks) one tilemap per map row, each with its own Order In Layer
/// (-(row + 0.5) * 100, the same scale workers use). Lower rows therefore always draw in front of higher rows,
/// and workers walk in front of / behind trees correctly. The Deposits tilemap itself stays as the data source
/// (its renderer is switched off); the row objects are children of this object and visible in the Hierarchy.
/// </summary>
[RequireComponent(typeof(Tilemap))]
public class DepositRows : MonoBehaviour
{
    public int orderScale = 100;

    private Tilemap data;
    private Tilemap[] rows;

    private void EnsureData()
    {
        if (data == null) data = GetComponent<Tilemap>();
    }

    /// <summary>Rebuilds every row from the data tilemap.</summary>
    public void Rebuild()
    {
        EnsureData();
        var src = data.GetComponent<TilemapRenderer>();
        if (src != null) src.enabled = false;

        data.CompressBounds();
        var b = data.cellBounds;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) { c.transform.SetParent(null); Destroy(c); } else DestroyImmediate(c);
        }
        rows = new Tilemap[Mathf.Max(0, b.size.y)];
        for (int y = b.yMin; y < b.yMax; y++)
        {
            for (int x = b.xMin; x < b.xMax; x++)
            {
                var p = new Vector3Int(x, y, 0);
                var t = data.GetTile(p);
                if (t == null) continue;
                Row(y).SetTile(p, t);
            }
        }
    }

    /// <summary>Removes a deposit's visual (the data tile is removed by the caller).</summary>
    public void Remove(Vector3Int cell)
    {
        EnsureData();
        if (rows == null) return;
        int i = cell.y - data.cellBounds.yMin;
        if (i >= 0 && i < rows.Length && rows[i] != null) rows[i].SetTile(cell, null);
    }

    public void ClearAll()
    {
        EnsureData();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) { c.transform.SetParent(null); Destroy(c); } else DestroyImmediate(c);
        }
        rows = null;
    }

    private Tilemap Row(int y)
    {
        int i = y - data.cellBounds.yMin;
        if (rows[i] != null) return rows[i];
        var go = new GameObject("Row " + y);
        go.transform.SetParent(transform, false);
        var tm = go.AddComponent<Tilemap>();
        var tr = go.AddComponent<TilemapRenderer>();
        var src = data.GetComponent<TilemapRenderer>();
        tr.sortingLayerID = src.sortingLayerID;
        tr.sortingOrder = -Mathf.RoundToInt((y + 0.5f) * orderScale);
        tr.sharedMaterial = src.sharedMaterial;
        rows[i] = tm;
        return tm;
    }
}
