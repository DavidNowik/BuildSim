using System;
using UnityEngine;

/// <summary>The colony stockpile. Everything that changes a resource goes through here (HUD listens to Changed).</summary>
public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance;

    [Header("Starting stockpile")]
    public int startWood = 50;
    public int startStone = 10;

    /// <summary>kind, new amount, delta</summary>
    public event Action<ResourceKind, int, int> Changed;

    private int stone, wood;
    public int TotalDelivered(ResourceKind k) { return k == ResourceKind.Stone ? deliveredStone : deliveredWood; }
    private int deliveredStone, deliveredWood;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        stone = startStone;
        wood = startWood;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public int Get(ResourceKind k)
    {
        return k == ResourceKind.Stone ? stone : k == ResourceKind.Wood ? wood : 0;
    }

    public void Add(ResourceKind k, int amount, bool fromWorker = false)
    {
        if (amount == 0 || k == ResourceKind.None) return;
        if (k == ResourceKind.Stone) { stone += amount; if (fromWorker) deliveredStone += amount; }
        else { wood += amount; if (fromWorker) deliveredWood += amount; }
        if (Changed != null) Changed(k, Get(k), amount);
    }

    public bool CanAfford(Cost c) { return wood >= c.wood && stone >= c.stone; }

    public bool Spend(Cost c)
    {
        if (!CanAfford(c)) return false;
        Add(ResourceKind.Wood, -c.wood);
        Add(ResourceKind.Stone, -c.stone);
        return true;
    }

    public void Refund(Cost c, float fraction)
    {
        Add(ResourceKind.Wood, Mathf.FloorToInt(c.wood * fraction));
        Add(ResourceKind.Stone, Mathf.FloorToInt(c.stone * fraction));
    }

    // ------------------------------------------------------------ save / load
    public void WriteSave(LevelSave s)
    {
        s.wood = wood; s.stone = stone;
        s.deliveredWood = deliveredWood; s.deliveredStone = deliveredStone;
    }

    public void ReadSave(LevelSave s)
    {
        deliveredWood = s.deliveredWood; deliveredStone = s.deliveredStone;
        int dw = s.wood - wood, ds = s.stone - stone;
        wood = s.wood; stone = s.stone;
        if (Changed != null)
        {
            Changed(ResourceKind.Wood, wood, dw);
            Changed(ResourceKind.Stone, stone, ds);
        }
    }
}
