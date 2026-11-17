using UnityEngine;

/// <summary>Scripted play-mode checks for the Level scene (dev helper). Safe to delete.</summary>
public static class DevLevelTests
{
    public static void Zoom4()
    {
        Application.runInBackground = true;
        if (CameraMotor.Instance != null) CameraMotor.Instance.SetZoomLevel(4);
    }

    public static void Zoom3()
    {
        Application.runInBackground = true;
        if (CameraMotor.Instance != null) CameraMotor.Instance.SetZoomLevel(3);
    }

    public static void Crate()
    {
        Application.runInBackground = true;
        var m = CrateManager.Instance;
        if (m == null || m.All.Count == 0) { Debug.Log("[Dev] no crates"); return; }
        var c = m.All[0];
        if (ResourceManager.Instance != null) ResourceManager.Instance.Add(ResourceKind.Wood, 200);
        var cam = Camera.main;
        cam.transform.position = new Vector3(c.transform.position.x + 3f, c.transform.position.y, cam.transform.position.z);
        if (CameraMotor.Instance != null) CameraMotor.Instance.SetZoomLevel(4);
        SelectionController.Instance.SelectCrate(c);
        Debug.Log("[Dev] crate at " + c.data.anchor + " needs " + c.data.amount + " pays " + c.data.pearls);
    }

    public static void OpenCrate()
    {
        Application.runInBackground = true;
        Crate();
        var c = CrateManager.Instance.All[0];
        bool ok = CrateManager.Instance.TryOpen(c, ResourceKind.Wood);
        Debug.Log("[Dev] open result " + ok + " opened=" + c.Opened);
    }

    public static void Fisher()
    {
        Application.runInBackground = true;
        Time.maximumDeltaTime = 3f;
        var g = GridManager.instance;
        var col = Colony.Instance;
        var hutPrefab = col.PrefabByName("Fisher Hut");
        ResourceManager.Instance.Add(ResourceKind.Wood, 200);
        Cell shore = null;
        foreach (var n in g.Nodes) if (n.resource == ResourceKind.Fish && Vector2Int.Distance(n.pos, g.startCell) < 14) { shore = n; break; }
        if (shore == null) { Debug.Log("[Dev] no fishing spot near start"); return; }
        for (int r = 1; r < 5; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    var a = shore.pos + new Vector2Int(dx, dy);
                    if (!g.CanPlace(a, hutPrefab.footprint)) continue;
                    var hut = Object.Instantiate(hutPrefab);
                    hut.Place(a, false);
                    Debug.Log("[Dev] hut at " + a + " shore " + shore.pos + " recruit=" + col.Recruit(ResourceKind.Fish));
                    var cam = Camera.main; cam.transform.position = new Vector3(g.CellToWorld(a).x, g.CellToWorld(a).y, cam.transform.position.z);
                    CameraMotor.Instance.SetZoomLevel(4);
                    Time.timeScale = 4f;
                    return;
                }
        Debug.Log("[Dev] no room for the hut");
    }

    public static void CollapseSome()
    {
        foreach (var c in Object.FindObjectsOfType<Collapsible>())
        {
            string n = c.panel.name;
            c.SetCollapsed(n == "Left Toolbar" || n == "Right Toolbar" || n == "Inspector" || n == "Tabs" || n == "Build Bar");
        }
    }

    public static void ExpandAll()
    {
        foreach (var c in Object.FindObjectsOfType<Collapsible>()) c.SetCollapsed(false);
    }

    public static void SelectWorker()
    {
        if (Worker.All.Count > 0) SelectionController.Instance.ClickCell(GridManager.instance.WorldToCell(Worker.All[0].transform.position));
    }
}
