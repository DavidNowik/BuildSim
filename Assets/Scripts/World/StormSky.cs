using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The sky of the overworld. Everything it animates is a real object under the camera:
/// a gradient quad, drifting cloud puffs (they gather and darken as "dread" rises), a lightning flash,
/// plus the global post-processing volume (colours drain). The controller only calls SetDread / Flash.
/// </summary>
public class StormSky : MonoBehaviour
{
    [Serializable]
    public class Cloud
    {
        public Transform root;
        public SpriteRenderer[] puffs;
        [Range(0f, 1f)] public float x01;
        [Range(0f, 1f)] public float yFrac;
        public float speed = 0.4f;
        public float size = 1f;
    }

    [Header("Scene references")]
    public Camera cam;
    public MeshFilter skyGradient;
    public Cloud[] clouds;
    public SpriteRenderer lightning;
    public Volume volume;

    [Header("Colours")]
    public Color dayTop = new Color(0.14f, 0.07f, 0.33f);
    public Color dayBottom = new Color(0.24f, 0.10f, 0.42f);
    public Color stormTop = new Color(0.02f, 0.04f, 0.10f);
    public Color stormBottom = new Color(0.06f, 0.09f, 0.19f);
    public Color cloudDay = new Color(0.80f, 0.74f, 0.96f);
    public Color cloudStorm = new Color(0.12f, 0.16f, 0.27f);

    /// <summary>0 = calm evening, 1 = storm.</summary>
    public float Dread { get { return dread; } }

    private float dread, dreadTarget, flash;
    private Mesh quad;
    private ColorAdjustments colorAdj;
    private Vignette vignette;

    private void Awake()
    {
        quad = new Mesh { name = "SkyGradient (runtime)" };
        quad.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(1, -1, 0) };
        quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        quad.colors = new Color[4];
        if (skyGradient != null) skyGradient.sharedMesh = quad;
        if (volume != null)
        {
            volume.profile.TryGet(out colorAdj);
            volume.profile.TryGet(out vignette);
        }
    }

    private static Color V(Color c) { return QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c; }

    private void OnDestroy() { if (quad != null) Destroy(quad); }

    public void SetDread(float target) { dreadTarget = Mathf.Clamp01(target); }
    public void SnapDread(float v) { dread = dreadTarget = Mathf.Clamp01(v); }
    public void Flash() { flash = 1f; }

    private void Update()
    {
        dread = Mathf.Lerp(dread, dreadTarget, 1f - Mathf.Exp(-1.2f * Time.unscaledDeltaTime));
        float ortho = cam.orthographicSize, halfW = ortho * cam.aspect;

        // gradient + camera background
        Color top = Color.Lerp(dayTop, stormTop, dread), bottom = Color.Lerp(dayBottom, stormBottom, dread);
        if (skyGradient != null)
        {
            quad.colors = new[] { V(bottom), V(top), V(top), V(bottom) };
            skyGradient.transform.localScale = new Vector3(halfW * 1.05f, ortho * 1.05f, 1f);
        }
        cam.backgroundColor = bottom;

        // clouds: drift, gather and darken
        float span = halfW * 2f + 14f;
        Color cc = Color.Lerp(cloudDay, cloudStorm, dread);
        cc.a = 0.5f + dread * 0.4f;
        float scale = ortho / 9f;
        if (clouds != null)
            foreach (var c in clouds)
            {
                if (c.root == null) continue;
                float x = Mathf.Repeat(c.x01 * span + Time.time * c.speed * (1f + dread * 1.5f), span) - span * 0.5f;
                float y = ortho * (0.88f - c.yFrac * 0.13f - dread * 0.05f);
                c.root.localPosition = new Vector3(x, y, 8f);   // in front of the camera, behind nothing
                c.root.localScale = Vector3.one * (scale * c.size * (1f + dread * 0.4f));
                foreach (var p in c.puffs) if (p != null) p.color = cc;
            }

        // lightning flash
        if (lightning != null)
        {
            flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 3.5f);
            lightning.color = new Color(0.78f, 0.88f, 1f, flash * 0.34f);
            lightning.transform.localScale = new Vector3(halfW * 2.2f, ortho * 1.3f, 1f);
        }

        // colours drain as the tide approaches
        if (colorAdj != null) colorAdj.saturation.value = Mathf.Lerp(10f, -55f, dread);
        if (vignette != null) vignette.intensity.value = Mathf.Lerp(0.26f, 0.5f, dread);
    }
}
