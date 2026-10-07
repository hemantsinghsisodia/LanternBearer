# Visual Pass Phase F1: Moths, Shades and Beacons — Design

Status: approved in conversation on 2026-10-07, pending spec review.

**Parent:** `2026-10-03-art-direction-design.md` (Phase A). Its rules are binding:
- the stylized painterly night;
- the warm/cold rule applies to lights only;
- the fixed signal colours;
- readability rules 1–4;
- the performance budgets.

The approved concept boards are `docs/look/concepts/moths.html`, `shades.html` and `beacons.html`.

**Phase F is split in two:**
- **F1 (this spec):** moths, Shades and beacons.
- **F2 (later):** the HUD redesign.

## Goal
Replace the placeholder moth and bare-pole beacon with modelled assets that match the keeper and lantern. Give Shades the smoke-wraith treatment. Add the full beacon lighting moment. Gameplay is unchanged.

## Current state (for reference)
- **Moth.** `Moth.prefab` is a black ball with two stick wings and a white glow sprite.
  - Logic: `Moth.cs` (593 lines), `MothSpawner.cs`, `MothDrainFx.cs` (white glow).
- **Shade.** `Shade.prefab` uses the hollow-wraith mesh with a flat dark material.
  - Logic: `Shade.cs` (725 lines), `ShadeSpawner.cs`.
- **Beacon.** `Beacon.prefab` is a thin pole with a small lantern on top.
  - Logic: `Beacon.cs` (455 lines), `BeaconSafeRing.cs` (165 lines), `BeaconCompass`.
- **Existing hooks to reuse:**
  - `Lightning.CurrentFlash` (already capped by Reduce flashing)
  - the wind globals
  - the MoonRim pass
  - `ProceduralAudio`
  - `GraphicsQuality` / the Low preset

## 1. Moths
- **Model (Blender).**
  - About 300–500 triangles, with a wingspan of about 0.25 m.
  - A dark fuzzy body: a tapered capsule plus a fuzz shell.
  - Two pairs of pale, dusty wings: thin double-sided quads with painted textures and faint eye-spots.
- **Wing motion.** The `LanternKeeper/MothWing` shader flaps the wings in the vertex stage about the body axis. There is no rig.
  - Per-instance random phase.
  - Flap rate 14–20 Hz, with a slower glide every few seconds.
  - The Low preset uses a cheaper flap. Moths always flap.
- **Look.**
  - Dim grey-blue at a distance.
  - Close to the lantern, the wing albedo takes the warm light through normal lighting. Amber stays the lantern's colour.
  - A thin moon rim keeps the silhouette readable.
- **While draining.**
  - A soft violet `#B48CFF` aura.
  - Falling pale wing-dust particles, with a lower count on Low.
  - This replaces the white glow in `MothDrainFx`.
- **Unchanged:** flight, drain, spawning and `Moth.cs` logic. Only the visual child of `Moth.prefab` changes.

## 2. Shades
- **Model:** the existing hollow-wraith mesh is kept.
- **Shader `LanternKeeper/Shade`.**
  - **Body:** ink-dark (near-black, tinted by the island sky hue) and near-opaque, with a dim moon rim (readability rule 1).
  - **Ragged edges:** the hem and sleeve ends break up with animated noise. The bottom 30% dissolves into wisps through dithered alpha-cutout (no real transparency).
  - **Tatter sway:** a vertex wave grows towards the hem, driven by the wind globals and the Shade's speed.
  - **Eyes:** two soft pinprick eyes, cool and pale, flickering slightly. They are small and never beams.
- **Smoke trail.** Soft particles while the Shade moves; the trail is shorter on Low.
- **Lightning (Island 4).** A crisp pale-blue `#BCD2FF` outline for the duration of `Lightning.CurrentFlash`, so Reduce flashing softens it.
- **Frozen in lantern light.** The edges flicker and erode faster, as if burning away. This is driven by `Shade.cs`'s existing frozen state.
- **Unchanged:** behaviour, spawning, freeze and steal. Only the materials, the shader and a small visual controller change.

## 3. Beacons
- **Model (Blender).** A keeper's lamp-post about 3.2 m tall.
  - A dry-stone cairn about 1.2 m tall and 1.4 m wide, tinted with the island's `rockTint`.
  - A short iron post and bracket.
  - A storm lantern about 0.9 m tall, in the same design language as the keeper's lantern: a square forged frame, four panes, a peaked roof cap and a ring.
  - About 3–5k triangles, with a distance LOD.
  - The collider matches today's beacon footprint, so pathing and reachability are unchanged.
- **Unlit.** Dark glass, a moonlit roof-cap edge, faint cold reflections and no glow.
- **Lit.**
  - The panes blaze with warm amber emission (`#FFB15C`, core `#FFD38A`) and a gentle flicker.
  - Four soft additive light-beam cards, one per pane, faded by camera angle. They are shorter and dimmer on Low.
  - A warm point light.
  - A warm ground ring at the 8 m gameplay radius. This restyles `BeaconSafeRing`.
- **Lighting moment.** It is triggered by `Beacon.cs`'s existing light-up event. The beacon is safe on the same frame as today; the visuals never delay gameplay.

| Time | Effect |
|---|---|
| 0–0.15 s | Flare at the panes, with a bloom kick on Medium and up |
| 0–1 s | Light intensity and pane emission ramp up |
| 0.1–1.5 s | Embers rise: 30–60 particles, 15 on Low |
| On ignition | A deep "whoomp", synthesised with `ProceduralAudio` (no download) |
| 0.3–1.2 s | The safe ring blooms outward from the base to its full 8 m |

- **Unchanged:** beacon logic, positions, radius, `BeaconCompass` and Log pages.

## 4. Build, testing and validation
- **Models.**
  - Built by Blender 5.2 Python scripts kept in the repo at `ArtSource/Blender/moth_build.py` and `beacon_build.py`.
  - Exported as FBX at true scale (1 unit = 1 m) to `Assets/Game/Art/Creatures/Moth/` and `Assets/Game/Art/Beacons/`.
  - Textures are generated or painted by the scripts. There are no downloads.
- **Shaders.** Hand-written URP shaders (MothWing, Shade, BeaconGlass, BeaconBeam), each with:
  - forward, depth, depth-normals and shadow passes where they are needed;
  - GPU instancing;
  - a Low path selected by a uniform branch or global float, not new keyword variants.
- **Prefab swap.** Idempotent editor installers swap only the visual children of `Moth.prefab`, `Shade.prefab` and `Beacon.prefab`.
  - No full `IslandBuilder` level rebuilds.
  - Terrain and world content stay byte-identical, verified by hash.
- **Effects.** Wing dust, the smoke trail, embers and beams use cheap particles or quad cards. Each has a Low variant.
- **EditMode tests.**
  - Drain aura, lightning outline and lit-beacon colours exactly equal the parent spec's signal colours.
  - Pure timing curves for the flap cycle and the lighting moment (a `BeaconLightCurve` in Logic).
  - Prefab wiring.
- **PlayMode tests.**
  - The existing smoke test and `BeaconReachabilityTest` pass unchanged.
  - New `BeaconLightMomentTest`:
    - the light reaches full intensity within 1.2 s;
    - the ring reaches 8 m by 1.2 s;
    - the beacon's safe state is set on the same frame as the light-up event.
- **Validate Scene Wiring.** Every island uses the new moth, Shade and beacon visuals, and the placeholder meshes and the white glow sprite are no longer referenced.
- **Performance.**
  - One probe run per island on Low and Ultra against the Phase E numbers. Limit: +0.3 ms average.
  - If any island fails, report the numbers to the user before investigating. No open-ended A/B runs.
- **User checkpoints.**
  1. Midway: close-up and gameplay screenshots of the new moth and beacon, including the lighting moment, before the Shade work and the rollout to every island.
  2. End: a playable non-development release build, approved before merge.
- **Prerequisite.** The Phase E follow-up branch lands first. It fixes the legacy music-mute migration and the per-frame RenderSettings writes.

## Out of scope
- The HUD redesign (F2).
- Gameplay changes.
- New animations or rigs.
- Firefly visuals.
