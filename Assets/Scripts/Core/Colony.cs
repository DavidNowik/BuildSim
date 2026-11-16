using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Game director of a level: owns the building catalog and the worker prefab (assigned in the scene),
/// places the starter camp on a new map, handles recruiting and the goal chain, and restores a saved colony.
/// </summary>
public class Colony : MonoBehaviour
{
    public static Colony Instance;

    [Header("Prefabs (assign in the scene)")]
    [Tooltip("Buildings the player can construct, in build-bar order")]
    public List<Building> catalog = new List<Building>();
    public Worker workerPrefab;

    [Header("New map")]
    public bool starterColony = true;

    public IReadOnlyList<Building> Catalog { get { return catalog; } }
    public Worker WorkerPrefab { get { return workerPrefab; } }

    // ---- goals
    private class Goal
    {
        public string title;
        public Func<int> progress;
        public int target;
        public Cost reward;
    }
    private readonly List<Goal> goals = new List<Goal>();
    private int goalIndex;

    public bool AllGoalsDone { get { return goalIndex >= goals.Count; } }
    public int GoalNumber { get { return Mathf.Min(goalIndex + 1, goals.Count); } }
    public int GoalCount { get { return goals.Count; } }
    public string GoalTitle { get { return AllGoalsDone ? "All goals complete - keep growing!" : goals[goalIndex].title; } }
    public int GoalProgress { get { return AllGoalsDone ? 0 : Mathf.Min(goals[goalIndex].progress(), goals[goalIndex].target); } }
    public int GoalTarget { get { return AllGoalsDone ? 0 : goals[goalIndex].target; } }
    public Cost GoalReward { get { return AllGoalsDone ? new Cost() : goals[goalIndex].reward; } }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (workerPrefab == null) Debug.LogError("Colony: worker prefab is not assigned.");
        BuildGoals();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        var pending = LevelSession.Instance != null ? LevelSession.Instance.Pending : null;
        if (pending != null) ReadSave(pending);
        else if (starterColony) PlaceStarter();
    }

    // ------------------------------------------------------------ goals
    private void BuildGoals()
    {
        goals.Add(new Goal { title = "Deliver 25 stone", target = 25, reward = new Cost(10, 0), progress = () => ResourceManager.Instance.TotalDelivered(ResourceKind.Stone) });
        goals.Add(new Goal { title = "Build a Woodcutter Camp and deliver 40 wood", target = 40, reward = new Cost(0, 15), progress = () => ResourceManager.Instance.TotalDelivered(ResourceKind.Wood) });
        goals.Add(new Goal { title = "Grow to 4 workers", target = 4, reward = new Cost(20, 0), progress = () => Worker.All.Count });
        goals.Add(new Goal { title = "Deliver 150 stone", target = 150, reward = new Cost(20, 20), progress = () => ResourceManager.Instance.TotalDelivered(ResourceKind.Stone) });
        goals.Add(new Goal { title = "Deliver 150 wood", target = 150, reward = new Cost(20, 20), progress = () => ResourceManager.Instance.TotalDelivered(ResourceKind.Wood) });
        goals.Add(new Goal { title = "Grow to 8 workers", target = 8, reward = new Cost(40, 40), progress = () => Worker.All.Count });
    }

    private void Update()
    {
        if (AllGoalsDone || ResourceManager.Instance == null) return;
        var g = goals[goalIndex];
        if (g.progress() >= g.target)
        {
            goalIndex++;
            ResourceManager.Instance.Add(ResourceKind.Wood, g.reward.wood);
            ResourceManager.Instance.Add(ResourceKind.Stone, g.reward.stone);
            GameAudio.Play(Sfx.Chime);
            if (HUD.Instance != null)
                HUD.Instance.Toast("Goal complete: " + g.title + (g.reward.IsFree ? "" : "   Reward: +" + g.reward), ToastKind.Good, 5f);
            if (CameraMotor.Instance != null) CameraMotor.Instance.Shake(0.05f, 0.15f);
        }
    }

    // ------------------------------------------------------------ catalog helpers
    public Building PrefabFor(BuildingRole role)
    {
        for (int i = 0; i < catalog.Count; i++) if (catalog[i] != null && catalog[i].role == role) return catalog[i];
        return null;
    }

    public Building PrefabByName(string displayName)
    {
        for (int i = 0; i < catalog.Count; i++) if (catalog[i] != null && catalog[i].displayName == displayName) return catalog[i];
        return null;
    }

    public int Population { get { return Worker.All.Count; } }
    public int PopulationCap { get { return BuildingManager.instance != null ? BuildingManager.instance.TotalCapacity() : 0; } }

    // ------------------------------------------------------------ recruiting
    public static Cost RecruitCost(ResourceKind kind)
    {
        return kind == ResourceKind.Wood ? new Cost(0, 10) : new Cost(10, 0);
    }

    public bool CanRecruit(ResourceKind kind, out string reason)
    {
        reason = "";
        var bm = BuildingManager.instance;
        var camp = bm.GetNearestWithRoom(ResourceInfo.CampFor(kind), Vector3.zero);
        if (camp == null)
        {
            var prefab = PrefabFor(ResourceInfo.CampFor(kind));
            string campName = prefab != null ? prefab.displayName : "camp";
            reason = bm.HasRole(ResourceInfo.CampFor(kind)) ? "No room - build another " + campName : "Build a " + campName + " first";
            return false;
        }
        if (!ResourceManager.Instance.CanAfford(RecruitCost(kind)))
        {
            reason = "Not enough resources (" + RecruitCost(kind) + ")";
            return false;
        }
        return true;
    }

    public bool Recruit(ResourceKind kind)
    {
        string reason;
        if (!CanRecruit(kind, out reason))
        {
            GameAudio.Play(Sfx.Deny);
            if (HUD.Instance != null) HUD.Instance.Toast(reason, ToastKind.Warning);
            return false;
        }
        var camp = BuildingManager.instance.GetNearestWithRoom(ResourceInfo.CampFor(kind), Vector3.zero);
        if (workerPrefab == null || camp == null) return false;
        ResourceManager.Instance.Spend(RecruitCost(kind));
        var w = SpawnWorker(kind, camp, true);
        GameAudio.Play(Sfx.Recruit);
        if (HUD.Instance != null) HUD.Instance.Toast(w.workerName + " the " + w.JobTitle + " joined the colony!", ToastKind.Good);
        return true;
    }

    private Worker SpawnWorker(ResourceKind kind, Building camp, bool randomStats)
    {
        Vector2Int door;
        Vector3 at = camp.FootCenterWorld;
        if (BuildingManager.instance.TryGetDoorCell(camp, camp.FootCenterWorld, out door)) at = GridManager.instance.CellToWorld(door);

        var w = Instantiate(workerPrefab, at, Quaternion.identity);
        if (randomStats) w.RandomizeStats();
        w.Configure(kind, camp);
        if (FxManager.Instance != null) FxManager.Instance.Burst(at + Vector3.up * 0.2f, new Color(1f, 0.95f, 0.6f), 10, 2f, 3f, 0.5f, 0.1f);
        return w;
    }

    // ------------------------------------------------------------ starter colony (new map)
    private void PlaceStarter()
    {
        var grid = GridManager.instance;
        var camp = PrefabFor(BuildingRole.StoneCamp);
        var store = PrefabFor(BuildingRole.Storage);
        if (grid == null || camp == null || store == null) return;

        // the generator guarantees a cleared start area around grid.startCell
        Vector2Int pivot = grid.startCell;

        Vector2Int campAnchor;
        if (!FindSpot(camp.footprint, pivot + new Vector2Int(-2, 0), out campAnchor)) return;
        var campInst = Instantiate(camp);
        campInst.Place(campAnchor, false);

        Vector2Int storeAnchor;
        if (!FindSpot(store.footprint, campAnchor + new Vector2Int(4, 0), out storeAnchor)) return;
        var storeInst = Instantiate(store);
        storeInst.Place(storeAnchor, false);

        SpawnWorker(ResourceKind.Stone, campInst, false);

        if (HUD.Instance != null) HUD.Instance.Toast("Welcome! Your miner works on its own. Build more with the bar below.", ToastKind.Info, 6f);
    }

    private bool FindSpot(Vector2Int size, Vector2Int around, out Vector2Int result)
    {
        var grid = GridManager.instance;
        var bm = BuildingManager.instance;
        result = around;
        for (int r = 0; r < 24; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    Vector2Int a = new Vector2Int(around.x + dx, around.y + dy);
                    if (!grid.CanPlace(a, size)) continue;
                    if (!bm.HasDoorCell(a, size)) continue;
                    if (bm.WouldBlockDoors(a, size)) continue;
                    result = a;
                    return true;
                }
            }
        }
        return false;
    }

    // ------------------------------------------------------------ save / load
    public void WriteSave(LevelSave s)
    {
        s.goalIndex = goalIndex;
        s.buildings.Clear();
        s.workers.Clear();

        var order = new List<Building>();
        foreach (var b in BuildingManager.instance.All)
        {
            if (!b.IsPlaced) continue;
            order.Add(b);
            s.buildings.Add(new BuildingSave { prefab = b.displayName, anchor = b.Anchor });
        }
        foreach (var w in Worker.All) s.workers.Add(w.WriteSave(order));
    }

    private void ReadSave(LevelSave s)
    {
        goalIndex = Mathf.Clamp(s.goalIndex, 0, goals.Count);
        if (ResourceManager.Instance != null) ResourceManager.Instance.ReadSave(s);
        if (DayCycle.Instance != null) DayCycle.Instance.ReadSave(s);

        var placed = new List<Building>();
        foreach (var bs in s.buildings)
        {
            var prefab = PrefabByName(bs.prefab);
            if (prefab == null) { Debug.LogWarning("Colony: unknown building in save: " + bs.prefab); placed.Add(null); continue; }
            var inst = Instantiate(prefab);
            inst.Place(bs.anchor, false);
            placed.Add(inst);
        }

        foreach (var ws in s.workers)
        {
            Building home = ws.homeIndex >= 0 && ws.homeIndex < placed.Count ? placed[ws.homeIndex] : null;
            var w = Instantiate(workerPrefab, new Vector3(ws.position.x, ws.position.y, 0f), Quaternion.identity);
            w.Configure((ResourceKind)ws.kind, home);
            w.ReadSave(ws);
        }
    }
}
