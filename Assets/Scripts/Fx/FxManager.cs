using System.Collections.Generic;
using UnityEngine;

/// <summary>Tiny pooled pixel-particle system (no shaders/materials needed, so it never breaks in builds).</summary>
public class FxManager : MonoBehaviour
{
    public static FxManager Instance;

    [Tooltip("1x1 white pixel sprite (PPU 1) used for every particle")]
    public Sprite particleSprite;

    private class Particle
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 vel;
        public float life, maxLife, gravity, size;
        public Color color;
        public bool active;
    }

    private readonly List<Particle> pool = new List<Particle>();
    private const int MaxParticles = 256;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private Particle Get()
    {
        for (int i = 0; i < pool.Count; i++)
            if (!pool[i].active) return pool[i];
        if (pool.Count >= MaxParticles) return null;

        var go = new GameObject("p");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = particleSprite;
        sr.sortingLayerName = Layers.UI;
        sr.sortingOrder = -50;
        var p = new Particle { t = go.transform, sr = sr };
        pool.Add(p);
        return p;
    }

    /// <summary>Radial burst of square pixels with gravity.</summary>
    public void Burst(Vector3 pos, Color color, int count, float speed, float gravity = 7f, float life = 0.55f, float size = 0.12f)
    {
        for (int i = 0; i < count; i++)
        {
            var p = Get();
            if (p == null) return;
            float ang = Random.value * Mathf.PI * 2f;
            float spd = speed * Random.Range(0.4f, 1f);
            p.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f + 0.5f) * spd;
            p.gravity = gravity;
            p.maxLife = p.life = life * Random.Range(0.7f, 1.2f);
            p.size = Mathf.Max(1f / 16f, Mathf.Round(size * Random.Range(0.7f, 1.3f) * 16f) / 16f);
            float shade = Random.Range(0.85f, 1.15f);
            p.color = new Color(Mathf.Clamp01(color.r * shade), Mathf.Clamp01(color.g * shade), Mathf.Clamp01(color.b * shade), color.a);
            p.t.position = pos + new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.08f, 0.12f), 0f);
            p.t.localScale = new Vector3(p.size, p.size, 1f);
            p.sr.color = p.color;
            p.sr.enabled = true;
            p.active = true;
        }
    }

    /// <summary>Spark chips thrown from a pick/axe strike (mostly upwards).</summary>
    public void Chips(Vector3 pos, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var p = Get();
            if (p == null) return;
            p.vel = new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(1.4f, 3.2f));
            p.gravity = 9f;
            p.maxLife = p.life = Random.Range(0.3f, 0.5f);
            p.size = 1f / 8f;
            p.color = color;
            p.t.position = pos + new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.05f, 0.1f), 0f);
            p.t.localScale = new Vector3(p.size, p.size, 1f);
            p.sr.color = color;
            p.sr.enabled = true;
            p.active = true;
        }
    }

    /// <summary>Soft dust puff at ground level (e.g. when a building lands).</summary>
    public void Dust(Vector3 center, float width, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var p = Get();
            if (p == null) return;
            float x = Random.Range(-width * 0.5f, width * 0.5f);
            p.vel = new Vector2(x * 1.2f, Random.Range(0.2f, 0.9f));
            p.gravity = -0.5f;
            p.maxLife = p.life = Random.Range(0.4f, 0.7f);
            p.size = Random.Range(2, 4) / 16f;
            float g = Random.Range(0.78f, 0.95f);
            p.color = new Color(g, g * 0.94f, g * 0.82f, 0.9f);
            p.t.position = center + new Vector3(x, Random.Range(-0.05f, 0.05f), 0f);
            p.t.localScale = new Vector3(p.size, p.size, 1f);
            p.sr.color = p.color;
            p.sr.enabled = true;
            p.active = true;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < pool.Count; i++)
        {
            var p = pool[i];
            if (!p.active) continue;
            p.life -= dt;
            if (p.life <= 0f)
            {
                p.active = false;
                p.sr.enabled = false;
                continue;
            }
            p.vel.y -= p.gravity * dt;
            p.t.position += (Vector3)(p.vel * dt);
            float k = p.life / p.maxLife;
            var c = p.color;
            c.a = p.color.a * Mathf.Clamp01(k * 2f);
            p.sr.color = c;
            // shrink in the last third, snapped to the pixel grid
            float s = p.size * Mathf.Clamp01(k * 3f);
            s = Mathf.Max(1f / 16f, Mathf.Round(s * 16f) / 16f);
            p.t.localScale = new Vector3(s, s, 1f);
        }
    }
}
