using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PathFinder
{
    public static List<Tile> FindPath(Tile start, Tile goal)
    {
        List<Tile> openSet = new List<Tile> { start };
        HashSet<Tile> closedSet = new HashSet<Tile>();

        Dictionary<Tile, Tile> cameFrom = new Dictionary<Tile, Tile>();
        Dictionary<Tile, float> gScore = new Dictionary<Tile, float> { [start] = 0 };
        Dictionary<Tile, float> fScore = new Dictionary<Tile, float> { [start] = Heuristic(start, goal) };

        while (openSet.Count > 0)
        {
            // Find lowest fScore
            Tile current = openSet[0];
            foreach (Tile t in openSet)
            {
                if (fScore.ContainsKey(t) && fScore[t] < fScore[current])
                    current = t;
            }

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (Tile neighbor in GetNeighbors(current))
            {
                if (neighbor.IsOccupied || closedSet.Contains(neighbor))
                    continue;

                float tentativeG = gScore[current] + neighbor.MoveCost;

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(neighbor, goal);

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return null; // No path found
    }
    static float Heuristic(Tile a, Tile b)
    {
        return Vector2Int.Distance(a.GridPosition, b.GridPosition);
    }
    static List<Tile> ReconstructPath(Dictionary<Tile, Tile> cameFrom, Tile current)
    {
        List<Tile> totalPath = new List<Tile> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            totalPath.Insert(0, current);
        }
        return totalPath;
    }
    static List<Tile> GetNeighbors(Tile tile)
    {
        List<Tile> neighbors = new List<Tile>();
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1,0), new Vector2Int(-1,0),
            new Vector2Int(0,1), new Vector2Int(0,-1)
        };

        foreach (var dir in directions)
        {
            Tile neighbor = GridManager.instance.GetTile(tile.GridPosition.x + dir.x, tile.GridPosition.y + dir.y);
            if (neighbor != null)
                neighbors.Add(neighbor);
        }

        return neighbors;
    }
    public static Tile GetFreeNeighborTile(Tile resourceTile)
    {
        Vector2Int[] dirs = new Vector2Int[]
        {
        new Vector2Int(1, 0),   // East
        new Vector2Int(-1, 0),  // West
        new Vector2Int(0, 1),   // North
        new Vector2Int(0, -1)   // South
        };

        // Shuffle directions
        for (int i = 0; i < dirs.Length; i++)
        {
            int rnd = Random.Range(i, dirs.Length);
            Vector2Int temp = dirs[i];
            dirs[i] = dirs[rnd];
            dirs[rnd] = temp;
        }

        foreach (Vector2Int dir in dirs)
        {
            Tile neighbor = GridManager.instance.GetTile(resourceTile.GridPosition.x + dir.x,
                                                        resourceTile.GridPosition.y + dir.y);
            if (neighbor != null && !neighbor.IsOccupied)
                return neighbor;
        }

        return null;
    }
}
