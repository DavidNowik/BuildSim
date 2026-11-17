using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// A placeable building. All gameplay data (footprint, cost, max workers, role) lives in serialized fields on the prefab.
/// Placement is cell based (footprint is checked against GridManager data, not physics).
/// The existing prefab collider's offset is used as the "feet" anchor so the art does not need to be re-authored.
/// </summary>
public class Building : MonoBehaviour
{
    [Header("Building Settings")]
    public string displayName = "Building";
    public BuildingRole role = BuildingRole.Generic;
    [Tooltip("Which build menu layer lists this building")]
    public BuildCategory category = BuildCategory.Materials;
    public Vector2Int footprint = new Vector2Int(2, 1);
    public int costWood;
    public int costStone;
    [Tooltip("Worker slots of this workplace")]
    [FormerlySerializedAs("beds")] public int maxWorkers;

    /// <summary>Set by the player: how many workers this workplace should get, and how soon (5 = first served, 1 = last).</summary>
    [HideInInspector] public int wanted;
    [HideInInspector] public int priority = 3;
    public bool IsWorkplace => ResourceInfo.KindFor(role) != ResourceKind.None;

    public void SetWanted(int n) { wanted = Mathf.Clamp(n, 0, maxWorkers); if (BuildingManager.instance != null) BuildingManager.instance.Rebalance(); }
    public void SetPriority(int p) { priority = Mathf.Clamp(p, 1, 5); if (BuildingManager.instance != null) BuildingManager.instance.Rebalance(); }

    public Cost Cost => new Cost(costWood, costStone);
    public Vector2Int Anchor { get; private set; }
    public bool IsPlaced { get; private set; }
    public bool IsGhost { get; private set; }

    public readonly List<Cell> occupiedCells = new List<Cell>();
    public readonly List<Worker> residents = new List<Worker>();

    public int FreeSlots => Mathf.Max(0, maxWorkers - residents.Count);
    public int Sleepers { get; private set; }

    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private int[] baseOrders;
    private Collider2D col;
    private Rigidbody2D rb;
    private Animator anim;
    private bool hasRestingParam;
    [Header("Prefab parts (set up in the prefab)")]
    public SpriteRenderer shadow;
    public CampLight campLight;
    private Vector3 baseScale;

    // ------------------------------------------------------------ lifecycle
    protected virtual void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        baseOrders = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            baseColors[i] = renderers[i].color;
            baseOrders[i] = renderers[i].sortingOrder;
        }
        CacheSortingLayers();
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        baseScale = transform.localScale;
        if (campLight == null) campLight = GetComponent<CampLight>();

        if (anim != null)
        {
            foreach (var p in anim.parameters)
                if (p.name == "resting") hasRestingParam = true;
        }
    }

    private void Update()
    {
        if (!IsPlaced || anim == null || !hasRestingParam) return;
        anim.SetBool("resting", Sleepers > 0);
    }

    private void OnDestroy()
    {
        if (IsPlaced && BuildingManager.instance != null) BuildingManager.instance.UnregisterBuilding(this);
    }

    // ------------------------------------------------------------ geometry
    /// <summary>Offset from the transform to the centre of the building's footprint on the ground.</summary>
    public Vector3 FootOffset
    {
        get
        {
            var box = col as BoxCollider2D;
            if (box == null) return Vector3.zero;
            Vector3 s = baseScale;
            return new Vector3(box.offset.x * s.x, box.offset.y * s.y, 0f);
        }
    }

    public Vector3 FootCenterWorld
    {
        get
        {
            var grid = GridManager.instance;
            Vector3 c = grid.CellToWorld(Anchor) + new Vector3((footprint.x - 1) * 0.5f, (footprint.y - 1) * 0.5f, 0f) * grid.tileSpacing;
            return c;
        }
    }

    public Vector3 RootPositionForAnchor(Vector2Int anchor)
    {
        var grid = GridManager.instance;
        Vector3 center = grid.CellToWorld(anchor) + new Vector3((footprint.x - 1) * 0.5f, (footprint.y - 1) * 0.5f, 0f) * grid.tileSpacing;
        return center - FootOffset;
    }

    /// <summary>Anchor (lower-left footprint cell) that centres the footprint on a hovered cell.</summary>
    public Vector2Int AnchorForCell(Vector2Int hovered)
    {
        return new Vector2Int(hovered.x - (footprint.x - 1) / 2, hovered.y - (footprint.y - 1) / 2);
    }

    // ------------------------------------------------------------ ghost / tint
    public void SetGhost(bool ghost)
    {
        IsGhost = ghost;
        if (col != null) col.enabled = !ghost;
        if (rb != null) rb.simulated = !ghost;
        if (anim != null) anim.enabled = !ghost;
        if (shadow != null) shadow.enabled = !ghost;
        if (campLight != null) campLight.SetActive(!ghost);
        if (!ghost) SetTint(Color.white);
        SortForGhost(ghost);
    }

    private void SortForGhost(bool ghost)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingLayerName = ghost ? Layers.UI : renderers[i].sortingLayerName;
            if (ghost) renderers[i].sortingOrder = -10 + i;
        }
    }

    public void SetTint(Color tint)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var c = baseColors[i];
            renderers[i].color = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a * tint.a);
        }
    }

    // ------------------------------------------------------------ placement
    /// <summary>Converts a ghost / fresh instance into a real building at the given anchor.</summary>
    public void Place(Vector2Int anchor, bool animate = true)
    {
        var grid = GridManager.instance;
        Anchor = anchor;
        transform.localScale = baseScale;
        Vector3 root = RootPositionForAnchor(anchor);
        // keep buildings on the same 1/16 unit lattice as workers and the camera
        transform.position = new Vector3(Mathf.Round(root.x * 16f) / 16f, Mathf.Round(root.y * 16f) / 16f, root.z);
        occupiedCells.Clear();
        grid.Occupy(this, anchor, footprint, occupiedCells);

        // restore real sorting layers (SortForGhost overwrote the first layer)
        RestoreSortingLayers();
        SetGhost(false);
        IsPlaced = true;
        name = displayName;

        ApplyYSorting();

        BuildingManager.instance.RegisterBuilding(this);

        if (animate)
        {
            StartCoroutine(PlacePop());
            Vector3 foot = FootCenterWorld;
            if (FxManager.Instance != null) FxManager.Instance.Dust(foot + Vector3.down * 0.4f, footprint.x, 10);
            GameAudio.Play(Sfx.Place);
            if (CameraMotor.Instance != null) CameraMotor.Instance.Shake(0.06f, 0.12f);
        }
    }

    private Dictionary<SpriteRenderer, string> originalLayers;

    private void RestoreSortingLayers()
    {
        // the prefab's sorting layers were saved before the ghost overwrote them
        if (originalLayers == null) return;
        foreach (var kv in originalLayers) if (kv.Key != null) kv.Key.sortingLayerName = kv.Value;
    }

    public void CacheSortingLayers()
    {
        if (originalLayers != null) return;
        originalLayers = new Dictionary<SpriteRenderer, string>();
        foreach (var r in renderers) originalLayers[r] = r.sortingLayerName;
    }

    private void ApplyYSorting()
    {
        int yOrder = -Mathf.RoundToInt(FootCenterWorld.y * 10f);
        for (int i = 0; i < renderers.Length; i++) renderers[i].sortingOrder = baseOrders[i] + yOrder;
    }


    private IEnumerator PlacePop()
    {
        const float dur = 0.28f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            // squash -> stretch -> settle
            float sx = 1f + 0.12f * Mathf.Sin(k * Mathf.PI) * (1f - k);
            float sy = 1f - 0.16f * (1f - k) * (1f - k) + 0.06f * Mathf.Sin(k * Mathf.PI);
            transform.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z);
            yield return null;
        }
        transform.localScale = baseScale;
    }

    /// <summary>Tiny squash when something is delivered / used.</summary>
    public void Bump()
    {
        if (!IsPlaced || !gameObject.activeInHierarchy) return;
        StartCoroutine(BumpRoutine());
    }

    private IEnumerator BumpRoutine()
    {
        const float dur = 0.18f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float s = Mathf.Sin(k * Mathf.PI);
            transform.localScale = new Vector3(baseScale.x * (1f + 0.04f * s), baseScale.y * (1f - 0.06f * s), baseScale.z);
            yield return null;
        }
        transform.localScale = baseScale;
    }

    // ------------------------------------------------------------ residents
    public bool AddResident(Worker w)
    {
        if (residents.Contains(w)) return true;
        if (FreeSlots <= 0) return false;
        residents.Add(w);
        return true;
    }

    public void RemoveResident(Worker w)
    {
        residents.Remove(w);
    }

    public void SleeperEntered() { Sleepers++; }
    public void SleeperLeft() { Sleepers = Mathf.Max(0, Sleepers - 1); }

    // ------------------------------------------------------------ demolish
    public void Demolish()
    {
        if (!IsPlaced) return;
        IsPlaced = false;
        foreach (var w in residents.ToArray()) if (w != null) w.OnHomeLost();
        residents.Clear();
        Sleepers = 0;

        GridManager.instance.Vacate(this);
        BuildingManager.instance.UnregisterBuilding(this);
        if (ResourceManager.Instance != null) ResourceManager.Instance.Refund(Cost, 0.5f);
        if (FxManager.Instance != null) FxManager.Instance.Dust(FootCenterWorld, footprint.x, 14);
        GameAudio.Play(Sfx.Place, 0.7f, 0.7f);
        Destroy(gameObject);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!IsPlaced || GridManager.instance == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(FootCenterWorld, new Vector3(footprint.x, footprint.y, 0));
    }
}
