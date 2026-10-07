# Firefly Swarms (Phase F3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the placeholder firefly with a blinking six-mote swarm that streams into the lantern on collect, then fold in the F1 and F2 review leftovers. Gameplay stays unchanged.

**Architecture:**
- A pure Logic class, `FireflyCurve`, owns every timing rule: blink, lit count, stream, linger and fade-in.
- An instanced additive shader, `FireflyMote`, draws the motes. It computes the resting drift and blink on the GPU from per-mote properties.
- A `FireflySwarm` controller on the prefab drives three things on the CPU: the light breathing, the collect stream and linger (on mote transforms, so tests can observe them), and the respawn fade-in.
- An idempotent `CreatureInstaller.InstallFirefly` swaps the prefab visuals.

**Tech Stack:** Unity 6000.6.3f1, URP (Deferred), C#, HLSL, NUnit EditMode/PlayMode, Unity MCP.

**Spec:** `docs/superpowers/specs/2026-10-08-firefly-swarms-f3-design.md`

## Global Constraints
- **Gameplay unchanged.** Fuel +20 lands on the collect frame. The respawn delay, `PickHome`, trigger, bob/wander and `LightQuality` firefly handling all stay as they are.
- **Colour.** Mote colour is exactly `LookPalette.FireflyGreen` (`#B8FFB0`). Green is used nowhere else.
- **Mote counts.** 6 motes, or 4 on Low. 2 lingering motes, or 1 on Low.
- **Sizes.** Core about 0.03 m, halo about 0.15 m, cloud about 1 m wide, linger orbit about 0.3 m.
- **Low preset.** Selected by a global float `_LKFireflyLow`, published like `MothVisual.PublishQuality`. No new `multi_compile` keywords.
- **Prefab.** The islands use `Assets/Game/Prefabs/Gameplay/Firefly.prefab` (guid e5ea16fe…). Installers are idempotent. No `IslandBuilder` level rebuilds. Terrain and world content stay hash-identical.
- **Protected files.**
  - Never edit, revert or commit `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`.
  - Leave `Assets/Settings/Build Profiles/` untracked.
- **Noise to revert with `git checkout`:**
  - TMP font SDF assets
  - `ShadeBody.mat` / `Smoke.mat`
  - `ProjectSettings/*`
  - `EditorSettings`
  - Look volumes
  - URP light-data-only scene noise
- **PlayerPrefs.** Restore `LanternKeeperGraphics` to 0.
- **EditMode UI tests.** Call `DestroyImmediate` and `Undo.ClearAll()` in TearDown and OneTimeTearDown.
- **Perf.** One probe pass per island on Low and Ultra against `docs/look/perf/2026-10-08-phase-f2.md`. Limit +0.3 ms average. If it fails, report the numbers and stop. No A/B runs.

## Review Focus
1. **The keeper sprints away during the stream.** Motes must chase the lantern's current position and still arrive by 0.5 s. Task 3 test: `StreamFollowsMovingLantern`.
2. **The scene reloads or the lantern is destroyed mid-linger** (Retry, or death right after a pickup). There must be no exception, and the lingering motes fade out where they are. Task 3 test: `LingerSurvivesLanternDestroyed`.
3. **The graphics preset changes to Low at runtime.** At most 4 motes and 1 linger take effect without a reload, and the light breathing follows the visible count. Task 2 test: `LowPresetHidesExtraMotes`.
4. **Reduce flashing is toggled while running.** Motes never drop below the 30% floor, and the lantern kick is halved. Tests: Task 1 `ReduceFlashingFloor`, Task 3 `KickHalvedWithReduceFlashing`.
5. **Two swarms are collected within 0.5 s of each other.** Each streams independently, and fuel is added twice. Task 3 test: `TwoCollectsStreamIndependently`.

## Rulings made while planning
- **Stream and linger positions are set on the CPU, on mote transforms.** This deviates from the spec's "shader-driven by progress + lantern position". It makes the PlayMode position checks possible, and it covers only 2 s for at most 6 motes, so the cost is negligible. The shader still owns the resting drift and blink. A `_Stream` property (0→1) fades the shader drift out and holds the mote lit.
- **The unreferenced legacy `Assets/Game/Prefabs/Firefly.prefab`** (not the Gameplay one) is deleted with the stale scene in Task 4, under the same "only if unreferenced" rule.

---

### Task 1: `FireflyCurve` timing (Logic)

**Files:**
- Create: `Assets/Game/Scripts/Logic/FireflyCurve.cs` (namespace `LanternKeeper`, static class)
- Test: `Assets/Game/Tests/EditMode/FireflyCurveTests.cs`

**Interfaces (produces):**
```csharp
public static class FireflyCurve
{
    public const int Motes = 6, MotesLow = 4, Linger = 2, LingerLow = 1;
    public const float StreamTime = 0.5f, LingerEnd = 2.0f, FadeInTime = 1.0f;
    public const float ReduceFlashingFloor = 0.3f, LitThreshold = 0.5f;
    public static float Period(float swarmSeed01, bool low);            // 2.6–3.4 s; ×1.3 on Low
    public static float MoteOffset(int index, int count, float jitter01); // index/count + (jitter01-0.5)*0.06
    public static float Blink(float time, float period, float offset, int count, bool reduceFlashing); // 0..1
    public static int LitCount(float time, float period, float swarmSeed01, int count, bool reduceFlashing);
    public static float Stream01(float sinceCollect);                    // clamp01(t / StreamTime), smoothstep-eased
    public static Vector3 StreamPoint(Vector3 start, Vector3 target, float sideSign, float t01); // quadratic arc
    public static float StreamScale(float t01);                          // 1 until 0.7, eases to 0 at 1
    public static float LingerAlpha(float sinceCollect);                 // 1 until 1.5 s, eases to 0 at LingerEnd
    public static Vector3 LingerOffset(int lingerIndex, float sinceCollect); // radius 0.3 orbit, about 2 rad/s
    public static float FadeIn(int index, int count, float sinceRespawn);   // mote i starts at i*(0.8/count), 0.2 s fade
}
```

**Blink algorithm** (the tests don't fully determine it):
- Cycle phase `u = frac(time / period + offset)`.
- Envelope:
  - rise over `u ∈ [0, 0.1)`;
  - hold for `h`;
  - fall over 0.1;
  - dark for the rest.
- `h = 2.6 / count − 0.1`.
- With Reduce flashing, the rise and fall widths are 0.2 (`h` is reduced so the lit width stays the same) and the result is `max(result, ReduceFlashingFloor)`.
- `LitCount` counts the motes with `Blink ≥ LitThreshold`. Offsets use a jitter seeded deterministically from `swarmSeed01` and the index.

- [ ] **Step 1: Write the failing tests** in `FireflyCurveTests`:
  - `AtLeastTwoLitAlways`: for counts 6 and 4, seeds 0, 0.37 and 0.99, and `time` sampled every 0.01 s over 3 periods, `LitCount ≥ 2`.
  - `BlinkRiseIsAboutPointThreeSeconds`: with period 3, the time from 0 to the peak is 0.3 s ± 0.01.
  - `DarkGapWithinOneToThreeSeconds`: count 6, normal: the dark span (Blink < 0.05) is between 1 and 3 s for periods 2.6 and 3.4.
  - `ReduceFlashingFloor`: `Blink(..., true)` is at least 0.3 at every sample.
  - `StreamReachesTargetAtHalfSecond`:
    - `Stream01(0.5f) == 1`;
    - `StreamPoint(s, t, 1, 1) == t` (distance < 1e-4);
    - `StreamScale(1) == 0`;
    - `StreamScale(0.5f) == 1`.
  - `LingerEndsByTwoSeconds`:
    - `LingerAlpha(2f) == 0`;
    - `LingerAlpha(1f) == 1`;
    - `LingerOffset(0, t).magnitude` is 0.3 ± 0.01.
  - `FadeInDoneByOneSecond`: for every index < count, `FadeIn(i, count, 1f) == 1` and `FadeIn(i, count, 0f) == 0` for i > 0.
  - `PeriodRange`:
    - `Period(0,false) == 2.6f`;
    - `Period(1,false) == 3.4f`;
    - `Period(0,true) == 2.6f*1.3f`.
- [ ] **Step 2:** Run `FireflyCurveTests` alone through the MCP `run_tests` (EditMode, filter `FireflyCurveTests`). Expected: compile failure, because `FireflyCurve` doesn't exist yet.
- [ ] **Step 3:** Implement `FireflyCurve` with the signatures and values above.
- [ ] **Step 4:** Run `FireflyCurveTests`. Expected: all pass. Then run the full EditMode suite. Expected: all pass.
- [ ] **Step 5:** Commit `feat: FireflyCurve timing for blink, stream, linger and fade-in`.

---

### Task 2: Swarm at rest — shader, controller, prefab install

**Files:**
- Create:
  - `Assets/Game/Shaders/FireflyMote.shader` (`LanternKeeper/FireflyMote`)
  - `Assets/Game/Scripts/FireflySwarm.cs`
  - `Assets/Game/Tests/EditMode/FireflySwarmTests.cs`
  - `Assets/Game/Tests/PlayMode/FireflySwarmLiveTest.cs`
- Modify:
  - `Assets/Game/Editor/CreatureInstaller.cs`: add `InstallFirefly()` and call it from `InstallAll()`.
  - `Assets/Game/Scripts/Firefly.cs`: remove the `PickupBurst` lookup and its `ParticleQuality` call; make `SetShown` skip renderers under a `FireflySwarm`; call `swarm.SetShown`.
  - `Assets/Game/Prefabs/Gameplay/Firefly.prefab`, through the installer only.

**Interfaces:**
- Consumes: `FireflyCurve` (Task 1) and `LookPalette.FireflyGreen`.
- Produces:
```csharp
public class FireflySwarm : MonoBehaviour
{
    public const string LowGlobalName = "_LKFireflyLow";
    public static void PublishQuality();          // like MothVisual: 1 on GraphicsLevel.Low
    public int VisibleMotes { get; }              // 6, or 4 on Low
    public Transform[] Motes { get; }             // children "Mote0".."Mote5"
    public float LitFraction { get; }             // CPU mirror of the shader blink, LitCount/VisibleMotes
    public bool Streaming { get; }                // true from Collect until LingerEnd
    public void SetShown(bool shown);             // used by Firefly.SetShown; hides motes when not streaming
    public void Collect(Transform target);        // Task 3
    public void Respawn();                        // Task 3
}
```

**Shader.**
- Additive (`Blend One One`), unlit, `ZWrite Off`, `Cull Off`.
- Camera-facing in the vertex stage.
- `#pragma multi_compile_instancing`.
- No ShadowCaster, depth or DepthNormals passes.
- Instanced properties:
  - `_Phase` (the offset)
  - `_Period`
  - `_Seed`
  - `_Index`
  - `_Stream` (0..1)
  - `_Fade` (0..1)
- Globals: `_LKFireflyLow` and `_LKReduceFlashing`. Reuse the existing reduce-flashing global if one exists, otherwise publish it from `FireflySwarm`.
- Index ≥ 4 renders with zero alpha when `_LKFireflyLow` is 1.
- **Drift:** a per-mote Lissajous offset, amplitude 0.45 m, frequencies 0.2–0.5 Hz from `_Seed`. Multiply it by `(1 − _Stream)`.
- **Blink:** the same envelope as `FireflyCurve.Blink`, held at 1 when `_Stream > 0`.
- **Colour:** core `#FFFFFF`, blending out to `#B8FFB0` at the halo edge. The core radius is 0.2 of the quad, and the quad is 0.15 m.
- Mote renderers get large `localBounds` (3 m), so drift and stream are never culled.

**Controller.**
- `Awake` assigns each mote a `MaterialPropertyBlock` with values from `FireflyCurve`, seeded from the instance's position hash. It publishes quality on enable and on `GraphicsQuality` change, like `MothVisual`.
- `Update` sets the sibling `Glow` light intensity to `baseIntensity × lerp(0.6, 1.0, smoothed LitFraction)`, smoothed with a 0.25 s exponential. `baseIntensity` is read once from the light.
- It must not fight `LightQuality`: multiply against the intensity `LightQuality` set, cached at enable.

**Installer `InstallFirefly()`.**
- Opens the Gameplay prefab and removes the `Body` and `PickupBurst` children.
- Adds a `Swarm` child at local zero holding `FireflySwarm` and six `MoteN` quads:
  - the built-in Quad mesh;
  - material `Assets/Game/Materials/Generated/FireflyMote.mat` with `enableInstancing = true`;
  - `ShadowCastingMode.Off`;
  - `receiveShadows` false.
- Keeps `Glow`, the collider and `Firefly`.
- Returns early, without saving, when already installed: the swarm exists, there are 6 motes and the material is correct.
- Makes no changes to scenes.

- [ ] **Step 1: Write the failing EditMode tests** (`FireflySwarmTests`, loading the Gameplay prefab):
  - `PrefabHasSwarmAndNoPlaceholder`:
    - a `FireflySwarm` child exists;
    - `Motes.Length == 6`;
    - there is no `Body` and no `PickupBurst` child;
    - the `Glow` light and `Firefly` are still present.
  - `MoteColourIsFireflyGreen`: the material's `_Color` equals `LookPalette.FromHex(LookPalette.FireflyGreen)` exactly.
  - `MoteMaterialInstancedNoShadows`:
    - `enableInstancing`;
    - every mote has shadows off;
    - the shader has no `ShadowCaster` pass (`material.FindPass("ShadowCaster") == -1`).
  - `InstallFireflyIsIdempotent`: run `InstallFirefly()` twice; the prefab file bytes are identical after the second run.
- [ ] **Step 2:** Run the tests. Expected: they fail.
- [ ] **Step 3:** Implement the shader, `FireflySwarm`, the `Firefly.cs` edits and `InstallFirefly()`. Run the installer through the MCP.
- [ ] **Step 4:** Write the PlayMode `FireflySwarmLiveTest` on Island1:
  - `SwarmBlinksAndLightBreathes`: over 3 s, `LitFraction` takes at least 2 distinct values, and the Glow intensity varies but stays within 0.6–1.0 of the base.
  - `LowPresetHidesExtraMotes`:
    - switch `GraphicsQuality` to Low at runtime;
    - `VisibleMotes == 4`;
    - the global `_LKFireflyLow == 1`;
    - restore the preset afterwards.
- [ ] **Step 5:** Run the new classes alone, then the full EditMode and PlayMode suites. Expected: all pass.
- [ ] **Step 6:** Capture 1080p shots to `.superpowers/sdd/2026-10-08-firefly-f3/shots/task2/`:
  - swarm close-up on Island1, Ultra and Low;
  - gameplay distance on Island1 and Island4.
- [ ] **Step 7:** Revert the noise and commit `feat: firefly swarm visuals at rest`.

---

### Task 3: The collect moment

**Files:**
- Modify:
  - `Assets/Game/Scripts/FireflySwarm.cs`: implement `Collect` and `Respawn`.
  - `Assets/Game/Scripts/Firefly.cs`: in `OnTriggerEnter`, after `AddFuel` and before `SetShown(false)`, call `swarm.Collect(flame.GlowPoint)`. In `RespawnLater`, after moving to the new home, call `swarm.Respawn()`. Nothing else changes.
  - `Assets/Game/Scripts/LanternFlame.cs`:
    - add `public Transform GlowPoint` (the flame transform, or `transform`);
    - add `public void Kick(float strength)`: a boost that decays linearly over 0.3 s, multiplying the glass alpha by `(1 + 0.8k)` and the flame height by `(1 + 0.15k)`. Visual only.
  - `Assets/Game/Scripts/ProceduralAudio.cs`: add `public static AudioClip FireflyArrive()`, a soft high bell of about 0.4 s, cached like the other clips.
  - `Assets/Game/Scripts/AudioManager.cs`: add `public void PlayFireflyArrive(Vector3 position)` at volume 0.5.
- Test: `Assets/Game/Tests/PlayMode/FireflyCollectTest.cs`

**Interfaces:**
- Consumes:
  - `FireflyCurve.Stream01`
  - `FireflyCurve.StreamPoint`
  - `FireflyCurve.StreamScale`
  - `FireflyCurve.LingerAlpha`
  - `FireflyCurve.LingerOffset`
  - `FireflyCurve.FadeIn`
  - `FireflySwarm` from Task 2
- Produces:
  - `LanternFlame.Kick(float)`
  - `LanternFlame.GlowPoint`
  - `AudioManager.PlayFireflyArrive(Vector3)`

**Behaviour.**
- **`Collect(target)`:**
  - Records each mote's current world position, including the drift, which is mirrored on the CPU from the same formula. Sets `_Stream` to 1 on every mote.
  - Detaches nothing: the swarm stays parented, and positions are set in world space each frame.
  - Each frame for t < 0.5 s, it moves the motes along `StreamPoint(start, target.position, ±1, Stream01(t))` with scale `StreamScale`.
  - At t ≥ 0.5 s:
    - streaming motes hide;
    - lingering motes (0 and 1, or just 0 on Low) follow `target.position + LingerOffset` with alpha `LingerAlpha`, which is applied through `_Fade`;
    - it calls `flame.Kick(reduceFlashing ? 0.5f : 1f)` and `PlayFireflyArrive` once.
  - At t ≥ 2 s it hides all motes and sets `Streaming` to false.
  - If the target is destroyed, the motes freeze at their last position and finish the fade with no exception.
- **`Respawn()`:**
  - Resets the motes to local drift positions, with `_Stream` 0.
  - Runs `FadeIn` for 1 s.
  - Has no effect on fuel or the collider.
- **Timing.** Scaled time, so pause freezes the stream. Fuel was already added.

- [ ] **Step 1: Write the failing PlayMode tests** (`FireflyCollectTest`, Island1, intro keys marked seen as in `BeaconReachabilityTest`):
  - `CollectAddsFuelSameFrame`: set the lantern's fuel to 30% first, so the add is never clamped at max. The fuel delta is the firefly's `refillAmount` (20) on the trigger frame.
  - `MotesReachLanternByHalfSecond`: at 0.55 s, the streaming motes are hidden, and every lingering mote is within 0.4 m of `GlowPoint`.
  - `StreamFollowsMovingLantern`: move the lantern 3 m during the stream; at 0.55 s the lingering motes are within 0.4 m of the new position.
  - `HiddenUntilRespawn`:
    - after 2.1 s, no mote renderer is enabled;
    - the collider is disabled;
    - after `Respawn()` plus 1.05 s, all visible motes are shown.
  - `LingerSurvivesLanternDestroyed`: destroy the lantern GameObject at 0.6 s; no exception is logged through 2.2 s.
  - `TwoCollectsStreamIndependently`: start at 30% fuel, then collect two fireflies one frame apart. Fuel rises by 2 × refill, and both swarms report `Streaming`.
  - `KickHalvedWithReduceFlashing`: with Reduce flashing on, the boost peak is 0.5; with it off, the peak is 1. Read through a `LanternFlame.KickLevel` getter.
- [ ] **Step 2:** Run the tests. Expected: they fail.
- [ ] **Step 3:** Implement the behaviour above.
- [ ] **Step 4:** Run `FireflyCollectTest` alone, then the full EditMode and PlayMode suites. Expected: all pass.
- [ ] **Step 5:** Capture to `shots/task3/`:
  - mid-stream at about 0.25 s;
  - linger at about 1 s;
  - after respawn.

  Capture on Island1 Ultra. Add one Low mid-stream shot.
- [ ] **Step 6:** Commit `feat: firefly collect stream, linger and lantern kick`.
- [ ] **Step 7: USER GATE (midway).** The controller shows the Task 2 and Task 3 shots and waits for approval before Task 4.

---

### Task 4: Validation and leftovers

**Files:**
- Modify:
  - `Assets/Game/Editor/SceneWiring.cs`: firefly checks.
  - The F1 shaders: `MothWing`, `Shade`, `BeaconGlass`, `BeaconIron`.
  - `Assets/Game/Scripts/BeaconVisual.cs:151` and `Assets/Game/Editor/CreatureInstaller.cs:489`, for the cairn.
  - `GraphicsMenu.cs`, `Hud/FpsReadout.cs` and `Logic/SettingsMath.cs`, for the FPS key.
  - `CreatureInstaller.InstallShade` (~674-720).
- Delete, each only if a grep of Assets, ProjectSettings and the tests finds no reference:
  - `Assets/Game/Scenes/LanternKeeper.unity` (+ .meta)
  - `Assets/Game/Prefabs/Firefly.prefab` (+ .meta)

**Interfaces (produces):** `SettingsMath.FpsPrefsKey = "LanternKeeperFps"`. `GraphicsMenu.FpsKey` and `FpsReadout.FpsKey` are removed or delegate to it, and the existing `FpsPrefsKeyMatchesGraphicsMenu` test is updated to match.

- [ ] **Step 1: Validation.** Add to Validate Scene Wiring, for each `Firefly` in an island scene, with these exact messages:
  - `"<scene> firefly '<name>' missing swarm visual"`
  - `"<scene> firefly '<name>' still has placeholder '<child>'"` (for `Body` or `PickupBurst`)

  Confirm that the check fires on a scratch copy of Island1 with the swarm removed. Delete the copy afterwards.
- [ ] **Step 2: Dead ShadowCaster passes.**
  - A pass is dead only if every renderer using that shader, across the prefabs and scenes, has `ShadowCastingMode.Off`. Remove only those passes, and list each decision in the report.
  - Move any HLSL function duplicated across `MothWing`, `Shade`, `BeaconGlass` and `BeaconIron` into the existing `MoonRimCommon.hlsl`, or a new `CreatureCommon.hlsl`. This includes the moon-rim colour constant.
  - There must be no visual change: compare before and after captures of the moth, Shade and beacon at a fixed `Time` override, and note any pixel difference.
- [ ] **Step 3: Cairn.** Match the cairn by a component or serialized reference instead of `name.StartsWith("BeaconCairn")`. Prefer a serialized `cairnRenderer` field on `BeaconVisual`, set by `InstallBeacon`. Re-running `InstallBeacon` must change only that field. `BeaconVisualTests` must still pass.
- [ ] **Step 4: FPS key.** Implement the single constant, then run `FpsPrefsKeyMatchesGraphicsMenu` and the HUD tests.
- [ ] **Step 5: `InstallShade`.** Only call `SetColor`, `SetDirty` and `SaveAssets` on materials when a value differs. Verify by running it twice: `git status` must show no `.mat` changes.
- [ ] **Step 6: Deletions.** Delete the stale scene and prefab, each only if unreferenced; report any references found instead of deleting.
- [ ] **Step 7:** Run the full EditMode and PlayMode suites and Validate Scene Wiring. Expected: all tests pass, and Validate reports 0 problems.
- [ ] **Step 8:** Commit `chore: firefly validation and F1/F2 leftovers`.

---

### Task 5: After set, comparison, perf, release, user gate
- [ ] **Step 1:** Run `LookBaselineCapture.Run(<abs>/docs/look/baseline/2026-10-08-f3-after)`. If there is no `2026-10-08-f3-before` set, reuse `2026-10-08-f2-after` as "before".
- [ ] **Step 2:** Write `docs/look/compare/phase-f3.html` in the `phase-f2.html` format. Include the Task 2 and Task 3 shots as JPEG q90 in `docs/look/compare/phase-f3/`.
- [ ] **Step 3: Perf.**
  - One player build, then one probe per island on Low and Ultra, using the method in `docs/look/perf/2026-10-08-phase-f2.md`.
  - Write `docs/look/perf/2026-10-08-phase-f3.md` against F2.
  - If any result is above +0.3 ms, stop and report.
- [ ] **Step 4: Release build.**
  - Build `Builds/PhaseF3_Release/LanternKeeper.exe`, non-development.
  - Smoke-test it: run about 12 s with `-logFile`, then grep for "shader is null", "NullReference" and "Exception". Expect none, apart from the DepthOfField, Bokeh and Panini stripped notices.
- [ ] **Step 5:** Commit `docs: Phase F3 after set, comparison and perf`.
- [ ] **Step 6: USER GATE.** The user plays the build and approves before merge.
