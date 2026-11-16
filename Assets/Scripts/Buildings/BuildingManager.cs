using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager instance;

    private readonly List<Building> buildings = new List<Building>();
    public IReadOnlyList<Building> All => buildings;


    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Multiple BuildingManagers found! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    public void RegisterBuilding(Building building)
    {
        if (!buildings.Contains(building)) buildings.Add(building);
    }

    public void UnregisterBuilding(Building building)
    {
        buildings.Remove(building);
    }

    public Building GetNearest(BuildingRole role, Vector3 position)
    {
        Building best = null;
        float min = float.MaxValue;
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b.role != role) continue;
            float d = (b.transform.position - position).sqrMagnitude;
            if (d < min) { min = d; best = b; }
        }
        return best;
    }

    public Building GetNearestWithRoom(BuildingRole role, Vector3 position)
    {
        Building best = null;
        float min = float.MaxValue;
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b.role != role || b.FreeSlots <= 0) continue;
            float d = (b.transform.position - position).sqrMagnitude;
            if (d < min) { min = d; best = b; }
        }
        return best;
    }

    public int TotalCapacity()
    {
        int n = 0;
        for (int i = 0; i < buildings.Count; i++) n += buildings[i].maxWorkers;
        return n;
    }


    public bool HasRole(BuildingRole role)
    {
        for (int i = 0; i < buildings.Count; i++) if (buildings[i].role == role) return true;
        return false;
    }

    /// <summary>
    /// A walkable cell next to the building where workers stand to use it (deliver / enter).
    /// Prefers the south side (the entrance) and cells near the footprint's centre line.
    /// Always evaluated fresh, so it never points at a cell that has since been built on.
    /// </summary>
    public bool TryGetDoorCell(Building b, Vector3 from, out Vector2Int result)
    {
        result = default(Vector2Int);
        var grid = GridManager.instance;
        if (b == null || grid == null) return false;

        Vector2Int a = b.Anchor, size = b.footprint;
        float centerX = a.x + (size.x - 1) * 0.5f;
        Vector2Int fromCell = grid.WorldToCell(from);

        bool found = false;
        float best = float.MaxValue;

        for (int x = a.x - 1; x <= a.x + size.x; x++)
        {
            for (int y = a.y - 1; y <= a.y + size.y; y++)
            {
                bool insideX = x >= a.x && x < a.x + size.x;
                bool insideY = y >= a.y && y < a.y + size.y;
                if (insideX && insideY) continue;                 // footprint itself
                if (!insideX && !insideY) continue;               // corners are not adjacent
                Cell c = grid.GetCell(x, y);
                if (c == null || !c.Walkable) continue;

                bool south = y < a.y;
                float score = Mathf.Abs(x - centerX) + (south ? 0f : 4f)
                              + 0.05f * (Mathf.Abs(x - fromCell.x) + Mathf.Abs(y - fromCell.y));
                if (score < best) { best = score; result = new Vector2Int(x, y); found = true; }
            }
        }
        return found;
    }

    /// <summary>Fills <paramref name="result"/> with buildings of a role, nearest first.</summary>
    public void CollectByDistance(BuildingRole role, Vector3 position, List<Building> result)
    {
        result.Clear();
        for (int i = 0; i < buildings.Count; i++) if (buildings[i].role == role) result.Add(buildings[i]);
        result.Sort((a, b) =>
            (a.transform.position - position).sqrMagnitude.CompareTo((b.transform.position - position).sqrMagnitude));
    }

    /// <summary>
    /// True if placing a footprint at (anchor,size) would remove the last free door cell of any existing building.
    /// </summary>
    public bool WouldBlockDoors(Vector2Int anchor, Vector2Int size)
    {
        var grid = GridManager.instance;
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (!b.IsPlaced) continue;
            bool any = false;
            Vector2Int a = b.Anchor, s = b.footprint;
            for (int x = a.x - 1; x <= a.x + s.x && !any; x++)
                for (int y = a.y - 1; y <= a.y + s.y && !any; y++)
                {
                    bool insideX = x >= a.x && x < a.x + s.x;
                    bool insideY = y >= a.y && y < a.y + s.y;
                    if (insideX == insideY) continue;
                    Cell c = grid.GetCell(x, y);
                    if (c == null || !c.Walkable) continue;
                    bool inNew = x >= anchor.x && x < anchor.x + size.x && y >= anchor.y && y < anchor.y + size.y;
                    if (!inNew) any = true;
                }
            if (!any) return true;
        }
        return false;
    }

    /// <summary>True if at least one cell around the footprint would be walkable.</summary>
    public bool HasDoorCell(Vector2Int anchor, Vector2Int size)
    {
        var grid = GridManager.instance;
        for (int x = anchor.x - 1; x <= anchor.x + size.x; x++)
            for (int y = anchor.y - 1; y <= anchor.y + size.y; y++)
            {
                bool insideX = x >= anchor.x && x < anchor.x + size.x;
                bool insideY = y >= anchor.y && y < anchor.y + size.y;
                if (insideX == insideY) continue;
                Cell c = grid.GetCell(x, y);
                if (c != null && c.Walkable) return true;
            }
        return false;
    }
}
