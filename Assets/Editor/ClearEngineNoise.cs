using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity 2022.3 logs "GetTransformInfoExpectUpToDate ... RendererUpdateManager.UpdateAll" on startup (engine bug, harmless).
/// Console messages cannot be filtered from code, so when that one shows up and nothing else worrying was logged, the console is cleared.
/// </summary>
[InitializeOnLoad]
public static class ClearEngineNoise
{
    private static bool noise, other;

    static ClearEngineNoise()
    {
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
    }

    private static void OnLog(string msg, string stack, LogType type)
    {
        if (msg.Contains("GetTransformInfoExpectUpToDate")) noise = true;
        else if (type != LogType.Log) other = true;
    }

    private static void Tick()
    {
        if (!noise) return;
        noise = false;
        if (other) return;   // real warnings / errors are on screen: keep everything
        var t = Type.GetType("UnityEditor.LogEntries, UnityEditor");
        if (t != null) t.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
    }
}
