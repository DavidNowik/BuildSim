using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Warm window light on camps that glows at night (stronger while someone sleeps inside).
/// The Light2D and the door glow are real child objects of the camp prefab (see the prefab hierarchy).
/// </summary>
public class CampLight : MonoBehaviour
{
    public Building building;
    public Light2D light2d;
    public SpriteRenderer glow;

    [Header("Strength")]
    public float lightAtNight = 1.1f;
    public float glowAtNight = 0.22f;

    private void Reset() { building = GetComponent<Building>(); }

    /// <summary>Ghost buildings (placement preview) must not light the scene.</summary>
    public void SetActive(bool on)
    {
        if (light2d != null) light2d.gameObject.SetActive(on);
        if (glow != null) glow.gameObject.SetActive(on);
        enabled = on;
    }

    private void Update()
    {
        if (light2d == null || building == null) return;
        float night = DayCycle.Instance != null ? DayCycle.Instance.Night01 : 0f;
        float lit = building.Sleepers > 0 ? 1f : 0.55f;
        light2d.intensity = night * lightAtNight * lit;
        if (glow != null)
        {
            var c = glow.color;
            c.a = night * glowAtNight * lit;
            glow.color = c;
        }
    }
}
