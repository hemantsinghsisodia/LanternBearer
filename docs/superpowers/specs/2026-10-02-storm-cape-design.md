# Phase 2: The Storm Cape (Island 4) — Design

Status: approved in conversation 2026-10-02, pending spec review.

## Goal
Add Island 4, "The Storm Cape". It is a clear step up in difficulty from Island 3 and the last challenge before the Phase 3 lighthouse finale. Three new systems make it harder, and they work together:

- **Wind** pushes the keeper toward edges and water.
- **The Shade** is a stalker that moves in darkness and steals fuel.
- **Lightning** reveals the island and stuns Shades.

## Architecture
This follows the Phase 1 tide pattern.

- Each system is switched on per level by `LevelConfig` fields. `IslandBuilder` adds a system's component only when its field is greater than 0.
- Each system has two parts:
  - A plain C# logic class with no scene dependencies, which is unit-testable.
  - A thin `MonoBehaviour` that wires the logic to the scene.
- Islands 1–3 keep every new field at 0, so they build and play exactly as they do today.

| Unit | Kind | Responsibility |
|---|---|---|
| `WindCycle` | plain class | Calm → warning → gust state machine: timings, direction, strength ramp |
| `Wind` | MonoBehaviour | Runs `WindCycle`, checks shelter and safe rings, writes `PlayerController.ExternalPush`, drives grass, fog and audio |
| `ShadeLogic` | plain class | Maps reveal value to state (chase / creep / freeze / retreat) and returns speed |
| `Shade` | MonoBehaviour | Movement, touch-steal, re-form timer, visuals, audio, stun on flash |
| `ShadeSpawner` | MonoBehaviour | Spawn and cap rules, modeled on `MothSpawner` |
| `LightningSchedule` | plain class | Strike intervals, 1.5 s thunder lead, avoids gust warning windows |
| `Lightning` | MonoBehaviour | Thunder, flash light, exposure pulse, raises static `Flashed` event |

## Island 4 data
Defined in `IslandBuilder.WriteConfig`, which writes `Levels/Island4.asset`:

| Field | Value |
|---|---|
| levelId / scene | `island4` / `Island4` |
| radius / hill height | 42 / 14 |
| seed | 4404 |
| beacons / fireflies / moths / hidden paths | 9 / 18 / 5 / 3 |
| hasCliff | true, plus 2–3 exposed ridges between beacons |
| tide | off (amplitude 0) |
| fog | slate blue-grey (0.32, 0.36, 0.42), density 0.016 |
| biome | new `Levels/Biomes/Heath.asset`, generated like Marsh: heavy on rocks, saplings and deadwood, with a few trees |
| new `LevelConfig` fields | `float windStrength = 1` (0 = off), `int shadeCount = 2` (0 = off), `bool lightning = true` |
| logEntries | 9 pages; the previous keeper's trail ends on the cape and the last page points to the lighthouse |

`Island3.nextLevelScene = "Island4"`. Island 4 unlocks when Island 3 is won, using the existing `GameSettings.IsLevelUnlocked` chain.

### Difficulty multipliers (`GameSettings`)
| | Easy | Normal | Hard |
|---|---|---|---|
| Gust strength | 0.7× | 1× | 1.25× |
| Max Shades | 3 | 5 | 6 |
| Shade fuel steal | 10 | 15 | 20 |
| Lightning interval | 30–45 s | 35–55 s | 45–65 s |

## Wind
- **Cycle:**
  - Calm: 12–20 s (shorter on Hard).
  - Warning: 2 s.
  - Gust: 3–4 s, ramping up over 0.5 s and back down over 0.5 s. Peak push is 2.5 m/s × the difficulty multiplier.
- **Direction:** fixed for the whole of a gust. Each new gust picks a direction around the prevailing sea wind, ±60°.
- **Push:**
  - It is a horizontal velocity added to `planarVelocity` before `controller.Move` through the new `public static Vector3 PlayerController.ExternalPush`. This is the same static-hook style as `ExternalMove`.
  - The peak push is always below walk speed, so the player can always make progress against it.
  - The push is 0.5× while airborne.
- **Shelter:** a raycast of up to 4 m from the player's chest, upwind, against static scenery colliders (rocks, cliffs, trees). A hit reduces the push to 20% and dims the HUD arrow.
- **No push** while inside a lit beacon's safe ring (`Beacon.ContainsSafe`), during a water rescue, while dying, or while paused.
- **Being blown into water** uses the existing `WaterHazard` rescue: a splash, a respawn, and −10 fuel.
- Grass bend and fog streaks read `Wind.Direction` and `Wind.Strength01`. Moths and fireflies are not pushed.

## Shade
- **State** is decided each frame from `r = RevealMath.Evaluate(shadePos, lanternPos, lantern.Radius)`:

  | Reveal value | State | Behaviour |
  |---|---|---|
  | `r < 0.15` | Chase | 4.5 m/s toward the player (walk is about 3.5, sprint about 6) |
  | `0.15 ≤ r < 0.5` | Creep | 0.8 m/s, eyes flare |
  | `r ≥ 0.5` | Freeze | Holds still; after 1.5 s frozen it retreats to darkness at 3 m/s |

- **Touch** (within 1.0 m):
  - Calls `lantern.TrySpend(steal)`. If that fails because fuel is lower, it drains whatever is left.
  - Shows the "-N" fuel popup and an edge vignette pulse (reusing `LowFuelFX`).
  - The Shade dissolves, then re-forms at the dark island edge after 8 s.
- **Safe rings:** Shades cannot enter lit beacon safe rings (`Beacon.BlocksMoth`).
- **Steering:** Shades avoid terrain and props the same way moths do, by reusing `Moth`'s avoidance approach. They hover about 0.2 m above the ground, like a tall figure rather than a flier.
- **Spawning (`ShadeSpawner`):**
  - Starts with 2 Shades.
  - Adds 1 per 3 beacons lit, up to the difficulty cap.
  - Spawns at the dark edge at least 25 m from the player.
  - Fades out on round end.
- **Visuals:** a smoky silhouette and two faint eyes. Nearly invisible in darkness, outlined during a flash.
- **Interaction with moths:** moths shrink the lantern through `ProximityDim`, which lowers `Lantern.Radius`. This lets a Shade get closer, and that combination is intended.

## Lightning
- **Schedule:**
  - Waits the difficulty interval.
  - Thunder rumble and HUD glyph 1.5 s before the strike.
  - Flash: 0.3 s of a bright directional light plus an exposure pulse.
  - Afterglow fades over 0.5 s.
  - A strike is pushed back if its 1.5 s lead would overlap a wind warning.
- **On flash** (static event `Lightning.Flashed`):
  - All Shades are stunned for 3 s and outlined.
  - Unlit beacons are highlighted.
  - `LightRevealed` stones show visually for the flash and afterglow without enabling their colliders.
- **Graphics:** on Low there is no exposure pulse, only the light. On Ultra the flash light casts shadows.

## HUD and audio
- **HUD** (each element only when its system exists):
  - Wind arrow and arc in the tide gauge's slot. It pulses during the warning, fills during the gust, and dims while sheltered.
  - Thunder glyph.
  - Shade hit uses the existing fuel penalty popup and the `LowFuelFX` vignette.
  - No Shade proximity marker.
- **Menu:** the level list switches to a compact row layout that fits 5 islands.
- **Audio:** all built with `ProceduralAudio` and routed through `LanternMixer` groups:
  - A wind bed, plus a directional howl.
  - A thunder rumble and crack.
  - A Shade breathing drone that is positional and cuts out when the Shade freezes.
  - A light rain loop, quieter when sheltered or in a safe ring.
- **Rain particles:** half density on Low.

## Files
- **New:**
  - Scripts: `Scripts/Wind.cs`, `Scripts/WindCycle.cs`, `Scripts/Shade.cs`, `Scripts/ShadeLogic.cs`, `Scripts/ShadeSpawner.cs`, `Scripts/Lightning.cs`, `Scripts/LightningSchedule.cs`.
  - A Shade prefab or a builder-made Shade.
  - Tests: `Tests/EditMode/*` and `Tests/PlayMode/*` with their asmdefs.
  - Generated by the builder: `Levels/Island4.asset`, `Levels/Biomes/Heath.asset`, `Scenes/Island4.unity`.
- **Modified:**
  - Scripts: `LevelConfig.cs` (wind, shade and lightning fields), `GameSettings.cs` (multipliers, Island 4 unlock), `PlayerController.cs` (`ExternalPush`), `HUD.cs`, `MainMenu.cs` (compact rows), `LightRevealed.cs` (flash show), `ProceduralAudio.cs`.
  - Editor: `IslandBuilder*.cs` (Island 4 config, Heath biome, ridges, add components only when enabled), `SceneWiring.cs`.
  - Docs: `README.md`.

## Testing
- **EditMode:**
  - `WindCycle` phase order, durations and constant direction, the ramp, and the multipliers.
  - `ShadeLogic` thresholds.
  - `LightningSchedule` intervals, lead time, and no overlap with wind warnings.
  - Spawner cap math.
- **PlayMode smoke:**
  - Load Island 4 and run about 60 s.
  - Check: no errors are logged; Wind, Lightning and Shades are present; the player is never below the water without a rescue.
- **Regression:** rebuild all islands. Islands 1–3 must not change, so diff the scene files. New systems must use no RNG when disabled.
- **Manual playtest:**
  - You can walk against a gust.
  - Shelter reduces the push.
  - Being blown off a ridge gives −10 fuel and a respawn.
  - A Shade freezes in the light and gets in when moths dim the lantern.
  - A flash stuns Shades and reveals stones and beacons.
  - Island 4 unlocks after Island 3.
  - Frame rate is acceptable on Low and Ultra.

## Out of scope
- Medals.
- The lighthouse finale (Phase 3).
- Pushing moths or fireflies with wind.
- Lightning strikes that damage the player.

## Workflow
- Implementation is delegated to Claude Sonnet 5.5 High subagents.
- It should run in a session that has the Unity MCP connected, so it can build scenes and run tests.
