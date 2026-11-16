using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Menu: BuildSim/Setup/1 Generate Art Assets.
/// Writes the project's generated pixel art as real PNG sprites (Assets/Art/Generated) and Tile assets
/// (Assets/Tiles/Generated) so everything is visible in the Project window and can be replaced by hand-drawn
/// art later (keep the file names). Re-running overwrites the pixels but keeps GUIDs, so scene references stay valid.
/// </summary>
public static class ArtGenerator
{
    public const string SpriteDir = "Assets/Art/Generated";
    public const string TileDir = "Assets/Tiles/Generated";


    // ---------------------------------------------------------------- public lookups
    public static Sprite LoadSprite(string name) { return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/" + name + ".png"); }
    public static TileBase LoadTile(string name) { return AssetDatabase.LoadAssetAtPath<TileBase>(TileDir + "/" + name + ".asset"); }

    [MenuItem("BuildSim/Setup/1 Generate Art Assets")]
    public static void Run()
    {
        Directory.CreateDirectory(SpriteDir);
        Directory.CreateDirectory(TileDir);
        AssetDatabase.Refresh();

        // ---- entity / ui sprites (PPU 16 unless noted)
        WriteSprite("shadow", ShadowTex(), 16);
        WriteSprite("ring", RingTex(), 16);
        WriteSprite("glow", GlowTex(), 16);
        WriteSprite("bubble_sleep", Bitmap(new[]
        {
            "..kkkkk..", ".kwwwwwk.", "kwbbbbbwk", "kwwwwbwwk", "kwwwbwwwk", "kwwbwwwwk", "kwbbbbbwk", ".kwwwwwk.", "..kkkkk..",
        }), 16);
        WriteSprite("bubble_wait", Bitmap(new[]
        {
            "..kkkkk..", ".kwwwwwk.", "kwwbbbwwk", "kwwwwbwwk", "kwwwbbwwk", "kwwwwwwwk", "kwwwbwwwk", ".kwwwwwk.", "..kkkkk..",
        }), 16);
        WriteSprite("bubble_alert", Bitmap(new[]
        {
            "..kkkkk..", ".kwwwwwk.", "kwwwrwwwk", "kwwwrwwwk", "kwwwrwwwk", "kwwwwwwwk", "kwwwrwwwk", ".kwwwwwk.", "..kkkkk..",
        }), 16);
        WriteSprite("icon_stone", Bitmap(new[]
        {
            "..kkkk..", ".khhhak.", "khhaaack", "khaaaack", "kaaaccck", ".kcccck.", "..kkkk..", "........",
        }), 16);
        WriteSprite("icon_wood", Bitmap(new[]
        {
            "..kkkk..", ".kmmmnk.", "kmkkmnnk", "kmkkmnnk", "kmmmmnnk", ".kmnnnk.", "..kkkk..", "........",
        }), 16);
        WriteSprite("icon_pop", Bitmap(new[]
        {
            "..kkkk..", ".kyyyyk.", ".kyyyyk.", "..kkkk..", ".kbbbbk.", "kbbbbbbk", "kbbbbbbk", "kkkkkkkk",
        }), 16);
        WriteSprite("panel", PanelTex(), 100, new Vector4(3, 3, 3, 3));
        WriteSprite("bar", RoundedTex(6), 100, new Vector4(2, 2, 2, 2));
        WriteSprite("pixel", SolidTex(1), 1);

        // ---- map tiles (ground, deposits, outlines) come from MapArt; the overlay helper tiles stay here
        var tileSprites = new List<string>();
        MapArt.WriteGround(tileSprites);
        MapArt.WriteCrates();
        WriteTile("GridLine", GridLineTex(), tileSprites);
        WriteTile("Hl_Fill", SolidTex(16), tileSprites);
        WriteTile("Hl_Outline", OutlineTex(), tileSprites);
        WriteTile("Hl_Dot", DotTex(), tileSprites);

        AssetDatabase.Refresh();
        foreach (string name in tileSprites) CreateOrUpdateTile(name);
        AssetDatabase.SaveAssets();
        Debug.Log("[Dev] ArtGenerator: wrote sprites to " + SpriteDir + " and tiles to " + TileDir);
    }

    // ---------------------------------------------------------------- writing + import
    public static void WriteSprite(string name, Texture2D tex, int ppu, Vector4 border = default(Vector4), FilterMode filter = FilterMode.Point, Vector2? pivot = null)
    {
        string path = SpriteDir + "/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        ti.filterMode = filter;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.spriteBorder = border;
        if (pivot.HasValue) { var ts = new TextureImporterSettings(); ti.ReadTextureSettings(ts); ts.spriteAlignment = (int)SpriteAlignment.Custom; ts.spritePivot = pivot.Value; ti.SetTextureSettings(ts); }
        ti.SaveAndReimport();
    }

    public static void WriteTile(string name, Texture2D tex, List<string> list, Vector2? pivot = null)
    {
        WriteSprite("tile_" + name, tex, 16, default(Vector4), FilterMode.Point, pivot);
        list.Add(name);
    }

    private static void CreateOrUpdateTile(string name)
    {
        string path = TileDir + "/" + name + ".asset";
        var sprite = LoadSprite("tile_" + name);
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        bool isNew = tile == null;
        if (isNew) tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        // highlight tiles are tinted per cell at runtime, so their colour must not be locked
        tile.flags = name.StartsWith("Hl_") ? TileFlags.None : TileFlags.LockTransform;
        if (isNew) AssetDatabase.CreateAsset(tile, path);
        else EditorUtility.SetDirty(tile);
    }

    // ---------------------------------------------------------------- texture helpers
    public static Texture2D NewTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        var clear = new Color32[w * h];
        t.SetPixels32(clear);
        return t;
    }

    private static Texture2D SolidTex(int n)
    {
        var t = NewTex(n, n);
        var px = new Color32[n * n];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        t.SetPixels32(px);
        return t;
    }

    private static float Rand(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }

    private static readonly Dictionary<char, Color32> palette = new Dictionary<char, Color32>
    {
        { 'k', new Color32(36, 34, 52, 255) }, { 'w', new Color32(250, 248, 240, 255) },
        { 'r', new Color32(222, 70, 70, 255) }, { 'b', new Color32(70, 120, 214, 255) },
        { 'y', new Color32(244, 200, 64, 255) }, { 'g', new Color32(96, 190, 96, 255) },
        { 'a', new Color32(150, 160, 178, 255) }, { 'h', new Color32(206, 214, 228, 255) },
        { 'c', new Color32(104, 112, 132, 255) }, { 'n', new Color32(160, 104, 60, 255) },
        { 'm', new Color32(206, 150, 90, 255) }, { 'd', new Color32(104, 64, 38, 255) },
    };

    private static Texture2D Bitmap(string[] rows)
    {
        int h = rows.Length, w = rows[0].Length;
        var t = NewTex(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char ch = rows[h - 1 - y][x];
                Color32 c;
                if (ch != '.' && palette.TryGetValue(ch, out c)) t.SetPixel(x, y, c);
            }
        return t;
    }

    // ---------------------------------------------------------------- entity textures
    private static Texture2D ShadowTex()
    {
        const int w = 32, h = 16;
        var t = NewTex(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
                float d = dx * dx + dy * dy;
                if (d > 1f) continue;
                t.SetPixel(x, y, new Color(0, 0, 0, d < 0.45f ? 1f : 0.55f));
            }
        return t;
    }

    private static Texture2D RingTex()
    {
        const int w = 16, h = 9;
        var t = NewTex(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
                float d = dx * dx + dy * dy;
                if (d > 0.62f && d <= 1f) t.SetPixel(x, y, Color.white);
            }
        return t;
    }

    private static Texture2D GlowTex()
    {
        const int n = 32;
        var t = NewTex(n, n);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - n / 2f) / (n / 2f), dy = (y + 0.5f - n / 2f) / (n / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d >= 1f) continue;
                float a = Mathf.Pow(1f - d, 2f);
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Round(a * 5f) / 5f));
            }
        return t;
    }

    private static Texture2D PanelTex()
    {
        const int n = 12;
        var t = NewTex(n, n);
        Color border = new Color32(18, 14, 30, 255), hi = new Color32(104, 116, 160, 255);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int ex = Mathf.Min(x, n - 1 - x), ey = Mathf.Min(y, n - 1 - y);
                if (ex == 0 && ey == 0) continue;
                Color c = Color.white;
                if (ex == 0 || ey == 0) c = border;
                else if (y == n - 2 && ex >= 1) c = hi;
                t.SetPixel(x, y, c);
            }
        return t;
    }

    private static Texture2D RoundedTex(int n)
    {
        var t = NewTex(n, n);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int ex = Mathf.Min(x, n - 1 - x), ey = Mathf.Min(y, n - 1 - y);
                if (ex == 0 && ey == 0) continue;
                t.SetPixel(x, y, Color.white);
            }
        return t;
    }

    private static Texture2D GridLineTex()
    {
        var t = NewTex(16, 16);
        var line = new Color32(255, 255, 255, 70);
        for (int i = 0; i < 16; i++) { t.SetPixel(i, 0, line); t.SetPixel(0, i, line); }
        return t;
    }

    private static Texture2D OutlineTex()
    {
        var t = NewTex(16, 16);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                if (x < 2 || y < 2 || x > 13 || y > 13) t.SetPixel(x, y, Color.white);
        return t;
    }

    private static Texture2D DotTex()
    {
        var t = NewTex(16, 16);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float dx = x - 7.5f, dy = y - 7.5f;
                if (dx * dx + dy * dy <= 12f) t.SetPixel(x, y, Color.white);
            }
        return t;
    }
}
