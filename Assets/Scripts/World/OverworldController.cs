using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs the overworld: shows the net of lands for the current tide cycle, handles discovery (modifier choice),
/// opens lands as levels, advances the moon, and performs the flood that washes the world clean.
/// Scene layout: lands/paths are spawned from the Node/Edge prefabs under the two root objects; everything else
/// (sea, sky, camera, HUD, plateau, settlement sites) is a normal scene object referenced here.
/// </summary>
public class OverworldController : MonoBehaviour
{
    public static OverworldController Instance;

    [Header("Prefabs")]
    public OverworldNode nodePrefab;
    public OverworldEdge edgePrefab;

    [Header("Scene objects")]
    public Transform nodesRoot;
    public Transform edgesRoot;
    public TideSea sea;
    public StormSky sky;
    public OverworldCamera cameraRig;
    public OverworldHUD hud;
    [Tooltip("Locked settlement sites above the start (never reached by the tide)")]
    public Collider2D[] settlementSites;

    [Header("Icons (same order as Modifiers.All)")]
    public Sprite[] modifierIcons;

    [Header("Cycle")]
    public int moonMax = 10;
    public string levelSceneName = "Level";
    [Tooltip("World y where the flood stops: above the start settlement (the high-water mark); only the locked settlement sites stay dry")]
    public float highWaterY = 0.62f;

    [Header("Flood timing (seconds)")]
    public float warnSeconds = 1.8f;
    public float waveSeconds = 5.4f;
    public float ebbSeconds = 4.8f;

    private WorldSave save;
    private OverworldLayout layout;
    private OverworldNode[] nodes;
    private OverworldEdge[] edges;
    private int selected = -1;
    private int hoverId = -1;
    private bool flooding;
    private float riseNow, riseTarget;
    private MapModifier[] offered;

    public bool IsFlooding { get { return flooding; } }

    // ------------------------------------------------------------ lifecycle
    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        save = null;
        if (WorldStore.Exists())
        {
            try { save = WorldStore.Load(); }
            catch (System.Exception e) { Debug.LogWarning("Overworld: could not read world save: " + e.Message); }
        }
        if (save == null || save.nodes == null || save.nodes.Count == 0) save = NewWorld(1, 0, 0);

        BuildWorld(false);
        FitCamera();
        RefreshHud();
        if (sky != null) sky.SnapDread(0f);
        if (Application.isPlaying) Debug.Log("[Dev] Overworld: cycle " + save.cycle + ", " + layout.Count + " lands, seed " + save.seed);
    }

    private WorldSave NewWorld(int cycle, int pearls, int bestReach)
    {
        var w = new WorldSave { seed = Random.Range(1, int.MaxValue), cycle = cycle, pearls = pearls, bestReach = bestReach };
        var lay = OverworldGen.Generate(w.seed);
        for (int i = 0; i < lay.Count; i++) w.nodes.Add(new WorldNodeSave());

        // the start land is discovered from the beginning (a gentle one) and opens its neighbours
        var start = w.nodes[0];
        start.status = (int)NodeStatus.Done;
        start.modId = "calm";
        start.mapId = "w" + cycle + "_0";
        start.mapSeed = Random.Range(1, int.MaxValue);
        foreach (int nb in lay.adj[0]) if (w.nodes[nb].status == 0) w.nodes[nb].status = (int)NodeStatus.Open;
        return w;
    }

    // ------------------------------------------------------------ building the net
    private void BuildWorld(bool animate)
    {
        for (int i = nodesRoot.childCount - 1; i >= 0; i--) Destroy(nodesRoot.GetChild(i).gameObject);
        for (int i = edgesRoot.childCount - 1; i >= 0; i--) Destroy(edgesRoot.GetChild(i).gameObject);

        layout = OverworldGen.Generate(save.seed);
        edges = new OverworldEdge[layout.edges.Length];
        for (int i = 0; i < edges.Length; i++)
        {
            var e = Instantiate(edgePrefab, edgesRoot);
            e.name = "Path " + layout.edges[i].x + "-" + layout.edges[i].y;
            e.Setup(Pos(layout.edges[i].x), Pos(layout.edges[i].y));
            edges[i] = e;
        }

        nodes = new OverworldNode[layout.Count];
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = Instantiate(nodePrefab, nodesRoot);
            n.name = "Land " + i + " (" + layout.names[i] + ")";
            n.transform.position = Pos(i);
            n.Setup(i, layout.tier[i], layout.names[i]);
            nodes[i] = n;
        }
        ApplyAll(null, animate);
    }

    private Vector3 Pos(int i) { return new Vector3(layout.pos[i].x, layout.pos[i].y, 0f); }

    private static Color HueColor(int tier, float s) { return Color.HSVToRGB(((330f + tier * 38f) % 360f) / 360f, s, 1f); }

    private void ApplyAll(List<int> fresh, bool animate)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            var ws = save.nodes[i];
            var status = (NodeStatus)ws.status;
            MapModifier mod = status == NodeStatus.Done ? Modifiers.Get(ws.modId) : null;
            Sprite icon = mod != null && modifierIcons != null ? modifierIcons[Mathf.Min(modifierIcons.Length - 1, Modifiers.IndexOf(ws.modId))] : null;
            bool pop = animate || (fresh != null && fresh.Contains(i));
            nodes[i].SetState(status, mod, icon, pop);
        }
        for (int i = 0; i < edges.Length; i++)
        {
            var a = (NodeStatus)save.nodes[layout.edges[i].x].status;
            var b = (NodeStatus)save.nodes[layout.edges[i].y].status;
            EdgeState st = EdgeState.Hidden;
            if (a != NodeStatus.Hidden && b != NodeStatus.Hidden && !(a == NodeStatus.Open && b == NodeStatus.Open))
                st = (a == NodeStatus.Done && b == NodeStatus.Done) ? EdgeState.Done : EdgeState.Open;
            int tier = Mathf.Max(layout.tier[layout.edges[i].x], layout.tier[layout.edges[i].y]);
            edges[i].SetState(st, HueColor(tier, 0.65f));
        }
    }

    private void FitCamera()
    {
        float maxX = 0f;
        for (int i = 0; i < layout.Count; i++) maxX = Mathf.Max(maxX, Mathf.Abs(layout.pos[i].x));
        float top = 5.9f, bottom = layout.sea0 - 2.2f, halfW = maxX + 2.5f;
        var r = new Rect(-halfW, bottom, halfW * 2f, top - bottom);
        cameraRig.bounds = new Rect(-halfW, bottom + 2f, halfW * 2f, top - bottom - 4f);
        cameraRig.Fit(r);
    }

    private void RefreshHud()
    {
        int lands = 0, reach = 0;
        for (int i = 0; i < save.nodes.Count; i++)
            if (save.nodes[i].status == (int)NodeStatus.Done) { lands++; reach = Mathf.Max(reach, layout.tier[i]); }
        hud.SetStats(lands, reach, save.pearls, save.cycle);
        hud.SetMoon(save.moon, moonMax);
        if (!flooding)
        {
            float h = -layout.sea0;
            float p = Mathf.Clamp01((float)save.moon / moonMax);
            riseTarget = p * p * 0.17f * h;
            if (sky != null) sky.SetDread(p * 0.85f);
        }
    }

    // ------------------------------------------------------------ per frame
    private void Update()
    {
        if (layout == null) return;

        // the sea creeps up as the moon fills; lands it has reached turn grey
        riseNow += (riseTarget - riseNow) * (1f - Mathf.Exp(-1.5f * Time.deltaTime));
        float surface = layout.sea0 + riseNow;
        if (!flooding)
        {
            float h = -layout.sea0;
            sea.Set(surface, 0.10f + riseNow * 0.012f, 0.12f + riseNow / h);
            for (int i = 1; i < nodes.Length; i++) nodes[i].SetWet(nodes[i].transform.position.y < surface + 0.2f);
        }

        HandlePointer();
    }

    private void HandlePointer()
    {
        if (flooding || hud.ModalOpen) { SetHover(-1); return; }
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        int id = -1;
        Collider2D hit = null;
        if (!overUI)
        {
            Vector2 w = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            hit = Physics2D.OverlapPoint(w);
            if (hit != null) { var n = hit.GetComponentInParent<OverworldNode>(); if (n != null) id = n.Id; }
        }
        SetHover(id);

        if (Input.GetMouseButtonUp(0) && !overUI && !cameraRig.DragMoved)
        {
            if (id >= 0) Select(id);
            else if (hit != null && System.Array.IndexOf(settlementSites, hit) >= 0)
            {
                GameAudio.Play(Sfx.Deny, 0.6f);
                hud.Toast("Settlement sites unlock later", false, 2.2f);
            }
            else Deselect();
        }
    }

    private void SetHover(int id)
    {
        if (id == hoverId) return;
        if (hoverId >= 0 && hoverId < nodes.Length) nodes[hoverId].SetHover(false);
        hoverId = id;
        if (id >= 0) { nodes[id].SetHover(true); GameAudio.Play(Sfx.Hover, 0.5f); }
    }

    // ------------------------------------------------------------ selection / discovery
    private void Select(int id)
    {
        if (selected >= 0 && selected < nodes.Length) nodes[selected].SetSelected(false);
        selected = id;
        nodes[id].SetSelected(true);
        GameAudio.Play(Sfx.Click, 0.6f);

        var ws = save.nodes[id];
        var size = MapSettings.SizeForTier(layout.tier[id]);
        if (ws.status == (int)NodeStatus.Open)
        {
            hud.ShowInfo("Uncharted land", "Not yet discovered",
                "Tier " + layout.tier[id] + "\nMap size " + size.x + " x " + size.y + "\nYou choose how it forms.\nCosts moons.",
                "Discover", () => BeginDiscover(id));
        }
        else
        {
            var mod = Modifiers.Get(ws.modId);
            hud.ShowInfo(layout.names[id], "Discovered",
                "Tier " + layout.tier[id] + "\nMap size " + size.x + " x " + size.y + "\n" + mod.name + "\nPaths " + layout.adj[id].Count,
                "Enter map", () => EnterMap(id));
        }
    }

    private void Deselect()
    {
        if (selected >= 0 && selected < nodes.Length) nodes[selected].SetSelected(false);
        selected = -1;
        hud.HideInfo();
    }

    private void BeginDiscover(int id)
    {
        // three different modifiers to choose from
        var pool = new List<MapModifier>(Modifiers.All);
        offered = new MapModifier[3];
        for (int i = 0; i < 3; i++) { int k = Random.Range(0, pool.Count); offered[i] = pool[k]; pool.RemoveAt(k); }
        var icons = new Sprite[3];
        for (int i = 0; i < 3; i++) icons[i] = modifierIcons[Mathf.Min(modifierIcons.Length - 1, Modifiers.IndexOf(offered[i].id))];

        GameAudio.Play(Sfx.Recruit, 0.7f);
        hud.ShowModal("Tier " + layout.tier[id] + " - this land does not exist yet. Choose how it forms.", offered, icons, i => Discover(id, offered[i]));
    }

    private void Discover(int id, MapModifier mod)
    {
        var ws = save.nodes[id];
        ws.status = (int)NodeStatus.Done;
        ws.modId = mod.id;
        ws.mapId = "w" + save.cycle + "_" + id;
        ws.mapSeed = Random.Range(1, int.MaxValue);                    // created now, not before

        var fresh = new List<int> { id };
        foreach (int nb in layout.adj[id])
            if (save.nodes[nb].status == (int)NodeStatus.Hidden) { save.nodes[nb].status = (int)NodeStatus.Open; fresh.Add(nb); }
        save.moon = Mathf.Max(0, save.moon + mod.moonCost);

        ApplyAll(fresh, false);
        RefreshHud();
        WorldStore.Save(save);

        Vector3 p = Pos(id);
        if (FxManager.Instance != null) FxManager.Instance.Burst(p, mod.color, 40, 4f, 6f, 0.9f, 0.16f);
        if (cameraRig != null) cameraRig.Shake(0.08f, 0.2f);
        GameAudio.Play(Sfx.Chime);
        hud.Toast(layout.names[id] + (fresh.Count > 1 ? "  -  " + (fresh.Count - 1) + " new land" + (fresh.Count > 2 ? "s" : "") + " found" : ""), false, 2.6f);
        Select(id);

        if (save.moon >= moonMax) StartCoroutine(FloodAfter(1.4f));
    }

    private void EnterMap(int id)
    {
        var ws = save.nodes[id];
        if (!Application.CanStreamedLevelBeLoaded(levelSceneName))
        {
            hud.Toast("Scene '" + levelSceneName + "' is not in the build settings", false, 3f);
            return;
        }
        LevelContext.Set(ws.mapId, ws.mapSeed, layout.tier[id], ws.modId);
        WorldStore.Save(save);
        SceneManager.LoadScene(levelSceneName);
    }

    // ------------------------------------------------------------ THE TIDE
    private IEnumerator FloodAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        yield return Flood();
    }

    public void DebugFlood() { if (!flooding) StartCoroutine(Flood()); }
    /// <summary>Test helper: discovers random uncharted lands with random modifiers (no UI).</summary>
    public void DebugDiscover(int count)
    {
        for (int c = 0; c < count; c++)
        {
            var open = new List<int>();
            for (int i = 0; i < save.nodes.Count; i++) if (save.nodes[i].status == (int)NodeStatus.Open) open.Add(i);
            if (open.Count == 0) return;
            Discover(open[Random.Range(0, open.Count)], Modifiers.All[Random.Range(0, Modifiers.All.Length)]);
        }
    }

    /// <summary>Test helper: throws the current world away and starts a fresh cycle 1 (keeps nothing).</summary>
    public void DebugNewWorld()
    {
        StopAllCoroutines();
        flooding = false;
        save = NewWorld(1, 0, 0);
        riseNow = riseTarget = 0f;
        BuildWorld(false);
        FitCamera();
        RefreshHud();
        sky.SnapDread(0f);
        WorldStore.Save(save);
    }

    /// <summary>Test helper: opens a discovered land as a level (same path as the Enter map button).</summary>
    public void DebugEnterMap(int id) { EnterMap(id); }

    public void DebugAddMoon(int n) { save.moon = Mathf.Clamp(save.moon + n, 0, moonMax); RefreshHud(); WorldStore.Save(save); }

    private IEnumerator Flood()
    {
        if (flooding) yield break;
        flooding = true;
        Deselect(); hud.HideModal();

        int reach = 0, lands = 0;
        for (int i = 0; i < save.nodes.Count; i++)
            if (save.nodes[i].status == (int)NodeStatus.Done) { lands++; reach = Mathf.Max(reach, layout.tier[i]); }
        int gain = Mathf.Max(1, Mathf.RoundToInt((reach * 3 + lands) / 2f));

        hud.Toast("The water is rising...", true, 3.4f);
        GameAudio.Play(Sfx.TideRumble);
        sky.SetDread(1f);
        float h = -layout.sea0;
        riseTarget = h * 0.2f;

        // warning: clouds gather, the sea creeps up, the ground trembles
        for (float t = 0f; t < warnSeconds; t += Time.deltaTime)
        {
            float s = layout.sea0 + riseNow;
            sea.Set(s, 0.14f + riseNow * 0.02f, 0.3f + riseNow / h);
            for (int i = 1; i < nodes.Length; i++) nodes[i].SetWet(nodes[i].transform.position.y < s + 0.2f);
            riseNow += (riseTarget - riseNow) * (1f - Mathf.Exp(-1.5f * Time.deltaTime));
            yield return null;
        }

        // the wall of water climbs to the start's feet, swallowing everything below it
        float startSurface = layout.sea0 + riseNow;
        if (cameraRig != null) cameraRig.Shake(0.05f, waveSeconds);
        for (float t = 0f; t < waveSeconds; t += Time.deltaTime)
        {
            float p = t / waveSeconds;
            float e = p < 0.5f ? 4f * p * p * p : 1f - Mathf.Pow(-2f * p + 2f, 3f) / 2f;      // slow start, then crushing
            float surface = Mathf.Lerp(startSurface, highWaterY, e);
            sea.Set(surface, 0.55f + e * 0.75f, 1f);
            SprayAlongCrest(surface);
            SwallowBelow();
            FadeEdges(1f - e * 1.3f);
            if (Random.value < 0.012f) { sky.Flash(); GameAudio.Play(Sfx.Thunder); }
            yield return null;
        }

        // under the water: the old world is gone, the new cycle begins
        foreach (var ws in save.nodes) if (ws.status == (int)NodeStatus.Done && !string.IsNullOrEmpty(ws.mapId)) MapStore.Delete(ws.mapId);
        int newBest = Mathf.Max(save.bestReach, reach);
        save = NewWorld(save.cycle + 1, save.pearls + gain, newBest);
        riseNow = riseTarget = 0f;
        BuildWorld(false);
        FitCamera();
        RefreshHud();
        FadeEdges(1f);
        WorldStore.Save(save);
        GameAudio.Play(Sfx.BellLow);
        hud.ShowBanner("THE SEA TAKES BACK WHAT IT LENT",
            "Furthest reach: tier " + reach + "   -   " + lands + " lands lost to the tide",
            "Salvaged: " + gain + " pearls", 5.2f);
        yield return new WaitForSeconds(0.7f);
        GameAudio.Play(Sfx.BellHigh);

        // the ebb: the water drains back down, revealing fresh uncharted land
        sky.SetDread(0f);
        float from = highWaterY, to = layout.sea0;
        for (float t = 0f; t < ebbSeconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01((t / ebbSeconds - 0.12f) / 0.88f);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            sea.Set(Mathf.Lerp(from, to, e), 0.5f - 0.4f * e, 0.55f * (1f - e) + 0.12f);
            yield return null;
        }

        flooding = false;
        RefreshHud();
    }

    private void SprayAlongCrest(float surface)
    {
        if (FxManager.Instance == null) return;
        var cam = Camera.main;
        float halfW = cam.orthographicSize * cam.aspect;
        for (int i = 0; i < 3; i++)
        {
            float x = cam.transform.position.x + Random.Range(-halfW, halfW);
            FxManager.Instance.Chips(new Vector3(x, sea.CrestY(x), 0f), new Color(0.87f, 0.93f, 0.96f), 1);
        }
    }

    private void SwallowBelow()
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n.Swallowed || !n.gameObject.activeSelf) continue;
            if (n.transform.position.y < sea.CrestY(n.transform.position.x) + 0.25f)
            {
                n.Swallow();
                if (FxManager.Instance != null) FxManager.Instance.Burst(n.transform.position, new Color(0.55f, 0.6f, 0.65f), 16, 3f, 7f, 0.9f, 0.14f);
                GameAudio.Play(Sfx.Thump, 0.8f);
            }
        }
    }

    private void FadeEdges(float f)
    {
        f = Mathf.Clamp01(f);
        for (int i = 0; i < edges.Length; i++) edges[i].SetFade(f);
    }
}
