# BuildSim – Technical Analysis (for dry_review)

Date: 2026-11-16 · Unity 2022.3.8f1 · URP 14 / 2D Renderer · legacy Input Manager · no git repo, no tests, no asmdefs.
Scope: everything under `Assets/` (14 scripts, 8 prefabs, 1 scene, animations, art) + ProjectSettings.
All findings are from reading the code/YAML; **nothing was run in the editor**, so items marked (verify) need a play-mode check.

## 1. What the game is today
A 30x20 grid colony sim. Workers (`Miner`) path with A* to the nearest stone node (a tile painted on a `Tilemap` called RessourceMap), mine it (`yield` 100/node), carry up to 5, walk to a `storage` building, "unload" (floating text only), get tired after ~100 s, walk to a `stonetent` and sleep ~5 s. The player places buildings from 3 debug UI buttons (ghost follows mouse, LMB place, RMB cancel). Camera: MMB drag + wheel zoom.

## 2. Architecture overview
```
GridManager (singleton)  --spawns-->  600 x Tile GameObjects (SpriteRenderer + Collider2D + Tile.cs)
Tile.cs  --polls Tilemap every 0.1–1 s--> decides IsOccupied / adds RessourceNode / sets tag "stone"|"wood"
BuildingManager (singleton) : List<Building>, lookup by string `type`
Building -> StoneTent (animator + worker registry), WoodTent (EMPTY stub)
Worker -> Miner : coroutine "CheckTasks" every 0.5 s = the whole AI; Update() does movement
PathFinder : static A*, list-based open set
FloatingTextManager : pooled legacy UI Text
```
**Core design problem: source of truth is inverted.** The painted Tilemap (a visual) is the data; 600 GameObjects poll it and derive gameplay state from the *tile asset's name string*. Game state should live in plain data (cell array / ScriptableObject defs) and the view should be generated from it.

## 3. Bugs (ranked)

### Critical / gameplay-breaking
1. **Building occupancy is wiped by the tile poll.** `Tile.CheckTilemap()` calls `EmptyNode()` (`IsOccupied=false; Occupier=null`) whenever there is no tilemap tile under the cell — which is every cell without a resource. `Building.PlaceBuilding` sets occupied once, then within ≤1 s the poll clears it. Result: paths go through buildings, buildings can be placed on buildings. (verify in play mode; the fix is the same regardless — Tile.cs:62–67)
2. **Editor-only namespaces in runtime code → player build fails.** `PathFinder.cs:2 using UnityEditor;`, `Miner.cs:4 using UnityEditor.Experimental.GraphView;`. Also unused `UnityEngine.Timeline` in `Tile.cs:4`. Remove all three.
3. **Miner idles while no tent exists.** `Miner.CheckTasks` sets `resting=true`; `GoToRest` returns early without a `stonetent`. *(Corrected by dry_review: not permanent — re-evaluated every tick, recovers once a tent is built; same pattern for full inventory with no storage. Rest takes ~3.5 s, not 5.)* Still a UX gap: no "no bed" feedback. Also `Worker.GoTo` → `FindPath(start,…)` throws ArgumentNullException (null dictionary key) if `start` is null (worker off-grid).
4. **The loop has no payoff.** Unloading only shows "+N" text; there is no stockpile/resource count, UI or cost for buildings. Nothing the player does changes anything measurable.

### High
5. **Ghost loses its transparency.** `SetTransparency(0.5f)` is overwritten every frame by `spriteRenderer.color = valid ? Color.green : Color.red` (alpha 1). Building.cs:104–105.
6. **Placing through UI.** Click on a build button/panel while a ghost is active also triggers `GetMouseButtonDown(0)` placement; no `EventSystem.IsPointerOverGameObject()` check. No ESC cancel, no repeat-place, no rotation.
7. **Footprint is physics-based, not cell-based.** Validity = `Physics2D.OverlapBoxAll(col.bounds)` against tile colliders. Collider on StoneTent is 2.41x0.83 → footprint is whatever the collider overlaps, ghost snaps by pivot to the rounded cell. Out-of-bounds is not rejected (mouse is clamped instead, so a wide building can hang off the map and still be "valid"). Workers' "adjacent walkable tile" is computed from `occupiedTiles`, which depends on this.
8. **Static `Tile.updateTimer` is never reset after placing/cancelling** (`Building.Update` returns at `if (placed)` before the reset line), so the poll stays at 0.1 s forever = 600 coroutines × 10 Hz. Also each tile calls `FindObjectOfType<Tilemap>()` in Awake (600 scene scans) and uses `spriteRenderer.material` / `rend.material` (instantiates 600+ material copies → batching broken).
9. **Re-pathing every 0.5 s while walking.** `CheckGoToMine`/`CheckUnload` call `GoTo()` each tick, which rebuilds the A* path and resets `currentIndex=0` from `PosToTile()` (rounded position). Workers double back to the nearest tile centre mid-step → visible stutter, wasted CPU.
10. **Game logic is quantised to the 0.5 s AI tick.** Work progress is `workCounter += 0.5f` per tick, fatigue `+= 0.5/intelligence` per tick, rest `+= 0.5 + int/10` per tick. Animation state changes (idle→mine) can lag up to 0.5 s; every balance number is secretly "per half second". Use `Time.deltaTime` accumulators / a state machine.
11. **Grid coordinate assumptions.** `PosToTile` and `Building.FollowMouse` use `RoundToInt(world / spacing)` ignoring grid origin; `Worker.PosToTile` ignores `tileSpacing` entirely. Works only because origin=0, spacing=1. Needs one `WorldToCell/CellToWorld` pair in GridManager.

### Medium
12. A*: list-based open set (O(n²)), 4 Dictionaries + HashSet + List allocated per call, Euclidean heuristic on a 4-connected grid (should be Manhattan; with Euclid it still finds shortest but expands more). No path smoothing, no diagonal moves, no path cache. Fine at 600 cells × 1 worker; will not scale.
13. `FindClosestAccessibleResourceTile`: `FindGameObjectsWithTag` + full sort per call; `maxAttempts=10`; no node reservation → several miners pile on the same node. No fallback if all 10 nearest are enclosed.
14. `RessourceNode.Farm()` removes the visual from `FindObjectOfType<Tilemap>()` (first tilemap found — fragile) and `Destroy(this)` (component only). Tile keeps tag until set to "Untagged"; poll later resets.
15. Resource/building types are free strings: `"stonetent"`, `"storage"`, `"stone"`, `"Stone"`, `"Wood"`, tags `"stone"`, `"wood"`; the tile asset *name* is the type. One typo = silent failure. Replace with enums or ScriptableObject definitions.
16. `StoneTent.workerCount` is public and duplicates `registeredWorkers.Count`; `RegisterWorker` can silently fail (capacity 2) and the Miner ignores the return value → worker thinks it has a bed. `DeleteWorker` is never called (workers/buildings destroyed leave stale entries). `BuildingManager.UnregisterBuilding` never called.
17. `Miner.GoToRest` paints the sprite black as "sleeping" and finds `transform.GetChild(0)` repeatedly; `Worker.Awake` assumes child 0 has the Animator. Hard-wired hierarchy.
18. `GridManager.Awake` sets `instance` *after* `InitializeGrid()`; if a destroyed duplicate returns early `grid` is null for the survivor path only — OK today, but ordering is fragile; `IsInitialized` busy-wait in Worker coroutine papers over it.
19. `FloatingText.UpdateFloatingText` keeps repositioning after `Hide()` (no `return`), rise speed `0.001/frame` (frame-rate dependent and invisible), no fade/ease, default colour black. `Camera.main` every frame. Legacy `UnityEngine.UI.Text` (blurry, no TMP). `FloatingText.gameObject` field unused.
20. `Debug.Log` in every AI tick per worker and in every tile init (600 logs at start) — console spam and GC.
21. `CameraMotor`: `Lerp(a,b,0.1f)` per frame is frame-rate dependent; `panSpeed` unused; no bounds clamp (can pan off map); zoom does not zoom toward cursor; ortho size lerps through fractional values → pixel-art shimmer. File has a non-UTF-8 byte (apostrophe in a comment, line 51).
22. `WoodTent.cs` is an empty MonoBehaviour (not a Building); wood nodes exist but there is no woodcutter worker.
23. Naming: "Ressource" (sic) used consistently in code — fine but misspelled in API; mixed German comments.
24. No namespaces/asmdefs, no tests, no source control (not a git repo; Library/Temp/obj present). **Recommend `git init` + Unity .gitignore before any refactor** so we can scrap safely.

### Added after dry_review (verified by them)
- **Occupancy fix is more than "stop EmptyNode":** `GetAnyAdjacentWalkableTile` only tests neighbours' `IsOccupied`, so once the wipe is fixed it can still hand out a rest/unload tile *inside the same building's footprint*. Use a separate `occupiedByBuilding` flag / exclude the building's own tiles. Until fixed, pathing and `GetFreeNeighborTile` treat footprints as walkable.
- **E1. Partial loads never delivered.** `CheckUnload` only walks to storage when `carry >= capacity`; if the last node runs dry while carrying 1–4, the miner never unloads.
- **E2. Cached `unloadTile`/`restBuilding` never re-validated.** If a later building occupies that tile, `FindPath` returns null forever (occupied goal never entered) and delivery silently stops.
- **E3. Int division in `UpdateStats`:** `moveSpeed = dexterity/2`, `carryCapacity = strength/2` truncate (dex 11 → 5); `workTimer` is float.
- **E4.** `Destroy(this)` on depleted node is deferred → two miners on one node re-search next tick; `Farm()` calls `FindObjectOfType<Tilemap>()` each depletion.
- **E5.** `GoTo` is called every tick even when already standing on the target: full A*, snap to tile centre, `IsWorking=false` then re-set by `CheckWork` same tick.
- **E6. Camera starts fully zoomed in:** `minZoom=5` == scene ortho size 5 → only 10 of the grid's 20 rows visible; no bounds clamp; `panSpeed` unused; non-UTF8 byte is the apostrophe in "doesn't" (CameraMotor.cs:51).
- Per-tile `FindObjectOfType<Tilemap>()` is safe only because the scene has exactly one Tilemap (latent, not live).
- Canvas Scaler = Constant Pixel Size, scale 1 (ref 800x600 ignored) → UI doesn't scale with resolution.

## 4. Scene / assets / settings findings
- **Pixel density is actually consistent (correction from dry_review).** Prefabs use WorkerCamps (195x62), ZZZs, TopStorageTent, StorageTent — all 16 PPU, point filter. The bilinear 100-PPU `2 Objects/6 Tent/1,2,4.png` are referenced by nothing. The real issue is **non-integer child scales** (1.25 and 0.8 in StoneTent.prefab) → uneven pixel sizes next to 1:1 workers. Also ~211 unused bilinear textures = import bloat only.
- Three different art packs coexist (Minifolks 16px, "2 Objects" ~HD-ish pack, Tileset_Stones_16x16) + ~600 unused textures (66+65 tile PNGs, PSDs, Blue/Original sets, a `MinifolksVillagers2.7z` in `Assets/Settings`). Library bloat and confusion; an archive inside Assets.
- Sorting uses fixed **layers per object type** (Tiles/Default/Worker/…) instead of Y-sorting, so a worker always draws over a building even when behind it.
- URP 2D Renderer present, only a Global Light 2D; no post-processing, no pixel-perfect camera, MSAA off, HDR on (unneeded for 2D pixel art). VSync varies by quality level (0 on first two).
- UI: legacy Text, three "DebugButton Build" buttons, no resource display. Canvas scaling not verified.
- Tags: `stone`, `wood` only. Layers unused. Physics2D colliders on every tile only to support the placement overlap test.
- Audio: none. Particles: none. Animations: Miner idle/run/mine, tent idle/resting, floating text.

## 5. What is good (keep)
- Singleton + manager split is easy to follow; `Worker`/`Miner` inheritance is a reasonable seed for job types.
- Stats (strength/dexterity/intelligence → capacity/speed/work time) are a good hook for personality and progression.
- Chequerboard alpha tiles + tile highlight during placement is the right UX idea.
- Pooled floating text, ghost placement flow, tent "someone resting" animation toggle are good small touches.
- A* with `MoveCost` per tile is already terrain-aware (roads/mud ready).

## 6. Recommended re-architecture (details in POLISH_GUIDE.md §A)
1. **Grid data model** (`struct Cell { TerrainId, NodeId, Occupant, Walkable }`) as the single source of truth; Tilemap/sprites are *views* fed from it. Delete per-tile GameObjects and the polling coroutines. One `WorldToCell/CellToWorld`.
2. **ScriptableObject definitions** for `ResourceDef`, `BuildingDef` (footprint size, cost, sprite, tags), `WorkerDef`.
3. **Event-driven state:** `OnCellChanged`, `OnResourceChanged`; UI and visuals subscribe.
4. **Worker brain = small state machine + job queue** with a central `JobBoard` (reserves nodes/beds, so workers never double-book or get stuck).
5. **ResourceManager / Stockpile** + a top-bar HUD so the loop has meaning.
6. **Pathfinding:** binary-heap A* (or flow-field per resource), Manhattan heuristic, reused buffers, path requests only on change.
7. **Input layer** (placement controller with UI-blocking, ESC, hotkeys, repeat-place, footprint-based validity).

## 7. Suggested order of work (low risk first)
0. `git init`, .gitignore, commit baseline.
1. Remove editor usings; fix ghost alpha; fix occupancy wipe (separate `occupiedByBuilding` flag so the poll only touches resource state) **and** exclude a building's own footprint tiles from rest/unload tile selection (else workers rest inside tents); reset `Tile.updateTimer`; stop Debug.Log spam. (no design change)
   Note: build buttons call `EnterPlacementMode` on prefab assets, so the timer reset never happens; M4 (worker rewrite) must precede game-speed buttons since the 0.5 s tick and static timer break first at 4x.
2. Decide art direction (see guide §B) – everything else depends on it.
3. Grid data model + placement rewrite.
4. Resource economy + HUD.
5. Worker state machine + JobBoard.
6. Juice pass (guide §C–D).

## 8. Open questions for dry_review
- Please confirm/refute #1 (occupancy wipe) and #8 (timer never reset) by reading `Tile.cs` + `Building.cs`.
- Anything in `.meta`/scene YAML that contradicts §4 (e.g. the Canvas scaler, camera ortho size — I could not find `m_OrthographicSize` with my grep and did not read the camera block fully)?
- Any code paths I missed that reset `IsOccupied` after placement?
