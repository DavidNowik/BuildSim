using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the overworld sprites as real PNG assets (Assets/Art/Generated, prefix "ow_"):
/// land body / rim / gloss / rings, soft cloud puff, plateau, settlement hex, path dash, name plates,
/// moon phases, pearl and the 8 modifier icons. Replace any of them by hand-drawn art later (keep the file name).
/// </summary>
public static class OverworldArt
{
    public static readonly string[] ModifierIconNames =
        { "mod_veins", "mod_woods", "mod_lakes", "mod_dunes", "mod_blessing", "mod_storm", "mod_ruins", "mod_calm" };

    private static readonly Color32 Outline = new Color32(36, 34, 52, 255);

    public static void Run()
    {
        // ---- lands (bilinear: glossy, smooth; ~107 pixels per world unit => body is 1.2 units wide)
        ArtGenerator.WriteSprite("ow_node_body", RadialBody(128), 107, default(Vector4), FilterMode.Bilinear);
        ArtGenerator.WriteSprite("ow_node_rim", Disc(144, Color.white), 107, default(Vector4), FilterMode.Bilinear);
        ArtGenerator.WriteSprite("ow_node_gloss", Gloss(), 107, default(Vector4), FilterMode.Bilinear);
        ArtGenerator.WriteSprite("ow_ring", Ring(160, 6), 107, default(Vector4), FilterMode.Bilinear);
        ArtGenerator.WriteSprite("ow_select", Ring(176, 10), 107, default(Vector4), FilterMode.Bilinear);

        // ---- sky + decor
        ArtGenerator.WriteSprite("ow_cloud", Puff(128), 64, default(Vector4), FilterMode.Bilinear);
        ArtGenerator.WriteSprite("ow_plateau", Plateau(), 30, default(Vector4), FilterMode.Bilinear);
        ArtGenerator.WriteSprite("ow_hex", Hex(96), 107, default(Vector4), FilterMode.Bilinear);
        WriteDash();

        // ---- HUD icons
        ArtGenerator.WriteSprite("icon_pearl", Pearl(), 16, default(Vector4), FilterMode.Bilinear);
        for (int i = 0; i < 5; i++) ArtGenerator.WriteSprite("moon_" + i, Moon(i / 4f), 16, default(Vector4), FilterMode.Bilinear);

        // ---- modifier icons
        var painters = new System.Func<Texture2D>[] { PaintVeins, PaintWoods, PaintLakes, PaintDunes, PaintBlessing, PaintStorm, PaintRuins, PaintCalm };
        for (int i = 0; i < painters.Length; i++) ArtGenerator.WriteSprite(ModifierIconNames[i], painters[i](), 48, default(Vector4), FilterMode.Bilinear);

        Debug.Log("[Dev] OverworldArt: overworld sprites written to " + ArtGenerator.SpriteDir);
    }

    // ---------------------------------------------------------------- dash texture (wrap = repeat)
    private static void WriteDash()
    {
        var t = ArtGenerator.NewTex(32, 8);
        var px = new Color32[32 * 8];
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 32; x++)
            {
                bool on = x < 17;
                float edge = Mathf.Min(y, 7 - y) / 3f;
                px[y * 32 + x] = on ? new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(0.4f + edge))) : new Color32(255, 255, 255, 0);
            }
        t.SetPixels32(px);
        string path = ArtGenerator.SpriteDir + "/ow_dash.png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Default;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.filterMode = FilterMode.Bilinear;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
    }

    // ---------------------------------------------------------------- shapes
    private static Texture2D RadialBody(int n)
    {
        var t = ArtGenerator.NewTex(n, n);
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f, r = n * 0.5f - 1f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - c) / r, dy = (y - c) / r, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) continue;
                // grey radial shading, highlight up-left (tinted per land at runtime by multiplication)
                float hx = (x - (c - r * 0.3f)) / r, hy = (y - (c + r * 0.3f)) / r;
                float hl = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) * 0.95f);
                float v = Mathf.Lerp(0.55f, 1f, hl);
                float a = Mathf.Clamp01((1f - d) * r * 0.9f);
                px[y * n + x] = new Color(v, v, v, a);
            }
        t.SetPixels32(px);
        return t;
    }

    private static Texture2D Disc(int n, Color col)
    {
        var t = ArtGenerator.NewTex(n, n);
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f, r = n * 0.5f - 1f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (a > 0f) px[y * n + x] = new Color(col.r, col.g, col.b, a);
            }
        t.SetPixels32(px);
        return t;
    }

    private static Texture2D Ring(int n, float thickness)
    {
        var t = ArtGenerator.NewTex(n, n);
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f, r = n * 0.5f - 1f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(Mathf.Min(r - d, d - (r - thickness)) + 0.5f);
                if (a > 0f) px[y * n + x] = new Color(1, 1, 1, a);
            }
        t.SetPixels32(px);
        return t;
    }

    private static Texture2D Gloss()
    {
        const int w = 64, h = 32;
        var t = ArtGenerator.NewTex(w, h);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - w / 2f) / (w / 2f), dy = (y - h / 2f) / (h / 2f), d = dx * dx + dy * dy;
                if (d > 1f) continue;
                px[y * w + x] = new Color(1, 1, 1, (1f - d) * 0.65f);
            }
        t.SetPixels32(px);
        return t;
    }

    private static Texture2D Puff(int n)
    {
        var t = ArtGenerator.NewTex(n, n);
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f, r = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                if (d >= 1f) continue;
                float a = Mathf.Pow(1f - d, 1.6f);
                px[y * n + x] = new Color(1, 1, 1, a);
            }
        t.SetPixels32(px);
        return t;
    }

    private static Texture2D Plateau()
    {
        // 480 x 190 px, 30 px per unit => 16 x 6.3 units. A broad mesa that rises to a flat summit.
        const int w = 480, h = 190;
        var t = ArtGenerator.NewTex(w, h);
        var px = new Color32[w * h];
        Color top = new Color32(122, 105, 189, 255), bot = new Color32(42, 34, 82, 255), rim = new Color32(226, 217, 255, 255);
        for (int x = 0; x < w; x++)
        {
            float u = Mathf.Abs((x - (w - 1) * 0.5f) / (w * 0.5f));                          // 0 centre, 1 edge
            float prof = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Pow(u, 4.2f)), 0.38f);          // steep shoulder, flat summit
            int topY = Mathf.RoundToInt(prof * (h - 6)) + 2;
            for (int y = 0; y < topY && y < h; y++)
            {
                float k = y / (float)Mathf.Max(1, topY);
                Color c = Color.Lerp(bot, top, k);
                float edge = topY - y;
                if (edge < 3f) c = Color.Lerp(c, rim, 0.85f - edge * 0.2f);
                float shade = 0.94f + 0.06f * Mathf.PerlinNoise(x * 0.07f, y * 0.07f);
                c.r *= shade; c.g *= shade; c.b *= shade;
                c.a = 0.95f;
                px[y * w + x] = c;
            }
        }
        t.SetPixels32(px);
        return t;
    }

    private static Texture2D Hex(int n)
    {
        var t = ArtGenerator.NewTex(n, n);
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f, r = n * 0.5f - 3f;
        var pts = new Vector2[6];
        for (int i = 0; i < 6; i++) { float a = Mathf.PI / 3f * i + Mathf.PI / 6f; pts[i] = new Vector2(c + Mathf.Cos(a) * r, c + Mathf.Sin(a) * r); }
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(x, y);
                if (!Inside(pts, p)) continue;
                float edge = EdgeDist(pts, p);
                if (edge < 3f) px[y * n + x] = new Color32(207, 195, 255, 255);
                else px[y * n + x] = new Color32(29, 23, 69, 230);
            }
        t.SetPixels32(px);
        return t;
    }

    private static Texture2D Pearl()
    {
        const int n = 32;
        var t = ArtGenerator.NewTex(n, n);
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f, r = n * 0.5f - 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > r) continue;
                float hx = x - (c - r * 0.35f), hy = y - (c + r * 0.35f);
                float hl = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / (r * 1.1f));
                Color col = Color.Lerp(new Color32(150, 165, 200, 255), new Color32(255, 250, 245, 255), hl);
                if (d > r - 1.6f) col = Outline;
                px[y * n + x] = col;
            }
        t.SetPixels32(px);
        return t;
    }

    private static Texture2D Moon(float phase)
    {
        const int n = 48;
        var t = ArtGenerator.NewTex(n, n);
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f, r = n * 0.5f - 3f;
        Color lit = new Color32(252, 242, 200, 255), dark = new Color32(58, 56, 104, 255);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float nx = (x - c) / r, ny = (y - c) / r, d2 = nx * nx + ny * ny;
                if (d2 > 1f) continue;
                float term = Mathf.Cos(Mathf.PI * phase) * Mathf.Sqrt(1f - ny * ny);
                bool isLit = nx > term;
                Color col = isLit ? lit : dark;
                if (d2 > 0.86f) col = Color.Lerp(col, Outline, 0.85f);
                px[y * n + x] = col;
            }
        t.SetPixels32(px);
        return t;
    }

    // ---------------------------------------------------------------- modifier icons (32x32 drawn with primitives, then outlined)
    private static Texture2D Canvas32() { return ArtGenerator.NewTex(32, 32); }

    private static Texture2D Finish(Texture2D t)
    {
        // 1px dark outline around everything that was drawn, upscaled x1.5 for a bigger sprite
        var src = t.GetPixels32();
        var dst = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                int i = y * 32 + x;
                if (src[i].a > 0) { dst[i] = src[i]; continue; }
                bool near = false;
                for (int oy = -1; oy <= 1 && !near; oy++)
                    for (int ox = -1; ox <= 1 && !near; ox++)
                    {
                        int xx = x + ox, yy = y + oy;
                        if (xx < 0 || yy < 0 || xx >= 32 || yy >= 32) continue;
                        if (src[yy * 32 + xx].a > 0) near = true;
                    }
                if (near) dst[i] = Outline;
            }
        t.SetPixels32(dst);
        return t;
    }

    private static void FillPoly(Texture2D t, Vector2[] pts, Color col)
    {
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                if (Inside(pts, new Vector2(x + 0.5f, y + 0.5f))) t.SetPixel(x, y, col);
    }

    private static void FillDisc(Texture2D t, float cx, float cy, float r, Color col)
    {
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r) t.SetPixel(x, y, col);
    }

    private static void Erase(Texture2D t, float cx, float cy, float r)
    {
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r) t.SetPixel(x, y, new Color(0, 0, 0, 0));
    }

    private static void Line(Texture2D t, Vector2 a, Vector2 b, float w, Color col)
    {
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f), ab = b - a;
                float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                if (Vector2.Distance(p, a + ab * k) <= w * 0.5f) t.SetPixel(x, y, col);
            }
    }

    private static Texture2D PaintVeins()
    {
        var t = Canvas32();
        Line(t, new Vector2(7, 5), new Vector2(22, 20), 3.2f, new Color32(168, 108, 62, 255));          // handle
        Vector2 prev = new Vector2(6, 22);
        foreach (var p in new[] { new Vector2(10, 26), new Vector2(17, 28), new Vector2(24, 25), new Vector2(28, 18) })
        { Line(t, prev, p, 3.6f, new Color32(190, 205, 230, 255)); prev = p; }                              // pick head
        return Finish(t);
    }

    private static Texture2D PaintWoods()
    {
        var t = Canvas32();
        FillPoly(t, new[] { new Vector2(16, 29), new Vector2(26, 18), new Vector2(6, 18) }, new Color32(70, 190, 100, 255));
        FillPoly(t, new[] { new Vector2(16, 22), new Vector2(28, 9), new Vector2(4, 9) }, new Color32(50, 160, 84, 255));
        FillPoly(t, new[] { new Vector2(14, 9), new Vector2(18, 9), new Vector2(18, 2), new Vector2(14, 2) }, new Color32(150, 98, 58, 255));
        return Finish(t);
    }

    private static Texture2D PaintLakes()
    {
        var t = Canvas32();
        for (int k = 0; k < 3; k++)
        {
            float y0 = 8 + k * 8;
            Vector2 prev = new Vector2(3, y0);
            for (int x = 4; x <= 29; x += 1)
            {
                var p = new Vector2(x, y0 + Mathf.Sin(x * 0.55f + k) * 2.2f);
                Line(t, prev, p, 3f, k == 1 ? new Color32(120, 215, 255, 255) : new Color32(70, 175, 245, 255));
                prev = p;
            }
        }
        return Finish(t);
    }

    private static Texture2D PaintDunes()
    {
        var t = Canvas32();
        FillDisc(t, 24, 24, 5f, new Color32(255, 214, 80, 255));
        for (int x = 2; x < 30; x++)
        {
            int h1 = Mathf.RoundToInt(10 + 4 * Mathf.Sin(x * 0.28f));
            int h2 = Mathf.RoundToInt(7 + 3 * Mathf.Sin(x * 0.35f + 2f));
            for (int y = 3; y < h1; y++) t.SetPixel(x, y, new Color32(238, 190, 108, 255));
            for (int y = 3; y < h2; y++) t.SetPixel(x, y, new Color32(206, 150, 82, 255));
        }
        return Finish(t);
    }

    private static Texture2D PaintBlessing()
    {
        var t = Canvas32();
        FillDisc(t, 15, 16, 12f, new Color32(238, 220, 255, 255));
        Erase(t, 21, 19, 10f);
        FillDisc(t, 24, 25, 2.2f, new Color32(255, 240, 160, 255));
        FillDisc(t, 27, 12, 1.6f, new Color32(255, 240, 160, 255));
        return Finish(t);
    }

    private static Texture2D PaintStorm()
    {
        var t = Canvas32();
        FillPoly(t, new[]
        {
            new Vector2(19, 30), new Vector2(7, 15), new Vector2(14, 15), new Vector2(11, 2),
            new Vector2(26, 19), new Vector2(18, 19), new Vector2(22, 30)
        }, new Color32(255, 226, 60, 255));
        return Finish(t);
    }

    private static Texture2D PaintRuins()
    {
        var t = Canvas32();
        FillPoly(t, new[] { new Vector2(3, 24), new Vector2(16, 30), new Vector2(29, 24) }, new Color32(240, 170, 205, 255));
        for (int i = 0; i < 3; i++) FillPoly(t, Rect(6 + i * 8, 8, 5, 15), new Color32(250, 215, 232, 255));
        FillPoly(t, Rect(3, 3, 26, 4), new Color32(220, 150, 188, 255));
        return Finish(t);
    }

    private static Texture2D PaintCalm()
    {
        var t = Canvas32();
        // a leaf: lens shape between two arcs, rotated
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float u = ((x + 0.5f - 16f) + (y + 0.5f - 16f)) * 0.7071f, v = ((x + 0.5f - 16f) - (y + 0.5f - 16f)) * 0.7071f;
                float k = 1f - (u / 13f) * (u / 13f);
                if (k > 0 && Mathf.Abs(v) < 8.5f * Mathf.Sqrt(k)) t.SetPixel(x, y, new Color32(140, 220, 90, 255));
            }
        Line(t, new Vector2(6, 6), new Vector2(26, 26), 1.6f, new Color32(70, 150, 60, 255));
        return Finish(t);
    }

    private static Vector2[] Rect(float x, float y, float w, float h)
    {
        return new[] { new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h) };
    }

    // ---------------------------------------------------------------- geometry helpers
    private static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    private static float EdgeDist(Vector2[] poly, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % poly.Length], ab = b - a;
            float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * k));
        }
        return best;
    }
}
