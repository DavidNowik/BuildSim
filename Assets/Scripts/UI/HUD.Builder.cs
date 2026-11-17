#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Editor-only: builds the HUD hierarchy as real GameObjects in the scene (Inspector button "Rebuild UI").
/// Rebuilding deletes and recreates everything under the HUD canvas, so hand edits are lost on rebuild;
/// after you are happy with the layout just stop pressing the button.
/// </summary>
public partial class HUD
{
    public void BuildUI(Colony colony)
    {
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);

        BuildTopBar();
        BuildGoalPanel();
        BuildInspectorPanel();
        BuildTooltipObject();
        BuildHelpText();
        // (no build bar here any more: buildings live in the build layer tabs of the HUD PoC; recruiting is a debug command)
        BuildTemplates();
        EditorUtility.SetDirty(this);
    }

    // ------------------------------------------------------------ helpers
    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    private static Image AddImage(RectTransform rt, Sprite s, Color c, bool sliced)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = s;
        img.color = c;
        if (sliced) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
        img.raycastTarget = false;
        return img;
    }

    private static Text AddText(RectTransform parent, string name, string value, int size, TextAnchor align, Color color, bool outline = true)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = UiFont.Get();
        t.text = value;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (outline)
        {
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.04f, 0.04f, 0.09f, 0.9f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }
        return t;
    }

    private RectTransform MakePanel(string name, Transform parent, Color tint)
    {
        var rt = NewRect(name, parent);
        var img = AddImage(rt, panelSprite, tint, true);
        img.raycastTarget = true; // panels block clicks from reaching the world
        return rt;
    }

    private Button MakeButton(RectTransform rt, Image bg)
    {
        bg.raycastTarget = true;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        var cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.35f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.9f);
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.06f;
        b.colors = cb;
        rt.gameObject.AddComponent<UiButtonFx>();
        return b;
    }

    // ------------------------------------------------------------ top bar
    private void BuildTopBar()
    {
        var bar = MakePanel("TopBar", transform, PanelCol);
        Anchor(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(0, 60));

        stoneLabel = MakeCounter(bar, "Stone", stoneSprite, 28, out stoneIcon);
        woodLabel = MakeCounter(bar, "Wood", woodSprite, 176, out woodIcon);
        fishLabel = MakeCounter(bar, "Fish", fishSprite, 324, out fishIcon);

        var pi = NewRect("PopulationIcon", bar);
        Anchor(pi, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(472, 0), new Vector2(36, 36));
        var pimg = AddImage(pi, popSprite, Color.white, false); pimg.preserveAspect = true;
        popText = AddText(bar, "PopulationCount", "0 / 0", 28, TextAnchor.MiddleLeft, TextCol);
        Anchor(popText.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(518, 0), new Vector2(120, 40));

        dayText = AddText(bar, "DayLabel", "Day 1", 24, TextAnchor.MiddleLeft, AccentCol);
        Anchor(dayText.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(668, 8), new Vector2(120, 30));
        var bg = NewRect("DayBarBackground", bar);
        Anchor(bg, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(668, -14), new Vector2(112, 8));
        AddImage(bg, barSprite, new Color(0f, 0f, 0f, 0.5f), true);
        var fill = NewRect("DayBar", bg);
        Anchor(fill, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        dayBar = AddImage(fill, pixelSprite, Color.white, false);
        var mk = NewRect("DayMarker", bg);
        Anchor(mk, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 14));
        dayMarker = AddImage(mk, pixelSprite, Color.white, false);

        string[] labels = { "II", "1x", "2x", "4x" };
        var imgs = new List<Image>();
        for (int i = 0; i < 4; i++)
        {
            int speed = GameClock.Speeds[i];
            var rt = NewRect("Speed " + labels[i], bar);
            Anchor(rt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16 - (3 - i) * 66, 0), new Vector2(58, 40));
            var img = AddImage(rt, panelSprite, ButtonCol, true);
            var btn = MakeButton(rt, img);
            var t = AddText(rt, "Label", labels[i], 22, TextAnchor.MiddleCenter, TextCol);
            Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UnityEventTools.AddIntPersistentListener(btn.onClick, OnSpeedButton, speed);
            imgs.Add(img);
        }
        speedImages = imgs.ToArray();

        // back to the overworld (also Tab)
        var wm = NewRect("World Map Button", bar);
        Anchor(wm, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-290, 0), new Vector2(150, 40));
        var wmImg = AddImage(wm, panelSprite, ButtonCol, true);
        var wmBtn = MakeButton(wm, wmImg);
        var wmText = AddText(wm, "Label", "World map (Tab)", 20, TextAnchor.MiddleCenter, TextCol);
        Anchor(wmText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UnityEventTools.AddPersistentListener(wmBtn.onClick, new UnityAction(OnWorldMapButton));
    }

    private CounterLabel MakeCounter(RectTransform bar, string name, Sprite icon, float x, out RectTransform iconRt)
    {
        var ic = NewRect(name + "Icon", bar);
        Anchor(ic, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(36, 36));
        var img = AddImage(ic, icon, Color.white, false); img.preserveAspect = true;
        iconRt = ic;

        var t = AddText(bar, name + "Count", "0", 28, TextAnchor.MiddleLeft, TextCol);
        Anchor(t.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x + 46, 0), new Vector2(100, 40));
        var c = t.gameObject.AddComponent<CounterLabel>();
        c.text = t;
        c.baseColor = TextCol;
        return c;
    }

    // ------------------------------------------------------------ goal panel
    private void BuildGoalPanel()
    {
        var p = MakePanel("GoalPanel", transform, PanelCol);
        Anchor(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -72), new Vector2(520, 70));

        var head = AddText(p, "Heading", "GOAL", 16, TextAnchor.UpperLeft, AccentCol);
        Anchor(head.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -8), new Vector2(80, 20));
        goalText = AddText(p, "Title", "", 21, TextAnchor.UpperLeft, TextCol);
        Anchor(goalText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -26), new Vector2(490, 26));
        goalReward = AddText(p, "Reward", "", 16, TextAnchor.UpperRight, GoodCol);
        Anchor(goalReward.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -8), new Vector2(300, 20));

        var bg = NewRect("BarBackground", p);
        Anchor(bg, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(-28, 10));
        AddImage(bg, barSprite, new Color(0, 0, 0, 0.55f), true);
        var f = NewRect("Fill", bg);
        Anchor(f, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        goalFill = AddImage(f, pixelSprite, AccentCol, false);
        goalFill.type = Image.Type.Filled;
        goalFill.fillMethod = Image.FillMethod.Horizontal;
    }

    // ------------------------------------------------------------ build bar
    private void BuildBar(Colony colony)
    {
        var list = new List<BarButton>();
        int buildCount = colony.catalog.Count;
        int count = buildCount + 3;
        const float bw = 156f, bh = 94f, gap = 10f, group = 22f;
        float total = count * bw + (count - 1) * gap + group + 24f;

        var bar = MakePanel("BuildBar", transform, PanelCol);
        Anchor(bar, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(total, bh + 20));

        float x = 12f;
        int key = 0;
        for (int i = 0; i < buildCount; i++, key++)
        {
            var b = colony.catalog[i];
            list.Add(AddBarButton(bar, list.Count, x, bw, bh, b, ResourceKind.None, KeyCode.Alpha1 + key, key + 1, PrimarySprite(b.gameObject), b.displayName, b.Cost.ToString()));
            x += bw + gap;
        }
        x += group;

        Sprite head = null;
        if (colony.workerPrefab != null)
        {
            var sr = colony.workerPrefab.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null) head = sr.sprite;
        }
        list.Add(AddBarButton(bar, list.Count, x, bw, bh, null, ResourceKind.Stone, KeyCode.Alpha1 + key, key + 1, head, "Recruit Miner", Colony.RecruitCost(ResourceKind.Stone).ToString()));
        x += bw + gap; key++;
        list.Add(AddBarButton(bar, list.Count, x, bw, bh, null, ResourceKind.Wood, KeyCode.Alpha1 + key, key + 1, head, "Recruit Woodcutter", Colony.RecruitCost(ResourceKind.Wood).ToString()));
        x += bw + gap; key++;
        list.Add(AddBarButton(bar, list.Count, x, bw, bh, null, ResourceKind.Fish, KeyCode.Alpha1 + key, key + 1, head, "Recruit Fisher", Colony.RecruitCost(ResourceKind.Fish).ToString()));

        barButtons = list.ToArray();
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

    private BarButton AddBarButton(RectTransform bar, int index, float x, float w, float h, Building building, ResourceKind recruit, KeyCode key, int keyNumber, Sprite icon, string title, string cost)
    {
        var rt = NewRect(title, bar);
        Anchor(rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(w, h));
        var bg = AddImage(rt, panelSprite, ButtonCol, true);
        var btn = MakeButton(rt, bg);

        var ic = NewRect("Icon", rt);
        Anchor(ic, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(w - 28, 38));
        var iimg = AddImage(ic, icon, Color.white, false);
        iimg.preserveAspect = true;

        var name = AddText(rt, "Name", title, 17, TextAnchor.MiddleCenter, TextCol);
        Anchor(name.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(w, 22));
        var cst = AddText(rt, "Cost", cost, 15, TextAnchor.MiddleCenter, DimCol);
        Anchor(cst.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(w, 20));
        var k = AddText(rt, "HotKey", keyNumber.ToString(), 15, TextAnchor.UpperLeft, AccentCol);
        Anchor(k.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -5), new Vector2(24, 20));

        UnityEventTools.AddIntPersistentListener(btn.onClick, OnBarButton, index);
        return new BarButton { rt = rt, bg = bg, icon = iimg, name = name, cost = cst, key = k, building = building, recruit = recruit, hotkey = key };
    }

    // ------------------------------------------------------------ inspector
    private void BuildInspectorPanel()
    {
        inspector = MakePanel("InspectorPanel", transform, PanelCol);
        Anchor(inspector, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, -20), new Vector2(350, 300));

        inspTitle = AddText(inspector, "Title", "", 28, TextAnchor.UpperLeft, TextCol);
        Anchor(inspTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(318, 34));
        inspSub = AddText(inspector, "Subtitle", "", 19, TextAnchor.UpperLeft, AccentCol);
        Anchor(inspSub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -46), new Vector2(318, 26));
        inspBody = AddText(inspector, "Body", "", 20, TextAnchor.UpperLeft, TextCol);
        inspBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        Anchor(inspBody.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -80), new Vector2(318, 130));

        inspBarLabel = AddText(inspector, "EnergyLabel", "Energy", 16, TextAnchor.LowerLeft, DimCol);
        Anchor(inspBarLabel.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 36), new Vector2(120, 20));
        var bg = NewRect("EnergyBarBackground", inspector);
        Anchor(bg, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(-32, 14));
        inspBarBg = AddImage(bg, barSprite, new Color(0, 0, 0, 0.55f), true);
        var f = NewRect("Fill", bg);
        Anchor(f, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        inspBarFill = AddImage(f, pixelSprite, GoodCol, false);
        inspBarFill.type = Image.Type.Filled;
        inspBarFill.fillMethod = Image.FillMethod.Horizontal;

        var db = NewRect("DemolishButton", inspector);
        Anchor(db, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(300, 44));
        var dimg = AddImage(db, panelSprite, new Color(0.45f, 0.18f, 0.18f, 0.97f), true);
        var dbtn = MakeButton(db, dimg);
        demolishLabel = AddText(db, "Label", "Demolish (Del)", 20, TextAnchor.MiddleCenter, TextCol);
        Anchor(demolishLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UnityEventTools.AddPersistentListener(dbtn.onClick, new UnityAction(OnDemolishButton));
        demolishButton = db.gameObject;

        inspector.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ tooltip / help
    private void BuildTooltipObject()
    {
        tooltip = NewRect("Tooltip", transform);
        var img = AddImage(tooltip, panelSprite, new Color(0.07f, 0.08f, 0.13f, 0.95f), true);
        img.raycastTarget = false;
        tooltipText = AddText(tooltip, "Text", "", 19, TextAnchor.MiddleCenter, TextCol);
        Anchor(tooltipText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        tooltip.pivot = new Vector2(0, 1);
        tooltip.sizeDelta = new Vector2(120, 34);
        tooltip.gameObject.SetActive(false);
    }

    private void BuildHelpText()
    {
        helpText = AddText(GetComponent<RectTransform>(), "HelpText",
            "WASD / middle-drag   pan\nMouse wheel   zoom\n1-5   build / recruit\nClick   inspect      Del   demolish\nSpace   pause     + / -   speed\nF5   save      F9   load      M   mute      H   hide this",
            17, TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.85f));
        Anchor(helpText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -74), new Vector2(420, 150));
        helpText.lineSpacing = 1.1f;
    }

    // ------------------------------------------------------------ templates (inactive, cloned at runtime)
    private void BuildTemplates()
    {
        var toast = MakePanel("ToastTemplate", transform, new Color(0.09f, 0.11f, 0.17f, 0.94f));
        toast.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        Anchor(toast, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 150), new Vector2(300, 40));
        var t = AddText(toast, "Text", "Toast", 21, TextAnchor.MiddleLeft, TextCol);
        Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        t.rectTransform.offsetMin = new Vector2(16, 0);
        t.rectTransform.offsetMax = new Vector2(-12, 0);
        var stripe = NewRect("stripe", toast);
        Anchor(stripe, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(4, -6));
        AddImage(stripe, pixelSprite, AccentCol, false);
        toast.gameObject.SetActive(false);
        toastTemplate = toast;

        var fly = NewRect("FlyIconTemplate", transform);
        fly.sizeDelta = new Vector2(30, 30);
        var fi = AddImage(fly, stoneSprite, Color.white, false);
        fi.preserveAspect = true;
        fly.gameObject.SetActive(false);
        flyTemplate = fly;
    }
}
#endif
