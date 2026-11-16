using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pooled floating numbers. The canvas and the template Text are objects in the scene (child of this object);
/// entries are cloned from the template. Rise with ease-out, pop-in, fade-out, and rapid "+1 +1 +1" merge into "+3".
/// </summary>
public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance { get; private set; }

    [Header("Scene references")]
    public Canvas canvas;
    [Tooltip("Inactive Text object that is cloned for every popup")]
    public Text template;

    private class Entry
    {
        public GameObject go;
        public RectTransform rt;
        public Text txt;
        public Vector3 world;
        public float t, duration, rise;
        public Color color;
        public string key;
        public int amount;
        public string suffix;
        public float pop;
        public bool active;
    }

    private readonly List<Entry> pool = new List<Entry>();
    private Camera cam;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (template != null) template.gameObject.SetActive(false);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // ------------------------------------------------------------ API
    /// <summary>Numeric popup that merges with a still-fresh popup using the same key.</summary>
    public void ShowAmount(string key, int delta, string suffix, Color color, Vector3 position)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            var e = pool[i];
            if (e.active && e.key == key && e.t < 0.55f)
            {
                e.amount += delta;
                e.txt.text = "+" + e.amount + e.suffix;
                e.t = Mathf.Min(e.t, 0.08f); // re-pop
                e.pop = 0f;
                return;
            }
        }
        Spawn("+" + delta + suffix, color, position, 26, 1.1f, 0.9f, key, delta, suffix);
    }

    // ------------------------------------------------------------ internals
    private Entry Get()
    {
        for (int i = 0; i < pool.Count; i++)
            if (!pool[i].active) return pool[i];
        if (pool.Count >= 48 || template == null || canvas == null) return null;

        var txt = Instantiate(template, canvas.transform);
        txt.name = "FloatText";
        var e = new Entry { go = txt.gameObject, rt = txt.rectTransform, txt = txt };
        pool.Add(e);
        return e;
    }

    private void Spawn(string text, Color color, Vector3 pos, int size, float duration, float rise, string key, int amount, string suffix)
    {
        var e = Get();
        if (e == null) return;
        e.txt.text = text;
        e.txt.fontSize = size;
        e.txt.color = color;
        e.color = color;
        e.world = pos + new Vector3(Random.Range(-0.15f, 0.15f), 0.55f, 0f);
        e.t = 0f;
        e.pop = 0f;
        e.duration = duration;
        e.rise = rise;
        e.key = key;
        e.amount = amount;
        e.suffix = suffix;
        e.active = true;
        e.go.SetActive(true);
        Place(e, 0f);
    }

    private void Place(Entry e, float k)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        float ease = 1f - Mathf.Pow(1f - k, 3f);
        Vector3 w = e.world + Vector3.up * (e.rise * ease);
        e.rt.position = cam.WorldToScreenPoint(w);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < pool.Count; i++)
        {
            var e = pool[i];
            if (!e.active) continue;
            e.t += dt;
            e.pop += dt;
            float k = e.t / e.duration;
            if (k >= 1f)
            {
                e.active = false;
                e.go.SetActive(false);
                continue;
            }
            Place(e, k);

            // pop-in: 0.6 -> 1.25 -> 1.0 over 0.2s
            float p = Mathf.Clamp01(e.pop / 0.2f);
            float s = p < 0.5f ? Mathf.Lerp(0.6f, 1.25f, p * 2f) : Mathf.Lerp(1.25f, 1f, (p - 0.5f) * 2f);
            e.rt.localScale = new Vector3(s, s, 1f);

            var c = e.color;
            c.a = k < 0.65f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.65f) / 0.35f);
            e.txt.color = c;
        }
    }
}
