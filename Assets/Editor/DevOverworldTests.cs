using UnityEngine;
using UnityEditor;

/// <summary>Scripted play-mode checks used by the dev helper (play ... DevOverworldTests.X). Safe to delete.</summary>
public static class DevOverworldTests
{
    private static void Prepare()
    {
        Application.runInBackground = true;
        Time.maximumDeltaTime = 3f;                 // an unfocused editor runs at a few fps; keep game time = real time
        OverworldController.Instance.DebugNewWorld();
    }

    public static void Progress()
    {
        Prepare();
        var c = OverworldController.Instance;
        c.DebugDiscover(6);
        c.DebugAddMoon(1);
    }

    public static void EnterStart()
    {
        Prepare();
        OverworldController.Instance.DebugEnterMap(0);
    }

    /// <summary>Overworld -> Level -> back, to check both directions of the scene hand-off.</summary>
    public static void RoundTrip()
    {
        Prepare();
        OverworldController.Instance.DebugEnterMap(0);
        double start = UnityEditor.EditorApplication.timeSinceStartup;
        UnityEditor.EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (LevelSession.Instance == null || UnityEditor.EditorApplication.timeSinceStartup - start < 4.0) return;
            UnityEditor.EditorApplication.update -= tick;
            LevelSession.Instance.ReturnToWorld();
        };
        UnityEditor.EditorApplication.update += tick;
    }



    public static void Flood()
    {
        Prepare();
        var c = OverworldController.Instance;
        c.DebugDiscover(6);
        c.DebugFlood();
    }
}
