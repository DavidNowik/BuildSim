# BuildSim: working rules

Unity 2022.3.8f1 (URP, 2D). A cell-grid base builder: A* workers with a state machine, placement controller, HUD, procedural art, map generator, overworld.
The user playtests in Unity, which is usually open. Never edit scenes (`*.unity`) by script while it is open; use the editor scripts in `Assets/Editor` and tell the user to save.

## Git
- The user handles git. Never commit, push, branch or merge unless asked. Never offer to.
- When asked for a commit: write the message yourself, fitting the change, no Co-Authored-By trailer.
- No local checkout or merge while the editor is open (scene and asset locks).

## How to work
- KEEP IT SIMPLE. This is the most important rule. Never overcomplicate. Pick the easiest solution that works, in code, design and explanations. If a fix needs a lot of new code, a new system or an abstraction, stop and find a simpler way, or ask.
- Unsure about a choice: ask first, before acting.
- No extra abstractions, no scope creep, no features nobody asked for.
- Dead or replaced code, scripts, scene objects and assets get deleted outright (with their `.meta`). Git is the backup.
- Few code comments. If one is needed, a compact one-liner.
- When told to do X, do X. No review rounds unless asked.
- One MonoBehaviour per file, named like the class. A component saved into a scene needs this or the scene breaks.
- Edit code with the edit tool, not shell heredocs (escape sequences like `\n` get mangled).
- Formulas, rules, algorithms: mathematical notation and academic writing are welcome.
- Pixel art and generated art: no drawn limbs; keep to the existing procedural art style.

## Answers
- Short: what changed, what to test. No recaps of what the user already knows.
- Report honestly. Say what was run and what was not. Never claim something works that was not tested.

## Verifying changes
1. After code changes check that it compiles: read the console errors the editor reports (see the harness below), or compile with csc against the project's `.rsp` if the editor is closed.
2. After many changes, or any change to gameplay (building, workers, resources, map, HUD), PLAY it with the agent harness. Do not just read the code and say it should work.

## Play-test switch
`Assets/Comms/playtest.txt` holds one word, set by the user. Read it before any testing.
- `false`: never play-test (no harness, no Play Mode, no screenshots). Only quick validation that needs no Unity focus (compile check, reading code). Say that nothing was played.
- `true`: go all out with the harness below.

## Agent harness (play-testing)
Files: `Assets/Editor/AgentHarness.cs` (runs a script of player actions in Play Mode), `Assets/Editor/DevCommandRunner.cs` (command file bridge), `Assets/Editor/DevCaptureUtil.cs` (screenshots).
Needs: the Unity editor open, not compiling, and the open scene saved (the harness refuses a dirty scene). If nothing happens, the editor may be compiling or unfocused: do not wait on it or ask the user to focus it unless the task cannot continue otherwise. Finish the work, guess what works, and say what was not verified.

How to run:
1. Delete the old `Temp/dev_result.txt`.
2. Write the test script (text file, one command per line, `#` for comments) somewhere outside `Assets`, for example the scratchpad.
   First line `# fresh` starts from a brand new map instead of the saved one.
3. Write one line `agent <absolute path to script>` into `Temp/dev_command.txt`. The editor polls it about every 0.4 s and deletes it.
4. Wait for `Temp/dev_result.txt` (and `Temp/agent_report.txt`). It holds the result plus console errors and warnings from the run.
5. Look at the screenshots in `Temp/shots/` (Read them) and judge what is on screen, not only the log.
The harness backs up the player's saves and restores them afterwards, so runs never cost saves.

Other commands for `Temp/dev_command.txt`:
- `menu <Menu/Path>`: run an editor menu item.
- `shot <name>`: screenshot of the editor view.
- `play <seconds> <name> [Class.Method]`: enter Play Mode, optionally call a static method, shoot after N seconds.

Script commands:
- Time: `wait <realSeconds>`, `waitgame <gameSeconds>`, `speed <timeScale>`.
- Look: `state`, `cell <x> <y>`, `selection`, `cam <x> <y> [zoom 1-6]`, `shot <name>`, `log <text>`.
- Find: `find free <w> <h> [near x y]`, `find shore|stone|wood|crate|water [near x y]`.
- Act (the real player path): `build "<Building>" <x> <y>` (picks the prefab, hovers the cell, clicks; costs resources), `recruit stone|wood|fish`, `ui "<Button name>"`, `select <x> <y>`, `pay stone|wood|fish`, `demolish <x> <y>`.
- Cheats for testing: `give stone|wood|fish <n>`, `call Class.Method`, `save`.

When to use it:
- After a batch of changes: write a script that exercises every changed feature the way a player would, run it, read the report, view the screenshots.
- If the run finds a bug, report it and ask before fixing.
- Test the whole loop (build, recruit, workers gather and deliver, resources update), not only the new part, to catch regressions.
- In the answer: list what the run covered and what it did not (the harness cannot judge feel, balance or art taste; the user still playtests that).
- If a command the test needs is missing, add it to `AgentHarness.cs` (keep it small), do not work around it.
- Never leave test cheats or debug code in the game scripts.

## Project notes (add as they come up)
- Corrections from the user: save the lesson here or to memory right away.
- Build or editor quirks and "never do this" items go here.
