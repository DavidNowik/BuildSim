using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Marks a static method as a console command: <c>[DebugCommand("tide", "what it does")] static string Tide(string[] args)</c>.
/// The returned text is printed in the console. Commands are found automatically (no registration).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class DebugCommandAttribute : Attribute
{
    public readonly string Name;
    public readonly string Help;
    public DebugCommandAttribute(string name, string help) { Name = name; Help = help; }
}

/// <summary>
/// In-game debug console. Open/close with ` (backquote) or F1, type a command, Enter to run, Up/Down for history.
/// The UI is a prefab (Assets/Prefabs/DebugConsole.prefab) placed in the scenes; this component only drives it.
/// While it is open <see cref="IsOpen"/> is true and the game ignores its own hotkeys.
/// </summary>
public class DebugConsole : MonoBehaviour
{
    public static DebugConsole Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [Header("Scene references (children of the prefab)")]
    public GameObject panel;
    public InputField input;
    public Text output;

    [Header("Behaviour")]
    public KeyCode[] toggleKeys = { KeyCode.BackQuote, KeyCode.F1 };
    public int maxLines = 14;

    private struct Command { public string name, help; public MethodInfo method; }

    private static Dictionary<string, Command> commands;
    private readonly List<string> lines = new List<string>();
    private readonly List<string> history = new List<string>();
    private int historyIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        IsOpen = false;
        if (commands == null) FindCommands();
        panel.SetActive(false);
        // the toggle key must not be typed into the field
        input.onValidateInput = (text, index, c) => (c == '`' || c == '~') ? '\0' : c;
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; IsOpen = false; }
    }

    private void Update()
    {
        foreach (var k in toggleKeys)
            if (Input.GetKeyDown(k)) { if (IsOpen) Close(); else Open(); return; }
        if (!IsOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Submit();
        if (Input.GetKeyDown(KeyCode.UpArrow)) Recall(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow)) Recall(+1);
    }

    public void Open()
    {
        IsOpen = true;
        panel.SetActive(true);
        if (lines.Count == 0) Print("Debug console. Type 'help' for the list of commands.");
        input.text = "";
        input.ActivateInputField();
        input.Select();
    }

    public void Close()
    {
        IsOpen = false;
        panel.SetActive(false);
        input.DeactivateInputField();
    }

    private void Submit()
    {
        string line = input.text.Trim();
        input.text = "";
        input.ActivateInputField();
        if (line.Length == 0) return;
        history.Add(line);
        historyIndex = history.Count;
        Print("> " + line);
        string result = Execute(line);
        if (!string.IsNullOrEmpty(result)) Print(result);
    }

    private void Recall(int dir)
    {
        if (history.Count == 0) return;
        historyIndex = Mathf.Clamp(historyIndex + dir, 0, history.Count);
        input.text = historyIndex < history.Count ? history[historyIndex] : "";
        input.caretPosition = input.text.Length;
    }

    // ------------------------------------------------------------ output
    public static void Print(string text)
    {
        if (Instance != null) Instance.AddLines(text);
    }

    private void AddLines(string text)
    {
        foreach (var l in text.Split('\n')) lines.Add(l);
        while (lines.Count > maxLines) lines.RemoveAt(0);
        output.text = string.Join("\n", lines);
    }

    // ------------------------------------------------------------ commands
    /// <summary>Runs a command line like "tide" or "moon 3". Returns the text to print.</summary>
    public static string Execute(string line)
    {
        if (commands == null) FindCommands();
        var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "";
        Command c;
        if (!commands.TryGetValue(parts[0].ToLowerInvariant(), out c)) return "Unknown command '" + parts[0] + "'. Type 'help'.";
        var args = new string[parts.Length - 1];
        Array.Copy(parts, 1, args, 0, args.Length);
        try { return c.method.Invoke(null, new object[] { args }) as string; }
        catch (Exception e) { return "Error: " + (e.InnerException != null ? e.InnerException.Message : e.Message); }
    }

    private static void FindCommands()
    {
        commands = new Dictionary<string, Command>();
        foreach (var type in typeof(DebugConsole).Assembly.GetTypes())
            foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var a = (DebugCommandAttribute)Attribute.GetCustomAttribute(m, typeof(DebugCommandAttribute));
                if (a == null) continue;
                if (m.ReturnType != typeof(string) || m.GetParameters().Length != 1) { Debug.LogWarning("DebugCommand " + a.Name + ": needs the signature  static string Name(string[] args)"); continue; }
                commands[a.Name.ToLowerInvariant()] = new Command { name = a.Name, help = a.Help, method = m };
            }
    }

    [DebugCommand("help", "List all commands")]
    private static string HelpCommand(string[] args)
    {
        var names = new List<string>(commands.Keys);
        names.Sort();
        var sb = new System.Text.StringBuilder();
        foreach (var n in names) sb.AppendLine("  " + commands[n].name + "  -  " + commands[n].help);
        return sb.ToString().TrimEnd();
    }

    [DebugCommand("clear", "Clear the console")]
    private static string ClearCommand(string[] args)
    {
        if (Instance != null) { Instance.lines.Clear(); Instance.output.text = ""; }
        return "";
    }
}
