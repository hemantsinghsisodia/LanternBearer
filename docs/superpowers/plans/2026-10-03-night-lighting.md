# Visual Pass Phase B (Night Lighting) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the painterly balanced-darkness night on every island, driven by each island's `LookProfile`. That means:
- a sky shader
- cool moonlight, ambient light and fog
- ground mist
- per-island post-processing
- an always-on moon-rim pass
- the lantern's glow

Gameplay stays unchanged.

**Architecture:**
- **Pure mapping.** A pure `LookMapping` (Logic) turns profile values into rendering values. EditMode tests cover it.
- **Scene start.** A runtime `LookApplier` applies those values when the scene starts.
- **Builder.** `IslandBuilder.CreateAtmosphere` only places the components and assets; it no longer hard-codes any values.
- **Grading.** Post-processing is split into per-island look volumes plus one effects volume that only low fuel and lightning write to.
- **Moon rims.** A custom URP renderer feature draws them from depth.

**Tech Stack:** Unity 6000.6.3f1, URP (Volume framework, ScriptableRendererFeature, RenderGraph-compatible), hand-written HLSL, C#, Unity Test Framework (NUnit), Unity MCP.

**Spec:** `docs/superpowers/specs/2026-10-03-night-lighting-design.md`. Parent spec: `docs/superpowers/specs/2026-10-03-art-direction-design.md`. Read both.

## Global Constraints
- **Code style:**
  - `namespace LanternKeeper`, Allman braces, explicit `if`/`return`.
  - `[SerializeField]` private fields on MonoBehaviours.
  - Comment density matches the surrounding code.
- **Shaders:** hand-written URP HLSL in `Assets/Game/Shaders/`. No Shader Graph.
- **Gameplay unchanged.**
  - `Lantern.maxRange` (14) and `minRange` (3.5) must not change. `Lantern.Radius` is the gameplay reveal radius.
  - No changes to reveal, moth or Shade logic.
  - Terrain, hidden paths and props stay identical in rebuilt scenes. Check with a semantic comparison against the pre-task build: per-object name, transform and components.
- **Warm/cold rule:** the only warm light sources are the lantern, lit beacons, torches and fireflies. Nothing in ambient light, fog, moonlight or grading is warm.
- **Colours** (verbatim from the specs):
  - Signal colours:
    - lantern amber `#FFB15C`
    - glow core `#FFD38A`
    - drain violet `#B48CFF`
    - lightning blue `#BCD2FF`
    - firefly green `#B8FFB0`
  - Dawn colours:
    - `dawnTop` `#7E9CCB`
    - `dawnHorizon` `#F4B38A`
    - `dawnGround` `#C98A7A`
    - `dawnLight` `#FFD9B0`
- **Starting values** (to be tuned against captures; final values live in `LookProfile` / `LookMapping` constants):
  - Moon intensity: 0.35, or 0.12 when `!moonOn`.
  - `skyHorizon` = the sky colour moved 35% toward `moonRim` (toward `extra` on island4).
  - Moon-rim strength: from the profile (1.0; 0.6 on island4).
- **Post-processing by preset:**

  | Preset | Post-processing | Moon rim | Mist |
  |---|---|---|---|
  | Low | none | half-res simple rim | none |
  | Medium | grading + bloom (reduced) | full rim | half |
  | High | full (tonemapping, grading, bloom, vignette) | full rim | full |
  | Ultra | full + SSAO + light film grain | full rim | full |

  The moon rim is never off.
- **Performance:** RTX 4050 Laptop, plugged in, Windows "Best performance", 1080p.
  - Ultra: ≤ 12.5 ms.
  - Low: ≥ 60 fps on the integrated GPU (the user runs this one).
  - Gate: if any island's Ultra is above **10 ms** in a player build before the change, do Task 2 (cost cuts) before the effects tasks.
- **Unity environment:**
  - The editor is open with the Unity MCP. Its tools are deferred, so load them via ToolSearch. Never use batchmode.
  - Use temporary `runInBackground` for play-mode checks, and restore it afterwards.
  - Mark the intro as seen before play-mode checks: PlayerPrefs `LanternKeeperIntro_<levelId>`. Restore the previous state afterwards.
  - After Build All Levels, revert noise in: `Keeper.controller`, `ProjectSettings/*.asset`, unrelated `Materials/Generated/*.mat`, and `*_Terrain.asset`.
- **Commits** end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Never push.

## Review Focus
1. **Dawn after a win plays all the way to the dawn look.** Sky, ambient light, moon rim and grading must all reach their dawn values, and nothing may stay stuck at night values. Pinned by `LookMappingTests.DawnLerpEndpoints` (Task 3) and the dawn play-mode check in Task 6, Step 5.
2. **Loading a different island resets the look.** Going from Island 4 (lavender rim, no moon) to Island 1 must not carry Island 4's rim colour, fog or sky over. `LookApplier` sets every value on each scene load. Pinned by Task 5, Step 5 (load island4 then island1 and compare the applied values).
3. **Changing the graphics preset mid-game updates moon-rim quality, mist and volumes live.** It must work from the pause menu, without a reload. Pinned by Task 7, Step 5 and Task 6, Step 5 (switch Low ↔ Ultra in play mode).
4. **Lightning and low fuel never change the island's base grading.** After a flash, or after fuel recovers, the look volume profile asset must be byte-identical on disk. Pinned by Task 6, Step 5 (`git status` shows no `LookVolume_*.asset` change after play).
5. **Shades and moths stay findable in the darker night.** On a dim monitor they must stay visible at default brightness. Pinned by Task 9's readability pass, which captures Shade and moth shots on every island and checks them against readability rule 1.

---

### Task 1: Player-build performance baseline

**Files:**
- Create: `docs/look/perf/2026-10-03-baseline.md`
- Modify: `Assets/Game/Scripts/LK_PerfProbe.cs`, only if `-lkscene` rejects Island3 or Island4. Its comment says Island1/Island2.

**Interfaces:**
- Consumes: the `LK_PerfProbe` command-line flags `-lkperf -lkscene=<Scene> -lkseconds=<n> -lkreport=<path>`, and `-lkvsync` (forced off for the run).
- Produces: per-island Low and Ultra frame times, plus the gate decision. Later tasks read it from the doc.

- [ ] **Step 1: Make a player build.**
  - Windows 64-bit, non-development, Burst off.
  - Output to `Builds/PerfB/LanternKeeper.exe`.
  - Use the existing BuildPipeline approach with `BuildOptions.None`.
  - Revert any tracked files the build touches.
- [ ] **Step 2: Confirm `-lkscene` accepts all four islands.** Read `LK_PerfProbe` (~lines 90–110). If it restricts to Island1 and Island2, widen it to any build scene, then rebuild.
- [ ] **Step 3: Run the probe 8 times** (four islands × Low and Ultra).
  - How to select the preset: either set the PlayerPrefs `LanternKeeperGraphics` value before each run (0 = Low, 3 = Ultra) and restore it afterwards, or use a probe flag if one exists.
  - Command: `Builds/PerfB/LanternKeeper.exe -lkperf -lkscene=IslandN -lkseconds=20 -lkreport=<abs path> -screen-width 1920 -screen-height 1080 -screen-fullscreen 1`.
  - This launches the game fullscreen on the user's laptop for about 30 s per run. The controller tells the user first.
- [ ] **Step 4: Write the baseline doc.** In `docs/look/perf/2026-10-03-baseline.md`, record:
  - a table: island, preset, avg ms, p95 ms (if the report has it), fps
  - the machine notes: GPU name from the report, resolution
  - the gate decision, one line: "Ultra max X ms → Task 2 required / not required"
- [ ] **Step 5: Commit.**
  ```bash
  git add docs/look/perf
  git commit -m "docs: Phase B player-build performance baseline"
  ```
  Also commit `LK_PerfProbe.cs` if it changed. `Builds/` stays ignored.

### Task 2: Ultra cost cuts (only if Task 1's gate tripped)

If Task 1 shows Ultra at 10 ms or below on every island, skip this task. The controller records "Task 2: skipped (gate not tripped)" in the ledger.

**Files:**
- Modify: `Assets/Game/Settings/Graphics/GraphicsProfile_Ultra.asset`, and `GraphicsProfile_High.asset` only if a cut must cascade.
- Modify: the scatter density hook for Island 3 grass, either in `IslandBuilder` scatter or `TerrainQuality`, whichever applies `grassDensity`.

**Interfaces:**
- Consumes: the `GraphicsProfile` fields `waterReflectionProbe`, `shadowDistance`, `shadowCascadeCount`, `grassDensity`, `glowLightCap`, `glowLightCapFireflies`, `glowLightCapMoths`.

- [ ] **Step 1: Apply cuts one at a time, in this order. Stop as soon as every island is at or below 10 ms in the player-build probe.**
  1. `waterReflectionProbe = false`
  2. `shadowDistance` reduced by 25%, `shadowCascadeCount` 4 → 2
  3. Island 3 grass density × 0.75 (Island 3 only)
  4. `glowLightCap` × 0.75
- [ ] **Step 2: Re-run the probe after each cut.** Record each result in a "Cuts" section of the baseline doc.
- [ ] **Step 3: Visual check.** Recapture with Lantern Keeper → Look → Capture Look Baseline into a scratch folder outside the repo. Compare the Ultra spawn and beacon shots for an obvious loss. Note it in the doc.
- [ ] **Step 4: Commit.**
  ```bash
  git commit -m "perf: Ultra cost cuts to meet Phase B budget"
  ```

### Task 3: LookMapping, profile additions and DawnLook

**Files:**
- Create: `Assets/Game/Scripts/Logic/LookMapping.cs`
- Create: `Assets/Game/Scripts/Look/DawnLook.cs`
- Create: `Assets/Game/Art/Look/DawnLook.asset`
- Modify: `Assets/Game/Scripts/Look/LookProfile.cs`
- Modify: the four `LookProfile_island*.asset` files
- Modify: `Assets/Game/Scripts/Graphics/GraphicsProfile.cs` (add `public int moonRimQuality;`)
- Modify: the four `GraphicsProfile_*.asset` files (`moonRimQuality`: Low 0, Medium/High/Ultra 1)
- Test: `Assets/Game/Tests/EditMode/LookMappingTests.cs`

**Interfaces:**
- Produces, in `LookMapping` (static class, Logic; inputs and outputs are plain `Color` and `float`, no Unity objects):
  - `struct AmbientValues { Color sky; Color equator; Color ground; float intensity; }`
  - `static AmbientValues Ambient(Color sky, Color sea, Color land, float ambientIntensity)`
    - sky = sky × 1.6, equator = sea × 1.4, ground = land × 0.6, then clamped to 0..1.
    - These are starting constants, as named `const` fields.
  - `static Color Horizon(Color sky, Color toward, float t = 0.35f)`, i.e. `Color.Lerp(sky, toward, t)`
  - `static float MoonIntensity(bool moonOn, float profileIntensity)`
    - returns `profileIntensity` when `moonOn`
    - otherwise `Mathf.Min(profileIntensity, 0.12f)`
  - `static Color LerpDawn(Color night, Color dawn, float u)`: clamped lerp.
  - `static bool IsCool(Color c)`
    - true when `c.b >= c.r`, or the colour is near-black (max channel < 0.04)
    - used to enforce the warm/cold rule
- Produces, `LookProfile` additions:
  - `Color skyHorizon`, `Color skyGround`
  - `float moonIntensity` (0.35; island4 0.12)
  - `float ambientIntensity` (1.0, tuned in Task 5)
  - `DawnLook dawn`
  - Asset values:
    - `skyHorizon = LookMapping.Horizon(sky, moonRim)`, or `Horizon(sky, extra)` on island4
    - `skyGround` = the island's sea colour × 0.6
- Produces, `DawnLook : ScriptableObject`:
  - `Color dawnTop, dawnHorizon, dawnGround, dawnLight` (values from Global Constraints)
  - `float dawnAmbientBoost = 0.28f`

- [ ] **Step 1: Write the failing tests.**
  - `AmbientStaysCoolForEveryIsland`: for each `LookProfile_island1..4`, every colour in `LookMapping.Ambient(...)` satisfies `IsCool`.
  - `HorizonIsLerp`: `Horizon(black, white, 0.35f)` equals grey 0.35 within 1e-4.
  - `MoonOffCapsIntensity`: `MoonIntensity(false, 0.35f) == 0.12f` and `MoonIntensity(true, 0.35f) == 0.35f`.
  - `DawnLerpEndpoints`: `LerpDawn(a, b, 0) == a`, `LerpDawn(a, b, 1) == b`, `LerpDawn(a, b, 2) == b`, and `LerpDawn(a, b, 0.5f)` is the midpoint.
  - `DawnLookMatchesSpec`: the hex of each colour in `DawnLook.asset` equals the Global Constraints values.
  - `IslandProfilesHaveDawnAndHorizon`: every profile has a non-null `dawn`, and its `skyHorizon` equals the formula above within 1/255.
  - `MoonRimQualityPerPreset`: load the four GraphicsProfile assets. Low has `moonRimQuality == 0`, the others have 1.
- [ ] **Step 2: Run the EditMode tests.** Expected: FAIL, because the types don't exist yet.
- [ ] **Step 3: Implement** the code, assets and fields above.
- [ ] **Step 4: Run the EditMode tests.** Expected: all pass, including all existing tests.
- [ ] **Step 5: Commit.**
  ```bash
  git commit -m "feat: LookMapping, DawnLook and profile additions for Phase B"
  ```

### Task 4: NightSky shader

**Files:**
- Create: `Assets/Game/Shaders/NightSky.shader` (`Shader "LanternKeeper/NightSky"`, a URP skybox-compatible unlit shader)
- Create: `Assets/Game/Art/Look/Sky/NightSky_island1..4.mat`
- Modify: `Assets/Game/Scripts/DawnSequence.cs`, only if a property it animates is missing or renamed.

**Interfaces:**
- Produces shader properties:
  - `_Top`, `_Horizon`, `_Ground` (Color)
  - `_Blend` (Float 0..1, dawn)
  - `_DawnTop`, `_DawnHorizon`, `_DawnGround` (Color)
  - `_MoonDir` (Vector)
  - `_MoonColor` (Color)
  - `_MoonOn` (Float 0/1)
  - `_MoonSize` (Float, default 0.035)
  - `_MoonGlow` (Float, default 0.25)
  - `_Clouds` (Float 0/1)
  - `_CloudColor` (Color)
- Consumes: the existing `StarTwinkle` stars (separate geometry; leave them as they are), and the existing `DawnSequence` blend of `_Top`/`_Horizon`/`_Ground`/`_Blend`.

- [ ] **Step 1: Write the shader.**
  - Gradient: `_Ground` → `_Horizon` → `_Top` by view direction y, with a soft horizon band.
  - Moon: a disc plus a glow toward `_MoonDir`, enabled by `_MoonOn`.
  - Clouds: slow scrolling noise bands when `_Clouds`.
  - Dawn: `lerp(night, dawn, _Blend)` per colour.
  - Fog: none. The skybox ignores fog.
- [ ] **Step 2: Create the four materials** from each `LookProfile`:
  - `_Top` = sky
  - `_Horizon` = skyHorizon
  - `_Ground` = skyGround
  - `_Moon*` from moon / moonOn
  - `_Clouds` = rain
  - `_DawnTop`, `_DawnHorizon`, `_DawnGround` from `DawnLook`
  - `_MoonDir` from the existing moon-direction logic (`BrightestCubemapDirection` result), or a fixed elevation of 25°
- [ ] **Step 3: Verify.**
  - Assign `NightSky_island1.mat` as the skybox in Island 1 temporarily, without saving the scene.
  - Take a Game-view screenshot to a scratch folder.
  - Confirm: a gradient with no banding, the moon visible, and no pink "missing shader" output.
  - Then set `_Blend` to 1 and confirm the dawn colours.
- [ ] **Step 4: Commit.**
  ```bash
  git commit -m "feat: painterly NightSky shader and per-island sky materials"
  ```

### Task 5: LookApplier, moonlight, ambient light, fog, mist and builder wiring

**Files:**
- Create: `Assets/Game/Scripts/Look/LookApplier.cs`. It's a MonoBehaviour. If it can't see `LevelConfig`, put it in Assembly-CSharp instead (`Assets/Game/Scripts/LookApplier.cs`).
- Create: `Assets/Game/Scripts/Look/GroundMist.cs`. It's a MonoBehaviour that manages the mist cards and reacts to `GraphicsQuality.QualityChanged`. It lives in Assembly-CSharp if it needs `GraphicsQuality`.
- Modify: `Assets/Game/Editor/IslandBuilder.cs` (`CreateAtmosphere`, ~438–480)
- Modify: `Assets/Game/Editor/SceneWiring.cs`
- Modify: `Assets/Game/Scenes/Island1..4.unity`, via a rebuild.

**Interfaces:**
- Consumes: `LookMapping` and `DawnLook` (Task 3), and the NightSky materials (Task 4).
- Produces: `LookApplier`:
  - `[SerializeField] LookProfile profile`, `[SerializeField] Light moon`, `[SerializeField] Material skyMaterial`
  - `public LookProfile Profile`
  - `public void Apply()`: sets everything below. It runs in `Awake`, and again on `GraphicsQuality.QualityChanged`.
    - `RenderSettings.skybox`
    - moon colour, intensity and shadows
    - `RenderSettings.ambientMode = Trilight` and the three ambient colours, from `LookMapping.Ambient`
    - fog on, exponential, colour and density
    - `Shader.SetGlobalColor("_LKMoonRimColor", …)` and `SetGlobalFloat("_LKMoonRimStrength", …)`, which the Task 7 rim pass reads
    - `Shader.SetGlobalVector("_LKMoonDir", -moon.transform.forward)`, the direction toward the moon, which the rim pass's facing term reads
  - `public static LookApplier Current`
- Produces: `GroundMist`.
  - Fields: `[SerializeField] float density` (1 = full).
  - Places soft camera-facing quads, with the additive or alpha-faded `LanternKeeper/AdditiveUnlit` or a new `MistCard` material. They go near water level and in valleys, at deterministic positions from the island seed and a dedicated `System.Random(seed + 70)`.
  - Card count by preset: High and Ultra 1.0, Medium 0.5, Low 0.

- [ ] **Step 1: Write `LookApplier` and `GroundMist`** as specified.
- [ ] **Step 2: Change `CreateAtmosphere`.**
  - Remove the hard-coded ambient colours, intensities and fog values.
  - Place a `LookApplier` wired to `config.lookProfile`, the moon light and the island sky material.
  - Keep the moonlight's direction logic.
  - Place `GroundMist` only when `lookProfile.mist`.
  - `RenderSettings` saved in the scene should still get sensible values, set through `LookApplier.Apply()` in the editor, so edit-mode previews look right.
- [ ] **Step 3: Add SceneWiring checks.**
  - Every island has exactly one `LookApplier`, with a profile whose `levelId` matches the scene's `LevelConfig`.
  - `RenderSettings.skybox.shader.name == "LanternKeeper/NightSky"`.
- [ ] **Step 4: Rebuild.** Run Build All Levels, then Validate.
  - Expected: 0 problems.
  - The semantic comparison against the pre-task build shows changes only in lighting, sky, mist and fog objects.
  - Revert noise.
- [ ] **Step 5: Island-switch reset check (Review Focus 2).** In play mode, load Island4, then load Island1. Log `RenderSettings` fog colour and density, the ambient colours and the `_LKMoonRim*` globals each time. Island1's values must equal Island1's mapping, with nothing left over from Island4.
- [ ] **Step 6: Commit.**
  ```bash
  git commit -m "feat: LookApplier drives sky, moon, ambient, fog and mist per island"
  ```

### Task 6: Look volumes, effects volume and dawn grading

**Files:**
- Create: `Assets/Game/Editor/Look/LookVolumeBuilder.cs`. It's an editor utility that generates profiles from `LookProfile`.
- Create:
  - `Assets/Game/Art/Look/Volumes/LookVolume_island1..4.asset`
  - `Assets/Game/Art/Look/Volumes/LookVolume_dawn.asset`
  - `Assets/Game/Art/Look/Volumes/EffectsVolume.asset`
- Modify:
  - `Assets/Game/Scripts/LowFuelFX.cs`
  - `Assets/Game/Scripts/Lightning.cs`
  - `Assets/Game/Scripts/DawnSequence.cs`
  - `Assets/Game/Editor/IslandBuilder.cs` and `IslandBuilder.Art.cs` (stop creating `NightVolumeProfile`)
  - `Assets/Game/Editor/SceneWiring.cs`
  - the four island scenes, via a rebuild
- Delete: `Assets/Game/Materials/Generated/NightVolumeProfile.asset`, once it's referenced nowhere.

**Interfaces:**
- Produces, in each scene: a GameObject `LookVolume` holding a global Volume with priority 0 and the island's `LookVolume_<id>` profile.
  - High and Ultra contents:
    - Tonemapping Neutral
    - ColorAdjustments from the profile: saturation, contrast, postExposure
    - Shadows-Midtones-Highlights, shadows tinted toward the island's `sea`
    - Lift-Gamma-Gain, with a slight lift
    - Bloom: threshold 1.1, intensity 0.6, scatter 0.65
    - Vignette 0.22
    - FilmGrain 0.12, Ultra only, toggled at runtime
  - Medium: the same with bloom at half intensity.
  - Low: Volume disabled, since the camera has post off.
  - Toggling is done by a small `LookVolumeQuality` component reacting to `GraphicsQuality.QualityChanged`, inside `LookApplier` or alongside it.
- Produces, in each scene: a GameObject `EffectsVolume`.
  - A global Volume with priority 10, using its own runtime profile.
  - Vignette, with `intensity` overridden starting at 0.
  - ColorAdjustments, with `postExposure` 0 and `saturation` 0 overridden.
- Produces: `DawnSequence` adds a `LookVolume_dawn` volume. While dawn plays it fades that volume's weight 0→1 and the look volume's 1→0, and it lerps `_LKMoonRimStrength` to 0.
- Changes:
  - `LowFuelFX` writes only to `EffectsVolume`. Its serialized `volume` reference is wired to it by the builder and SceneWiring. It no longer uses `FindAnyObjectByType<Volume>`.
  - `Lightning` writes exposure only to `EffectsVolume`.
  - Neither ever touches `LookVolume_*`.

- [ ] **Step 1: Write `LookVolumeBuilder`** with the menu item `Lantern Keeper/Look/Build Look Volumes`. It generates the five profiles and `EffectsVolume.asset` from the `LookProfile` and `DawnLook` assets. It's idempotent: it updates existing assets in place and keeps their GUIDs.
- [ ] **Step 2: Wire the builder.** It places `LookVolume`, `EffectsVolume` and the dawn volume, and wires `LowFuelFX` and `Lightning`. Remove all `NightVolumeProfile` creation. Then grep for `NightVolumeProfile`: there must be no references left in code or assets. Then delete the asset.
- [ ] **Step 3: Add SceneWiring checks.**
  - `EffectsVolume.priority > LookVolume.priority`.
  - `LowFuelFX.volume` and `Lightning.volume` reference `EffectsVolume`.
  - No `NightVolumeProfile` is referenced.
- [ ] **Step 4: Rebuild.** Run Build All Levels then Validate: 0 problems. The semantic comparison may show only volume objects added and the old volume removed.
- [ ] **Step 5: Play-mode checks** on Island 4 and Island 1:
  - a. Force a lightning flash, then force low fuel to 10% and back to full. Afterwards `git status` shows no change to any `LookVolume_*.asset` (Review Focus 4). The runtime `EffectsVolume` values go back to 0.
  - b. Force a win and let dawn play to the end. The look volume weight reaches 0, the dawn volume weight reaches 1, `_LKMoonRimStrength` reaches 0, and the sky `_Blend` reaches 1 (Review Focus 1).
  - c. Switch the preset Low → Ultra → Low in play mode. The volume is disabled on Low, grain is on only on Ultra, and nothing throws (Review Focus 3).
- [ ] **Step 6: Commit.**
  ```bash
  git commit -m "feat: per-island look volumes, effects volume and dawn grading"
  ```

### Task 7: MoonRimFeature

**Files:**
- Create: `Assets/Game/Scripts/Look/MoonRimFeature.cs` (ScriptableRendererFeature plus its pass, RenderGraph API)
- Create: `Assets/Game/Shaders/MoonRim.shader` (`Shader "Hidden/LanternKeeper/MoonRim"`)
- Create: `Assets/Game/Scripts/Look/MoonRimSettings.cs`. This is the runtime component that sets quality from `GraphicsQuality` and drives the Island 4 lightning boost. Put it in Assembly-CSharp if it needs `GraphicsQuality` and `Lightning`.
- Modify: `Assets/Settings/PC_Renderer.asset` (add the feature)
- Modify: `Assets/Game/Editor/SceneWiring.cs`
- Test: add `MoonRimQualityFor` tests to `LookMappingTests.cs`

**Interfaces:**
- Consumes: the `_LKMoonRimColor` and `_LKMoonRimStrength` globals (Task 5), `GraphicsProfile.moonRimQuality` (Task 3), `Lightning.CurrentFlash` (existing static, 0..1), and the camera depth texture.
- Produces: the pure function `LookMapping.MoonRimQualityFor(int moonRimQuality)`, which returns `struct MoonRimQuality { float resolutionScale; bool simpleEdge; }`.
  - Quality 0 gives (0.5, true).
  - Otherwise (1.0, false).
  - Never "off".
- Produces: the pass.
  1. Reconstruct normals from depth with neighbour taps.
  2. `rim = edgeTerm (depth discontinuity) + facing term (normal · moonDir)`, faded by distance (start 15 m, end 70 m).
  3. Composite additively: `colour += _LKMoonRimColor * _LKMoonRimStrength * rim`.

  It runs after opaques and before post-processing. On Low it renders at half resolution and upsamples. The globals `_LKMoonDir` (from the moon light, set by `LookApplier`) and `_LKMoonRimBoost` (0..1) feed the facing term and the lightning boost.
- Produces: `MoonRimSettings`. It sets the feature's quality on `QualityChanged`. Each frame, on islands without a moon, it sets `_LKMoonRimBoost = Lightning.CurrentFlash`, which raises strength by up to +0.8 during a flash.

- [ ] **Step 1: Write the failing tests.**
  - `MoonRimQualityFor(0)` returns (0.5, true).
  - `MoonRimQualityFor(1)` returns (1.0, false).
  - `MoonRimQualityFor(99)` returns (1.0, false).
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement** the function, the feature, the shader and the settings. Add the feature to `PC_Renderer.asset`. Make sure the camera's depth texture is enabled. Check the PC_RPAsset `supportsCameraDepthTexture` setting and enable it if it's off; note the cost.
- [ ] **Step 4: Run the tests.** Expected: all pass. Add a SceneWiring check that `PC_Renderer` contains a `MoonRimFeature`.
- [ ] **Step 5: Play-mode checks** on Island 1 and Island 4:
  - Ultra and Low screenshots of a cliff edge and a shoreline from the shot list. Rims must be visible on both presets.
  - Switch presets live: quality changes without errors.
  - Island 4: force a flash and confirm the rim brightens, then settles.
  - Dawn: the rim fades to 0.
- [ ] **Step 6: Commit.**
  ```bash
  git commit -m "feat: always-on moon-rim pass keeps edges readable at night"
  ```

### Task 8: Lantern glow

**Files:**
- Modify: `Assets/Game/Scripts/Lantern.cs` (light colour and intensity curve only)
- Modify: `Assets/Game/Scripts/LanternFlicker.cs`, only if it overrides colour
- Modify: `Assets/Game/Editor/IslandBuilder.Keeper.cs` (`AttachLantern` / `ConfigureLanternPoint`: light colour; add the halo)
- Create: `Assets/Game/Art/Look/LanternHalo.png` (generated soft radial texture) and `Assets/Game/Art/Look/LanternHalo.mat` (additive, `LanternKeeper/AdditiveUnlit`)
- Modify: `Assets/Game/Prefabs/Characters/Keeper.prefab`, via a rebuild or Refresh Keeper Look

**Interfaces:**
- Consumes: `LookPalette.LanternAmber` and `GlowCore`.
- Produces:
  - The lantern Light colour is `FromHex(LanternAmber)`.
  - `maxIntensity` is retuned. Start at 5.5, then tune against captures.
  - `minIntensity` stays at 0.35.
  - `maxRange` and `minRange` are **unchanged**.
  - A child quad `LanternHalo` at the light position:
    - camera-facing, size 0.9 m
    - colour `GlowCore`
    - alpha follows `Lantern.FuelNormalized` × flicker
    - no colliders, no shadows

- [ ] **Step 1: Implement** the colour, the intensity retune and the halo.
- [ ] **Step 2: Regression check.** EditMode and PlayMode tests pass. Also run a play-mode check that hidden-stone reveal works at full and at low fuel: `LightRevealed.Reveal` near the player is above 0.5 at full fuel. `Lantern.Radius` is unchanged from before the task (log it).
- [ ] **Step 3: Commit.**
  ```bash
  git commit -m "feat: amber lantern glow with halo; reveal radius unchanged"
  ```

### Task 9: After set, comparison page, performance and readability

**Files:**
- Create: `docs/look/baseline/<YYYY-MM-DD>/…`, the "after" capture (JPEG q92, via LFS)
- Create: `docs/look/compare/phase-b.html`
- Create: `docs/look/perf/<YYYY-MM-DD>-phase-b.md`
- Modify: `docs/look/baseline/README.md`, to add a link to the compare page

**Interfaces:**
- Consumes: the Phase A "before" set `docs/look/baseline/2026-10-03/`, the capture tool and `LK_PerfProbe`.

- [ ] **Step 1: Capture the after set.** Run Lantern Keeper → Look → Capture Look Baseline into today's date folder. If today is still 2026-10-03, use `2026-10-03-b` so the before set isn't overwritten.
- [ ] **Step 2: Build the compare page.** `phase-b.html` is a standalone page. For each island × shot and each screen, on Low and Ultra, it shows the before and after images side by side, using relative paths to both sets.
- [ ] **Step 3: Measure performance.**
  - Run the player-build probe (as in Task 1) after the change, on all islands, Low and Ultra.
  - Write `docs/look/perf/<date>-phase-b.md` with before/after tables and a pass or fail against Ultra ≤ 12.5 ms.
  - If it fails, report it to the controller. The controller decides on more cuts through Task 2's list, and does not loosen the budget silently.
- [ ] **Step 4: Readability pass (Review Focus 5).** Look at every after-set Ultra and Low image of a cliff, shoreline, water, Shade (island4) and moth. Confirm per readability rule 1 that edges and creatures are visible at default brightness. List any failures in the perf doc's "Readability" section, with the image path.
- [ ] **Step 5: Ask the user** to run the Low preset on the integrated GPU. The controller asks, using the exe path, the Windows setting (Settings → Display → Graphics → set LanternKeeper.exe to Power saving), and the probe command. Record the result when the user reports back.
- [ ] **Step 6: Commit.**
  ```bash
  git add docs/look
  git commit -m "docs: Phase B after-set, comparison page, perf and readability"
  ```
