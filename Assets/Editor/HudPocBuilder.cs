using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Menu: BuildSim/Setup/10 Build HUD PoC. Adds a "HUD PoC" canvas to the Level scene: panels full of proxy buttons
/// (A1, A2, ... group letter + number) in every screen zone, so you can judge the layout. Rebuilding replaces only the PoC.
/// </summary>
public static class HudPocBuilder
{
    private static Sprite panel;
    private static readonly Color PanelCol = new Color(0.09f, 0.08f, 0.15f, 0.94f);
    private static readonly Color ButtonCol = new Color(0.22f, 0.2f, 0.36f, 1f);
    private static readonly Color TextCol = new Color(0.97f, 0.96f, 1f);
    private static readonly Color Accent = new Color(1f, 0.82f, 0.4f);
    private static HudPoc poc;

    [MenuItem("BuildSim/Setup/10 Build HUD PoC")]
    public static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
    }

    [MenuItem("BuildSim/Dev/Build HUD PoC (no prompt)")]
    public static void Silent()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Dev] HUD PoC aborted: the open scene has unsaved changes."); return; }
        Run();
    }

    private static void Run()
    {
        ArtGenerator.Run();     // the category icons must exist before they are assigned
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene(LevelSceneBuilder.ScenePath, OpenSceneMode.Single);
        panel = ArtGenerator.LoadSprite("panel");

        // the old HUD is gone for good: this PoC is the HUD now
        foreach (var old in Object.FindObjectsOfType<HUD>()) Object.DestroyImmediate(old.gameObject);

        foreach (var go in scene.GetRootGameObjects())   // (Find would miss an inactive one)
            if (go.name == "HUD PoC") Object.DestroyImmediate(go);

        var root = new GameObject("HUD PoC");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 11;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        // the controller lives under Systems; any leftovers from earlier builds are removed first
        foreach (var stale in Object.FindObjectsOfType<HudPoc>()) Object.DestroyImmediate(stale.gameObject);
        var ctl = new GameObject("HUD PoC Controller");
        var systems = GameObject.Find("Systems");
        if (systems != null) ctl.transform.SetParent(systems.transform, false);
        poc = ctl.AddComponent<HudPoc>();

        var rt = (RectTransform)root.transform;

        poc.lastClick = Label(rt, "Last Click", "last click: -", 26, TextAnchor.MiddleCenter, Accent, new Vector2(0, 1), new Vector2(330, -170), new Vector2(420, 40));

        // ---- top bar (clock + resource list, grows downward); the tab row (T) hangs under it
        var top = TopBarPanel(rt);
        var tabs = Panel("Tabs", top, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(620, 60));
        Row(tabs, "T", 5, new Vector2(10, 0), 114, 44, 8, new Vector2(0, 0.5f));

        // ---- left toolbar (C), right toolbar (D)
        var left = Panel("Left Toolbar", rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, -20), new Vector2(86, 8 * 68 + 12));
        Column(left, "C", 8, 62, 62, 6);
        var right = Panel("Right Toolbar", rt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, -20), new Vector2(86, 8 * 68 + 12));
        Column(right, "D", 8, 62, 62, 6);

        // ---- build bar (E): one tab per build layer, each opens a menu of its buildings above the bar
        var bar = BuildBar(rt);

        // ---- bottom-left: map/info block (F), bottom-right: speed + actions (G)
        var bl = Panel("Bottom Left", rt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 14), new Vector2(300, 116));
        Label(bl, "Title", "MAP / INFO", 18, TextAnchor.UpperLeft, Accent, new Vector2(0, 1), new Vector2(12, -8), new Vector2(200, 24));
        Row(bl, "F", 4, new Vector2(10, -14), 64, 60, 8, new Vector2(0, 0.5f));
        var br = Panel("Bottom Right", rt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 14), new Vector2(300, 116));
        Label(br, "Title", "SPEED / ACTIONS", 18, TextAnchor.UpperRight, Accent, new Vector2(1, 1), new Vector2(-12, -8), new Vector2(240, 24));
        Row(br, "G", 4, new Vector2(-10, -14), 64, 60, 8, new Vector2(1, 0.5f), true);

        // ---- floating inspector panel (H) in the right third, between toolbar and build bar
        var insp = Panel("Inspector", rt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-110, 40), new Vector2(380, 360));
        inspTitleRef = Label(insp, "Title", "INSPECTOR", 22, TextAnchor.UpperLeft, Accent, new Vector2(0, 1), new Vector2(16, -12), new Vector2(300, 30));
        inspBodyRef = Label(insp, "Body", "name\nstatus lines ...\nstats ...", 20, TextAnchor.UpperLeft, TextCol, new Vector2(0, 1), new Vector2(16, -52), new Vector2(340, 150));
        Row(insp, "H", 4, new Vector2(12, 14), 84, 52, 8, new Vector2(0, 0), false, true);

        // ---- goal / notifications panel top left under the bar (J)
        var goal = Panel("Goal Panel", rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -14), new Vector2(430, 124));
        Label(goal, "Title", "GOAL", 18, TextAnchor.UpperLeft, Accent, new Vector2(0, 1), new Vector2(14, -8), new Vector2(200, 24));
        Label(goal, "Body", "Goal text goes here", 22, TextAnchor.UpperLeft, TextCol, new Vector2(0, 1), new Vector2(14, -34), new Vector2(400, 30));
        Row(goal, "J", 3, new Vector2(12, 10), 110, 38, 8, new Vector2(0, 0), false, true);

        // ---- every panel can be slid off its nearest screen edge (click or drag the little handle)
        Attach(left, Collapsible.Edge.Left);
        Attach(right, Collapsible.Edge.Right);
        Attach(bar, Collapsible.Edge.Bottom);
        Attach(bl, Collapsible.Edge.Left);
        Attach(br, Collapsible.Edge.Right);
        MakeAutoOpen(insp);
        Attach(goal, Collapsible.Edge.Left);

        Label(rt, "Hint", "HUD PoC  -  proxy buttons only report their id", 20, TextAnchor.LowerCenter, new Color(1, 1, 1, 0.6f), new Vector2(0.5f, 0), new Vector2(0, 140), new Vector2(1000, 30));

        // build menus must be drawn above every other panel
        foreach (Transform t in rt) if (t.name.StartsWith("Menu E")) t.SetAsLastSibling();

        EditorUtility.SetDirty(poc);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Dev] HUD PoC built");
    }

    private static RectTransform TopBarPanel(RectTransform rt)
    {
        var bar = Panel("Top Bar", rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(160, 82));
        Frame(bar.gameObject, new Color(0.45f, 0.4f, 0.75f, 0.8f));
        var tb = bar.gameObject.AddComponent<TopBar>();
        tb.bar = bar;

        // clock: day number and a time-of-day bar
        tb.dayLabel = Label(bar, "Day", "Day 1", 24, TextAnchor.UpperLeft, Accent, new Vector2(0, 1), new Vector2(16, -8), new Vector2(140, 30));
        var face = new GameObject("Worker Icon", typeof(RectTransform));
        var fr = (RectTransform)face.transform;
        fr.SetParent(bar, false);
        fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0, 1);
        fr.anchoredPosition = new Vector2(16, -52); fr.sizeDelta = new Vector2(32, 28);
        var fimg = face.AddComponent<Image>(); fimg.sprite = ArtGenerator.LoadSprite("icon_worker"); fimg.preserveAspect = true; fimg.raycastTarget = false;
        tb.workersLabel = Label(bar, "Workers", "0/0", 22, TextAnchor.MiddleLeft, TextCol, new Vector2(0, 1), new Vector2(54, -54), new Vector2(100, 24));
        var pixel = ArtGenerator.LoadSprite("pixel");
        var track = new GameObject("Time Track", typeof(RectTransform));
        var trt = (RectTransform)track.transform;
        trt.SetParent(bar, false);
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0, 1);
        trt.anchoredPosition = new Vector2(16, -42); trt.sizeDelta = new Vector2(132, 10);
        var timg = track.AddComponent<Image>(); timg.sprite = pixel; timg.color = new Color(0, 0, 0, 0.5f);
        var fill = new GameObject("Fill", typeof(RectTransform));
        var frt = (RectTransform)fill.transform;
        frt.SetParent(trt, false);
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        tb.timeFill = fill.AddComponent<Image>();
        tb.timeFill.sprite = pixel; tb.timeFill.type = Image.Type.Filled; tb.timeFill.fillMethod = Image.FillMethod.Horizontal; tb.timeFill.color = Accent;

        // resource list: one inactive entry per resource, shown the first time its stock is above 0
        // (a field of its own with an outline; TopBar resizes it and the bar as entries appear)
        var grt = Panel("Resources", bar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(tb.clockWidth, -tb.margin), new Vector2(100, 52));
        tb.field = grt;
        grt.GetComponent<Image>().color = new Color(0.05f, 0.045f, 0.1f, 0.95f);
        Frame(grt.gameObject, new Color(0.62f, 0.55f, 0.95f, 0.75f));
        var g = grt.gameObject.AddComponent<GridLayoutGroup>();
        g.padding = new RectOffset((int)tb.pad, (int)tb.pad, (int)tb.pad, (int)tb.pad);
        g.cellSize = tb.cell; g.spacing = new Vector2(tb.gap, tb.gap);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = tb.columns;
        g.childAlignment = TextAnchor.UpperLeft;
        grt.gameObject.SetActive(false);

        var list = new System.Collections.Generic.List<TopBar.Entry>();
        foreach (ResourceKind k in System.Enum.GetValues(typeof(ResourceKind)))
        {
            if (k == ResourceKind.None) continue;
            var eg = new GameObject(k.ToString(), typeof(RectTransform));
            var ert = (RectTransform)eg.transform;
            ert.SetParent(grt, false);
            var eimg = eg.AddComponent<Image>(); eimg.sprite = panel; eimg.type = Image.Type.Sliced; eimg.color = ButtonCol;
            var icon = new GameObject("Icon", typeof(RectTransform));
            var irt = (RectTransform)icon.transform;
            irt.SetParent(ert, false);
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0, 0.5f); irt.anchoredPosition = new Vector2(6, 0); irt.sizeDelta = new Vector2(28, 28);
            var iimg = icon.AddComponent<Image>(); iimg.sprite = ArtGenerator.LoadSprite("icon_" + k.ToString().ToLower()); iimg.preserveAspect = true; iimg.raycastTarget = false;
            var amount = Label(ert, "Amount", "0 / 0", 18, TextAnchor.MiddleLeft, TextCol, new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(76, 30));
            eg.SetActive(false);
            list.Add(new TopBar.Entry { kind = k, root = eg, amount = amount });
        }
        tb.entries = list.ToArray();
        EditorUtility.SetDirty(tb);
        return bar;
    }

    private static void Frame(GameObject go, Color col)
    {
        var o = go.AddComponent<Outline>();
        o.effectColor = col; o.effectDistance = new Vector2(2, -2);
    }

    private static RectTransform BuildBar(RectTransform rt)
    {
        var colony = Object.FindObjectOfType<Colony>();
        string[] titles = { "Materials", "Food & Luxury", "Water", "Power" };
        string[] iconNames = { "cat_materials", "cat_food", "cat_water", "cat_power" };
        const float tabW = 104f, gap = 8f;   // square buttons

        // the Fisher Hut belongs to the food layer
        foreach (var b in colony.catalog)
            if (b != null && b.role == BuildingRole.FishCamp && b.category != BuildCategory.Food)
            {
                string path = AssetDatabase.GetAssetPath(b);
                var root = PrefabUtility.LoadPrefabContents(path);
                root.GetComponent<Building>().category = BuildCategory.Food;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }

        var bar = Panel("Build Bar", rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(10 * (tabW + gap) + 12, 124));
        var cat = bar.gameObject.AddComponent<BuildCategoryBar>();
        cat.tabs = new BuildCategoryBar.Tab[4];
        var entries = new System.Collections.Generic.List<BuildCategoryBar.Entry>();

        for (int i = 0; i < 4; i++)
        {
            // tab button
            var id = "E" + (i + 1);
            var go = new GameObject(id + " " + titles[i], typeof(RectTransform));
            var trt = (RectTransform)go.transform;
            trt.SetParent(bar, false);
            trt.anchorMin = trt.anchorMax = new Vector2(0, 0.5f); trt.pivot = new Vector2(0, 0.5f);
            trt.anchoredPosition = new Vector2(10 + i * (tabW + gap), 0); trt.sizeDelta = new Vector2(tabW, tabW);
            var img = go.AddComponent<Image>();
            img.sprite = panel; img.type = Image.Type.Sliced; img.color = ButtonCol;
            var btn = go.AddComponent<UnityEngine.UI.Button>(); btn.targetGraphic = img;
            var cb = btn.colors; cb.highlightedColor = new Color(1.25f, 1.25f, 1.4f); cb.pressedColor = new Color(0.8f, 0.8f, 0.9f); cb.fadeDuration = 0.06f; btn.colors = cb;
            go.AddComponent<UiButtonFx>();
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            var irt = (RectTransform)iconGo.transform;
            irt.SetParent(trt, false);
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 1); irt.anchoredPosition = new Vector2(0, -10); irt.sizeDelta = new Vector2(64, 64);
            var iimg = iconGo.AddComponent<Image>(); iimg.sprite = ArtGenerator.LoadSprite(iconNames[i]); iimg.preserveAspect = true; iimg.raycastTarget = false;
            Label(trt, "Title", titles[i], 15, TextAnchor.MiddleCenter, TextCol, new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(tabW, 20));
            Label(trt, "Id", id, 14, TextAnchor.UpperLeft, Accent, new Vector2(0, 1), new Vector2(8, -5), new Vector2(40, 18));
            UnityEventTools.AddIntPersistentListener(btn.onClick, cat.OnTab, i);

            // menu above the bar
            var list = new System.Collections.Generic.List<Building>();
            foreach (var b in colony.catalog) if (b != null && (int)b.category == i) list.Add(b);
            float w = Mathf.Max(300f, list.Count * 112f + 16f);
            var menu = Panel("Menu " + id + " " + titles[i], rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14 + 124 + 8), new Vector2(w, 136));
            Label(menu, "Title", titles[i].ToUpper(), 16, TextAnchor.UpperLeft, Accent, new Vector2(0, 1), new Vector2(12, -6), new Vector2(300, 22));
            if (list.Count == 0) Label(menu, "Empty", "Nothing to build here yet", 22, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(w, 40));
            for (int k = 0; k < list.Count; k++)
            {
                var b = list[k];
                var bgo = new GameObject(b.displayName, typeof(RectTransform));
                var brt = (RectTransform)bgo.transform;
                brt.SetParent(menu, false);
                brt.anchorMin = brt.anchorMax = new Vector2(0, 0); brt.pivot = new Vector2(0, 0);
                brt.anchoredPosition = new Vector2(10 + k * 112, 8); brt.sizeDelta = new Vector2(104, 104);
                var bimg = bgo.AddComponent<Image>(); bimg.sprite = panel; bimg.type = Image.Type.Sliced; bimg.color = ButtonCol;
                var bbtn = bgo.AddComponent<UnityEngine.UI.Button>(); bbtn.targetGraphic = bimg;
                bgo.AddComponent<UiButtonFx>();
                var pic = new GameObject("Icon", typeof(RectTransform));
                var prt = (RectTransform)pic.transform;
                prt.SetParent(brt, false);
                prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 1); prt.anchoredPosition = new Vector2(0, -6); prt.sizeDelta = new Vector2(90, 52);
                var pimg = pic.AddComponent<Image>(); pimg.sprite = PrimarySprite(b.gameObject); pimg.preserveAspect = true; pimg.raycastTarget = false;
                Label(brt, "Name", b.displayName, 14, TextAnchor.MiddleCenter, TextCol, new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(104, 18));
                var cost = Label(brt, "Cost", b.Cost.ToString(), 12, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.7f), new Vector2(0.5f, 0), new Vector2(0, 7), new Vector2(104, 16));
                UnityEventTools.AddIntPersistentListener(bbtn.onClick, cat.OnBuild, entries.Count);
                entries.Add(new BuildCategoryBar.Entry { building = b, bg = bimg, cost = cost });
            }
            menu.gameObject.SetActive(false);
            cat.tabs[i] = new BuildCategoryBar.Tab { button = btn, bg = img, menu = menu.gameObject };
        }
        // E5..E10 stay as plain proxy buttons (no meaning yet), same square size
        for (int i = 4; i < 10; i++)
            Button(bar, "E" + (i + 1), new Vector2(0, 0.5f), new Vector2(10 + i * (tabW + gap), 0), tabW, tabW, new Vector2(0, 0.5f));
        cat.entries = entries.ToArray();
        EditorUtility.SetDirty(cat);
        return bar;
    }

    private static Sprite PrimarySprite(GameObject prefab)
    {
        Sprite best = null; float area = 0f;
        foreach (var sr in prefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            float a = sr.sprite.rect.width * sr.sprite.rect.height;
            if (a > area) { area = a; best = sr.sprite; }
        }
        return best;
    }

    private static Text inspTitleRef, inspBodyRef;

    /// <summary>The inspector has no handle: it slides in by itself on selection and only has an X.</summary>
    private static void MakeAutoOpen(RectTransform insp)
    {
        var go = new GameObject("Auto Open", typeof(RectTransform));
        go.transform.SetParent(insp, false);
        var slide = go.AddComponent<Collapsible>();
        slide.panel = insp; slide.edge = Collapsible.Edge.Right; slide.remember = false; slide.startCollapsed = true;
        var auto = go.AddComponent<AutoOpenPanel>();
        auto.slide = slide; auto.title = inspTitleRef; auto.body = inspBodyRef;

        var x = new GameObject("Close X", typeof(RectTransform));
        var rt = (RectTransform)x.transform;
        rt.SetParent(insp, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-8, -8); rt.sizeDelta = new Vector2(40, 40);
        var img = x.AddComponent<Image>();
        img.sprite = panel; img.type = Image.Type.Sliced; img.color = new Color(0.5f, 0.2f, 0.28f, 1f);
        var btn = x.AddComponent<UnityEngine.UI.Button>(); btn.targetGraphic = img;
        x.AddComponent<UiButtonFx>();
        Label(rt, "X", "X", 24, TextAnchor.MiddleCenter, TextCol, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
        UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(auto.Close));

        // workplace controls: workers - / value / +, priority - / value / +
        var ctl = new GameObject("Workplace Controls", typeof(RectTransform));
        var crt = (RectTransform)ctl.transform;
        crt.SetParent(insp, false);
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0, 0);
        crt.anchoredPosition = new Vector2(12, 80); crt.sizeDelta = new Vector2(356, 120);
        auto.controls = ctl;
        auto.workersValue = StepRow(crt, "Workers", 62, auto.ChangeWorkers);
        auto.priorityValue = StepRow(crt, "Priority (5 = first)", 0, auto.ChangePriority);
        EditorUtility.SetDirty(slide); EditorUtility.SetDirty(auto);
    }

    private static Text StepRow(RectTransform parent, string caption, float y, UnityAction<int> change)
    {
        var cap = Label(parent, caption + " Caption", caption, 16, TextAnchor.LowerLeft, Accent, new Vector2(0, 0), new Vector2(0, y + 44), new Vector2(340, 20));
        StepButton(parent, "-", new Vector2(0, y), change, -1);
        var val = Label(parent, caption + " Value", "0", 26, TextAnchor.MiddleCenter, TextCol, new Vector2(0, 0), new Vector2(110, y), new Vector2(110, 40));
        StepButton(parent, "+", new Vector2(220, y), change, 1);
        return val;
    }

    private static void StepButton(RectTransform parent, string text, Vector2 pos, UnityAction<int> change, int arg)
    {
        var go = new GameObject("Step " + text, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(110, 40);
        var img = go.AddComponent<Image>();
        img.sprite = panel; img.type = Image.Type.Sliced; img.color = ButtonCol;
        var btn = go.AddComponent<UnityEngine.UI.Button>(); btn.targetGraphic = img;
        go.AddComponent<UiButtonFx>();
        Label(rt, "Text", text, 28, TextAnchor.MiddleCenter, TextCol, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 40));
        UnityEventTools.AddIntPersistentListener(btn.onClick, change, arg);
    }

    /// <summary>Adds the slide-away handle on the panel's inner side (the side facing the screen centre).</summary>
    private static void Attach(RectTransform panelRt, Collapsible.Edge edge, float along = 0.5f)
    {
        const float strip = 28f;
        bool horizontal = edge == Collapsible.Edge.Left || edge == Collapsible.Edge.Right;
        float len = Mathf.Min(horizontal ? panelRt.sizeDelta.y : panelRt.sizeDelta.x, 120f);
        var go = new GameObject("Slide Handle", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(panelRt, false);
        switch (edge)
        {
            case Collapsible.Edge.Left: rt.anchorMin = rt.anchorMax = new Vector2(1, along); rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(strip, len); break;
            case Collapsible.Edge.Right: rt.anchorMin = rt.anchorMax = new Vector2(0, along); rt.pivot = new Vector2(1, 0.5f); rt.sizeDelta = new Vector2(strip, len); break;
            case Collapsible.Edge.Top: rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0); rt.pivot = new Vector2(0.5f, 1); rt.sizeDelta = new Vector2(len, strip); break;
            default: rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(len, strip); break;
        }
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = panel; img.type = Image.Type.Sliced; img.color = new Color(0.3f, 0.27f, 0.5f, 0.95f);
        var arrow = Label(rt, "Arrow", "<", 22, TextAnchor.MiddleCenter, Accent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(strip, Mathf.Min(len, strip)));
        var col = go.AddComponent<Collapsible>();
        col.panel = panelRt; col.edge = edge; col.arrow = arrow;
        EditorUtility.SetDirty(col);
    }

    // ---------------------------------------------------------------- building blocks
    private static RectTransform Panel(string name, RectTransform parent, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = panel; img.type = Image.Type.Sliced; img.color = PanelCol;
        return rt;
    }

    private static Text Label(RectTransform parent, string name, string text, int size, TextAnchor align, Color col, Vector2 anchor, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = sz;
        var t = go.AddComponent<Text>();
        t.font = UiFont.Get(); t.text = text; t.fontSize = size; t.alignment = align; t.color = col;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var o = go.AddComponent<Outline>();
        o.effectColor = new Color(0.04f, 0.03f, 0.09f, 0.9f); o.effectDistance = new Vector2(1.5f, -1.5f);
        return t;
    }

    /// <summary>A horizontal run of buttons id "<letter>1".."<letter>n".</summary>
    private static void Row(RectTransform parent, string letter, int count, Vector2 start, float w, float h, float gap, Vector2 anchor, bool fromRight = false, bool bottomAligned = false)
    {
        for (int i = 0; i < count; i++)
        {
            float x = start.x + (fromRight ? -(i * (w + gap)) : i * (w + gap));
            Button(parent, letter + (fromRight ? count - i : i + 1), anchor, new Vector2(x, start.y), w, h, bottomAligned ? new Vector2(fromRight ? 1 : 0, 0) : new Vector2(fromRight ? 1 : 0, 0.5f));
        }
    }

    private static void Column(RectTransform parent, string letter, int count, float w, float h, float gap)
    {
        for (int i = 0; i < count; i++)
            Button(parent, letter + (i + 1), new Vector2(0.5f, 1), new Vector2(0, -6 - i * (h + gap)), w, h, new Vector2(0.5f, 1));
    }

    private static void Button(RectTransform parent, string id, Vector2 anchor, Vector2 pos, float w, float h, Vector2 pivot)
    {
        var go = new GameObject(id, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(w, h);
        var img = go.AddComponent<Image>();
        img.sprite = panel; img.type = Image.Type.Sliced; img.color = ButtonCol;
        var btn = go.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.4f); cb.pressedColor = new Color(0.8f, 0.8f, 0.9f); cb.fadeDuration = 0.06f;
        btn.colors = cb;
        go.AddComponent<UiButtonFx>();
        Label(rt, "Id", id, h < 50 ? 20 : 24, TextAnchor.MiddleCenter, TextCol, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
        UnityEventTools.AddStringPersistentListener(btn.onClick, new UnityAction<string>(poc.OnProxy), id);
    }
}
