using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// Menu: BuildSim/Setup/3 Create Level Scene.
/// Builds Assets/Scenes/Level.unity: every system, tilemap, light, UI element and audio source is a visible
/// GameObject in the Hierarchy (nothing is created by code at runtime except workers, buildings and effects).
/// After this you own the scene: edit it by hand; use the MapGenerator / HUD Inspector buttons to regenerate parts.
/// </summary>
public static class LevelSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Level.unity";
    private const string VolumePath = "Assets/Settings/LevelPostFX.asset";

    [MenuItem("BuildSim/Setup/3 Create Level Scene")]
    public static void MenuCreate()
    {
        if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Create Level Scene",
            "Assets/Scenes/Level.unity already exists. Rebuilding replaces it (your hand edits in that scene are lost).", "Rebuild", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    [MenuItem("BuildSim/Dev/Setup Everything (no prompt)")]
    public static void SetupEverything()
    {
        if (SceneIsDirty()) { Debug.LogError("[Dev] Setup aborted: the open scene has unsaved changes."); return; }
        ArtGenerator.Run();
        PrefabSetup.Run();
        Build();
    }

    private static bool SceneIsDirty()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty) return true;
        return false;
    }

    // ---------------------------------------------------------------- build
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---- camera, light, post-processing
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 10f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.17f, 0.28f, 0.18f);
        cam.transform.position = new Vector3(16f, 12f, -10f);
        camGo.AddComponent<AudioListener>();
        var urpCam = camGo.AddComponent<UniversalAdditionalCameraData>();
        urpCam.renderPostProcessing = true;
        camGo.AddComponent<CameraMotor>();

        var globalLight = new GameObject("Global Light 2D");
        var gl = globalLight.AddComponent<Light2D>();
        gl.lightType = Light2D.LightType.Global;
        gl.intensity = 1f;
        LightUtil.ApplyToAllSortingLayers(gl);

        var volumeGo = new GameObject("Post FX Volume");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = GetOrCreateProfile();

        // ---- map: grid + tilemaps + generator
        var map = new GameObject("Map");
        var gridManager = map.AddComponent<GridManager>();
        var generator = map.AddComponent<MapGenerator>();

        var gridGo = new GameObject("Grid");
        gridGo.transform.SetParent(map.transform, false);
        gridGo.transform.position = new Vector3(-0.5f, -0.5f, 0f);   // tile cell (x,y) is centred on world (x,y)
        gridGo.AddComponent<Grid>();

        var ground = MakeTilemap("Ground", gridGo.transform, Layers.Ground, -10000);
        var decor = MakeTilemap("Decor", gridGo.transform, Layers.Ground, -9800);
        var gridLines = MakeTilemap("GridLines", gridGo.transform, Layers.Ground, -9500);
        var highlight = MakeTilemap("Highlight", gridGo.transform, Layers.Ground, -9000);
        var deposits = MakeTilemap("Deposits", gridGo.transform, Layers.Objects, -5000);

        var overlay = gridLines.gameObject.AddComponent<GridOverlay>();
        overlay.map = gridLines;
        var hl = highlight.gameObject.AddComponent<HighlightLayer>();
        hl.map = highlight;
        hl.fillTile = ArtGenerator.LoadTile("Hl_Fill");
        hl.outlineTile = ArtGenerator.LoadTile("Hl_Outline");
        hl.dotTile = ArtGenerator.LoadTile("Hl_Dot");

        gridManager.groundMap = ground;
        gridManager.resourceMap = deposits;
        gridManager.minedTiles = Tiles("Dirt_", 3);

        generator.grid = gridManager;
        generator.ground = ground;
        generator.decor = decor;
        generator.resources = deposits;
        generator.overlay = gridLines;
        generator.grassLight = Tiles("Grass_Light_", 3);
        generator.grassMid = Tiles("Grass_Mid_", 4);
        generator.grassDark = Tiles("Grass_Dark_", 3);
        generator.grassDecor = Tiles("Grass_Decor_", 5);
        generator.dirt = Tiles("Dirt_", 3);
        generator.water = Tiles("Water_", 2);
        generator.bush = Tiles("Bush_", 2);
        generator.gridLine = ArtGenerator.LoadTile("GridLine");
        MapLook.Setup(generator, gridManager, gridGo.transform);

        // ---- systems (one object per system, all visible)
        var systems = new GameObject("Systems").transform;
        Child<GameClock>(systems, "Game Clock");
        Child<ResourceManager>(systems, "Stockpile");
        Child<BuildingManager>(systems, "Buildings");

        var colony = Child<Colony>(systems, "Colony");
        colony.catalog = LoadBuildingCatalog();
        colony.workerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSetup.WorkerPrefab).GetComponent<Worker>();

        var placement = Child<PlacementController>(systems, "Placement");
        placement.highlight = hl;
        placement.gridOverlay = overlay;

        var selection = Child<SelectionController>(systems, "Selection");
        selection.highlight = hl;

        var day = Child<DayCycle>(systems, "Day Cycle");
        day.globalLight = gl;

        var fx = Child<FxManager>(systems, "Effects");
        fx.particleSprite = ArtGenerator.LoadSprite("pixel");

        BuildAudio(systems);
        BuildFloatingText(systems);

        var session = Child<LevelSession>(systems, "Level Session");
        session.generator = generator;
        session.grid = gridManager;

        // ---- UI
        var uiGo = new GameObject("UI");
        var canvas = uiGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = uiGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        uiGo.AddComponent<GraphicRaycaster>();
        var hud = uiGo.AddComponent<HUD>();
        var hso = new SerializedObject(hud);
        SetRef(hso, "panelSprite", ArtGenerator.LoadSprite("panel"));
        SetRef(hso, "barSprite", ArtGenerator.LoadSprite("bar"));
        SetRef(hso, "pixelSprite", ArtGenerator.LoadSprite("pixel"));
        SetRef(hso, "stoneSprite", ArtGenerator.LoadSprite("icon_stone"));
        SetRef(hso, "woodSprite", ArtGenerator.LoadSprite("icon_wood"));
        SetRef(hso, "popSprite", ArtGenerator.LoadSprite("icon_pop"));
        hso.ApplyModifiedPropertiesWithoutUndo();
        hud.BuildUI(colony);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // ---- first map
        generator.Generate();
        var b = gridManager.WorldBounds;
        cam.transform.position = new Vector3(b.center.x, b.center.y, -10f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log("[Dev] LevelSceneBuilder: created " + ScenePath + " (map " + gridManager.width + "x" + gridManager.height + ", seed " + generator.settings.seed + ")");
    }

    // ---------------------------------------------------------------- pieces
    public static void BuildAudio(Transform systems)
    {
        var go = new GameObject("Audio");
        go.transform.SetParent(systems, false);
        var audio = go.AddComponent<GameAudio>();

        var sfx = new List<AudioSource>();
        for (int i = 0; i < 12; i++) sfx.Add(NewSource(go.transform, "SFX " + (i + 1)));
        audio.sfxSources = sfx.ToArray();
        audio.musicPad = NewSource(go.transform, "Music Pad");
        audio.musicNotes = NewSource(go.transform, "Music Notes");
        audio.wind = NewSource(go.transform, "Ambient Wind");
        audio.birds = NewSource(go.transform, "Ambient Birds");
    }

    private static AudioSource NewSource(Transform parent, string name)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        var s = g.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        return s;
    }

    private static void BuildFloatingText(Transform systems)
    {
        var go = new GameObject("Floating Text");
        go.transform.SetParent(systems, false);
        var mgr = go.AddComponent<FloatingTextManager>();

        var cgo = new GameObject("FloatingTextCanvas");
        cgo.transform.SetParent(go.transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var tgo = new GameObject("Template", typeof(RectTransform));
        tgo.transform.SetParent(cgo.transform, false);
        var text = tgo.AddComponent<Text>();
        text.font = UiFont.Get();
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = "+1";
        var outline = tgo.AddComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.07f, 0.12f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);
        text.rectTransform.sizeDelta = new Vector2(260, 60);
        tgo.SetActive(false);

        mgr.canvas = canvas;
        mgr.template = text;
    }

    private static Tilemap MakeTilemap(string name, Transform parent, string layer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tm = go.AddComponent<Tilemap>();
        var tr = go.AddComponent<TilemapRenderer>();
        tr.sortingLayerName = layer;
        tr.sortingOrder = order;
        var lit = PrefabSetup.LitMaterial;
        if (lit != null) tr.sharedMaterial = lit;
        return tm;
    }

    private static T Child<T>(Transform parent, string name) where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    private static TileBase[] Tiles(string prefix, int count)
    {
        var list = new TileBase[count];
        for (int i = 0; i < count; i++) list[i] = ArtGenerator.LoadTile(prefix + i);
        return list;
    }

    private static List<Building> LoadBuildingCatalog()
    {
        var list = new List<Building>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabSetup.BuildingDir }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var b = go != null ? go.GetComponent<Building>() : null;
            if (b != null) list.Add(b);
        }
        list.Sort((a, b) =>
        {
            int c = ((int)a.role).CompareTo((int)b.role);
            return c != 0 ? c : string.CompareOrdinal(a.displayName, b.displayName);
        });
        return list;
    }

    private static VolumeProfile GetOrCreateProfile()
    {
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
        if (existing != null) return existing;

        Directory.CreateDirectory("Assets/Settings");
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, VolumePath);

        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.26f);
        vig.smoothness.Override(0.55f);
        vig.color.Override(new Color(0.05f, 0.03f, 0.1f));
        AssetDatabase.AddObjectToAsset(vig, profile);

        var adj = profile.Add<ColorAdjustments>(true);
        adj.saturation.Override(10f);
        adj.contrast.Override(6f);
        AssetDatabase.AddObjectToAsset(adj, profile);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("LevelSceneBuilder: field not found " + field); return; }
        p.objectReferenceValue = value;
    }

    private static void AddToBuildSettings(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == path);
        list.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
