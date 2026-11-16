using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class BuildingSave
{
    public string prefab;          // Building.displayName of the catalog prefab
    public Vector2Int anchor;
}

[Serializable]
public class WorkerSave
{
    public string workerName;
    public int kind;               // ResourceKind
    public int strength, dexterity, intelligence;
    public float fatigue;
    public int carry;
    public Vector2 position;
    public int homeIndex = -1;     // index into LevelSave.buildings, -1 = none
}

/// <summary>Everything needed to come back to a map later: the map itself plus the colony on it.</summary>
[Serializable]
public class LevelSave
{
    public int version = 1;
    public string mapId;
    public string savedAt;
    public float playSeconds;

    public MapData map;

    public int wood, stone;
    public int deliveredWood, deliveredStone;
    public int day;
    public float timeOfDay;
    public int goalIndex;

    public List<BuildingSave> buildings = new List<BuildingSave>();
    public List<WorkerSave> workers = new List<WorkerSave>();
}

/// <summary>JSON files in persistentDataPath/saves, one per map id. The overworld menu can list them later.</summary>
public static class MapStore
{
    public static string Dir { get { return Path.Combine(Application.persistentDataPath, "saves"); } }
    public static string PathFor(string mapId) { return Path.Combine(Dir, mapId + ".json"); }

    public static bool Exists(string mapId) { return File.Exists(PathFor(mapId)); }

    public static void Save(LevelSave save)
    {
        Directory.CreateDirectory(Dir);
        save.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string tmp = PathFor(save.mapId) + ".tmp";
        File.WriteAllText(tmp, JsonUtility.ToJson(save));
        if (File.Exists(PathFor(save.mapId))) File.Delete(PathFor(save.mapId));
        File.Move(tmp, PathFor(save.mapId));   // write-then-move so a crash never leaves a half-written save
    }

    public static LevelSave Load(string mapId)
    {
        return JsonUtility.FromJson<LevelSave>(File.ReadAllText(PathFor(mapId)));
    }

    public static void Delete(string mapId)
    {
        if (Exists(mapId)) File.Delete(PathFor(mapId));
    }

    public static List<string> ListIds()
    {
        var ids = new List<string>();
        if (!Directory.Exists(Dir)) return ids;
        foreach (var f in Directory.GetFiles(Dir, "*.json")) ids.Add(Path.GetFileNameWithoutExtension(f));
        ids.Sort();
        return ids;
    }
}
