using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Build-mode grid lines. The lines are painted once by the MapGenerator onto the scene's "Overlay" Tilemap;
/// this component only fades that Tilemap in and out.
/// </summary>
public class GridOverlay : MonoBehaviour
{
    public static GridOverlay Instance { get; private set; }

    public Tilemap map;
    public float fadeSeconds = 0.15f;

    private float target, alpha;

    private void Awake()
    {
        Instance = this;
        alpha = target = 0f;
        Apply();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void SetVisible(bool visible) { target = visible ? 1f : 0f; }

    private void Update()
    {
        if (Mathf.Approximately(alpha, target)) return;
        alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
        Apply();
    }

    private void Apply()
    {
        if (map != null) map.color = new Color(1f, 1f, 1f, alpha);
    }
}
