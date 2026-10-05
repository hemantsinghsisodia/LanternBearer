# Visual Pass Phase D1: Ground, Grass, Water and Horizon — Design

Status: approved in conversation on 2026-10-05, pending spec review.

Parent spec: `docs/superpowers/specs/2026-10-03-art-direction-design.md` (Phase A). Its rules are binding:
- the stylized painterly night: never photographic
- the warm/cold split, with the lantern and lit beacons as the only warm sources
- the island palettes
- readability rules 1–4
- the performance budgets

Phase B (night lighting, `LookProfile`, `LookApplier`, `LookMapping`, the MoonRim pass) and Phase C (keeper and lantern) are in place.

Phase D is split in two:
- **D1 (this spec):** ground, grass, water and shoreline, distant ridges
- **D2 (a later spec):** cliff walls, rocks, trees, undergrowth, set pieces

## Goal
Replace the photographic and over-bright environment surfaces with the painterly night look:
- a calm painterly sea with a moonlit shoreline
- cool painted ground
- sparse, low, cool grass that only warms inside the lantern
- flat silhouette ridges

Gameplay is unchanged.

## Current state (for reference)
- **Water:** `LanternKeeper/Water` uses photographic normal maps, sky and dawn cubemap reflection, and glitter, so it reads as noisy static. Its colours are hard-coded in `IslandBuilder.Art.cs` (around lines 231-239 and 1087-1088).
- **Terrain:** URP `Terrain/Lit`, with five layers: sand, grass ground, dirt path, rock and moss. Biome variants are `WarmKarooLayers`, Heath and Marsh. The textures are Poly Haven photos in `Assets/Game/Textures/PolyHaven/`.
- **Grass:** terrain detail tufts from `GrassTuftBuilder` (`GrassBend` shader, `Textures/Grass/GrassBlade.png`) render vivid bright green. Density comes from `GraphicsProfile.grassDensity`.
- **Ridges:** `DistantRange` (photo rock and ground textures plus a cubemap tint) and `HorizonHaze`. Island 2 shows an orange ridge, and the others a pale one.
- **Deferred from Phase B and owned here:** the water-line rim, the pale and orange ridges, and the vivid grass.

## 1. Water and shoreline (user choice: "painterly soft bands")
A rewritten `LanternKeeper/Water`, keeping the same shader name and materials:
- **Colour:** a depth gradient from shallow (lighter, from `sea`) to deep (darker). No photo normals, no cubemap reflection, no glitter.
- **Swells:** two or three soft, blurred bands of slightly lighter tone, drifting slowly in one fixed direction. They're computed from smooth low-frequency noise in the shader, with no high-frequency shimmer.
- **Moon path:** a soft, broken streak toward the moon direction, made of blurred drifting dashes.
  - It is drawn only when `moonOn` is set.
  - On Island 4, the water brightens briefly while `Lightning.Flash01 > 0`.
- **Shoreline**, from a depth-intersection test:
  - a gentle pale **foam line** that pulses softly with the swell;
  - a thin **moonlit water-line rim** in `moonRim` just beyond it, which carries readability rule 1.
- **Lantern:** shows on the water only through normal lighting, as a soft warm patch. No emissive warm term.
- **Dawn:** the existing `_WaterTint` blend is kept.
- **Island 3:** a pale wet **tide band**, a lighter shallow band near the shore.
- **Low preset:** swells and moon path at reduced cost, with no refraction. The foam line and water-line rim are always on.
- **Unchanged:** the water plane height, the Island 3 `Tide` motion, `WaterHazard` and the rescue.

## 2. Ground and grass (user choices: "cool painted ground + sparse tufts", "keep terrain shader, generate textures")
**Painted terrain layers:**
- The builder generates painterly albedo and normal textures for each terrain layer and island, into `Assets/Game/Art/Environment/Ground/<levelId>/`. It assigns them to the existing terrain layers, including the biome variants.
- **Albedo:** a flat base colour from the palette, plus soft large-scale painted mottling: two or three tones within a small range of the base, with features of 1 m or more at the terrain's tiling. No photographic detail or fine noise.
- **Normal:** very subtle and low-frequency.
- **Per island,** derived from `LookProfile`:

  | Island | Ground |
  |---|---|
  | 1 | deep green land |
  | 2 | slate cliff tops, pine-floor ground |
  | 3 | reed-green ground, plus the pale wet tide band on sand |
  | 4 | heath purple-grey |

- **Paths:** the dirt path layer is slightly lighter and warmer-neutral (never amber), so walkable paths read in moonlight.
- **Hidden paths** keep the `PathReveal` shader and its behaviour.
- **Photo textures** are no longer referenced from island scenes or terrain layers. The files remain in the repo.

**Grass:**
- `GrassBlade.png` becomes shape-only: alpha, with white albedo.
- `GrassBend` adds:
  - a vertical gradient from a dark root (near `land`) to a cool, desaturated blue-green tip;
  - a faint `moonRim` tint on moon-facing tips.

  No bright greens. Inside the lantern's light, the tips warm through normal lighting only.
- **Density and height:** every preset's `grassDensity` is about 40% lower than today, and tufts are shorter. Low keeps its own lower value. Bending stays as it is.
- Fireflies, moths, revealed stones and hazards stay clearly visible above the grass.
- **Unchanged:** terrain heights, splat painting, holes, collision and detail placement positions.

## 3. Distant ridges and horizon
- **`DistantRange`:** flat silhouettes in two or three depth layers.
  - Each layer is a lighter, hazier step from `land` toward the sky's horizon colour.
  - A faint `moonRim` edge on the moon-facing top line.
  - No photo textures and no cubemap.
  - This removes the pale and orange ridges.
- **`HorizonHaze`:** its tint comes from the profile's `fogColour`.
- **Low:** identical silhouettes.

## 4. Data and wiring
- **`LookProfile` additions**, each with a default derived from the existing palette, so current assets stay valid:
  - `waterShallow`, `waterDeep`, `foamColour`
  - `groundTones` (array)
  - `ridgeColour`
  - `tideBand` (Island 3)
- **`LookMapping`** (pure, in Logic) gains:
  - the water depth colours from the profile;
  - the ridge layer colour ramp from `land` to `skyHorizon`;
  - the ground tone ramp.
- **`LookApplier`** sets the water and ridge material colours at scene start.
- **The builder** generates the ground textures and assigns the materials, and stops hard-coding environment colours.

## 5. Validation and testing
- **Validate Scene Wiring** additions:
  - No terrain layer or material in an island scene references `Assets/Game/Textures/PolyHaven/*` ground textures.
  - The water material uses `LanternKeeper/Water` and has no cubemap assigned.
  - The ridge materials have no photo textures.
- **EditMode tests:**
  - `LookMapping` water and ridge colours are cool: the hue is in the blue-cyan-violet range, with no warm hue, for every island.
  - The ground tones stay within ΔE 12 (or an equivalent HSV range) of the palette base.
  - The ridge layers step monotonically from `land` toward `skyHorizon`.
- **Captures:**
  - the existing look-capture tool, before and after, on Low and Ultra, all islands;
  - `docs/look/compare/phase-d1.html`, for user approval.
- **Performance:** a player-build probe on all islands, Low and Ultra. Ultra average ≤ 12.5 ms; p95 tracked.
- **Regression:**
  - EditMode and PlayMode tests pass.
  - Build All Levels and Validate Scene Wiring report 0 problems.
  - Play-mode check of tide, water rescue, dawn, lightning, hidden-path reveal and grass bending.

## Out of scope
- **D2:** cliff walls, rocks, trees, undergrowth, set pieces.
- UI (Phases E and F).
- Moths, Shades and beacons (Phase F).
