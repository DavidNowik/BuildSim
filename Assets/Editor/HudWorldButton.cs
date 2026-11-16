using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Adds the "World map (Tab)" button to the existing HUD in the Level scene without rebuilding (keeps hand edits).</summary>
public static class HudWorldButton
{
    [MenuItem("BuildSim/Setup/7 Add World Map Button To HUD")]
    public static void Menu() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Run(); }

    [MenuItem("BuildSim/Dev/Add World Map Button (no prompt)")]
    public static void Silent()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Dev] aborted: open scene has unsaved changes."); return; }
        Run();
    }

    private static void Run()
    {
        var scene = EditorSceneManager.OpenScene(LevelSceneBuilder.ScenePath, OpenSceneMode.Single);
        var hud = Object.FindObjectOfType<HUD>();
        if (hud == null || hud.SpeedImages == null || hud.SpeedImages.Length == 0) { Debug.LogError("HudWorldButton: HUD with speed buttons not found."); return; }
        var bar = hud.SpeedImages[0].transform.parent.parent as RectTransform;
        if (bar.Find("World Map Button") != null) { Debug.Log("[Dev] World map button already exists."); return; }

        var panel = hud.SpeedImages[0].sprite;
        var go = new GameObject("World Map Button", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(bar, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0.5f);
        rt.anchoredPosition = new Vector2(-290, 0);
        rt.sizeDelta = new Vector2(150, 40);
        var img = go.AddComponent<Image>();
        img.sprite = panel; img.type = Image.Type.Sliced; img.color = hud.SpeedImages[0].color;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        go.AddComponent<UiButtonFx>();

        var tgo = new GameObject("Label", typeof(RectTransform));
        var trt = (RectTransform)tgo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        var src = hud.SpeedImages[0].transform.GetComponentInChildren<Text>();
        var t = tgo.AddComponent<Text>();
        t.font = src != null ? src.font : UiFont.Get();
        t.fontSize = 20; t.alignment = TextAnchor.MiddleCenter; t.color = src != null ? src.color : Color.white;
        t.raycastTarget = false; t.text = "World map (Tab)";

        UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(hud.OnWorldMapButton));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Dev] World map button added to the HUD.");
    }
}
