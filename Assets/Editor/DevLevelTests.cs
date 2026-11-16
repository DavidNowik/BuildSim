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
}
