using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Slow day/night colour grading of the scene's Global Light 2D.</summary>
public class DayCycle : MonoBehaviour
{
    public static DayCycle Instance;

    [Tooltip("The Global Light 2D of the scene")]
    public Light2D globalLight;
    [Tooltip("Real seconds (at 1x speed) for one full day")]
    public float dayLength = 240f;
    [Range(0f, 1f)] public float startTime = 0.12f;

    [Header("Worker sleep")]
    [Tooltip("Workers go to bed when the night amount reaches this value")]
    [Range(0f, 1f)] public float sleepStartsAtNight = 0.6f;
    [Tooltip("Workers wake up when the night amount falls below this value")]
    [Range(0f, 1f)] public float wakeAtNight = 0.2f;

    /// <summary>True from dusk until dawn: workers with a home camp sleep.</summary>
    public bool IsSleepTime { get; private set; }

    /// <summary>0 = full daylight, 1 = deep night.</summary>
    public float Night01 { get; private set; }
    public int Day { get; private set; }
    public float TimeOfDay { get { return t; } }

    private float t;
    private Gradient colors;
    private AnimationCurve nightCurve;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        t = startTime;
        Day = 1;

        colors = new Gradient();
        colors.SetKeys(new[]
        {
            new GradientColorKey(new Color(1.00f, 0.84f, 0.70f), 0.00f), // dawn
            new GradientColorKey(new Color(1.00f, 0.96f, 0.88f), 0.14f), // morning
            new GradientColorKey(new Color(1.00f, 1.00f, 0.97f), 0.34f), // noon
            new GradientColorKey(new Color(1.00f, 0.95f, 0.85f), 0.55f), // afternoon
            new GradientColorKey(new Color(1.00f, 0.66f, 0.46f), 0.68f), // dusk
            new GradientColorKey(new Color(0.46f, 0.50f, 0.78f), 0.79f), // early night
            new GradientColorKey(new Color(0.40f, 0.44f, 0.72f), 0.90f), // night
            new GradientColorKey(new Color(1.00f, 0.84f, 0.70f), 1.00f), // dawn again
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        nightCurve = new AnimationCurve(
            new Keyframe(0.00f, 0.15f), new Keyframe(0.14f, 0f), new Keyframe(0.55f, 0f),
            new Keyframe(0.68f, 0.35f), new Keyframe(0.79f, 1f), new Keyframe(0.90f, 1f), new Keyframe(1f, 0.15f));
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start() { Apply(); }

    private void Update()
    {
        float prev = t;
        t += Time.deltaTime / Mathf.Max(10f, dayLength);
        if (t >= 1f) { t -= 1f; Day++; }
        if (t < prev && HUD.Instance != null) HUD.Instance.Toast("Day " + Day + " begins", ToastKind.Info);
        Apply();
    }

    private void Apply()
    {
        Night01 = nightCurve.Evaluate(t);
        if (!IsSleepTime && Night01 >= sleepStartsAtNight) IsSleepTime = true;
        else if (IsSleepTime && Night01 <= wakeAtNight) IsSleepTime = false;
        if (globalLight != null)
        {
            globalLight.color = colors.Evaluate(t);
            globalLight.intensity = 1f;
        }
    }

    public void WriteSave(LevelSave s) { s.day = Day; s.timeOfDay = t; }

    public void ReadSave(LevelSave s)
    {
        Day = Mathf.Max(1, s.day);
        t = Mathf.Repeat(s.timeOfDay, 1f);
        Apply();
    }
}
