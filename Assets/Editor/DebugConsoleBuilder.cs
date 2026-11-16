using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu: BuildSim/Setup/5 Add Debug Console To Scenes.
/// Creates Assets/Prefabs/DebugConsole.prefab (a real UI hierarchy: panel, output text, input field) and places one
/// instance in the Overworld and Level scenes. Edit the prefab to change the look of the console.
/// </summary>
public static class DebugConsoleBuilder
{
    private const string PrefabPath = "Assets/Prefabs/DebugConsole.prefab";
    private static readonly string[] ScenePaths = { OverworldSceneBuilder.ScenePath, LevelSceneBuilder.ScenePath };

    [MenuItem("BuildSim/Setup/5 Add Debug Console To Scenes")]
    public static void MenuAdd()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
    }

    [MenuItem("BuildSim/Dev/Add Debug Console (no prompt)")]
    public static void SilentAdd()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Dev] Debug console setup aborted: the open scene has unsaved changes."); return; }
        Run();
    }

    private static void Run()
    {
        var prefab = BuildPrefab();
        foreach (string path in ScenePaths)
        {
            if (!File.Exists(path)) { Debug.LogWarning("DebugConsoleBuilder: scene not found: " + path); continue; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (Object.FindObjectOfType<DebugConsole>() == null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = "Debug Console";
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        EditorSceneManager.OpenScene(OverworldSceneBuilder.ScenePath, OpenSceneMode.Single);
        Debug.Log("[Dev] DebugConsoleBuilder: console added to the Overworld and Level scenes");
    }

    // ---------------------------------------------------------------- prefab
    private static GameObject BuildPrefab()
    {
        Directory.CreateDirectory("Assets/Prefabs");
        var panelSprite = ArtGenerator.LoadSprite("panel");
        var pixel = ArtGenerator.LoadSprite("pixel");

        var root = new GameObject("Debug Console");
        var console = root.AddComponent<DebugConsole>();

        var canvasGo = Rect(root.transform, "Canvas");
        var canvas = canvasGo.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.gameObject.AddComponent<GraphicRaycaster>();

        // panel at the top of the screen
        var panel = Rect(canvasGo, "Panel");
        Anchor(panel, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(-40, 400));
        var bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = panelSprite; bg.type = Image.Type.Sliced; bg.color = new Color(0.04f, 0.05f, 0.09f, 0.94f);

        // output (read-only text, newest at the bottom)
        var outRt = Rect(panel, "Output");
        Anchor(outRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        outRt.offsetMin = new Vector2(18, 62); outRt.offsetMax = new Vector2(-18, -14);
        var output = outRt.gameObject.AddComponent<Text>();
        Style(output, 21, new Color(0.78f, 0.95f, 0.82f), TextAnchor.LowerLeft);
        output.horizontalOverflow = HorizontalWrapMode.Wrap;
        output.verticalOverflow = VerticalWrapMode.Truncate;

        // input field
        var inRt = Rect(panel, "Input");
        Anchor(inRt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(-36, 40));
        var inBg = inRt.gameObject.AddComponent<Image>();
        inBg.sprite = pixel; inBg.color = new Color(0.12f, 0.14f, 0.22f, 1f);

        var textRt = Rect(inRt, "Text");
        Anchor(textRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        textRt.offsetMin = new Vector2(12, 4); textRt.offsetMax = new Vector2(-12, -4);
        var text = textRt.gameObject.AddComponent<Text>();
        Style(text, 22, Color.white, TextAnchor.MiddleLeft);
        text.supportRichText = false;

        var phRt = Rect(inRt, "Placeholder");
        Anchor(phRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        phRt.offsetMin = new Vector2(12, 4); phRt.offsetMax = new Vector2(-12, -4);
        var placeholder = phRt.gameObject.AddComponent<Text>();
        Style(placeholder, 22, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleLeft);
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.text = "type a command  (help = list)   -   ` or F1 closes";

        var field = inRt.gameObject.AddComponent<InputField>();
        field.targetGraphic = inBg;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = InputField.LineType.SingleLine;
        field.caretBlinkRate = 0.9f;
        field.customCaretColor = true;
        field.caretColor = new Color(0.8f, 1f, 0.85f);

        console.panel = panel.gameObject;
        console.input = field;
        console.output = output;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    private static void Style(Text t, int size, Color color, TextAnchor align)
    {
        t.font = UiFont.Get();
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
    }
}
