# Phase H3: Playtest Feedback, Island 5 Challenge, Dawn Progression (Design)

Status: approved in conversation on 2026-10-13.

Parent: `2026-10-09-island5-great-light-design.md`. This addendum changes that spec where stated.

## Why
The user played the merged H2 build and raised five points:
- Island 5 shows a second, lit lighthouse.
- The menu shows 3 of 8 beacons lit after a full Island 5 win.
- The beacon fire whoosh needs replacing.
- Island 5 is the easiest island, which is wrong for a finale.
- Every island ends in a dawn, so the finale sunrise isn't special.

## Decisions (user)
1. **Menu lit count.** The roofs in each island row show the most beacons ever lit on that island. A won island shows all of them. The Keeper's Log keeps its own page count.
2. **The horizon lighthouse.**
   - Islands 1–4 show the Great Light's tower (the H2 lighthouse model), dark, with the cold unlit glint. There is no beam: the storm put it out.
   - Island 5 has no scenery lighthouse.
   - The main menu's lighthouse stays dark until Island 5 is won, then it is lit with its beam turning.
3. **Growing pre-dawn.** Island 1–4 wins blend from night only part of the way to dawn. There is no sun, the stars dim but stay, and the horizon glows.

   | Island | Reach |
   |---|---|
   | 1 | 0.15 |
   | 2 | 0.25 |
   | 3 | 0.35 |
   | 4 | 0.50 |

   The Island 5 finale has the game's only full sunrise. This replaces the "existing dawn" on the Island 1–4 wins.
4. **The beacon fire whoosh** (the `SoundCues.BeaconIgnite` variants) is replaced.
   - The user picks from a CC0 shortlist: Kenney, OpenGameArt or BigSoundBank. No Freesound.
   - Loudness −18 LUFS, true peak ≤ −1 dBTP.
   - The stingers are unchanged.
5. **Island 5 challenge.** There are three additions, all Island 5 only.
   - **Rising storm.**
     - A dark surge rises from the sea, starting 8 s after the first beacon is lit (retuned at the midway gate).
     - Its pace keeps a full-pace climber about 8–12 m ahead (a 70%-pace climber is caught before the summit).
     - Each beacon lit pushes it down 3 m.
     - It stops at the base of the summit plateau.
     - **Inside the surge:**
       - fuel drains ×3;
       - dark fog, a heartbeat and lightning flicker;
       - beacon safe rings don't protect;
       - bridges are still ground.
     - A HUD chip reads "Storm rising: N m below".
     - The music tension floor rises as the surge closes in.
     - Retry resets it.
   - **Lightning hunts you.**
     - From the Mid ring up (Mid, High, Summit), before the lamp is lit.
     - Every 4–6 s a strike targets a point 2–4 m from the keeper's predicted position, after a 1.0 s glowing warning ring on the ground.
     - A hit costs 15% fuel (never below 1 point left) and knocks the keeper back 3 m, shortened or flipped at a drop so it never throws the keeper off the path.
     - Bridges and lit-beacon safe rings are immune.
   - **Scarce fuel.**
     - Firefly density ×0.5.
     - Lighting a beacon refunds 20% of the beacon's fuel cost (3 of 15 on Normal).
     - The Island 5 lantern drains x1.6 (LevelConfig.drainMultiplier).
   - **Balance.**
     - On Normal, a bot that climbs the reachability route at walking pace must reach the lamp, and the surge must never catch it.
     - Its fuel must never reach 0, and it must arrive at the lamp with 25–40% fuel (scarce, not impossible), measured without the random moths and Shades.
     - A bot that waits 60 s at the beach must be caught.

## Unchanged
- The unlock chain (Island 5 needs an Island 4 win). It already holds; the user's save had Island 4 won.
- Islands 1–4 gameplay and terrain (byte-identical hashes). Only the win's look and the horizon lighthouse change.

## Performance
One probe pass on Island 5, Low and Ultra, against the H2 numbers (Low 2.85 ms, Ultra 10.03 ms). The limit is +0.3 ms. If it fails, report the numbers before investigating.
