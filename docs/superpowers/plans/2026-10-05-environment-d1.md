# Visual Pass Phase D1 (Ground, Grass, Water, Horizon) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the photographic and over-bright ground, grass, water and distant ridges with the painterly night look, driven by each island's `LookProfile`. Gameplay is unchanged.

**Architecture:**
- **Pure mappings.** New `LookMapping` functions turn profile colours into water, ridge and ground tones. EditMode tests cover them.
- **Builder.** The builder generates painterly ground textures and assigns them to the existing terrain layers.
- **Shaders.** Rewritten Water, GrassBend and DistantRange shaders take palette colours.
- **Scene start.** `LookApplier` applies the water and ridge colours.

**Tech Stack:** Unity 6000.6.3f1 URP (Deferred renderer), hand-written HLSL, C#, NUnit, Unity MCP.

**Spec:** `docs/superpowers/specs/2026-10-05-environment-d1-design.md`. Parent spec: `docs/superpowers/specs/2026-10-03-art-direction-design.md`.

## Global Constraints
- **Code style:** `namespace LanternKeeper`, Allman braces, `[SerializeField]` private fields. Comment density matches the surrounding code.
- **Shaders:** hand-written URP HLSL in `Assets/Game/Shaders/`. No Shader Graph. No new packages.
- **The renderer is Deferred.** Custom stencil use must stay within URP user bits 0–3. Bit 8 is taken by the keeper's MoonRim exclusion.
- **Never photographic.** No `Assets/Game/Textures/PolyHaven/*` texture is referenced by terrain layers, water, grass or ridges after D1. The files stay in the repo.
- **Warm/cold.** No warm emissive term anywhere in D1. Warmth comes only from the lantern and beacon lights, through normal lighting. Every new palette colour is cool (`LookMapping.IsCool`).
- **Gameplay unchanged:**
  - terrain heights, splat painting, holes, collision and detail positions
  - the water plane height, `Tide`, `WaterHazard` and the rescue
  - `PathReveal` hidden paths
  - Lantern values 0.35/5.5/3.5/14
- **Low preset:** the foam line, the water-line rim and the ridge silhouettes are always on. No gameplay-relevant visual is Ultra-only.
- **Performance:** Ultra average ≤ 12.5 ms at 1080p in a player build (RTX 4050 Laptop); p95 is tracked.
- **Unity process** (rulings from Phase C):
  - Run Build All Levels only where the task says.
  - Commit real scene wiring changes. Revert pure noise: generated `.mat` files, ProjectSettings, terrain and biome assets, Shade.prefab, Look volumes, Keeper.controller.
  - Never `git checkout` the Scenes folder mid-task. It triggers a long reimport that drops the MCP.
  - Keep the Unity window focused for play mode and tests.
  - If the MCP drops for more than about 2 minutes, or a git action is denied, stop and report.
- **Model split (AGENTS.md):** implementers and reviewers run on Sonnet.

## Review Focus
1. **Island 3's tide moves the water plane.** The foam line and water-line rim must follow the moving intersection, with no fixed shore line baked in. Covered in Task 3's play check at low and high tide.
2. **Dawn on every island.** The new water, ground and ridges must still blend toward dawn (`_WaterTint` and the existing dawn globals), with no night colours stuck at dawn. Covered in Task 6's play check.
3. **Low preset with no post-processing.** The cool palette must not turn muddy black: the shore, the paths and the ridge silhouettes must still read. Covered in Task 7's Low captures and the readability check.
4. **Grass hiding gameplay.** Revealed stones, BankStep, fireflies and moths must be visible over grass on every island. Covered in Task 5's play check.
5. **Lightning on Island 4.** The water brightens briefly, the ridges get the lightning rim, and nothing stays bright after the flash. Covered in Task 3's and Task 6's checks.

---

### Task 1: The "before" capture set

**Files:**
- Create (output): `docs/look/baseline/2026-10-05-d1-before/**`

- [ ] **Step 1:** Run `LookBaselineCapture.Run("docs/look/baseline/2026-10-05-d1-before")` through the MCP.
  - Use the tool directly so the default output folder isn't overwritten.
  - Expected: the full island and screen set on Low and Ultra (29 shots × 2 plus the screens) and `frametimes.md`.
  - `git status` shows only the new folder.
- [ ] **Step 2:** View the spawn, shoreline and water shots for Island 1 and Island 3 on Ultra to confirm the set is good.
- [ ] **Step 3:** Commit: `docs: Phase D1 before capture set`.

### Task 2: Profile data and pure mappings

**Files:**
- Modify: `Assets/Game/Scripts/Look/LookProfile.cs`
- Modify: `Assets/Game/Art/Look/LookProfile_island{1..4}.asset` (or wherever the four profiles live; find them via `LevelConfig`)
- Modify: `Assets/Game/Scripts/Logic/LookMapping.cs`
- Test: `Assets/Game/Tests/EditMode/LookMappingTests.cs` (extend)

**Interfaces:**
- **`LookProfile` new fields:** `public Color waterShallow, waterDeep, foamColour, ridgeColour, tideBand; public Color[] groundTones;`
  - All default to `Color.clear` / null, meaning "derive".
- **`LookMapping`** (static, pure; uses Unity `Color` like the existing functions):
  - `public static Color WaterShallow(Color sea, Color moonRim)`: `sea` lerped 25% toward `moonRim`, then +10% value.
  - `public static Color WaterDeep(Color sea)`: `sea` with value × 0.45.
  - `public static Color Foam(Color moonRim)`: `moonRim` lerped 40% toward white, alpha 0.55.
  - `public static Color[] RidgeLayers(Color land, Color skyHorizon, int count)`: `count` colours, where layer i is `Lerp(land × 0.8, skyHorizon, (i + 1) / (count + 1))`. Layer 0 is nearest and darkest.
  - `public static Color[] GroundTones(Color baseColour)`: three tones, base × value 0.85, base, and base × value 1.12 with saturation × 0.9.
  - `public static Color OrDerived(Color authored, Color derived)`: returns `derived` when `authored.a == 0`.

- [ ] **Step 1: Write the failing tests**, for all four island profiles:
  - `WaterShallow`, `WaterDeep`, `Foam` and every `RidgeLayers(…, 3)` colour satisfy `LookMapping.IsCool`.
  - `RidgeLayers` is monotonic: each layer's distance to `skyHorizon` strictly decreases.
  - `GroundTones(base)` has 3 entries, each within 0.15 of `base` in HSV value and 0.08 in hue.
  - `OrDerived(Color.clear, x) == x` and `OrDerived(c, x) == c` for any c with alpha 1.
  - `WaterDeep(sea)` is darker than `WaterShallow(sea, rim)` (HSV value).
- [ ] **Step 2:** Run the EditMode tests. Expected: FAIL (compile errors).
- [ ] **Step 3:** Implement the fields and functions. Leave the profile assets at derived defaults, except `tideBand` on island3: `#4E6E66`, a pale wet tide band.
- [ ] **Step 4:** Run the EditMode tests. Expected: all pass, including the existing LookProfile validation tests.
- [ ] **Step 5:** Commit: `feat: D1 palette fields and LookMapping water/ridge/ground tones`.

### Task 3: Painterly water and shoreline

**Files:**
- Modify (rewrite): `Assets/Game/Shaders/Water.shader`
- Modify: `Assets/Game/Editor/IslandBuilder.Art.cs` (the water material setup around lines 225-290 and 1080-1100; stop setting cubemaps, normals, glitter and hard-coded colours) and `Assets/Game/Editor/IslandBuilder.Scenery.cs` (around line 53, the per-scenery water)
- Modify: `Assets/Game/Scripts/Lighting/LookApplier.cs` (`ApplyWater`)
- Modify: `Assets/Game/Editor/SceneWiring.cs` (water validation)

**Interfaces:**
- **Shader properties:** `_ShallowColor`, `_DeepColor`, `_FoamColor`, `_RimColor`, `_TideBandColor` (alpha 0 = off), `_DepthFade` (default 2.4), `_FoamWidth` (0.35 m), `_RimWidth` (0.12 m), `_SwellScale` (0.02), `_SwellSpeed` (0.04), `_MoonDir` (float4, xz; w = 1 when the moon is on), `_MoonPathStrength` (0.55).
  - Global: `_LKLightningFlash` (float, 0..1).
  - Removed: `_NormalA`, `_NormalB`, `_FoamNoise`, `_SkyCube`, `_DawnCube`, `_Glitter`, `_SkyExposure`, `_DawnExposure`. Keep `_WaveAmp`, `_FadeStart`, `_FadeEnd` and `_WaterTint`.
- **Shading:**
  - depth = scene depth minus water depth, via `_CameraDepthTexture`
  - colour = lerp(shallow, deep, saturate(depth / _DepthFade))
  - swell bands = smooth value noise at `_SwellScale` scrolled at `_SwellSpeed`, remapped to a 0–0.12 brightness lift
  - moon path = a soft, broken streak along `_MoonDir` in view space, multiplied by low-frequency noise for the dashes
  - foam = `smoothstep` over `depth < _FoamWidth`, pulsing by ±20% with the swell
  - rim = a thin band at `_FoamWidth .. _FoamWidth + _RimWidth`, in `_RimColor`
  - tide band = `_TideBandColor` over `depth < 0.6 m` when its alpha > 0
  - lightning = + `_LKLightningFlash` × 0.25 × `_RimColor`
  - Lighting comes from the main and additional lights through URP functions, so the lantern lights the water. There is no emissive warm term.
- **Low preset:** use the existing graphics keyword pattern; check how KeeperQuality, MoonRim or GraphicsProfile switch per preset. Swell and moon path are skipped, or computed per vertex, and refraction is skipped. Foam and rim are always on.
- **`LookApplier.ApplyWater(LookProfile p)`:** finds the water materials (renderers using `LanternKeeper/Water` in the scene) and sets the colours from `OrDerived(profile field, LookMapping.*)`, `_RimColor = p.moonRim`, `_MoonDir` from the moon light, `_TideBandColor = p.tideBand`.
  - It writes the `_LKLightningFlash` global each frame from `Lightning.CurrentFlash`, or 0 when there's no Lightning.
- **Validation:** "Water material <name> uses <shader>", if not `LanternKeeper/Water`. "Water material <name> still references texture <prop>", if any texture is assigned.

- [ ] **Step 1:** Rewrite the shader. Update the builder to stop assigning the removed properties.
- [ ] **Step 2:** Implement `ApplyWater` and the validation.
- [ ] **Step 3:** Run Build All Levels, then Validate Scene Wiring. Expected: 0 problems.
- [ ] **Step 4: Play-mode checks.** Take screenshots and look at them.
  - Island 1: the sea reads as calm soft bands, the moon path is visible, and there's a foam line plus a pale rim at the shore.
  - Island 3: at low and high tide, the foam and rim follow the shoreline (Review Focus 1), and the tide band is visible.
  - Island 4: no moon path; the water brightens briefly on lightning and returns (Review Focus 5).
  - Water rescue still works.
- [ ] **Step 5:** Run EditMode and PlayMode. Expected: pass.
- [ ] **Step 6:** Commit the shader, code and scenes (the wiring changes). Revert the noise. Message: `feat: painterly water with foam line and moonlit water-line rim`.

### Task 4: Painted terrain layers

**Files:**
- Create: `Assets/Game/Editor/GroundTextureBuilder.cs`
- Modify: `Assets/Game/Editor/IslandBuilder.Terrain.cs` (layer creation: lines 34-44 and `WarmKarooLayers`, `TintForestGrass`, `TintLayer` around lines 1363-1400) and `IslandBuilder.Art.cs` (where the layers get the Poly Haven textures)
- Create (output): `Assets/Game/Art/Environment/Ground/<levelId>/{sand,grass,dirt,rock,moss}_albedo.png` and `_normal.png`
- Modify: `Assets/Game/Editor/SceneWiring.cs`

**Interfaces:**
- **`GroundTextureBuilder.Build(string levelId, string layerName, Color baseColour, int seed) -> (Texture2D albedo, Texture2D normal)`**
  - 512×512, tileable.
  - The albedo uses the three `LookMapping.GroundTones(baseColour)`, blended by two octaves of tileable value noise. The noise feature size is about ¼ of the texture: large, soft blobs, with no fine noise.
  - The normal is a very subtle normal map derived from the same noise, at strength 0.15.
  - It writes PNGs to the output path and imports them: albedo sRGB; normal as NormalMap.
- **Base colours per layer**, from the profile:
  - grass = `land`
  - sand = `land` lerped 50% toward `sea` at value × 1.15
  - dirt (paths) = `land` lerped 35% toward neutral grey `#5A5650` at value × 1.25. Warmer-neutral but never amber: it must still pass a hue check that it isn't in the amber range of 20–50°.
  - rock = `land` lerped 50% toward `moonRim` at value × 0.9
  - moss = `land` at saturation × 1.1, value × 0.9

  Island 2 rock uses its slate (`extra` if set). Island 3 sand gets an extra pale band handled by its tide (the shader in Task 3).
- **Validation:** "Terrain layer <name> uses photo texture <path>" for any layer whose diffuse or normal path is under `Textures/PolyHaven/`.

- [ ] **Step 1:** Write `DirtPathReadsInMoonlight` as an EditMode test in `LookMappingTests` or a new `GroundTextureTests`. For each island profile, the derived dirt base colour's hue is outside 20–50°, and its value is above the grass base value, so paths read.
- [ ] **Step 2:** Run it. Expected: FAIL.
- [ ] **Step 3:** Implement the builder, the base-colour helper (`static Color LayerBase(LookProfile p, string layer)` in the builder) and the terrain-layer assignment for all islands and biomes.
- [ ] **Step 4:** Run the tests. Expected: pass.
- [ ] **Step 5:** Run Build All Levels, then Validate Scene Wiring. Expected: 0 problems.
- [ ] **Step 6: Play-mode look on each island.** The ground is flat painted with soft mottling, paths read in moonlight, the hidden-path reveal still works, and there's no photo detail. Screenshot each island.
- [ ] **Step 7:** Commit (textures, code, terrain layer assets, scenes). Revert the noise. Message: `feat: painted terrain layers generated from island palettes`.

### Task 5: Cool, sparse grass

**Files:**
- Modify: `Assets/Game/Shaders/GrassBend.shader`
- Modify: `Assets/Game/Editor/GrassTuftBuilder.cs` (shape-only blade texture: white albedo plus alpha; shorter tufts, `_TipHeight` 0.42 → 0.30)
- Modify: `Assets/Game/Settings/Graphics/GraphicsProfile_{Low,Medium,High,Ultra}.asset` and `Assets/Game/Editor/GraphicsProfileSetup.cs`, if it generates them: `grassDensity` × 0.6
- Modify: `Assets/Game/Scripts/Lighting/LookApplier.cs`: set the grass globals

**Interfaces:**
- **Shader properties and globals:** global `_LKGrassRoot` and `_LKGrassTip` (Color), and `_LKMoonRim` (Color, or reuse an existing Phase B global if one exists).
  - Albedo = texture alpha shape × lerp(root, tip, uv.y).
  - Moon-facing tips get + 0.15 × `_LKMoonRim` × saturate(dot(normal, moonDir)) × uv.y.
  - No emissive term.
- **`LookApplier`** sets `_LKGrassRoot` to `land` × value 0.7 and `_LKGrassTip` to `land` lerped 40% toward `moonRim`, desaturated × 0.8.

- [ ] **Step 1:** Implement the shader, the texture, the tuft height and the density change. Run Build All Levels.
- [ ] **Step 2: Play-mode checks on every island** (Review Focus 4).
  - The grass is dark and cool outside the lantern, and warms only inside its light.
  - Revealed stones, BankStep, fireflies and moths are visible over the grass.
  - Grass bending still works.
  - Screenshot each island.
- [ ] **Step 3:** EditMode and PlayMode, and Validate Scene Wiring with 0 problems.
- [ ] **Step 4:** Commit and revert the noise. Message: `feat: cool sparse grass with root-to-tip palette`.

### Task 6: Flat silhouette ridges and haze

**Files:**
- Modify (rewrite): `Assets/Game/Shaders/DistantRange.shader`
- Modify: `Assets/Game/Shaders/Silhouette.shader` (`HorizonRidge` and the islets) and `HorizonHaze.shader`, if they hold photo or orange tints
- Modify: `Assets/Game/Editor/IslandBuilder.Scenery.cs` (the range material setup around lines 166 and 312-385: stop assigning the photo textures and cubemaps)
- Modify: `Assets/Game/Scripts/Lighting/LookApplier.cs` (`ApplyRidges`)
- Modify: `Assets/Game/Editor/SceneWiring.cs`

**Interfaces:**
- **`DistantRange` properties:**
  - `_LayerColor` (Color), the layer's flat colour
  - `_RimColor`
  - `_RimStrength` (0.25)
  - `_MoonDir`
  - `_AerialStart` / `_AerialEnd` kept
  - global `_LKLightningFlash`, which adds the rim on lightning

  Removed: `_RockMap`, `_RockNormal`, `_GroundMap`, `_GroundNormal`, `_SkyCube`, `_DawnCube`, the exposures and `_Tint`.
- **Rendering:** the flat `_LayerColor` with the top-line moon rim (a height or normal-based edge facing `_MoonDir`), and fog-aware.
- **`LookApplier.ApplyRidges(LookProfile p)`:** assigns `LookMapping.RidgeLayers(p.land, p.skyHorizon, 3)` to the range renderers, nearest to farthest. Use the existing range object names from the builder: HorizonRange, HorizonRidge, Islet. `HorizonHaze`'s tint becomes `p.fogColour`.
- **Dawn:** keep the current dawn behaviour. If the old shader blended to the dawn cube, blend `_LayerColor` toward `p.dawn.dawnHorizon` by the same dawn global `DawnSequence` drives. Check DawnSequence for the parameter.
- **Validation:** "Ridge material <name> still references texture <prop>".

- [ ] **Step 1:** Implement the shaders, the builder change, `ApplyRidges` and the validation. Run Build All Levels, then Validate Scene Wiring. Expected: 0 problems.
- [ ] **Step 2: Play-mode checks.**
  - All islands: flat layered ridges stepping toward the horizon colour, and no orange or pale ridge on Island 2.
  - Island 4: a lightning rim flash.
  - Trigger dawn on Island 1 and Island 3 (use the existing debug or dawn trigger the smoke test uses). The water, ground and ridges move to dawn colours with no stuck night colour (Review Focus 2).
  - Screenshot each.
- [ ] **Step 3:** EditMode and PlayMode tests.
- [ ] **Step 4:** Commit and revert the noise. Message: `feat: flat silhouette ridges and palette haze`.

### Task 7: After set, comparison page, performance, regression

**Files:**
- Create (output): `docs/look/baseline/2026-10-05-d1-after/**`
- Create: `docs/look/compare/phase-d1.html`
- Create: `docs/look/perf/2026-10-05-phase-d1.md`

- [ ] **Step 1:** Run `LookBaselineCapture.Run("docs/look/baseline/2026-10-05-d1-after")`. View the shoreline, water, spawn and grass shots on Low and Ultra for all islands. Check Review Focus 3: on Low, the shore, the paths and the ridges read without post-processing.
- [ ] **Step 2:** Write `phase-d1.html` in the same format as `docs/look/compare/phase-b.html`: before (Task 1) next to after, per island and shot, with Low and Ultra tabs.
- [ ] **Step 3:** Make a player build and run the perf probe on all 4 islands, Low and Ultra (method in `docs/look/perf/2026-10-03-phase-b.md`). Write `2026-10-05-phase-d1.md` against the Phase C2 numbers. Expected: Ultra average ≤ 12.5 ms. If it fails, stop and report.
- [ ] **Step 4: Regression.**
  - EditMode and PlayMode tests.
  - Build All Levels plus Validate Scene Wiring on all scenes: 0 problems.
  - Play-mode check of tide, water rescue, dawn, lightning, the hidden-path reveal, grass bending and the keeper's look.
- [ ] **Step 5:** Commit the docs, images and any scene wiring. Revert the noise. Message: `docs: Phase D1 after set, comparison and perf`.
- [ ] **Step 6:** Controller gate: the user approves `phase-d1.html`.
