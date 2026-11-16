using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager instance; // Singleton reference

    [Header("Grid Settings")]
    public Tile tilePrefab;
    public int width = 10;
    public int height = 10;
    public float tileSpacing = 1;
    public bool IsInitialized { get; private set; } = false;


    private Tile[,] grid;

    private void Awake()
    {
        // Basic singleton logic (no DontDestroyOnLoad)
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Multiple GridManagers found! Destroying duplicate.");
            Destroy(this.gameObject);
            return;
        }
        grid = new Tile[width, height];
        InitializeGrid();

        IsInitialized = true;
        instance = this;

    }

    void InitializeGrid()
    {
        if (tilePrefab == null)
        {
            Debug.LogError("Tile prefab not assigned!");
            return;
        }

        Vector3 startPos = transform.position;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Position auf X/Y Ebene
                Vector3 pos = startPos + new Vector3(x * tileSpacing, y * tileSpacing, 0f);

                Tile newTile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
                newTile.name = $"Tile_{x}_{y}";
                newTile.GridPosition = new Vector2Int(x, y);
                grid[x, y] = newTile;

                // Schachbrett Alpha-Muster
                Renderer rend = newTile.GetComponent<Renderer>();
                if (rend != null)
                {
                    Color baseColor = rend.material.color;
                    float alpha = ((x + y) % 2 == 0) ? 0.2f : 0.3f;
                    rend.material.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                }
            }
        }
    }

    // Example getter
    public Tile GetTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return null;
        return grid[x, y];
    }
}
