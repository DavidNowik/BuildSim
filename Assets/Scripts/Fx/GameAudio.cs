using System.Collections;
using UnityEngine;

public enum Sfx { StrikeStone, StrikeWood, Place, Deny, Deposit, Click, Hover, Recruit, Chime, Pickup, TideRumble, Thunder, Thump, BellLow, BellHigh }

/// <summary>
/// Fully procedural audio: synthesised SFX, ambient wind + birds and a soft generative music bed.
/// No audio assets are needed. M toggles mute.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance;

    [Range(0f, 1f)] public float sfxVolume = 0.7f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float ambienceVolume = 0.5f;

    private const int Rate = 32000;

    private AudioClip[][] clips;
    private float[] lastPlayed;
    [Header("Audio sources (child objects of this GameObject)")]
    public AudioSource[] sfxSources;
    public AudioSource musicPad;
    public AudioSource wind;
    public AudioSource musicNotes;
    public AudioSource birds;
    private int nextSource;
    private AudioClip[] notes;
    private AudioClip[] chirps;
    private bool muted;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildClips();

        if (sfxSources == null || sfxSources.Length == 0 || musicPad == null || wind == null || musicNotes == null || birds == null)
        {
            Debug.LogError("GameAudio: audio source objects are not assigned - run BuildSim/Setup/Create Level Scene or assign them in the Inspector.");
            return;
        }

        musicPad.loop = true; musicPad.clip = MakePad();
        musicPad.volume = 0.12f * musicVolume;
        musicPad.Play();

        wind.loop = true; wind.clip = MakeWind();
        wind.volume = 0.16f * ambienceVolume;
        wind.Play();

        StartCoroutine(MusicRoutine());
        StartCoroutine(BirdRoutine());
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        if (!DebugConsole.IsOpen && Input.GetKeyDown(KeyCode.M))
        {
            muted = !muted;
            AudioListener.volume = muted ? 0f : 1f;
            if (HUD.Instance != null) HUD.Instance.Toast(muted ? "Sound off" : "Sound on", ToastKind.Info);
        }
    }


    // ------------------------------------------------------------------ playback
    public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
    {
        if (Instance != null) Instance.PlayInternal(sfx, volume, pitch);
    }

    /// <summary>Plays only when the world position is roughly on screen.</summary>
    public static void PlayAt(Sfx sfx, Vector3 world, float volume = 1f, float pitch = 1f)
    {
        if (Instance == null) return;
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 v = cam.WorldToViewportPoint(world);
            if (v.x < -0.1f || v.x > 1.1f || v.y < -0.1f || v.y > 1.1f) return;
        }
        Instance.PlayInternal(sfx, volume, pitch);
    }

    private void PlayInternal(Sfx sfx, float volume, float pitch)
    {
        if (sfxSources == null || sfxSources.Length == 0) return;
        int idx = (int)sfx;
        float now = Time.unscaledTime;
        if (now - lastPlayed[idx] < 0.045f) return;
        lastPlayed[idx] = now;

        var variants = clips[idx];
        var clip = variants[Random.Range(0, variants.Length)];
        var src = sfxSources[nextSource];
        nextSource = (nextSource + 1) % sfxSources.Length;
        src.pitch = pitch * Random.Range(0.94f, 1.06f);
        src.volume = Mathf.Clamp01(volume * sfxVolume);
        src.clip = clip;
        src.Play();
    }

    // ------------------------------------------------------------------ routines
    private IEnumerator MusicRoutine()
    {
        yield return new WaitForSeconds(2f);
        int step = 4;
        while (true)
        {
            // random walk along the pentatonic scale
            step = Mathf.Clamp(step + Random.Range(-2, 3), 0, notes.Length - 1);
            musicNotes.pitch = 1f;
            musicNotes.volume = Random.Range(0.07f, 0.13f) * musicVolume;
            musicNotes.PlayOneShot(notes[step]);
            float wait = Random.value < 0.3f ? Random.Range(0.35f, 0.6f) : Random.Range(1.4f, 3.4f);
            yield return new WaitForSeconds(wait);
        }
    }

    private IEnumerator BirdRoutine()
    {
        yield return new WaitForSeconds(5f);
        while (true)
        {
            birds.pitch = Random.Range(0.85f, 1.25f);
            birds.volume = Random.Range(0.04f, 0.09f) * ambienceVolume;
            birds.PlayOneShot(chirps[Random.Range(0, chirps.Length)]);
            if (Random.value < 0.5f)
            {
                yield return new WaitForSeconds(0.18f);
                birds.PlayOneShot(chirps[Random.Range(0, chirps.Length)]);
            }
            yield return new WaitForSeconds(Random.Range(6f, 15f));
        }
    }

    // ------------------------------------------------------------------ synthesis
    private delegate float Gen(float t, float dur);

    private static AudioClip Make(string name, float dur, Gen g, bool loop = false)
    {
        int n = Mathf.CeilToInt(dur * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(g(i / (float)Rate, dur), -1f, 1f);
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Noise() { return Random.value * 2f - 1f; }
    private static float Sin(float f, float t) { return Mathf.Sin(2f * Mathf.PI * f * t); }

    private void BuildClips()
    {
        int count = System.Enum.GetValues(typeof(Sfx)).Length;
        clips = new AudioClip[count][];
        lastPlayed = new float[count];
        for (int i = 0; i < count; i++) lastPlayed[i] = -10f;

        float[] stoneF = { 720f, 840f, 640f };
        var stone = new AudioClip[3];
        for (int i = 0; i < 3; i++)
        {
            float f = stoneF[i];
            stone[i] = Make("stone" + i, 0.14f, (t, d) => 0.45f * Sin(f, t) * Mathf.Exp(-t * 42f) + 0.3f * Noise() * Mathf.Exp(-t * 90f));
        }
        clips[(int)Sfx.StrikeStone] = stone;

        float[] woodF = { 170f, 150f, 200f };
        var wood = new AudioClip[3];
        for (int i = 0; i < 3; i++)
        {
            float f = woodF[i];
            float lp = 0f;
            wood[i] = Make("wood" + i, 0.18f, (t, d) =>
            {
                lp += (Noise() - lp) * 0.25f;
                return 0.6f * Sin(f, t) * Mathf.Exp(-t * 26f) + 0.5f * lp * Mathf.Exp(-t * 55f);
            });
        }
        clips[(int)Sfx.StrikeWood] = wood;

        clips[(int)Sfx.Place] = new[]
        {
            Make("place", 0.34f, (t, d) =>
                0.8f * Mathf.Sin(2f * Mathf.PI * (45f * t + 60f * (1f - Mathf.Exp(-t * 25f)) / 25f)) * Mathf.Exp(-t * 11f)
                + 0.35f * Noise() * Mathf.Exp(-t * 38f))
        };

        clips[(int)Sfx.Deny] = new[]
        {
            Make("deny", 0.24f, (t, d) =>
            {
                bool on = t < 0.09f || (t > 0.12f && t < 0.21f);
                return on ? 0.22f * Mathf.Sign(Sin(140f, t)) : 0f;
            })
        };

        clips[(int)Sfx.Deposit] = new[]
        {
            Make("deposit", 0.26f, (t, d) =>
            {
                float a = Sin(660f, t) * Mathf.Exp(-t * 14f);
                float tt = t - 0.09f;
                float b = tt > 0f ? Sin(880f, tt) * Mathf.Exp(-tt * 12f) : 0f;
                return 0.28f * (a + b);
            })
        };

        clips[(int)Sfx.Click] = new[] { Make("click", 0.05f, (t, d) => 0.35f * Sin(1000f, t) * Mathf.Exp(-t * 70f)) };
        clips[(int)Sfx.Hover] = new[] { Make("hover", 0.035f, (t, d) => 0.15f * Sin(1500f, t) * Mathf.Exp(-t * 110f)) };

        clips[(int)Sfx.Recruit] = new[]
        {
            Make("recruit", 0.28f, (t, d) => 0.3f * Mathf.Sin(2f * Mathf.PI * (300f * t + 800f * t * t)) * Mathf.Min(1f, (d - t) * 8f))
        };

        clips[(int)Sfx.Chime] = new[]
        {
            Make("chime", 0.9f, (t, d) =>
            {
                float s = 0f;
                float[] fs = { 523.25f, 659.25f, 783.99f };
                for (int i = 0; i < 3; i++)
                {
                    float tt = t - i * 0.11f;
                    if (tt > 0f) s += Sin(fs[i], tt) * Mathf.Exp(-tt * 4.5f) + 0.3f * Sin(fs[i] * 2f, tt) * Mathf.Exp(-tt * 7f);
                }
                return 0.22f * s;
            })
        };

        clips[(int)Sfx.Pickup] = new[] { Make("pickup", 0.06f, (t, d) => 0.25f * Sin(900f + 700f * t, t) * Mathf.Exp(-t * 45f)) };

        // ---- tide sounds (overworld): deep rumble, thunder, thump of a land going under, two bells
        float rlp = 0f, rph = 0f;
        clips[(int)Sfx.TideRumble] = new[]
        {
            Make("rumble", 7f, (t, d) =>
            {
                float rise = d * 0.6f;
                float f = 34f + 18f * Mathf.Clamp01(t / rise);
                rph += f / Rate;
                float saw = 2f * (rph - Mathf.Floor(rph + 0.5f));
                rlp += (saw - rlp) * 0.03f;
                float env = t < rise ? Mathf.Pow(t / rise, 2.2f) : Mathf.Pow(1f - (t - rise) / (d - rise), 1.5f);
                return 1.4f * rlp * env;
            })
        };

        float tlp = 0f, tph = 0f;
        clips[(int)Sfx.Thunder] = new[]
        {
            Make("thunder", 1.7f, (t, d) =>
            {
                float f = 70f * Mathf.Pow(30f / 70f, t / 1.4f);
                tph += f / Rate;
                float saw = 2f * (tph - Mathf.Floor(tph + 0.5f));
                tlp += (saw + 0.6f * Noise() - tlp) * 0.06f;
                return 2.2f * tlp * Mathf.Min(1f, t / 0.08f) * Mathf.Exp(-t * 2.1f);
            })
        };

        clips[(int)Sfx.Thump] = new[]
        {
            Make("thump", 0.32f, (t, d) => 0.7f * Mathf.Sin(2f * Mathf.PI * (40f * t + 50f * (1f - Mathf.Exp(-t * 12f)) / 12f)) * Mathf.Exp(-t * 10f))
        };

        clips[(int)Sfx.BellLow] = new[]
        {
            Make("bellLow", 3.4f, (t, d) => 0.4f * (Sin(110f, t) + 0.4f * Sin(220f, t) * Mathf.Exp(-t * 2f)) * Mathf.Min(1f, t / 0.05f) * Mathf.Exp(-t * 1.3f))
        };
        clips[(int)Sfx.BellHigh] = new[]
        {
            Make("bellHigh", 3.4f, (t, d) => 0.35f * (Sin(165f, t) + 0.4f * Sin(330f, t) * Mathf.Exp(-t * 2f)) * Mathf.Min(1f, t / 0.05f) * Mathf.Exp(-t * 1.3f))
        };

        // music notes (A minor pentatonic)
        float[] nf = { 220f, 261.63f, 293.66f, 329.63f, 392f, 440f, 523.25f, 587.33f, 659.25f };
        notes = new AudioClip[nf.Length];
        for (int i = 0; i < nf.Length; i++)
        {
            float f = nf[i];
            notes[i] = Make("note" + i, 2.2f, (t, d) =>
            {
                float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t * 2.2f);
                return 0.7f * env * (Sin(f, t) + 0.28f * Sin(f * 2f, t) * Mathf.Exp(-t * 3f) + 0.1f * Sin(f * 3f, t) * Mathf.Exp(-t * 5f));
            });
        }

        chirps = new AudioClip[3];
        for (int i = 0; i < 3; i++)
        {
            float basef = 2400f + i * 450f;
            float vib = 14f + i * 6f;
            chirps[i] = Make("chirp" + i, 0.22f, (t, d) =>
            {
                float f = basef + 700f * Mathf.Sin(Mathf.PI * t / d) + 120f * Sin(vib, t);
                float env = Mathf.Sin(Mathf.PI * t / d);
                return 0.5f * env * Mathf.Sin(2f * Mathf.PI * f * t);
            });
        }
    }

    private AudioClip MakePad()
    {
        // all frequencies complete an integer number of cycles in 8 s -> seamless loop
        return Make("pad", 8f, (t, d) =>
        {
            float trem = 0.75f + 0.25f * Sin(1f / 8f, t);
            float s = Sin(110f, t) + 0.8f * Sin(165f, t) + 0.6f * Sin(220f, t) + 0.35f * Sin(277.5f, t);
            return 0.22f * s * trem;
        }, true);
    }

    private AudioClip MakeWind()
    {
        float lp = 0f, lp2 = 0f;
        return Make("wind", 6f, (t, d) =>
        {
            float swell = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Mathf.PI * t / d * 1.5f));
            lp += (Noise() - lp) * 0.05f;
            lp2 += (lp - lp2) * 0.12f;
            float edge = Mathf.Min(1f, t / 0.15f) * Mathf.Min(1f, (d - t) / 0.15f);
            return lp2 * 6f * swell * edge;
        }, true);
    }
}
