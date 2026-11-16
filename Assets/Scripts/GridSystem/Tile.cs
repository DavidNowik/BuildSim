using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using UnityEngine.Timeline;

public class Tile : MonoBehaviour
{
    public Vector2Int GridPosition;
    public bool IsOccupied = false;
    public float MoveCost = 1f;
    public GameObject Occupier = null;

    private SpriteRenderer spriteRenderer;
    private Tilemap tilemap;
    [HideInInspector]
    public static float updateTimer = 1;

    // Highlight
    private bool highlighted = false;
    private Color baseColor = Color.green;
    private Color highlightColor = new Color(1f, 1f, 0f, 0.5f); // yellow-ish

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        tilemap = FindObjectOfType<Tilemap>(); // optionally assign via Inspector
    }

    void Start()
    {
        if (tilemap != null)
            StartCoroutine(CheckTilemapRoutine());
    }

    IEnumerator CheckTilemapRoutine()
    {
        while (true)
        {
            CheckTilemap();
            yield return new WaitForSeconds(updateTimer);
        }
    }

    private bool isInitialized = false;

    void CheckTilemap()
    {
        if (tilemap == null) return;

        Vector3Int cellPos = tilemap.WorldToCell(transform.position);
        TileBase tile = tilemap.GetTile(cellPos);

        if (tile != null)
        {
            // Only init if not already initialized
            if (!isInitialized)
            {
                InitNode(tile, cellPos);
                isInitialized = true;
            }
        }
        else
        {
            // Tile was mined / deleted
            EmptyNode();
            isInitialized = false; // allow re-initialization when a new tile is placed
        }

        // Highlight logic
        if (highlighted)
        {
            spriteRenderer.enabled = true;
            if (IsOccupied)
            {
                SetTransparency(0.4f);
                UpdateColor(Color.red);
            }
            else
            {
                SetTransparency(0.1f);
                UpdateColor(Color.green);
            }
        }
        else
        {
            spriteRenderer.enabled = false;
        }

        highlighted = false;
    }


    public void Highlight(bool isHighlighted)
    {
        highlighted = isHighlighted;
    }

    private void SetTransparency(float alpha)
    {
        if (spriteRenderer == null) return;
        Color c = spriteRenderer.material.color;
        c.a = alpha;
        spriteRenderer.material.color = c;
    }

    private void EmptyNode()
    {
        IsOccupied = false;
        Occupier = null;
    }

    private void InitNode(TileBase tileBase, Vector3Int cellPos)
    {
        if (tileBase == null)
        {
            Debug.LogWarning($"[{name}] InitNode called with NULL tileBase at {cellPos}");
            return;
        }

        Debug.Log($"[{name}] InitNode: Found tileBase '{tileBase.name}' at {cellPos}");

        switch (tileBase.name)
        {
            case "Stone":
                IsOccupied = true;
                AddOrUpdateNode("Stone", 100);
                tag = "stone";
                Debug.Log($"[{name}] Stone node initialized at {cellPos}");
                break;

            case "Wood":
                IsOccupied = true;
                AddOrUpdateNode("Wood", 50);
                tag = "wood";
                Debug.Log($"[{name}] Wood node initialized at {cellPos}");
                break;

            default:
                IsOccupied = false;
                Debug.Log($"[{name}] Unrecognized tile '{tileBase.name}' at {cellPos}, marked as free.");
                break;
        }
    }


    private void AddOrUpdateNode(string type, int yield)
    {
        RessourceNode node = GetComponent<RessourceNode>();
        if (node == null)
            node = gameObject.AddComponent<RessourceNode>();

        node.type = type;
        node.yield = yield;
    }

    public void SetOccupied(bool occupied, GameObject obj = null)
    {
        IsOccupied = occupied;
        Occupier = obj;
    }

    private void UpdateColor(Color color)
    {
        if (spriteRenderer == null) return;

        Color targetColor = color;
        spriteRenderer.color = targetColor;
    }
}
