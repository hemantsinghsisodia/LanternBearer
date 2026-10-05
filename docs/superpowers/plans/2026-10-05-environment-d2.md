# Visual Pass Phase D2 (Props, Cliffs, Set Pieces) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Poly Haven photo props and the stretched terrain cliff faces with Quaternius Stylized Nature MegaKit props (with distinct natural colours per island) and rock-mesh cliff cladding. Gameplay is unchanged.

**Architecture:**
- **Importer.** A new `NatureKitImporter` (editor) mirrors `PolyHavenImporter`. It imports the used MegaKit FBX files, builds tinted materials and per-category prefabs (with the same collider rules), and rewrites the four biome assets to reference them.
- **Cladding.** A pure `CliffCladdingPlanner` (Logic) plans the cladding rocks from the heightmap, and the builder instantiates them.
- **Foliage shader.** A new `LanternKeeper/Foliage` shader handles leaves, plants and flowers.

**Tech Stack:** Unity 6000.6.3f1 URP (Deferred), hand-written HLSL, C#, NUnit, Unity MCP.

**Spec:** `docs/superpowers/specs/2026-10-05-environment-d2-design.md`. Also binding: the D1 spec amendment ("warm/cold applies to lights only").

## Global Constraints
- **Source:** `ArtSource/Nature/MegaKit/FBX/*.fbx` and `MegaKit/Textures/*.png` (CC0). These are git-ignored. Copy only the used files into `Assets/Game/Art/Environment/Nature/{Models,Textures,Materials,Prefabs}`. Add `CREDITS.txt` line: `Stylized Nature MegaKit by Quaternius (CC0) — https://quaternius.com`.
- **Colour:** materials keep natural, distinct hue families per island. No tint may pull every island toward one blue. The warm/cold rule applies to lights only.
- **Keeper scale** (the keeper is 1.76 m):

  | Prop | Size |
  |---|---|
  | Trees | 4–8 m |
  | Saplings | 2–3 m |
  | Bushes | about 1 m |
  | Flowers | about 0.3 m |
  | Medium rocks | 0.5–1.5 m |
  | Boulders | 2–4 m |
  | Cladding rocks | 2–6 m |

- **Gameplay unchanged:**
  - terrain heights, alphamaps, holes and detail positions are byte-identical (verify by hash against base, as D1 did)
  - biome entry counts and categories are kept
  - collider rules match `PolyHavenImporter.AddCollider`
  - cladding has no colliders
  - placement keeps props off trails, beacon rings, hidden paths and the shore
- **Name tokens:** the placement code selects by prefab-name substrings (`"fern"` in `IslandBuilder.cs:904-905`; `_hero` variants). New prefab names must keep those tokens. For example, use `Nature_Fern_1` for ferns, and add the `_hero` suffix to hero trees.
- **Performance:** every new material sets `enableInstancing = true`. Ultra average ≤ 12.5 ms at 1080p in a player build; p95 is tracked.
- **Unity process:**
  - Revert pure noise: `Materials/Generated/*.mat` (ShadeBody, Smoke), `ProjectSettings/*`, Look volumes, Shade.prefab, Keeper.controller.
  - NEVER revert `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` while the editor is open.
  - Never `git checkout` the whole Scenes folder. Commit scenes only when they carry real changes.
  - Keep Unity focused for play mode and tests.
  - Put screenshots in `.superpowers/sdd/2026-10-05-environment-d2/shots/`.
  - If the MCP drops for more than about 2 minutes, or a git action is denied, stop and report.
- **Model split (AGENTS.md):** implementers and reviewers run on Sonnet.

## Review Focus
1. **Paths blocked by a new prop.** A swapped tree or boulder with a larger footprint lands on a path or beacon approach. Expected: every beacon is reachable on foot. PlayMode reachability test in Task 6.
2. **Cladding floating or z-fighting at cliff tops and bottoms.** Rocks must be sunk into the slope, never hanging in the air or covering the walkable plateau top. Covered by the planner test (no placement cell above the edge's top height) in Task 5, plus screenshots.
3. **Colour collapse on any island.** Expected: four distinct foliage hues, checked by a hue-spread test in Task 2 and viewed in Task 3's and Task 7's screenshots.
4. **Low preset cost and readability.** Cladding and foliage culled by distance on Low; the Low average is no worse than D1's by more than 1 ms. Covered by Task 7's perf run.
5. **Leftover photo props in hero spots** (spawn, beacons, paths). Validation fails if a `Prefabs/PolyHaven/*` prefab sits within 6 m of spawn or a beacon. Covered by Task 6.

---

### Task 1: D2 "before" capture set
- [ ] **Step 1:** Run `LookBaselineCapture.Run("docs/look/baseline/2026-10-05-d2-before")` through the MCP, once. Expected: 67 files.
- [ ] **Step 2:** View Island 1 and Island 2 spawn and cliff shots on Ultra.
- [ ] **Step 3:** Commit `docs: Phase D2 before capture set`.

### Task 2: Nature kit import, foliage shader, materials, prefabs, palette fields

**Files:**
- Create: `Assets/Game/Editor/NatureKitImporter.cs`
- Create: `Assets/Game/Shaders/Foliage.shader`
- Modify: `Assets/Game/Scripts/Look/LookProfile.cs`
- Modify: the four `LookProfile_island*.asset` files
- Modify: `Assets/Game/Scripts/Logic/LookMapping.cs`
- Create: `Assets/Game/Art/Environment/Nature/**`
- Modify: `CREDITS.txt`
- Test: `Assets/Game/Tests/EditMode/NaturePaletteTests.cs`

**Interfaces:**
- **`LookProfile` fields:** `public Color foliage, bark, rockTint;`, with authored values set on all four profiles.

  | Island | foliage | bark | rockTint |
  |---|---|---|---|
  | island1 | `#3E7A3A` | `#5A4030` | `#8A8C8E` |
  | island2 | `#A88A3A` | `#6B5236` | `#9A8A74` |
  | island3 | `#5E7238` | `#4E4632` | `#6E7A68` |
  | island4 | `#7A4A6E` | `#4A3C3A` | `#5A5E6E` |

- **`LookMapping.HueSpread(Color[] colours) -> float`:** the maximum pairwise circular hue distance (0..0.5).
- **Menu items:** `Lantern Keeper → Nature → Import Kit` runs `NatureKitImporter.ImportKit()`, which copies the used FBX and PNG files, sets the importers, builds the materials, and builds the prefabs.
- **Prefabs** in `Art/Environment/Nature/Prefabs/<Category>/Nature_<Model>[_<island>].prefab`, with colliders via the same rules as `PolyHavenImporter.AddCollider` (reuse it: make it `internal static` and call it).
- **Category mapping:**

  | Category | MegaKit models |
  |---|---|
  | Tree | CommonTree_*, Pine_*, TwistedTree_*, DeadTree_* |
  | Sapling | the same models at 0.4–0.5 scale |
  | Deadwood | DeadTree_* scaled and laid down. Fallback: keep Poly Haven, allow-listed. |
  | Rock | Rock_Medium_*, Pebble_* |
  | Undergrowth | Bush_*, Fern_1, Plant_* |
  | Flower | Flower_*, Bush_Common_Flowers |
  | GroundCover | Clover_*, Grass_*, Mushroom_*, Petal_* |

- **Materials:**
  - **Foliage** (`LanternKeeper/Foliage`): albedo texture × the island `foliage` tint, alpha cutout 0.5, wrap lighting `0.5 + 0.5 × NdotL`, wind sway from the existing wind globals (vertex, weighted by height), instancing.
  - **Low:** keyword `_LK_FOLIAGE_LOW` disables the sway; reuse the GraphicsProfile keyword pattern from GrassBend and Water.
  - **Bark and rock:** URP/Lit or KeeperLit with the pack textures × the `bark` / `rockTint` tint, instancing.
  - One material set per island: `Nature_<kind>_<levelId>.mat`.

- [ ] **Step 1: Write the failing test `NaturePaletteTests.FoliageHuesDistinct`.**
  - `LookMapping.HueSpread` over the four profiles' `foliage` is ≥ 0.25.
  - Each pair differs by ≥ 0.05, except island1 and island3, which may be closer (≥ 0.03).
  - `bark` and `rockTint` are set (alpha 1) on all four.
- [ ] **Step 2:** Run the EditMode tests. Expected: FAIL.
- [ ] **Step 3:** Implement the fields, the values, `HueSpread`, the shader, the importer and `CREDITS.txt`. Run Import Kit.
- [ ] **Step 4:** Run EditMode. Expected: PASS. Check that every prefab has the right scale (tree bounds 4–8 m tall and so on, against the Global Constraints table) by logging the bounds from the importer.
- [ ] **Step 5:** Commit `feat: import Stylized Nature MegaKit with per-island foliage, bark and rock materials`.

### Task 3: Convert Island 1 (PineForest), then the user's direction gate

**Files:**
- Modify: `NatureKitImporter.cs`, adding `RebuildBiome(string biomeName, string levelId)`
- Modify: `Levels/Biomes/PineForest.asset`
- Modify: `IslandBuilder.Terrain.cs` (`AddBiomeDetails` and `AddPackGrassCarpet`): swap the Poly Haven ground-cover and flower detail prototypes for nature-kit ones on biomes that have been rebuilt

**Interfaces:** `NatureKitImporter.RebuildBiome(string biomeName, string levelId)` replaces each entry's prefab with a nature prefab of the same category, keeping `count`, `scaleRange` and the other placement fields, with scaleRange adjusted for the keeper-scale table.

- [ ] **Step 1:** Rebuild PineForest, then Build Island for island1.
- [ ] **Step 2: Validate.**
  - Validate Scene Wiring: 0 problems.
  - Terrain data hash is identical to base.
  - Every beacon is reachable on foot: walk via `PlayerController.ExternalMove`, or a navmesh-free line-of-path check along the trails.
- [ ] **Step 3: Screenshots** on Ultra and Low: spawn, a cliff, a beacon, and a close-up of the trees and bushes. Look at them.
- [ ] **Step 4:** Commit `feat: Island 1 props converted to nature kit`.
- [ ] **Step 5: STOP. User gate:** the controller shows the user the Island 1 screenshots, and a quick build if wanted. Continue only after approval of the direction and colour.

### Task 4: Convert Islands 2–4, plus marsh reeds and heath

**Files:**
- Modify: `Levels/Biomes/{Namaqualand,Marsh,Heath}.asset`
- Create: `Assets/Game/Editor/ReedTuftBuilder.cs`, which generates a reed clump mesh and prefab: 9–15 blades 1.0–1.6 m tall, slightly bent, with a seed head, using the Foliage shader with the island3 tones (olive `#5E7238` to straw `#B49A5A` along the height)
- Modify: `IslandBuilder.Terrain.cs`: the marsh reed detail uses `ReedTuftBuilder`'s prefab where the Poly Haven reed billboard was used

**Notes:**
- Heath uses Bush_Common and Plant_* with island4's foliage tint.
- Namaqualand uses TwistedTree, DeadTree and the golden plants.
- Anything with no match keeps its Poly Haven prefab and goes in `NatureKitImporter.AllowList` (a static string array), with the reason in a comment.

- [ ] **Step 1:** Rebuild the three biomes, then Build All Levels and Validate Scene Wiring: 0 problems. Check that terrain hashes are identical.
- [ ] **Step 2:** Take screenshots per island (Ultra and Low): spawn, beacon and a close-up. Look at them, checking hue distinctness and scale against the keeper.
- [ ] **Step 3:** Commit `feat: Islands 2-4 props converted; generated marsh reeds; heath bushes`.

### Task 5: Cliff cladding

**Files:**
- Create: `Assets/Game/Scripts/Logic/CliffCladdingPlanner.cs`
- Modify: `IslandBuilder.cs` (`PlaceCliff`, around line 1078: replace the SetPiece placement with cladding) or a new `IslandBuilder.Cliffs.cs`
- Test: `Assets/Game/Tests/EditMode/CliffCladdingPlannerTests.cs`

**Interfaces:**
- `public struct CladdingRock { public Vector3 position; public float yaw, tilt, scale; public int variant; }`
- `public static List<CladdingRock> CliffCladdingPlanner.Plan(float[,] heights, float worldSize, float heightScale, Vector2 originXZ, Func<Vector2,bool> excluded, int seed)`
  - steep cells: slope > 45°
  - rocks every 2–3 m along each steep edge
  - scale 2–6 m
  - position sunk 30–50% into the slope, toward the downhill side
  - a second staggered row when the face is taller than 3 m
  - variant 0..2 (Rock_Medium_1..3)
  - deterministic for a seed
- **The builder:** instantiates the island-tinted Rock_Medium prefab variants with their **colliders removed**, under a `CliffCladding` parent. Static batching is off; instancing is on. A per-preset cull distance comes from the existing prop distance settings (`GraphicsProfile`).

- [ ] **Step 1: Write the failing tests** on a synthetic heightmap: a 64×64 flat plain with a 6 m-high square plateau in the middle.
  - `PlanCoversEdge`: every steep edge cell has a rock within 3 m.
  - `PlanIsDeterministic`: the same seed gives the same list.
  - `PlanRespectsExclusion`: `excluded` returning true for x < 0 means no rocks at x < 0.
  - `PlanNeverAboveTop`: no rock position.y exceeds the plateau top minus 0.3 m (Review Focus 2).
  - `PlanScalesInRange`: all scales are within 2–6.
- [ ] **Step 2:** Run them. Expected: FAIL.
- [ ] **Step 3:** Implement the planner and the builder integration. Exclusions: trails (`TrailMask` > 0), beacon tops (within 8 m of a beacon), shore (height below sea level + 0.5 m).
- [ ] **Step 4:** Run the tests. Expected: PASS. Then Build All Levels and Validate Scene Wiring: 0 problems. Terrain hashes are identical.
- [ ] **Step 5:** Take cliff screenshots on every island with cliffs (Ultra and Low). They should show natural rock walls, no stretched faces visible, and no floating rocks. Look at them.
- [ ] **Step 6:** Commit `feat: rock-mesh cliff cladding replaces stretched faces and photo set pieces`.

### Task 6: Validation and reachability test

**Files:**
- Modify: `Assets/Game/Editor/SceneWiring.cs`
- Test: `Assets/Game/Tests/PlayMode/BeaconReachabilityTest.cs`

**Validation messages:**
- `"Photo prop <name> outside allow-list"`: any renderer under the props parent whose prefab source is under `Prefabs/PolyHaven/` and isn't in `NatureKitImporter.AllowList`.
- `"Photo prop <name> within 6 m of spawn/beacon"`: the same, for allow-listed props within 6 m of spawn or a beacon (Review Focus 5).
- `"Tree/rock <name> missing collider"`: a Tree, Sapling, Rock or Deadwood instance with no Collider.
- `"Cliff face at <pos> has no cladding"`: any steep run longer than 4 m and taller than 2 m without a cladding rock within 3 m.

**Test:** `BeaconReachabilityTest.EveryBeaconReachable` (PlayMode, all 4 islands). From spawn, follow each trail polyline (from the level data) with `PlayerController.ExternalMove` and assert the keeper arrives within 2 m of each beacon within a time limit. A failure names the island and beacon (Review Focus 1).

- [ ] **Step 1:** Write the test and the validation. Run them and fix any real blockers found by moving or removing the offending prop placement. Never change the terrain.
- [ ] **Step 2:** EditMode, PlayMode, and Validate Scene Wiring on all scenes: 0 problems.
- [ ] **Step 3:** Commit `feat: D2 validation (photo props, colliders, cladding) and beacon reachability test`.

### Task 7: After set, comparison page, performance, build

- [ ] **Step 1:** Run `LookBaselineCapture.Run("docs/look/baseline/2026-10-05-d2-after")`. View the spawn, cliff and beacon shots for all islands on Low and Ultra.
- [ ] **Step 2:** Write `docs/look/compare/phase-d2.html` (the phase-b.html format, before vs after).
- [ ] **Step 3:** Player build, then the perf probe on all 4 islands, Low and Ultra (method in `docs/look/perf/2026-10-03-phase-b.md`). Write `docs/look/perf/2026-10-05-phase-d2.md` against D1. Expected: Ultra ≤ 12.5 ms, and Low no more than 1 ms worse than D1 (Review Focus 4). If it fails, stop and report.
- [ ] **Step 4:** Make a non-development build at `Builds/PhaseD2_Release/LanternKeeper.exe`. Launch it for about 10 s and check its log for `"shader is null"` or exceptions. Expected: none.
- [ ] **Step 5:** Commit `docs: Phase D2 after set, comparison and perf`.
- [ ] **Step 6: User gate:** the user plays the build and approves before merge.
