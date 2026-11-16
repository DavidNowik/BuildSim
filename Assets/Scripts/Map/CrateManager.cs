using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the pearl crates of the level: spawns them from the map data (children of this object, visible in the Hierarchy),
/// blocks their grid cells, hands them back for saving and opens them when a player pays the required products.
/// </summary>
public class CrateManager : MonoBehaviour
{
    public static CrateManager Instance;

    [Header("Scene references")]
    public GridManager grid;
    public Crate prefab;
    [Tooltip("Index = CrateData.variant")]
    public Sprite[] closedSprites;
    public Sprite[] openSprites;

    private readonly List<Crate> crates = new List<Crate>();
    public IReadOnlyList<Crate> All { get { return crates; } }

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start() { RegisterAll(); }

    // ------------------------------------------------------------ spawning
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) { c.transform.SetParent(null); Destroy(c); }   // detach first: Destroy is deferred
            else DestroyImmediate(c);
        }
        crates.Clear();
    }

    /// <summary>Creates the crate objects for a map (editor generate, or loading a save).</summary>
    public void Spawn(MapData d)
    {
        Clear();
        if (prefab == null || d == null || d.crates == null) return;
        foreach (var cd in d.crates) Make(cd);
    }

    private void Make(CrateData cd)
    {
        Crate c;
#if UNITY_EDITOR
        c = Application.isPlaying ? Instantiate(prefab, transform) : (Crate)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform);
#else
        c = Instantiate(prefab, transform);
#endif
        c.data = new CrateData { anchor = cd.anchor, variant = cd.variant, tag = cd.tag, amount = cd.amount, pearls = cd.pearls, opened = cd.opened };
        c.name = "Crate " + cd.anchor.x + "," + cd.anchor.y;
        // centre of the 2x2 block (cell centres sit on whole numbers)
        float s = grid != null ? grid.tileSpacing : 1f;
        Vector3 origin = grid != null ? grid.transform.position : Vector3.zero;
        origin.z = 0f;
        c.transform.position = origin + new Vector3((cd.anchor.x + 0.5f) * s, (cd.anchor.y + 0.5f) * s, 0f);
        if (c.body != null)
        {
            c.body.sortingLayerName = Layers.Objects;
            // same scale as workers / deposit rows: lower on screen = in front
            c.body.sortingOrder = -Mathf.RoundToInt((cd.anchor.y + 0.1f) * 100f);
        }
        c.Refresh(Pick(closedSprites, cd.variant), Pick(openSprites, cd.variant));
        crates.Add(c);
    }

    private static Sprite Pick(Sprite[] arr, int i) { return arr != null && arr.Length > 0 ? arr[Mathf.Clamp(i, 0, arr.Length - 1)] : null; }

    /// <summary>Marks the crates' cells as blocked (called once the grid exists, and again after a spawn in play mode).</summary>
    public void RegisterAll()
    {
        crates.Clear();
        foreach (var c in GetComponentsInChildren<Crate>(true)) crates.Add(c);
        if (grid == null) return;
        foreach (var c in crates)
            foreach (var p in c.Cells)
            {
                var cell = grid.GetCell(p);
                if (cell != null) cell.crate = c;
            }
    }

    /// <summary>Crate data for the save file.</summary>
    public void Capture(MapData d)
    {
        d.crates.Clear();
        foreach (var c in crates)
            if (c != null) d.crates.Add(new CrateData { anchor = c.data.anchor, variant = c.data.variant, tag = c.data.tag, amount = c.data.amount, pearls = c.data.pearls, opened = c.data.opened });
    }

    // ------------------------------------------------------------ opening
    /// <summary>Pays the crate's price with one product of the right tag. Returns false (with a toast) when it cannot.</summary>
    public bool TryOpen(Crate c, ResourceKind payWith)
    {
        var hud = HUD.Instance;
        var rm = ResourceManager.Instance;
        if (c == null || c.Opened || rm == null) return false;
        if (!Products.Has(payWith, c.Tag)) { if (hud != null) hud.Toast(ResourceInfo.Name(payWith) + " cannot open this crate", ToastKind.Warning); return false; }
        if (rm.Get(payWith) < c.data.amount) { if (hud != null) hud.Toast("Not enough " + ResourceInfo.Name(payWith) + " (" + c.data.amount + " needed)", ToastKind.Warning); return false; }

        rm.Add(payWith, -c.data.amount);
        c.data.opened = true;
        c.Refresh(Pick(closedSprites, c.data.variant), Pick(openSprites, c.data.variant));

        int total = PearlBank.Add(c.data.pearls);
        if (hud != null) hud.Toast("Crate opened!  +" + c.data.pearls + " pearls" + (total < 0 ? "  (no world save yet - not kept)" : "  (total " + total + ")"), ToastKind.Good, 4f);
        if (FxManager.Instance != null)
        {
            FxManager.Instance.Burst(c.transform.position + Vector3.up * 0.3f, new Color(0.92f, 0.96f, 1f), 26, 4.5f, 6f, 0.9f, 0.14f);
            FxManager.Instance.Burst(c.transform.position + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.4f), 14, 3f, 5f, 0.8f, 0.12f);
        }
        GameAudio.Play(Sfx.BellHigh, 0.6f);
        return true;
    }
}
