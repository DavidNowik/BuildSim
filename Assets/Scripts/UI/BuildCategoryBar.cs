using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The build bar: one tab per build layer (Materials, Food, Water, Power). Clicking a tab (or pressing 1-4) opens that layer's
/// menu above the bar; clicking a building in it starts placing it. Water and Power are empty for now.
/// Built into the scene by HudPocBuilder (all buttons are real objects).
/// </summary>
public class BuildCategoryBar : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public Button button;
        public Image bg;
        public GameObject menu;
    }

    [System.Serializable]
    public class Entry
    {
        public Building building;
        public Image bg;
        public Text cost;
    }

    public Tab[] tabs;
    public Entry[] entries;

    private static readonly Color Idle = new Color(0.22f, 0.2f, 0.36f, 1f);
    private static readonly Color Active = new Color(0.46f, 0.36f, 0.78f, 1f);
    private int open = -1;

    private void Start() { Refresh(); }

    private void Update()
    {
        if (!DebugConsole.IsOpen)
            for (int i = 0; i < tabs.Length && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) OnTab(i);

        if (open >= 0 && Input.GetKeyDown(KeyCode.Escape) && !(PlacementController.Instance != null && PlacementController.Instance.IsPlacing)) { open = -1; Refresh(); }

        var rm = ResourceManager.Instance;
        if (rm == null || entries == null) return;
        foreach (var e in entries)
        {
            if (e.building == null || e.bg == null) continue;
            bool ok = rm.CanAfford(e.building.Cost);
            e.bg.color = ok ? Idle : new Color(0.16f, 0.14f, 0.2f, 1f);
        }
    }

    /// <summary>Wired to each tab button (index as int argument).</summary>
    public void OnTab(int i)
    {
        if (i < 0 || i >= tabs.Length) return;
        open = open == i ? -1 : i;
        GameAudio.Play(Sfx.Click, 0.5f, 1.1f);
        Refresh();
    }

    /// <summary>Wired to each building button (entry index as int argument).</summary>
    public void OnBuild(int entry)
    {
        var e = entries[entry];
        if (e.building == null || PlacementController.Instance == null) return;
        if (ResourceManager.Instance != null && !ResourceManager.Instance.CanAfford(e.building.Cost))
        {
            GameAudio.Play(Sfx.Deny);
            if (HUD.Instance != null) HUD.Instance.Toast("Not enough resources (" + e.building.Cost + ")", ToastKind.Warning);
            return;
        }
        PlacementController.Instance.Begin(e.building);
        open = -1;
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].menu != null) tabs[i].menu.SetActive(open == i);
            if (tabs[i].bg != null) tabs[i].bg.color = open == i ? Active : Idle;
        }
    }
}
