using System.IO;
using UnityEngine;

/// <summary>Console commands: reset prefs / reset map / reset world / reset all.</summary>
public static class ResetCommands
{
    [DebugCommand("reset", "reset prefs | map | world | all   (prefs = PlayerPrefs, map = this level's save, world = world map + all saves)")]
    private static string Reset(string[] args)
    {
        string what = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
        switch (what)
        {
            case "prefs": return ResetPrefs();
            case "map": return ResetMap();
            case "world": return ResetWorld();
            case "all": return ResetPrefs() + "\n" + ResetWorld();
            default: return "Usage: reset prefs | map | world | all";
        }
    }

    // also available as single words
    [DebugCommand("resetprefs", "Deletes all PlayerPrefs (volume, mute, ...)")]
    private static string ResetPrefsCmd(string[] args) { return ResetPrefs(); }

    [DebugCommand("resetmap", "Level: deletes this map's save and starts it fresh")]
    private static string ResetMapCmd(string[] args) { return ResetMap(); }

    private static string ResetPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        return "PlayerPrefs deleted.";
    }

    private static string ResetMap()
    {
        var s = LevelSession.Instance;
        if (s == null) return "Only available in the Level scene (use 'reset world' on the world map).";
        s.ResetMap();
        return "Resetting the map...";
    }

    private static string ResetWorld()
    {
        var ow = OverworldController.Instance;
        // wipe every level save and the world save
        if (Directory.Exists(MapStore.Dir))
            foreach (string f in Directory.GetFiles(MapStore.Dir, "*.json")) File.Delete(f);
        LevelContext.Clear();
        if (ow != null) { ow.DebugNewWorld(); return "World and all map saves deleted; a new world began."; }
        return "World and all map saves deleted (a new world starts next time you open the world map).";
    }
}
