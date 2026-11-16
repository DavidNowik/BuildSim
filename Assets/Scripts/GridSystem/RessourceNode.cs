using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RessourceNode : MonoBehaviour
{
    public string type;
    public int yield;

    public void Farm()
    {
        Debug.Log("Farm Tile "+name);
        yield--;

        if (yield <= 0)
        {
            // 1️⃣ Free the tile
            Tile tile = GetComponent<Tile>();
            if (tile != null)
            {
                tile.SetOccupied(false);
            }

            // 2️⃣ Remove overlay tile from Tilemap
            Tilemap overlay = FindObjectOfType<Tilemap>(); // or assign via inspector
            if (overlay != null)
            {
                Vector3Int cellPos = overlay.WorldToCell(transform.position);
                overlay.SetTile(cellPos, null); // removes the visual tile
            }
            gameObject.tag = "Untagged";
            Destroy(this);
        }
    }
}
public class RessourceCarry
{
    public string type;
    public int yield;

    // Constructor
    public RessourceCarry(string type, int yield)
    {
        this.type = type;
        this.yield = yield;
    }
}

