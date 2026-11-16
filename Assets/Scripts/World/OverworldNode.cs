using System.Collections;
using UnityEngine;

/// <summary>
/// One land on the overworld. All parts are child objects of the Node prefab (visible in the hierarchy);
/// this component only drives their look: bobbing, pulsing "?" lands, pop-in, wet/sinking during the tide.
/// </summary>
public class OverworldNode : MonoBehaviour
{
    [Header("Parts (children of the prefab)")]
    public SpriteRenderer shadow;
    public SpriteRenderer rim;
    public SpriteRenderer body;
    public SpriteRenderer gloss;
    public SpriteRenderer icon;
    public SpriteRenderer pulseA;
    public SpriteRenderer pulseB;
    public SpriteRenderer selectRing;
    public TextMesh questionLabel;
    public TextMesh nameLabel;
    public TextMesh infoLabel;
    public TextMesh nameShadow;
    public TextMesh infoShadow;
    public Transform visual;                 // everything that bobs

    public int Id { get; private set; }
    public int Tier { get; private set; }
    public string LandName { get; private set; }
    public NodeStatus Status { get; private set; }
    public bool Swallowed { get; private set; }

    private float bobPhase;
    private bool hover;
    private float hoverScale = 1f;
    private float wet;                        // 0..1
    private Color hue = Color.white;
    private float baseRadius = 0.6f;

    public void Setup(int id, int tier, string landName)
    {
        Id = id; Tier = tier; LandName = landName;
        bobPhase = id * 0.37f;
        hue = Color.HSVToRGB(((330f + tier * 38f) % 360f) / 360f, 0.72f, 1f);
        float r = 0.55f + Mathf.Min(tier, 8) * 0.04f;
        baseRadius = r;
        transform.localScale = Vector3.one * (r / 0.6f * 1.15f);   // lands are drawn a bit larger than their logical radius
    }

    public void SetState(NodeStatus status, MapModifier mod, Sprite modIcon, bool animateIn)
    {
        Status = status;
        bool open = status == NodeStatus.Open, done = status == NodeStatus.Done;

        body.color = open ? new Color(0.35f, 0.23f, 0.62f) : hue;
        rim.color = open ? Color.white : Color.Lerp(hue, Color.white, 0.7f);
        gloss.enabled = done;
        pulseA.enabled = pulseB.enabled = open;
        pulseA.color = pulseB.color = hue;
        questionLabel.gameObject.SetActive(open);
        icon.enabled = done && modIcon != null;
        icon.sprite = modIcon;
        nameLabel.text = nameShadow.text = open ? "Uncharted" : LandName;
        nameLabel.color = open ? new Color(0.85f, 0.78f, 1f) : new Color(1f, 0.97f, 0.9f);
        infoLabel.text = infoShadow.text = open ? "tier " + Tier : "tier " + Tier + (mod != null ? "  -  " + mod.name : "");
        infoLabel.color = Color.Lerp(hue, Color.white, 0.5f);
        gameObject.SetActive(status != NodeStatus.Hidden);
        if (animateIn && gameObject.activeInHierarchy) StartCoroutine(PopIn());
    }

    public void SetSelected(bool on) { if (selectRing != null) selectRing.enabled = on; }
    public void SetHover(bool on) { hover = on; }
    public void SetWet(bool on) { wet = on ? 1f : 0f; }

    public void Swallow()
    {
        if (Swallowed) return;
        Swallowed = true;
        StartCoroutine(Sink());
    }

    private IEnumerator PopIn()
    {
        float t = 0f; const float dur = 0.7f;
        Vector3 target = transform.localScale;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float s = k < 0.6f ? Mathf.Lerp(0f, 1.25f, k / 0.6f) : Mathf.Lerp(1.25f, 1f, (k - 0.6f) / 0.4f);
            visual.localScale = Vector3.one * s;
            visual.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-40f, 0f, k));
            yield return null;
        }
        visual.localScale = Vector3.one;
        visual.localRotation = Quaternion.identity;
    }

    private IEnumerator Sink()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        var baseCols = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseCols[i] = renderers[i].color;
        var labels = new[] { questionLabel, nameLabel, infoLabel, nameShadow, infoShadow };
        var labelCols = new[] { questionLabel.color, nameLabel.color, infoLabel.color, nameShadow.color, infoShadow.color };
        Color dark = new Color(0.12f, 0.2f, 0.3f);

        float t = 0f; const float dur = 1.6f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = k * k;
            visual.localPosition = Vector3.down * (1.2f * e);
            visual.localRotation = Quaternion.Euler(0, 0, 14f * e);
            visual.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, e);
            float fade = 1f - Mathf.Clamp01((k - 0.4f) / 0.6f);
            for (int i = 0; i < renderers.Length; i++)
            {
                Color c = Color.Lerp(baseCols[i], dark, Mathf.Clamp01(k * 2f));
                renderers[i].color = new Color(c.r, c.g, c.b, baseCols[i].a * fade);
            }
            for (int i = 0; i < labels.Length; i++)
            {
                var c = labelCols[i]; labels[i].color = new Color(c.r, c.g, c.b, c.a * fade);
            }
            yield return null;
        }
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Swallowed) return;
        float t = Time.time;
        // gentle bob
        if (visual != null && Status != NodeStatus.Hidden)
        {
            float bob = Mathf.Sin(t * 1.9f + bobPhase * 6f) * 0.08f;
            visual.localPosition = new Vector3(0f, bob, 0f);
        }
        // hover pop
        hoverScale = Mathf.Lerp(hoverScale, hover ? 1.12f : 1f, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        if (body != null) body.transform.localScale = Vector3.one * hoverScale;

        // pulsing rings on uncharted lands
        if (Status == NodeStatus.Open)
        {
            Pulse(pulseA, t, 0f);
            Pulse(pulseB, t, 0.5f);
            questionLabel.transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * 4f));
        }

        // wet lands (the rising water has reached them) turn grey
        float w = wet;
        if (body != null)
        {
            Color c = Status == NodeStatus.Open ? new Color(0.35f, 0.23f, 0.62f) : hue;
            Color g = Color.Lerp(c, new Color(0.35f, 0.42f, 0.5f), w * 0.75f);
            body.color = Color.Lerp(body.color, g, 1f - Mathf.Exp(-4f * Time.deltaTime));
        }
    }

    private static void Pulse(SpriteRenderer sr, float t, float offset)
    {
        float k = Mathf.Repeat(t * 0.7f + offset, 1f);
        sr.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.9f, k);
        var c = sr.color; c.a = (1f - k) * 0.9f; sr.color = c;
    }
}
