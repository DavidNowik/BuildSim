using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Play-testing harness: runs a script of player actions in Play Mode and reports what happened.
/// Start it through the dev command file:  agent <path to script>
///   (script = text file, one command per line, '#' starts a comment)
///
/// Commands (cells are grid coordinates, quote names with spaces):
///   wait <realSeconds>            waitgame <gameSeconds>        speed <timeScale>
///   state                         cell <x> <y>                  selection
///   find free <w> <h> [near x y]  find shore|stone|wood|crate|water [near x y]
///   build "<Building>" <x> <y>    (the real path: pick prefab, hover cell, click; costs resources)
///   recruit stone|wood|fish       ui "<Button name>"            (clicks a real HUD button)
///   select <x> <y>                pay stone|wood|fish           demolish <x> <y>
///   cam <x> <y> [zoom 1-6]        shot <name>                   save
///   give stone|wood|fish <n>      (cheat, for testing)          call Class.Method
///   log <text>
/// Output: Temp/dev_result.txt (+ Temp/agent_report.txt); screenshots in Temp/shots.
/// Safe to delete (this file only; DevCommandRunner then loses the "agent" verb).
/// </summary>
[InitializeOnLoad]
public static class AgentHarness
{
    private static readonly string TempDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp");

    private static string[] lines;
    private static int idx;
    private static StringBuilder report = new StringBuilder();
    private static double waitRealUntil;
    private static float waitGameUntil = -1f;
    private static double started;

    static AgentHarness()
    {
        EditorApplication.update += Update;
        // an editor crash / forced stop during a run must never cost the player's saves: put them back on the next start
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetString("agent.state", "") == "" && !EditorApplication.isPlayingOrWillChangePlaymode) RestoreSaves();
        };
    }

    public static void Start(string scriptPath)
    {
        if (!File.Exists(scriptPath)) { DevCommandRunner.Finish("ERROR agent script not found: " + scriptPath); return; }
        for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
            if (UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i).isDirty) { DevCommandRunner.Finish("ERROR agent: the open scene has unsaved changes, save it first."); return; }
        if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path != LevelSceneBuilder.ScenePath)
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(LevelSceneBuilder.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        // the run must never touch the player's saves: back them up, restore them afterwards
        // (first script line "# fresh" starts from a brand new map instead of the saved one)
        string saves = MapStore.Dir, backup = Path.Combine(TempDir, "agent_saves_backup");
        if (Directory.Exists(backup)) Directory.Delete(backup, true);
        Directory.CreateDirectory(backup);
        if (Directory.Exists(saves))
        {
            foreach (string f in Directory.GetFiles(saves)) File.Copy(f, Path.Combine(backup, Path.GetFileName(f)));
            string first = File.ReadAllLines(scriptPath).Length > 0 ? File.ReadAllLines(scriptPath)[0] : "";
            if (first.ToLowerInvariant().Contains("fresh"))
                foreach (string f in Directory.GetFiles(saves, "*.json")) if (Path.GetFileName(f) != "world.json") File.Delete(f);
        }
        SessionState.SetString("agent.script", scriptPath);
        SessionState.SetString("agent.state", "entering");
        EditorApplication.isPlaying = true;
    }

    private static void RestoreSaves()
    {
        string saves = MapStore.Dir, backup = Path.Combine(TempDir, "agent_saves_backup");
        if (!Directory.Exists(backup)) return;
        Directory.CreateDirectory(saves);
        foreach (string f in Directory.GetFiles(saves)) File.Delete(f);
        foreach (string f in Directory.GetFiles(backup)) File.Copy(f, Path.Combine(saves, Path.GetFileName(f)));
        Directory.Delete(backup, true);
    }

    private static void Update()
    {
        string state = SessionState.GetString("agent.state", "");
        if (state == "") return;

        if (state == "entering")
        {
            if (!EditorApplication.isPlaying) return;
            var g = GridManager.instance;
            if (g == null || !g.IsInitialized || Time.timeSinceLevelLoad < 1.5f || LevelSession.Instance == null) return;
            Application.runInBackground = true;
            Time.maximumDeltaTime = 3f;
            lines = File.ReadAllLines(SessionState.GetString("agent.script", ""));
            idx = 0; report = new StringBuilder(); waitRealUntil = 0; waitGameUntil = -1f;
            started = EditorApplication.timeSinceStartup;
            SessionState.SetString("agent.state", "running");
            return;
        }

        if (state == "running")
        {
            if (!EditorApplication.isPlaying) { SessionState.SetString("agent.state", ""); Finish("ERROR play mode ended early\n" + report); return; }
            if (EditorApplication.timeSinceStartup - started > 900) { Stop("TIMEOUT after 15 minutes"); return; }
            if (EditorApplication.timeSinceStartup < waitRealUntil) return;
            if (waitGameUntil >= 0f && Time.time < waitGameUntil) return;
            waitGameUntil = -1f;

            while (idx < lines.Length)
            {
                string raw = lines[idx++].Trim();
                if (raw.Length == 0 || raw[0] == '#') continue;
                try { Run(raw); }
                catch (Exception e) { report.AppendLine("  ! " + e.GetType().Name + ": " + e.Message); }
                if (EditorApplication.timeSinceStartup < waitRealUntil || waitGameUntil >= 0f) return;
            }
            Stop("script finished");
            return;
        }

        if (state == "stopping")
        {
            if (EditorApplication.isPlaying) return;
            SessionState.SetString("agent.state", "");
            RestoreSaves();
            string text = report.ToString();
            File.WriteAllText(Path.Combine(TempDir, "agent_report.txt"), text);
            Finish("OK agent\n" + text);
        }
    }

    private static void Stop(string why)
    {
        report.AppendLine("== " + why + " ==");
        SessionState.SetString("agent.state", "stopping");
        Time.timeScale = 1f;
        EditorApplication.isPlaying = false;
    }

    private static void Finish(string text) { DevCommandRunner.Finish(text); }

    // ---------------------------------------------------------------- commands
    private static void Run(string raw)
    {
        var a = Tokens(raw);
        string cmd = a[0].ToLowerInvariant();
        report.AppendLine("> " + raw);
        var grid = GridManager.instance;
        switch (cmd)
        {
            case "wait": waitRealUntil = EditorApplication.timeSinceStartup + F(a, 1); break;
            case "waitgame": waitGameUntil = Time.time + F(a, 1); break;
            case "speed": Time.timeScale = F(a, 1); break;
            case "log": report.AppendLine("  " + raw.Substring(3).Trim()); break;
            case "shot": report.AppendLine("  " + DevCaptureUtil.Capture(Path.Combine(ShotDir(), a[1] + ".png"))); break;
            case "state": Dump(); break;
            case "selection": DumpSelection(); break;
            case "save": LevelSession.Instance.Save(true); report.AppendLine("  saved"); break;
            case "cell": DumpCell(I(a, 1), I(a, 2)); break;
            case "find": Find(a); break;
            case "cam":
                CameraMotor.Instance.FocusOn(grid.CellToWorld(new Vector2Int(I(a, 1), I(a, 2))));
                if (a.Count > 3) CameraMotor.Instance.SetZoomLevel(I(a, 3));
                break;
            case "give":
                ResourceManager.Instance.Add(Kind(a[1]), I(a, 2));
                report.AppendLine("  cheat: +" + I(a, 2) + " " + a[1]);
                break;
            case "recruit": report.AppendLine("  recruit " + a[1] + " -> " + Colony.Instance.Recruit(Kind(a[1]))); break;
            case "build": Build(a[1], I(a, 2), I(a, 3)); break;
            case "ui": ClickUi(a[1]); break;
            case "select": SelectionController.Instance.ClickCell(new Vector2Int(I(a, 1), I(a, 2))); DumpSelection(); break;
            case "pay":
                {
                    var c = SelectionController.Instance.SelectedCrate;
                    report.AppendLine("  pay -> " + (c != null && CrateManager.Instance.TryOpen(c, Kind(a[1]))));
                    break;
                }
            case "demolish":
                SelectionController.Instance.ClickCell(new Vector2Int(I(a, 1), I(a, 2)));
                if (SelectionController.Instance.SelectedBuilding == null) report.AppendLine("  no building there");
                else { report.AppendLine("  demolishing " + SelectionController.Instance.SelectedBuilding.displayName); SelectionController.Instance.DemolishSelected(); }
                break;
            case "call": CallStatic(a[1]); break;
            default: report.AppendLine("  ! unknown command"); break;
        }
    }

    private static void Build(string name, int x, int y)
    {
        var col = Colony.Instance;
        var prefab = col.PrefabByName(name);
        if (prefab == null) { report.AppendLine("  ! no such building: " + name + "   (have: " + string.Join(", ", col.catalog.ConvertAll(b => b.displayName)) + ")"); return; }
        PlacementController.Instance.Begin(prefab);
        string why;
        bool ok = PlacementController.Instance.ClickCell(new Vector2Int(x, y), out why);
        if (!ok) PlacementController.Instance.Cancel(false);
        report.AppendLine("  build " + name + " at " + x + "," + y + " -> " + (ok ? "placed" : "DENIED: " + why));
    }

    private static void ClickUi(string name)
    {
        foreach (var b in UnityEngine.Object.FindObjectsOfType<Button>())
        {
            if (!b.gameObject.activeInHierarchy || !b.interactable) continue;
            if (b.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) continue;
            b.onClick.Invoke();
            report.AppendLine("  clicked button '" + b.name + "'");
            return;
        }
        report.AppendLine("  ! no active, enabled button matching '" + name + "'");
    }

    private static void Find(List<string> a)
    {
        var grid = GridManager.instance;
        string what = a[1].ToLowerInvariant();
        Vector2Int near = grid.startCell;
        int ni = a.IndexOf("near");
        if (ni >= 0) near = new Vector2Int(I(a, ni + 1), I(a, ni + 2));
        var hits = new List<KeyValuePair<float, string>>();

        if (what == "free")
        {
            var size = new Vector2Int(I(a, 2), I(a, 3));
            for (int x = 0; x < grid.width; x++)
                for (int y = 0; y < grid.height; y++)
                {
                    var p = new Vector2Int(x, y);
                    if (!grid.CanPlace(p, size) || !BuildingManager.instance.HasDoorCell(p, size) || BuildingManager.instance.WouldBlockDoors(p, size)) continue;
                    hits.Add(new KeyValuePair<float, string>(Vector2Int.Distance(p, near), "anchor " + x + "," + y));
                }
        }
        else if (what == "crate")
        {
            foreach (var c in CrateManager.Instance.All)
                hits.Add(new KeyValuePair<float, string>(Vector2Int.Distance(c.data.anchor, near), "crate at " + c.data.anchor.x + "," + c.data.anchor.y + (c.Opened ? " (opened)" : " needs " + c.data.amount + " " + Products.TagName(c.Tag) + ", " + c.data.pearls + " pearls")));
        }
        else
        {
            ResourceKind k = what == "shore" || what == "fish" ? ResourceKind.Fish : (what == "stone" ? ResourceKind.Stone : ResourceKind.Wood);
            foreach (var c in grid.Nodes)
            {
                if (what == "water") break;
                if (c.resource != k) continue;
                hits.Add(new KeyValuePair<float, string>(Vector2Int.Distance(c.pos, near), what + " node at " + c.pos.x + "," + c.pos.y));
            }
            if (what == "water")
                for (int x = 0; x < grid.width; x++)
                    for (int y = 0; y < grid.height; y++)
                        if (grid.GetCell(x, y).terrain == TerrainKind.Water)
                            hits.Add(new KeyValuePair<float, string>(Vector2Int.Distance(new Vector2Int(x, y), near), "water at " + x + "," + y));
        }
        hits.Sort((p, q) => p.Key.CompareTo(q.Key));
        for (int i = 0; i < Mathf.Min(5, hits.Count); i++) report.AppendLine("  " + hits[i].Value + "   (dist " + hits[i].Key.ToString("0.0") + ")");
        if (hits.Count == 0) report.AppendLine("  nothing found");
    }

    private static void DumpCell(int x, int y)
    {
        var c = GridManager.instance.GetCell(x, y);
        if (c == null) { report.AppendLine("  outside the map"); return; }
        report.AppendLine("  " + x + "," + y + " terrain=" + c.terrain + " resource=" + c.resource + (c.HasNode ? " " + c.yield + "/" + c.maxYield : "")
            + " building=" + (c.building != null ? c.building.displayName : "-") + " crate=" + (c.crate != null) + " walkable=" + c.Walkable);
    }

    private static void DumpSelection()
    {
        var s = SelectionController.Instance;
        if (s.SelectedWorker != null) report.AppendLine("  selected worker " + s.SelectedWorker.workerName + " (" + s.SelectedWorker.JobTitle + ") " + s.SelectedWorker.StatusText);
        else if (s.SelectedBuilding != null) report.AppendLine("  selected building " + s.SelectedBuilding.displayName + " workers " + s.SelectedBuilding.residents.Count + "/" + s.SelectedBuilding.maxWorkers);
        else if (s.SelectedNode != null) report.AppendLine("  selected " + s.SelectedNode.resource + " node " + s.SelectedNode.pos + " " + s.SelectedNode.yield + "/" + s.SelectedNode.maxYield);
        else if (s.SelectedCrate != null) report.AppendLine("  selected crate " + s.SelectedCrate.data.anchor + (s.SelectedCrate.Opened ? " (opened)" : " needs " + s.SelectedCrate.data.amount + " " + Products.TagName(s.SelectedCrate.Tag)));
        else report.AppendLine("  nothing selected");
    }

    private static void Dump()
    {
        var rm = ResourceManager.Instance; var g = GridManager.instance; var col = Colony.Instance;
        report.AppendLine("  map " + g.width + "x" + g.height + " start " + g.startCell + "  game time " + Time.time.ToString("0") + "s  speed x" + Time.timeScale);
        report.AppendLine("  stock: stone " + rm.Get(ResourceKind.Stone) + "  wood " + rm.Get(ResourceKind.Wood) + "  fish " + rm.Get(ResourceKind.Fish)
            + "   delivered s/w/f " + rm.TotalDelivered(ResourceKind.Stone) + "/" + rm.TotalDelivered(ResourceKind.Wood) + "/" + rm.TotalDelivered(ResourceKind.Fish));
        report.AppendLine("  population " + col.Population + "/" + col.PopulationCap + "   goal " + col.GoalNumber + "/" + col.GoalCount + ": " + col.GoalTitle + " (" + col.GoalProgress + "/" + col.GoalTarget + ")");
        foreach (var b in BuildingManager.instance.All)
            if (b.IsPlaced) report.AppendLine("  building " + b.displayName + " at " + b.Anchor.x + "," + b.Anchor.y + " workers " + b.residents.Count + "/" + b.maxWorkers);
        foreach (var w in Worker.All)
            report.AppendLine("  worker " + w.workerName + " (" + w.JobTitle + ") " + w.StatusText + " carrying " + w.Carry + " at " + g.WorldToCell(w.transform.position).x + "," + g.WorldToCell(w.transform.position).y);
        foreach (var c in CrateManager.Instance.All)
            report.AppendLine("  crate at " + c.data.anchor.x + "," + c.data.anchor.y + (c.Opened ? " opened" : " locked, needs " + c.data.amount + " " + Products.TagName(c.Tag) + " for " + c.data.pearls + " pearls"));
    }

    private static void CallStatic(string call)
    {
        string[] p = call.Split('.');
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(p[0]);
            var m = t != null ? t.GetMethod(p[1], System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) : null;
            if (m != null) { var r = m.Invoke(null, null); report.AppendLine("  -> " + (r != null ? r.ToString() : "ok")); return; }
        }
        report.AppendLine("  ! call target not found");
    }

    // ---------------------------------------------------------------- helpers
    private static string ShotDir() { string d = Path.Combine(TempDir, "shots"); Directory.CreateDirectory(d); return d; }
    private static float F(List<string> a, int i) { return float.Parse(a[i], CultureInfo.InvariantCulture); }
    private static int I(List<string> a, int i) { return int.Parse(a[i], CultureInfo.InvariantCulture); }

    private static ResourceKind Kind(string s)
    {
        switch (s.ToLowerInvariant())
        {
            case "stone": return ResourceKind.Stone;
            case "wood": return ResourceKind.Wood;
            case "fish": return ResourceKind.Fish;
            default: throw new Exception("unknown product: " + s);
        }
    }

    private static List<string> Tokens(string line)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        foreach (char ch in line)
        {
            if (ch == '"') { quoted = !quoted; continue; }
            if (ch == ' ' && !quoted) { if (sb.Length > 0) { list.Add(sb.ToString()); sb.Length = 0; } continue; }
            sb.Append(ch);
        }
        if (sb.Length > 0) list.Add(sb.ToString());
        return list;
    }
}
