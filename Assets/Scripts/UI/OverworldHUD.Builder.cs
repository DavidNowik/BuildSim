#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Editor-only: builds the overworld HUD hierarchy as real GameObjects (Inspector button "Rebuild UI").</summary>
public partial class OverworldHUD
{
    private static readonly Color PanelCol = new Color(0.09f, 0.11f, 0.17f, 0.93f);
    private static readonly Color ButtonCol = new Color(0.18f, 0.21f, 0.32f, 0.96f);
    private static readonly Color GoodCol = new Color(0.55f, 0.92f, 0.55f);

    public void BuildUI(Sprite[] previewIcons)
    {
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);

        BuildTopBar();
        BuildInfoPanel();
        BuildModal(previewIcons);
        BuildBanner();
        BuildToastTemplate();
        helpText = AddText(Rect(transform, "HelpText"), "HelpText",
            "Drag / WASD  pan      Wheel  zoom      Click a land to inspect      Settlement sites unlock later",
            17, TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.75f));
        Anchor(helpText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -72), new Vector2(900, 28));
        EditorUtility.SetDirty(this);
    }

    // ------------------------------------------------------------ helpers
    private static RectTransform Rect(Transform parent, string name)
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

    private Image Img(RectTransform rt, Sprite s, Color c, bool sliced)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = s; img.color = c;
        if (sliced) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
        img.raycastTarget = false;
        return img;
    }

    private static Text AddText(RectTransform host, string name, string value, int size, TextAnchor align, Color color, bool outline = true)
    {
        // when host was just created for the text itself, reuse it; otherwise add a child
        Text t = host.gameObject.GetComponent<Text>();
        RectTransform rt = host;
        if (host.name != name || t != null) { rt = Rect(host, name); }
        t = rt.gameObject.AddComponent<Text>();
        t.font = UiFont.Get(); t.text = value; t.fontSize = size; t.alignment = align; t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (outline)
        {
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.04f, 0.04f, 0.09f, 0.9f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }
        return t;
    }

    private RectTransform Panel(string name, Transform parent, Color tint)
    {
        var rt = Rect(parent, name);
        Img(rt, panelSprite, tint, true).raycastTarget = true;
        return rt;
    }

    private Button MakeButton(RectTransform rt, Image bg)
    {
        bg.raycastTarget = true;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        var cb = b.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.25f, 1.25f, 1.35f); cb.pressedColor = new Color(0.8f, 0.8f, 0.9f);
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.06f;
        b.colors = cb;
        rt.gameObject.AddComponent<UiButtonFx>();
        return b;
    }

    private Text Label(Transform parent, string name, string text, int size, TextAnchor a, Color c, Vector2 pos, Vector2 sz, Vector2 anchor)
    {
        var rt = Rect(parent, name);
        var t = AddText(rt, name, text, size, a, c);
        Anchor(rt, anchor, anchor, anchor, pos, sz);
        return t;
    }

    // ------------------------------------------------------------ top bar
    private void BuildTopBar()
    {
        var bar = Panel("TopBar", transform, PanelCol);
        Anchor(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 60));
        var left = new Vector2(0, 0.5f);

        Label(bar, "LandsLabel", "Lands", 20, TextAnchor.MiddleLeft, DimCol, new Vector2(24, 0), new Vector2(80, 36), left);
        landsText = Label(bar, "LandsValue", "1", 28, TextAnchor.MiddleLeft, TextCol, new Vector2(100, 0), new Vector2(70, 40), left);
        Label(bar, "ReachLabel", "Reach", 20, TextAnchor.MiddleLeft, DimCol, new Vector2(190, 0), new Vector2(80, 36), left);
        reachText = Label(bar, "ReachValue", "0", 28, TextAnchor.MiddleLeft, TextCol, new Vector2(276, 0), new Vector2(70, 40), left);

        var pi = Rect(bar, "PearlIcon");
        Anchor(pi, left, left, left, new Vector2(366, 0), new Vector2(34, 34));
        var pimg = Img(pi, pearlSprite, Color.white, false); pimg.preserveAspect = true;
        var pt = Label(bar, "PearlCount", "0", 28, TextAnchor.MiddleLeft, TextCol, new Vector2(410, 0), new Vector2(90, 40), left);
        pearlsLabel = pt.gameObject.AddComponent<CounterLabel>();
        pearlsLabel.text = pt; pearlsLabel.baseColor = TextCol;

        // tide bar (centre): moon + bar + label
        tideRoot = Panel("TideBar", bar, new Color(0.07f, 0.09f, 0.15f, 0.95f));
        Anchor(tideRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 46));
        var mi = Rect(tideRoot, "Moon");
        Anchor(mi, left, left, left, new Vector2(10, 0), new Vector2(34, 34));
        moonIcon = Img(mi, moonSprites != null && moonSprites.Length > 0 ? moonSprites[0] : null, Color.white, false);
        moonIcon.preserveAspect = true;
        var bg = Rect(tideRoot, "BarBackground");
        Anchor(bg, left, left, left, new Vector2(56, 0), new Vector2(290, 14));
        Img(bg, barSprite, new Color(0, 0, 0, 0.55f), true);
        var fill = Rect(bg, "Fill");
        Anchor(fill, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        tideFill = Img(fill, pixelSprite, new Color(0.25f, 0.62f, 0.78f), false);
        tideFill.type = Image.Type.Filled; tideFill.fillMethod = Image.FillMethod.Horizontal; tideFill.fillAmount = 0f;
        tideLabel = Label(tideRoot, "TideLabel", "Tide in 10", 20, TextAnchor.MiddleLeft, new Color(0.75f, 0.85f, 0.95f), new Vector2(358, 0), new Vector2(155, 36), left);

        cycleText = Label(bar, "CycleLabel", "Tide 1", 24, TextAnchor.MiddleRight, AccentCol, new Vector2(-24, 0), new Vector2(160, 36), new Vector2(1, 0.5f));
    }

    // ------------------------------------------------------------ info panel
    private void BuildInfoPanel()
    {
        infoPanel = Panel("InfoPanel", transform, PanelCol);
        Anchor(infoPanel, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 16), new Vector2(350, 290));
        var tl = new Vector2(0, 1);
        infoTitle = Label(infoPanel, "Title", "", 28, TextAnchor.UpperLeft, TextCol, new Vector2(16, -12), new Vector2(318, 34), tl);
        infoSub = Label(infoPanel, "Subtitle", "", 19, TextAnchor.UpperLeft, AccentCol, new Vector2(16, -46), new Vector2(318, 26), tl);
        infoBody = Label(infoPanel, "Body", "", 20, TextAnchor.UpperLeft, TextCol, new Vector2(16, -80), new Vector2(318, 130), tl);
        infoBody.horizontalOverflow = HorizontalWrapMode.Wrap;

        var btn = Rect(infoPanel, "ActionButton");
        Anchor(btn, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(300, 48));
        var img = Img(btn, panelSprite, new Color(0.45f, 0.36f, 0.12f, 0.98f), true);
        var b = MakeButton(btn, img);
        infoButtonLabel = AddText(Rect(btn, "Label"), "Label", "Discover", 22, TextAnchor.MiddleCenter, TextCol);
        Anchor(infoButtonLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UnityEventTools.AddPersistentListener(b.onClick, new UnityAction(OnInfoButton));
        infoButton = btn.gameObject;
        infoPanel.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ modifier choice (3 cards)
    private void BuildModal(Sprite[] previewIcons)
    {
        var go = Rect(transform, "ModifierChoice");
        Anchor(go, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var dim = Img(go, pixelSprite, new Color(0.04f, 0.02f, 0.12f, 0.72f), false);
        dim.raycastTarget = true;
        modal = go.gameObject;

        var title = AddText(Rect(go, "Title"), "Title", "New land discovered!", 40, TextAnchor.MiddleCenter, TextCol);
        Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(900, 60));
        modalSub = AddText(Rect(go, "Subtitle"), "Subtitle", "", 22, TextAnchor.MiddleCenter, DimCol);
        Anchor(modalSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(1100, 40));

        var list = new List<Card>();
        for (int i = 0; i < 3; i++)
        {
            var card = Rect(go, "Card " + (i + 1));
            Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 310, -20), new Vector2(280, 340));
            var bg = Img(card, panelSprite, ButtonCol, true);
            var btn = MakeButton(card, bg);
            var ic = Rect(card, "Icon");
            Anchor(ic, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(110, 110));
            var iimg = Img(ic, previewIcons != null && i < previewIcons.Length ? previewIcons[i] : null, Color.white, false);
            iimg.preserveAspect = true;
            var t = AddText(Rect(card, "Name"), "Name", "Modifier", 26, TextAnchor.MiddleCenter, TextCol);
            Anchor(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(260, 36));
            var d = AddText(Rect(card, "Description"), "Description", "", 19, TextAnchor.UpperCenter, DimCol);
            d.horizontalOverflow = HorizontalWrapMode.Wrap;
            Anchor(d.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(240, 90));
            var c = AddText(Rect(card, "Cost"), "Cost", "+1 moon", 20, TextAnchor.MiddleCenter, AccentCol);
            Anchor(c.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(240, 30));
            UnityEventTools.AddIntPersistentListener(btn.onClick, OnCard, i);
            list.Add(new Card { root = card.gameObject, bg = bg, icon = iimg, title = t, desc = d, cost = c });
        }
        cards = list.ToArray();
        go.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ banner + toast
    private void BuildBanner()
    {
        var go = Rect(transform, "FloodBanner");
        Anchor(go, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        banner = go.gameObject.AddComponent<CanvasGroup>();
        banner.blocksRaycasts = false;
        bannerTitle = AddText(Rect(go, "Title"), "Title", "THE SEA TAKES BACK WHAT IT LENT", 48, TextAnchor.MiddleCenter, new Color(0.9f, 0.95f, 0.97f));
        Anchor(bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1500, 80));
        bannerSub = AddText(Rect(go, "Subtitle"), "Subtitle", "", 26, TextAnchor.MiddleCenter, new Color(0.66f, 0.83f, 0.9f));
        Anchor(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1500, 44));
        bannerPearls = AddText(Rect(go, "Pearls"), "Pearls", "", 30, TextAnchor.MiddleCenter, AccentCol);
        Anchor(bannerPearls.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -75), new Vector2(1000, 50));
        go.gameObject.SetActive(false);
    }

    private void BuildToastTemplate()
    {
        var toast = Panel("ToastTemplate", transform, new Color(0.09f, 0.11f, 0.17f, 0.94f));
        toast.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        Anchor(toast, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 110), new Vector2(300, 40));
        var t = AddText(Rect(toast, "Text"), "Text", "Toast", 21, TextAnchor.MiddleLeft, TextCol);
        Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        t.rectTransform.offsetMin = new Vector2(16, 0); t.rectTransform.offsetMax = new Vector2(-12, 0);
        toast.gameObject.SetActive(false);
        toastTemplate = toast;
    }
}
#endif
