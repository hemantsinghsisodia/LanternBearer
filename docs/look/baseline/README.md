# Look baseline captures

Each dated folder is one "before" set for the visual pass: world shots on every island and the UI screens, on Low and Ultra, at 1920x1080 (JPEG q92).

## How to run
1. `Lantern Keeper/Look/Generate Shot Lists` (only needed once or after a level rebuild; it asks for a Regenerate confirmation if lists exist).
2. Leave play mode, save open scenes, then run `Lantern Keeper/Look/Capture Look Baseline`. It writes `docs/look/baseline/<today>/`.
   - It switches the Game view to a fixed 1920x1080 size, opens each scene in play mode, and puts every editor setting back afterwards (graphics level, PlayerPrefs, timeScale, runInBackground, scene, Game view size).
   - Output goes to `<folder>.tmp` first and replaces the old folder only when the whole run succeeds, so a stopped or failed run never touches an existing baseline.
   - `Lantern Keeper/Look/Abort Capture` stops a run. It also restores the editor from a saved snapshot if an earlier editor session died mid-run (the snapshot lives in `Library/LookCapture/`).
   - Pressing Stop mid-run, an exception, or a phase running over 180 s aborts and restores.
   - A run takes about 4 minutes. Keep the editor open; do not use it meanwhile.
3. `Run(string outputDir)` can capture into any folder, for example to compare two runs.

## Noise floor between runs
Game time is pinned (fixed 1/60 s steps, then time stopped) while the static shots are taken.
- Static shots (spawn, beacon, shoreline, cliff, water, grass) and the main menu / journal / HUD / pause screens: mean absolute pixel difference between two runs is under 0.01 / 255, effectively identical.
- Moth and Shade close-ups differ by up to about 6 / 255. Those shots need game time to run until the prop spawns, so water, grass and the prop pose move slightly.

## Notes
- The frozen-moth shot has no glow light, by design. The moth is frozen with `enabled = false`, and its glow slot is released when it is disabled.
- The Shade shot places the Shade 4 m ahead of the listed pose and backs the camera 3 m off, so the whole head is in frame.
- Frame times in `frametimes.md` are Unity editor numbers (Game view, editor overhead, with `runInBackground`). They are not player-build numbers. Use them only to compare editor runs of the same machine.
