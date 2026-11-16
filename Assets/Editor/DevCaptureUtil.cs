using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Renders the main camera (and screen-space overlay UI) to a PNG. Works in edit and play mode.</summary>
public static class DevCaptureUtil
{
    public static string Capture(string path, int width = 1600, int height = 900)
    {
        var cam = Camera.main;
        if (cam == null) return "ERROR no Camera.main in the open scene";

        var restore = new List<KeyValuePair<Canvas, RenderMode>>();
        foreach (var c in Object.FindObjectsOfType<Canvas>())
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            restore.Add(new KeyValuePair<Canvas, RenderMode>(c, c.renderMode));
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = 1f;
        }

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        try
        {
            Canvas.ForceUpdateCanvases();
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return "OK shot " + path;
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            rt.Release();
            Object.DestroyImmediate(rt);
            foreach (var kv in restore) kv.Key.renderMode = kv.Value;
        }
    }
}
