using UnityEngine;

/// <summary>
/// The Miner prefab's component. All behaviour lives in <see cref="Worker"/>; a Miner is simply a Worker
/// that gathers stone. (Woodcutters are the same prefab configured to gather wood.)
/// </summary>
public class Miner : Worker
{
    protected override void Awake()
    {
        gatherKind = ResourceKind.Stone;
        base.Awake();
    }
}
