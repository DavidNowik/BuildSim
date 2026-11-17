using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autonomous gatherer. A small state machine replaces the old 0.5 s polling AI:
///   Idle -> GoToNode -> Gather -> (GoToStorage) -> Idle ... and Idle -> GoToBed -> Sleep when tired.
/// All timers use deltaTime (works at any game speed). Nodes are reserved through GridManager so
/// two workers never fight over one deposit.
/// </summary>
public class Worker : MonoBehaviour
{
    public static readonly List<Worker> All = new List<Worker>();

    [Header("Attributes")]
    public int strength = 10;
    public int dexterity = 10;
    public int intelligence = 10;

    [Header("Job")]
    public ResourceKind gatherKind = ResourceKind.Stone;

    // ---- public read-only state (HUD / selection)
    public string workerName { get; private set; }
    public string JobTitle { get { return Employed ? ResourceInfo.JobTitle(gatherKind) : "Unassigned"; } }
    public WorkerState State { get; private set; }
    public Building home { get; private set; }
    public int Carry { get; private set; }
    public int CarryCapacity { get { return carryCapacity; } }
    public float Fatigue { get; private set; }
    public bool IsExhausted { get { return Fatigue >= 1f && State != WorkerState.Sleep && State != WorkerState.GoToBed; } }
    /// <summary>True while the worker is inside its camp (invisible and not selectable).</summary>
    public bool IsHidden { get { return asleepInside; } }

    public string StatusText
    {
        get
        {
            switch (State)
            {
                case WorkerState.GoToNode: return "Heading to " + ResourceInfo.Name(gatherKind).ToLower();
                case WorkerState.Gather: return gatherKind == ResourceKind.Wood ? "Chopping" : (gatherKind == ResourceKind.Fish ? "Fishing" : "Mining");
                case WorkerState.GoToStorage: return "Delivering";
                case WorkerState.GoToBed: return "Going to rest";
                case WorkerState.Sleep: return "Sleeping";
                case WorkerState.Waiting: return waitReason;
                default: return "Thinking";
            }
        }
    }

    // ---- derived stats
    private float moveSpeed;
    private float gatherTime;
    private int carryCapacity;
    private float fatigueSeconds;
    private float restSeconds;
    private int strikesPerUnit;

    // ---- refs
    private Animator anim;
    private Transform visual;
    private SpriteRenderer visualSr;
    [Header("Visual parts (children of the prefab)")]
    public SpriteRenderer shadow;
    public SpriteRenderer bubble;
    public SpriteRenderer carryIcon;
    public SpriteRenderer ring;
    [Header("Sprites")]
    public Sprite bubbleSleep;
    public Sprite bubbleWait;
    public Sprite bubbleAlert;
    public Sprite stoneIcon;
    public Sprite woodIcon;
    public Sprite fishIcon;
    private Color baseTint = Color.white;

    // ---- movement
    private readonly List<Vector2Int> path = new List<Vector2Int>();
    private int pathIdx;
    private int facing = 1;

    // ---- job
    private Cell node;
    private Building storage;
    private float strikeTimer, nextStrike;
    private int strikesDone;
    private float thinkTimer;
    private string waitReason = "";
    private float visualAlpha = 1f;
    private bool asleepInside;
    private bool started;

    private const float StrikePeriod = 0.4167f; // MinerMine clip length (5 frames @ 12 fps)
    private const float FirstStrike = 0.30f;

    private static readonly string[] Names =
    {
        "Bram", "Edda", "Tomas", "Mira", "Oskar", "Lena", "Finn", "Hilda", "Rurik", "Wren", "Pip", "Greta",
        "Anselm", "Sunniva", "Jory", "Maren", "Cael", "Odile", "Hugo", "Ilsa", "Torin", "Nessa", "Rolf", "Tilda"
    };

    private static readonly Dictionary<string, float> lastToast = new Dictionary<string, float>();

    // ------------------------------------------------------------ lifecycle
    protected virtual void Awake()
    {
        workerName = Names[Random.Range(0, Names.Length)];
        UpdateStats();

        visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
        visualSr = visual.GetComponent<SpriteRenderer>();
        anim = visual.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("miner");
        if (visualSr != null) baseTint = visualSr.color;
    }

    private void OnEnable() { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    private void Start()
    {
        var p = transform.position;
        transform.position = new Vector3(p.x, p.y, 0f);
        if (GridManager.instance != null)
        {
            // snap onto the grid so path starts are exact
            Vector2Int c = GridManager.instance.WorldToCell(transform.position);
            c.x = Mathf.Clamp(c.x, 0, GridManager.instance.width - 1);
            c.y = Mathf.Clamp(c.y, 0, GridManager.instance.height - 1);
            transform.position = GridManager.instance.CellToWorld(c);
        }
        started = true;
        State = WorkerState.Idle;
    }

    private void OnDestroy()
    {
        if (asleepInside && home != null) home.SleeperLeft();
        if (node != null && GridManager.instance != null) GridManager.instance.ReleaseNode(node, this);
        if (home != null) home.RemoveResident(this);
    }

    public void UpdateStats()
    {
        carryCapacity = Mathf.Max(2, Mathf.RoundToInt(strength * 0.5f));
        moveSpeed = dexterity * 0.32f;
        gatherTime = 40f / (intelligence * 2f);
        fatigueSeconds = 60f + intelligence * 4f;
        restSeconds = Mathf.Max(3f, 12f - intelligence * 0.4f);
        strikesPerUnit = Mathf.Max(1, Mathf.RoundToInt(gatherTime / StrikePeriod));
    }

    // ------------------------------------------------------------ job (set by BuildingManager.Rebalance)
    private Building job;
    private bool assigned;
    public bool Employed { get { return assigned && job != null && job.IsPlaced; } }
    public Building Job { get { return Employed ? job : null; } }

    public void Assign(Building b)
    {
        if (assigned && job == b) return;
        job = b; assigned = true;
        gatherKind = ResourceInfo.KindFor(b.role);
        if (gatherKind == ResourceKind.Wood) baseTint = new Color(0.78f, 1.0f, 0.74f);
        else if (gatherKind == ResourceKind.Fish) baseTint = new Color(0.74f, 0.9f, 1f);
        else baseTint = Color.white;
        Carry = 0;
        CancelJob();
    }

    public void Unassign()
    {
        if (!assigned) return;
        assigned = false; job = null;
        baseTint = Color.white;
        Carry = 0;
        CancelJob();
    }

    public void RandomizeStats()
    {
        strength = 10 + Random.Range(-2, 4);
        dexterity = 10 + Random.Range(-2, 4);
        intelligence = 10 + Random.Range(-2, 4);
        UpdateStats();
    }


    private Sprite IconFor(ResourceKind k) { return k == ResourceKind.Wood ? woodIcon : (k == ResourceKind.Fish && fishIcon != null ? fishIcon : stoneIcon); }

    // ------------------------------------------------------------ save / load
    public WorkerSave WriteSave(List<Building> buildingOrder)
    {
        return new WorkerSave
        {
            workerName = workerName, kind = (int)gatherKind,
            strength = strength, dexterity = dexterity, intelligence = intelligence,
            fatigue = Fatigue, carry = Carry,
            position = transform.position,
            homeIndex = home != null ? buildingOrder.IndexOf(home) : -1
        };
    }

    public void ReadSave(WorkerSave s)
    {
        workerName = s.workerName;
        strength = s.strength; dexterity = s.dexterity; intelligence = s.intelligence;
        UpdateStats();
        Fatigue = Mathf.Clamp01(s.fatigue);
        Carry = Mathf.Clamp(s.carry, 0, carryCapacity);
    }

    public void SetSelected(bool sel)
    {
        selected = sel;
        if (ring != null) ring.enabled = sel && !asleepInside;
    }
    private bool selected;

    // ------------------------------------------------------------ main loop
    private void Update()
    {
        if (!started || GridManager.instance == null || !GridManager.instance.IsInitialized) return;
        float dt = Time.deltaTime;

        // nightfall: stop working and head home (a carried load is delivered first, see Decide)
        if (WantsNightSleep && (State == WorkerState.GoToNode || State == WorkerState.Gather)) CancelJob();

        switch (State)
        {
            case WorkerState.Idle: TickIdle(dt); break;
            case WorkerState.GoToNode: TickMove(dt, OnArriveNode, RepathToNode); break;
            case WorkerState.Gather: TickGather(dt); break;
            case WorkerState.GoToStorage: TickMove(dt, OnArriveStorage, RepathToStorage); break;
            case WorkerState.GoToBed: TickMove(dt, OnArriveBed, RepathToBed); break;
            case WorkerState.Sleep: TickSleep(dt); break;
            case WorkerState.Waiting: TickWaiting(dt); break;
        }

        UpdateVisuals(dt);
    }

    // ------------------------------------------------------------ decisions
    private void TickIdle(float dt)
    {
        thinkTimer -= dt;
        if (thinkTimer > 0f) return;
        thinkTimer = 0.15f;
        Decide();
    }

    private void TickWaiting(float dt)
    {
        thinkTimer -= dt;
        if (thinkTimer > 0f) return;
        thinkTimer = 1.0f;
        Decide();
    }

    private void Decide()
    {
        if (!Employed) { Wait("No job", false, null); return; }

        // tired, or night time and we have a home: finish the delivery first (if any), then go to bed
        if (Fatigue >= 1f || WantsNightSleep)
        {
            if (Carry > 0 && TryStartDelivery()) return;
            if (TryStartRest()) return;
            if (WantsNightSleep) { Wait("Cannot reach bed", true, null); return; } // never keep working through the night
        }

        if (Carry >= carryCapacity)
        {
            if (TryStartDelivery()) return;
            WaitNoStorage();
            return;
        }

        if (TryStartGather()) return;

        // nothing left to gather: don't sit on a partial load
        if (Carry > 0)
        {
            if (TryStartDelivery()) return;
            WaitNoStorage();
            return;
        }

        bool any = false;
        var g = GridManager.instance;
        for (int i = 0; i < g.Nodes.Count; i++) if (g.Nodes[i].resource == gatherKind) { any = true; break; }
        Wait(any ? "No reachable " + ResourceInfo.Name(gatherKind).ToLower() : "No " + ResourceInfo.Name(gatherKind).ToLower() + " left", false, null);
    }

    private void Wait(string reason, bool alert, string toast)
    {
        waitReason = reason;
        State = WorkerState.Waiting;
        SetAnim(false, false);
        waitAlert = alert;
        if (toast != null && HUD.Instance != null)
        {
            float last;
            if (!lastToast.TryGetValue(toast, out last) || Time.unscaledTime - last > 25f)
            {
                lastToast[toast] = Time.unscaledTime;
                HUD.Instance.Toast(toast, ToastKind.Warning);
            }
        }
    }
    private bool waitAlert;
    private bool nightSleep;

    /// <summary>Night has fallen and this worker has a home camp to sleep in.</summary>
    private bool WantsNightSleep { get { return DayCycle.Instance != null && DayCycle.Instance.IsSleepTime && home != null && home.IsPlaced; } }

    private void WaitNoStorage()
    {
        if (BuildingManager.instance.HasRole(BuildingRole.Storage)) Wait("Storage unreachable", true, "A Storage is walled in - free a tile next to it.");
        else Wait("Needs a Storage", true, "Build a Storage so workers can deliver!");
    }

    private Vector2Int MyCell() { return GridManager.instance.WorldToCell(transform.position); }

    private bool TryStartGather()
    {
        var g = GridManager.instance;
        if (node != null) { g.ReleaseNode(node, this); node = null; }
        Cell claimed;
        if (!g.TryClaimNode(this, gatherKind, MyCell(), path, out claimed)) return false;
        node = claimed;
        pathIdx = 0;
        State = WorkerState.GoToNode;
        SetAnim(true, false);
        return true;
    }

    private bool TryStartDelivery()
    {
        var bm = BuildingManager.instance;
        bm.CollectByDistance(BuildingRole.Storage, transform.position, buildingBuffer);
        // try every storage, nearest first, until one is reachable
        for (int i = 0; i < buildingBuffer.Count; i++)
        {
            if (!TryPathToDoor(buildingBuffer[i])) continue;
            storage = buildingBuffer[i];
            State = WorkerState.GoToStorage;
            SetAnim(true, false);
            return true;
        }
        return false;
    }

    private readonly List<Building> buildingBuffer = new List<Building>();

    /// <summary>Plans a path to a free cell at the building's entrance (sets path on success).</summary>
    private bool TryPathToDoor(Building b)
    {
        Vector2Int door;
        if (b == null || !b.IsPlaced) return false;
        if (!BuildingManager.instance.TryGetDoorCell(b, transform.position, out door)) return false;
        if (!PathFinder.FindPath(MyCell(), door, path)) return false;
        pathIdx = 0;
        return true;
    }

    private bool TryStartRest()
    {
        var bm = BuildingManager.instance;
        // current home first; if it is unreachable give the bed up so another camp can be used
        if (home != null && home.IsPlaced && TryPathToDoor(home))
        {
            State = WorkerState.GoToBed;
            SetAnim(true, false);
            return true;
        }
        if (home != null) { home.RemoveResident(this); home = null; }   // BuildingManager.Rebalance hands out a new bed
        return false;
    }

    public void TakeHome(Building house) { if (house.AddResident(this)) home = house; }

    // ------------------------------------------------------------ movement
    private delegate bool RepathFn();

    private void TickMove(float dt, System.Action onArrive, RepathFn repath)
    {
        if (pathIdx >= path.Count) { onArrive(); return; }

        var g = GridManager.instance;
        Cell next = g.GetCell(path[pathIdx]);
        if (next == null || !next.Walkable)
        {
            // something got built on our route: plan again from where we stand
            if (!repath()) { CancelJob(); }
            return;
        }

        float speed = moveSpeed * (Fatigue > 0.8f ? Mathf.Lerp(1f, 0.75f, (Fatigue - 0.8f) / 0.2f) : 1f);
        if (IsExhausted) speed *= 0.8f;

        Vector3 target = g.CellToWorld(path[pathIdx]);
        Vector3 pos = transform.position;
        Vector3 d = target - pos;
        float dist = d.magnitude;
        float step = speed * dt;

        if (Mathf.Abs(d.x) > 0.02f) facing = d.x > 0f ? 1 : -1;

        if (dist <= step)
        {
            pos = target;
            pathIdx++;
        }
        else pos += d / dist * step;
        transform.position = pos;

        if (pathIdx >= path.Count) onArrive();
    }

    private bool RepathToNode()
    {
        if (node == null || !node.HasNode) return false;
        return GridManager.instance.TryPathToNeighbor(MyCell(), node.pos, path) && ResetPath();
    }

    private bool RepathToStorage()
    {
        Vector2Int door;
        if (storage == null || !storage.IsPlaced) { storage = BuildingManager.instance.GetNearest(BuildingRole.Storage, transform.position); }
        if (storage == null) return false;
        if (!BuildingManager.instance.TryGetDoorCell(storage, transform.position, out door)) return false;
        return PathFinder.FindPath(MyCell(), door, path) && ResetPath();
    }

    private bool RepathToBed()
    {
        Vector2Int door;
        if (home == null || !home.IsPlaced) return false;
        if (!BuildingManager.instance.TryGetDoorCell(home, transform.position, out door)) return false;
        return PathFinder.FindPath(MyCell(), door, path) && ResetPath();
    }

    private bool ResetPath() { pathIdx = 0; return true; }

    private void CancelJob()
    {
        if (node != null) { GridManager.instance.ReleaseNode(node, this); node = null; }
        State = WorkerState.Idle;
        thinkTimer = 0.3f;
        SetAnim(false, false);
    }

    // ------------------------------------------------------------ arrivals
    private void OnArriveNode()
    {
        if (node == null || !node.HasNode || node.resource != gatherKind) { CancelJob(); return; }
        // face the deposit
        int dx = node.pos.x - MyCell().x;
        if (dx != 0) facing = dx > 0 ? 1 : -1;
        State = WorkerState.Gather;
        strikeTimer = 0f;
        nextStrike = FirstStrike;
        strikesDone = 0;
        SetAnim(false, true);
    }

    private void OnArriveStorage()
    {
        if (storage == null || !storage.IsPlaced) { State = WorkerState.Idle; SetAnim(false, false); return; }
        int amount = Carry;
        Carry = 0;
        if (amount > 0)
        {
            ResourceManager.Instance.Add(gatherKind, amount, true);
            Vector3 at = storage.FootCenterWorld + Vector3.up * 0.4f;
            if (FloatingTextManager.Instance != null)
                FloatingTextManager.Instance.ShowAmount("dep" + storage.GetInstanceID() + gatherKind, amount, " " + ResourceInfo.Name(gatherKind), ResourceInfo.Tint(gatherKind), at);
            if (HUD.Instance != null) HUD.Instance.FlyIcon(gatherKind, at);
            GameAudio.PlayAt(Sfx.Deposit, at);
            storage.Bump();
        }
        State = WorkerState.Idle;
        thinkTimer = 0.05f;
        SetAnim(false, false);
    }

    private void OnArriveBed()
    {
        if (home == null || !home.IsPlaced) { State = WorkerState.Idle; SetAnim(false, false); return; }
        State = WorkerState.Sleep;
        nightSleep = DayCycle.Instance != null && DayCycle.Instance.IsSleepTime;
        asleepInside = false;
        SetAnim(false, false);
    }

    // ------------------------------------------------------------ gather
    private void TickGather(float dt)
    {
        if (node == null || !node.HasNode) { CancelJob(); return; }

        strikeTimer += dt;
        while (strikeTimer >= nextStrike)
        {
            nextStrike += StrikePeriod;
            Strike();
            if (State != WorkerState.Gather) return;
        }
    }

    private void Strike()
    {
        Vector3 nodePos = GridManager.instance.CellToWorld(node.pos);
        Vector3 hit = Vector3.Lerp(transform.position, nodePos, 0.75f) + Vector3.up * 0.1f;

        bool wood = gatherKind == ResourceKind.Wood, fishing = gatherKind == ResourceKind.Fish;
        if (FxManager.Instance != null)
            FxManager.Instance.Chips(hit, fishing ? new Color(0.7f, 0.92f, 1f) : (wood ? new Color(0.78f, 0.56f, 0.32f) : new Color(0.82f, 0.86f, 0.94f)), wood ? 3 : 4);
        GameAudio.PlayAt(fishing ? Sfx.Splash : (wood ? Sfx.StrikeWood : Sfx.StrikeStone), nodePos, 0.8f);

        strikesDone++;
        if (strikesDone < strikesPerUnit) return;
        strikesDone = 0;

        // one unit gathered on this strike
        Cell harvested = node;
        bool depleted = GridManager.instance.HarvestOne(harvested);
        Carry++;
        if (FloatingTextManager.Instance != null)
            FloatingTextManager.Instance.ShowAmount("w" + GetInstanceID(), 1, "", ResourceInfo.Tint(gatherKind), transform.position + Vector3.up * 0.3f);
        GameAudio.PlayAt(Sfx.Pickup, transform.position, 0.5f);

        if (depleted)
        {
            node = null;
            State = WorkerState.Idle;
            thinkTimer = 0f;
            SetAnim(false, false);
            return;
        }

        if (Carry >= carryCapacity || Fatigue >= 1f)
        {
            GridManager.instance.ReleaseNode(node, this);
            node = null;
            State = WorkerState.Idle;
            thinkTimer = 0f;
            SetAnim(false, false);
        }
    }

    // ------------------------------------------------------------ sleep
    private void TickSleep(float dt)
    {
        if (home == null || !home.IsPlaced) { WakeUp(); return; }
        if (!asleepInside)
        {
            visualAlpha = Mathf.MoveTowards(visualAlpha, 0f, dt / 0.25f);
            if (visualAlpha <= 0.001f)
            {
                asleepInside = true;
                home.SleeperEntered();
            }
            return;
        }
        Fatigue = Mathf.Max(0f, Fatigue - dt / restSeconds);
        if (!nightSleep && DayCycle.Instance != null && DayCycle.Instance.IsSleepTime) nightSleep = true; // night fell while napping
        if (nightSleep)
        {
            if (DayCycle.Instance == null || !DayCycle.Instance.IsSleepTime) WakeUp();
            return;
        }
        if (Fatigue <= 0f) WakeUp();
    }

    private void WakeUp()
    {
        if (asleepInside && home != null) home.SleeperLeft();
        asleepInside = false;
        State = WorkerState.Idle;
        thinkTimer = 0f;
        SetAnim(false, false);
        if (FxManager.Instance != null) FxManager.Instance.Burst(transform.position + Vector3.up * 0.3f, new Color(1f, 1f, 0.8f), 5, 1.2f, 2f, 0.4f, 0.1f);
    }

    public void OnHomeLost()
    {
        if (asleepInside && home != null) home.SleeperLeft();
        home = null;
        if (State == WorkerState.Sleep) { asleepInside = false; State = WorkerState.Idle; SetAnim(false, false); }
        else if (State == WorkerState.GoToBed) CancelJob();
    }

    // ------------------------------------------------------------ visuals
    private void SetAnim(bool running, bool working)
    {
        if (anim == null) return;
        anim.SetBool("running", running);
        anim.SetBool("working", working);
    }

    private void UpdateVisuals(float dt)
    {
        // fade in again if we are not sleeping
        if (State != WorkerState.Sleep) visualAlpha = Mathf.MoveTowards(visualAlpha, 1f, dt / 0.2f);

        Vector3 p = transform.position;
        transform.localScale = new Vector3(facing, 1f, 1f);

        int order = -Mathf.RoundToInt(p.y * 100f);
        if (visualSr != null)
        {
            var c = baseTint; c.a = visualAlpha;
            visualSr.color = c;
            visualSr.sortingOrder = order;
            // pixel-snap the sprite itself so it never shimmers between sub-pixel positions
            visual.position = Snap(p);
        }
        if (shadow != null)
        {
            shadow.sortingOrder = order - 1;
            shadow.color = new Color(0f, 0f, 0f, 0.3f * visualAlpha);
        }
        if (ring != null)
        {
            ring.enabled = selected && !asleepInside;
            ring.sortingOrder = order - 2;
        }

        if (carryIcon != null)
        {
            bool show = Carry > 0 && visualAlpha > 0.5f;
            carryIcon.enabled = show;
            if (show)
            {
                carryIcon.sprite = IconFor(gatherKind);
                carryIcon.sortingOrder = order + 1;
                float full = (float)Carry / Mathf.Max(1, carryCapacity);
                float s = 0.55f + 0.3f * full;
                carryIcon.transform.localScale = new Vector3(s, s, 1f);
            }
        }

        // status bubble
        if (bubble != null)
        {
            Sprite want = null;
            if (State == WorkerState.Waiting) want = waitAlert ? bubbleAlert : bubbleWait;
            else if (IsExhausted && home == null) want = bubbleAlert;
            else if (State == WorkerState.Sleep && !asleepInside) want = bubbleSleep;

            bubble.enabled = want != null;
            bubble.transform.localScale = new Vector3(facing, 1f, 1f); // cancel the root's mirroring so glyphs never flip
            if (want != null)
            {
                bubble.sprite = want;
                float bob = Mathf.Round(Mathf.Sin(Time.time * 5f) * 1.2f) / 16f;
                bubble.transform.position = Snap(p + new Vector3(0f, 0.85f + bob, 0f));
            }
        }
    }

    private static Vector3 Snap(Vector3 v)
    {
        // snap to 1/16 world unit (one sprite pixel)
        return new Vector3(Mathf.Round(v.x * 16f) / 16f, Mathf.Round(v.y * 16f) / 16f, 0f);
    }

    private void OnDrawGizmos()
    {
        if (path == null || path.Count == 0 || GridManager.instance == null) return;
        Gizmos.color = Color.cyan;
        for (int i = pathIdx; i < path.Count - 1; i++)
            Gizmos.DrawLine(GridManager.instance.CellToWorld(path[i]), GridManager.instance.CellToWorld(path[i + 1]));
    }
}
