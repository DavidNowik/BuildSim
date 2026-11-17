using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Build mode.
///  - footprint is validated against GridManager data (per cell), not physics
///  - per-cell green/red highlight + entrance marker are painted on the scene's Highlight tilemap
///  - the grid fades in (GridOverlay), clicks on UI never place, RMB / Esc cancel, Shift keeps placing
///  - every failure gives feedback (toast, sound, red flash)
/// </summary>
[DefaultExecutionOrder(100)] // runs after SelectionController so a placing click is never also a select click
public class PlacementController : MonoBehaviour
{
    public static PlacementController Instance;

    [Header("Scene references")]
    public HighlightLayer highlight;
    public GridOverlay gridOverlay;

    public bool IsPlacing { get { return ghost != null; } }
    public Building CurrentPrefab { get { return prefab; } }

    private Building prefab;
    private Building ghost;
    private Camera cam;
    private Vector2Int lastAnchor = new Vector2Int(int.MinValue, int.MinValue);
    private float popTimer;
    private float flashTimer;
    private string reason = "";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // ------------------------------------------------------------ public API
    public void Begin(Building source)
    {
        if (source == null) return;
        Cancel(false);
        if (SelectionController.Instance != null) SelectionController.Instance.Clear();

        prefab = source;
        ghost = Instantiate(source);
        ghost.name = source.displayName + " (ghost)";
        ghost.SetGhost(true);
        ghost.gameObject.SetActive(false);
        lastAnchor = new Vector2Int(int.MinValue, int.MinValue);
        if (gridOverlay != null) gridOverlay.SetVisible(true);
        GameAudio.Play(Sfx.Click);
        if (HUD.Instance != null) HUD.Instance.Toast("Left click: place    Right click / Esc: cancel    Shift: keep placing", ToastKind.Info, 3.5f);
    }

    /// <summary>Same as left-clicking with the cursor over this cell while placing (harness / tests). Returns false with the reason when it was denied.</summary>
    public bool ClickCell(Vector2Int cell, out string why)
    {
        why = "not placing";
        if (ghost == null) return false;
        Vector2Int anchor = ghost.AnchorForCell(cell);
        if (!Validate(anchor, out why)) { Deny(why); return false; }
        ghost.gameObject.SetActive(true);   // a real cursor hover would have shown the ghost already
        Place(anchor);
        return true;
    }

    public void Cancel(bool sound = true)
    {
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = null;
        prefab = null;
        if (highlight != null) highlight.Clear();
        if (gridOverlay != null) gridOverlay.SetVisible(false);
        if (sound) GameAudio.Play(Sfx.Click, 0.6f, 0.8f);
    }

    // ------------------------------------------------------------ loop
    private void Update()
    {
        if (ghost == null) return;
        if (cam == null) cam = Camera.main;
        var grid = GridManager.instance;

        if (Input.GetKeyDown(KeyCode.Escape) || (Input.GetMouseButtonUp(1) && !CameraMotor.RightDragged))
        {
            Cancel();
            return;
        }

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (overUI)
        {
            ghost.gameObject.SetActive(false);
            if (highlight != null) highlight.Clear();
            return;
        }

        Vector3 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int cell = grid.WorldToCell(mouse);
        Vector2Int anchor = ghost.AnchorForCell(cell);

        if (anchor != lastAnchor)
        {
            lastAnchor = anchor;
            popTimer = 0.1f;
        }

        bool valid = Validate(anchor, out reason);

        // ghost visuals
        ghost.gameObject.SetActive(true);
        Vector3 root = ghost.RootPositionForAnchor(anchor);
        root.x = Mathf.Round(root.x * 16f) / 16f;
        root.y = Mathf.Round(root.y * 16f) / 16f;
        ghost.transform.position = root;

        float pulse = 0.72f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);
        ghost.SetTint(valid ? new Color(0.72f, 1f, 0.72f, pulse) : new Color(1f, 0.5f, 0.5f, pulse));

        popTimer -= Time.unscaledDeltaTime;
        float pop = popTimer > 0f ? 1f + 0.05f * (popTimer / 0.1f) : 1f;
        ghost.transform.localScale = Vector3.Scale(prefab.transform.localScale, new Vector3(pop, pop, 1f));

        DrawHighlight(anchor, valid);

        if (Input.GetMouseButtonDown(0))
        {
            if (valid) Place(anchor);
            else Deny(reason);
        }
    }

    // ------------------------------------------------------------ validation
    private bool Validate(Vector2Int anchor, out string why)
    {
        var grid = GridManager.instance;
        var size = ghost.footprint;
        why = "";

        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                if (!grid.InBounds(anchor.x + x, anchor.y + y)) { why = "Outside the map"; return false; }

        if (!grid.CanPlace(anchor, size)) { why = "Something is in the way"; return false; }
        if (!BuildingManager.instance.HasDoorCell(anchor, size)) { why = "Needs a free tile next to it"; return false; }
        if (BuildingManager.instance.WouldBlockDoors(anchor, size)) { why = "Would block another building's entrance"; return false; }
        if (!ResourceManager.Instance.CanAfford(prefab.Cost)) { why = "Not enough resources (" + prefab.Cost + ")"; return false; }
        return true;
    }

    private void Place(Vector2Int anchor)
    {
        ResourceManager.Instance.Spend(prefab.Cost);
        var placed = ghost;
        ghost = null;
        placed.Place(anchor);
        if (highlight != null) highlight.Clear();

        var source = prefab;
        bool keep = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        prefab = null;
        if (keep && ResourceManager.Instance.CanAfford(source.Cost)) Begin(source);
        else if (gridOverlay != null) gridOverlay.SetVisible(false);
    }

    private void Deny(string why)
    {
        GameAudio.Play(Sfx.Deny);
        if (HUD.Instance != null) HUD.Instance.Toast(why, ToastKind.Warning);
        flashTimer = 0.25f;
        if (CameraMotor.Instance != null) CameraMotor.Instance.Shake(0.03f, 0.08f);
    }

    // ------------------------------------------------------------ highlight
    private void DrawHighlight(Vector2Int anchor, bool valid)
    {
        if (highlight == null) return;
        var grid = GridManager.instance;
        var size = ghost.footprint;

        flashTimer -= Time.unscaledDeltaTime;
        bool flash = flashTimer > 0f && ((int)(flashTimer * 40f) % 2 == 0);

        highlight.Begin();
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int p = new Vector2Int(anchor.x + x, anchor.y + y);
                if (!grid.InBounds(p)) continue;
                bool cellOk = grid.GetCell(p).Buildable;
                Color col = (cellOk && valid) ? new Color(0.35f, 1f, 0.45f, 0.38f) : new Color(1f, 0.3f, 0.3f, 0.45f);
                if (flash) col = new Color(1f, 1f, 1f, 0.7f);
                highlight.Paint(p, HighlightShape.Fill, col);
            }

        // entrance marker (south of the footprint's centre)
        Vector2Int door = new Vector2Int(anchor.x + (size.x - 1) / 2, anchor.y - 1);
        if (grid.InBounds(door) && grid.GetCell(door).Walkable)
            highlight.Paint(door, HighlightShape.Dot, new Color(1f, 0.92f, 0.45f, 0.9f));
        highlight.End();
    }
}
