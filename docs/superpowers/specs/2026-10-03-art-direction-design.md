# Visual Pass, Phase A: Art Direction — Design

Status: approved in conversation on 2026-10-03, pending spec review.

## Goal
Give Lantern Keeper a single, coherent art direction. The core image is **one warm light carried through a cold, dark world**. Today the game mixes photo-real Poly Haven assets, a low-poly flat-shaded keeper, flat card grass, untextured primitives and flat unlit creatures. Phase A fixes the direction and the rules. Phases B–F build it.

- Platform: PC (Windows).
- Engine: Unity 6 with URP.
- Gameplay stays unchanged.

Phase A changes nothing visible in the game.

## Phase plan (context)

| Phase | Work |
|---|---|
| A | Art direction, concepts, baseline capture, data containers (this spec) |
| B | Night lighting and post-processing |
| C | Keeper and lantern: true-scale import, hooded outfit, held storm lantern, animations |
| D | Environment: water, grass, cliffs and shoreline, props |
| E | Main menu and settings screen (display, graphics, audio, controls, accessibility), journal |
| F | HUD redesign; moths, Shades, beacons |

Each phase gets its own spec, plan, subagent build, reviews and playtest build. Each phase must name the rules below that it satisfies.

## 1. Art direction rules

### Style
**Stylized painterly night.**
- Soft gradients and simple shapes that read as silhouettes.
- Surfaces are painted or kept flat, never photographic.
- References: Alba, A Short Hike at night, Firewatch's palette.

### Warm/cold split (core rule)
- The lantern and lit beacons are the **only** warm light sources.
- Everything else is cool.
- No warm ambient light, no warm fill light on the environment, and no warm fog outside a light source.

### Darkness: balanced
- **Outside the lantern:** shapes and silhouettes in deep blue.
- **Inside the lantern's light:** full warm colour.
- **Moonlit edges:** cliff edges, the shoreline and the water surface get pale-blue moonlit rims, so hazards stay readable without the lantern. Every environment asset must support this.
- **Player control:** a brightness calibration setting (Phase E) adjusts the default. The art is tuned for the default.

### Fixed signal colours
Each colour means exactly one thing, everywhere: world, VFX and UI.

| Meaning | Colour |
|---|---|
| Lantern / safe warmth | amber `#FFB15C`, glow core `#FFD38A` |
| Something is draining you (moths) | violet `#B48CFF` |
| Lightning, Shade outline when stunned | pale blue `#BCD2FF` |
| Fireflies (pickup) | firefly green `#B8FFB0` |

### Island night palettes

| Island | Mood | Sky | Sea/water | Land | Moonlit rim | Extra |
|---|---|---|---|---|---|---|
| 1 First Light | gentle, inviting | `#0C1630` | `#17304D` | `#163027` | `#6FB3C8` | clear sky, lots of firefly green |
| 2 The Pine Reach | tall, austere | `#0D1124` | `#151D33` | pine `#1B2A2A`, slate cliff `#1D2233` | `#8A9CC4` | silver moonlit rims on cliffs and mesas, valley mist |
| 3 The Drowned Marsh | murky, uneasy | `#0A1A1C` | `#132A28` | reeds `#1B2E24` | mist `#7FB8A2` | low ground mist, hazy green moon `#D8F0D0`, pale wet tide band |
| 4 The Storm Cape | violent, cold | `#120F22` | `#1C1A30` | heath `#2A2638` | `#B8B2E0` | no moon; edges lit by distant lightning `#D6DCFF`; rain catches lantern light |

### Keeper: hooded wanderer
- **Silhouette:**
  - a deep hood, with the face lost in shadow
  - a long cloak with a torn hem, and a satchel
  - the lantern held out at arm's length
- **Build:** an outfit (hood, cloak, satchel) modeled in Blender over the **current body and skeleton**, skinned to the existing bones so every animation keeps working.
- **Scale:** re-exported at true scale, 1 unit = 1 m. The ×93.22 armature scale and every compensation for it are removed.

### Lantern: iron storm lantern
- **Shape:** a square forged-iron frame, four glass panes, a peaked roof cap and a ring handle.
- **How it's held:** it hangs from the handle in the keeper's fist (a hand socket and hand IK) and swings like a pendulum (the existing `LanternSway`).
- **Flame and light:**
  - The flame shrinks and reddens as fuel drops.
  - The glass glow pulses with the flicker.
  - The light sits at the flame.
- **HUD:** the shape doubles as the HUD fuel icon.

### Moths: dusty
- **Look:**
  - pale, dusty wings and a dark fuzzy body
  - far away: dim grey-blue
  - close up: the wings catch the lantern's warm light
- **Motion:** wings flap through a shader (no rig), with jittery fluttering flight.
- **While draining:** a violet aura and falling wing dust.

### Shades: smoke wraith
- **Model:** the existing hollow-wraith mesh.
- **Look:**
  - an ink-dark, near-opaque body
  - noisy, ragged edges that break into smoke at the hem and trail behind
  - shader-driven tatter sway
  - soft pinprick eyes
- **In lightning:** a crisp pale-blue outline.
- **Frozen in lantern light:** the edges flicker as if burning away.

### Beacons: keeper's lamp-post
- **Form:** a large storm lantern on a dry-stone cairn.
- **Unlit:** dark glass, with a moonlit roof edge.
- **Lit:** the panes blaze, four soft light beams, and a warm glowing ring on the ground marking the safe ring (about 8 m).
- **Lighting moment:**
  - a flare, and a light ramp of about 1 s
  - embers
  - a deep "whoomp"
  - the safe ring blooming outward

### UI: forged iron and amber
- **Panels:** deep ink panels with thin dark-brass frame lines.
- **Accent:** amber marks the primary action.
- **Type:**
  - titles: Cormorant Garamond
  - flavour text: Spectral (italic)
  - UI text and numbers: Inter
  - all three are SIL Open Font Licence fonts
- **HUD icons:** share the storm-lantern shape language.
  - fuel: a lantern whose flame and glow shrink
  - beacons: small roofs that light up amber
  - drain: icons (moth ×N, shield in a safe ring, ember when sprinting) instead of a "Drain xN" readout
- **Keeper's Log:** a parchment journal spread.

### Readability rules
Every phase is checked against these.
1. Cliff edges, water and Shades are readable without the lantern, at the default brightness.
2. Signal colours (amber, violet, pale blue, firefly green) are never reused for other meanings.
3. HUD text is at least 18 px at 1080p.
4. The Low preset keeps the same readability with fewer effects. No effect that gameplay needs may be Ultra-only.

## 2. Technical decisions

1. **Authored art; the builder only places it.**
   - Art lives in authored materials, prefabs and textures under `Assets/Game/Art/` (Keeper, Lantern, Creatures, Beacons, Environment, UI).
   - `IslandBuilder` places them through settings assets that it references.
   - Builder-generated materials and primitives are migrated phase by phase.
2. **`LookProfile` (ScriptableObject), one per island.**
   - Referenced from `LevelConfig`.
   - Holds:
     - palette colours, sky colours
     - moon on/off, colour and intensity
     - fog colour and density
     - grading parameters
     - moonlit rim strength
     - mist and rain flags
   - Lighting and post-processing code reads only from this.
3. **`UITheme` (ScriptableObject), shared.**
   - Holds colour values, font assets, frame and panel sprites, button styles and icon sprites.
   - UI code reads only from this.
4. **UI technology.**
   - Stays on uGUI and TextMeshPro, as authored prefabs: one per widget or screen, styled from `UITheme`.
   - `HUD.cs` (about 2,100 lines) is broken up into components during Phases E and F.
   - No migration to UI Toolkit.
5. **Shaders.**
   - Hand-written URP HLSL, consistent with the existing GrassBend, PathReveal, KeeperLit and Flame shaders. No Shader Graph.
   - Every new shader has a cheaper path for Low.
6. **Performance budgets**, measured on the developer's machine (NVIDIA GeForce RTX 4050 Laptop GPU, plugged in, Windows "Best performance"):
   - **Ultra:** a steady 60 fps at 1080p, with frame time ≤ 12.5 ms (about 25% headroom). If the panel is 1440p, that is also recorded, but not held to the budget.
   - **Low:** 60 fps at 1080p on the laptop's integrated GPU. To test this, force the game to the iGPU via Windows Settings → Display → Graphics → "Power saving".
   - The README's existing 800×600 table is a measurement, not a target.
7. **Scene validation.**
   - Validate Scene Wiring gains a check for every automatable rule:
     - the keeper is at true scale (no armature scale ≠ 1)
     - the lantern is in the hand (already exists, ≤ 1.5 m)
     - authored materials are present on beacons and creatures
     - a `LookProfile` is assigned on every island
     - a `UITheme` is present
     - minimum font sizes on HUD prefabs
   - Each phase adds the checks for its rules.

## 3. Phase A deliverables

1. **This spec.**
2. **Concept boards** in `docs/look/concepts/`: the approved mock-ups for style, darkness, island palettes, keeper, lantern, moths, Shades, beacons and UI theme.
   - Saved as their HTML source, renderable in a browser.
   - Plus an `index.md` listing each decision.
3. **Baseline capture tool**: an editor menu item, **Lantern Keeper → Capture Look Baseline**.
   - **Shots:** a fixed, data-driven list of shots per island, stored in a `LookShotList` asset (camera pose + label):
     - spawn
     - a lit beacon
     - the shoreline
     - a cliff
     - a water close-up
     - grass at the keeper's feet
     - a Shade up close (Island 4 only; Islands 1–3 have no Shades)
     - a moth up close
   - **Screens:** the main menu, the HUD, pause and the journal.
   - **Presets:** each shot is taken on Low and on Ultra, at 1920×1080.
   - **Output:** `docs/look/baseline/<YYYY-MM-DD>/<island>/<preset>/<shot>.png`, plus a `frametimes.md` with the average frame time over a fixed window per island and preset.
   - **Behaviour:**
     - Creatures are placed deterministically for the close-up shots.
     - The tool restores the scene, PlayerPrefs and the graphics preset afterwards, and leaves no scene changes.
   - It is run once in Phase A to produce the "before" set.
4. **Data containers, defined and filled in but not yet used.**
   - `LookProfile` with four island assets holding the palette values above.
   - `UITheme` with one asset holding the colours above. Font slots are filled with the imported fonts.
   - Nothing in the game reads them yet. Phase B wires `LookProfile`, and Phase E wires `UITheme`.
5. **Fonts:** Cormorant Garamond, Spectral and Inter.
   - TTFs placed under `Assets/Game/Art/UI/Fonts/`.
   - TextMeshPro SDF font assets generated from them.
   - Licences (OFL) added to `CREDITS.txt`.
6. **Housekeeping:** add `.superpowers/` to `.gitignore`.

## Testing
- **EditMode tests:**
  - `LookProfile` validation (all colours set, fog density in range, each island's profile present)
  - `UITheme` validation (all font slots assigned, the signal colours exactly match this spec's values)
- **Capture tool:** run once. It must produce the full set: 7 shots on Islands 1–3 and 8 on Island 4, since the Shade close-up exists only there, which is 29 shots × 2 presets; plus 4 screens × 2 presets and a `frametimes.md`, and leave `git status` showing only the new `docs/look/` files.
- **Regression:**
  - Build All Levels must still validate with 0 problems.
  - The game must look and play exactly as before. The capture tool's own "before" images are the evidence.
- Existing EditMode tests (50) and the PlayMode smoke test must pass.

## Out of scope for Phase A
- Any visible change to the game.
- Modelling the keeper outfit, the lantern, moths or beacons (Phases C and F).
- New shaders (Phases B, D and F).
- Menu and HUD rebuilds (Phases E and F).
