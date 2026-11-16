using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One play session of one map. Lives in the Level scene.
///  - Awake (very early): if a save exists for <see cref="mapId"/> the saved map is painted into the scene
///    tilemaps BEFORE the GridManager scans them; otherwise an empty scene map is generated.
///  - Colony.Start restores buildings/workers from <see cref="Pending"/>.
///  - Autosaves every N seconds and on quit/pause. F5 = save now, F9 = reload the scene from the save.
/// </summary>
[DefaultExecutionOrder(-200)]
public class LevelSession : MonoBehaviour
{
    public static LevelSession Instance { get; private set; }

    [Header("Identity")]
    [Tooltip("Save slot of this map. The overworld will set this when it opens a map.")]
    public string mapId = "map_01";

    [Header("Overworld link")]
    [Tooltip("Scene to return to (must be in the build settings)")]
    public string worldSceneName = "Overworld";

    [Header("Behaviour")]
    public bool loadSaveOnStart = true;
    public bool autoSave = true;
    public float autoSaveSeconds = 30f;

    [Header("Scene references")]
    public MapGenerator generator;
    public GridManager grid;

    /// <summary>The save that was loaded at start (null for a fresh map).</summary>
    public LevelSave Pending { get; private set; }

    /// <summary>Set by 'reset map': the next Level load generates a fresh map instead of using the scene's painted one.</summary>
    public static bool ForceGenerate;

    private float playSeconds;
    private float autoTimer;
    private bool ready;

    private void Awake()
    {
        Instance = this;

        if (generator == null || grid == null) { Debug.LogError("LevelSession: generator / grid not assigned."); return; }

        bool fromWorld = LevelContext.HasContext;
        if (fromWorld) ApplyWorldContext();

        if (loadSaveOnStart && MapStore.Exists(mapId))
        {
            try
            {
                Pending = MapStore.Load(mapId);
                playSeconds = Pending.playSeconds;
                generator.Apply(Pending.map);
                Debug.Log("[Dev] LevelSession: loaded save '" + mapId + "' (" + Pending.map.width + "x" + Pending.map.height + ")");
            }
            catch (System.Exception e)
            {
                Debug.LogError("LevelSession: could not load save '" + mapId + "': " + e.Message);
                Pending = null;
            }
        }

        if (Pending == null && (fromWorld || (generator.generateOnPlayIfEmpty && generator.IsEmpty) || generator.regenerateOnPlay || ForceGenerate))
        {
            generator.Generate();
            Debug.Log("[Dev] LevelSession: generated a new map (seed " + generator.settings.seed + ")");
        }
        ForceGenerate = false;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        // the grid has scanned the tilemaps in its own Awake; now restore remaining deposit amounts
        if (Pending != null) grid.ApplyYields(Pending.map);
        ready = true;
    }

    private void Update()
    {
        playSeconds += Time.unscaledDeltaTime;
        if (!ready) return;

        if (autoSave)
        {
            autoTimer += Time.unscaledDeltaTime;
            if (autoTimer >= autoSaveSeconds) { autoTimer = 0f; Save(false); }
        }

        if (DebugConsole.IsOpen) return;
        if (Input.GetKeyDown(KeyCode.F5)) Save(true);
        if (Input.GetKeyDown(KeyCode.F9)) Reload();
        if (Input.GetKeyDown(KeyCode.Tab)) ReturnToWorld();
    }

    private void OnApplicationQuit() { if (autoSave && ready) Save(false); }
    private void OnApplicationPause(bool paused) { if (paused && autoSave && ready) Save(false); }

    // ------------------------------------------------------------ actions
    public void Save(bool toast)
    {
        if (!ready || grid == null || !grid.IsInitialized) return;

        var save = new LevelSave { mapId = mapId, playSeconds = playSeconds };
        int seed = generator != null ? generator.settings.seed : 0;
        int tier = generator != null ? generator.settings.tier : 0;
        save.map = grid.Capture(seed, tier);
        if (CrateManager.Instance != null) CrateManager.Instance.Capture(save.map);

        if (ResourceManager.Instance != null) ResourceManager.Instance.WriteSave(save);
        if (DayCycle.Instance != null) DayCycle.Instance.WriteSave(save);
        if (Colony.Instance != null) Colony.Instance.WriteSave(save);

        MapStore.Save(save);
        if (toast && HUD.Instance != null) HUD.Instance.Toast("Map saved", ToastKind.Good, 1.8f);
    }

    /// <summary>Reloads the scene; the saved map and colony are restored on start.</summary>
    public void Reload()
    {
        if (!MapStore.Exists(mapId)) { if (HUD.Instance != null) HUD.Instance.Toast("No save to load", ToastKind.Warning); return; }
        loadSaveOnStart = true;
        autoSave = false; // do not overwrite the save with the state we are about to throw away
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void DeleteSave() { MapStore.Delete(mapId); }

    /// <summary>Throws this map's save away and reloads the level with a freshly generated map (same seed when opened from the world map).</summary>
    public void ResetMap()
    {
        MapStore.Delete(mapId);
        ForceGenerate = true;
        autoSave = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Saves this map and goes back to the overworld.</summary>
    public void ReturnToWorld()
    {
        if (!Application.CanStreamedLevelBeLoaded(worldSceneName))
        {
            if (HUD.Instance != null) HUD.Instance.Toast("Scene '" + worldSceneName + "' is not in the build settings", ToastKind.Warning);
            return;
        }
        Save(false);
        Time.timeScale = 1f;
        SceneManager.LoadScene(worldSceneName);
    }

    /// <summary>The overworld decided which map to open: slot, seed, tier and modifier.</summary>
    private void ApplyWorldContext()
    {
        mapId = LevelContext.MapId;
        loadSaveOnStart = true;
        var s = generator.settings;
        s.seed = LevelContext.Seed;
        s.tier = LevelContext.Tier;
        s.sizeFromTier = true;

        float yieldMul; int bonusWood, bonusStone;
        Modifiers.ApplyToLevel(LevelContext.ModifierId, s, out yieldMul, out bonusWood, out bonusStone);
        grid.stoneYield = Mathf.RoundToInt(grid.stoneYield * yieldMul);
        grid.woodYield = Mathf.RoundToInt(grid.woodYield * yieldMul);
        var stock = FindObjectOfType<ResourceManager>();
        if (stock != null) { stock.startWood += bonusWood; stock.startStone += bonusStone; }
    }
}
