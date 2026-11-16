using UnityEngine;

public enum ResourceKind { None = 0, Stone = 1, Wood = 2 }

public enum BuildingRole { Generic = 0, Storage = 1, StoneCamp = 2, WoodCamp = 3 }

public enum WorkerState { Idle, GoToNode, Gather, GoToStorage, GoToBed, Sleep, Waiting }

[System.Serializable]
public struct Cost
{
    public int wood;
    public int stone;
    public Cost(int wood, int stone) { this.wood = wood; this.stone = stone; }
    public bool IsFree => wood == 0 && stone == 0;
    public override string ToString()
    {
        if (IsFree) return "Free";
        if (wood > 0 && stone > 0) return wood + " Wood  " + stone + " Stone";
        return wood > 0 ? wood + " Wood" : stone + " Stone";
    }
}

public static class ResourceInfo
{
    public static string Name(ResourceKind k)
    {
        switch (k) { case ResourceKind.Stone: return "Stone"; case ResourceKind.Wood: return "Wood"; default: return "-"; }
    }

    public static Color Tint(ResourceKind k)
    {
        switch (k)
        {
            case ResourceKind.Stone: return new Color(0.78f, 0.84f, 0.95f);
            case ResourceKind.Wood: return new Color(0.92f, 0.70f, 0.42f);
            default: return Color.white;
        }
    }

    public static string JobTitle(ResourceKind k)
    {
        switch (k) { case ResourceKind.Stone: return "Miner"; case ResourceKind.Wood: return "Woodcutter"; default: return "Worker"; }
    }

    public static BuildingRole CampFor(ResourceKind k)
    {
        switch (k) { case ResourceKind.Stone: return BuildingRole.StoneCamp; case ResourceKind.Wood: return BuildingRole.WoodCamp; default: return BuildingRole.Generic; }
    }
}

/// <summary>Shared sorting-layer names (they already exist in TagManager).</summary>
public static class Layers
{
    public const string Ground = "Tiles";
    public const string Objects = "Default";
    public const string Workers = "Worker";
    public const string Roof = "Dach";
    public const string UI = "UI";
}
