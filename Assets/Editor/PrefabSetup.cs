using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Menu: BuildSim/Setup/2 Setup Prefabs.
/// Adds the visual parts that used to be created in code as real child objects of the prefabs
/// (worker: shadow, carry icon, status bubble, selection ring; buildings: shadow, camp light + door glow)
/// and wires the serialized references. Safe to run repeatedly: existing parts are replaced.
/// </summary>
public static class PrefabSetup
{
    public const string WorkerPrefab = "Assets/Prefabs/Workers/Miner.prefab";
    public const string BuildingDir = "Assets/Prefabs/Buildings";
    private const string LitMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3"; // Sprite-Lit-Default used by the existing art

    public static Material LitMaterial
    {
        get { return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(LitMaterialGuid)); }
    }

    [MenuItem("BuildSim/Setup/2 Setup Prefabs")]
    public static void Run()
    {
        SetupWorker();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { BuildingDir }))
            SetupBuilding(AssetDatabase.GUIDToAssetPath(guid));
        AssetDatabase.SaveAssets();
        Debug.Log("[Dev] PrefabSetup: worker + building prefabs updated");
    }

    // ---------------------------------------------------------------- worker
    private static void SetupWorker()
    {
        var root = PrefabUtility.LoadPrefabContents(WorkerPrefab);
        try
        {
            var worker = root.GetComponent<Worker>();
            if (worker == null) { Debug.LogError("PrefabSetup: " + WorkerPrefab + " has no Worker component"); return; }

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            Remove(root.transform, "Shadow"); Remove(root.transform, "CarryIcon");
            Remove(root.transform, "StatusBubble"); Remove(root.transform, "SelectRing");

            var shadow = NewRenderer(root.transform, "Shadow", ArtGenerator.LoadSprite("shadow"), Layers.Workers, 0, true);
            shadow.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            shadow.transform.localScale = new Vector3(0.45f, 0.5f, 1f);
            shadow.color = new Color(0f, 0f, 0f, 0.3f);

            var carry = NewRenderer(root.transform, "CarryIcon", ArtGenerator.LoadSprite("icon_stone"), Layers.Workers, 0, false);
            carry.transform.localPosition = new Vector3(0.32f, -0.12f, 0f);
            carry.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            carry.enabled = false;

            var bubble = NewRenderer(root.transform, "StatusBubble", ArtGenerator.LoadSprite("bubble_wait"), Layers.UI, 5, false);
            bubble.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            bubble.enabled = false;

            var ring = NewRenderer(root.transform, "SelectRing", ArtGenerator.LoadSprite("ring"), Layers.Workers, 0, false);
            ring.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            ring.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
            ring.color = new Color(1f, 0.95f, 0.5f, 0.95f);
            ring.enabled = false;

            var so = new SerializedObject(worker);
            Set(so, "shadow", shadow); Set(so, "bubble", bubble); Set(so, "carryIcon", carry); Set(so, "ring", ring);
            Set(so, "bubbleSleep", ArtGenerator.LoadSprite("bubble_sleep"));
            Set(so, "bubbleWait", ArtGenerator.LoadSprite("bubble_wait"));
            Set(so, "bubbleAlert", ArtGenerator.LoadSprite("bubble_alert"));
            Set(so, "stoneIcon", ArtGenerator.LoadSprite("icon_stone"));
            Set(so, "woodIcon", ArtGenerator.LoadSprite("icon_wood"));
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, WorkerPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------------------------------------------------------- buildings
    private static void SetupBuilding(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var b = root.GetComponent<Building>();
            if (b == null) return;

            // components left over from earlier setups whose script no longer resolves ("missing script")
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            Remove(root.transform, "Shadow"); Remove(root.transform, "CampLight"); Remove(root.transform, "DoorGlow");
            var oldLight = root.GetComponent<CampLight>();
            if (oldLight != null) Object.DestroyImmediate(oldLight);

            var box = root.GetComponent<BoxCollider2D>();
            Vector2 foot = box != null ? box.offset : Vector2.zero;       // local-space centre of the footprint
            Vector3 s = root.transform.localScale;                        // prefab root scale
            var firstSr = root.GetComponent<SpriteRenderer>();
            string layer = firstSr != null ? firstSr.sortingLayerName : Layers.Objects;

            // shadow: sits just below the footprint and slightly wider than it
            var shadow = NewRenderer(root.transform, "Shadow", ArtGenerator.LoadSprite("shadow"), layer, -5, true);
            shadow.transform.localPosition = new Vector3(foot.x, foot.y - 0.18f / s.y, 0f);
            shadow.transform.localScale = new Vector3((b.footprint.x + 0.7f) / 2f / s.x, (0.9f + b.footprint.y * 0.2f) / s.y, 1f);
            shadow.color = new Color(0f, 0f, 0f, 0.32f);

            var so = new SerializedObject(b);
            Set(so, "shadow", shadow);

            if (b.maxWorkers > 0)
            {
                // warm window light + a flat glow on the door; children compensate the root scale so radii are in world units
                var lightGo = new GameObject("CampLight");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = new Vector3(foot.x, foot.y + 0.35f / s.y, 0f);
                lightGo.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f);
                var light = lightGo.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = new Color(1f, 0.72f, 0.38f);
                light.intensity = 0f;
                light.pointLightOuterRadius = 3.6f;
                light.pointLightInnerRadius = 0.3f;
                light.shadowsEnabled = false;
                LightUtil.ApplyToAllSortingLayers(light);

                var glow = NewRenderer(root.transform, "DoorGlow", ArtGenerator.LoadSprite("glow"), Layers.Workers, 3, false);
                glow.transform.localPosition = new Vector3(foot.x, foot.y - 0.25f / s.y, 0f);
                glow.transform.localScale = new Vector3(1.6f / s.x, 0.9f / s.y, 1f);
                glow.color = new Color(1f, 0.78f, 0.4f, 0f);

                var cl = root.AddComponent<CampLight>();
                var cso = new SerializedObject(cl);
                Set(cso, "building", b); Set(cso, "light2d", light); Set(cso, "glow", glow);
                cso.ApplyModifiedPropertiesWithoutUndo();
                Set(so, "campLight", cl);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------------------------------------------------------- helpers
    private static void Remove(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }

    private static SpriteRenderer NewRenderer(Transform parent, string name, Sprite sprite, string layer, int order, bool lit)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        if (lit && LitMaterial != null) sr.sharedMaterial = LitMaterial;
        return sr;
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("PrefabSetup: field not found: " + so.targetObject.GetType().Name + "." + field); return; }
        p.objectReferenceValue = value;
    }
}

/// <summary>Editor helpers for URP 2D lights.</summary>
public static class LightUtil
{
    /// <summary>A Light2D created from code has no sorting layers selected; select all of them (private serialized field).</summary>
    public static void ApplyToAllSortingLayers(Light2D l)
    {
        var ids = new int[SortingLayer.layers.Length];
        for (int i = 0; i < ids.Length; i++) ids[i] = SortingLayer.layers[i].id;
        var f = typeof(Light2D).GetField("m_ApplyToSortingLayers", BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) f.SetValue(l, ids);
        else Debug.LogWarning("LightUtil: could not find Light2D sorting layer field; set 'Target Sorting Layers' by hand.");
    }
}
