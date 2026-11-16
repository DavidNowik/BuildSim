using UnityEngine;

/// <summary>A pearl crate on a 2x2 footprint. It is a plain scene object (child of the CrateManager); the state lives in <see cref="data"/>.</summary>
public class Crate : MonoBehaviour
{
    public CrateData data;
    public SpriteRenderer body;

    public ProductTag Tag { get { return (ProductTag)data.tag; } }
    public bool Opened { get { return data.opened; } }

    /// <summary>The four grid cells this crate sits on.</summary>
    public Vector2Int[] Cells
    {
        get
        {
            var a = data.anchor;
            return new[] { a, a + Vector2Int.right, a + Vector2Int.up, a + Vector2Int.one };
        }
    }

    public void Refresh(Sprite closed, Sprite open)
    {
        if (body != null) body.sprite = data.opened ? open : closed;
    }
}
