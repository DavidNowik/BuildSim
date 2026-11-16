using System.Collections.Generic;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager instance;

    private List<Building> buildings = new List<Building>();
    public bool IsInitialized { get; private set; } = false;
    private void Awake()
    {
        // Basic singleton logic (no DontDestroyOnLoad)
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Multiple BuildingManagers found! Destroying duplicate.");
            Destroy(this.gameObject);
            return;
        }
        IsInitialized = true;
        instance = this;
    }

    public void RegisterBuilding(Building building)
    {
        if (!buildings.Contains(building))
            buildings.Add(building);
    }

    public void UnregisterBuilding(Building building)
    {
        buildings.Remove(building);
    }

    // Example: get nearest building of type
    public Building GetNearestBuilding(Vector3 position, string type)
    {
        Building nearest = null;
        float minDist = float.MaxValue;
        foreach (var b in buildings)
        {
            if (b.type == type)
            {
                float dist = Vector3.Distance(position, b.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = b;
                }
            }
            
        }
        return nearest;
    }
    public Tile GetAnyAdjacentWalkableTile(Building building)
    {
        if (building == null || building.occupiedTiles.Count == 0)
            return null;

        Tile[] tiles = building.occupiedTiles.ToArray();
        Vector2Int[] dirs = new Vector2Int[]
        {
        new Vector2Int(1,0), new Vector2Int(-1,0),
        new Vector2Int(0,1), new Vector2Int(0,-1)
        };

        int startTile = Random.Range(0, tiles.Length);

        for (int t = 0; t < tiles.Length; t++)
        {
            Tile tile = tiles[(startTile + t) % tiles.Length];

            int startDir = Random.Range(0, dirs.Length);
            for (int d = 0; d < dirs.Length; d++)
            {
                Vector2Int dir = dirs[(startDir + d) % dirs.Length];
                Tile neighbor = GridManager.instance.GetTile(tile.GridPosition.x + dir.x, tile.GridPosition.y + dir.y);

                if (neighbor != null && !neighbor.IsOccupied)
                    return neighbor;
            }
        }

        return null;
    }




}
