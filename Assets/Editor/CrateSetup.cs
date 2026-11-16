using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Menu: BuildSim/Setup/8 Add Pearl Crates.
/// Generates the crate art, creates Assets/Prefabs/Crate.prefab, adds a "Crates" object (CrateManager) under the Map in the
/// Level scene and a crate panel (one real button per product that can pay) to the HUD. Only ADDS things, hand edits stay.
/// Run it again after adding a product to the Products tag list to get its button.
/// </summary>
public static class CrateSetup
{
    private const string PrefabPath = "Assets/Prefabs/Crate.prefab";

    [MenuItem("BuildSim/Setup/8 Add Pearl Crates")]
    public static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
    }

    [MenuItem("BuildSim/Dev/Add Pearl Crates (no prompt)")]
    public static void Silent()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Dev] Crate setup aborted: the open scene has unsaved changes."); return; }
        Run();
    }

    private static void Run()
    {
        ArtGenerator.Run();
        AssetDatabase.Refresh();
        var prefab = BuildPrefab();

        var scene = EditorSceneManager.OpenScene(LevelSceneBuilder.ScenePath, OpenSceneMode.Single);
        var gen = Object.FindObjectOfType<MapGenerator>();
        var grid = Object.FindObjectOfType<GridManager>();
        var hud = Object.FindObjectOfType<HUD>();
        if (gen == null || grid == null || hud == null) { Debug.LogError("CrateSetup: Level scene needs a MapGenerator, GridManager and HUD."); return; }

        // ---- manager under the Map object
        var mgr = Object.FindObjectOfType<CrateManager>();
        if (mgr == null)
        {
            var go = new GameObject("Crates");
            go.transform.SetParent(gen.transform.parent != null ? gen.transform.parent : gen.transform, false);
            mgr = go.AddComponent<CrateManager>();
        }
        mgr.grid = grid;
        mgr.prefab = prefab.GetComponent<Crate>();
        mgr.closedSprites = new Sprite[3];
        mgr.openSprites = new Sprite[3];
        for (int i = 0; i < 3; i++)
        {
            mgr.closedSprites[i] = ArtGenerator.LoadSprite("crate_closed_" + i);
            mgr.openSprites[i] = ArtGenerator.LoadSprite("crate_open_" + i);
        }
        EditorUtility.SetDirty(mgr);
        gen.crates = mgr;
        EditorUtility.SetDirty(gen);

        BuildPanel(hud);

        gen.Generate();   // same seed as before, now with crates
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Dev] CrateSetup: crates added (" + (gen.Current != null ? gen.Current.crates.Count : 0) + " on the current map)");
    }

    // ---------------------------------------------------------------- prefab
    private static GameObject BuildPrefab()
    {
        Directory.CreateDirectory("Assets/Prefabs");
        var root = new GameObject("Crate");
        var crate = root.AddComponent<Crate>();
        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(root.transform, false);
        var sr = bodyGo.AddComponent<SpriteRenderer>();
        sr.sprite = ArtGenerator.LoadSprite("crate_closed_0");
        sr.sortingLayerName = Layers.Objects;
        var lit = PrefabSetup.LitMaterial;
        if (lit != null) sr.sharedMaterial = lit;
        crate.body = sr;
        var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    // ---------------------------------------------------------------- panel
    private static void BuildPanel(HUD hud)
    {
        var canvas = hud.transform;
        foreach (string n in new[] { "CratePanel", "CratePanelController" })
        {
            var old = canvas.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        var insp = canvas.Find("InspectorPanel") as RectTransform;
        if (insp == null) { Debug.LogError("CrateSetup: HUD has no InspectorPanel to copy the style from."); return; }

        var panel = new GameObject("CratePanel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvas, false);
        panel.anchorMin = insp.anchorMin; panel.anchorMax = insp.anchorMax; panel.pivot = insp.pivot;
        panel.anchoredPosition = insp.anchoredPosition; panel.sizeDelta = new Vector2(insp.sizeDelta.x, 300f);
        var srcImg = insp.GetComponent<Image>();
        var img = panel.gameObject.AddComponent<Image>();
        img.sprite = srcImg.sprite; img.type = srcImg.type; img.color = srcImg.color;

        Text title = CloneText(insp, "Title", panel);
        Text sub = CloneText(insp, "Subtitle", panel);
        Text body = CloneText(insp, "Body", panel);
        body.rectTransform.offsetMax = new Vector2(body.rectTransform.offsetMax.x, body.rectTransform.offsetMax.y);

        // the controller sits on its own always-active object (the panel itself is switched off when nothing is selected)
        var ctl = new GameObject("CratePanelController");
        ctl.transform.SetParent(canvas, false);
        var cp = ctl.AddComponent<CratePanel>();
        cp.root = panel.gameObject; cp.title = title; cp.sub = sub; cp.body = body;

        var products = Products.Accepting(ProductTag.BuildingMaterial);
        var list = new List<CratePanel.PayButton>();
        for (int i = 0; i < products.Count; i++)
        {
            var bgo = new GameObject("Pay " + ResourceInfo.Name(products[i]), typeof(RectTransform));
            var brt = (RectTransform)bgo.transform;
            brt.SetParent(panel, false);
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0, 14 + i * 52);
            brt.sizeDelta = new Vector2(panel.sizeDelta.x - 32, 44);
            var bimg = bgo.AddComponent<Image>();
            bimg.sprite = srcImg.sprite; bimg.type = Image.Type.Sliced; bimg.color = new Color(0.18f, 0.32f, 0.26f, 0.97f);
            var btn = bgo.AddComponent<Button>(); btn.targetGraphic = bimg;
            bgo.AddComponent<UiButtonFx>();
            var label = CloneText(insp, "Body", brt);
            label.name = "Label";
            label.alignment = TextAnchor.MiddleCenter;
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            UnityEventTools.AddIntPersistentListener(btn.onClick, cp.OnPay, (int)products[i]);
            list.Add(new CratePanel.PayButton { product = products[i], button = btn, label = label });
        }
        cp.buttons = list.ToArray();
        panel.gameObject.SetActive(false);
        EditorUtility.SetDirty(cp);
    }

    private static Text CloneText(RectTransform inspector, string name, RectTransform parent)
    {
        var src = inspector.Find(name);
        var clone = Object.Instantiate(src.gameObject, parent, false);
        clone.name = name;
        clone.SetActive(true);
        return clone.GetComponent<Text>();
    }
}
