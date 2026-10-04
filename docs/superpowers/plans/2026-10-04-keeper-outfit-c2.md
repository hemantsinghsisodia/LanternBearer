# Visual Pass Phase C2 (Keeper Outfit and Detail Levels) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the long cloak with a hood plus a short torn shoulder cape, upgrade the visible body, and ship two detail levels: LOD0 on High and Ultra, LOD1 on Low and Medium. Everything else from Phase C stays working.

**Architecture:**
- **One FBX.** Both detail levels go in a single FBX: `_LOD0` and `_LOD1` skinned meshes on the unchanged `CharacterArmature`.
- **Unity.** A `LODGroup` on the keeper, forced per graphics preset through a new `GraphicsProfile.keeperLod` field that `KeeperQuality` applies with `LODGroup.ForceLOD`.
- **Unchanged.** The animator, lantern, events and materials are reused as they are.

**Tech Stack:**
- Blender 5.2: CLI scripts in `ArtSource/Keeper/`, or the Blender MCP.
- Unity 6000.6.3f1 URP, C#, NUnit, the Unity MCP.

**Spec:** `docs/superpowers/specs/2026-10-04-keeper-outfit-c2-design.md`. It amends `docs/superpowers/specs/2026-10-03-keeper-lantern-design.md`. Read both.

## Global Constraints
- **Branch:** `phase-c-keeper-lantern`. Never push.
- **Triangles:**
  - LOD0: about 20,000, cap 25,000
  - LOD1: about 7,000, cap 8,000
  - Both share `CharacterArmature`, with mesh names suffixed `_LOD0` / `_LOD1`.
- **Unchanged:**
  - the skeleton, all clips, `HandSocket`, `LanternHold`, `Lean`
  - `KeeperClipTests` must pass unmodified
  - the lantern ≤ 1.5 m from the keeper
  - Lantern values 0.35 / 5.5 / 3.5 / 14
- **Vertex colours** (exported with `colors_type='LINEAR'`):
  - R = sway weight (cape edge 1 → collar 0; 0 on everything else)
  - G = AO (hood inner shell 0; face ≈ 0.14; elsewhere 1)
  - B = worn edge
- **Materials:** reuse `Assets/Game/Art/Keeper/KeeperCloth|KeeperLeather|KeeperSkin|KeeperDark.mat`. The cloth is the hood plus the cape.
- **Shading:** smooth, with sharp edges only at real creases (boot soles, cuffs, belt, collar).
- **Export:** export from the existing `ArtSource/Keeper/Keeper.blend` via `export_keeper.py`. Never rebuild from the FBX.
- **Unity process** (rulings from Phase C):
  - Do NOT run Build All Levels except once, in Task 4.
  - Revert `Assets/Game/Scenes` only as the very last Unity-affecting step.
  - Keep the Unity window focused for play mode and tests.
  - If the MCP drops for more than about 2 minutes, or a git action is denied, stop and report.
- **Model split (AGENTS.md):** implementers and reviewers run on Sonnet.

## Review Focus
1. **Switching preset at runtime** (Settings → Graphics while playing). The keeper must swap LOD at once with no pop of a missing mesh, and the lantern, animator and sway must keep working. Test in Task 3.
2. **The face under the lantern on LOD1.** Low has no post-processing and fewer facets, and the face must still read as a dark hollow. Checked in Task 4's capture (low/front_idle).
3. **The cape against the raised lantern arm.** LanternHold lifts the right upper arm into the cape's side. The clipping check must include LanternHold and the Interact reach, with a target of ≤ 3 cm. Task 1.
4. **The edit-mode capture.** KeeperQuality doesn't run in edit mode, so `KeeperCloseupCapture` must force the LOD itself per preset, or both presets will show the same level. Task 3.
5. **Scenes holding stale renderer references.** Anything that cached the old cloak renderers (KeeperQuality's renderer list, material instancing for the rim keyword) must pick up both LOD sets. Task 3's PlayMode test checks the rim keyword on LOD0 and LOD1 renderers.

---

### Task 1: Blender model, both detail levels, renders (stop at the approval gate)

**Files:**
- Modify: `ArtSource/Keeper/Keeper.blend`
- Create or modify: `ArtSource/Keeper/build_outfit.py` (or a new `build_outfit_c2.py`; keep the old cloak builder only if still imported elsewhere), `ArtSource/Keeper/build_lods.py`, `ArtSource/Keeper/check_clipping.py`, `ArtSource/Keeper/render_keeper.py`
- Create: `docs/look/keeper/renders/c2/{lod0,lod1}_{front,side,threequarter,lantern}.png`, `docs/look/keeper/renders/c2/lod0_face.png`

**Interfaces:**
- **Produces, in the .blend:**
  - `_LOD0` meshes: `Body_LOD0` (or the existing body object names with the suffix), `Hood_LOD0`, `Cape_LOD0`, `Satchel_LOD0`
  - a matching `_LOD1` set
  - all skinned to `CharacterArmature`
  - material slots named `KeeperCloth`, `KeeperLeather`, `KeeperSkin`, plus the existing `Black` slot (which maps to `KeeperDark` in `TintKeeper`)
- **Removes:** the old `Cloak`, `Hood` and `Satchel` objects, and the old body objects (replaced by the suffixed sets)

- [ ] **Step 1: Hood.** Rebuild the hood deeper and rounder at LOD0 detail, with an inner shell at G = 0. Keep the face vertex G ≈ 0.14.
- [ ] **Step 2: Cape.** A short shoulder cape with a torn lower edge, ending around mid-back and the elbows, with nothing below the waist. Weight it to Abdomen, Torso, Chest, the Shoulders and the UpperArms. R runs 0 at the collar to 1 at the edge; B = 1 on the torn edge.
- [ ] **Step 3: Body upgrade.**
  - smooth shading, with sharp edges marked at boot soles, cuffs, belt and collar
  - one subdivision level on the tunic and trousers, plus fold shaping
  - reshaped hands (the fist around the ring must read as a hand)
  - refined boots
- [ ] **Step 4: Satchel.** At LOD0 detail.
- [ ] **Step 5: LOD1.** A reduced copy of every LOD0 mesh, using Decimate (planar or collapse) plus cleanup. Keep the silhouette, the vertex colours and the weights. Check the totals:
  - LOD0: 18,000–25,000
  - LOD1: 6,000–8,000

  Print the totals.
- [ ] **Step 6: Clipping.** Run `check_clipping.py` on the LOD0 cape against the body for Idle, Walk, Run, Roll, Interact, HitRecieve, Death and LanternHold. The target is ≤ 3 cm in Walk, Run, Interact and LanternHold. Report the per-clip worst values.
- [ ] **Step 7: Renders.** Render the PNGs listed above:
  - a dark night-blue background
  - a cool moon key
  - a warm point light at the lantern's `FlameAnchor` for the lantern shots
  - Lantern.blend appended and parented to `HandSocket`, with the keeper in the LanternHold pose for the lantern shots

  View every image.
- [ ] **Step 8:** Commit the .blend, the scripts and the renders: "art: C2 hood, short cape, body upgrade, two LODs (draft for approval)". Do NOT export the FBX. STOP and report. The controller shows the user the renders, and you are resumed for Task 2 only after approval.

### Task 2: Export and import both detail levels

**Files:**
- Modify: `ArtSource/Keeper/export_keeper.py` (exports both sets; `colors_type='LINEAR'`)
- Modify: `Assets/Game/Models/Keeper/Keeper.fbx` (+ .meta if the importer settings change)

**Interfaces:**
- **Consumes:** Task 1's `_LOD0` / `_LOD1` objects.
- **Produces, in Unity under the imported keeper:** `SkinnedMeshRenderer`s whose GameObject names end in `_LOD0` / `_LOD1`, all with `rootBone` under `CharacterArmature`.

- [ ] **Step 1:** Export from `Keeper.blend`, then refresh Unity.
- [ ] **Step 2:** Run `KeeperClipTests`. Expected: PASS, unmodified.
- [ ] **Step 3:** Verify in Unity, through the MCP:
  - the triangle totals per suffix: LOD0 ≤ 25,000, LOD1 ≤ 8,000
  - vertex colours present on `Cape_LOD0` (R 0..1) and on the head (G min ≈ 0.14)
  - every renderer's bones resolve to `CharacterArmature`
- [ ] **Step 4:** Commit: "feat: keeper FBX with C2 outfit at two detail levels".

### Task 3: LODGroup, per-preset detail, validation, capture

**Files:**
- Modify: `Assets/Game/Scripts/Graphics/GraphicsProfile.cs`: add `public int keeperLod;` under a `[Header("Keeper")]`
- Modify: `Assets/Game/Settings/Graphics/GraphicsProfile_{Low,Medium,High,Ultra}.asset`: keeperLod 1, 1, 0, 0. Also update `Assets/Game/Editor/GraphicsProfileSetup.cs` if it generates these values.
- Modify: `Assets/Game/Scripts/Graphics/KeeperQuality.cs`: apply `ForceLOD`; include both LOD sets in the renderer handling
- Modify: `Assets/Game/Editor/IslandBuilder.Keeper.cs`: build the `LODGroup` in `BuildImportedKeeper` / Refresh Keeper Look; make `TintKeeper` cover both sets
- Modify: `Assets/Game/Editor/SceneWiring.cs`: the LOD validation
- Modify: `Assets/Game/Editor/Look/KeeperCloseupCapture.cs`: force the LOD per preset
- Modify: `Assets/Game/Prefabs/Characters/Keeper.prefab`
- Test: `Assets/Game/Tests/EditMode/KeeperLodProfileTests.cs`; extend `Assets/Game/Tests/PlayMode/KeeperEventsTest.cs` (or add `KeeperLodTest.cs`)

**Interfaces:**
- `GraphicsProfile.keeperLod` (int; 0 = LOD0, 1 = LOD1).
- **The keeper root has a `LODGroup`:**
  - LOD 0 = every renderer named `*_LOD0`, screen-relative height 0.01
  - LOD 1 = every renderer named `*_LOD1`, height 0.001
  - `fadeMode` None
- **`KeeperQuality.Apply(profile)`:** calls `lodGroup.ForceLOD(profile.keeperLod)`. RestoreBaseline calls `ForceLOD(-1)`.
- **`KeeperCloseupCapture`:** forces LOD1 for the Low preset and LOD0 for Ultra. Read `keeperLod` from the profile asset.
- **Validation messages:**
  - "Keeper has no LODGroup"
  - "Keeper LODGroup must have 2 levels"
  - "Keeper LOD<n> renderer <name> is not skinned to CharacterArmature"
  - "Keeper LOD<n> has <t> triangles (cap <c>)", with caps 25,000 and 8,000

- [ ] **Step 1: Write the failing EditMode test.** `KeeperLodProfileTests.KeeperLodPerPreset`: load the four profile assets (path pattern as in `LookMappingTests`: `Assets/Game/Settings/Graphics/GraphicsProfile_<Name>.asset`). Assert:
  - Low.keeperLod == 1
  - Medium == 1
  - High == 0
  - Ultra == 0
- [ ] **Step 2: Write the failing PlayMode test.** `KeeperLodTest.PresetSwitchSwapsLod` (Review Focus 1 and 5). On Island1:
  1. `GraphicsQuality.Set(Ultra)`. After 2 frames, the `*_LOD0` renderers are visible and the `*_LOD1` renderers are not.
  2. `Set(Low)`. After 2 frames, the reverse.
  3. After each switch, the Animator is still playing, `LanternFlame` exists, and `_LKKeeperSway` is written.
  4. The rim keyword state matches the preset on BOTH sets' material instances.
- [ ] **Step 3:** Run both. Expected: FAIL.
- [ ] **Step 4:** Implement the field, the assets, the LODGroup build, `TintKeeper` for both sets, KeeperQuality's ForceLOD and renderer lists, the validation, and the capture LOD forcing. Regenerate the prefab via Refresh Keeper Look. Do NOT run Build All Levels.
- [ ] **Step 5:** Run EditMode (all) and PlayMode (all). Expected: PASS. Run Validate Scene Wiring on Island1. Expected: 0 problems.
- [ ] **Step 6:** Commit: "feat: keeper LODGroup forced per graphics preset; LOD validation".

### Task 4: After set, comparison page, performance, regression

**Files:**
- Modify: `docs/look/keeper/2026-10-03-after/**` (re-capture, overwriting)
- Modify: `docs/look/compare/phase-c.html`: show the C2 renders in place of the cloak renders, and the new after set
- Modify: `docs/look/perf/2026-10-03-phase-c.md`: add a C2 section

- [ ] **Step 1:** Run `KeeperCloseupCapture.Run("docs/look/keeper/2026-10-03-after")`. Expected: 18 JPGs, with low using LOD1 and ultra using LOD0. View at least:
  - ultra/front_idle and low/front_idle: dark face hollow (Review Focus 2)
  - ultra/threequarter_idle: the cape clear of the raised arm (Review Focus 3)
  - ultra/threequarter_gust
  - low/threequarter_lowfuel
- [ ] **Step 2:** Run Build All Levels once, then Validate Scene Wiring. Expected: 0 problems on all scenes, and the lantern ≤ 1.5 m.
- [ ] **Step 3:** Player build, then the perf probe on all 4 islands, Low and Ultra, using the method in `docs/look/perf/2026-10-03-phase-b.md`. Expected: Ultra average ≤ 12.5 ms. Record it against the Phase C numbers.
- [ ] **Step 4:** EditMode and PlayMode (all). Then a play-mode regression on Island1 with real movement through `PlayerController.ExternalMove`: the cape sways backward when running, the lantern hangs down, footstep dust appears, and a preset switch swaps the LOD.
- [ ] **Step 5:** Update `phase-c.html` and the perf doc. Revert the noise, with Scenes LAST. Commit: "docs: Phase C2 after set, comparison and perf".
- [ ] **Step 6:** Controller gate: show the user `phase-c.html` for approval.
