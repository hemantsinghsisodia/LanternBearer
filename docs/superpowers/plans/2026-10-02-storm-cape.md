# Storm Cape (Island 4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Island 4, "The Storm Cape", with three new systems — Wind, the Shade and Lightning — switched on per level and covered by EditMode tests.

**Architecture:**
- Each system pairs a pure logic class with a thin `MonoBehaviour`.
  - The logic classes live in a new `LanternKeeper.Logic` assembly with no project dependencies, so the EditMode test assembly can reference it.
  - The `MonoBehaviour`s stay in `Assembly-CSharp`. Predefined assemblies auto-reference asmdefs, so they can use the logic classes.
- `IslandBuilder` adds each component only when its `LevelConfig` field enables it. This mirrors `CreateTide`.

**Tech Stack:** Unity 6000.6.3f1, URP, C#, Input System, TextMeshPro, Unity Test Framework 1.8.0 (NUnit), Unity MCP.

**Spec:** `docs/superpowers/specs/2026-10-02-storm-cape-design.md` (read it alongside this plan).

## Global Constraints
- Code style: `namespace LanternKeeper`, Allman braces, `[SerializeField]` private fields, explicit `if`/`return` style as in `Scripts/GameSettings.cs`. Match the surrounding comment density.
- Islands 1–3 must build identically. Every new `LevelConfig` field defaults to off: `windStrength = 0`, `shadeCount = 0`, `lightning = false`. The builder must make **no extra `System.Random` calls** when a system is off.
- Difficulty values are verbatim from the spec:

  | | Easy | Normal | Hard |
  |---|---|---|---|
  | Gust strength | 0.7× | 1× | 1.25× |
  | Max Shades | 3 | 5 | 6 |
  | Shade fuel steal | 10 | 15 | 20 |
  | Lightning interval | 30–45 s | 35–55 s | 45–65 s |

- Wind timing:
  - Calm 12–20 s (shorter on Hard). Warning 2 s. Gust 3–4 s, with a 0.5 s ramp up and a 0.5 s ramp down.
  - Peak push 2.5 m/s × the difficulty multiplier. 0.5× while airborne, 0.2× while sheltered (raycast ≤ 4 m upwind).
  - Gust direction is the prevailing direction ±60°.
- Shade:
  - Thresholds on the reveal value `r`: chase below 0.15 at 4.5 m/s; creep from 0.15 to below 0.5 at 0.8 m/s; freeze at 0.5 and above, then retreat after 1.5 s frozen at 3 m/s.
  - Touch radius 1.0 m. Re-forms 8 s after a touch. Stunned for 3 s by a flash. Hovers 0.2 m above the ground.
  - Spawns 2 at the start, +1 per 3 beacons lit, up to the cap, at least 25 m from the player.
- Lightning: 1.5 s thunder lead, 0.3 s flash, 0.5 s afterglow. Never during a wind warning.
- Island 4 data:
  - `island4` / `Island4`, radius 42, hill 14, seed 4404.
  - 9 beacons, 18 fireflies, 5 moths, 3 hidden paths, `hasCliff` true, tide 0.
  - Fog (0.32, 0.36, 0.42), density 0.016. Biome `Levels/Biomes/Heath.asset`.
  - `windStrength = 1`, `shadeCount = 2`, `lightning = true`. 9 log pages.
  - `Island3.nextLevelScene = "Island4"`.
- **Implementation model:** Claude Sonnet 5.5 High subagents (see `AGENTS.md`).
- **Unity environment:**
  - Run with the Unity editor open and the Unity MCP connected (`.mcp.json`). Never use batchmode while the editor is open.
  - Run tests with the Unity MCP test-runner tool (EditMode or PlayMode), or with **Window → General → Test Runner**.
  - Read compile errors from the MCP console tool or from `%LOCALAPPDATA%\Unity\Editor\Editor.log`.

## Review Focus
1. **Walking into a gust on the worst difficulty:** the player must still make headway. Peak push on Hard is 3.125 m/s, below the 3.5 m/s walk speed. Pinned by `WindTests.PeakPushBelowWalkSpeedOnAllDifficulties` (Task 3).
2. **Shade touch when fuel is below the steal amount:** fuel goes to exactly 0, the normal lose sequence runs once, and fuel is never negative. Pinned by `ShadeTests.StealNeverExceedsFuel` (Task 5).
3. **Respawning after a fall with a Shade next to the respawn point:** no instant second penalty. A Shade cannot steal within 2 s of a water rescue. Pinned by `ShadeTests.NoStealDuringRescueGrace` (Task 5).
4. **Pause, death sequence, rescue, or standing in a safe ring during a gust:** zero push, and timers don't advance while paused. Pinned by `WindTests.PushGatedWhenBlocked` and `WindTests.ZeroDeltaDoesNotAdvance` (Task 3).
5. **A lightning strike that would land during a wind warning, or after the round is over:** it is delayed past the warning, and never fires once the round has ended. Pinned by `LightningTests.StrikeShiftedPastWindWarning` and `LightningTests.NoStrikeWhenRoundOver` (Task 7).

---

### Task 1: Logic assembly, test assembly, data fields and difficulty tuning

**Files:**
- Create: `Assets/Game/Scripts/Logic/LanternKeeper.Logic.asmdef` (no references, `autoReferenced: true`)
- Create: `Assets/Game/Scripts/Logic/StormTuning.cs`
- Create: `Assets/Game/Tests/EditMode/LanternKeeper.Tests.EditMode.asmdef`
  - References: `LanternKeeper.Logic`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`.
  - `includePlatforms: ["Editor"]`, `overrideReferences: true` with `nunit.framework.dll`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`.
- Create: `Assets/Game/Tests/EditMode/StormTuningTests.cs`
- Modify: `Assets/Game/Scripts/LevelConfig.cs`, `Assets/Game/Scripts/GameSettings.cs` (`IsLevelUnlocked` around line 208)

**Interfaces:**
- Produces:
  - `StormTuning` (static class; `difficulty` is an `int`: 0 Easy, 1 Normal, 2 Hard):
    - `float GustMultiplier(int difficulty)`
    - `int MaxShades(int difficulty)`
    - `float ShadeSteal(int difficulty)`
    - `Vector2 LightningInterval(int difficulty)` (min, max)
    - `int ShadeTarget(int baseCount, int litCount, int cap)`, which returns `min(cap, baseCount + litCount / 3)`
    - Constants: `WalkSpeed = 3.5f`, `PeakPush = 2.5f`
  - `GameSettings` wrappers that pass `(int)Current`: `GustMultiplier`, `MaxShades`, `ShadeSteal`, `LightningInterval`.
  - `LevelConfig` fields: `public float windStrength;`, `public int shadeCount;`, `public bool lightning;`
  - `GameSettings.IsLevelUnlocked("island4")` returns `HasWon("island3")`.

- [ ] **Step 1: Write the failing tests** in `StormTuningTests.cs`:
  - `GustMultiplier(0..2)` returns 0.7, 1, 1.25.
  - `MaxShades` returns 3, 5, 6.
  - `ShadeSteal` returns 10, 15, 20.
  - `LightningInterval` returns (30,45), (35,55), (45,65).
  - `ShadeTarget(2,0,5)==2`, `ShadeTarget(2,3,5)==3`, `ShadeTarget(2,9,5)==5`, `ShadeTarget(2,9,3)==3`.
- [ ] **Step 2: Run the EditMode tests.** Expected: compile failure or FAIL, because `StormTuning` does not exist yet.
- [ ] **Step 3: Implement `StormTuning`, the `GameSettings` wrappers, the `LevelConfig` fields and the `island4` unlock.**
- [ ] **Step 4: Run the EditMode tests.** Expected: all `StormTuningTests` PASS and there are no console compile errors.
- [ ] **Step 5: Commit** with the message `feat: storm tuning, logic and test assemblies, island4 fields`.

### Task 2: Island 4 and the Heath biome in the builder, plus the compact menu

**Files:**
- Modify: `Assets/Game/Editor/IslandBuilder.cs`
  - `LevelNames` (line 19): add `"Island4"`.
  - Config writing near `EnsureMarshBiome` (around line 99): set Island 4's new fields after `WriteConfig` returns, so `WriteConfig`'s signature does not grow.
  - Add `EnsureHeathBiome()`.
  - Menu construction: switch to compact rows that fit 5 levels.
- Modify: `Assets/Game/Editor/IslandBuilder.Terrain.cs` (2–3 exposed ridges between beacons, controlled by a new `public bool ridges;` field on `LevelConfig` that is set true only for Island 4; do not branch on `levelId`)
- Modify: `Assets/Game/Scripts/MainMenu.cs` (compact row layout)

**Interfaces:**
- Consumes: Task 1 `LevelConfig` fields.
- Produces:
  - `Levels/Island4.asset`, `Levels/Biomes/Heath.asset`, `Scenes/Island4.unity`, and an Island 4 entry in the build settings.
  - Island 4 has no storm components yet.

- [ ] **Step 1: Before changing anything, save the Islands 1–3 baseline.** Run `git stash list` and confirm it is empty, then confirm `git status` is clean. The current committed scenes are the baseline.
- [ ] **Step 2: Implement the Island 4 config** with the Global Constraints values and 9 log pages. In those pages the previous keeper's trail ends on the cape, and the last page points to the lighthouse.
- [ ] **Step 3: Implement `EnsureHeathBiome()`.** Model it on `EnsureMarshBiome()`: heavy Rock, Sapling and Deadwood, few Tree, and skip with a warning any prefab name that is not found.
- [ ] **Step 4: Implement the ridges.** Add narrow raised spines that join 2–3 beacon pairs, consuming RNG only when `ridges` is true. Then implement the compact menu rows.
- [ ] **Step 5: Run Lantern Keeper → Build All Levels.** Then run **Validate Scene Wiring** on each scene. Expected: 0 nulls, and the build settings list MainMenu and Island1–Island4, with real GUIDs (not all zeros).
- [ ] **Step 6: Regression check.** Run `git diff --stat -- Assets/Game/Scenes/Island1.unity Assets/Game/Scenes/Island2.unity Assets/Game/Scenes/Island3.unity Assets/Game/Levels/Island*_Terrain.asset`.
  - Expected: only serialization-order noise.
  - Spot-check that the beacon `m_LocalPosition` values are unchanged: `git diff Assets/Game/Scenes/Island2.unity | grep -A3 "Beacon"` shows no position changes.
  - Any real change means RNG consumption changed. Fix it before continuing.
- [ ] **Step 7: Play Island 4 from the menu.** Unlock it by finding the PlayerPrefs key that `GameSettings.HasWon("island3")` reads and setting it with `PlayerPrefs.SetInt` from the MCP, or by winning Island 3. Expected: the island loads and plays like a normal island with no storm systems yet.
- [ ] **Step 8: Commit** with the message `feat: Island 4 Storm Cape terrain, Heath biome, compact level menu`.

### Task 3: WindCycle logic

**Files:**
- Create: `Assets/Game/Scripts/Logic/WindCycle.cs`
- Test: `Assets/Game/Tests/EditMode/WindTests.cs`

**Interfaces:**
- Produces:
  - `enum WindPhase { Calm, Warning, Gust }`
  - `class WindCycle`:
    - Constructor `WindCycle(int seed, float prevailingDegrees, float strengthScale, bool hard)`.
    - `void Tick(float dt)`.
    - Properties: `WindPhase Phase`, `Vector3 Direction` (unit vector on the XZ plane, constant through Warning and Gust), `float Strength01` (0 outside a gust; ramps 0→1 over 0.5 s, holds, then 1→0 over the last 0.5 s), `float TimeToNextWarning`, `float WarningEndsIn`.
  - `static Vector3 WindCycle.Push(Vector3 dir, float strength01, float strengthScale, bool airborne, bool sheltered)` returns `dir * strength01 * StormTuning.PeakPush * strengthScale`, then applies ×0.5 if airborne and ×0.2 if sheltered.
  - `static bool WindCycle.PushAllowed(bool paused, bool dying, bool rescuing, bool inSafeRing)`

- [ ] **Step 1: Write the failing tests:**
  - `PhasesCycleInOrder`: Calm → Warning → Gust → Calm.
  - `DurationsInRange`: calm between 12 and 20, or shorter on Hard; warning 2; gust 3–4.
  - `DirectionConstantDuringWarningAndGust`.
  - `DirectionWithin60OfPrevailing`.
  - `StrengthRamps`: at 0.25 s into a gust `Strength01` is about 0.5, and mid-gust it is 1.
  - `PeakPushBelowWalkSpeedOnAllDifficulties`: `Push(...).magnitude < StormTuning.WalkSpeed` for every `GustMultiplier`.
  - `AirborneAndShelterScale`: 0.5× and 0.2×.
  - `PushGatedWhenBlocked`: any true input returns false.
  - `ZeroDeltaDoesNotAdvance`.
- [ ] **Step 2: Run the EditMode tests.** Expected: FAIL.
- [ ] **Step 3: Implement `WindCycle`.** Draw durations and directions from a `System.Random(seed)` it owns.
- [ ] **Step 4: Run the EditMode tests.** Expected: all `WindTests` PASS.
- [ ] **Step 5: Commit** with the message `feat: WindCycle logic with tests`.

### Task 4: Wind in the scene

**Files:**
- Create: `Assets/Game/Scripts/Wind.cs`
- Modify: `Assets/Game/Scripts/PlayerController.cs`
  - Add `public static Vector3 ExternalPush;` next to `ExternalMove` (line 61).
  - Line 271: `Vector3 velocity = planarVelocity + ExternalPush;`. Do **not** feed the push into `planarVelocity`, so there is no inertia.
  - Reset it to zero where `ExternalMove` is reset (line 130).
- Modify: `Assets/Game/Editor/IslandBuilder.cs` (add `CreateWind(config, stage)` after `CreateTide`, only when `windStrength > 0`), `Assets/Game/Editor/SceneWiring.cs` (wire and validate the `Wind` references)

**Interfaces:**
- Consumes: `WindCycle`, `StormTuning`, `GameManager.Instance` (`IsPaused`, `IsDying`, `IsRoundOver`), `WaterHazard.IsRescuing`, `Beacon.All` with `ContainsSafe(Vector3)`, `PlayerController.IsGrounded`.
- Produces:
  - `Wind` (MonoBehaviour):
    - `public static Wind Instance`
    - Properties: `WindPhase Phase`, `Vector3 Direction`, `float Strength01`, `bool Sheltered`, `float TimeToNextWarning`, `float WarningEndsIn`.
    - Serialized fields: `strength`, `prevailingDegrees`, `player`, `hazard`.
  - Shelter check: a `Physics.Raycast` from the player's chest along `-Direction`, at most 4 m, against static non-trigger colliders, excluding the player layer.
  - Grass bend and fog streaks read `Direction` and `Strength01`. Reuse the existing grass bend shader globals if they exist; otherwise set a `Shader.SetGlobalVector("_LKWind", ...)` that the grass material ignores safely.

- [ ] **Step 1: Implement `Wind`, the `PlayerController` hook, `CreateWind` and the wiring.**
- [ ] **Step 2: Rebuild Island 4, then run Validate Scene Wiring.** Expected: 0 nulls.
- [ ] **Step 3: Play mode check through the MCP.** Read `Wind.Instance.Phase` over about 60 s.
  - Expected: phases cycle.
  - During a gust, `PlayerController.ExternalPush` is non-zero.
  - Standing in a lit beacon ring, or while paused, it is zero.
- [ ] **Step 4: Manual check.**
  - Walking into a gust still makes progress.
  - Standing behind a large rock reduces the push.
  - Getting blown off a ridge into water triggers the existing rescue and −10 fuel.
- [ ] **Step 5: Commit** with the message `feat: Wind pushes the keeper on Island 4`.

### Task 5: ShadeLogic

**Files:**
- Create: `Assets/Game/Scripts/Logic/ShadeLogic.cs`
- Test: `Assets/Game/Tests/EditMode/ShadeTests.cs`

**Interfaces:**
- Produces:
  - `enum ShadeState { Chase, Creep, Freeze, Retreat, Reforming, Stunned }`
  - `static ShadeState ShadeLogic.StateFor(float reveal, float frozenSeconds)`:
    - `reveal < 0.15` → Chase.
    - `reveal < 0.5` → Creep.
    - Otherwise Freeze, or Retreat once `frozenSeconds >= 1.5`.
  - `static float ShadeLogic.SpeedFor(ShadeState s)`: Chase 4.5, Creep 0.8, Retreat 3, all others 0.
  - `static float ShadeLogic.StealAmount(float fuel, float steal)` returns `Mathf.Min(fuel, steal)`, never below 0.
  - `static bool ShadeLogic.CanSteal(ShadeState s, float distance, float secondsSinceRescue)` returns true only when `distance <= 1.0`, the state is Chase or Creep, and `secondsSinceRescue >= 2`.
  - Constants: `TouchRadius = 1f`, `ReformSeconds = 8f`, `StunSeconds = 3f`, `HoverHeight = 0.2f`, `RescueGrace = 2f`.

- [ ] **Step 1: Write the failing tests:**
  - `ThresholdsMapToStates`: at reveal 0.149, 0.15, 0.499 and 0.5.
  - `RetreatAfterFrozen`: at 1.49 s and 1.5 s.
  - `SpeedsMatchSpec`.
  - `StealNeverExceedsFuel`: fuel 7 with steal 15 gives 7; fuel 0 gives 0.
  - `NoStealDuringRescueGrace`: 1.99 s gives false, 2.0 s gives true.
  - `NoStealWhenFrozenOrStunned`.
- [ ] **Step 2: Run the EditMode tests.** Expected: FAIL.
- [ ] **Step 3: Implement `ShadeLogic`.**
- [ ] **Step 4: Run the EditMode tests.** Expected: all `ShadeTests` PASS.
- [ ] **Step 5: Commit** with the message `feat: ShadeLogic with tests`.

### Task 6: Shade and ShadeSpawner in the scene

**Files:**
- Create: `Assets/Game/Scripts/Shade.cs`, `Assets/Game/Scripts/ShadeSpawner.cs`
- Create: the Shade visual, built by the builder:
  - A tall dark capsule using a soft transparent unlit material, plus two small emissive eye quads and a smoke `ParticleSystem`.
  - Saved as `Assets/Game/Prefabs/Gameplay/Shade.prefab` by a new `EnsureShadePrefab()` in `IslandBuilder.Art.cs`.
- Modify: `Assets/Game/Scripts/WaterHazard.cs` (expose `public float LastRescueTime { get; private set; }`, set when a rescue completes), `Assets/Game/Editor/IslandBuilder.cs` (`CreateShadeSpawner`, only when `shadeCount > 0`), `Assets/Game/Editor/SceneWiring.cs`

**Interfaces:**
- Consumes:
  - `ShadeLogic`, `StormTuning.ShadeTarget`, `GameSettings.MaxShades` and `ShadeSteal`.
  - `RevealMath.Evaluate(point, lanternPos, lantern.Radius)`.
  - `Lantern.TrySpend(float)`, `Lantern.Fuel`, `Lantern.AddFuel`.
  - `Beacon.All` with `BlocksMoth(Vector3)`.
  - `HUD.ShowFuelPenalty(string)`.
  - `GameManager.Instance.LitCount`, `LostGame`, `WonGame`.
  - The avoidance approach in `Moth.AvoidObstacles` and `Moth.AvoidBeacons` (`Moth.cs` lines 360–401).
- Produces:
  - `Shade`:
    - `public static readonly List<Shade> All`
    - Properties: `ShadeState State`, `bool Stunned`.
    - Methods: `void Stun(float seconds)`, `void Bind(Transform player, Lantern lantern, WaterHazard hazard, HUD hud)`, `void BeginDespawn()`.
  - `ShadeSpawner`: `public static ShadeSpawner Instance`, `int Cap`, `int AliveCount`.
  - A static event `Shade.Stole` (an `Action<float>`) for the HUD vignette in Task 9.
- Notes:
  - **Stealing:**
    1. Compute `amount = ShadeLogic.StealAmount(lantern.Fuel, steal)`.
    2. Call `lantern.TrySpend(amount)`.
    3. Show `hud.ShowFuelPenalty("-" + amount)`.
    4. Dissolve, then re-form at the dark edge after 8 s.
  - **Steering:** copy Moth's avoidance into private methods on `Shade`. Do not refactor `Moth`.
  - **Spawner:** follows `MothSpawner`'s edge-spawn pattern. Spawn attempts are capped. If no point at least 25 m from the player is found, take the farthest candidate. Never loop without a bound.

- [ ] **Step 1: Implement `Shade`, `ShadeSpawner`, the prefab, the builder hook and the wiring.**
- [ ] **Step 2: Rebuild Island 4, then run Validate Scene Wiring.** Expected: 0 nulls.
- [ ] **Step 3: Play mode check through the MCP.**
  - At the start `ShadeSpawner.Instance.AliveCount == 2`.
  - After 3 beacons are lit the count is 3, and it never exceeds `Cap`.
  - A Shade inside the lantern core reports `Freeze`.
- [ ] **Step 4: Manual check.**
  - A Shade approaches in the dark and freezes in the light.
  - With moths nearby, the dimmer lantern lets it touch: you see "-15" on Normal, and it re-forms at the edge after about 8 s.
  - Shades never enter a lit beacon ring.
  - With fuel under 15, a touch drains fuel to 0 and the lose sequence plays once.
- [ ] **Step 5: Commit** with the message `feat: Shade stalker and spawner on Island 4`.

### Task 7: LightningSchedule logic

**Files:**
- Create: `Assets/Game/Scripts/Logic/LightningSchedule.cs`
- Test: `Assets/Game/Tests/EditMode/LightningTests.cs`

**Interfaces:**
- Produces:
  - `enum LightningPhase { Waiting, Thunder, Flash, Afterglow }`
  - `class LightningSchedule`:
    - Constructor `LightningSchedule(int seed, Vector2 interval)`.
    - `void Tick(float dt, bool roundOver, float windWarningStartsIn, float windWarningEndsIn)`.
      - A negative `windWarningStartsIn` means there is no upcoming warning.
      - If entering Thunder now would overlap a warning, the strike is delayed until `windWarningEndsIn + 0.1`.
      - While `roundOver` is true it stays in Waiting.
    - Properties: `LightningPhase Phase`, `float Flash01` (1 during the flash, 1→0 across the afterglow), `bool FlashedThisTick`.
  - Constants: `ThunderLead = 1.5f`, `FlashSeconds = 0.3f`, `AfterglowSeconds = 0.5f`.

- [ ] **Step 1: Write the failing tests:**
  - `IntervalsWithinRange`: over 50 strikes for each difficulty interval.
  - `PhaseOrderAndDurations`.
  - `FlashedThisTickExactlyOncePerStrike`.
  - `StrikeShiftedPastWindWarning`.
  - `NoStrikeWhenRoundOver`.
  - `ZeroDeltaDoesNotAdvance`.
- [ ] **Step 2: Run the EditMode tests.** Expected: FAIL.
- [ ] **Step 3: Implement `LightningSchedule`.**
- [ ] **Step 4: Run the EditMode tests.** Expected: all `LightningTests` PASS.
- [ ] **Step 5: Commit** with the message `feat: LightningSchedule logic with tests`.

### Task 8: Lightning in the scene

**Files:**
- Create: `Assets/Game/Scripts/Lightning.cs`
- Modify:
  - `Assets/Game/Scripts/LightRevealed.cs`: subscribe to `Lightning.Flashed`. During `Flash01 > 0`, show the renderers at `Flash01` alpha **without** enabling the colliders, then return to normal reveal.
  - `Assets/Game/Scripts/Shade.cs`: subscribe to `Lightning.Flashed` and call `Stun(ShadeLogic.StunSeconds)`, with an outline or emissive boost while stunned.
  - `Assets/Game/Scripts/Beacon.cs`: highlight unlit beacons for the duration of the flash.
  - `Assets/Game/Editor/IslandBuilder.cs`: `CreateLightning`, only when `lightning`.
  - `Assets/Game/Editor/SceneWiring.cs`.

**Interfaces:**
- Consumes: `LightningSchedule`, `GameSettings.LightningInterval`, `Wind.Instance.TimeToNextWarning` and `WarningEndsIn` (pass −1 when there is no `Wind`), `GameManager.Instance.IsRoundOver`, `GraphicsQuality` (current preset).
- Produces:
  - `Lightning`:
    - `public static Lightning Instance`
    - `public static event Action Flashed`
    - Properties: `LightningPhase Phase`, `float Flash01`.
  - The flash is a directional `Light` animated by `Flash01`.
  - The exposure pulse uses a URP Volume `ColorAdjustments.postExposure` weight. It is skipped on Low, where post-processing is off.
  - The flash light casts shadows only on Ultra.

- [ ] **Step 1: Implement `Lightning`, the three hooks, the builder hook and the wiring.**
- [ ] **Step 2: Rebuild Island 4, then run Validate Scene Wiring.** Expected: 0 nulls.
- [ ] **Step 3: Play mode check through the MCP.**
  - `Flashed` fires every 35–55 s on Normal.
  - On the flash frame every `Shade.All[i].Stunned == true`.
  - A hidden stepping stone's collider stays disabled during the flash.
- [ ] **Step 4: Manual check.**
  - The flash reads clearly on Low and Ultra.
  - Stones and unlit beacons glint and then fade.
  - Shades are outlined and frozen for 3 s.
  - Thunder never comes during a gust warning.
- [ ] **Step 5: Commit** with the message `feat: Lightning reveals and stuns on Island 4`.

### Task 9: HUD, audio, rain and graphics hooks

**Files:**
- Modify:
  - `Assets/Game/Scripts/HUD.cs`: create these at runtime the same way the tide gauge is created, and only when `Wind.Instance` or `Lightning.Instance` exists.
    - Wind arrow and arc in the tide gauge's slot. It pulses during the Warning, fills by `Strength01` during the Gust, and is dimmed to 40% alpha while `Sheltered`.
    - Thunder glyph, visible during `LightningPhase.Thunder`.
    - A dark edge pulse on `Shade.Stole`, reusing the vignette in `LowFuelFX`.
  - `Assets/Game/Scripts/ProceduralAudio.cs`: add `WindBed()`, `WindHowl()`, `ThunderRumble()`, `ThunderCrack()`, `ShadeDrone()`, `Rain()`, each modeled on `SurfSwell()`.
  - `Wind.cs`, `Lightning.cs`, `Shade.cs`:
    - Play the clips above, routed to the existing `LanternMixer` groups the way `Tide` does.
    - The howl is positioned upwind of the player.
    - The Shade drone is a 3D source and mutes while the Shade is in `Freeze`.
  - Rain: a `ParticleSystem` that follows the camera, created by the builder when `lightning` is true. A `Rain.cs` component halves emission on the Low preset and lowers rain volume while sheltered or in a safe ring.
  - On Low, `Wind` stops writing the grass bend global, so grass stays still (spec: "grass doesn't bend in the wind" on Low).
- Create: `Assets/Game/Scripts/Rain.cs`

**Interfaces:**
- Consumes: the Task 4, 6 and 8 public members; `HUD.ShowFuelPenalty`; the existing graphics preset accessor used by `ParticleQuality`.

- [ ] **Step 1: Implement the HUD widgets, the audio clips and sources, and Rain.**
- [ ] **Step 2: Rebuild Island 4, then run Validate Scene Wiring.** Expected: 0 nulls.
- [ ] **Step 3: Manual check.**
  - The arrow matches the push direction.
  - The thunder glyph leads the flash by about 1.5 s.
  - The Shade drone is audible from its side and stops when the Shade is frozen.
  - M mutes everything.
  - Rain is lighter on Low.
  - Islands 1–3 show none of these widgets.
- [ ] **Step 4: Commit** with the message `feat: storm HUD, audio and rain`.

### Task 10: PlayMode smoke test, README and final verification

**Files:**
- Create: `Assets/Game/Tests/PlayMode/LanternKeeper.Tests.PlayMode.asmdef`
  - References: `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, nunit.
  - It cannot reference `Assembly-CSharp`, so it finds components by type name.
- Create: `Assets/Game/Tests/PlayMode/StormCapeSmokeTest.cs`
- Modify: `README.md` (add Island 4, wind, the Shade and lightning to the rules and controls sections)

- [ ] **Step 1: Write `StormCapeSmokeTest.IslandRunsSixtySeconds`.**
  - Load `Island4` with `SceneManager.LoadScene`.
  - Set `Time.timeScale = 4` and `yield return new WaitForSeconds(60)`. `WaitForSeconds` counts scaled time, so this is 60 s of game time in about 15 s of real time. Restore `timeScale` to 1 in `[TearDown]`.
  - Assert:
    - `LogAssert.NoUnexpectedReceived()`.
    - Objects whose types are named `Wind`, `Lightning` and `ShadeSpawner` exist.
    - At least one object of type `Shade` exists.
    - The player's `transform.position.y` is never more than 0.5 below `WaterHazard.SurfaceY` for longer than 1 s. Read it each frame by reflection.
- [ ] **Step 2: Run the PlayMode tests.** Expected: PASS.
- [ ] **Step 3: Run all EditMode tests.** Expected: everything passes.
- [ ] **Step 4: Run the full regression.**
  1. Build All Levels.
  2. Run the `git diff --stat` command from Task 2 Step 6. Islands 1–3 should show only serialization noise.
  3. Play Island 1 and Island 3 briefly. Expected: no wind, Shades, lightning or storm HUD.
- [ ] **Step 5: Run the performance check.** Island 4 on Low and Ultra with the FPS counter on. Expected: within the README's targets.
- [ ] **Step 6: Update the README.**
- [ ] **Step 7: Commit** with the message `test: Island 4 smoke test; docs: Storm Cape in README`.
