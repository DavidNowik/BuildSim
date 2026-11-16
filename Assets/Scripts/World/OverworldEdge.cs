using UnityEngine;

public enum EdgeState { Hidden, Open, Done }

/// <summary>A path between two lands (LineRenderer on the Edge prefab). Open paths flow with moving dashes; done paths are solid.</summary>
[RequireComponent(typeof(LineRenderer))]
public class OverworldEdge : MonoBehaviour
{
    public LineRenderer line;
    public float flowSpeed = 1.1f;
    [Tooltip("Length of one dash + gap in world units")]
    public float dashLength = 0.45f;

    private EdgeState state = EdgeState.Hidden;
    private Material mat;
    private float offset;
    private float dashTiles = 1f;
    private float baseWidth = 0.1f;
    private Color baseColor = Color.white;

    public void Setup(Vector3 a, Vector3 b)
    {
        if (line == null) line = GetComponent<LineRenderer>();
        baseWidth = line.widthMultiplier;
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        mat = line.material;            // per-edge instance so the dash offset animates independently
        dashTiles = Mathf.Max(1f, Vector3.Distance(a, b) / dashLength);
    }

    public void SetState(EdgeState s, Color color)
    {
        state = s;
        line.enabled = s != EdgeState.Hidden;
        baseColor = color;
        line.startColor = line.endColor = color;
        line.widthMultiplier = baseWidth * (s == EdgeState.Done ? 1.2f : 1f);
        // done = solid (a tiny slice of the dash texture stretched along the line), open = dashed
        if (mat != null) mat.mainTextureScale = new Vector2(s == EdgeState.Done ? 0.001f : dashTiles, 1f);
    }

    /// <summary>Fades the whole path (used while the tide washes the world away).</summary>
    public void SetFade(float f)
    {
        var c = baseColor; c.a *= f;
        line.startColor = line.endColor = c;
    }

    private void Update()
    {
        if (state != EdgeState.Open || mat == null) return;
        offset -= Time.deltaTime * flowSpeed;
        mat.mainTextureOffset = new Vector2(offset, 0f);
    }

    private void OnDestroy() { if (mat != null) Destroy(mat); }
}
