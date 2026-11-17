using UnityEngine;
using UnityEngine.UI;

/// <summary>Top bar: day/night clock plus a field with every resource the colony has owned (have / max). The field and the bar grow with the entries.</summary>
public class TopBar : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public ResourceKind kind;
        public GameObject root;
        public Text amount;
        [HideInInspector] public bool seen;
    }

    public RectTransform bar, field;
    public Entry[] entries;
    [Header("Layout")]
    public int columns = 4;
    public Vector2 cell = new Vector2(120, 40);
    public float gap = 6f, pad = 6f, margin = 8f, clockWidth = 160f, minHeight = 82f;
    [Tooltip("Placeholder: the game has no storage cap yet")]
    public int maxStorage = 100;

    public Text dayLabel, workersLabel;
    public Image timeFill;
    public Color dayColor = new Color(1f, 0.82f, 0.4f), nightColor = new Color(0.4f, 0.45f, 0.85f);

    private int lastActive = -1;

    private void OnEnable()
    {
        foreach (var e in entries) { e.seen = false; e.root.SetActive(false); }
        lastActive = -1;
    }

    private void Update()
    {
        var rm = ResourceManager.Instance;
        int active = 0;
        if (rm != null)
            foreach (var e in entries)
            {
                int n = rm.Get(e.kind);
                if (n > 0 && !e.seen) { e.seen = true; e.root.SetActive(true); }
                if (!e.seen) continue;
                e.amount.text = n + " / " + maxStorage;
                active++;
            }
        if (active != lastActive) { lastActive = active; Resize(active); }

        workersLabel.text = Worker.All.Count + "/" + (Colony.Instance != null ? Colony.Instance.PopulationCap : 0);

        var day = DayCycle.Instance;
        if (day == null) return;
        dayLabel.text = "Day " + day.Day;
        timeFill.fillAmount = day.TimeOfDay;
        timeFill.color = Color.Lerp(dayColor, nightColor, day.Night01);
    }

    private void Resize(int active)
    {
        field.gameObject.SetActive(active > 0);
        if (active == 0) { bar.sizeDelta = new Vector2(clockWidth, minHeight); return; }
        int cols = Mathf.Min(active, columns), rows = Mathf.CeilToInt(active / (float)columns);
        var size = new Vector2(cols * cell.x + (cols - 1) * gap + 2 * pad, rows * cell.y + (rows - 1) * gap + 2 * pad);
        field.sizeDelta = size;
        bar.sizeDelta = new Vector2(clockWidth + size.x + margin, Mathf.Max(minHeight, size.y + 2 * margin));
    }
}
