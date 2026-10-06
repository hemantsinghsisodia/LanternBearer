# Visual Pass Phase D2: Props, Cliffs and Set Pieces — Design

Status: approved in conversation on 2026-10-05, pending spec review.

**Parents:** `2026-10-03-art-direction-design.md` (Phase A) and `2026-10-05-environment-d1-design.md` (D1, including its amendment).

**Binding lesson from D1:** the warm/cold rule applies to **lights only**. Materials keep natural, distinct hue families per island, and must never collapse into one shared blue.

## Goal
Replace the photo-scanned Poly Haven props and the stretched terrain cliff faces with stylized models that match the keeper and the D1 ground. Gameplay is unchanged.

## Current state (for reference)
- **Props:** about 185 Poly Haven prefabs in `Assets/Game/Prefabs/PolyHaven/`:

  | Category | Count |
  |---|---|
  | Tree | 14 |
  | Sapling | 12 |
  | Deadwood | 10 |
  | Rock | 25 |
  | Undergrowth | 39 |
  | GroundCover | 49 |
  | Flower | 34 |
  | SetPiece (photo cliffs) | 2 |

- **Placement:** `Levels/Biomes/*.asset` entries (prefab, category, count) are placed by `IslandBuilder` from the level seed.
  - Island 1 = PineForest
  - Island 2 = Namaqualand
  - Island 3 = Marsh
  - Island 4 = Heath
- **Cliffs:** the steep sides of terrain plateaus and ridges (`StampPlateaus`, `RaiseRidges`). The terrain stretches the ground layers down vertical faces, giving a corrugated, palisade look.

## Source assets
**Quaternius Stylized Nature MegaKit, Standard (free) version.**
- 68 models, CC0 1.0. The user downloaded it to `ArtSource/Nature/`, unzipped to `ArtSource/Nature/MegaKit`.
- **Contents:**
  - trees: CommonTree, Pine, TwistedTree and DeadTree, 1–5 each
  - Rock_Medium 1–3, Pebble_Round and Pebble_Square, RockPath variants
  - Bush_Common (with flowers), Fern_1, Plant_1/1_Big/7/7_Big
  - Flower_3/4 (Single and Group), Petals 1–5
  - Clover 1–2, Grass_Common and Grass_Wispy (Short and Tall), Mushroom_Common and Mushroom_Laetiporus
  - textures for bark, leaves, flowers, grass, rocks and mushrooms
- **Import:** only the models used go into `Assets/Game/Art/Environment/Nature/{Models,Textures,Materials,Prefabs}`. `CREDITS.txt` gets a line: "Stylized Nature MegaKit by Quaternius (CC0)".

## 1. Materials and colour
- **Stylized foliage shader** `LanternKeeper/Foliage` (hand-written URP), for leaves, bushes, flowers, plants and clover:
  - the pack's alpha-cutout textures, multiplied by an island tint
  - soft wrap lighting for the Ghibli feel
  - gentle wind sway from the existing wind globals
  - lit by the lantern and moon through URP functions
  - GPU instancing
  - Low: no sway, and simpler lighting
- **Bark and rocks:** lit materials with the pack textures, tinted per island. Instancing is on.
- **New `LookProfile` fields:** `foliage`, `bark` and `rockTint`, with authored values per island.

  | Island | Trees | Rocks | Plants |
  |---|---|---|---|
  | 1 PineForest | deep green pines and common trees, brown bark | grey | green ferns and bushes |
  | 2 Namaqualand | twisted and dead trees, olive and gold foliage | warm grey and tan | golden plants, coloured flowers |
  | 3 Marsh | olive twisted trees | mossy green-grey | reeds (Section 3) |
  | 4 Heath | dead and sparse twisted trees | dark slate | purple-brown heath bushes |

- **Keeper scale** (the keeper is 1.76 m): trees 4–8 m, saplings 2–3 m, bushes about 1 m, flowers about 0.3 m, medium rocks 0.5–1.5 m, boulders 2–4 m.

## 2. Prop replacement and cliff cladding
**Prop swap:**
- Each biome entry's Poly Haven prefab is replaced by a MegaKit-based prefab of the same category. The count stays, and the placement rules and seed stay, so spots and density stay as today.
- **Trees and large rocks** get fitted colliders (a capsule for trunks, a box or mesh for rocks) wherever today's prop had one. Small plants have none.
- **Hero variants** (`_hero`) map to the largest tree of the island's type.
- **Ground cover:** the terrain detail prototypes switch from Poly Haven ground-cover and flower meshes to MegaKit clover, flowers and mushrooms with the foliage shader. D1's GrassBend tufts stay.
- **Unchanged:** the placement rules that keep props off trails, beacon rings, hidden paths and the shore.

**Cliff cladding** (the user chose rock meshes):
- A pure planner `CliffCladdingPlanner` (Logic, tested) takes the heightmap and finds steep cells (slope over 45°). It traces edges and outputs rock placements:
  - position, rotation, tilt
  - scale 2–6 m
  - spacing 2–3 m, with two staggered rows on faces taller than 3 m
  - rocks sunk 30–50% into the slope
  - seeded per island
  - excluding trails, beacon tops and shore cells
- The builder instantiates MegaKit rocks from the plan with **no colliders**, so the terrain keeps providing collision and movement is unchanged.
- The two Poly Haven SetPiece cliffs are replaced by cladding.
- **Culling:** cladding uses GPU instancing plus a per-preset distance cull, tied to the existing prop and detail distance settings.

## 3. Gaps, performance, validation and testing
**Gaps:**
- **Marsh reeds (Island 3):** generated by the builder, like `GrassTuftBuilder`.
  - tall, thin, slightly bent blades with a seed head, in olive and straw tones
  - placed as detail where the Poly Haven reeds are today
  - sway with wind
- **Heath (Island 4):** Bush_Common and Plant variants tinted heath purple-brown.
- **Big boulders:** scaled and rotated Rock_Medium.
- **Anything with no good match:** the Poly Haven prefab stays and is listed in the report and the validation allow-list. None may sit in hero spots (spawn, beacons, paths).

**Performance:** instancing and LOD or distance culling. Player-build probe on all islands, Low and Ultra: Ultra average ≤ 12.5 ms; p95 tracked.

**Validation (Validate Scene Wiring):**
- No island-scene prop uses `Prefabs/PolyHaven/*`, except the allow-list.
- Every tree and large rock with a collider before still has one.
- Every cliff face taller than 2 m has cladding.
- Terrain data is unchanged (heights, alphamaps, holes, details positions), verified against base.

**Tests:**
- **EditMode:**
  - the per-island foliage, bark and rock tints are distinct (a hue-spread check)
  - `CliffCladdingPlanner` is deterministic, and keeps out of trails and beacon rings on a synthetic heightmap
  - every steep edge cell is covered
- **PlayMode:** the smoke test, plus a walking check that the keeper is blocked by tree and boulder colliders and can reach every beacon by path on each island.

**Approval:**
- Mid-way screenshots go to the user before the full rollout: one island fully converted, to check direction and colour.
- At the end: before and after capture sets, `docs/look/compare/phase-d2.html`, and a player build for the user to play before merge.

## Out of scope
- Moths, Shades and beacons (Phase F).
- UI (Phase E).
- New animations.
- Terrain shape changes.

## Amendment (2026-10-06, after the cladding fix round): rock-wall shading
**Finding:** rock meshes cannot cover cliff walls that border trails, beacon rings or rim walkways without intruding on walkable ground. The stretched terrain texture still shows there.

**Decision (user chose "rocks + rock-wall shading"):** keep the cladding rocks, and add **rock-wall shading** to the terrain.
- A project copy of URP `Terrain/Lit`, named `LanternKeeper/TerrainLit`, used by every island and the menu terrain material.
- On steep faces (normal.y below about cos 50°, blended over about 10°) the splat result is replaced by the island's rock layer, sampled **triplanar** (world XZ-projected from the side), so it has no vertical stretching.
- The flat-ground look is unchanged.
- Holes, the basemap/distance pass, instancing and Low are kept. Low uses a 2-sample (dominant-axis) projection.
- **Unchanged:** terrain data (heights, alphamaps, holes, details) stays byte-identical. This is shading only.
- **Validation:** Validate Scene Wiring checks that each island terrain uses `LanternKeeper/TerrainLit`. Cliff screenshots show no vertical stripes beside trails.
- Performance stays within Ultra ≤ 12.5 ms.
