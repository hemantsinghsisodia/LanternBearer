# Phase H4: Faster Storm, Richer Finale (Design)

Status: approved in conversation on 2026-10-13.

Parents: `2026-10-13-h3-polish-design.md` and `2026-10-09-island5-great-light-design.md`. This addendum overrides them where stated.

## Decisions (user)
1. **Rising storm (Island 5).**
   - Rise 1.4 m/s (was 1.0).
   - Beacon push-back 4 m (was 3). If a full-pace climber is caught at 4 m, it becomes 5 m.
   - Unchanged: the 8 s start delay, the ×3 caught drain and the cap.
   - A caught keeper recovers by lighting the next beacon. Out-climbing the surge is no longer guaranteed.
2. **The finale gets four upgrades:**
   - **Detailed islands.**
     - Islands 1–4 are miniatures, not flat silhouettes: 128×128 terrain in each island's own palette, their largest trees and rocks, and real beacon models that light with flame, glass glow and beams.
     - At most 25k triangles per island.
   - **Dramatic clouds.**
     - Layered soft cloud banks break open from the beam bearing.
     - Edges catch the sunrise, undersides stay violet.
     - God rays over the sea; none on Low, and softened under Reduce Flashing.
   - **Sea and sunrise.**
     - A deep blue-teal sea with a gold sun path.
     - A visible sun disc and glow.
     - Finale-only colour grading: warm balance, mild bloom and a slight vignette.
   - **Cinematic camera.**
     - A spline flight: from the lighthouse, past each island as its beacons flare, settling on the lighthouse against the rising sun.
     - The finale becomes about 66 s before the credits. That replaces the 45–60 s range in the parent spec.

     | Time | Phase |
     |---|---|
     | 0–6 s | Ignition |
     | 6–18 s | Beam and storm break |
     | 18–42 s | Island flyby (flares at 21, 27, 33 and 39 s) |
     | 42–60 s | Sunrise |
     | 60–66 s | Hold |
     | 66 s | Credits |

## Unchanged
- Islands 1–4 gameplay, scenes and terrain hashes. The baking only reads their data.
- The pre-dawn wins on Islands 1–4.
- The credits roll (75 s or less) and its skip rules.

## Performance
- One probe on Island 5 at spawn against H3 (Low 3.11 ms, Ultra about 10.0 ms), with a +0.3 ms limit. If it fails, report the numbers before investigating.
- Plus one report-only Ultra measurement at a finale pose.
