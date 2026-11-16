# BuildSim – Implementation status (refactor M0–M6)

Baseline commit (before any change): `02fe250 Baseline before refactor`. Everything below is uncommitted working-tree changes.
**Verification level:** all scripts compile cleanly with Unity's own Roslyn (`csc` with the project's response file). **Nothing has been play-tested in the editor yet** — see "First run checklist".

## Controls
| Input | Action |
|---|---|
| WASD / arrows, middle-mouse drag | pan (clamped to map), Home recentres |
| Mouse wheel | integer zoom (1x–6x) toward the cursor |
| 1 / 2 / 3 | build Storage / Miner Camp / Woodcutter Camp |
| 4 / 5 | recruit Miner / Woodcutter (needs a free bed + resources) |
| LMB / RMB / Esc / Shift | place / cancel / cancel / keep placing |
| Click a worker, building or deposit | inspector panel; Del = demolish (50 % refund) |
| Space, + / - | pause, game speed (1x/2x/4x) |
| M, H | mute, hide help |

## What was done per milestone
**M0 – hotfixes / safety**: git baseline + `.gitignore`; removed all `UnityEditor` usings from runtime code (build blocker); ghost alpha bug gone (ghost is tinted separately); occupancy wipe gone (state lives in `Cell`, nothing polls); removed 600 per-tile coroutines/materials; no per-tick `Debug.Log`; non-UTF-8 `CameraMotor.cs` replaced.

**M1 – look**: procedural pixel-art ground in ONE texture (grass tones, tufts, flowers, bushes in the border, dirt where deposits were mined); build-mode grid overlay (fades in/out); blob shadows under workers/buildings; Y-sorting of workers and buildings; sprites snapped to the pixel grid; pixel-perfect camera (integer zoom + snapped position); warm global light with a day/night colour cycle; camps glow at night; vignette + colour-grade post-processing (runtime Volume). Miner prefab child offset (3.2, 1.7) zeroed.

**M2 – grid/placement**: `Cell` data model in `GridManager` (resource nodes seeded once from the painted Tilemap, which is now only a view); footprint-based placement (`footprint` on each prefab, validated per cell, entrance cell required); per-cell green/red overlay, entrance marker, pop/dust/shake on place, deny feedback; clicks on UI never place.

**M3 – economy**: `ResourceManager` stockpile (start 50 wood / 10 stone), building + recruit costs, HUD with count-up numbers and flying icons from storage to the counter, goal chain with rewards.

**M4 – workers**: `Worker` state machine (Idle → GoToNode → Gather → GoToStorage → … → GoToBed → Sleep), deltaTime timers (any game speed), path computed once and re-planned only if blocked, node reservation (no double-booking), partial loads are delivered when nothing is left, no cached stale tiles, float stats, binary-heap A* with reused buffers, mining strikes synced to the 12 fps mining clip, workers enter camps to sleep (fade out, tent lights up).

**M5 – feel**: procedural audio (strikes, thuds, chimes, deposit, UI, ambient wind + birds, soft generative pentatonic music), pooled pixel particles, merged floating numbers (+1 +1 +1 → +3) with pop/rise/fade, camera shake, hover pop on buttons, game speed controls, toasts.

**M6 – loop**: Woodcutter chain (same prefab, green tint, gathers wood), camps = beds = population cap, recruiting, six goals with rewards.

## Design decisions that deviate from the guide
- **ScriptableObject definitions → serialized data on the building prefabs** (`displayName, role, footprint, costWood, costStone, beds`). Same data-driven effect without hand-writing `.asset` YAML; can be converted to SOs later.
- **No Pixel Perfect Camera component:** `CameraMotor` does integer zoom itself (fewer moving parts, works with the existing camera).
- **Ground is procedural, not Rule Tiles** (fast, one draw, mined patches paintable at runtime).
- **Audio is synthesised** (no asset files needed). Easy to swap for real clips later.
- Prefabs moved to `Assets/Resources/Buildings` and `Assets/Resources/Prefabs/Miner.prefab` (GUIDs preserved) so `Colony` can load them.
- The 3 old `DebugButton Build*` objects are **hidden at runtime** by `GameBootstrap` (not deleted from the scene file, to avoid editing the scene while the editor is open). Delete them in the editor when convenient.

## Cleanup pass (after first playtest)
Removed everything not reachable from the scene, `Resources/` or ProjectSettings (GUID reference graph), plus dead code:
- **Assets:** 226 unreferenced files (the unused "2 Objects" tile/decor/grass/box/house packs + PSDs, Blue/Original character sets except `MiniMiner.png`, `Font/House/Worker.png`, FloatingText anim/controller, `Terrain.prefab`, `MinifolksVillagers2.7z`) and the empty folders. Kept: miner animations/controllers, 4 tent sprites, `Tileset_Stones_16x16.png`, Stone/Wood tile assets, URP settings. All deleted art is still in git baseline `02fe250`.
- **Scene:** removed the old Canvas with the 3 debug build buttons (32 objects) and stale serialized fields (`tilePrefab`, old camera fields, `textContainer/textPrefab`).
- **Code:** unused events (`GridManager.CellChanged`, `BuildingManager.Changed`, `ResourceManager.Delivered`), `CellAtWorld`, `UsedBeds`, `Building.type` + `EnterPlacementMode`, write-only `Worker.goalCell`, unused `ShowText` overloads, `Layers.Roof`, debug-button hiding in `GameBootstrap`.
- **Packages:** removed unused direct dependencies (collab-proxy, visual scripting, test framework, TextMeshPro, Timeline, Rider integration, and the unused 3D/XR/web/analytics/video/terrain/AI built-in modules).
- Kept on purpose: `Settings/Lit2DSceneTemplate` + `Scenes/URP2DSceneTemplate` (Unity's own "New 2D scene" template).

## Files
New: `Scripts/Core/{Defs,Art,ResourceManager,GameClock,SelectionController,Colony,GameBootstrap}.cs`, `Scripts/Fx/{FxManager,GroundRenderer,GameAudio,Lighting}.cs`, `Scripts/UI/HUD.cs`, `Scripts/Buildings/PlacementController.cs`.
Rewritten: `GridManager, PathFinder, Building, BuildingManager, Worker, Miner, CameraMotor, FloatingTextManager`.
Deleted: `Tile.cs, RessourceNode.cs, StoneTent.cs, WoodTent.cs, FloatingText.cs`, prefabs `Tile, StoneNode, WoodNode`.
Everything is wired at runtime by `GameBootstrap` (`[RuntimeInitializeOnLoadMethod]`); the scene needs no manual setup.

## First run checklist (things I could not verify without running Unity)
1. Let Unity recompile (focus the editor); Console should show 0 errors. If you see missing-script warnings on the scene's GridManager/FloatingTextManager, it is only the removed `tilePrefab/textPrefab` fields — harmless.
2. Press Play: expect green ground, one camp + storage near the stones with one miner working, HUD on top, build bar at the bottom.
3. Check: building the Woodcutter Camp (needs 10 wood + 10 stone) and recruiting a Woodcutter; wood nodes are `Wood` tiles painted on the RessourceMap — if none exist inside the 30x20 grid, woodcutters will show "No wood left".
4. Tent footprint: the existing collider offset is used as the "feet"; if a building looks shifted by a cell, adjust its `footprint` or collider offset on the prefab.
5. If post-processing/lights misbehave (URP versions differ), `PostFx.Setup` and `CampLight` are isolated in `Lighting.cs` and can be disabled in `GameBootstrap` / `Building.Place`.
6. Balance numbers to tune: `Worker.UpdateStats`, `Colony.RecruitCost`, `Colony.BuildGoals`, `ResourceManager.start*`, prefab costs.

## Not done / suggestions
- New character art for the Woodcutter (currently a tint of the Miner), tree sprites if wood nodes should look different from stone.
- Deleting ~650 unused textures and the `.7z` from `Assets/` (left untouched on purpose – your art).
- Save/load, real music/SFX assets, tutorial, unit tests (the A* and grid are now plain classes and easy to test).
