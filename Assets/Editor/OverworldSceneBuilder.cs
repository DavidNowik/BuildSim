using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Menu: BuildSim/Setup/4 Create Overworld Scene.
/// Creates the Node / Edge prefabs and Assets/Scenes/Overworld.unity. Everything is a visible GameObject:
/// camera + sky + clouds, the sea, plateau, settlement sites, the lands/paths roots, effects, audio and the HUD.
/// Lands and paths themselves are spawned from the prefabs when the scene runs (the net is different every tide).
/// </summary>
public static class OverworldSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Overworld.unity";
    public const string PrefabDir = "Assets/Prefabs/Overworld";
    private const string MaterialDir = "Assets/Materials";
    private const string VolumePath = "Assets/Settings/OverworldPostFX.asset";

    [MenuItem("BuildSim/Setup/4 Create Overworld Scene")]
    public static void MenuCreate()
    {
        if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Create Overworld Scene",
            "Assets/Scenes/Overworld.unity already exists. Rebuilding replaces it (hand edits in that scene are lost).", "Rebuild", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Everything();
    }

    [MenuItem("BuildSim/Dev/Setup Overworld (no prompt)")]
    public static void SetupSilent()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Dev] Overworld setup aborted: the open scene has unsaved changes."); return; }
        Everything();
    }

    private static void Everything()
    {
        ArtGenerator.Run();
        OverworldArt.Run();
        AssetDatabase.Refresh();
        BuildPrefabs();
        BuildScene();
    }

    // ---------------------------------------------------------------- materials
    private static Material GetMaterial(string name, Texture tex)
    {
        Directory.CreateDirectory(MaterialDir);
        string path = MaterialDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Sprites/Default")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.mainTexture = tex;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Sprite S(string name) { return ArtGenerator.LoadSprite(name); }

    private static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, int order, Vector3 pos, float scale = 1f, string layer = "Default")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return sr;
    }

    private static TextMesh MakeText(Transform parent, string name, string text, float size, Color color, int order, Vector3 pos, bool bold = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.font = UiFont.Get();
        tm.fontSize = 64;
        tm.characterSize = size;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        tm.text = text;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = tm.font.material;
        mr.sortingLayerName = "Default";
        mr.sortingOrder = order;
        return tm;
    }

    // ---------------------------------------------------------------- prefabs
    private static void BuildPrefabs()
    {
        Directory.CreateDirectory(PrefabDir);

        // ---- Node
        var root = new GameObject("Node");
        var col = root.AddComponent<CircleCollider2D>();
        col.radius = 0.68f;
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);

        var shadow = MakeSprite(visual, "Shadow", S("shadow"), -2, new Vector3(0f, -0.78f, 0f), 0.065f * 16f / 4f);
        shadow.transform.localScale = new Vector3(0.7f, 0.5f, 1f);
        shadow.color = new Color(0, 0, 0, 0.45f);
        var pulseA = MakeSprite(visual, "PulseA", S("ow_ring"), -1, Vector3.zero);
        var pulseB = MakeSprite(visual, "PulseB", S("ow_ring"), -1, Vector3.zero);
        var rim = MakeSprite(visual, "Rim", S("ow_node_rim"), 0, Vector3.zero);
        var body = MakeSprite(visual, "Body", S("ow_node_body"), 1, Vector3.zero);
        var gloss = MakeSprite(visual, "Gloss", S("ow_node_gloss"), 2, new Vector3(-0.2f, 0.26f, 0f));
        gloss.transform.localRotation = Quaternion.Euler(0, 0, 28f);
        var icon = MakeSprite(visual, "Icon", null, 3, new Vector3(0f, 0.02f, 0f));
        var question = MakeText(visual, "Question", "?", 0.1f, new Color(1f, 0.9f, 0.48f), 4, new Vector3(0f, 0.02f, 0f));
        var select = MakeSprite(visual, "SelectRing", S("ow_select"), 5, Vector3.zero);
        select.color = new Color(1f, 0.95f, 0.5f, 0.95f);
        select.enabled = false;

        var nameShadow = MakeText(visual, "NameShadow", "", 0.056f, new Color(0.07f, 0.03f, 0.16f, 0.95f), 5, new Vector3(0.03f, -0.99f, 0f));
        var nameLabel = MakeText(visual, "Name", "", 0.056f, Color.white, 6, new Vector3(0f, -0.96f, 0f));
        var infoShadow = MakeText(visual, "InfoShadow", "", 0.036f, new Color(0.07f, 0.03f, 0.16f, 0.95f), 5, new Vector3(0.025f, -1.27f, 0f), false);
        var infoLabel = MakeText(visual, "Info", "", 0.036f, Color.white, 6, new Vector3(0f, -1.25f, 0f), false);

        var node = root.AddComponent<OverworldNode>();
        node.shadow = shadow; node.rim = rim; node.body = body; node.gloss = gloss; node.icon = icon;
        node.pulseA = pulseA; node.pulseB = pulseB; node.selectRing = select;
        node.questionLabel = question; node.nameLabel = nameLabel; node.infoLabel = infoLabel;
        node.nameShadow = nameShadow; node.infoShadow = infoShadow; node.visual = visual;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Node.prefab");
        Object.DestroyImmediate(root);

        // ---- Edge
        var dashTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtGenerator.SpriteDir + "/ow_dash.png");
        var edge = new GameObject("Edge");
        var lr = edge.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.widthMultiplier = 0.1f;
        lr.textureMode = LineTextureMode.Tile;
        lr.numCapVertices = 3;
        lr.sharedMaterial = GetMaterial("OverworldDash", dashTex);
        lr.sortingLayerName = "Default";
        lr.sortingOrder = -5;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        var eo = edge.AddComponent<OverworldEdge>();
        eo.line = lr;
        PrefabUtility.SaveAsPrefabAsset(edge, PrefabDir + "/Edge.prefab");
        Object.DestroyImmediate(edge);
        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- scene
    private static void BuildScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---- camera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 9f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.24f, 0.10f, 0.42f);
        cam.transform.position = new Vector3(0f, -6f, -10f);
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        var rig = camGo.AddComponent<OverworldCamera>();

        // ---- sky (children of the camera so they stay on screen)
        var skyObj = new GameObject("Sky");
        skyObj.transform.SetParent(camGo.transform, false);
        var sky = skyObj.AddComponent<StormSky>();
        sky.cam = cam;

        var gradGo = new GameObject("Sky Gradient");
        gradGo.transform.SetParent(skyObj.transform, false);
        gradGo.transform.localPosition = new Vector3(0, 0, 10f);
        var gf = gradGo.AddComponent<MeshFilter>();
        var gr = gradGo.AddComponent<MeshRenderer>();
        gr.sharedMaterial = GetMaterial("SkyGradient", null);
        gr.sortingLayerName = Layers.Ground;
        gr.sortingOrder = -200;
        sky.skyGradient = gf;

        var cloudsRoot = new GameObject("Clouds").transform;
        cloudsRoot.SetParent(skyObj.transform, false);
        var clouds = new List<StormSky.Cloud>();
        var rnd = new System.Random(7);
        for (int i = 0; i < 9; i++)
        {
            var c = new GameObject("Cloud " + (i + 1)).transform;
            c.SetParent(cloudsRoot, false);
            var puffs = new List<SpriteRenderer>();
            for (int k = 0; k < 5; k++)
            {
                float rr = 1.6f * (1f - Mathf.Abs(k - 2) * 0.16f);
                var sr = MakeSprite(c, "Puff " + (k + 1), S("ow_cloud"), 0, new Vector3((k - 2) * 1.2f, Mathf.Sin(k * 1.7f + i) * 0.3f, 0f), rr, Layers.Roof);
                puffs.Add(sr);
            }
            clouds.Add(new StormSky.Cloud
            {
                root = c, puffs = puffs.ToArray(),
                x01 = (float)rnd.NextDouble(), yFrac = (float)rnd.NextDouble(),
                speed = 0.25f + (float)rnd.NextDouble() * 0.5f, size = 0.7f + (float)rnd.NextDouble() * 0.7f
            });
        }
        sky.clouds = clouds.ToArray();

        var flash = MakeSprite(skyObj.transform, "Lightning Flash", S("pixel"), -40, new Vector3(0, 0, 5f), 1f, Layers.UI);
        flash.color = new Color(0.78f, 0.88f, 1f, 0f);
        sky.lightning = flash;

        var volumeGo = new GameObject("Post FX Volume");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = GetOrCreateProfile();
        sky.volume = volume;

        // ---- world decor: plateau (high ground), high-water mark, locked settlement sites
        var world = new GameObject("World").transform;
        var plateau = MakeSprite(world, "Plateau (high ground)", S("ow_plateau"), -20, new Vector3(0f, 0.955f, 0f));
        var plateauLabel = MakeText(world, "High Ground Label", "HIGH GROUND", 0.07f, new Color(0.81f, 0.76f, 1f), -15, new Vector3(0f, 3.3f, 0f));
        plateauLabel.fontSize = 64;

        var dashMat = GetMaterial("OverworldDash", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtGenerator.SpriteDir + "/ow_dash.png"));
        var hw = new GameObject("High-Water Mark");
        hw.transform.SetParent(world, false);
        var hwl = hw.AddComponent<LineRenderer>();
        hwl.useWorldSpace = true; hwl.positionCount = 2;
        hwl.SetPosition(0, new Vector3(-9.5f, 0.62f, 0f)); hwl.SetPosition(1, new Vector3(9.5f, 0.62f, 0f));
        hwl.widthMultiplier = 0.04f; hwl.textureMode = LineTextureMode.Tile; hwl.sharedMaterial = dashMat;
        hwl.startColor = hwl.endColor = new Color(0.28f, 0.78f, 1f, 0.45f);
        hwl.sortingLayerName = "Default"; hwl.sortingOrder = -10;
        var hwLabel = MakeText(world, "High-Water Label", "high-water mark", 0.055f, new Color(0.5f, 0.82f, 0.92f), -9, new Vector3(-8.4f, 0.83f, 0f), false);
        hwLabel.anchor = TextAnchor.MiddleLeft;
        hwLabel.transform.localPosition = new Vector3(-9.4f, 0.83f, 0f);

        var sites = new List<Collider2D>();
        Vector2[] slotPos = { new Vector2(-3.58f, 1.58f), new Vector2(0f, 2.75f), new Vector2(3.58f, 1.58f) };
        for (int i = 0; i < 3; i++)
        {
            var site = new GameObject("Settlement Site " + (i + 1) + " (locked)").transform;
            site.SetParent(world, false);
            site.position = slotPos[i];
            MakeSprite(site, "Hex", S("ow_hex"), -8, Vector3.zero).color = new Color(1, 1, 1, 0.85f);
            MakeText(site, "Glyph", "H", 0.09f, new Color(0.81f, 0.76f, 1f), -7, new Vector3(0, 0.02f, 0));
            MakeText(site, "Label", "settlement site", 0.045f, new Color(0.66f, 0.61f, 0.92f), -7, new Vector3(0, -0.78f, 0), false);
            var cc = site.gameObject.AddComponent<CircleCollider2D>();
            cc.radius = 0.5f;
            sites.Add(cc);
            var link = new GameObject("Link").AddComponent<LineRenderer>();
            link.transform.SetParent(site, false);
            link.useWorldSpace = true; link.positionCount = 2;
            link.SetPosition(0, new Vector3(0, 0, 0)); link.SetPosition(1, new Vector3(slotPos[i].x, slotPos[i].y, 0));
            link.widthMultiplier = 0.05f; link.textureMode = LineTextureMode.Tile; link.sharedMaterial = dashMat;
            link.startColor = link.endColor = new Color(0.81f, 0.76f, 1f, 0.5f);
            link.sortingLayerName = "Default"; link.sortingOrder = -9;
        }

        // ---- lands + paths (filled at runtime from the prefabs)
        var lands = new GameObject("Lands").transform;
        var paths = new GameObject("Paths").transform;

        // ---- the sea
        var seaGo = new GameObject("Sea");
        var seaFilter = seaGo.AddComponent<MeshFilter>();
        var seaRenderer = seaGo.AddComponent<MeshRenderer>();
        seaRenderer.sharedMaterial = GetMaterial("TideSea", null);
        seaRenderer.sortingLayerName = Layers.UI;
        seaRenderer.sortingOrder = -100;
        seaRenderer.shadowCastingMode = ShadowCastingMode.Off;
        var sea = seaGo.AddComponent<TideSea>();

        // ---- systems
        var systems = new GameObject("Systems").transform;
        var fxGo = new GameObject("Effects"); fxGo.transform.SetParent(systems, false);
        var fx = fxGo.AddComponent<FxManager>();
        fx.particleSprite = S("pixel");
        LevelSceneBuilder.BuildAudio(systems);

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
        var hud = uiGo.AddComponent<OverworldHUD>();
        var hso = new SerializedObject(hud);
        Ref(hso, "panelSprite", S("panel")); Ref(hso, "barSprite", S("bar")); Ref(hso, "pixelSprite", S("pixel")); Ref(hso, "pearlSprite", S("icon_pearl"));
        var moons = hso.FindProperty("moonSprites");
        moons.arraySize = 5;
        for (int i = 0; i < 5; i++) moons.GetArrayElementAtIndex(i).objectReferenceValue = S("moon_" + i);
        hso.ApplyModifiedPropertiesWithoutUndo();

        var icons = new Sprite[Modifiers.All.Length];
        for (int i = 0; i < icons.Length; i++) icons[i] = S(OverworldArt.ModifierIconNames[i]);
        hud.BuildUI(icons);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // ---- controller (references everything above)
        var ctlGo = new GameObject("Overworld");
        var ctl = ctlGo.AddComponent<OverworldController>();
        ctl.nodePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Node.prefab").GetComponent<OverworldNode>();
        ctl.edgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Edge.prefab").GetComponent<OverworldEdge>();
        ctl.nodesRoot = lands; ctl.edgesRoot = paths;
        ctl.sea = sea; ctl.sky = sky; ctl.cameraRig = rig; ctl.hud = hud;
        ctl.settlementSites = sites.ToArray();
        ctl.modifierIcons = icons;

        // frame the world for the editor view (the controller re-fits at runtime)
        cam.transform.position = new Vector3(0f, -9f, -10f);
        cam.orthographicSize = 12f;
        sea.Set(-20f, 0.1f, 0.12f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        // build order: Overworld first, then Level
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == ScenePath);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("[Dev] OverworldSceneBuilder: created " + ScenePath + " and prefabs in " + PrefabDir);
    }

    private static void Ref(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("OverworldSceneBuilder: field not found " + field); return; }
        p.objectReferenceValue = value;
    }

    private static VolumeProfile GetOrCreateProfile()
    {
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
        if (existing != null) return existing;
        Directory.CreateDirectory("Assets/Settings");
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, VolumePath);
        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.26f); vig.smoothness.Override(0.55f); vig.color.Override(new Color(0.05f, 0.03f, 0.1f));
        AssetDatabase.AddObjectToAsset(vig, profile);
        var adj = profile.Add<ColorAdjustments>(true);
        adj.saturation.Override(10f); adj.contrast.Override(6f);
        AssetDatabase.AddObjectToAsset(adj, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }
}
