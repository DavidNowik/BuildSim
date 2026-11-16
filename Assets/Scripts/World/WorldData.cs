using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum NodeStatus { Hidden = 0, Open = 1, Done = 2 }

[Serializable]
public class WorldNodeSave
{
    public int status;        // NodeStatus
    public string modId;      // chosen modifier (Done nodes)
    public string mapId;      // level save slot (Done nodes)
    public int mapSeed;       // level seed, created when the land is discovered
}

/// <summary>The overworld meta state: one cycle of the tide. Pearls and best reach survive the flood.</summary>
[Serializable]
public class WorldSave
{
    public int version = 1;
    public int seed;
    public int cycle = 1;
    public int moon;
    public int pearls;
    public int bestReach;
    public List<WorldNodeSave> nodes = new List<WorldNodeSave>();
}

public static class WorldStore
{
    public static string PathFile { get { return Path.Combine(MapStore.Dir, "world.json"); } }
    public static bool Exists() { return File.Exists(PathFile); }

    public static void Save(WorldSave w)
    {
        Directory.CreateDirectory(MapStore.Dir);
        string tmp = PathFile + ".tmp";
        File.WriteAllText(tmp, JsonUtility.ToJson(w));
        if (File.Exists(PathFile)) File.Delete(PathFile);
        File.Move(tmp, PathFile);
    }

    public static WorldSave Load() { return JsonUtility.FromJson<WorldSave>(File.ReadAllText(PathFile)); }
    public static void Delete() { if (Exists()) File.Delete(PathFile); }
}

/// <summary>What the Level scene should load. Set by the overworld right before switching scenes.</summary>
public static class LevelContext
{
    public static bool HasContext { get; private set; }
    public static string MapId { get; private set; }
    public static int Seed { get; private set; }
    public static int Tier { get; private set; }
    public static string ModifierId { get; private set; }

    public static void Set(string mapId, int seed, int tier, string modifierId)
    {
        HasContext = true; MapId = mapId; Seed = seed; Tier = tier; ModifierId = modifierId;
    }

    public static void Clear() { HasContext = false; }
}

[Serializable]
public class MapModifier
{
    public string id;
    public string name;
    public string desc;
    public int moonCost;
    public Color color;
}

/// <summary>The choices offered when a land is discovered. Each changes how that land forms.</summary>
public static class Modifiers
{
    private static Color C(int r, int g, int b) { return new Color32((byte)r, (byte)g, (byte)b, 255); }

    public static readonly MapModifier[] All =
    {
        new MapModifier { id = "veins",    name = "Rich Veins",      desc = "Many stone deposits. Fewer trees.",           moonCost = 1,  color = C(159, 210, 255) },
        new MapModifier { id = "woods",    name = "Deep Woods",      desc = "Dense forests. Little stone.",                moonCost = 1,  color = C(125, 255, 154) },
        new MapModifier { id = "lakes",    name = "Many Lakes",      desc = "Lots of water. Less room to build.",         moonCost = 1,  color = C(71, 200, 255) },
        new MapModifier { id = "dunes",    name = "Shifting Dunes",  desc = "Open land, few resources.",                  moonCost = 1,  color = C(255, 210, 122) },
        new MapModifier { id = "blessing", name = "Moon Blessing",   desc = "The tide is held back: +2 moons of time.",   moonCost = -2, color = C(217, 184, 255) },
        new MapModifier { id = "storm",    name = "Storm Coast",     desc = "Rich deposits - but the tide rises faster.", moonCost = 2,  color = C(255, 233, 74) },
        new MapModifier { id = "ruins",    name = "Old Ruins",       desc = "Start with extra wood and stone.",           moonCost = 1,  color = C(255, 159, 208) },
        new MapModifier { id = "calm",     name = "Gentle Hills",    desc = "Balanced land. A safe choice.",              moonCost = 1,  color = C(198, 255, 138) },
    };

    public static int IndexOf(string id)
    {
        for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i;
        return All.Length - 1;
    }

    public static MapModifier Get(string id) { return All[IndexOf(id)]; }

    /// <summary>Adjusts the level generator settings and start bonuses for a modifier.</summary>
    public static void ApplyToLevel(string id, MapSettings s, out float yieldMultiplier, out int bonusWood, out int bonusStone)
    {
        yieldMultiplier = 1f; bonusWood = 0; bonusStone = 0;
        switch (id)
        {
            case "veins": s.stoneAmount = 2.2f; s.forestAmount = 0.5f; break;
            case "woods": s.forestAmount = 2.2f; s.stoneAmount = 0.5f; break;
            case "lakes": s.waterAmount = 0.30f; break;
            case "dunes": s.dirtAmount = 0.35f; s.stoneAmount = 0.6f; s.forestAmount = 0.5f; break;
            case "storm": yieldMultiplier = 1.5f; break;
            case "ruins": bonusWood = 40; bonusStone = 20; break;
        }
    }
}

/// <summary>Pearls live in the world save (they survive the tide). Levels add to them without the overworld being loaded.</summary>
public static class PearlBank
{
    /// <summary>Adds pearls to the saved world; returns the new total, or -1 when there is no world save yet.</summary>
    public static int Add(int n)
    {
        if (!WorldStore.Exists()) return -1;
        var w = WorldStore.Load();
        w.pearls += n;
        WorldStore.Save(w);
        return w.pearls;
    }
}
