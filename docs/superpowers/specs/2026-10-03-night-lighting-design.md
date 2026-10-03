# Visual Pass Phase B: Night Lighting and Post-Processing — Design

Status: approved in conversation on 2026-10-03, pending spec review.

Parent spec: `docs/superpowers/specs/2026-10-03-art-direction-design.md` (Phase A). Its rules are binding here:
- the warm/cold split
- balanced darkness with moonlit edges
- the signal colours
- the island palettes
- readability rules 1–4
- the performance budgets

## Goal
Build the night itself:
- a painterly sky per island
- balanced darkness: cool, low ambient light; the lantern as the only warm light
- pale-blue moonlit edges that keep hazards readable
- per-island fog and colour grading
- bloom on the lantern and other glows
- Island 4 without a moon

All of it is driven by the Phase A `LookProfile` assets. Gameplay is unchanged.

## Current state (for reference)
- **Sky:** the photographic Poly Haven HDRI "qwantani moonrise", which reads as dusk.
- **Moonlight:** a directional light, intensity 1.08.
- **Ambient light and fog:** ambient comes from the HDRI at 0.18. Fog is fixed per `LevelConfig`. Both are set in `IslandBuilder.CreateAtmosphere`.
- **Post-processing:** one shared `Materials/Generated/NightVolumeProfile.asset`. The `GraphicsProfile.postProcessing` setting is Off on Low, Lighter on Medium and Full on High and Ultra.
- **Effects that write into the shared profile:** `LowFuelFX` (vignette, saturation) and `Lightning` (exposure).
- **Dawn:** `DawnSequence` blends the sky through the `_Top`, `_Horizon`, `_Ground` and `_Blend` properties, and blends fog, ambient light and the sun.
- **Baseline frame times (editor, 1080p):**
  - Ultra: 15.5–21.3 ms, with Island 3 the worst.
  - Low: about 4.5 ms.

## 1. Sky, moon, ambient light and fog

### Night sky shader
New hand-written URP shader `LanternKeeper/NightSky`.
- **Gradient:** a vertical gradient through three colours: `_Top` (the profile's `sky`), `_Horizon` (a lighter horizon band) and `_Ground`.
- **Moon:** a soft disc with a glow halo, in the profile's `moon` colour. It is drawn only when `moonOn`.
- **Stars:** the existing `StarTwinkle` stars are kept.
- **Clouds:** on Island 4 (`rain` set), slow, dark cloud bands.
- **Dawn:** it keeps the property names `DawnSequence` already animates (`_Top`, `_Horizon`, `_Ground`, `_Blend`).
- The HDRI is no longer used as the night sky. The file stays in the repo, unused.

### Moonlight
- When `moonOn`: a directional light, low-angle and cool, using the profile's `moon` colour. Start at intensity 0.35, with soft shadows.
- When `!moonOn` (Island 4): a very dim violet-grey fill, start at intensity 0.12. Lightning provides the drama.

### Ambient light
- Gradient (trilight) ambient: sky, equator and ground colours derived from the profile's `sky`, `sea` and `land`.
- Low intensity, so that outside the lantern the scene reads as silhouettes in deep blue.
- No warm ambient light, ever.

### Fog
- Exponential fog. Colour and density come from `LookProfile` (`fogColour`, `fogDensity`).
- **Ground mist** on Islands 2 and 3 (`mist`): soft, low-lying fog cards near the water and in valleys.
  - High and Ultra: full density.
  - Medium: half density.
  - Low: none.

### Applying the look
- New runtime component `LookApplier`, placed in each island scene and referencing the island's `LookProfile`.
- At scene start it applies: sky material colours, moonlight, ambient light, fog, the post-processing profile and the moon-rim settings.
- The mapping from profile to rendering values is a pure function `LookMapping`, in Logic, so EditMode tests can cover it.
- `IslandBuilder.CreateAtmosphere` no longer hard-codes any of these values. It places `LookApplier`, the night-sky material, the volumes and the mist.
- The lighting starting values in this spec (intensities, multipliers) are tuned during the build against before/after captures. Final values live in `LookProfile` and `LookMapping` constants.

### `LookProfile` additions
- `Color skyHorizon`: the horizon band. Start from the sky colour moved 35% toward `moonRim`, or toward `extra` on Island 4.
- `Color skyGround`
- Shared dawn colours, held in a single `DawnLook` asset referenced by every profile:
  - `dawnTop` = `#7E9CCB`
  - `dawnHorizon` = `#F4B38A`
  - `dawnGround` = `#C98A7A`
  - `dawnLight` = `#FFD9B0`
- `float moonIntensity`
- `float ambientIntensity`

## 2. The lantern's glow, post-processing and moon rims

### The lantern is the main light; gameplay is untouched
- The lantern light's **range is unchanged**. `Lantern.Radius` is the gameplay reveal radius used by `LightRevealed`, Shades and moths.
- Only the look changes:
  - colour amber `#FFB15C`, with a glow core of `#FFD38A`
  - a brighter core and softer falloff, retuned against the darker night
- A visual-only **halo sprite** sits at the lantern flame on every preset, so Low gets a glow without post-processing.
- High and Ultra bloom catches the lantern, fireflies, lit beacons and Shade eyes.

### Post-processing per island and per preset
- **Profiles:**
  - Each island gets a **look profile**, generated from its `LookProfile` into `Assets/Game/Art/Look/Volumes/LookVolume_<levelId>.asset`.
  - A shared **dawn profile**: `LookVolume_dawn.asset`.
  - The shared `NightVolumeProfile` is retired.
- **High and Ultra:**
  - neutral tonemapping
  - colour grading: cool shadows, a slight lift so silhouettes stay readable, and saturation, contrast and exposure from `LookProfile`
  - bloom, with its threshold tuned so only glows bloom
  - a soft vignette
  - SSAO on **Ultra only**
  - film grain on **Ultra only**, light
- **Medium:** grading and bloom at reduced quality. No SSAO and no grain.
- **Low:** no post-processing. The grading look is approximated through the ambient and fog colours.

### Moon-rim pass
- **What it is:** a custom URP `ScriptableRendererFeature`, `MoonRimFeature`, added to `Assets/Settings/PC_Renderer.asset`.
- **How it works:** one fullscreen pass that reads depth only.
  1. Reconstruct normals from depth.
  2. Add a soft rim in the profile's `moonRim` colour, scaled by `moonRimStrength`, to depth discontinuities (edges) and to surfaces facing the moon.
  3. Fade the rim with distance.
- **Result:** cliff edges, shorelines and the water line catch pale-blue light.
- **Never off.** It carries readability rule 1.
  - **Low:** half resolution, with a simpler edge test.
  - **Medium, High and Ultra:** full resolution.
  - Per-preset quality comes from a new `GraphicsProfile.moonRimQuality` field (0 = half-res simple, 1 = full).
- **Island 4:** the rim is lavender (from the profile) at strength 0.6, and it brightens briefly while `Lightning.Flash01 > 0`.
- **Dawn:** the rim fades to 0 as dawn plays.

### Performance gate
- The first plan task runs the existing `-lkperf` probe in a **player build** on the RTX 4050 Laptop GPU, plugged in, Windows "Best performance", at 1080p.
- It covers all four islands on Low and Ultra.
- If Ultra exceeds **10 ms** on any island, the plan adds Ultra cost cuts **before** the new effects go in. Candidates, in order of least visible change first:
  1. the water reflection probe
  2. shadow distance and cascades
  3. grass density on Island 3
  4. the extra point-light caps

## 3. Integration with existing systems

### Dawn
- `DawnSequence` keeps animating the sky shader properties, from the profile's night colours to the `DawnLook` colours.
- At the same time it crossfades the look volume to the dawn volume (by weight).
- It fades the moon-rim strength and the night ambient light to their dawn values.

### Effects volume
- Each island scene gets a separate, higher-priority **effects Volume**, using its own runtime profile with Vignette and ColorAdjustments only.
- `LowFuelFX` (vignette, saturation, the Shade-steal edge pulse) and `Lightning` (exposure pulse) write **only** to it.
- The island's base grading is never modified at runtime.
- **Low:** behaviour is as today. Lightning uses only its light, and low fuel uses its HUD fallback.

### Behaviour that must stay the same (verified in play mode)
- hidden-stone reveal visuals (`LightRevealed` reads `Lantern.BaseIntensity`)
- Shade and moth visibility in the darker night, including the moth drain glow
- beacon glints during lightning
- HUD readability
- BankStep and stone readability

### Builder and scenes
- **Builder output:** `CreateAtmosphere` produces the sky material, `LookApplier`, the look and effects volumes, the mist (Islands 2 and 3), and the lantern halo.
- **What may change in island scenes:** only lighting, sky, volume, mist and fog objects. Terrain, hidden paths and props must stay identical, checked with a semantic comparison against the current build.

### Scene validation additions
- Every island has a `LookApplier` with a valid profile whose `levelId` matches.
- The night sky material uses `LanternKeeper/NightSky`, not the HDRI.
- The effects volume exists, and its priority is higher than the look volume's.
- `PC_Renderer` contains `MoonRimFeature`.
- `NightVolumeProfile` is referenced nowhere.

## Testing
- **EditMode tests:**
  - `LookMapping`: profile values map to the expected ambient, fog and grading values, and nothing warm is produced. The ambient hue stays in the blue-violet range for every island.
  - `skyHorizon` derivation.
  - Dawn colour interpolation at u = 0, 0.5 and 1.
  - Moon-rim settings per preset: Low is half-res and never off.
- **Captures:**
  - Re-run Lantern Keeper → Look → Capture Look Baseline into `docs/look/baseline/<date>/` as the **after** set.
  - Add `docs/look/compare/phase-b.html`: a side-by-side before/after page per island and shot, Low and Ultra. It needs your approval.
- **Performance:**
  - Player-build probe after the change, on Low and Ultra, all islands.
  - Ultra ≤ 12.5 ms at 1080p (or the gate's cuts apply).
  - Low ≥ 60 fps on the integrated GPU. The user runs that one, because it requires the Windows per-app GPU setting.
- **Readability:** at default brightness, cliff edges, shorelines, water and Shades are readable on all four islands. Checked through the after set plus a short play-mode check per island.
- **Regression:**
  - existing EditMode tests (61) and the PlayMode smoke test pass
  - Build All Levels + Validate Scene Wiring report 0 problems
  - dawn, lightning and low fuel verified in play mode

## Out of scope
- New material or terrain shaders, water and grass rewrites (Phase D).
- Keeper and lantern models (Phase C).
- UI changes (Phases E and F).
- Moth, Shade and beacon art (Phase F).
- A player brightness setting (Phase E).
