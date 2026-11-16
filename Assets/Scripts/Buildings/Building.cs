using System.Collections.Generic;
using UnityEngine;

public class Building : MonoBehaviour
{
    [Header("Building Settings")]
    public string type;

    // Protected so children can access
    protected SpriteRenderer spriteRenderer;
    protected Collider2D col;

    protected bool isPlacing = false;
    protected bool placed = false;
    protected List<Tile> overlappingTiles = new List<Tile>();
    public List<Tile> occupiedTiles = new List<Tile>();

    private static Building ghostInstance; // single ghost reference

    // --- Unity Lifecycle ---
    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    protected virtual void Update()
    {
        if (placed) return;

        if (!isPlacing)
        {
            Tile.updateTimer = 1f;
            return;
        }

        Tile.updateTimer = 0.1f;

        FollowMouse();
        CheckCollisions();

        if (Input.GetMouseButtonDown(0))
            PlaceBuilding();
        else if (Input.GetMouseButtonDown(1))
            CancelPlacement();
    }

    // --- Placement Mode ---
    public virtual void EnterPlacementMode()
    {
        if (ghostInstance != null)
            Destroy(ghostInstance.gameObject);

        ghostInstance = Instantiate(this);
        ghostInstance.isPlacing = true;
        ghostInstance.SetTransparency(0.5f);
    }

    protected virtual void FollowMouse()
    {
        if (col == null) return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;

        // Snap to grid
        float spacing = GridManager.instance.tileSpacing;
        int gridX = Mathf.RoundToInt(mouseWorld.x / spacing);
        int gridY = Mathf.RoundToInt(mouseWorld.y / spacing);

        gridX = Mathf.Clamp(gridX, 0, GridManager.instance.width - 1);
        gridY = Mathf.Clamp(gridY, 0, GridManager.instance.height - 1);

        Tile tile = GridManager.instance.GetTile(gridX, gridY);
        if (tile != null)
            transform.position = tile.transform.position;
    }

    protected virtual void CheckCollisions()
    {
        overlappingTiles.Clear();
        Collider2D[] hits = Physics2D.OverlapBoxAll(col.bounds.center, col.bounds.size, 0);

        bool valid = true;

        foreach (Collider2D hit in hits)
        {
            Tile tile = hit.GetComponent<Tile>();
            if (tile != null)
            {
                overlappingTiles.Add(tile);
                if (tile.IsOccupied)
                {
                    tile.Highlight(true);
                    valid = false;
                }
                else
                {
                    tile.Highlight(true);
                }
            }
        }

        if (spriteRenderer != null)
            spriteRenderer.color = valid ? Color.green : Color.red;
    }

    protected virtual void PlaceBuilding()
    {
        foreach (Tile t in overlappingTiles)
        {
            if (t.IsOccupied)
            {
                Debug.Log("Tiles are already occupied");
                return;
            }
        }

        isPlacing = false;
        ResetSprite();

        foreach (Tile t in overlappingTiles)
        {
            occupiedTiles.Add(t);
            t.SetOccupied(true, gameObject);
        }

        BuildingManager.instance.RegisterBuilding(this);
        ghostInstance = null;
        placed = true;
    }

    protected virtual void CancelPlacement()
    {
        Destroy(gameObject);
        ghostInstance = null;
    }

    // --- Utility ---
    protected void SetTransparency(float alpha)
    {
        if (spriteRenderer == null) return;
        Color c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
    }

    protected void ResetSprite()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = Color.white;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (col != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
