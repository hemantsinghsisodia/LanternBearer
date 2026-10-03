# Visual Pass Phase C: Keeper and Lantern — Design

Status: approved in conversation on 2026-10-03, pending spec review.

Parent spec: `docs/superpowers/specs/2026-10-03-art-direction-design.md` (Phase A). Its rules are binding here:
- the warm/cold split
- the signal colours
- the hooded-wanderer keeper
- the iron storm lantern
- readability rules 1–4
- the performance budgets

Phase B (`2026-10-03-night-lighting-design.md`) is in place. The lantern light, halo, moon rim and look volumes from Phase B are reused, not replaced.

## Goal
Give the player character its final look and motion:
- a true-scale keeper (1 unit = 1 m), with every ×93.22 workaround removed
- a hooded-wanderer outfit: a hood, a long torn cloak and a satchel
- an iron storm lantern hanging from a hand socket and swinging like a pendulum, with a flame that shrinks and reddens as fuel drops
- animations driven by real game events: the lantern held up, a lean into gusts, a stagger on a Shade steal, the beacon-lighting gesture, the lantern dying on a loss, and a shake-off after a water rescue
- polish: cloak sway, a rim on the lantern iron, footstep dust at the feet

### Constraints
- **Gameplay unchanged:**
  - movement and collision
  - the lantern's reveal radius (`Lantern.Radius`, 3.5–14 m) and intensity curve
  - the lantern stays within 1.5 m of the keeper
- **Scope:** keeper and lantern only. Moths, Shades and beacons are Phase F.

### Done means
- Before/after keeper close-ups are approved.
- Validation confirms there are no scale workarounds.
- Animations fire from game events.
- Tests and the smoke test pass.
- Performance is within budget.

## Current state (for reference)
- **Model:**
  - `Assets/Game/Models/Keeper/Keeper.fbx`, a Quaternius low-poly model with a Generic rig (no humanoid IK).
  - `CharacterArmature` has scale 93.22.
  - 24 clips: Death, Gun_Shoot, HitRecieve, HitRecieve_2, Idle, Idle_Gun, Idle_Gun_Pointing, Idle_Gun_Shoot, Idle_Neutral, Idle_Sword, Interact, Kick_Left, Kick_Right, Punch_Left, Punch_Right, Roll, Run, Run_Back, Run_Left, Run_Right, Run_Shoot, Sword_Slash, Walk, Wave.
- **`Keeper.controller`:** one layer. The "Locomotion" blend tree is driven by `Speed`, `Grounded`, `VerticalSpeed` and `Jump`.
- **Lantern build:** `IslandBuilder.Keeper.AttachLantern` builds `LanternPivot` under `Wrist.R`:
  - a cube body and a cap
  - its offset and scale are divided by `hand.lossyScale` (the ×93 workaround)
  - `LanternLight`, the `Lantern` component and the Phase B `LanternHalo`
- **Halo sizing:** `LanternHalo` compensates its size by `parent.lossyScale`.
- **Procedural animation:** `KeeperAnimator` falls back to procedural swing and squash drives when there is no controller.
- **Validation:** Validate Scene Wiring checks the lantern is ≤ 1.5 m from the player. Today it is 1.04 m.

## 1. True-scale re-export, outfit and materials

### True scale
- **Blender source:** a single file, `ArtSource/Keeper/Keeper.blend`, following the `ArtSource/Shade/Shade.blend` precedent.
- **Re-export steps:**
  1. Import the current FBX with all 24 clips.
  2. Apply the armature scale so the skeleton is 1 unit = 1 m.
  3. Re-export `Keeper.fbx` with the animations baked.
- **Import settings:** Unity imports it at global scale 1, Generic, with clips and loops set by `PrepareKeeperImport` as today.
- **Removing the workarounds:**
  - the scale division in `AttachLantern`
  - the `lossyScale` compensation in `LanternHalo`
  - the ×93 compensations in `KeeperAnimator`'s procedural fallback
  - any placement in the builder or `KeeperQuality` (chest fill included) that scales by `lossyScale`
- **Clip check:** the re-export risks corrupting clips. Every clip in use is compared before and after:
  - clips in use: Idle, Walk, Run, the jump set, Roll, Interact, HitRecieve, HitRecieve_2 and Death
  - what is compared: clip length (equal), and bone rotations at sampled frames (similar)

### Outfit
Modeled in Blender over the existing body and skinned to the existing bones:
- **Hood:** deep, with a dark hollow for the face opening so the face is lost in shadow. It follows the head bone.
- **Cloak:**
  - long, with a torn, ragged hem
  - weighted to the spine and shoulders, with the lower strips blending toward the legs
  - a vertex colour marks the sway weight (hem = 1, shoulders = 0)
- **Satchel:** on a strap across the body, on the hip opposite the lantern hand.
- **Body:** the visible hands and lower legs keep the existing mesh, recoloured.
- **Budget:** under about 6k triangles for outfit plus body.

### Materials
- **Colours:**
  - cloak and hood: deep desaturated slate-blue cloth, with a slightly lighter worn edge
  - satchel and strap: dark leather
  - hands: muted skin
- **Shader:** the existing `LanternKeeper/KeeperLit`, extended with the cloak sway term (Section 3) and keeping its rim term.
- **Lighting:** no new lights. The keeper is lit by the lantern (warm, on the lit side), the moon and moon rim (cool), and the existing chest fill.

### Approval checkpoint
Before the outfit and lantern go into the game, show Blender renders for approval:
- front, side and three-quarter views
- one view with the lantern held

## 2. Storm lantern, socket and flame

### Model
- **Source:** `ArtSource/Lantern/Lantern.blend`.
- **Export:** `Assets/Game/Art/Lantern/Lantern.fbx`.
- **Shape:** a square forged-iron frame, four glass panes, a peaked roof cap and a ring handle.
- **Size and budget:** about 0.28 m tall, under about 800 triangles.
- **Approval:** included in the Section 1 render checkpoint.

### Holding it
- **Socket:** a `HandSocket` empty under `Wrist.R`, exported in the keeper FBX and placed at the closed fist.
- **Hierarchy:**
  ```
  HandSocket
    LanternPivot          (at the ring handle; the pendulum pivot; LanternSway)
      Lantern mesh        (iron frame, glass, cap)
        Flame             (Flame shader)
        LanternLight      (Light + Lantern component, as today)
        LanternHalo
  ```
- **Swing:** the existing `LanternSway` drives the pendulum from the keeper's movement. The angle is clamped so the lantern never clips the leg or cloak.
- **No hand IK:** the authored arm pose (Section 3) places the fist.
- **Builder:** parents to the socket with no magic offsets and no scale division.

### Flame and light
- **Flame:** a small flame mesh using the existing `LanternKeeper/Flame` shader.
  - **Height:** `lerp(0.45, 1.0, FuelNormalized)` of full height.
  - **Colour:** from the glow core `#FFD38A` at full fuel to a deep red-orange near empty.
  - **Mapping:** a pure function in `LanternKeeper.Logic` so it can be tested.
- **Glass:** the panes get a soft emissive glow that pulses with the existing `LanternFlicker`.
- **Light:** `LanternLight` moves to the flame.
  - Its colour, intensity curve and range are unchanged.
  - `Lantern`, `Radius`, `BaseIntensity` and `FuelNormalized` keep their meaning, so `LightRevealed`, Shades, moths and the HUD are unaffected.
- **Halo:** the Phase B halo moves to the flame and scales and fades with fuel.

### Rim and presets
- The iron frame and cap use the `KeeperLit` rim term, so the lantern's outline reads at low flame.
- **Every preset** gets the flame, the glass glow and the halo. They are unlit meshes with no extra lights.
- The rim follows the existing `KeeperQuality` per-preset keyword.

## 3. Animations, event hooks and polish

### Authored poses (Blender, on the existing skeleton)
- **LanternHold:** the right arm held out with the fist closed around the ring. A single-frame looping clip.
- **Lean:** the spine, neck and head bent into the wind. A single-frame additive clip.

### Animator layers (`Keeper.controller`)
1. **Base:** the existing locomotion blend tree and jumps, unchanged.
2. **LanternArm:**
   - masked to the right shoulder, arm and hand
   - plays LanternHold at weight 1 through idle, walking and running
   - blends to 0 only for the beacon gesture
3. **Lean:**
   - additive, masked to the spine, neck and head
   - its weight is a pure function `LeanWeight(phase, strength01, sheltered)` in Logic:
     - `strength01` while the phase is `Gust`
     - 0 while sheltered or outside a gust
   - eased over about 0.3 s
4. **Actions:** a full-body override layer for one-shot clips, driven by triggers.

### Event mapping

| Animation | Trigger | Clip / effect |
|---|---|---|
| Stagger | `Shade.Stole` (existing static event) | `HitRecieve`, plus an extra swing impulse on `LanternSway` |
| Beacon lighting | new static `Beacon.Lit`, raised on a successful `TryLight` | `Interact`; LanternArm blends to 0 for the reach |
| Lantern dies | new `GameManager.DeathStarted`, raised in `OnFuelDepleted` when the death sequence starts (`LostGame` fires too late, after the sequence) | `Death`; the flame gutters out over the clip, the halo fades, the light drops to its existing minimum |
| Water rescue | new `WaterHazard.Rescued`, raised after the fade-in | `HitRecieve_2` as a short shake-off; the existing splash VFX and audio stay |

**How the events are wired:**
- `KeeperAnimator` subscribes to these events and only sets animator parameters.
- Events are unsubscribed in `OnDisable`.

**No gameplay timing changes:**
- Death and rescue already lock controls.
- The beacon gesture affects the upper body only and does not block movement.

### Polish
- **Cloak sway:** a vertex-displacement term in `KeeperLit`.
  - Weighted by the painted vertex colour.
  - Driven by keeper speed and the global wind value.
  - On every preset.
- **Lantern rim:** the rim term on the lantern iron (Section 2).
- **Footstep dust:** the existing dust, emitted at the foot bones on the existing `OnFootstep` events.

### Procedural fallback
`KeeperAnimator`'s procedural drives stay only as the fallback when there is no controller. Their ×93 compensations are removed.

## Validation and testing

### Validate Scene Wiring additions
- The keeper armature and every keeper transform have scale 1, within 0.001.
- `HandSocket` exists, and the `Lantern` component lies under it.
- The lantern is within 1.5 m of the keeper (existing check).
- `Keeper.controller` has the LanternArm, Lean and Actions layers.

### EditMode tests
- `LeanWeight`: calm, warning, gust and sheltered map to the expected weights.
- The flame mapping:
  - fuel 0 → 45% height, red-orange
  - fuel 1 → full height, `#FFD38A`
  - it is monotonic, and the height stays within [0.45, 1]
- The re-export clip check: each clip in use matches its pre-export length, and sampled bone rotations are within tolerance. The reference data is captured from the old FBX before it is replaced.

### PlayMode
- The existing smoke test passes.
- A new test raises each event and asserts the matching animator parameter or trigger fired:
  - `Shade.Stole`
  - `Beacon.Lit`
  - death start
  - `WaterHazard.Rescued`

### Captures
- **New keeper shots** in the look-capture tool:
  - front, three-quarter and back views
  - idle, gust and low fuel
  - Low and Ultra
- **Before set:** taken before any model change.
- **After set:** taken at the end.
- **Comparison page:** `docs/look/compare/phase-c.html`, for user approval.

### Performance
- The player-build `-lkperf` probe runs on all islands, Ultra and Low.
- Gate: Ultra average ≤ 12.5 ms at 1080p. p95 is tracked.
- The Low iGPU run stays a user step.

### Regression
- Build All Levels and Validate Scene Wiring report 0 problems.
- All existing EditMode tests pass.
- Movement, collision, reveal radius, dawn, lightning, low fuel and water rescue are verified in play mode.

## Out of scope
- Moth, Shade and beacon art (Phase F).
- New climbing or swimming clips.
- Humanoid retargeting or IK packages.
- Environment changes (Phase D).
- UI changes (Phases E and F), including the HUD lantern icon.
