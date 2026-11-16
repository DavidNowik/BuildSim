using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum ToastKind { Info, Good, Warning }

public static class UiFont
{
    private static Font font;
    public static Font Get()
    {
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
        }
        return font;
    }
}

/// <summary>
/// The game HUD. The whole hierarchy (top bar, goal, build bar, inspector, tooltip, templates) is real
/// GameObjects in the scene under this component's Canvas. It is created by the editor button
/// "Rebuild UI" on this component (see HUD.Builder.cs) and can then be edited freely by hand.
/// This runtime part only updates the existing objects.
/// </summary>
public partial class HUD : MonoBehaviour
{
    public static HUD Instance;

    // palette
    private static readonly Color PanelCol = new Color(0.09f, 0.11f, 0.17f, 0.93f);
    private static readonly Color ButtonCol = new Color(0.18f, 0.21f, 0.32f, 0.96f);
    private static readonly Color TextCol = new Color(0.95f, 0.96f, 1f);
    private static readonly Color DimCol = new Color(0.66f, 0.71f, 0.84f);
    private static readonly Color AccentCol = new Color(0.98f, 0.80f, 0.38f);
    private static readonly Color GoodCol = new Color(0.55f, 0.92f, 0.55f);
    private static readonly Color WarnCol = new Color(1f, 0.56f, 0.46f);

    [System.Serializable]
    public class BarButton
    {
        public RectTransform rt;
        public Image bg;
        public Image icon;
        public Text name;
        public Text cost;
        public Text key;
        public Building building;       // build button (null for recruit buttons)
        public ResourceKind recruit;    // recruit button kind
        public KeyCode hotkey;
    }

    [Header("Sprites (assigned by the scene setup tool)")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite barSprite;
    [SerializeField] private Sprite pixelSprite;
    [SerializeField] private Sprite stoneSprite;
    [SerializeField] private Sprite woodSprite;
    [SerializeField] private Sprite popSprite;

    [Header("Top bar")]
    [SerializeField] private CounterLabel stoneLabel;
    [SerializeField] private CounterLabel woodLabel;
    [SerializeField] private RectTransform stoneIcon;
    [SerializeField] private RectTransform woodIcon;
    [SerializeField] private Text popText;
    [SerializeField] private Text dayText;
    [SerializeField] private Image dayBar;
    [SerializeField] private Image dayMarker;
    [SerializeField] private Image[] speedImages;
    public Image[] SpeedImages { get { return speedImages; } }

    [Header("Goal")]
    [SerializeField] private Text goalText;
    [SerializeField] private Text goalReward;
    [SerializeField] private Image goalFill;

    [Header("Build bar")]
    [SerializeField] private BarButton[] barButtons;

    [Header("Inspector panel")]
    [SerializeField] private RectTransform inspector;
    [SerializeField] private Text inspTitle;
    [SerializeField] private Text inspSub;
    [SerializeField] private Text inspBody;
    [SerializeField] private Text inspBarLabel;
    [SerializeField] private Image inspBarBg;
    [SerializeField] private Image inspBarFill;
    [SerializeField] private GameObject demolishButton;
    [SerializeField] private Text demolishLabel;

    [Header("Tooltip / help")]
    [SerializeField] private RectTransform tooltip;
    [SerializeField] private Text tooltipText;
    [SerializeField] private Text helpText;

    [Header("Templates (inactive objects that are cloned at runtime)")]
    [SerializeField] private RectTransform toastTemplate;
    [SerializeField] private RectTransform flyTemplate;

    private Canvas canvas;
    private float helpFade = 1f;
    private bool helpManual;
    private int flying;
    private float popTimer, barTimer;

    private class Toasty { public RectTransform rt; public Text txt; public CanvasGroup cg; public float age, life; public string key; }
    private readonly List<Toasty> toasts = new List<Toasty>();

    // ------------------------------------------------------------ lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        canvas = GetComponent<Canvas>();
        if (toastTemplate != null) toastTemplate.gameObject.SetActive(false);
        if (flyTemplate != null) flyTemplate.gameObject.SetActive(false);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        var rm = ResourceManager.Instance;
        if (rm == null) return;
        rm.Changed += OnResourceChanged;
        stoneLabel.Set(rm.Get(ResourceKind.Stone), true);
        woodLabel.Set(rm.Get(ResourceKind.Wood), true);
    }

    private void OnResourceChanged(ResourceKind kind, int amount, int delta)
    {
        (kind == ResourceKind.Stone ? stoneLabel : woodLabel).Set(amount);
    }

    // ------------------------------------------------------------ button targets (wired as persistent listeners)
    public void OnSpeedButton(int speed) { if (GameClock.Instance != null) GameClock.Instance.Set(speed); }
    public void OnWorldMapButton() { if (LevelSession.Instance != null) LevelSession.Instance.ReturnToWorld(); }
    public void OnDemolishButton() { if (SelectionController.Instance != null) SelectionController.Instance.DemolishSelected(); }
    public void OnBarButton(int index) { if (barButtons != null && index >= 0 && index < barButtons.Length) ActivateBarButton(barButtons[index]); }

    private void ActivateBarButton(BarButton b)
    {
        if (b.building != null)
        {
            var pc = PlacementController.Instance;
            if (pc.IsPlacing && pc.CurrentPrefab == b.building) pc.Cancel();
            else
            {
                if (!ResourceManager.Instance.CanAfford(b.building.Cost))
                {
                    GameAudio.Play(Sfx.Deny);
                    Toast("Not enough resources (" + b.building.Cost + ")", ToastKind.Warning);
                    return;
                }
                pc.Begin(b.building);
            }
        }
        else
        {
            Colony.Instance.Recruit(b.recruit);
        }
    }

    // ------------------------------------------------------------ per-frame
    private void Update()
    {
        // hotkeys: 1..N for build/recruit buttons
        if (barButtons != null && !DebugConsole.IsOpen)
            for (int i = 0; i < barButtons.Length; i++)
                if (Input.GetKeyDown(barButtons[i].hotkey)) ActivateBarButton(barButtons[i]);

        if (!DebugConsole.IsOpen && Input.GetKeyDown(KeyCode.H)) { helpManual = !helpManual; helpFade = helpManual ? 0f : 1f; }
        if (!helpManual && Time.unscaledTime > 40f) helpFade = Mathf.MoveTowards(helpFade, 0f, Time.unscaledDeltaTime / 2f);
        if (helpText != null) helpText.color = new Color(1f, 1f, 1f, 0.85f * helpFade);

        barTimer -= Time.unscaledDeltaTime;
        if (barTimer <= 0f) { barTimer = 0.1f; UpdateBar(); }

        popTimer -= Time.unscaledDeltaTime;
        if (popTimer <= 0f)
        {
            popTimer = 0.2f;
            var colony = Colony.Instance;
            if (colony != null)
            {
                int pop = colony.Population, cap = colony.PopulationCap;
                popText.text = pop + " / " + cap;
                popText.color = pop >= cap && cap > 0 ? AccentCol : TextCol;
            }
        }

        UpdateGoal();
        UpdateClock();
        UpdateInspector();
        UpdateTooltip();
        UpdateToasts();
    }

    private void UpdateBar()
    {
        if (barButtons == null) return;
        var pc = PlacementController.Instance;
        var rm = ResourceManager.Instance;
        for (int i = 0; i < barButtons.Length; i++)
        {
            var b = barButtons[i];
            bool ok;
            Cost c;
            bool active = false;
            if (b.building != null)
            {
                c = b.building.Cost;
                ok = rm.CanAfford(c);
                active = pc != null && pc.IsPlacing && pc.CurrentPrefab == b.building;
            }
            else
            {
                c = Colony.RecruitCost(b.recruit);
                string why;
                ok = Colony.Instance.CanRecruit(b.recruit, out why);
            }

            bool afford = rm.CanAfford(c);
            b.cost.color = afford ? DimCol : WarnCol;
            float dim = ok ? 1f : 0.55f;
            b.icon.color = new Color(dim, dim, dim, 1f);
            b.name.color = ok ? TextCol : new Color(0.7f, 0.72f, 0.8f);
            b.bg.color = active ? new Color(0.45f, 0.38f, 0.16f, 0.98f) : ButtonCol;
        }
    }

    private void UpdateGoal()
    {
        var c = Colony.Instance;
        if (c == null) return;
        if (c.AllGoalsDone)
        {
            goalText.text = c.GoalTitle;
            goalReward.text = "";
            goalFill.fillAmount = 1f;
            return;
        }
        goalText.text = c.GoalNumber + "/" + c.GoalCount + "  " + c.GoalTitle + "  (" + c.GoalProgress + "/" + c.GoalTarget + ")";
        goalReward.text = "Reward +" + c.GoalReward;
        float target = (float)c.GoalProgress / Mathf.Max(1, c.GoalTarget);
        goalFill.fillAmount = Mathf.Lerp(goalFill.fillAmount, target, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
    }

    private void UpdateClock()
    {
        var d = DayCycle.Instance;
        if (d != null)
        {
            dayText.text = "Day " + d.Day;
            var barRt = dayMarker.rectTransform;
            barRt.anchorMin = barRt.anchorMax = new Vector2(d.TimeOfDay, 0.5f);
            barRt.anchoredPosition = Vector2.zero;
            dayBar.color = Color.Lerp(new Color(1f, 0.85f, 0.45f), new Color(0.25f, 0.3f, 0.6f), d.Night01);
        }
        var gc = GameClock.Instance;
        if (gc != null && speedImages != null)
            for (int i = 0; i < speedImages.Length; i++)
                speedImages[i].color = GameClock.Speeds[i] == gc.Speed ? new Color(0.55f, 0.45f, 0.15f, 0.98f) : ButtonCol;
    }

    private void UpdateInspector()
    {
        var sel = SelectionController.Instance;
        if (sel == null || !sel.HasSelection) { if (inspector.gameObject.activeSelf) inspector.gameObject.SetActive(false); return; }
        if (!inspector.gameObject.activeSelf) inspector.gameObject.SetActive(true);

        bool worker = sel.SelectedWorker != null;
        bool building = sel.SelectedBuilding != null;
        inspBarLabel.gameObject.SetActive(worker);
        inspBarBg.gameObject.SetActive(worker);
        demolishButton.SetActive(building);

        if (worker)
        {
            var w = sel.SelectedWorker;
            inspTitle.text = w.workerName;
            inspSub.text = w.JobTitle;
            string home = w.home != null ? w.home.displayName : "none";
            inspBody.text = "STR " + w.strength + "   DEX " + w.dexterity + "   INT " + w.intelligence
                + "\nStatus: " + w.StatusText
                + "\nCarrying: " + w.Carry + " / " + w.CarryCapacity + " " + ResourceInfo.Name(w.gatherKind)
                + "\nHome: " + home;
            inspBarFill.fillAmount = 1f - w.Fatigue;
            inspBarFill.color = w.Fatigue > 0.85f ? WarnCol : (w.Fatigue > 0.6f ? AccentCol : GoodCol);
        }
        else if (building)
        {
            var b = sel.SelectedBuilding;
            inspTitle.text = b.displayName;
            inspSub.text = b.role == BuildingRole.Storage ? "Stockpile" : (b.maxWorkers > 0 ? "Camp" : "Building");
            string body = "";
            if (b.maxWorkers > 0)
            {
                body = "Workers: " + b.residents.Count + " / " + b.maxWorkers + "\nResting now: " + b.Sleepers;
                if (b.residents.Count > 0)
                {
                    body += "\nResidents:";
                    foreach (var r in b.residents) if (r != null) body += "\n  " + r.workerName;
                }
            }
            else if (b.role == BuildingRole.Storage)
            {
                var rm = ResourceManager.Instance;
                body = "Stone: " + rm.Get(ResourceKind.Stone) + "\nWood: " + rm.Get(ResourceKind.Wood) + "\nWorkers deliver here.";
            }
            inspBody.text = body;
            demolishLabel.text = "Demolish (Del)   +" + new Cost(Mathf.FloorToInt(b.costWood * 0.5f), Mathf.FloorToInt(b.costStone * 0.5f)).ToString().Replace("Free", "0");
        }
        else if (sel.SelectedNode != null)
        {
            var n = sel.SelectedNode;
            inspTitle.text = ResourceInfo.Name(n.resource) + " deposit";
            inspSub.text = "Resource";
            inspBody.text = "Remaining: " + n.yield + " / " + n.maxYield
                + (n.reservedBy != null ? "\nBeing worked by " + n.reservedBy.workerName : "\nUnclaimed");
        }
    }

    private void UpdateTooltip()
    {
        var sel = SelectionController.Instance;
        string text = sel != null ? sel.HoverText : null;
        if (string.IsNullOrEmpty(text)) { if (tooltip.gameObject.activeSelf) tooltip.gameObject.SetActive(false); return; }
        if (!tooltip.gameObject.activeSelf) tooltip.gameObject.SetActive(true);
        tooltipText.text = text;
        float w = tooltipText.preferredWidth + 28f;
        tooltip.sizeDelta = new Vector2(w, 34f);
        float scale = canvas.scaleFactor;
        Vector3 m = Input.mousePosition;
        float x = Mathf.Min(m.x + 22f, Screen.width - w * scale - 4f);
        float y = Mathf.Max(m.y - 8f, 40f * scale);
        tooltip.position = new Vector3(x, y, 0f);
    }

    // ------------------------------------------------------------ toasts
    public void Toast(string message, ToastKind kind = ToastKind.Info, float life = 3.4f)
    {
        if (toastTemplate == null) return;

        // refresh an identical toast instead of stacking duplicates
        for (int i = 0; i < toasts.Count; i++)
            if (toasts[i].key == message && toasts[i].age < toasts[i].life) { toasts[i].age = Mathf.Min(toasts[i].age, 0.2f); return; }

        var rt = Instantiate(toastTemplate, transform);
        rt.name = "Toast";
        rt.gameObject.SetActive(true);
        var txt = rt.GetComponentInChildren<Text>();
        var cg = rt.GetComponent<CanvasGroup>();
        Color tint = kind == ToastKind.Warning ? WarnCol : (kind == ToastKind.Good ? GoodCol : TextCol);
        txt.text = message;
        txt.color = tint;
        var stripe = rt.Find("stripe");
        if (stripe != null) stripe.GetComponent<Image>().color = kind == ToastKind.Warning ? WarnCol : (kind == ToastKind.Good ? GoodCol : AccentCol);
        float w = Mathf.Min(900f, txt.preferredWidth + 44f);
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);

        toasts.Insert(0, new Toasty { rt = rt, txt = txt, cg = cg, age = 0f, life = life, key = message });
        while (toasts.Count > 5) { Destroy(toasts[toasts.Count - 1].rt.gameObject); toasts.RemoveAt(toasts.Count - 1); }
    }

    private void UpdateToasts()
    {
        float dt = Time.unscaledDeltaTime;
        float y = 150f;
        for (int i = 0; i < toasts.Count; i++)
        {
            var t = toasts[i];
            t.age += dt;
            if (t.age >= t.life) { Destroy(t.rt.gameObject); toasts.RemoveAt(i); i--; continue; }

            float inK = Mathf.Clamp01(t.age / 0.2f);
            float outK = Mathf.Clamp01((t.life - t.age) / 0.45f);
            float ease = 1f - Mathf.Pow(1f - inK, 3f);
            t.cg.alpha = Mathf.Min(inK, outK);
            Vector2 target = new Vector2(16f - (1f - ease) * 40f, y);
            t.rt.anchoredPosition = Vector2.Lerp(t.rt.anchoredPosition, target, 1f - Mathf.Exp(-16f * dt));
            y += 46f;
        }
    }

    // ------------------------------------------------------------ fly icons
    public void FlyIcon(ResourceKind kind, Vector3 world)
    {
        var cam = Camera.main;
        if (cam == null || flying >= 12 || flyTemplate == null) return;
        RectTransform target = kind == ResourceKind.Stone ? stoneIcon : woodIcon;
        CounterLabel label = kind == ResourceKind.Stone ? stoneLabel : woodLabel;
        StartCoroutine(Fly(kind, cam.WorldToScreenPoint(world), target, label));
    }

    private IEnumerator Fly(ResourceKind kind, Vector2 from, RectTransform target, CounterLabel label)
    {
        flying++;
        var rt = Instantiate(flyTemplate, transform);
        rt.name = "FlyIcon";
        rt.gameObject.SetActive(true);
        var img = rt.GetComponent<Image>();
        img.sprite = kind == ResourceKind.Wood ? woodSprite : stoneSprite;
        rt.position = from;

        float t = 0f, dur = 0.75f;
        Vector2 p0 = from;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            Vector2 p2 = target.position;
            Vector2 p1 = (p0 + p2) * 0.5f + Vector2.up * 140f;
            float e = k * k * (3f - 2f * k);
            Vector2 pos = (1 - e) * (1 - e) * p0 + 2f * (1 - e) * e * p1 + e * e * p2;
            rt.position = pos;
            float s = Mathf.Lerp(1.2f, 0.7f, k);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        Destroy(rt.gameObject);
        label.Pulse();
        flying--;
    }
}
