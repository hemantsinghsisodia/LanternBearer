# Phase H4 (Faster Storm, Richer Finale): Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox syntax where present; each task ends with full suites, Validate on 6 scenes and a commit.

**Spec:** `docs/superpowers/specs/2026-10-13-h4-finale-visuals-design.md` (parents: the H2 and H3 specs).

**Tech Stack:** Unity 6000.6.3f1, URP, C#, NUnit, Unity MCP.

## Context
You asked for two things after H3 merged (main `8a8f168`):
1. **The Island 5 rising storm should be faster.** It's 1.0 m/s now, and a full-speed climber has about 10 m to spare.
2. **The finale after lighting the great lamp should look much richer.** That's the islands, the clouds and the whole scene.

The current finale (`FinaleSequence`, 52 s) shows:
- Islands 1–4 as flat dark silhouettes, 48×48 heightmap meshes at 300 m;
- a ribbon-card storm ring;
- a grey-brown sunrise sea;
- a camera that pulls back from the lighthouse and pans.

**Your choices:**
- **Storm:** 1.4 m/s, the harsh option. The beacon push-back goes up to 5 m so a caught keeper can still recover (4 m failed the fuel-aware test).
- **Finale, all four upgrades:**
  - detailed islands;
  - dramatic clouds;
  - sea and sunrise;
  - a cinematic camera, making the finale about 60–70 s long.

## Workflow
The usual one:
1. A spec addendum: `docs/superpowers/specs/2026-10-13-h4-finale-visuals-design.md`.
2. A writing-plans plan: `docs/superpowers/plans/2026-10-13-h4-finale-visuals.md`.
3. Subagent-driven development: Sonnet implementers and reviewers, a ledger, a final whole-branch review.
4. A new branch `phase-h4-finale-visuals` from main.

**User gates:**
- midway screenshots of the new finale, after the islands and clouds are in;
- the final release build.

Merge only on "merge in main and push".

## Tasks

### Task 1: Storm at 1.4 m/s
**Changes:**
- `Assets/Game/Scripts/Logic/StormSurgeLogic.cs`: `RisePerSecond` 1.0 → 1.4, `PushBack` 3 → 5.
- Spec addendum and plan constraints.

**Retune the tests:**
- `SteadyClimberStaysAhead`: the full-pace bot is never caught; measured: 8.1 m ahead at the tightest point of the open stretch (10–30 m up), 2.8 m as the surge starts (the lowest gap). The test asserts 5–12 m and a lowest gap of at least 2 m.
- `SlowClimberGetsCaught`: 85% pace is caught.
- `CaughtKeeperCanClimbOut` becomes `CaughtKeeperRecoversByLightingABeacon`. The rise is now faster than the steepest climb rate, so recovery is through the 5 m push-back.
- Keep the PlayMode `SteadyBotReachesLamp`: everything on, fuel within 25–40%, never caught.

**If the full-pace bot gets caught at 1.4:** shorten nothing else. Raise the push-back to 5 m, and report it. Result: the full-pace bot was never caught at 4 m, but the fuel-aware `CaughtKeeperAt85PercentCanRecover` failed at 4 m, so the final push-back is 5 m.

**Unchanged:** the 8 s start delay, the ×3 caught drain and the cap.

### Task 2: Detailed miniature islands
`FinaleInstaller` bakes richer stand-ins of Islands 1–4, saved under `Assets/Game/Art/Finale/`. It reads only each island's `IslandN_Terrain.asset` and the island scene's prop list, read-only, without loading gameplay scenes into play.

**Each island gets:**
- **Terrain:** a 128×128 heightmap mesh with the sea skirt cut away. Its vertex colours come from that island's alphamap layers and terrain layer tints, so each island keeps its own palette.
- **Props:** the 40–80 largest trees and rocks, as merged low-LOD meshes. Use the island's own prefabs at their lowest LOD, merged per material into 1–3 meshes per island.
- **Beacons:** the real beacon model (`Assets/Game/Art/Beacons/Beacon.fbx`, LOD1) at each beacon slot. The flare lights its flame, glass glow and `BeaconBeam` cards, replacing the current card clusters.

**Scale and placement:**
- true scale, about 300 m away (as in H3);
- no vertical exaggeration needed once there's real detail; keep at most 1.2×;
- placed by `FinaleLayout`.

**Look:**
- night: lit by the moon rim plus its own beacon flare;
- sunrise: warm rim light from the sun side, via the `_SilhouetteTint` / dawn globals.

**Budget:** each island at or under about 25k triangles, with a combined finale draw-call increase of 20 or less.

### Task 3: Dramatic clouds
- **Replace the `StormRing` ribbon band in the finale** with layered soft cloud banks: 3 depth layers of large soft-edged billboard or ribbon cards, using a noise-based shader with soft alpha and edge lighting. Gameplay keeps the current storm ring look.
- **The storm breaks open** from the beam bearing, as now, with banks drifting apart.
- **Sunrise lighting:** from 36 s, cloud edges catch gold and pink from the sun direction, and the undersides stay violet.
- **God rays:** additive light-shaft cards from the sun through the cloud gaps over the sea. Finale only; their strength follows `ReduceFlashing` and the quality preset.
- **Reuse:** the existing `StormRing.shader` / `StormRing.mat` patterns. One material per layer.

### Task 4: Sea and sunrise
- **Fix the grey-brown finale sea.**
  - Trace why the water stays brown: the H2 agent found it isn't the fog or the haze. Check the water shader's colour inputs, `_WaterTint`, the sky reflection and the grade.
  - Target: deep blue-teal with a bright gold sun path (the gold `_RimColor` path from H2).
- **Sun:** a visible sun disc and glow rising over the horizon (sky shader or a sun card), at the real sun bearing.
- **Finale-only colour grading:** a `Volume` with warm white balance, lifted shadows, mild bloom and a slight vignette, blended in over the sunrise. Islands 1–4 volumes are unchanged.
- **Hooks:** keep it all in the `DawnSequence` finale overrides and `FinaleSequence`, so Islands 1–4 and the pre-dawn are untouched.

### Task 5: Cinematic camera and timeline
`FinaleTimeline` gets a longer timeline, about 66 s in total:

| Time | Phase |
|---|---|
| 0–6 s | Ignition |
| 6–18 s | Beam and storm break |
| 18–42 s | Island flyby |
| 42–60 s | Sunrise |
| 60–66 s | Hold |
| 66 s | Credits |

**The island flyby:** the camera glides toward and past each island in turn. Each beacon flares as the camera passes, at 21, 27, 33 and 39 s.

**The path:** `FinaleCamera`, using a Catmull-Rom spline through authored waypoints:
- rise from the lighthouse;
- an arc along the island chain;
- settle with the lighthouse silhouetted against the sun.

**Camera limits:**
- pitch no steeper than −20°;
- gameplay FOV;
- unscaled time;
- `TestSpeed` keeps working.

**Update along with it:**
- the Task 1 (H2) timeline tests and the finale tests;
- the credits start;
- the finale cue (80.6 s), which still covers it, with the menu underlay after it.

### Task 6: Validation, perf, build
- **Tests:** the full EditMode and PlayMode suites, including the Island 1–4 hash tests, the finale and credits tests, and the balance bots. Then Validate on all 6 scenes. Add checks for the new finale assets.
- **Perf:**
  - one probe pass on Island 5 at spawn, against H3 (Low 3.11 ms, Ultra about 10.0 ms), limit +0.3 ms;
  - plus one report-only measurement at a finale pose (30 s) on Ultra, to show the cost of the richer finale;
  - if the spawn probe fails, report before investigating.
- **Screenshots:** at 3, 12, 24, 36, 48, 56 and 64 s, 1920×1080.
- **Release:** `Builds/Island5_H4/LanternKeeper.exe`, smoke-tested.

## Critical files
- **Storm:** `Logic/StormSurgeLogic.cs`, `Tests/EditMode/StormSurgeLogicTests.cs`, `Tests/PlayMode/StormSurgeTest.cs`, `Island5BalanceTest.cs`.
- **Finale:**
  - `Scripts/FinaleSequence.cs`, `FinaleCamera.cs`, `FinaleIslands.cs`, `StormRing.cs`;
  - `Logic/FinaleTimeline.cs`, `FinaleLayout.cs`;
  - `Editor/FinaleInstaller.cs`;
  - `DawnSequence.cs` (finale overrides);
  - shaders `Silhouette.shader`, `StormRing.shader`, `Water.shader`, `NightSky.shader`.
- **Reuse:**
  - `Beacon.fbx` and `BeaconVisual` beam and glass materials;
  - `GreatLampBeam`;
  - the island `TerrainData` and LookProfile palettes;
  - `LighthouseInstaller` helpers.

## Constraints
- Islands 1–4 gameplay, scenes and terrain hashes are unchanged. The baking reads their data only.
- Never touch `UniversalRenderPipelineGlobalSettings.asset`.
- Leave `Build Profiles/` untracked.
- Never text-edit a loaded scene.
- `ReduceFlashing` and the Low preset are respected: fewer cloud layers, no god rays on Low, simpler islands on Low.

## Verification
- **Tests:**
  - EditMode: storm logic, timeline and installer idempotence;
  - PlayMode: the finale runs to completion at `TestSpeed`, at `timeScale` 0, and on Low and Ultra; the credits still start; and the balance bots.
- **Screenshots** at each phase, for the midway user gate and the end gate.
- **Perf** as above.
- **Release build,** smoke-tested, and then your play-test.
