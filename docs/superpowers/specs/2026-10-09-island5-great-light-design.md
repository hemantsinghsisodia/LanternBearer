# Phase H: Island 5 "The Great Light" (Finale) — Design

Status: approved in conversation on 2026-10-09, pending spec review.

**Parents:**
- `2026-10-03-art-direction-design.md`: art direction, signal colours, readability rules and the performance budget.
- `2026-10-08-audio-pass-g-design.md`: the music director, tension layer, stingers and CC0 sourcing workflow.

Concept boards: `.superpowers/brainstorm/1800-1791528913/content/finale-concepts.html` and `island5-layout.html`.

## Goal
Add a fifth and final island where the keeper climbs a storm-ringed mountain to relight a great lighthouse. Light bridges between beacons carry the climb. The storm escalates with height and never relaxes. The game ends with a finale cinematic that flies over all five islands into the game's first sunrise. Islands 1–4 gameplay is unchanged.

## Phasing
- **H1, the playable island:**
  - spire terrain
  - light bridges
  - height rings with ratcheting hazards
  - the thin-air effect
  - content, look and music bed
  - unlock from Island 4
  - a placeholder ending: lighting the great lamp runs the existing dawn and win
- **H2, the lighthouse and finale:**
  - the lighthouse model and lamp ignition
  - the finale cinematic
  - the credits
  - the finale music cue

Each sub-phase gets its own plan, reviews and playable build. This spec covers both.

## 1. Layout and flow (H1)
- **Shape:**
  - island radius about 45 m, summit about 40 m high;
  - a spiral path circling the mountain about 1¾ times from the beach spawn to the summit.
- **Height rings:**

  | Ring | Height |
  |---|---|
  | Low | 0–10 m |
  | Mid | 10–25 m |
  | High | 25–38 m |
  | Summit | above 38 m |

- **Beacons:** 8, all of which must be lit (the usual rule), plus the **great lamp** on the summit.
- **Required gaps (4):** each can only be crossed by a light bridge.
  1. a flooded inlet (low ring);
  2. a tidal chasm (low/mid);
  3. a broken stair (mid/high);
  4. the summit span. It opens only once all 8 beacons are lit, and replaces the usual immediate win.
- **Optional bridges:** 2 shortcut bridges between beacons on different turns of the spiral.
- **Unlock:** Island 4's `nextLevelScene` becomes Island 5. Island 5 appears in the menu island list and the HUD labels as "Island 5 · The Great Light".
- **Length:** about 12–18 min on a first play, about 6–8 min using the shortcuts.
- **Story:** 3 new Keeper's Log pages tell the lighthouse's story, plus intro card lines.

## 2. Mechanics (H1)
- **Required bridges:**
  1. Lighting the beacon on the near side of a gap sends a beam arc (about 1 s) that kindles the far-side **bridge lantern**.
  2. The bridge then grows from both ends over about 1.5 s. Total time from the beacon lighting to a walkable bridge is 2.5 s or less.
- **Optional bridges** form when the beacons at both ends are lit.
- **Bridges as ground and light:**
  - a bridge is solid, walkable ground with invisible side rails, so the keeper cannot fall off;
  - it counts as **safe light**, like a beacon's safe ring: moths do not follow onto it, Shades cannot cross it, and fuel drain is the safe-ring rate;
  - once formed, a bridge is permanent.
- **Summit gate:** when all 8 beacons are lit, a light column rises from each beacon, then the summit span forms. Lighting the great lamp is the win trigger. In H1 it runs the existing dawn and win; in H2 it runs the finale.
- **Ring hazards** use the existing systems with Island 5 settings:

  | Ring | On entering |
  |---|---|
  | Low | tide in the two inlets (Island 3 `Tide`); 3–4 moths; dense fireflies |
  | Mid | wind (Island 4 `Wind`, gentler); 2 Shades patrolling the cliff path; fewer fireflies |
  | High | lightning (Island 4 `Lightning`); **thin air**: lantern light radius −20% and fuel drain +15% |
  | Summit | calm until the great lamp is lit |

- **Ratchet (the user's choice):** the hazard level is the highest ring reached so far. It never goes down, even if the keeper walks back down. Walking back stays possible, so the keeper can never get stuck: missed beacons and low-ring fireflies stay reachable.
- **Unchanged rules:** losing (fuel at 0), Retry, fuel costs, safe rings, the compass and fireflies work as on the other islands.
- **New code:**
  - `LightBridge` (beacon pair, far lantern, grow animation, walkable collider, safe-light zone);
  - `RingZones` (a height check feeding the ratchet; pure Logic in `RingLogic`);
  - `ThinAir` (light radius and drain multipliers through existing Lantern hooks);
  - `SummitGate`;
  - `GreatLamp` (the win trigger).

## 3. Look
- **Low ring:** dark wet rock, kelp-green grass and tide pools in a cold teal palette, darker than Island 3.
- **Mid ring:** wind-bent heather, ochre and slate cliffs, and wind-sculpted pines.
- **High ring:** bare black crags with frost-blue moon-rim highlights. This is the darkest place in the game.
- **Cliffs:** D2 cliff skin and cladding, taller. Each required gap gets a hand-placed set piece:
  - a sea arch;
  - a split chasm;
  - a collapsed stone stair;
  - a knife-edge summit ridge.
- **Sky and storm:**
  - a cloud wall circling the island far out at sea, lit from within by distant lightning and visible from every ring;
  - as the keeper climbs, the clouds lower and thicken, the moon is hidden, and the fog shifts from teal to a cold violet;
  - this is driven by the ratchet level through the island `LookProfile`.
- **Light bridges:**
  - translucent amber planks of light (`#FFB15C`) with a brighter core line (`#FFD38A`);
  - drifting ember motes;
  - a soft glow on the rock below.
- **Lighthouse (H2):**
  - a stone tower about 14 m tall, with an iron gallery and a caged lantern room, modelled in Blender in the F1 beacon style;
  - lit: amber panes and a **rotating beam** (scaled-up F1 beam technique) sweeping the sea.
- **Rules:**
  - Phase A rules apply: the warm/cold rule covers lights only, and signal colours are not reused.
  - The finale sunrise is the single deliberate exception: warm sky and sea.

## 4. Music
- **Island 5 slots in `MusicLibrary`:**
  - island track: brooding, building, a step darker than Island 4;
  - tension loop;
  - summit hush: a quiet sustained drone or held chord;
  - finale cue (H2): about 60–90 s, hopeful and swelling.
- **Ring tension floor:** `MusicDirector` keeps the tension layer at a minimum level for the highest ring reached:

  | Ring reached | Tension floor |
  |---|---|
  | Low | 0 |
  | Mid | 0.3 |
  | High | 0.6 |

  The floor ratchets like the hazards. Danger can push tension above the floor, never below it.
- **Summit:** track and tension fade out under the summit hush. Lighting the great lamp starts the finale cue (H2), or the existing win stinger (H1).
- **Sourcing:** as in Phase G. A CC0 shortlist page offers 2–3 candidates per slot. The user picks. Only the picks are downloaded, then loudness-matched and credited.
- **Finale cue fallback:** if no CC0 candidate fits, reuse the menu track (First Light Particles).
- **Sub-phase split:** H1 delivers the track, tension and hush. H2 delivers the finale cue.

## 5. Finale cinematic (H2)
1. **Great lamp ignition:** a scaled-up F1 lighting moment, with flare, embers and a deep chord.
2. **Beam and storm:** the rotating beam sweeps out, the storm ring tears open, and the camera pulls out and up.
3. **The earlier islands:** distant silhouettes of Islands 1–4 appear on the horizon. Their beacons flare one island after another, in time with the finale cue. These are low-poly stand-ins built from each island's terrain, not loaded scenes.
4. **Sunrise:** the first sunrise in the game. The sky warms from violet to gold, and the sea catches the light.
5. **Credits:** a short credits roll over the dawn. It is skippable with any key or button and returns to the main menu. Credits list the CC0 sources from `CREDITS.txt`.

- **Length:** about 45–60 s before the credits.
- **Unscaled time:** the sequence runs on unscaled time.
- **Gameplay:** input is disabled while it plays.

## 6. Testing and validation
- **EditMode tests:**
  - spire generation is deterministic from its seed;
  - ring height bands and ring classification are correct;
  - `RingLogic` ratchet: the level never goes down;
  - thin-air values are exactly −20% radius and +15% drain;
  - the summit gate stays shut below 8 lit beacons;
  - bridge geometry spans each gap;
  - Island 5 music slots are complete;
  - the tension floor values are 0, 0.3 and 0.6.
- **PlayMode tests:**
  - lighting a gap's near beacon gives a walkable bridge within 2.5 s;
  - the bridge counts as safe light (no moth follow, Shades blocked);
  - the summit opens only after 8 beacons;
  - ring hazards stay on after walking back down;
  - the Island 4 win panel offers "Next island" leading to Island 5;
  - (H2) the finale sequence completes and returns to the menu, and the skip works.
- **Reachability:** `BeaconReachabilityTest` is extended to Island 5, including bridges and the summit. Every beacon and the great lamp must be reachable within the allowed jumps.
- **Validate Scene Wiring:** Island 5 is checked like Islands 1–4, plus the bridges are wired (near beacon, far lantern, collider) and the great lamp exists.
- **Performance:**
  - one probe pass on Island 5, Low and Ultra, against the average of Islands 1–4 in Phase G, limit +0.3 ms;
  - Islands 1–4 are re-probed once, so no regression;
  - if anything fails, report the numbers before investigating.
- **User checkpoints:**
  1. H1 midway: screenshots and a flythrough of the generated mountain, before hazards and bridges.
  2. H1 end: a playable build of Island 5, start to finish.
  3. H2 midway: the lighthouse model and lamp ignition.
  4. H2 end: the full finale in a release build, approved before merge.

## Out of scope
- New enemy types.
- New keeper abilities.
- Gameplay changes to Islands 1–4, apart from Island 4 now unlocking Island 5.
- Voice acting.
- Save or continue systems.
