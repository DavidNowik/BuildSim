using UnityEngine;

public enum ResourceKind { None = 0, Stone = 1, Wood = 2, Fish = 3 }

/// <summary>Build menu layers (HUD build bar tabs).</summary>
public enum BuildCategory { Materials = 0, Food = 1, Water = 2, Power = 3 }

public enum BuildingRole { Generic = 0, Storage = 1, StoneCamp = 2, WoodCamp = 3, FishCamp = 4, House = 5 }

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
        switch (k) { case ResourceKind.Stone: return "Stone"; case ResourceKind.Wood: return "Wood"; case ResourceKind.Fish: return "Fish"; default: return "-"; }
    }

    public static Color Tint(ResourceKind k)
    {
        switch (k)
        {
            case ResourceKind.Stone: return new Color(0.78f, 0.84f, 0.95f);
            case ResourceKind.Wood: return new Color(0.92f, 0.70f, 0.42f);
            case ResourceKind.Fish: return new Color(0.62f, 0.88f, 1f);
            default: return Color.white;
        }
    }

    public static string JobTitle(ResourceKind k)
    {
        switch (k) { case ResourceKind.Stone: return "Miner"; case ResourceKind.Wood: return "Woodcutter"; case ResourceKind.Fish: return "Fisher"; default: return "Worker"; }
    }

    public static ResourceKind KindFor(BuildingRole r)
    {
        switch (r) { case BuildingRole.StoneCamp: return ResourceKind.Stone; case BuildingRole.WoodCamp: return ResourceKind.Wood; case BuildingRole.FishCamp: return ResourceKind.Fish; default: return ResourceKind.None; }
    }

    public static BuildingRole CampFor(ResourceKind k)
    {
        switch (k) { case ResourceKind.Stone: return BuildingRole.StoneCamp; case ResourceKind.Wood: return BuildingRole.WoodCamp; case ResourceKind.Fish: return BuildingRole.FishCamp; default: return BuildingRole.Generic; }
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
