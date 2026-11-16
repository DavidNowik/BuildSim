using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD of the overworld. The whole hierarchy is real GameObjects under this Canvas, created by the
/// "Rebuild UI" button on this component (OverworldHUD.Builder.cs) and freely editable by hand.
/// This runtime part only fills the existing objects.
/// </summary>
public partial class OverworldHUD : MonoBehaviour
{
    public static OverworldHUD Instance;

    private static readonly Color TextCol = new Color(0.95f, 0.96f, 1f);
    private static readonly Color DimCol = new Color(0.66f, 0.71f, 0.84f);
    private static readonly Color AccentCol = new Color(0.98f, 0.80f, 0.38f);
    private static readonly Color DarkToast = new Color(0.81f, 0.91f, 0.96f);

    [System.Serializable]
    public class Card
    {
        public GameObject root;
        public Image bg;
        public Image icon;
        public Text title;
        public Text desc;
        public Text cost;
    }

    [Header("Sprites (assigned by the scene setup tool)")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite barSprite;
    [SerializeField] private Sprite pixelSprite;
    [SerializeField] private Sprite pearlSprite;
    [SerializeField] private Sprite[] moonSprites;

    [Header("Top bar")]
    [SerializeField] private Text landsText;
    [SerializeField] private Text reachText;
    [SerializeField] private CounterLabel pearlsLabel;
    [SerializeField] private RectTransform tideRoot;
    [SerializeField] private Image moonIcon;
    [SerializeField] private Image tideFill;
    [SerializeField] private Text tideLabel;
    [SerializeField] private Text cycleText;

    [Header("Info panel")]
    [SerializeField] private RectTransform infoPanel;
    [SerializeField] private Text infoTitle;
    [SerializeField] private Text infoSub;
    [SerializeField] private Text infoBody;
    [SerializeField] private GameObject infoButton;
    [SerializeField] private Text infoButtonLabel;

    [Header("Modifier choice")]
    [SerializeField] private GameObject modal;
    [SerializeField] private Text modalSub;
    [SerializeField] private Card[] cards;

    [Header("Banner (after the flood)")]
    [SerializeField] private CanvasGroup banner;
    [SerializeField] private Text bannerTitle;
    [SerializeField] private Text bannerSub;
    [SerializeField] private Text bannerPearls;

    [Header("Misc")]
    [SerializeField] private RectTransform toastTemplate;
    [SerializeField] private Text helpText;

    public bool ModalOpen { get { return modal != null && modal.activeSelf; } }

    private Action infoAction;
    private Action<int> pickAction;
    private float fillShown;
    private int moonNow, moonMax = 10;

    private class Toasty { public RectTransform rt; public CanvasGroup cg; public float age, life; public string key; }
    private readonly List<Toasty> toasts = new List<Toasty>();

    private void Awake()
    {
        Instance = this;
        if (toastTemplate != null) toastTemplate.gameObject.SetActive(false);
        if (modal != null) modal.SetActive(false);
        if (banner != null) { banner.alpha = 0f; banner.gameObject.SetActive(false); }
        if (infoPanel != null) infoPanel.gameObject.SetActive(false);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // ------------------------------------------------------------ state
    public void SetStats(int lands, int reach, int pearls, int cycle)
    {
        landsText.text = lands.ToString();
        reachText.text = reach.ToString();
        pearlsLabel.Set(pearls);
        if (cycleText != null) cycleText.text = "Tide " + cycle;
    }

    public void SetMoon(int moon, int max)
    {
        moonNow = Mathf.Clamp(moon, 0, max); moonMax = max;
        float p = (float)moonNow / max;
        if (moonSprites != null && moonSprites.Length > 0) moonIcon.sprite = moonSprites[Mathf.Min(moonSprites.Length - 1, Mathf.FloorToInt(p * moonSprites.Length))];
        int left = max - moonNow;
        tideLabel.text = left <= 0 ? "The sea rises" : (left <= 2 ? "The sea stirs... " + left : "Tide in " + left);
    }

    private void Update()
    {
        if (tideFill != null)
        {
            float target = moonMax > 0 ? (float)moonNow / moonMax : 0f;
            fillShown = Mathf.Lerp(fillShown, target, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            tideFill.fillAmount = fillShown;
        }
        if (tideRoot != null)
        {
            bool danger = moonMax - moonNow <= 2;
            float s = danger ? 1f + 0.025f * Mathf.Sin(Time.unscaledTime * 2.4f) : 1f;
            tideRoot.localScale = new Vector3(s, s, 1f);
        }
        UpdateToasts();
    }

    // ------------------------------------------------------------ info panel
    public void ShowInfo(string title, string sub, string body, string buttonLabel, Action onButton)
    {
        infoPanel.gameObject.SetActive(true);
        infoTitle.text = title; infoSub.text = sub; infoBody.text = body;
        infoButton.SetActive(onButton != null);
        infoButtonLabel.text = buttonLabel;
        infoAction = onButton;
    }

    public void HideInfo() { if (infoPanel != null) infoPanel.gameObject.SetActive(false); infoAction = null; }
    public void OnInfoButton() { var a = infoAction; if (a != null) a(); }

    // ------------------------------------------------------------ modifier choice
    public void ShowModal(string sub, MapModifier[] options, Sprite[] icons, Action<int> onPick)
    {
        modalSub.text = sub;
        pickAction = onPick;
        for (int i = 0; i < cards.Length; i++)
        {
            bool on = i < options.Length;
            cards[i].root.SetActive(on);
            if (!on) continue;
            var m = options[i];
            cards[i].title.text = m.name; cards[i].title.color = m.color;
            cards[i].desc.text = m.desc;
            cards[i].cost.text = m.moonCost < 0 ? m.moonCost + " moon" : "+" + m.moonCost + " moon";
            cards[i].icon.sprite = icons != null && i < icons.Length ? icons[i] : null;
            cards[i].icon.color = Color.white;
        }
        modal.SetActive(true);
    }

    public void HideModal() { if (modal != null) modal.SetActive(false); }
    public void OnCard(int index) { var a = pickAction; HideModal(); if (a != null) a(index); }

    // ------------------------------------------------------------ banner
    public void ShowBanner(string title, string sub, string pearls, float seconds)
    {
        StopCoroutine("BannerRoutine");
        StartCoroutine(BannerRoutine(title, sub, pearls, seconds));
    }

    private IEnumerator BannerRoutine(string title, string sub, string pearls, float seconds)
    {
        bannerTitle.text = title; bannerSub.text = sub; bannerPearls.text = pearls;
        banner.gameObject.SetActive(true);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            banner.alpha = Mathf.Min(Mathf.Clamp01(t / 1.8f), Mathf.Clamp01((seconds - t) / 1.2f));
            yield return null;
        }
        banner.alpha = 0f;
        banner.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ toasts
    public void Toast(string message, bool dark = false, float life = 2.4f)
    {
        if (toastTemplate == null) return;
        for (int i = 0; i < toasts.Count; i++)
            if (toasts[i].key == message && toasts[i].age < toasts[i].life) { toasts[i].age = Mathf.Min(toasts[i].age, 0.2f); return; }

        var rt = Instantiate(toastTemplate, transform);
        rt.name = "Toast";
        rt.gameObject.SetActive(true);
        var txt = rt.GetComponentInChildren<Text>();
        txt.text = message;
        txt.color = dark ? DarkToast : TextCol;
        rt.sizeDelta = new Vector2(Mathf.Min(900f, txt.preferredWidth + 44f), rt.sizeDelta.y);
        toasts.Insert(0, new Toasty { rt = rt, cg = rt.GetComponent<CanvasGroup>(), age = 0f, life = life, key = message });
        while (toasts.Count > 4) { Destroy(toasts[toasts.Count - 1].rt.gameObject); toasts.RemoveAt(toasts.Count - 1); }
    }

    private void UpdateToasts()
    {
        float dt = Time.unscaledDeltaTime, y = 24f;
        for (int i = 0; i < toasts.Count; i++)
        {
            var t = toasts[i];
            t.age += dt;
            if (t.age >= t.life) { Destroy(t.rt.gameObject); toasts.RemoveAt(i); i--; continue; }
            float inK = Mathf.Clamp01(t.age / 0.2f), outK = Mathf.Clamp01((t.life - t.age) / 0.45f);
            t.cg.alpha = Mathf.Min(inK, outK);
            Vector2 target = new Vector2(24f, y + 110f - (1f - (1f - Mathf.Pow(1f - inK, 3f))) * 30f);
            t.rt.anchoredPosition = Vector2.Lerp(t.rt.anchoredPosition, target, 1f - Mathf.Exp(-16f * dt));
            y += 46f;
        }
    }
}
