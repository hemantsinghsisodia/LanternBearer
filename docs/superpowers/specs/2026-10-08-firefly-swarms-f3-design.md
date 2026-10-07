# Visual Pass Phase F3: Firefly Swarms — Design

Status: approved in conversation on 2026-10-08, pending spec review.

**Parents:**
- `2026-10-03-art-direction-design.md` (Phase A). Its binding rules apply here:
  - firefly green `#B8FFB0` is a signal colour and is used only for fireflies (rule 2);
  - readability rules 1–4;
  - the performance budgets.
- `2026-10-07-creatures-beacons-f1-design.md` (F1). This spec follows its pattern: a visual-only prefab swap, a `ProceduralAudio` sound, a pure Logic timing curve and an idempotent installer.
- F1 left firefly visuals out of scope. F3 picks them up.

## Goal
Replace the placeholder firefly (a sphere with a green point light) with a living swarm of blinking motes that streams into the keeper's lantern when it is collected. Also fold in the small leftovers from the F1 and F2 reviews. Gameplay is unchanged.

## Current state (for reference)
- **`Firefly.prefab`**:
  - a `Body` sphere;
  - a `Glow` green point light (range 3.5, intensity 1.6), registered with `LightQuality`;
  - a `PickupBurst` particle system.
- **`Firefly.cs` (513 lines)**:
  - bobs and wanders around `home`;
  - on a Player trigger it calls `Lantern.AddFuel(refillAmount = 20)`, plays `AudioManager.PlayFirefly` and the burst, and hides itself;
  - it respawns after about 9 s (scaled by `GameSettings.FireflyRespawnMultiplier`) at a new home chosen by `PickHome`.

## 1. The swarm at rest
- **Shape.**
  - 6 motes per swarm, or 4 on Low, drifting in a loose cloud about 1 m wide.
  - Each mote follows a slow wandering path with its own phase, so the cloud keeps changing shape.
  - The swarm as a whole keeps today's bob and wander from `Firefly.cs`. That movement code is unchanged.
- **Motes.**
  - Each mote has a bright core about 3 cm across, whiter towards the centre, and a soft halo about 15 cm across in `#B8FFB0`.
  - They are additive, unlit and camera-facing.
- **Blink.**
  - Each mote has its own rhythm: it rises over about 0.3 s, holds, fades, then stays dark for 1–3 s.
  - At least 2 motes are lit at any moment, so a swarm never vanishes.
  - Reduce flashing replaces off with a dim floor (about 30%) and softens the rise.
- **Ground glow.**
  - The existing green point light stays.
  - Its intensity follows the fraction of motes that are lit, smoothed so it breathes and never flickers.
  - `LightQuality`'s per-preset handling of firefly lights is unchanged.
- **Respawn.** On reappearing, the motes fade in one by one over about 1 s.
- **Readability.** The halo keeps a swarm readable at about 25 m in the darkest island areas. Green appears nowhere else.

## 2. The collect moment

| Time | Effect |
|---|---|
| Collect frame | Fuel +20 on the same frame as today. The HUD "+20" and the bar flash come from F2. |
| 0–0.5 s | Each mote curves into the lantern on its own arc. The arc follows the lantern's current position as the keeper moves, and the motes shrink as they arrive. |
| On arrival | The lantern flame brightens softly for about 0.3 s (visual only), with a gentle chime synthesised with `ProceduralAudio` and layered with the existing pickup sound. No downloads. |
| 0.5–2 s | 2 motes (1 on Low) linger, orbiting the lantern at about 0.3 m, blink, then fade. |
| After that | The swarm stays hidden until it respawns. Timing and placement are unchanged. |

- The stream replaces `PickupBurst`.
- Reduce flashing keeps the stream and softens the flame brightening.
- Two swarms collected close together each run their own stream independently.

## 3. Build
- **`FireflyMote` shader (`LanternKeeper/FireflyMote`).**
  - Hand-written URP shader: additive, unlit, GPU instanced, no shadow or depth passes.
  - The drift orbit and blink are computed in the shader from a per-mote phase.
  - The stream and linger are driven by per-swarm properties: progress 0→1 and the lantern's world position.
  - The Low path is selected by a global float, as F1 did. No new keywords.
- **`FireflySwarm.cs`.** The visual controller on the prefab's visual child.
  - It owns the motes, which are quads, and sets their properties with a `MaterialPropertyBlock`.
  - It drives the point-light breathing and plays the collect sequence.
  - `Firefly.cs` gets a minimal hook. On collect it tells the swarm the lantern's transform, then hides as today. On respawn it calls the fade-in.
  - No gameplay values or timings change.
- **`FireflyCurve` (Logic).** Pure functions for the blink envelope, the "lit count" and the stream, linger and fade-in timings. It is editor-testable, like `BeaconLightCurve`.
- **Lantern brightening.** It uses the lantern's existing visual flame or glow. The hook is visual only and never changes fuel or light radius.
- **Installer.**
  - An idempotent editor installer swaps only `Firefly.prefab`'s visual children: `Body` and `PickupBurst` out, swarm in.
  - No `IslandBuilder` level rebuilds.
  - Terrain and world content stay byte-identical, verified by hash.

## 4. Folded leftovers (small and safe)
- **F1:**
  - remove the dead ShadowCaster passes and the duplicated HLSL in the F1 shaders, with no visual change;
  - find the beacon cairn by component instead of by name.
- **F2:**
  - make the FPS PlayerPrefs key a single constant shared by `FpsReadout` and `GraphicsMenu`;
  - `InstallShade` stops re-saving materials when nothing changed.
- **Stale scene.** Delete `Assets/Game/Scenes/LanternKeeper.unity`. It is not in the build settings. Delete it only if no code, test or validation references it; otherwise remove those references in the same change.

## 5. Testing and validation
- **EditMode:**
  - the mote colour equals `LookPalette` firefly green `#B8FFB0`;
  - `FireflyCurve`:
    - blink envelope shape;
    - at least 2 lit at all times;
    - Reduce-flashing floor;
    - stream reaches 1 at 0.5 s;
    - linger ends by 2 s;
    - fade-in ends by 1 s;
  - prefab wiring: the swarm is present, `Body` and `PickupBurst` are gone, and the Glow light is still registered.
- **PlayMode (`FireflyCollectTest`):**
  - collecting adds fuel on the same frame;
  - by 0.55 s the streaming motes have arrived and are hidden, and the lingering motes are within 0.4 m of the lantern;
  - the swarm is hidden until respawn;
  - the existing tests pass: smoke, beacons, reachability and HUD.
- **Validate Scene Wiring.** Every island's fireflies use the swarm visual, and the placeholder sphere and `PickupBurst` are no longer referenced.
- **Look.**
  - a close-up and a gameplay-distance capture per island, on Low and Ultra;
  - a mid-collect capture;
  - `docs/look/compare/phase-f3.html`.
- **Performance.**
  - One probe pass per island on Low and Ultra against F2. Limit: +0.3 ms average.
  - If it fails, report the numbers before any investigation. No A/B runs.
- **User checkpoints:**
  1. Midway: screenshots of the swarm at rest and mid-collect, before the rollout to every island.
  2. End: a playable non-development release build, approved before merge.

## Out of scope
- Gameplay changes: fuel amounts, respawn timing, placement, counts.
- New HUD or compass markers for fireflies.
- Modelled insect meshes.
- Audio beyond the collect chime.
