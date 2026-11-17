/// <summary>Debug console commands for the colony (the recruit buttons were removed from the HUD).</summary>
public static class ColonyDebugCommands
{
    [DebugCommand("worker", "worker <n>   - add n workers for free, up to the housing cap")]
    private static string AddWorkers(string[] args)
    {
        var colony = Colony.Instance;
        if (colony == null) return "Only available in the Level scene.";
        int n;
        if (args == null || args.Length == 0 || !int.TryParse(args[0], out n) || n < 1) return "Usage: worker <n>";
        int added = colony.AddWorkers(n);
        return added + " worker(s) added (" + colony.Population + "/" + colony.PopulationCap + ").";
    }

    [DebugCommand("speed", "speed <n>   - set the game speed (1 = normal, 0 = pause)")]
    private static string Speed(string[] args)
    {
        int n;
        if (args == null || args.Length == 0 || !int.TryParse(args[0], out n) || n < 0 || n > 20) return "Usage: speed <0-20>";
        if (GameClock.Instance != null) GameClock.Instance.Set(n);
        else UnityEngine.Time.timeScale = n;
        return "Game speed " + n + "x.";
    }

    [DebugCommand("recruit", "recruit stone|wood|fish [free]   - hire a worker into a hut with room (free = no cost)")]
    private static string Recruit(string[] args)
    {
        var colony = Colony.Instance;
        if (colony == null) return "Only available in the Level scene.";
        if (args == null || args.Length == 0) return "Usage: recruit stone|wood|fish [free]";

        ResourceKind kind;
        switch (args[0].ToLowerInvariant())
        {
            case "stone": case "miner": kind = ResourceKind.Stone; break;
            case "wood": case "woodcutter": kind = ResourceKind.Wood; break;
            case "fish": case "fisher": kind = ResourceKind.Fish; break;
            default: return "Unknown worker type: " + args[0];
        }

        bool free = args.Length > 1 && args[1].ToLowerInvariant() == "free";
        if (free)
        {
            var c = Colony.RecruitCost(kind);
            ResourceManager.Instance.Add(ResourceKind.Wood, c.wood);
            ResourceManager.Instance.Add(ResourceKind.Stone, c.stone);
        }
        string reason;
        if (!colony.CanRecruit(kind, out reason)) return "Cannot recruit: " + reason;
        return colony.Recruit(kind) ? "Recruited a " + ResourceInfo.JobTitle(kind) + "." : "Recruiting failed.";
    }
}
