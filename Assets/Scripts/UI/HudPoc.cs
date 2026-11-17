using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The HUD proof of concept, step by step becoming the real HUD. Most buttons are still proxies with ids (A1, A2, ... B1 ...)
/// that only report their id. Built by BuildSim/Setup/10 Build HUD PoC; everything below the PoC canvas is real, editable scene objects.
/// </summary>
public class HudPoc : MonoBehaviour
{
    public Text lastClick;

    /// <summary>Wired (as persistent listener with the id string) to every proxy button.</summary>
    public void OnProxy(string id)
    {
        if (lastClick != null) lastClick.text = "last click: " + id;
        Debug.Log("[Dev] HUD PoC button " + id);
        GameAudio.Play(Sfx.Click, 0.5f, 1.2f);
    }
}
