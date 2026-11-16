/// <summary>Debug console commands that only make sense in the Overworld scene.</summary>
public static class OverworldDebugCommands
{
    [DebugCommand("tide", "Overworld: the tide comes in now (the sea takes the world)")]
    private static string Tide(string[] args)
    {
        var c = OverworldController.Instance;
        if (c == null) return "Only available in the Overworld scene.";
        if (c.IsFlooding) return "The tide is already coming in.";
        c.DebugFlood();
        return "The tide rises...";
    }
}
