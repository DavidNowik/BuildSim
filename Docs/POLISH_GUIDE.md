# BuildSim – Visual & Game-Feel Guide (+ proposed redesign)

Principle: **the idea matters, not the current implementation.** The idea = "place camps, little workers autonomously gather and rest, you grow a colony." Everything below keeps that idea and replaces whatever serves it badly. Items marked **[SCRAP]** are things I recommend deleting and rebuilding; **[KEEP]** stays; **[DECIDE]** needs your call.

---
## A. Core systems – what to scrap and what replaces it

| Today | Verdict | Replacement |
|---|---|---|
| 600 `Tile` GameObjects polling a Tilemap by coroutine; type = tile asset name | **[SCRAP]** | `GridData`: `Cell[,]` (terrain, node, occupant, walkable, moveCost). Tilemaps are rendered *from* data. 0 GameObjects per cell. |
| Physics2D overlap for placement validity | **[SCRAP]** | `BuildingDef.footprint (w,h)` → check cells directly. Deterministic, out-of-bounds aware, instant. |
| String types (`"stonetent"`) | **[SCRAP]** | ScriptableObjects: `ResourceDef`, `BuildingDef`, `WorkerDef`. Add content without code. |
| `Miner.CheckTasks` 0.5 s tick with nested ifs | **[SCRAP]** | Worker state machine: `Idle → MoveToNode → Work → MoveToStorage → Deposit → (Rest)`; per-frame timers with `deltaTime`; animation driven by state. |
| Each worker picks its own node/bed | **[SCRAP]** | `JobBoard` hands out reserved nodes/beds; never double-booked, never stuck (if no bed: worker rests in place / idles with a "!" bubble). |
| List-based A* with dictionaries | **[SCRAP]** | Binary-heap A*, Manhattan heuristic, pooled buffers; or one **flow field** per resource type ("all workers share one Dijkstra map from storage") – best for many workers. |
| Floating text via legacy UI Text | **[SCRAP]** | World-space TextMeshPro pool with rise/fade/scale-pop curves (see §D). |
| Singleton managers, ghost via `Instantiate(this)` | **[KEEP/refine]** | Keep singletons for now; replace ghost with a `PlacementController` that owns one ghost sprite + footprint overlay. |
| Worker stats (STR/DEX/INT) | **[KEEP]** | Great hook. Add traits/names later. |
| Chequerboard + tile highlight when placing | **[KEEP]** | Draw it with one Tilemap/shader instead of 600 sprites. |

Proposed folder/namespace layout: `Core/` (GridData, Defs, Events), `Gameplay/` (Workers, Jobs, Buildings, Resources), `View/` (Tilemap renderer, FX), `UI/`, `Input/`.

---
## B. Visual direction (do this first; everything depends on it)

**B1. One pixel density, one art style. [mostly settled]**
Verified: everything the prefabs actually use is already 16 PPU / point filter (workers, tents, storage). The 100-PPU bilinear tent files are unused. So the 16 px direction is basically already in place; real fixes are the **non-integer child scales (1.25 / 0.8 in StoneTent.prefab)** — re-export those sprites at the needed size instead — and deleting the unused bilinear set. Minifolks has more workers (Gatherer, Blacksmith, Hunter…) in Original/Blue for later.
Rule: **PPU identical for all sprites, filter = Point, compression = None, Mesh Type = Full Rect**, no per-prefab non-uniform scaling.

**B2. Pixel-perfect rendering.**
Add `Pixel Perfect Camera` (URP 2D): Assets PPU 16, reference e.g. 480x270, Upscale Render Texture on, Crop Frame off. Zoom in **integer steps** (e.g. 1x,2x,3x,4x) → no shimmering. Snap camera to pixel grid after the smoothing.

**B3. Ground.**
- Replace the flat blue background with a real terrain layer (grass base from `Textures/2 Objects/1 Tiles` / FieldsTileset with Rule Tiles for edges). 
- Break repetition: 3–4 grass variants weighted randomly, sprinkle `5 Grass` tufts and `3 Decor` flowers/rocks at 5–10 % density, none on cells you want free (or let building placement erase them).
- Chequerboard: remove from gameplay view; show the grid **only in build mode** (fade in 0→0.15 alpha over 0.15 s).

**B4. Depth & layering.**
- Switch the 2D Renderer **Transparency Sort Mode = Custom Axis (0,1,0)** and put all dynamic things (workers, buildings, trees, rocks) on **one sorting layer**; pivot at the feet. Workers then correctly walk behind tents and trees.
- Keep layers only for: Ground, Decals, Objects(Y-sorted), Overlays(ghost/selection), FX.
- Add the **Shadow** sprites you already own (`1 Shadow`) as a child blob under workers/buildings (alpha ~0.35, multiply look).

**B5. Lighting & colour.**
- Use the Light 2D you have properly: a warm global light (≈ #FFF1D6, intensity 0.9) + point lights on camps (flicker 0.9–1.1) to give a cosy feel. Add a slow **day/night gradient** on the Global Light (even 4 colour keys over 3 minutes feels huge).
- Add Post-processing (Volume): subtle Vignette 0.2, Color Adjustments (+ saturation 10), Bloom threshold high (only lights glow). Turn **HDR off** unless using bloom; keep MSAA off for pixel art.
- Give everything a consistent palette (e.g. pick PICO-8/Endesga-32-style 16–32 colours; tint UI with the same).

**B6. Readability.**
- 1-px dark outline for workers and resource nodes (you have `Outline` variants in `Textures/Original/Outline`) so they pop off grass.
- Nodes show remaining yield by **sprite stages** (full → chipped → almost empty) instead of invisible numbers; hit-flash (white 0.08 s) on each strike.
- Worker state icons above the head (pickaxe, "Zz", "!", stone icon) – tiny 8x8 bubbles, bob 1 px.

---
## C. Game feel ("juice") – by system

### Camera
- Frame-rate independent smoothing: `pos = Vector3.SmoothDamp(...)` or `1 - Mathf.Exp(-k * dt)` instead of `Lerp(a,b,0.1f)`.
- **Zoom toward cursor**, integer zoom levels (B2), eased 0.12 s.
- Pan: WASD/arrows + edge scroll + MMB drag with slight inertia; **clamp to map bounds + margin**; Home key recentres.
- Tiny screen shake (0.05 units, 0.1 s) when a building is placed (not on mining).

### Placement (the most-used interaction)
- Ghost = building sprite at 60 % alpha, **tinted green/red via a separate overlay**, not by overwriting colour (fixes the alpha bug).
- Footprint overlay shows each cell individually: green free / red blocked; show worker-access side as a small arrow/dots.
- Ghost snaps with a 1-frame "pop" (scale 1.05→1) on cell change, subtle tick sound.
- On place: scale squash 0.8→1.1→1 (0.2 s, back-ease), dust puff particles (8 px, 6 particles), thud SFX, small shake; construction phase (scaffold sprite + progress bar) if you want workers to build it.
- Block clicks over UI; ESC/RMB cancel; **hold Shift to place repeatedly**; hotkeys 1–9; R to rotate if non-square.
- Invalid click: red flash on blocking cells + "Blocked" text + low "bonk" sound, never silent.

### Workers
- Move with **smooth continuous pathing** (don't re-path every 0.5 s; path once, repath only if blocked). Slight acceleration/deceleration at corners; 1–2 px random lateral offset per worker so they don't stack exactly.
- Walk animation bob; face direction (you flip via scale – keep, but flip the *child sprite*, not the root, so colliders/children don't mirror).
- Mining: wind-up → strike frame triggers **hit event** (animation event) → spark particles at node + node shake (±1 px, 0.1 s) + `tick` SFX with ±pitch randomisation. Resource is gained **on the strike frame**, not on a hidden timer.
- Carrying: show a small stone sprite on the worker (or stack count) – instantly readable.
- Depositing: a quick hop/throw into the storage, storage squash, "+5" text, coin-like icon flies to the HUD counter (see §E).
- Tired: yawn bubble, slower walk (−25 %) before the rest threshold, so the state change feels earned instead of sudden. Sleeping: sprite stays in colour; tent shows "Zzz" (you have it) + light dims inside.
- Idle: random look-around/stretch every 3–6 s; never frozen.

### Resource nodes
- Hover highlight (outline shader), tooltip "Stone 62/100".
- Depleting: crumble animation + debris particles + fade; ground beneath becomes "mined" tile variant instead of vanishing instantly.

### Floating text / numbers
- TMP world-space, font = pixel font (you already have `Font.png`), outline 1 px.
- Rise 0.6 units over 0.8 s with ease-out, scale pop 0.6→1.2→1, fade last 30 %, slight random x-offset. Colours: stone grey-blue, wood brown, bad red. Merge rapid "+1 +1 +1" into "+3".
- Pool 32; skip off-screen.

### Audio (currently none – biggest "feels dead" factor)
- Ambient loop (wind/birds), music with slow layers, SFX: pick strike (3 variants, pitch ±8 %), place, deny, deposit, button hover/click, sleep snore. Mix: music −18 dB, ambience −22 dB, SFX −8 dB. Duck music slightly on build mode.

### Time
- Game-speed buttons 0/1x/2x/4x (Time.timeScale) – this is the single best feature for a watch-your-colony game. Fix per-tick timers first (report §3 #10) or speed-up will break.

---
## D. UI/UX
- Replace the 3 "DebugButton Build" with a **bottom build bar**: icon, name, cost, hotkey, greyed when unaffordable; tooltip with description.
- **Top HUD:** Stone / Wood / Population (3/5 beds) with icon + number that **ticks up** (count-up tween) and pulses on change. 
- **Selection panel:** click a worker → name, STR/DEX/INT bars, current state, carried amount, fatigue meter; click a building → occupants, capacity, demolish button.
- **Notifications:** short toasts bottom-left ("No free bed", "Storage full") instead of Debug.Log.
- Scaling: Canvas Scaler = Scale With Screen Size 1920x1080, match 0.5; use TMP, pixel font, 9-slice panels in the same palette; UI sounds.
- Accessibility: don't rely on red/green only (add ✓/✗ icons), rebindable keys later.

---
## E. The missing game loop (design, not polish) **[DECIDE]**
Currently mining produces nothing. Minimum meaningful loop:
1. Stockpile (Stone, Wood) → HUD.
2. Buildings cost resources (StoneTent 10 wood, Storage 20 wood …). 
3. Beds = population cap; "Recruit worker" button costs food/stone or happens over time.
4. Wood chain: **Woodcutter** worker + `WoodTent` (stub exists) + tree nodes.
5. Goal/progression: milestones ("Store 100 stone → unlock Forge") with a progress bar; a soft end-goal (build the Town Hall).
6. Optional depth later: roads (lower MoveCost → workers prefer them → visible desire paths), upgrade tents, worker traits.

---
## F. Performance/robustness quick wins (ship with the above)
- Remove `Debug.Log` from per-tick code; wrap diagnostics in `[Conditional("UNITY_EDITOR")]`.
- Never use `.material` on shared sprites (use `MaterialPropertyBlock` or `.color`).
- Cache `Camera.main`; avoid `FindObjectOfType`/`FindGameObjectsWithTag` in loops.
- Remove `UnityEditor` usings from runtime code (build blocker).
- Move the unused ~650 textures and the `.7z` out of `Assets/` (keep a `_Source` folder outside the project); import only what is used → faster domain reload/import.
- `git init` + Unity `.gitignore` + commit before refactor.
- Consider asmdefs (`BuildSim.Core`, `BuildSim.Gameplay`, `BuildSim.UI`) for compile speed.

---
## G. Proposed milestones (we do these together, one at a time, playable after each)
| # | Milestone | Outcome you can see |
|---|---|---|
| M0 | git + hotfixes (usings, ghost alpha, occupancy bug, log spam) | Same game, builds, doesn't break |
| M1 | Art direction + pixel-perfect camera + ground + Y-sort + shadows | Looks cohesive immediately |
| M2 | `GridData` + `BuildingDef` + new PlacementController | Buildings place reliably, footprints, juicy placement |
| M3 | Stockpile + HUD + costs | Mining matters |
| M4 | Worker state machine + JobBoard + new pathing + mining strike events | Smooth, never-stuck workers |
| M5 | Audio + particles + floating text + camera polish + speed controls | The "feel" pass |
| M6 | Wood chain, recruiting, milestones | Real game loop |

## H. Decisions I need from you
1. Art direction: staying with the 16 px style already in use (recommended; much cheaper than first thought)?
2. OK to **scrap** the Tile-GameObject/Tilemap-polling system and rebuild on `GridData` (M2)? (Biggest change; recommended.)
3. Do you want workers autonomous only (current), or also direct-commandable (click to assign)?
4. Target platform/aspect (PC only? mobile later) – affects UI + input system choice.
5. Is the game's tone cosy/relaxed (my assumption: warm light, soft music) or gritty/survival?
