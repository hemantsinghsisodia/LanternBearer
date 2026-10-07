# Phase F1: Moths, Shades and Beacons — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the placeholder moth and the bare-pole beacon with Blender-built models, give Shades a smoke-wraith shader, and add the full beacon lighting moment. Gameplay does not change.

**Architecture:**
- Blender 5.2 Python build scripts in `ArtSource/Blender/` produce FBX files at true scale.
- Hand-written URP shaders provide the look.
- Small visual controllers read the existing gameplay state (`Moth`/`MothDrainFx`, `Shade.State`, `Beacon.IsLit`/`Beacon.Lit`, `Lightning.CurrentFlash`). They never write gameplay state.
- Idempotent editor installers swap only the visual children of `Moth.prefab`, `Shade.prefab` and `Beacon.prefab`.
- Timing curves are pure Logic code with tests.

**Tech Stack:** Unity 6 (6000.6.3f1), URP Deferred, HLSL shaders, ParticleSystem, Blender 5.2 (headless CLI: `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -P script.py`), NUnit EditMode/PlayMode, Unity MCP.

**Spec:** `docs/superpowers/specs/2026-10-07-creatures-beacons-f1-design.md` (parent: `2026-10-03-art-direction-design.md`).

## Global Constraints

**Colours and look**
- Signal colours are exact and never reused:
  - drain violet `#B48CFF`
  - lightning pale blue `#BCD2FF`
  - lantern amber `#FFB15C`, core `#FFD38A`
- Use `LookPalette` constants. The warm/cold rule applies to lights only.

**Gameplay unchanged**
- No change to `Moth.cs`/`Shade.cs`/`Beacon.cs` gameplay logic, spawners, radii (beacon safe radius 8 m), colliders, or `ShadeLogic`.
- A beacon is safe on the same frame `Beacon.Lit` fires.
- Visual code only reads gameplay state.

**Scale**
- 1 unit = 1 m.
- Moth wingspan about 0.25 m, 300–500 triangles.
- Beacon about 3.2 m tall: cairn about 1.2 m tall × 1.4 m wide, lantern about 0.9 m, 3–5k triangles plus a distance LOD.

**Shaders**
- Hand-written URP: forward, depth and depth-normals passes, plus shadow where the object casts one.
- GPU instancing on.
- The Low path uses a uniform branch or global float. Add no new `multi_compile` keywords.

**Low preset**
- Every effect has a Low variant: fewer particles, shorter beams and trails, cheaper flap.
- Readability rule 4: nothing gameplay needs may be Ultra-only.

**World content**
- Terrain heights, alphamaps, holes and details must be hash-identical to the base.
- No full `IslandBuilder` level rebuilds. Use installers (the pattern of `UIBuilder.InstallPause` and `InstallMainMenuInActiveScene`).

**Assets**
- Blender scripts are committed under `ArtSource/Blender/`. That folder must not be git-ignored. Check `.gitignore`; only `ArtSource/Nature/` is ignored today.
- Generated `.blend` files may stay untracked.
- FBX and textures go under `Assets/Game/Art/Creatures/Moth/` and `Assets/Game/Art/Beacons/`.
- Download nothing.

**Unity process**
- Never revert, edit or commit `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`.
- Revert noise: TMP font assets, `ShadeBody.mat`/`Smoke.mat`, `ProjectSettings/*`, Look volumes, and scenes or prefabs that only re-serialised. Check with `git diff --ignore-space-at-eol --stat`.
- Never `git checkout` the whole Scenes folder.
- Delete `Assets/Resources/PerformanceTestRun*` if it appears.
- Restore the `LanternKeeperGraphics` pref to 0 afterwards.
- EditMode tests that build objects use `DestroyImmediate` plus `Undo.ClearAll()` in teardown. Run new classes alone first. If Unity crashes, stop.
- Screenshots go to `.superpowers/sdd/2026-10-07-creatures-beacons-f1/shots/`, as 1920×1080 RT renders in play mode.
- If the MCP drops for more than about 2 minutes, or a git action is denied, stop and report.

**Performance**
- Run only in Task 6: one probe run per island, Low and Ultra, against `docs/look/perf/2026-10-06-phase-e.md`. The limit is +0.3 ms average.
- If it fails, stop and report the numbers. Do no A/B investigation without the user's go-ahead.

**Model split (AGENTS.md):** implementers and reviewers run on Sonnet.

**Test access:** Assembly-CSharp types are reached by reflection (`Type.GetType("LanternKeeper.X, Assembly-CSharp")`). Pure curves live in the Logic assembly.

## Review Focus
1. **Draining stops, or a moth dies mid-drain.** The violet aura and dust must fade out and stop emitting, never stay stuck on. Test `MothVisualTests.AuraFadesWhenDrainStops` (Task 2).
2. **A beacon lit twice or reloaded already lit** (for example a scene restart or a lit-state restore). The lighting moment must not replay. The beacon must show the steady lit state with the ring at full 8 m. Test `BeaconLightMomentTest.AlreadyLitShowsSteadyState` (Task 3).
3. **Low preset.** Moths still flap, Shade hems still read, the beacon is still clearly lit with its ring visible. Effects are reduced, never removed. Test `LowPresetVisualTests` (Task 5).
4. **Lightning with Reduce flashing on.** The Shade's pale-blue outline scales with the capped `Lightning.CurrentFlash`, so it never exceeds the cap. Test `ShadeVisualTests.OutlineFollowsCappedFlash` (Task 4).
5. **Shade frozen, then released.** The burn-edge flicker stops and the edges return to normal. No erosion state may leak. Test `ShadeVisualTests.FreezeBurnClearsOnRelease` (Task 4).

---

### Task 1: Before capture, colour constants and timing curves

**Files:**
- Create: `Assets/Game/Scripts/Logic/BeaconLightCurve.cs`, `Assets/Game/Scripts/Logic/MothFlap.cs`
- Modify: `Assets/Game/Scripts/Logic/LookPalette.cs` (add `DrainVioletAura` only if it differs; otherwise reuse `DrainViolet`)
- Test: `Assets/Game/Tests/EditMode/CreatureCurveTests.cs`
- Capture: `docs/look/baseline/2026-10-07-f1-before/`

**Interfaces:**
- `BeaconLightCurve` (static):
  - `float Flare(float t)`: peaks in 0–0.15 s, 0 after 0.3 s.
  - `float Intensity01(float t)`: smooth ramp 0 → 1 over 0–1 s, clamped at 1.
  - `float RingRadius(float t, float maxRadius)`: 0 before 0.3 s, eases to `maxRadius` by 1.2 s.
  - `float EmberRate01(float t)`: positive only in 0.1–1.5 s.
  - `const float Duration = 1.5f`.
- `MothFlap` (static):
  - `float Angle(float time, float phase, float hz, float glide01)`: returns the wing angle in degrees, in [−55, 55].
  - `float GlideFactor(float time, float phase)`: 0..1, about 1 s of glide every 3–6 s, deterministic per phase.
  - `const float MinHz = 14f`, `MaxHz = 20f`.

- [ ] **Step 1:** Run `LookBaselineCapture.Run("docs/look/baseline/2026-10-07-f1-before")` and open the moth, Shade and beacon shots.
- [ ] **Step 2: Write the failing tests** in `CreatureCurveTests`:
  - `IntensityReachesFullBy1s`: `Intensity01(1f) == 1` and `Intensity01(0f) == 0`; it is monotonic.
  - `RingStartsAt0_3AndReachesMaxBy1_2`.
  - `FlareIsEarlyOnly`: `Flare(0.08) > 0.8` and `Flare(0.5) == 0`.
  - `EmbersWindow`.
  - `FlapAngleBounded`: |angle| ≤ 55 over 10 s for many phases.
  - `FlapRateInRange`.
  - `SignalColoursExact`: `DrainViolet == #B48CFF`, `LightningBlue == #BCD2FF`, `LanternAmber == #FFB15C`, `GlowCore == #FFD38A`.
- [ ] **Step 3:** Run them. Expected: FAIL.
- [ ] **Step 4:** Implement `BeaconLightCurve` and `MothFlap`.
- [ ] **Step 5:** Run the full EditMode suite. Expected: PASS.
- [ ] **Step 6:** Commit `feat: F1 before capture, beacon light and moth flap curves`.

### Task 2: Moth model, wing shader and drain visuals

**Files:**
- Create:
  - `ArtSource/Blender/moth_build.py`
  - `Assets/Game/Art/Creatures/Moth/Moth.fbx` (+ textures)
  - `Assets/Game/Shaders/MothWing.shader`
  - `Assets/Game/Scripts/MothVisual.cs`
  - `Assets/Game/Editor/CreatureInstaller.cs` (menu: **Lantern Keeper → Install Creature Visuals**)
- Modify:
  - `Assets/Game/Scripts/MothDrainFx.cs`: the white halo becomes a violet aura plus pale wing dust. Keep its public API: `SetDraining`, `Amount`, `TrailEnabled`.
  - `Assets/Game/Prefabs/Gameplay/Moth.prefab`: visual child only, via the installer.
- Test: `Assets/Game/Tests/EditMode/MothVisualTests.cs`

**Interfaces:**
- **Consumes:** `MothFlap` (Task 1), `LookPalette.DrainViolet`.
- **Produces:**
  - `MothVisual : MonoBehaviour` sets per-instance `_Phase` and `_FlapHz` through a `MaterialPropertyBlock`, and reads the Low global.
  - `CreatureInstaller.InstallMoth()`, idempotent.

**Model:**
- Body: a tapered capsule plus a fuzz shell, dark brown-grey.
- Two pairs of wings: thin double-sided quads. The script paints a pale dusty texture with faint eye-spots.
- Wing pivots sit at the body axis.
- Vertex colour R marks the wing span (0 at the root, 1 at the tip) for the shader.

**MothWing shader:**
- Rotates wing vertices about the body axis by `MothFlap`-equivalent HLSL using `_Phase`/`_FlapHz` and `_Time`.
- Lit, with wrap lighting so the lantern warms the wings.
- MoonRim-compatible rim.
- Low uses a simpler sine with no glide. Moths still flap.

**Drain FX:**
- A violet aura fades in and out over 0.2 s.
- Falling wing-dust particles: 12/s, or 5/s on Low.
- The aura and dust stop when draining stops or the moth is destroyed.

- [ ] **Step 1:** Write `moth_build.py`. Run it headless and confirm that `Moth.fbx` exports at true scale with a wingspan of 0.24–0.26 m and 300–500 triangles. Print the bounds and triangle count from the script.
- [ ] **Step 2: Write the failing tests** in `MothVisualTests`:
  - `PrefabUsesNewMoth`: the Moth prefab visual child has the `MothVisual` component and the `LanternKeeper/MothWing` material, and no legacy sphere mesh.
  - `AuraIsDrainViolet`.
  - `AuraFadesWhenDrainStops`: `SetDraining(true)`, tick, `SetDraining(false)`, tick 0.3 s; then `Amount == 0` and the dust emission is off.
  - `MothCollidersUnchanged`: the collider type, size and centre equal the base prefab's.
- [ ] **Step 3:** Run them. Expected: FAIL.
- [ ] **Step 4:** Implement the shader, `MothVisual`, the `MothDrainFx` changes and `InstallMoth`. Run the installer.
- [ ] **Step 5:**
  - Run the tests and the full EditMode suite (PASS), and the full PlayMode suite (PASS).
  - Capture `moth-closeup-ultra.png`, `moth-gameplay-ultra.png`, `moth-drain-ultra.png` and `moth-low.png`, and open them.
- [ ] **Step 6:** Commit `feat: modelled moth with shader-flapped wings and violet drain aura`.

### Task 3: Beacon model, glass and beams, and the lighting moment, then the midway user gate

**Files:**
- Create:
  - `ArtSource/Blender/beacon_build.py`
  - `Assets/Game/Art/Beacons/Beacon.fbx` (+ LOD1, textures)
  - `Assets/Game/Shaders/BeaconGlass.shader`, `Assets/Game/Shaders/BeaconBeam.shader`
  - `Assets/Game/Scripts/BeaconVisual.cs`
- Modify:
  - `Assets/Game/Scripts/BeaconSafeRing.cs`: restyle to a warm glowing ground ring, and add a bloom radius driven by `BeaconVisual`.
  - `Assets/Game/Scripts/ProceduralAudio.cs`: add `AudioClip Whoomp()`.
  - `CreatureInstaller.cs`: add `InstallBeacon()`.
  - `Beacon.prefab`: visual child only.
- Test: `Assets/Game/Tests/PlayMode/BeaconLightMomentTest.cs`, `Assets/Game/Tests/EditMode/BeaconVisualTests.cs`

**Interfaces:**
- **Consumes:** `BeaconLightCurve` (Task 1), `Beacon.Lit` (static `Action<Beacon>`), `Beacon.IsLit`, `Beacon.ZoneRadius`, the island `LookProfile.rockTint`.
- **Produces:**
  - `BeaconVisual : MonoBehaviour`:
    - `void PlayLightMoment()`
    - `void ShowSteadyLit()`
    - `void ShowUnlit()`
    - `float MomentTime` (for tests)
  - `BeaconVisual` subscribes to `Beacon.Lit` for its own beacon.
  - In `OnEnable`, if `IsLit` is already true, it calls `ShowSteadyLit()` with no replay.

**Model:**
- A dry-stone cairn of stacked rock blocks, tinted with `rockTint` through the material colour per island.
- An iron post and bracket.
- A storm lantern: square forged frame, 4 pane quads (a separate material slot for the glass), a peaked roof cap and a ring.
- The collider is unchanged from the current Beacon prefab.

**Unlit state:**
- Dark glass.
- The roof cap gets a MoonRim edge.
- No glow.

**Lit state:**
- Pane emission amber with a 0.6–1.0 flicker.
- 4 additive beam cards faded by view angle. On Low the beams are 50% length and 60% intensity.
- A warm point light. Its intensity is the existing beacon light value × `Intensity01`.
- The ring at the full `ZoneRadius`.

**Moment:**
- Driven by `BeaconLightCurve` for `Duration` in unscaled-independent game time.
- Embers: a ParticleSystem bursting 45, or 15 on Low.
- `Whoomp()` plays once on the SFX mixer group.
- A bloom kick through a short exposure bump only when post-processing is on. It must not disturb `UserSettingsApplier`'s brightness.

- [ ] **Step 1:** Write `beacon_build.py`. Run it headless, check the bounds (height 3.1–3.3 m) and triangle counts (LOD0 3–5k), and export.
- [ ] **Step 2: Write the failing tests:**
  - `BeaconVisualTests` (EditMode):
    - `PrefabUsesNewBeacon`
    - `ColliderUnchanged`: same type, size and centre as the base
    - `LitColoursAreAmber`
    - `NoLegacyPoleMesh`
  - `BeaconLightMomentTest` (PlayMode, Island1):
    - `SafeOnSameFrameAsLit`: in the frame `Beacon.Lit` fires, `SafeRadius == 8`
    - `LightFullBy1_2s`
    - `RingReaches8mBy1_2s`
    - `AlreadyLitShowsSteadyState`: re-enable a lit beacon; no moment replays, and the ring is at 8 m immediately
- [ ] **Step 3:** Run them. Expected: FAIL.
- [ ] **Step 4:** Implement the shaders, `BeaconVisual`, the safe-ring restyle, `Whoomp` and `InstallBeacon`. Run the installer.
- [ ] **Step 5:**
  - Run the tests, the full EditMode suite and the full PlayMode suite (including `BeaconReachabilityTest` unchanged). All PASS.
  - Capture `beacon-unlit-ultra.png`, `beacon-lit-ultra.png`, `beacon-moment-0_2s.png`, `beacon-moment-0_8s.png`, `beacon-lit-low.png` and `beacon-closeup-ultra.png`, and open them.
- [ ] **Step 6:** Commit `feat: storm-lantern beacon on a cairn with the full lighting moment`.
- [ ] **Step 7: USER GATE (midway).** The controller shows the moth and beacon captures and waits for approval before Task 4.

### Task 4: Shade shader, smoke trail, lightning outline and freeze burn

**Files:**
- Create: `Assets/Game/Shaders/Shade.shader`, `Assets/Game/Scripts/ShadeVisual.cs`
- Modify:
  - `CreatureInstaller.cs`: add `InstallShade()`.
  - `Shade.prefab`: materials and visual child only. The existing hollow-wraith mesh is kept.
- Test: `Assets/Game/Tests/EditMode/ShadeVisualTests.cs`

**Interfaces:**
- **Consumes:** `Shade.State` (`ShadeState.Chase/Creep/Freeze/Retreat/Reforming/Stunned`), `Lightning.CurrentFlash` (already capped by Reduce flashing), the wind globals, `LookPalette.LightningBlue`.
- **Produces:** `ShadeVisual : MonoBehaviour`.
  - Each frame it sets `_Outline = Lightning.CurrentFlash`.
  - It sets `_Burn` from 0 to 1 while in the `Freeze` state, easing back to 0 over 0.3 s after release.
  - It sets `_Speed` from the Shade's velocity.
  - It drives the smoke-trail emission by speed.

**Shader:**
- Ink-dark body, near-opaque, tinted by the island sky hue, with a dim MoonRim rim.
- Hem breakup: the bottom 30% by object-space height uses animated noise and dithered alpha-cutout.
- Tatter sway: a vertex wave grows toward the hem, driven by wind and `_Speed`.
- Two soft pinprick eyes at mesh-UV or vertex-colour marked spots: cool pale, slight flicker, small.
- `_Outline`: a pale-blue `#BCD2FF` rim outline, multiplied by `_Outline`.
- `_Burn`: edge erosion speeds up with an ember-tinted edge flicker. This is not amber, because amber means safe warmth; use a pale ash-orange that is desaturated and dim. Check it against the signal-colour rule and note the chosen hex in the report.
- Low: simpler noise, a shorter trail, and the same outline.

- [ ] **Step 1: Write the failing tests:**
  - `PrefabUsesShadeShader`
  - `OutlineFollowsCappedFlash`: with Reduce flashing on and a forced flash, `_Outline ≤ FlashCap(true)`
  - `FreezeBurnClearsOnRelease`: Freeze → 1; release → reaches 0 within 0.35 s
  - `OutlineColourIsLightningBlue`
  - `ShadeColliderAndLogicUnchanged`
- [ ] **Step 2:** Run them. Expected: FAIL.
- [ ] **Step 3:** Implement the shader, `ShadeVisual` and `InstallShade`. Run the installer.
- [ ] **Step 4:**
  - Run the tests and the full EditMode and PlayMode suites. All PASS.
  - Capture `shade-ultra.png` and `shade-lightning-ultra.png` (Island 4, forced flash), `shade-frozen-ultra.png` and `shade-low.png`, and open them.
- [ ] **Step 5:** Commit `feat: smoke-wraith Shade shader with lightning outline and freeze burn`.

### Task 5: Rollout to every island, Low-preset checks and validation

**Files:**
- Modify:
  - `Assets/Game/Editor/SceneWiring.cs`: new checks.
  - `CreatureInstaller.cs`: `InstallAllIslands()`.
  - Island scenes: only through the installer, and only if the prefabs are not already referenced.
- Test: `Assets/Game/Tests/PlayMode/LowPresetVisualTests.cs`

**Validation messages:**
- `"<scene> moth uses legacy visual"`
- `"<scene> beacon uses legacy visual"`
- `"<scene> shade missing ShadeVisual"`
- `"<scene> references legacy moth glow sprite"`

- [ ] **Step 1: Write the failing test.** `LowPresetVisualTests` covers Low on Island 4. A moth's wing angle changes over frames, the Shade material's hem breakup is active, and a lit beacon's pane emission is greater than 0 and its ring radius equals 8.
- [ ] **Step 2:** Run it. Expected: FAIL if anything is Ultra-only, otherwise PASS. Fix any failures.
- [ ] **Step 3:**
  - Add the validation messages, run `InstallAllIslands()`, then run Validate on MainMenu and Islands 1–4. Expected: 0 problems.
  - Hash-check the terrain: identical to the base.
- [ ] **Step 4:** Run the full EditMode and PlayMode suites. Expected: PASS.
- [ ] **Step 5:** Commit `feat: F1 visuals on every island, Low-preset checks and validation`.

### Task 6: After set, comparison, performance and build, then the final user gate

- [ ] **Step 1:** Run `LookBaselineCapture.Run("docs/look/baseline/2026-10-07-f1-after")` and view the moth, Shade and beacon shots on Low and Ultra.
- [ ] **Step 2:** Write `docs/look/compare/phase-f1.html` (before vs after, in the format of `phase-e.html`). Include the Task 2–4 close-ups, copied into `docs/look/compare/phase-f1/`.
- [ ] **Step 3: Performance (single pass only).**
  - Make one player build and run the probe once per island on Low and Ultra.
  - Write `docs/look/perf/2026-10-07-phase-f1.md` against Phase E.
  - Expected: within +0.3 ms. If it fails, stop and report the numbers. Do not investigate without the user's go-ahead.
- [ ] **Step 4: Release build.**
  - Build the non-development release to `Builds/PhaseF1_Release/LanternKeeper.exe`.
  - Smoke test: run it about 12 s with `-logFile`, then grep for "shader is null", "NullReference" and "Exception". Expected: none, apart from the known DepthOfField/Panini notices.
- [ ] **Step 5:** Commit `docs: Phase F1 after set, comparison and perf`.
- [ ] **Step 6: USER GATE.** The user plays the build and approves before merge.
