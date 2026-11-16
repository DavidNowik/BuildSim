using UnityEngine;
using UnityEngine.UI;

/// <summary>Number that counts up/down smoothly and pulses when it changes.</summary>
public class CounterLabel : MonoBehaviour
{
    public Text text;
    public Color baseColor = Color.white;
    private float shown, target, pulse;
    private bool up = true;

    public void Set(int value, bool instant = false)
    {
        if (Mathf.RoundToInt(target) != value)
        {
            up = value > target;
            pulse = 1f;
        }
        target = value;
        if (instant) shown = value;
    }

    public void Pulse() { pulse = Mathf.Max(pulse, 0.7f); up = true; }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float speed = Mathf.Max(18f, Mathf.Abs(target - shown) * 7f);
        shown = Mathf.MoveTowards(shown, target, speed * dt);
        text.text = Mathf.RoundToInt(shown).ToString();
        pulse = Mathf.MoveTowards(pulse, 0f, dt * 2.5f);
        float s = 1f + 0.28f * pulse;
        text.rectTransform.localScale = new Vector3(s, s, 1f);
        text.color = Color.Lerp(baseColor, up ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.55f, 0.5f), pulse);
    }
}

