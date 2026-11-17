using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure, deterministic procedural map generator (same settings => identical map on every machine).
/// No scene access; MapGenerator paints the result into Tilemaps.
/// Steps: noise terrain (lakes, dirt) -> cleared start area -> stone/forest clusters
/// (one of each guaranteed near the start) -> remove deposits that cannot be reached.
/// </summary>
public static class MapGen
{
    private const int StartHalfW = 4, StartHalfH = 3; // cleared start area is 9 x 7

    public static MapData Generate(MapSettings s)
    {
        Vector2Int size = s.Size;
        int w = Mathf.Max(16, size.x), h = Mathf.Max(14, size.y);
        var d = new MapData(w, h) { seed = s.seed, tier = s.tier };
        var rng = new System.Random(s.seed);

        // ---------------------------------------------------------------- terrain
        float[] n1 = new float[w * h], n2 = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                n1[y * w + x] = Fbm(x / s.terrainScale, y / s.terrainScale, s.seed);
                n2[y * w + x] = Fbm(x / (s.terrainScale * 0.7f) + 50f, y / (s.terrainScale * 0.7f) + 50f, s.seed + 77);
            }

        float waterThreshold = Percentile(n1, s.waterAmount);
        float dirtThreshold = Percentile(n2, 1f - s.dirtAmount);
        for (int i = 0; i < w * h; i++)
        {
            if (n1[i] < waterThreshold) d.terrain[i] = (byte)TerrainKind.Water;
            else if (n2[i] > dirtThreshold) d.terrain[i] = (byte)TerrainKind.Dirt;
            else d.terrain[i] = (byte)TerrainKind.Grass;
        }

        // ---------------------------------------------------------------- start area
        d.start = FindStart(d);
        Rect clear = ClearRect(d.start);
        for (int y = (int)clear.yMin; y <= (int)clear.yMax; y++)
            for (int x = (int)clear.xMin; x <= (int)clear.xMax; x++)
                if (d.InBounds(x, y)) d.terrain[d.Index(x, y)] = (byte)TerrainKind.Grass;

        // ---------------------------------------------------------------- deposits
        float area = w * h;
        int stoneClusters = Mathf.Max(2, Mathf.RoundToInt(s.stoneAmount * area / 300f));
        int forestClusters = Mathf.Max(2, Mathf.RoundToInt(s.forestAmount * area / 330f));

        PlaceClusters(d, rng, ResourceKind.Stone, stoneClusters, 2, 3, s.stoneYield, s.seed + 1);
        PlaceClusters(d, rng, ResourceKind.Wood, forestClusters, 2, 4, s.woodYield, s.seed + 2);

        // ---------------------------------------------------------------- reachability
        RemoveUnreachable(d);

        // ---------------------------------------------------------------- pearl crates (own random stream: older maps stay identical)
        PlaceCrates(d, new System.Random(s.seed + 9001));
        return d;
    }

    // -------------------------------------------------------------------- start
    private static Rect ClearRect(Vector2Int start)
    {
        return new Rect(start.x - StartHalfW, start.y - StartHalfH, StartHalfW * 2, StartHalfH * 2);
    }

    private static bool InClear(MapData d, int x, int y, int pad = 0)
    {
        Rect r = ClearRect(d.start);
        return x >= r.xMin - pad && x <= r.xMax + pad && y >= r.yMin - pad && y <= r.yMax + pad;
    }

    /// <summary>Spiral outward from the map centre to find a spot where the whole start area is on the map and mostly land.</summary>
    private static Vector2Int FindStart(MapData d)
    {
        Vector2Int c = new Vector2Int(d.width / 2, d.height / 2);
        int maxR = Mathf.Max(d.width, d.height);
        Vector2Int best = c;
        int bestLand = -1;
        for (int r = 0; r < maxR; r++)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int cx = c.x + dx, cy = c.y + dy;
                    if (cx - StartHalfW < 1 || cy - StartHalfH < 1 || cx + StartHalfW > d.width - 2 || cy + StartHalfH > d.height - 2) continue;
                    int land = 0;
                    for (int x = cx - StartHalfW; x <= cx + StartHalfW; x++)
                        for (int y = cy - StartHalfH; y <= cy + StartHalfH; y++)
                            if (d.GetTerrain(x, y) != TerrainKind.Water) land++;
                    int total = (StartHalfW * 2 + 1) * (StartHalfH * 2 + 1);
                    if (land >= total * 0.8f) return new Vector2Int(cx, cy);
                    if (land > bestLand) { bestLand = land; best = new Vector2Int(cx, cy); }
                }
        }
        return best;
    }

    // -------------------------------------------------------------------- clusters
    private static void PlaceClusters(MapData d, System.Random rng, ResourceKind kind, int count, int rMin, int rMax, int yield, int noiseSeed)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2Int center;
            bool near = i == 0; // first cluster of each kind is guaranteed close to the start
            if (!PickCenter(d, rng, near, out center)) continue;
            int radius = rng.Next(rMin, rMax + 1);
            Blob(d, rng, center, radius, kind, yield, noiseSeed + i * 13);
            Strays(d, rng, center, radius + 1, radius + 4, rng.Next(2, 5), kind, yield);   // a few lone trees / rocks around it
        }
    }

    private static bool PickCenter(MapData d, System.Random rng, bool nearStart, out Vector2Int result)
    {
        result = default(Vector2Int);
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int x, y;
            if (nearStart)
            {
                float ang = (float)(rng.NextDouble() * Math.PI * 2);
                float dist = 8f + (float)rng.NextDouble() * 4f;
                x = d.start.x + Mathf.RoundToInt(Mathf.Cos(ang) * dist);
                y = d.start.y + Mathf.RoundToInt(Mathf.Sin(ang) * dist * 0.7f);
            }
            else
            {
                x = rng.Next(2, d.width - 2);
                y = rng.Next(2, d.height - 2);
            }
            if (!d.InBounds(x, y) || !d.IsLand(x, y)) continue;
            if (InClear(d, x, y, 3)) continue;
            result = new Vector2Int(x, y);
            return true;
        }
        return false;
    }

    /// <summary>Single standalone deposits scattered in a ring around a cluster.</summary>
    private static void Strays(MapData d, System.Random rng, Vector2Int c, int rMin, int rMax, int count, ResourceKind kind, int yield)
    {
        for (int n = 0; n < count; n++)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                float ang = (float)(rng.NextDouble() * Math.PI * 2), r = rMin + (float)rng.NextDouble() * (rMax - rMin);
                int x = c.x + Mathf.RoundToInt(Mathf.Cos(ang) * r), y = c.y + Mathf.RoundToInt(Mathf.Sin(ang) * r);
                if (!d.InBounds(x, y) || !d.IsLand(x, y) || InClear(d, x, y, 1)) continue;
                int i = d.Index(x, y);
                if (d.resource[i] != 0) continue;
                d.resource[i] = (byte)kind;
                d.yield[i] = yield;
                break;
            }
        }
    }

    private static void Blob(MapData d, System.Random rng, Vector2Int c, int radius, ResourceKind kind, int yield, int noiseSeed)
    {
        int reach = radius + 2;
        for (int y = c.y - reach; y <= c.y + reach; y++)
            for (int x = c.x - reach; x <= c.x + reach; x++)
            {
                if (!d.InBounds(x, y) || !d.IsLand(x, y)) continue;
                if (InClear(d, x, y, 1)) continue;
                int i = d.Index(x, y);
                if (d.resource[i] != 0) continue;

                float dist = Mathf.Sqrt((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y));
                float wobble = (Hash01(x, y, noiseSeed) - 0.5f) * 1.6f;
                if (dist + wobble > radius) continue;
                // the inside of a cluster is not solid: gaps let the grass show through (no carpet)
                if (dist > radius * 0.45f && Hash01(x, y, noiseSeed + 5) < 0.16f) continue;

                d.resource[i] = (byte)kind;
                d.yield[i] = yield;
            }
    }

    // -------------------------------------------------------------------- reachability
    /// <summary>A few 2x2 crates on free, reachable land away from the start; richer crates are rarer and cost more.</summary>
    private static void PlaceCrates(MapData d, System.Random rng)
    {
        var reach = Reachable(d);
        int count = Mathf.Clamp(d.tier + 1, 1, 12);   // one crate per tier level (the first map, tier 0, has one)
        for (int n = 0; n < count; n++)
        {
            for (int tries = 0; tries < 80; tries++)
            {
                int x = rng.Next(1, d.width - 2), y = rng.Next(1, d.height - 2);
                bool ok = !InClear(d, x, y, 3) && !InClear(d, x + 1, y + 1, 3);
                for (int dx = 0; dx <= 1 && ok; dx++)
                    for (int dy = 0; dy <= 1 && ok; dy++)
                    {
                        int i = d.Index(x + dx, y + dy);
                        if (d.terrain[i] == (byte)TerrainKind.Water || d.resource[i] != 0) ok = false;
                    }
                if (!ok) continue;
                // some free reachable cell next to the crate so it can be approached
                bool access = false;
                for (int dx = -1; dx <= 2; dx++)
                    for (int dy = -1; dy <= 2; dy++)
                    {
                        if (dx >= 0 && dx <= 1 && dy >= 0 && dy <= 1) continue;
                        if (d.InBounds(x + dx, y + dy) && reach[d.Index(x + dx, y + dy)]) access = true;
                    }
                if (!access) continue;
                bool overlap = false;
                foreach (var o in d.crates) if (Mathf.Abs(o.anchor.x - x) < 4 && Mathf.Abs(o.anchor.y - y) < 4) overlap = true;
                if (overlap) continue;

                float roll = (float)rng.NextDouble();
                int variant = roll < 0.6f ? 0 : (roll < 0.9f ? 1 : 2);
                int pearls = variant == 0 ? rng.Next(1, 3) : (variant == 1 ? rng.Next(3, 6) : rng.Next(7, 11));
                int amount = 15 + pearls * 6 + d.tier * 5;
                d.crates.Add(new CrateData { anchor = new Vector2Int(x, y), variant = variant, tag = (int)ProductTag.BuildingMaterial, amount = amount, pearls = pearls });
                break;
            }
        }
    }

    private static bool[] Reachable(MapData d)
    {
        var reach = new bool[d.width * d.height];
        var q = new Queue<Vector2Int>();
        q.Enqueue(d.start);
        reach[d.Index(d.start.x, d.start.y)] = true;
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nx = p.x + dx[k], ny = p.y + dy[k];
                if (!d.InBounds(nx, ny)) continue;
                int ni = d.Index(nx, ny);
                if (reach[ni] || d.terrain[ni] == (byte)TerrainKind.Water || d.resource[ni] != 0) continue;
                reach[ni] = true;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }
        return reach;
    }

    private static void RemoveUnreachable(MapData d)
    {
        // flood fill over free land from the start
        var reach = new bool[d.width * d.height];
        var q = new Queue<Vector2Int>();
        q.Enqueue(d.start);
        reach[d.Index(d.start.x, d.start.y)] = true;
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nx = p.x + dx[k], ny = p.y + dy[k];
                if (!d.InBounds(nx, ny)) continue;
                int ni = d.Index(nx, ny);
                if (reach[ni] || d.terrain[ni] == (byte)TerrainKind.Water || d.resource[ni] != 0) continue;
                reach[ni] = true;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }

        // a deposit stays if a worker can reach it: either it touches reachable ground, or it is connected
        // (through other deposits) to one that does, so the inside of a big forest is mined from the outside in
        var keep = new bool[d.width * d.height];
        var dq = new Queue<Vector2Int>();
        for (int y = 0; y < d.height; y++)
            for (int x = 0; x < d.width; x++)
            {
                int i = d.Index(x, y);
                if (d.resource[i] == 0) continue;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + dx[k], ny = y + dy[k];
                    if (d.InBounds(nx, ny) && reach[d.Index(nx, ny)]) { keep[i] = true; dq.Enqueue(new Vector2Int(x, y)); break; }
                }
            }
        while (dq.Count > 0)
        {
            var p = dq.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nx = p.x + dx[k], ny = p.y + dy[k];
                if (!d.InBounds(nx, ny)) continue;
                int ni = d.Index(nx, ny);
                if (keep[ni] || d.resource[ni] == 0) continue;
                keep[ni] = true;
                dq.Enqueue(new Vector2Int(nx, ny));
            }
        }
        for (int i = 0; i < keep.Length; i++)
            if (d.resource[i] != 0 && !keep[i]) { d.resource[i] = 0; d.yield[i] = 0; }
    }

    // -------------------------------------------------------------------- noise
    private static float Percentile(float[] values, float fraction)
    {
        if (fraction <= 0f) return float.NegativeInfinity;
        var copy = (float[])values.Clone();
        Array.Sort(copy);
        int idx = Mathf.Clamp(Mathf.FloorToInt(copy.Length * fraction), 0, copy.Length - 1);
        return copy[idx];
    }

    public static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }

    private static float ValueNoise(float x, float y, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash01(x0, y0, seed), b = Hash01(x0 + 1, y0, seed);
        float c = Hash01(x0, y0 + 1, seed), e = Hash01(x0 + 1, y0 + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, e, fx), fy);
    }

    /// <summary>Low-frequency 0..1 tone used by the painter to form light/dark grass patches.</summary>
    public static float Tone(int x, int y, int seed)
    {
        return Fbm(x / 13f + 200f, y / 13f + 200f, seed + 555);
    }

    private static float Fbm(float x, float y, int seed)
    {
        float sum = 0f, amp = 0.55f, freq = 1f, norm = 0f;
        for (int o = 0; o < 3; o++)
        {
            sum += ValueNoise(x * freq, y * freq, seed + o * 31) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / norm;
    }
}
