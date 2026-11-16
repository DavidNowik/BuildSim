using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev helper: lets an external tool (Claude Code) drive the open editor through a command file.
///   Temp/dev_command.txt   ->  "menu <Menu/Path>" | "play <seconds> <shotName>" | "shot <shotName>"
///   Temp/dev_result.txt    <-  result + console errors/warnings captured while the command ran
///   Temp/shots/<name>.png  <-  screenshots (world + UI)
/// Safe to delete this file (and Assets/Editor/DevCaptureUtil.cs) at any time.
/// </summary>
[InitializeOnLoad]
public static class DevCommandRunner
{
    private static readonly string TempDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp");
    private static string CmdFile { get { return Path.Combine(TempDir, "dev_command.txt"); } }
    private static string ResFile { get { return Path.Combine(TempDir, "dev_result.txt"); } }
    private static string LogFile { get { return Path.Combine(TempDir, "dev_log.txt"); } }
    private static double nextPoll;

    static DevCommandRunner()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    private static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Log && !msg.StartsWith("[Dev]")) return;
        try { File.AppendAllText(LogFile, "[" + type + "] " + msg + (type == LogType.Exception || type == LogType.Error ? "\n    " + stack.Replace("\n", "\n    ") : "") + "\n"); }
        catch { }
    }

    private static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < nextPoll) return;
        nextPoll = now + 0.4;

        string busy = SessionState.GetString("dev.busy", "");
        if (busy == "play") { TickPlay(now); return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(CmdFile)) return;

        string cmd = File.ReadAllText(CmdFile).Trim();
        File.Delete(CmdFile);
        if (File.Exists(LogFile)) File.Delete(LogFile);
        Run(cmd);
    }

    private static void Run(string cmd)
    {
        string[] t = cmd.Split(new[] { ' ' }, 3);
        string verb = t[0];
        try
        {
            if (verb == "menu")
            {
                bool ok = EditorApplication.ExecuteMenuItem(t[1] + (t.Length > 2 ? " " + t[2] : ""));
                Finish(ok ? "OK menu" : "ERROR menu item not found: " + cmd);
            }
            else if (verb == "shot")
            {
                Finish(DevCaptureUtil.Capture(ShotPath(t[1])));
            }
            else if (verb == "play")
            {
                SessionState.SetString("dev.busy", "play");
                SessionState.SetString("dev.phase", "start");
                SessionState.SetFloat("dev.seconds", float.Parse(t[1], System.Globalization.CultureInfo.InvariantCulture));
                string[] a = (t.Length > 2 ? t[2] : "play").Split(' ');
                SessionState.SetString("dev.shot", a[0]);
                SessionState.SetString("dev.call", a.Length > 1 ? a[1] : "");
                EditorApplication.isPlaying = true;
            }
            else Finish("ERROR unknown command: " + cmd);
        }
        catch (System.Exception e) { Finish("ERROR " + e); }
    }

    private static void TickPlay(double now)
    {
        string phase = SessionState.GetString("dev.phase", "");
        if (phase == "start")
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isPlaying)
            {
                SessionState.SetFloat("dev.t0", (float)now);
                SessionState.SetString("dev.phase", "running");
                InvokeCall(SessionState.GetString("dev.call", ""));
            }
        }
        else if (phase == "running")
        {
            if (!EditorApplication.isPlaying) { SessionState.SetString("dev.busy", ""); Finish("ERROR play mode ended early"); return; }
            if (now - SessionState.GetFloat("dev.t0", 0f) >= SessionState.GetFloat("dev.seconds", 5f))
            {
                string r = DevCaptureUtil.Capture(ShotPath(SessionState.GetString("dev.shot", "play")));
                SessionState.SetString("dev.captureResult", r);
                SessionState.SetString("dev.phase", "stopping");
                EditorApplication.isPlaying = false;
            }
        }
        else if (phase == "stopping")
        {
            if (EditorApplication.isPlaying) return;
            SessionState.SetString("dev.busy", "");
            Finish(SessionState.GetString("dev.captureResult", "OK play"));
        }
    }

    /// <summary>Calls a static method "Class.Method" (any assembly) once, e.g. to script a play-mode test.</summary>
    private static void InvokeCall(string call)
    {
        if (string.IsNullOrEmpty(call)) return;
        string[] p = call.Split('.');
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = asm.GetType(p[0]);
            if (type == null) continue;
            var m = type.GetMethod(p[1], System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (m != null) { m.Invoke(null, null); return; }
        }
        Debug.LogError("[Dev] call target not found: " + call);
    }

    private static string ShotPath(string name)
    {
        string dir = Path.Combine(TempDir, "shots");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name + ".png");
    }

    private static void Finish(string result)
    {
        var sb = new StringBuilder();
        sb.AppendLine(result);
        if (File.Exists(LogFile)) { sb.AppendLine("--- console ---"); sb.Append(File.ReadAllText(LogFile)); }
        File.WriteAllText(ResFile, sb.ToString());
    }
}
