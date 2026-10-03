# Visual Pass Phase C (Keeper and Lantern) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the keeper its final look and motion. That means:
- true scale, with every ×93 workaround removed
- the hooded-wanderer outfit
- an iron storm lantern on a hand socket, with a flame that follows fuel
- animator layers driven by game events
- cloak sway, a rim on the lantern iron, and footstep dust at the feet

Gameplay stays unchanged.

**Architecture:**
- **Blender.** Art is authored in `ArtSource/Keeper/Keeper.blend` and `ArtSource/Lantern/Lantern.blend` and exported as FBX. That covers the true-scale skeleton, the `HandSocket`, the two poses, the outfit and the lantern.
- **Builder.** `IslandBuilder.Keeper` places the keeper, parents the lantern to the socket with no offsets, and builds the extra animator layers.
- **Pure functions.** The flame and lean mappings are pure Logic functions covered by EditMode tests.
- **Events.** `KeeperAnimator` subscribes to game events and only sets animator parameters and layer weights.

**Tech Stack:**
- Unity 6000.6.3f1, URP.
- Hand-written HLSL (`KeeperLit`, `Flame`).
- C#, Unity Test Framework.
- Unity MCP.
- Blender 5.2: through the Blender MCP when Blender is open, otherwise `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -P script.py`.

**Spec:** `docs/superpowers/specs/2026-10-03-keeper-lantern-design.md`. Parent spec: `docs/superpowers/specs/2026-10-03-art-direction-design.md`. Read both.

## Global Constraints
- **Code style:**
  - `namespace LanternKeeper`, Allman braces, explicit `if`/`return`.
  - `[SerializeField]` private fields.
  - Match the surrounding comment density.
- **Shaders:** hand-written URP HLSL in `Assets/Game/Shaders/`. No Shader Graph. No new packages.
- **Gameplay unchanged:**
  - `Lantern` `minIntensity` 0.35, `maxIntensity` 5.5, `minRange` 3.5, `maxRange` 14.
  - `Radius`, `BaseIntensity` and `FuelNormalized` keep their meaning.
  - `PlayerController` movement and `CharacterController` collision values are unchanged.
  - The lantern stays ≤ 1.5 m from the keeper.
  - Terrain, paths and props are untouched.
- **Colours:**
  - lantern amber `#FFB15C`
  - glow core `#FFD38A`
  - flame near empty: red-orange `#E8502A`
  - No other warm light is added.
- **Budgets:**
  - Keeper plus outfit: under 6,000 triangles.
  - Lantern: under 800 triangles, about 0.28 m tall.
  - Performance: Ultra average ≤ 12.5 ms at 1080p in a player build on the RTX 4050 Laptop GPU (p95 tracked). Low reaches 60 fps on the iGPU (a user step).
- **Scale:** after Task 4, every keeper transform has a scale of 1 (±0.001). No code may divide or multiply by `lossyScale` to compensate for the keeper's scale.
- **Assets:**
  - Blender sources live under `ArtSource/` (LFS via `*.blend`). Do not commit `.blend1` files: add `*.blend1` to `.gitignore` in Task 4.
  - FBX files are LFS.
- **Unity process:**
  - Never use batchmode while the editor is open; drive Unity through the MCP.
  - After every build or play session, revert the noise before committing: font SDFs, generated `.mat` files, ProjectSettings, terrain and biome assets. `Keeper.controller` counts as noise **except** in Task 9, which owns it.
- **Model split (AGENTS.md):** implementer and reviewer subagents run on Claude Sonnet 5.5, high effort.

## Review Focus
1. **Pause and slow motion.** `Time.timeScale` is 0 while paused. Animation layers and flame updates must freeze with the game and resume with no pop. The lean must not keep easing while paused.
2. **Overlapping events.** A Shade steal during the beacon gesture, or death during a stagger. Death must always win, and the LanternArm weight must never stay stuck at 0 after an interrupted gesture.
3. **Restarting a level after a loss** (HUD restart reloads the scene). Static events (`Shade.Stole`, `Beacon.Lit`) must not keep calling destroyed keepers. Check unsubscribing in `OnDisable`.
4. **Rescue while low on fuel.** The flame and halo must not reset to full after the rescue teleport. The flame always reads `FuelNormalized`.
5. **No `Wind` in the scene** (Islands 1–3 have no `Wind` component). The lean weight must be 0 and the sway must work without a null reference.

Each item has a test in its owning task: 1 and 2 in Task 9, 3 in Task 9's PlayMode test, 4 in Task 8, 5 in Tasks 2 and 9.

---

### Task 1: Keeper close-up capture tool and "before" set

**Files:**
- Create: `Assets/Game/Editor/Look/KeeperCloseupCapture.cs`
- Create (output): `docs/look/keeper/2026-10-03-before/<preset>/<shot>.jpg`

**Interfaces:**
- Produces:
  - menu item **Lantern Keeper → Look → Capture Keeper Close-ups**
  - `public static void KeeperCloseupCapture.Run(string outputDir)`, which Task 11 reuses for the after set

**What it does:**
- **Scene:** runs in **edit mode** on a temporary studio scene. It saves and later reopens the user's open scene.
- **Contents:**
  - a ground plane
  - a `Keeper.prefab` instance
  - a `LookApplier` using the island1 `LookProfile`, with `Apply()` called directly
  - a camera using the PC renderer, so the moon rim is included
- **Shots:** `front`, `threequarter`, `back` at 2.2 m distance, with the camera aimed at chest height.
- **States:** `idle`, `gust` and `lowfuel`.
  - **Pose:** sample the state with `animator.Rebind()` + `animator.Update(t)`.
  - **`gust`:** sets the `Lean` layer weight to 1 when that layer exists (Task 9 on). Before that it renders the same as idle; name the files by state anyway.
  - **`lowfuel`:** sets the lantern light to its fuel-0.15 intensity and range, using `Lantern`'s mapping. From Task 8 on, it also calls `LanternFlame.ApplyFuel(0.15f)` when that component is present (found by type name, so this task compiles before Task 8).
- **Presets:** Low and Ultra, set via `GraphicsQuality.Set`.
- **Output:** 1920×1080 JPEG, quality 92.
- **Cleanup:** restores the preset and the open scene, and leaves no asset changes.

- [ ] **Step 1:** Implement the tool, following the save, restore and JPEG conventions of `LookBaselineCapture`.
- [ ] **Step 2:** Run it through the MCP: `KeeperCloseupCapture.Run("docs/look/keeper/2026-10-03-before")`.
  - Expected: 18 JPGs (3 shots × 3 states × 2 presets).
  - `git status` shows only those files plus the new script and its .meta.
- [ ] **Step 3:** Open two of the images (the Ultra three-quarter idle, and the Low front) and confirm the keeper is framed and lit.
- [ ] **Step 4:** Commit: `feat: keeper close-up capture tool and Phase C before set`.

### Task 2: Pure flame and lean mappings

**Files:**
- Create: `Assets/Game/Scripts/Logic/LanternFlameMapping.cs`
- Create: `Assets/Game/Scripts/Logic/KeeperLean.cs`
- Test: `Assets/Game/Tests/EditMode/LanternFlameMappingTests.cs`
- Test: `Assets/Game/Tests/EditMode/KeeperLeanTests.cs`

**Interfaces (all in `namespace LanternKeeper`):**
- `public static class LanternFlameMapping`:
  - `public const float MinHeight = 0.45f;`
  - `public static float Height(float fuel01)`: returns `lerp(MinHeight, 1, clamp01(fuel01))`.
  - `public static Color FlameColour(float fuel01)`: `#E8502A` at 0, `#FFD38A` at 1, linear in between, with alpha 1.
  - `public static float HaloScale(float fuel01)`: returns `lerp(0.55, 1, clamp01(fuel01))`.
- `public static class KeeperLean`:
  - `public static float TargetWeight(WindPhase phase, float strength01, bool sheltered, bool hasWind)`: returns `strength01` (clamped) only when `hasWind && !sheltered && phase == WindPhase.Gust`, otherwise 0.
  - `public static float Step(float current, float target, float dt, float seconds = 0.3f)`: eases at a linear rate of `1/seconds` per second, and returns `current` when `dt <= 0`.

- [ ] **Step 1: Write the failing tests.**
  - `Height(0) == 0.45f`, `Height(1) == 1f`, `Height(-1) == 0.45f`, `Height(2) == 1f`.
  - `Height` is monotonic over 11 samples.
  - `FlameColour(1)` ≈ `#FFD38A` and `FlameColour(0)` ≈ `#E8502A` (within 1/255 per channel).
  - `HaloScale(0) == 0.55f`.
  - `TargetWeight(Gust, 0.8f, false, true) == 0.8f`.
  - `TargetWeight(Gust, 0.8f, true, true) == 0`.
  - `TargetWeight(Warning, 1, false, true) == 0`.
  - `TargetWeight(Gust, 1, false, false) == 0` (Review Focus 5).
  - `Step(0, 1, 0.15f) == 0.5f` (within 1e-4).
  - `Step(0.4f, 1, 0f) == 0.4f` (paused; Review Focus 1).
  - `Step(0.9f, 1, 1f) == 1f`.
- [ ] **Step 2:** Run the EditMode tests through the MCP (`run_tests`, EditMode). Expected: the new tests fail to compile or fail.
- [ ] **Step 3:** Implement both classes.
- [ ] **Step 4:** Run the EditMode tests. Expected: all pass (the existing tests plus the new ones).
- [ ] **Step 5:** Commit: `feat: lantern flame and keeper lean mappings`.

### Task 3: Clip reference data and re-export guard test

**Files:**
- Create: `Assets/Game/Editor/KeeperClipReference.cs`
- Create (output): `Assets/Game/Tests/EditMode/Data/KeeperClipReference.json`
- Test: `Assets/Game/Tests/EditMode/KeeperClipTests.cs`

**Interfaces:**
- Produces: menu item **Lantern Keeper → Keeper → Record Clip Reference**. It writes JSON containing:
  - for each clip in `{Idle, Walk, Run, Roll, Interact, HitRecieve, HitRecieve_2, Death}` plus the jump clips `EnsureKeeperController` uses: `name`, `length`
  - `samples`: at t = 0, 0.25, 0.5, 0.75 and 1 × length, the **local rotations** (quaternion xyzw) of these bones:
    - `Hips`, `Spine`, `Chest` (or the rig's equivalent spine bones; record the names used)
    - `Head`
    - `UpperArm.R`, `LowerArm.R`, `Wrist.R`
    - `UpperLeg.L`, `LowerLeg.L`
- **Sampling:** use `AnimationClip.SampleAnimation` on a temporary instance of `Keeper.fbx`.
- **Rotations only:** local rotations are unchanged when the scale is applied, while positions are not.

- [ ] **Step 1:** Implement the recorder and run it on the **current** FBX. Commit the JSON.
- [ ] **Step 2: Write `KeeperClipTests.ClipsMatchReference`.**
  - Load the JSON and the FBX clips (`AssetDatabase`).
  - For each recorded clip, assert that it exists and that `|length - ref| < 1e-3`.
  - Assert that every sampled bone rotation is within 2° (`Quaternion.Angle`).
  - Bones are looked up by name. A missing bone fails with its name.
- [ ] **Step 3:** Run the EditMode tests. Expected: pass against the unchanged FBX.
- [ ] **Step 4:** Commit: `test: keeper clip reference guard for the re-export`.

### Task 4: True-scale re-export, HandSocket, poses, and removing the workarounds

**Files:**
- Create: `ArtSource/Keeper/Keeper.blend`
- Create: `ArtSource/Keeper/export_keeper.py`: the import, scale apply and export script, kept for repeatability
- Modify: `Assets/Game/Models/Keeper/Keeper.fbx`
- Modify: `Assets/Game/Editor/IslandBuilder.Keeper.cs`: `AttachLantern` (~701), `EnsureChestFill` (~839), `PrepareKeeperImport` clip settings
- Modify: `Assets/Game/Scripts/LanternHalo.cs:58`
- Modify: `Assets/Game/Scripts/KeeperAnimator.cs`: the procedural fallback constants
- Modify: `Assets/Game/Scripts/Graphics/KeeperQuality.cs`, if it holds any scale compensation
- Modify: `Assets/Game/Editor/SceneWiring.cs`: the validation additions
- Modify: `.gitignore`: add `*.blend1`

**Interfaces:**
- **Produces, in the FBX:**
  - armature scale 1
  - an empty `HandSocket` parented to `Wrist.R`, at the closed-fist grip. Its +Y points up the fist, so the lantern hangs along −Y.
  - two new actions:
    - `LanternHold`: two identical frames, right arm held out in front and slightly to the side, fist about 0.35 m in front of the hip and at waist height
    - `Lean`: frame 0 is the rest pose, frames 1–2 are the spine, neck and head bent about 18° forward. It's imported as an additive clip with reference frame 0, clip range 1–2.
- **Produces, in `PrepareKeeperImport`:**
  - both clips loop
  - `Lean` has `hasAdditiveReferencePose = true` and `additiveReferencePoseFrame = 0`
- **Produces, in SceneWiring:** validation fails with `"Keeper transform <path> has scale <s>"` for any transform under the gameplay keeper whose `localScale` is not 1 ±0.001. Lantern, VFX and light children are skipped by name: `LanternPivot` and below, `Dust`, `LanternHalo`.

**Steps:**
- [ ] **Step 1: Blender.**
  1. Import `Keeper.fbx` with animations.
  2. Apply scale on the armature and mesh so `CharacterArmature` reads scale 1 and the keeper stays about 1.8 m tall. Check the existing height in Unity first and keep it.
  3. Add `HandSocket` and author the `LanternHold` and `Lean` actions.
  4. Save `Keeper.blend`.
  5. Export the FBX using the settings recorded in `export_keeper.py`:
     - scale 1, "FBX Units Scale", Y up, −Z forward
     - all actions baked
     - add leaf bones off
- [ ] **Step 2: Import and run the guard test.** Refresh Unity and run `KeeperClipTests`. Expected: pass. If it fails, fix the export rather than loosening the 2° tolerance.
- [ ] **Step 3: Remove the workarounds.**
  - `AttachLantern`: parent `LanternPivot` to `HandSocket` (fall back to `Wrist.R` only when the socket is missing), at local zero, rotation identity, scale 1. Delete the `lossy` division and the ×93 comment.
  - `LanternHalo`: use `size` directly.
  - Remove any remaining ×93 compensation in the `KeeperAnimator` fallback and in chest-fill placement.
  - Find every `lossyScale` use with `grep -rn lossyScale Assets/Game`. None may remain for keeper scale.
- [ ] **Step 4:** Add the scale check to Validate Scene Wiring.
- [ ] **Step 5: Rebuild and validate.** Run **Build All Levels**, then **Validate Scene Wiring**. Expected:
  - 0 problems
  - the lantern ≤ 1.5 m from the player, with its distance printed in the validation log
- [ ] **Step 6: Play-mode check on island1.**
  - Walk, run and jump; the keeper's size and the camera framing match the before set.
  - The lantern is in the hand.
  - The reveal radius is unchanged (`Lantern.Radius` = 14 at full fuel).
- [ ] **Step 7:** Run the EditMode tests and the PlayMode smoke test. Expected: all pass.
- [ ] **Step 8:** Revert the noise and commit: `feat: true-scale keeper with hand socket and hold/lean poses; remove x93 workarounds`.

### Task 5: Storm lantern model

**Files:**
- Create: `ArtSource/Lantern/Lantern.blend`
- Create: `Assets/Game/Art/Lantern/Lantern.fbx`

**Interfaces:**
- **Produces:** a Lantern FBX with origin at the inside of the ring handle (the pendulum pivot), hanging along −Y.
- **Separate objects:**
  - `Iron`: the frame, cap and ring
  - `Glass`: four panes
  - `FlameAnchor`: an empty at the wick, about 0.11 m below the ring's bottom
- **Material slots:** `LanternIron` and `LanternGlass`.

**Shape:** a square forged-iron frame, four glass panes, a peaked roof cap and a ring handle. About 0.28 m tall overall, under 800 triangles.

- [ ] **Step 1:** Model it in Blender and export at scale 1, Y up, −Z forward.
- [ ] **Step 2:** Render front and three-quarter thumbnails to `docs/look/keeper/renders/lantern_*.png`.
- [ ] **Step 3:** Import into Unity. Check the triangle count and that the `FlameAnchor` transform is present.
- [ ] **Step 4:** Commit: `feat: iron storm lantern model`.

### Task 6: Hooded-wanderer outfit, and the render approval checkpoint

**Files:**
- Modify: `ArtSource/Keeper/Keeper.blend`
- Modify: `Assets/Game/Models/Keeper/Keeper.fbx`
- Create: `docs/look/keeper/renders/keeper_{front,side,threequarter,lantern}.png`

**Interfaces:**
- **Produces, in the FBX:**
  - new meshes `Hood`, `Cloak` and `Satchel`, skinned to the existing bones
  - material slots `KeeperCloth`, `KeeperLeather` and the existing skin slot
  - vertex colours on the cloth meshes:
    - R = sway weight: cloak hem 1 → shoulders 0; hood and satchel 0
    - G = AO: 1 normally, 0 inside the hood hollow
    - B = worn edge: 1 on the hem and hood rim, 0 elsewhere
  - body mesh faces hidden under the outfit are removed, so the triangle count stays under 6,000

**The outfit:**
- **Hood:** follows `Head`. Its face opening is a dark hollow (inner faces using the cloth slot, darkened via vertex colour G = 0, which `KeeperLit` will use as an AO tint).
- **Cloak:**
  - long, with a ragged, torn hem
  - weighted to the spine and shoulders
  - the lower strips blend toward the legs, so walking moves it with no clipping in Walk, Run or Roll at the sampled frames
- **Satchel:** on the left hip, with a strap from the right shoulder.

**Steps:**
- [ ] **Step 1:** Model, skin and paint the vertex colours. Check Idle, Walk, Run, Roll, Interact, HitRecieve and Death in Blender for any clipping.
- [ ] **Step 2:** Render the four PNGs. The `lantern` render has `Lantern.fbx` parented to `HandSocket` with the LanternHold pose.
- [ ] **Step 3: User approval gate.** The controller shows the user the renders, including the Task 5 lantern renders, and waits for approval. Iterate on the requested changes before continuing.
- [ ] **Step 4:** Export the FBX (same script as Task 4). Run `KeeperClipTests` (expected: pass) and the triangle count check (expected: under 6,000).
- [ ] **Step 5:** Commit: `feat: hooded wanderer outfit (hood, torn cloak, satchel)`.

### Task 7: KeeperLit cloak sway, hood AO and outfit materials

**Files:**
- Modify: `Assets/Game/Shaders/KeeperLit.shader`
- Create: `Assets/Game/Art/Keeper/KeeperCloth.mat`, `KeeperLeather.mat`, `KeeperSkin.mat`
- Create: `Assets/Game/Art/Lantern/LanternIron.mat`, `LanternGlass.mat`
- Modify: `Assets/Game/Editor/IslandBuilder.Keeper.cs`: `TintKeeper` assigns the authored materials by slot name instead of generating tints
- Modify: `Assets/Game/Scripts/KeeperAnimator.cs`: writes the sway global

**Interfaces:**
- **Shader properties:**
  - `_SwayStrength` (Range 0–0.3, default 0): metres of displacement at weight 1. Cloth = 0.12; every other material = 0.
  - `_VertexAO` (Range 0–1, default 0): albedo is multiplied by `lerp(1, lerp(0.15, 1, vertexColor.g), _VertexAO)`. Cloth = 1.
  - `_EdgeLighten` (Range 0–0.5, default 0): albedo is multiplied by `1 + _EdgeLighten * vertexColor.b` (worn edges). Cloth = 0.25.
- **Shader global** `_LKKeeperSway` (float4):
  - x = speed01
  - y = wind01
  - zw = world wind direction xz
- **Sway displacement:**
  - backward along the object's −Z (the keeper faces its movement) scaled by x, plus along the world wind direction scaled by y
  - plus a `sin(_Time.y * 3 + worldPos.y * 4) * 0.25` flutter
  - all multiplied by `vertexColor.r * _SwayStrength`
  - the same term goes in the ShadowCaster pass
- **`KeeperAnimator.LateUpdate`** sets `_LKKeeperSway`:
  - speed01 = planar speed / 9 (the Run threshold)
  - wind01 = the current lean weight from Task 9, or 0 until then
  - direction from `Wind.Instance` when present, otherwise `(0, 1)`

**Colours:**
- `KeeperCloth`:
  - base `#2E3A4F` (deep desaturated slate-blue)
  - `_VertexAO` 1, `_EdgeLighten` 0.25
  - `_RimStrength` 0.7
- `KeeperLeather`: `#2B2119`, smoothness 0.3.
- `KeeperSkin`: `#A5806A`, desaturated 0.2.
- `LanternIron`: `KeeperLit`, `#2A2A2E`, metallic 0.6, smoothness 0.35, `_RimStrength` 1.0.
- `LanternGlass`: `LanternKeeper/AdditiveUnlit`, `#FFD38A` at alpha 0.35. Task 8 drives its intensity.

**Steps:**
- [ ] **Step 1:** Extend `KeeperLit` with the properties and the global. The rim keyword is unchanged, and `KeeperQuality` keeps toggling it.
- [ ] **Step 2:** Create the materials and wire `TintKeeper`.
- [ ] **Step 3:** Run **Refresh Keeper Look**, then **Build All Levels** and **Validate Scene Wiring**. Expected: 0 problems. If the Animator ends up null after the refresh, reimport the keeper prefab (the known tooling flake).
- [ ] **Step 4:** Play-mode check on island1: the cloak sways when running and is still when idle, with no shader errors in the console.
- [ ] **Step 5:** Revert the noise and commit: `feat: KeeperLit cloak sway and hood AO; authored keeper and lantern materials`.

### Task 8: Lantern on the socket: flame, glass glow, light at the flame

**Files:**
- Create: `Assets/Game/Scripts/LanternFlame.cs`
- Modify: `Assets/Game/Editor/IslandBuilder.Keeper.cs`: `AttachLantern`, `PlaceLanternLight`
- Modify: `Assets/Game/Scripts/LanternHalo.cs`: scale by fuel
- Modify: `Assets/Game/Scripts/LanternSway.cs`: add a kick and a clamp
- Modify: `Assets/Game/Editor/SceneWiring.cs`
- Test: `Assets/Game/Tests/EditMode/LanternFlameTests.cs`

**Interfaces:**
- **Consumes:** `LanternFlameMapping` (Task 2), the `Lantern.fbx` objects (Task 5), `HandSocket` (Task 4).
- **`public class LanternFlame : MonoBehaviour`:**
  - Fields: `[SerializeField] Lantern lantern`, `Transform flame`, `Renderer flameRenderer`, `Renderer glassRenderer`, `LanternFlicker flicker` (optional).
  - `public void ApplyFuel(float fuel01)`:
    - sets the flame's localScale to `baseScale * Height`
    - sets the flame colour via a `MaterialPropertyBlock` `_Color`
    - sets the glass intensity via `_Color` alpha = `0.35 * fuel01 * flickerFactor`
  - `LateUpdate`: calls `ApplyFuel(lantern.FuelNormalized)`. While `lantern.DeathLightActive`, the height and glass are also multiplied by `clamp01(lantern.DeathLight / 0.35)`, so the flame gutters with the existing death light curve.
- **`LanternSway`:**
  - `[SerializeField] float maxDegrees = 16f` is kept.
  - New: `public void Kick(float degrees)`, which adds a decaying impulse (half-life 0.35 s) on top of the sine.
  - The total is clamped to ±24°.
  - It uses `Time.deltaTime`, so it freezes while paused.
- **Hierarchy built by `AttachLantern`:**
  ```
  HandSocket
    LanternPivot  (LanternSway)
      Lantern     (FBX instance: Iron, Glass, FlameAnchor)
        Flame     (at FlameAnchor; a small camera-facing quad with the LanternKeeper/Flame shader, as the builder's torch flames use; LanternFlame lives on Lantern)
        LanternLight (at FlameAnchor; Light + Lantern + LanternFlicker, configured as today by ConfigureLanternPoint)
        LanternHalo  (at FlameAnchor)
  ```
  The old cube body and cap are deleted.
- **Validation:** `"Lantern is not under HandSocket"` if the `Lantern` component has no `HandSocket` ancestor.

**Steps:**
- [ ] **Step 1: Write `LanternFlameTests`.** Build a GameObject with a `Lantern`, a flame transform with base scale 1, and renderers.
  - `ApplyFuel(0)` → flame scale.y == 0.45.
  - `ApplyFuel(1)` → 1.
  - `ApplyFuel(0.3f)` called twice keeps the same scale (no compounding; Review Focus 4).
- [ ] **Step 2:** Run it. Expected: fails.
- [ ] **Step 3:** Implement `LanternFlame`, the `LanternSway` kick and clamp, halo scaling by `HaloScale(fuel)`, and the builder hierarchy.
- [ ] **Step 4:** Run the EditMode tests. Expected: pass.
- [ ] **Step 5:** Run **Build All Levels** and **Validate Scene Wiring**. Expected: 0 problems, and the lantern ≤ 1.5 m.
- [ ] **Step 6: Play-mode checks.**
  - **island1:** drain fuel with the console (or wait) and watch the flame shrink and redden while the light range is unchanged at the same fuel.
  - **Rescue:** walk into water at about 20% fuel. After the rescue, the flame is still small (Review Focus 4).
  - **Loss:** the flame gutters out with the light.
- [ ] **Step 7:** Run the smoke test. Expected: pass. Revert the noise and commit: `feat: storm lantern on the hand socket with fuel-driven flame and glass glow`.

### Task 9: Animator layers and event hooks

**Files:**
- Modify: `Assets/Game/Editor/IslandBuilder.Keeper.cs`: `EnsureKeeperController` adds the layers and avatar masks
- Create: `Assets/Game/Models/Keeper/KeeperArmMask.mask`, `KeeperSpineMask.mask`
- Modify: `Assets/Game/Models/Keeper/Keeper.controller` (owned by this task)
- Modify: `Assets/Game/Scripts/KeeperAnimator.cs`
- Modify: `Assets/Game/Scripts/Beacon.cs`: `public static event Action<Beacon> Lit;`, raised in `TryLight` right after `IsLit = true`
- Modify: `Assets/Game/Scripts/GameManager.cs`: `public event Action DeathStarted;`, raised in `OnFuelDepleted` after `dying = true`
- Modify: `Assets/Game/Scripts/WaterHazard.cs`: `public event Action Rescued;`, raised in `Rescue()` right after the fade back in (after the second `Fade`)
- Modify: `Assets/Game/Editor/SceneWiring.cs`
- Test: `Assets/Game/Tests/PlayMode/KeeperEventsTest.cs`

**Interfaces:**
- **Consumes:**
  - `KeeperLean.TargetWeight` and `Step` (Task 2)
  - `LanternSway.Kick` (Task 8)
  - the `LanternHold` and `Lean` clips (Task 4)
  - `Shade.Stole` (existing, static `Action<float>`)
- **Controller layers**, in this order:
  1. `Base Layer`: unchanged.
  2. `LanternArm`:
     - mask `KeeperArmMask`: `Shoulder.R`, `UpperArm.R`, `LowerArm.R`, `Wrist.R` and the finger bones under it; use the rig's actual names
     - override blending, weight set from code
     - a single state playing `LanternHold`
  3. `Lean`:
     - mask `KeeperSpineMask`: the spine bones, neck, head
     - additive blending, weight set from code
     - a single state playing `Lean`
  4. `Actions`:
     - no mask, override blending, weight 1
     - states: `Empty` (default, no motion), `Stagger` (HitRecieve), `Light` (Interact), `Die` (Death, no exit), `ShakeOff` (HitRecieve_2)
     - triggers: `Stagger`, `Light`, `Die`, `ShakeOff`, each a transition from Any State with 0.1 s blend
     - Stagger, Light and ShakeOff return to `Empty` at exit time 0.9 with 0.15 s blend
     - Any State → Stagger, Light and ShakeOff transitions have the condition `!Dead`: a bool `Dead` set together with `Die`, so death always wins (Review Focus 2)
- **Animator update:** keeps scaled time (the default), so pause freezes it (Review Focus 1).
- **`KeeperAnimator` additions:**
  - `OnEnable`: subscribe to `Shade.Stole`, `Beacon.Lit`, `GameManager.Instance.DeathStarted` and every `WaterHazard.Rescued` in the scene.
  - `OnDisable`: unsubscribe from all of them (Review Focus 3).
  - **Handlers:**
    - Stole: trigger `Stagger` and call `sway.Kick(14f)`.
    - Lit: trigger `Light` and start `armWeightTarget = 0` for `Interact.length * 0.8`, then 1.
    - DeathStarted: set `Dead` to true and trigger `Die`.
    - Rescued: trigger `ShakeOff`.
  - **Arm weight:** eased toward `armWeightTarget` at 1/0.2 s by `Time.deltaTime`. It is forced to 1 again whenever the Actions layer is in `Empty`, so an interrupted gesture can't leave it stuck (Review Focus 2).
  - **Lean weight:** `Step(current, TargetWeight(wind phase, strength, sheltered, Wind.Instance != null), Time.deltaTime)`.
  - **Facing the gust:** while the lean weight is above 0.05, the keeper's visual model (not the `CharacterController` root) is rotated up to 25° toward the wind direction. It is applied in `LateUpdate` and is visual only.
  - `public float LeanWeight { get; }` and `public float ArmWeight { get; }` for tests and the sway global.
- **Validation:** `"Keeper.controller missing layer <name>"` for LanternArm, Lean and Actions.

**Steps:**
- [ ] **Step 1: Write `KeeperEventsTest`** (PlayMode). It loads the island4 scene (it has `Wind` and Shades) through the same scene-loading path as `StormCapeSmokeTest`, then:
  1. Raises `Shade.Stole` via reflection on the static event field, or a test hook. Asserts the Actions layer enters `Stagger` within 3 frames.
  2. Calls `TryLight` on an in-range beacon. Asserts the `Light` state and that `ArmWeight` drops below 0.5, then returns to 1 within `Interact.length + 0.5` s.
  3. Sets `Time.timeScale = 0` for 10 frames and asserts `LeanWeight` and `ArmWeight` are unchanged. Restores the time scale (Review Focus 1).
  4. Triggers a rescue, then asserts `ShakeOff`.
  5. Raises `Shade.Stole` during `Light`, then `DeathStarted`. Asserts the `Die` state, and that a following `Stole` doesn't leave `Die` (Review Focus 2).
  6. Reloads the scene and raises `Shade.Stole`. Asserts no `MissingReferenceException` in the log (Review Focus 3).
  7. Loads island1 (no `Wind`). Asserts `LeanWeight == 0` after 30 frames, with no exceptions (Review Focus 5).
- [ ] **Step 2:** Run the PlayMode tests. Expected: `KeeperEventsTest` fails.
- [ ] **Step 3:** Implement the events, the layers, the masks and the `KeeperAnimator` changes.
- [ ] **Step 4:** Run the PlayMode tests (the smoke test plus `KeeperEventsTest`) and the EditMode tests. Expected: all pass.
- [ ] **Step 5:** Run **Build All Levels** and **Validate Scene Wiring**. Expected: 0 problems.
- [ ] **Step 6:** Play-mode check on island4: during a gust the keeper leans into the wind and the lantern stays held out while running. Lighting a beacon plays the reach.
- [ ] **Step 7:** Revert the noise (keep `Keeper.controller` and the masks) and commit: `feat: keeper lantern-arm, lean and action layers driven by game events`.

### Task 10: Footstep dust at the feet

**Files:**
- Modify: `Assets/Game/Scripts/KeeperAnimator.cs`: `OnFootstep`
- Modify: `Assets/Game/Scripts/PlayerController.cs`: `UpdateDust` continuous emission

**Interfaces:**
- **`KeeperAnimator.OnFootstep()`:** in addition to the audio, emits a burst of 4–6 particles from the `Dust` system at the world position of whichever of `Foot.L` / `Foot.R` is lower. Use `ParticleSystem.Emit(EmitParams, count)` with that position.
- **`PlayerController`:**
  - When `UseDistanceFootsteps` is false (the clips have footstep events), continuous dust emission while moving is turned off.
  - Landing dust (`BurstLandingDust`) is unchanged.
  - When the clips have no events, today's behaviour stays.

- [ ] **Step 1:** Implement the change.
- [ ] **Step 2:** Play-mode check on island1: walking and running leave small puffs at each foot, and landing dust still appears.
- [ ] **Step 3:** Run the smoke test. Expected: pass. Commit: `feat: footstep dust emitted at the feet`.

### Task 11: After set, comparison page, performance and regression

**Files:**
- Create: `docs/look/keeper/2026-10-03-after/**`
- Create: `docs/look/compare/phase-c.html`
- Create: `docs/look/perf/2026-10-03-phase-c.md`

**Steps:**
- [ ] **Step 1:** Run `KeeperCloseupCapture.Run("docs/look/keeper/2026-10-03-after")`. Expected: 18 JPGs. Check `gust` and `lowfuel` visually: the lean is visible, and the flame is small and red-orange.
- [ ] **Step 2: Write `phase-c.html`.**
  - Same structure and styling as `docs/look/compare/phase-b.html`.
  - One row per shot × state, with before and after side by side, Low and Ultra tabs.
  - Plus the Blender renders.
- [ ] **Step 3: Performance.**
  - Make a player build.
  - Run `-lkperf -lkscene=IslandN -lkseconds=20 -lkquality=0|3 -lkreport=...` for all four islands on Low and Ultra.
  - Write `2026-10-03-phase-c.md` with the average and p95 per run against the Phase B numbers.
  - Expected: Ultra average ≤ 12.5 ms everywhere. If it fails, stop and report to the controller; do not cut effects unilaterally.
- [ ] **Step 4: Regression.**
  - EditMode tests and PlayMode tests (the smoke test and `KeeperEventsTest`): all pass.
  - Build All Levels and Validate Scene Wiring: 0 problems.
  - Play-mode check of dawn, lightning, low fuel, water rescue, and that movement and collision feel the same on each island.
- [ ] **Step 5:** Revert the noise and commit: `docs: Phase C after set, comparison page and perf`.
- [ ] **Step 6: User approval gate.** The controller shows the user `phase-c.html` and waits for approval of the before/after close-ups.
