# Visual Pass Phase C2: Keeper Outfit Redesign and Detail Levels — Design

Status: approved in conversation on 2026-10-04, pending spec review.

This amends `docs/superpowers/specs/2026-10-03-keeper-lantern-design.md` (Phase C). It is built on the same branch, `phase-c-keeper-lantern`, before Phase C merges.

Everything else in Phase C stands unchanged:
- true scale
- the HandSocket, lantern, flame and pendulum
- the animator layers and event hooks
- footstep dust
- validation, tests and the performance budget

## Why
In the Phase C after-set, the keeper reads as too low-poly:
- **The cloth:** a 6,000-triangle cap, with only about 1,160 triangles for the hood and cloak combined, and flat-shaded faces.
- **The body:** the blocky Quaternius body.

The user chose to drop the long cloak, upgrade the visible body, and spend more triangles on High and Ultra.

## Changes to the Phase A / Phase C keeper rules
- **Silhouette:** a deep hood (face in shadow) and a **short torn shoulder cape** (to about mid-back and the elbows). The satchel and the held lantern stay.
- **The long torn cloak is removed.** This amends Phase A's "long cloak with a torn hem".
- **Budget:** replaces Phase C's "under 6,000 triangles" with two detail levels (below).

## 1. Model (Blender, `ArtSource/Keeper/Keeper.blend`)

### Outfit
- **Hood:** rebuilt deeper and rounder at the higher detail.
  - Inner shell: vertex colour G = 0 (AO).
  - Face: G ≈ 0.14, kept dark through KeeperSkin's `_AOFloor`.
  - The face must read as a dark hollow with at most a faint hint of features, on Low and Ultra.
- **Cape:**
  - a short shoulder cape with a torn, ragged lower edge, weighted to the spine, shoulders and upper arms
  - vertex colours: R = sway weight (edge 1 → collar 0), B = worn edge
  - no geometry below the waist, so nothing to clip against the legs
- **Satchel:** kept, at the higher detail for LOD0.

### Body upgrade
The body is visible now. It keeps the same skeleton and the Quaternius proportions; no new base body.
- **Smooth shading,** with sharp edges marked at real creases: boot soles, cuffs, belt and collar.
- **Tunic and trousers:** one subdivision level, plus fold shaping where it reads at gameplay distance.
- **Hands:** reshaped so the fist around the lantern ring reads as a hand, not a mitten.
- **Boots:** refined.
- **Face:** the existing face; it stays in hood shadow.

### Shading
Smooth shading is used on all outfit and body meshes. Sharp edges mark creases only, which replaces Phase C's flat-shaded cloth.

## 2. Detail levels

| Level | Used on | Triangle target |
|---|---|---|
| **LOD0** | High, Ultra | about 20,000 (cap 25,000) |
| **LOD1** | Low, Medium | about 7,000 (cap 8,000) |

- **One skeleton.** Both levels are skinned meshes on the same `CharacterArmature` in one FBX, named with `_LOD0` / `_LOD1` suffixes. All animations, HandSocket and the lantern are shared.
- **LOD1** is a reduced copy of LOD0, keeping the same silhouette and vertex-colour scheme. Readability rule 4: Low reads the same, with fewer facets.
- **Unity:** a `LODGroup` on the keeper with the two levels.
  - The preset forces the level: a new `GraphicsProfile.keeperLod` field (0 on High and Ultra, 1 on Low and Medium).
  - `KeeperQuality` applies it with `LODGroup.ForceLOD`.
  - The global `QualitySettings.maximumLODLevel` is not used, because it would also affect the scenery LODs.
- **Materials:** the Phase C authored materials are reused:
  - KeeperCloth for the hood and cape
  - KeeperLeather, KeeperSkin and KeeperDark as before
  - LanternIron and LanternGlass unchanged

## 3. Approval
- **Before anything goes into the game,** Blender renders go to the user for approval:
  - LOD0 and LOD1 side by side: front, side, three-quarter, and three-quarter with the lantern
  - plus one LOD0 close-up of the hood and face
- **At the end,** the Phase C after-set is re-captured at Low (LOD1) and Ultra (LOD0) with `KeeperCloseupCapture`, and `phase-c.html` is updated for the user's approval.

## 4. Validation and testing
- **KeeperClipTests:** must still pass. The skeleton and clips are unchanged.
- **Clipping check:** re-run for the cape against the arms and torso, on Idle, Walk, Run, Roll, Interact, HitRecieve, Death and LanternHold. Target ≤ 3 cm in Walk, Run and Interact.
- **Validate Scene Wiring** additions:
  - the keeper has a `LODGroup` with 2 levels
  - every renderer in both levels is skinned to the same armature
  - LOD0 is at most 25,000 triangles and LOD1 at most 8,000
- **EditMode test:** `GraphicsProfile.keeperLod` is 0 for High and Ultra and 1 for Low and Medium.
- **Performance:** the player-build probe on all islands, Ultra and Low. Ultra average ≤ 12.5 ms.
- **Regression:** all EditMode and PlayMode tests; Build All Levels and Validate Scene Wiring with 0 problems; the lantern ≤ 1.5 m.

## Amendment (2026-10-04, user reference images)
The user supplied concept renders (a deep hood with a black void face, a layered scarf/cowl over the shoulders, gloves, a sash). Taken as shape and silhouette references only; the game's stylized look is kept. Changes to Section 1:
- **Cape → a layered draped cowl.** Two or three wrapped fabric layers over the shoulders and upper chest, with a torn lower edge. It ends around the upper chest, the elbows and mid-back, and stays clear of the lantern arm.
- **Face:** a **full black void** inside the hood, with no visible features, on both LODs. The face region gets vertex colour G = 0.
- **Gloves:** dark leather gloves with short cuffs, using the KeeperLeather slot.
- **Sash:** a cloth sash at the waist, with a short tail above mid-thigh, weighted to Hips. Its R sway is on the tail only.
- **Legs stay clear:** no robe or skirt below the waist.

## Out of scope
- A new base body or new proportions.
- New animations.
- Lantern changes.
- Moths, Shades and beacons (Phase F).
