using System.Collections.Generic;
using UnityEngine;

/// <summary>Layout of one cycle's net of lands. Only positions and links exist; nothing about a land is decided until discovery.</summary>
public class OverworldLayout
{
    public Vector2[] pos;          // world units; index 0 = the start (high ground)
    public string[] names;
    public int[] tier;             // steps away from the start
    public Vector2Int[] edges;
    public List<int>[] adj;
    public float sea0;             // resting sea surface (world y), below the lowest land
    public int Count { get { return pos.Length; } }
}

/// <summary>
/// Deterministic planar net generator (same seed = same layout).
/// The start sits on high ground at (0,0); every other land lies BELOW it (negative y), toward the sea.
/// Links are a Gabriel graph (two lands may connect only if no other land lies between them), so lines never cross
/// and a land is always linked to its nearest neighbours. A spanning tree keeps everything reachable and a few
/// extra short links make loops, so it is a net, not a tree.
/// </summary>
public static class OverworldGen
{
    public const float Unit = 60f;               // layout is authored in "pixels", 60 of them per world unit
    public const int DefaultCount = 34;

    private static readonly string[] Names =
    {
        "Mossy Hollow","Pebble Bay","Whisper Pines","Amber Ridge","Lantern Glade","Frost Hollow","Sunken Quarry","Copper Dunes",
        "Willow Reach","Ember Flats","Starfall Moor","Ivory Cliffs","Bramble Vale","Opal Shore","Thistle Run","Cinder Peak",
        "Marigold Fen","Hushed Grove","Driftwood Isle","Quartz Basin","Fable Meadow","Rustling Wood","Kettle Hill","Glimmer Pond",
        "Wren's Rest","Tumble Stones","Honey Hollow","Saffron Steppe","Nimbus Ledge","Echo Canyon","Plum Orchard","Tidepool Ring",
        "Gilded Marsh","Owl's Perch","Dapple Dell","Moonlit Weir","Fern Terrace","Clover Crown","Hearth Hills","Silt Crossing"
    };

    public static OverworldLayout Generate(int seed, int count = DefaultCount)
    {
        var rng = new System.Random(seed);

        // shuffled names
        var names = new List<string>(Names);
        for (int i = names.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = names[i]; names[i] = names[j]; names[j] = t; }

        // scatter lands below the start (poisson-style: nothing crammed together); the world widens downward
        const float Y0 = 170f, Y1 = 900f;
        var pts = new List<Vector2> { Vector2.zero };
        float minD = 165f;
        while (pts.Count < count)
        {
            for (int tries = 0; tries < 500 && pts.Count < count; tries++)
            {
                float y = Y0 + Mathf.Pow((float)rng.NextDouble(), 0.9f) * (Y1 - Y0);
                float halfW = 300f + ((y - Y0) / (Y1 - Y0)) * 420f;
                float x = ((float)rng.NextDouble() * 2f - 1f) * halfW;
                var p = new Vector2(x, y);
                bool ok = true;
                for (int k = 0; k < pts.Count && ok; k++) if (Vector2.Distance(p, pts[k]) < minD) ok = false;
                if (ok) pts.Add(p);
            }
            minD *= 0.94f;
        }

        int n = pts.Count;
        float D(int a, int b) { return Vector2.Distance(pts[a], pts[b]); }

        // Gabriel graph
        var gab = new List<(int a, int b, float len)>();
        for (int a = 0; a < n; a++)
            for (int b = a + 1; b < n; b++)
            {
                Vector2 mid = (pts[a] + pts[b]) * 0.5f;
                float rad = D(a, b) * 0.5f;
                bool ok = true;
                for (int k = 0; k < n && ok; k++)
                {
                    if (k == a || k == b) continue;
                    if (Vector2.Distance(pts[k], mid) < rad * 0.999f) ok = false;
                }
                if (ok) gab.Add((a, b, D(a, b)));
            }
        gab.Sort((p, q) => p.len.CompareTo(q.len));

        // spanning tree (Kruskal)
        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }

        var edges = new List<Vector2Int>();
        var deg = new int[n];
        var used = new HashSet<long>();
        long Key(int a, int b) { return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }
        void Add(int a, int b) { edges.Add(new Vector2Int(a, b)); deg[a]++; deg[b]++; used.Add(Key(a, b)); }

        foreach (var e in gab)
        {
            int ra = Find(e.a), rb = Find(e.b);
            if (ra != rb) { parent[ra] = rb; Add(e.a, e.b); }
        }

        // short extra links make loops (never more than 3 per land, never a long stretch)
        var nearest = new float[n];
        for (int i = 0; i < n; i++)
        {
            float m = float.MaxValue;
            for (int j = 0; j < n; j++) if (j != i) m = Mathf.Min(m, D(i, j));
            nearest[i] = m;
        }
        foreach (var e in gab)
        {
            if (used.Contains(Key(e.a, e.b))) continue;
            if (deg[e.a] >= 3 || deg[e.b] >= 3) continue;
            if (e.len > 1.55f * Mathf.Max(nearest[e.a], nearest[e.b])) continue;
            if (rng.NextDouble() < 0.6) Add(e.a, e.b);
        }

        // tier = steps from the start
        var adj = new List<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();
        foreach (var e in edges) { adj[e.x].Add(e.y); adj[e.y].Add(e.x); }
        var tier = new int[n];
        var seen = new bool[n];
        var q = new Queue<int>();
        q.Enqueue(0); seen[0] = true;
        while (q.Count > 0)
        {
            int c = q.Dequeue();
            foreach (int nb in adj[c]) if (!seen[nb]) { seen[nb] = true; tier[nb] = tier[c] + 1; q.Enqueue(nb); }
        }

        float maxY = 0f;
        for (int i = 0; i < n; i++) maxY = Mathf.Max(maxY, pts[i].y);

        var layout = new OverworldLayout
        {
            pos = new Vector2[n], names = new string[n], tier = tier, edges = edges.ToArray(), adj = adj,
            sea0 = -(maxY + 190f) / Unit
        };
        for (int i = 0; i < n; i++)
        {
            layout.pos[i] = new Vector2(pts[i].x / Unit, -pts[i].y / Unit);   // screen-down becomes world-down
            layout.names[i] = names[i % names.Count];
        }
        return layout;
    }
}
