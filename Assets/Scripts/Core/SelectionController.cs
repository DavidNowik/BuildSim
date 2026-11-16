using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Click to inspect workers, buildings and resource deposits. Provides hover info for the tooltip.</summary>
public class SelectionController : MonoBehaviour
{
    public static SelectionController Instance;

    [Header("Scene references")]
    public HighlightLayer highlight;
    [Tooltip("Selection outline colour")]
    public Color outlineColor = new Color(1f, 0.95f, 0.5f, 0.95f);

    public Worker SelectedWorker { get; private set; }
    public Building SelectedBuilding { get; private set; }
    public Cell SelectedNode { get; private set; }
    /// <summary>Selected pearl crate (shown by the CratePanel, not by the HUD inspector).</summary>
    public Crate SelectedCrate { get; private set; }
    public bool HasSelection { get { return SelectedWorker != null || SelectedBuilding != null || SelectedNode != null; } }

    /// <summary>Text for the hover tooltip (null when nothing interesting is under the cursor).</summary>
    public string HoverText { get; private set; }

    private Camera cam;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Clear()
    {
        if (SelectedWorker != null) SelectedWorker.SetSelected(false);
        SelectedWorker = null;
        SelectedBuilding = null;
        SelectedNode = null;
        SelectedCrate = null;
    }

    private void Select(Worker w, Building b, Cell n, Crate cr)
    {
        if (SelectedWorker != null) SelectedWorker.SetSelected(false);
        SelectedWorker = w; SelectedBuilding = b; SelectedNode = n; SelectedCrate = cr;
        if (w != null) w.SetSelected(true);
        GameAudio.Play(Sfx.Click, 0.5f, 1.2f);
    }

    private void Update()
    {
        if (cam == null) cam = Camera.main;
        var grid = GridManager.instance;
        if (cam == null || grid == null) return;

        // drop stale references (destroyed building etc.)
        if (SelectedBuilding != null && !SelectedBuilding.IsPlaced) Clear();
        if (SelectedNode != null && !SelectedNode.HasNode) Clear();

        bool placing = PlacementController.Instance != null && PlacementController.Instance.IsPlacing;
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        HoverText = null;
        if (!placing && !overUI)
        {
            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            Worker hw; Building hb; Cell hn; Crate hc;
            Pick(mw, out hw, out hb, out hn, out hc);
            if (hw != null) HoverText = hw.workerName + " - " + hw.JobTitle;
            else if (hc != null) HoverText = hc.Opened ? "Crate (opened)" : "Pearl crate - needs " + hc.data.amount + " " + Products.TagName(hc.Tag).ToLower();
            else if (hb != null) HoverText = hb.displayName;
            else if (hn != null) HoverText = ResourceInfo.Name(hn.resource) + " deposit  " + hn.yield + "/" + hn.maxYield;

            if (Input.GetMouseButtonDown(0)) Select(hw, hb, hn, hc);
        }

        if ((HasSelection || SelectedCrate != null) && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) && !placing) Clear();
        if (!DebugConsole.IsOpen && Input.GetKeyDown(KeyCode.Delete) && SelectedBuilding != null && !placing) DemolishSelected();

        DrawSelection(placing);
    }

    /// <summary>Selects a crate from code (tests, debug commands).</summary>
    public void SelectCrate(Crate c) { Select(null, null, null, c); }

    public void DemolishSelected()
    {
        if (SelectedBuilding == null) return;
        var b = SelectedBuilding;
        Clear();
        if (HUD.Instance != null) HUD.Instance.Toast(b.displayName + " demolished (50% refund)", ToastKind.Info);
        b.Demolish();
    }

    private static void Pick(Vector3 mw, out Worker w, out Building b, out Cell n, out Crate cr)
    {
        w = null; b = null; n = null; cr = null;

        float best = 0.6f;
        for (int i = 0; i < Worker.All.Count; i++)
        {
            var cand = Worker.All[i];
            if (cand.IsHidden) continue;
            float d = Vector2.Distance(mw, cand.transform.position);
            if (d < best) { best = d; w = cand; }
        }
        if (w != null) return;

        var grid = GridManager.instance;
        Cell c = grid.GetCell(grid.WorldToCell(mw));
        if (c == null) return;
        if (c.crate != null) { cr = c.crate; return; }
        if (c.building != null) { b = c.building; return; }
        if (c.HasNode) n = c;
    }

    /// <summary>Outlines the selected building footprint / deposit on the Highlight tilemap.</summary>
    private void DrawSelection(bool placing)
    {
        if (highlight == null) return;
        // while placing, the PlacementController owns the highlight layer
        if (placing) return;

        highlight.Begin();
        float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);
        var col = new Color(outlineColor.r, outlineColor.g, outlineColor.b, pulse);

        if (SelectedBuilding != null && SelectedBuilding.IsPlaced)
        {
            foreach (var c in SelectedBuilding.occupiedCells) highlight.Paint(c.pos, HighlightShape.Outline, col);
        }
        else if (SelectedNode != null)
        {
            highlight.Paint(SelectedNode.pos, HighlightShape.Outline, col);
        }
        else if (SelectedCrate != null)
        {
            foreach (var p in SelectedCrate.Cells) highlight.Paint(p, HighlightShape.Outline, col);
        }
        highlight.End();
    }
}
