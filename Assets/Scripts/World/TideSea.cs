using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The sea at the bottom of the overworld: a mesh (vertex colours, no texture) with a RAGGED surface.
/// The edge is never a straight line: slow bulges, mid-size surges, fine spikes, broken foam and foam streaks
/// hanging down into the water, all moving with time. The controller only sets the surface height,
/// the wave amplitude and how much foam there is.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TideSea : MonoBehaviour
{
    [Header("Shape")]
    public float halfWidth = 45f;
    public float depth = 70f;
    public int columns = 260;

    [Header("Colours")]
    public Color surfaceColor = new Color(0.19f, 0.55f, 0.70f, 0.62f);
    public Color midColor = new Color(0.04f, 0.20f, 0.36f, 0.84f);
    public Color deepColor = new Color(0.012f, 0.063f, 0.133f, 0.96f);
    public Color foamColor = new Color(0.87f, 0.93f, 0.96f, 1f);

    public float Surface { get { return surface; } }

    private float surface, amp = 0.1f, foam = 0.12f;
    private Mesh mesh;
    private readonly List<Vector3> verts = new List<Vector3>();
    private readonly List<Color> cols = new List<Color>();
    private readonly List<int> tris = new List<int>();

    private static Color V(Color c) { return QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c; }

    private void Awake()
    {
        mesh = new Mesh { name = "TideSea (runtime)" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    private void OnDestroy() { if (mesh != null) Destroy(mesh); }

    public void Set(float surfaceY, float amplitude, float foamAmount)
    {
        surface = surfaceY; amp = amplitude; foam = foamAmount;
    }

    /// <summary>Height of the ragged crest at a world x (used to spawn spray exactly on the edge).</summary>
    public float CrestY(float x)
    {
        float t = Time.time;
        float big = (Mathf.PerlinNoise(x * 0.11f + t * 0.18f, 3.1f) - 0.5f) * 2f;     // slow bulges
        float mid = (Mathf.PerlinNoise(x * 0.45f - t * 0.50f, 7.7f) - 0.5f) * 2f;     // surges
        float fine = (Mathf.PerlinNoise(x * 2.2f + t * 1.30f, 11.3f) - 0.5f) * 2f;    // spikes
        float swell = Mathf.Sin(x * 0.6f + t * 1.2f);
        float tendril = Mathf.Pow(Mathf.Clamp01(Mathf.PerlinNoise(x * 0.9f + t * 0.8f, 19.7f) - 0.45f) * 1.8f, 2f);
        return surface + amp * (1.0f * big + 0.55f * mid + 0.25f * fine * foam + 0.25f * swell) + amp * foam * 1.1f * tendril;
    }

    private void LateUpdate() { Rebuild(); }

    private void Rebuild()
    {
        verts.Clear(); cols.Clear(); tris.Clear();
        int n = Mathf.Max(8, columns);
        float t = Time.time;
        float bottom = surface - depth;

        // ---- water body: 3 rows per column (crest, mid, deep)
        for (int i = 0; i <= n; i++)
        {
            float x = Mathf.Lerp(-halfWidth, halfWidth, i / (float)n);
            float y = CrestY(x);
            verts.Add(new Vector3(x, y, 0f)); cols.Add(V(surfaceColor));
            verts.Add(new Vector3(x, y - 2.4f, 0f)); cols.Add(V(midColor));
            verts.Add(new Vector3(x, Mathf.Min(bottom, y - 8f), 0f)); cols.Add(V(deepColor));
        }
        for (int i = 0; i < n; i++)
        {
            int a = i * 3, b = (i + 1) * 3;
            Quad(a, a + 1, b, b + 1);
            Quad(a + 1, a + 2, b + 1, b + 2);
        }

        // ---- foam: broken, ragged bands riding the crest
        float f = Mathf.Clamp01(foam);
        if (f > 0.02f)
        {
            AddFoamBand(n, t, f, 0.00f, 1.00f, 0.9f, 0.0f);     // main crest, sits on top
            AddFoamBand(n, t, f, -0.30f, 0.60f, 0.5f, 37.1f);   // second ragged band behind it
            AddStreaks(n, t, f);
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.bounds = new Bounds(new Vector3(0f, surface - depth * 0.5f, 0f), new Vector3(halfWidth * 2f, depth * 2f, 1f));
    }

    private void AddFoamBand(int n, float t, float f, float yOffset, float thicknessScale, float alphaScale, float noiseSeed)
    {
        int start = verts.Count;
        for (int i = 0; i <= n; i++)
        {
            float x = Mathf.Lerp(-halfWidth, halfWidth, i / (float)n);
            float y = CrestY(x) + yOffset * f;
            float cover = Mathf.PerlinNoise(x * 0.35f - t * 0.9f + noiseSeed, 5.3f);                 // where foam exists at all
            float th = f * thicknessScale * (0.10f + 0.65f * Mathf.PerlinNoise(x * 0.9f + t * 0.7f + noiseSeed, 1.9f));
            th *= cover > 0.33f ? 1f : 0.15f;                                                          // gaps: the line is broken
            float a = Mathf.Clamp01(0.25f + Mathf.PerlinNoise(x * 1.4f + t * 1.1f + noiseSeed, 8.8f)) * alphaScale * Mathf.Min(1f, f * 1.4f);
            Color c = V(foamColor); c.a = a;
            Color c0 = c; c0.a *= 0.35f;
            verts.Add(new Vector3(x, y - 0.08f, 0f)); cols.Add(c0);
            verts.Add(new Vector3(x, y + th, 0f)); cols.Add(c);
        }
        for (int i = 0; i < n; i++) { int a = start + i * 2, b = start + (i + 1) * 2; Quad(a, a + 1, b, b + 1); }
    }

    private void AddStreaks(int n, float t, float f)
    {
        // foam streaks trailing down from the crest into the water
        int count = Mathf.RoundToInt(Mathf.Lerp(4f, 40f, f));
        for (int s = 0; s < count; s++)
        {
            float u = (s * 0.6180339f + 0.13f) % 1f;
            float x = Mathf.Lerp(-halfWidth, halfWidth, u);
            float life = Mathf.PerlinNoise(s * 3.7f, t * 0.35f);
            if (life < 0.4f) continue;
            float len = (0.5f + 1.6f * life) * f;
            float w = 0.07f + 0.05f * life;
            float drift = Mathf.Sin(t * 0.8f + s) * 0.25f;
            float y = CrestY(x);
            int v = verts.Count;
            Color c = V(foamColor); c.a = 0.30f * life * f;
            Color c1 = V(foamColor); c1.a = 0f;
            verts.Add(new Vector3(x - w, y, 0f)); cols.Add(c);
            verts.Add(new Vector3(x + w, y, 0f)); cols.Add(c);
            verts.Add(new Vector3(x + drift, y - len, 0f)); cols.Add(c1);
            tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
        }
    }

    private void Quad(int a0, int a1, int b0, int b1)
    {
        // a0 top-left, a1 bottom-left, b0 top-right, b1 bottom-right (clockwise for a camera looking down -z)
        tris.Add(a0); tris.Add(b0); tris.Add(a1);
        tris.Add(b0); tris.Add(b1); tris.Add(a1);
    }
}
