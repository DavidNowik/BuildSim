using UnityEngine;

/// <summary>Game speed control (pause / 1x / 2x / 4x). Space toggles pause, +/- change speed.</summary>
public class GameClock : MonoBehaviour
{
    public static GameClock Instance;
    public static readonly int[] Speeds = { 0, 1, 2, 4 };

    public int Speed { get; private set; } = 1;
    private int lastRunning = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Set(1);
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; Time.timeScale = 1f; }
    }

    public void Set(int speed)
    {
        Speed = speed;
        if (speed > 0) lastRunning = speed;
        Time.timeScale = speed;
    }

    private void Update()
    {
        if (DebugConsole.IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Space)) Set(Speed == 0 ? lastRunning : 0);
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) Step(1);
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) Step(-1);
    }

    private void Step(int dir)
    {
        int idx = System.Array.IndexOf(Speeds, Speed);
        idx = Mathf.Clamp(idx + dir, 1, Speeds.Length - 1); // stepping never lands on pause
        Set(Speeds[idx]);
    }
}
