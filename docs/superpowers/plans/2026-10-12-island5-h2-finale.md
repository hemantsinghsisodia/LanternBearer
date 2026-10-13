# Island 5 "The Great Light", H2 (Lighthouse and Finale): Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Island 5's placeholder ending with:
- the lighthouse on the summit;
- a finale cinematic, about 52 s: ignition, the beam and the storm breaking, Islands 1–4 lighting up, the first sunrise;
- a skippable credits roll that returns to the main menu;
- a CC0 finale music cue.

**Architecture:**
- **Pure Logic.** `FinaleTimeline` holds the phase timings and blends. `CreditsFormat` turns `CREDITS.txt` into roll entries. `FinaleLayout` places the island silhouettes.
- **Blender script.** It builds the lighthouse FBX in the same style as `beacon_build.py`. The editor installers place it, the beam, the storm ring, the silhouettes, the finale and the credits.
- **The finale hooks into the existing win the way the dawn does.** `GameManager.Win()` calls `FinaleSequence.Play(FinishWin)` when a finale is wired, instead of the dawn.
- **Then the credits.** The HUD then shows the credits roll in place of the result panel.

**Tech Stack:** Unity 6000.6.3f1, URP, C#, NUnit (EditMode and PlayMode), Unity MCP, Blender 4.x in background mode (`blender.exe -b -P`), ffmpeg and Python for audio, WebFetch for music research.

**Spec:** `docs/superpowers/specs/2026-10-09-island5-great-light-design.md`, which is the authority. This plan covers the H2 items:
- §3 Lighthouse and Light bridges, look;
- §4 finale cue;
- §5 Finale cinematic;
- the H2 items of §6.

## Global Constraints
- **Islands 1–4 and the menu are unchanged.** Their gameplay, scenes and terrain stay byte-identical (the hash tests must pass). Never rebuild Islands 1–4. The Island 1–4 win still plays the dawn, then the result panel.
- **Lighthouse:**
  - a stone tower about **14 m** tall, with an iron gallery and a caged lantern room;
  - modelled in Blender in the F1 beacon style: dry stone, dark iron, amber glass;
  - lit, it shows amber panes and a **rotating beam**, made with the F1 beam technique (`LanternKeeper/BeaconBeam` cards) scaled up, sweeping the sea.
- **Finale order:**
  1. great lamp ignition: flare, embers, deep chord;
  2. the beam sweeps out, the storm ring tears open, and the camera pulls out and up;
  3. Islands 1–4 appear as distant silhouettes, and their beacons flare one island after another, in time with the cue;
  4. sunrise: the sky warms from violet to gold and the sea catches the light;
  5. the credits.
- **Timing:** **45–60 s** before the credits. Everything runs on **unscaled time**. Gameplay input is off throughout.
- **Silhouettes:** low-poly stand-ins built from each island's `IslandN_Terrain.asset`. Do not load other scenes.
- **Credits:**
  - a short roll over the dawn;
  - skippable with **any key or button**;
  - returns to `MainMenu`;
  - lists the CC0 sources from `CREDITS.txt`.
- **The sunrise is the game's only deliberate warm sky and sea.** Otherwise the Phase A rules hold. Signal colours: amber `#FFB15C`, core `#FFD38A`.
- **Finale cue:**
  - CC0 only, from OpenGameArt, Kenney or BigSoundBank. No Freesound.
  - about **60–90 s**, hopeful and swelling.
  - shortlist with 2–3 candidates; the user picks, and only the pick is downloaded;
  - −23 LUFS, true peak ≤ −1 dBTP;
  - fallback: the menu track `Music_Menu_1.ogg` (First Light Particles).
- **Protected files:**
  - Never edit, revert or commit `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`.
  - Leave `Assets/Settings/Build Profiles/` untracked.
  - Don't commit the `ProjectSettings.asset` build side effect.
- **Noise:**
  - Revert with `git checkout`: TMP font SDF assets, the Liberation fallback, `EditorSettings` and `Assets/InitTestScene*.unity`.
  - Never `git checkout` scenes or source files.
  - Never text-edit a loaded scene.
  - Restore PlayerPrefs `LanternKeeperGraphics` to 0. Don't touch `LanternKeeperIntro_*` or the player registry.
- **Perf:** one probe pass on Island 5, Low and Ultra, against the Island 5 H1 re-measure in `docs/look/perf/2026-10-09-island5-h1.md`, limit **+0.3 ms**. Same build settings and render scale as that run. If it fails, report the numbers before investigating.
  - Ruling: the H1 result was accepted, so H2 is measured against it, not against the Islands 1–4 average.

## Review Focus
1. **A key is held or mashed while the lamp is lit** (E is still down). The credits must not skip at once. The skip is armed only after a key-up and a 1.0 s guard. Test: Task 5 `HeldKeyDoesNotSkipCredits`.
2. **Escape or pause during the finale.** Pause must stay blocked, the finale must not freeze or desync, and time-scale tricks must not change its length. Test: Task 4 `FinaleIgnoresPauseAndTimeScale`, which runs at `Time.timeScale = 0` and must still finish.
3. **The lamp is lit on the Low preset, or at a 4K render scale.** The beam, silhouettes and storm ring must render, with no missing-shader magenta, and the Low beam must use the `BeaconVisual` Low scales. Test: Task 4 `FinaleObjectsHaveShaders`, with every renderer's shader `isSupported`.
4. **The player wins Island 5 a second time** (replay). The finale and credits play again, `MarkWon` and the best time still record, and the menu shows Island 5 as won. Test: Task 5 `FinaleRecordsBestAndWon`.
5. **The credits end while the finale cue is still playing.** Going back to the menu must stop the cue and start the menu track, with no double music. Test: Task 5 `ReturnToMenuStopsFinaleCue`.

---

### Task 1: Pure logic (`FinaleTimeline`, `FinaleLayout`, `CreditsFormat`)

**Files:**
- Create: `Assets/Game/Scripts/Logic/FinaleTimeline.cs`, `FinaleLayout.cs`, `CreditsFormat.cs`
- Test: `Assets/Game/Tests/EditMode/FinaleTimelineTests.cs`, `CreditsFormatTests.cs`

**Interfaces (produces):**
```csharp
public enum FinalePhase { Ignition = 0, BeamAndStorm = 1, Islands = 2, Sunrise = 3, Credits = 4 }
public static class FinaleTimeline
{
    // Unscaled seconds from the lamp being lit. The phase starts are fixed; credits begin at Total.
    public const float IgnitionStart = 0f, BeamStart = 6f, IslandsStart = 18f, SunriseStart = 36f, Total = 52f;
    public const float SunriseLength = 16f;
    public static FinalePhase PhaseAt(float t);          // < 0 → Ignition, ≥ Total → Credits
    public static float IslandFlareTime(int island);     // island 0..3 = Islands 1..4: 20, 24, 28, 32
    public static float Phase01(float t, FinalePhase p); // 0..1 progress inside phase p, clamped
    public static float CameraHeight(float t);           // smooth: 6 m at 0 → 120 m by IslandsStart, held after
}
public static class FinaleLayout
{
    public const float SilhouetteDistance = 520f;        // metres from the Island 5 centre
    public static Vector2 SilhouetteXZ(int island);      // bearings 200°, 245°, 290°, 335° (one arc the camera faces)
    public static float SilhouetteScale(int island);     // 1.0 for every island (true size; distance does the work)
}
public struct CreditEntry { public string heading; public string title; public string author; public string licence; }
public static class CreditsFormat
{
    // Reads CREDITS.txt text. Section rules (`---` lines + a heading line) become headings. Each block of
    // "Assets/… path / Title by Author / URL / Licence: X" lines becomes one entry. Paths and URLs are dropped.
    public static List<CreditEntry> Parse(string raw);
}
```

- [ ] **Step 1: Write the failing tests.**
  - `PhasesInOrder`:
    - PhaseAt(0) = Ignition, PhaseAt(5.9) = Ignition, PhaseAt(6) = BeamAndStorm;
    - PhaseAt(18) = Islands, PhaseAt(36) = Sunrise, PhaseAt(52) = Credits.
  - `LengthInSpec`: `Total` is between 45 and 60 inclusive.
  - `FlaresInsideIslandsPhase`:
    - every `IslandFlareTime(i)` is in [IslandsStart, SunriseStart);
    - the times strictly increase;
    - the values are exactly 20, 24, 28 and 32.
  - `CameraRisesThenHolds`: CameraHeight(0) = 6, CameraHeight(18) = 120, CameraHeight(40) = 120, and it is monotonic over [0, 18].
  - `SilhouettesOnTheHorizon`: each `SilhouetteXZ` has magnitude 520 ± 0.01, and the bearings are 45° apart.
  - `ParsesRepoCredits`:
    - parse the real `CREDITS.txt` (read from the project root);
    - there is at least one entry with title "First Light Particles" and author "Yoiyami";
    - no entry field contains "http" or "Assets/";
    - every entry's licence starts with "CC0".
  - `ParseIgnoresBlankAndUnknownLines`: garbage lines produce no entries and don't throw.
- [ ] **Step 2:** Run the tests. Expected: FAIL (the types don't exist yet).
- [ ] **Step 3:** Implement the three classes. `CameraHeight` uses SmoothStep. The bearing is measured from +Z, clockwise.
- [ ] **Step 4:** Run the tests, then the full EditMode suite. Expected: all pass.
- [ ] **Step 5:** Commit: `feat: finale timeline, silhouette layout and credits parser`.

---

### Task 2: Lighthouse model and lamp ignition (then the midway gate)

**Files:**
- Create: `ArtSource/Blender/lighthouse_build.py`. It writes `ArtSource/Blender/Lighthouse.blend` and exports `Assets/Game/Art/Lighthouse/Lighthouse.fbx`.
- Create: `Assets/Game/Editor/LighthouseInstaller.cs`, with menu item `Lantern Keeper/Install Lighthouse (Island 5)`.
- Create: `Assets/Game/Scripts/GreatLampBeam.cs`.
- Modify: `Assets/Game/Scripts/GreatLamp.cs`: `interactRange` becomes serialized at 4.5 for the lighthouse.
- Modify: `Assets/Game/Editor/IslandBuilder.Spire.cs`: `Island5PostSteps` gets `LighthouseInstaller.Install` after `BridgeInstaller` and before `Island5PerfInstaller`.
- Test: `Assets/Game/Tests/EditMode/LighthouseModelTests.cs`, `Island5BuildChainTests.cs` (new step order).

**Model (the script is the source of truth, written like `beacon_build.py`):**
- Blender Z is up. The origin is the centre of the tower base on the ground. 1 unit = 1 m.
- **Tower:** a tapered dry-stone cylinder, base radius 2.2 m and top radius 1.6 m, from 0 to 10.5 m. It has a door recess facing −Y (Unity −Z) and two slit windows.
- **Gallery:** an iron deck at 10.5 m, radius 2.3 m, with a railing 1.0 m high.
- **Lantern room:** 10.6–13.2 m, 8 iron mullions and 8 glass panes. It has a dome cap to 14.0 m with a vent ball.
- **Objects and material slots:**

  | Object | Material slot | What it is |
  |---|---|---|
  | `LighthouseStone` | `BeaconCairn` | the tower |
  | `LighthouseIron` | `BeaconIron` | gallery, mullions and cap |
  | `BeaconGlass` | `BeaconGlass` | the 8 panes, UV 0..1 per pane |
  | `LighthouseStone_L1`, `LighthouseIron_L1` | as above | LOD1 meshes |
  | `FlameRoot` | none | an empty at 11.9 m |

  The slots reuse the beacon materials. The `BeaconGlass` and `FlameRoot` names are kept so `GreatLamp` finds them unchanged.
- **Budget:** LOD0 ≤ 6k triangles and LOD1 ≤ 1.5k.

**Installer:**
- Replaces the `GreatLamp` object's visuals with the lighthouse at the `GreatLampSlot`, rotated so the door faces the end of the summit span.
- Moves the `GreatLamp` component, its `SummitGate` link and the point `Light` onto the lighthouse root. The light goes to the `FlameRoot` height.
- Adds a mesh collider on the stone only. The door recess is not walk-in.
- Adds `GreatLampBeam`: two opposed `BeaconBeam` cards, 3.5 m wide and **60 m** long, at `FlameRoot`.
- Idempotent: running it twice gives the same scene.

**`GreatLampBeam`:**
- `public void Begin(float rampSeconds)` ramps the intensity from 0 to 1 over `rampSeconds` of unscaled time.
- It rotates at **24°/s** in unscaled time.
- It uses `BeaconVisual.BeamIntensityScaleLow` and `BeamLengthScaleLow` on Low.
- `GreatLamp.TryLight` starts the beam (ramp 2 s).

- [ ] **Step 1: Write the failing tests.**
  - `LighthouseFbxHasSlots`: the FBX loads, the child names listed above exist, the LOD0 triangle count is ≤ 6000, and the bounds height is 14 ± 0.5 m.
  - `Island5PostStepsOrder`: Bridge, Ring, Lighthouse, Perf, Audio.
  - `LampPromptReachableFromPlateau`: a point 4.0 m from the tower axis on the summit span side is within `interactRange`, and that point is not inside the stone collider.
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Write and run the Blender script (`blender.exe -b -P ArtSource/Blender/lighthouse_build.py`). Commit the `.blend` and the FBX.
- [ ] **Step 4:** Write the installer, `GreatLampBeam` and the `GreatLamp` changes. Run the installer on Island5 through the post-step chain (`BuildIsland5`'s post steps only, without re-sculpting): close the scene first or use `EditorSceneManager`.
- [ ] **Step 5:** Run the full EditMode and PlayMode suites. `Island5EveryBeaconAndLampReachable` must still pass. Run Validate on all 6 scenes.
- [ ] **Step 6:** Commit: `feat: Island 5 lighthouse and rotating beam`.
- [ ] **Step 7: USER GATE (H2 midway).**
  - Screenshots to `.superpowers/sdd/2026-10-12-island5-h2/shots/`:
    - the lighthouse from the beach;
    - from the summit span;
    - close up;
    - lit at night with the beam sweeping the sea.
  - A short play-mode capture of the ignition.
  - The user approves before Task 4.

---

### Task 3: Finale cue sourcing (runs while the user reviews the Task 2 gate)

**Files:**
- Create: `docs/audio/finale-shortlist.html`, in the format of `docs/audio/island5-shortlist.html`. 2–3 CC0 candidates, 60–90 s, hopeful and swelling, with a link, length, licence and a one-line description each. WebFetch research only.
- Modify: `Assets/Game/Scripts/MusicLibrary.cs`: add `public AudioClip finaleCue;`.
- Modify: `ArtSource/Audio/process_audio.py`: add a job for the pick at −23 LUFS, unlooped. If the source is longer than 90 s, trim it to 90 s with a 3 s fade-out.
- Modify: `Assets/Game/Editor/AudioInstaller.cs`: `InstallLibrary` fills `finaleCue` with `Assets/Game/Audio/Music/Finale_1.ogg`, falling back to `Music_Menu_1.ogg`.
- Modify: `CREDITS.txt`, adding the pick.
- Test: extend `AudioImportTests`. `FinaleCueAssigned`: `finaleCue` is non-null and its length is 45–95 s.

- [ ] **Step 1:** Write the shortlist and commit `docs: finale cue shortlist`.
- [ ] **Step 2: USER GATE.** The user picks a candidate, or chooses the fallback. Record the choice in the ledger.
- [ ] **Step 3:** Download only the pick, process it, assign it and credit it. The loudness report must be within ±1 LU.
- [ ] **Step 4:** Run the tests. Expected: pass. Commit `feat: finale cue`.

---

### Task 4: Finale cinematic (`FinaleSequence`, storm ring, silhouettes)

**Files:**
- Create: `Assets/Game/Scripts/FinaleSequence.cs`, `FinaleCamera.cs`, `StormRing.cs`, `FinaleIslands.cs`.
- Create: `Assets/Game/Editor/FinaleInstaller.cs`. It bakes the silhouette meshes to `Assets/Game/Art/Finale/Silhouette_Island{1..4}.asset`, builds the storm ring, and wires everything into Island5. It is the fifth `Island5PostSteps` entry, before Audio; update `Island5PostStepsOrder` to Bridge, Ring, Lighthouse, Perf, Finale, Audio.
- Modify:
  - `GameManager.cs`: add `[SerializeField] FinaleSequence finale;` and `public bool HasFinale => finale != null;`. In `Win()`, when `finale != null` call `finale.Play(FinishWin)` instead of `dawn.Play`. `DawnPlaying` stays true while it plays, which blocks pause.
  - `DawnSequence.cs`: add `public void Play(Action onComplete, float durationSeconds, bool unscaled)`. The existing `Play(Action)` keeps its exact behaviour, so Islands 1–4 are unchanged.
  - `MusicDirector.cs`: add `public void PlayFinale()`, which fades out the track, tension and hush over 1.5 s and plays `finaleCue` once on the music bus. `OnWon` skips the win stinger when `GameManager.Instance.HasFinale`.
- Test: `Assets/Game/Tests/PlayMode/FinaleSequenceTest.cs`, `Assets/Game/Tests/EditMode/FinaleInstallTests.cs`.

**Behaviour, by `FinaleTimeline` (all unscaled):**
- **0 s.**
  - `MusicDirector.PlayFinale()` starts the cue. The cue's opening is the deep chord.
  - A flare: lamp light intensity ×4, decaying over 1.2 s.
  - An ember burst of 40 additive sparks from `FlameRoot`.
  - `FinaleCamera` takes the main camera: it disables `CameraFollow` and holds a 3/4 view of the lantern room.
- **6 s.**
  - The beam has been sweeping since ignition.
  - The camera pulls back and up to `CameraHeight(t)`, orbiting at 10°/s to frame the island and the silhouette arc.
  - `StormRing` opens: the gap grows from the beam's bearing to fully open by 18 s.
- **StormRing:**
  - a band of cloud cards at radius 380 m, height 20–140 m;
  - lit from within by lightning flicker (`Lightning` is reused only for the timing);
  - it is visible during play too, which is the spec §3 cloud wall that H1 did not build;
  - it opens by a dissolve threshold in its shader, angular from the beam bearing;
  - on Low it uses half as many cards.
- **18–36 s.**
  - `FinaleIslands` fades in the 4 silhouettes, using the `Silhouette` shader at `FinaleLayout` positions.
  - At each `IslandFlareTime(i)`, that island's beacon cluster flares: 5 `BeaconBeam` card clusters plus one additive glow sprite, with no realtime lights.
  - Each silhouette mesh is a 48×48 grid from that terrain's heightmap, scaled to its true size, with the sea skirt cut away.
- **36–52 s.** `dawn.Play(null, FinaleTimeline.SunriseLength, unscaled: true)`. The sky goes violet to gold, using Island 5's `DawnLook`. The water and the silhouettes go warm (`_WaterTint`, `_SilhouetteTint`).
- **52 s.** `onComplete` is called, which runs `FinishWin` → `WonGame`. `FinaleCamera` holds its final frame.

- [ ] **Step 1: Write the failing tests.**
  - `FinaleRunsToCompletion` (PlayMode, Island5): call `FinaleSequence.Play` with a test speed multiplier `FinaleSequence.TestSpeed = 8`, and assert:
    - `WonGame` fires once;
    - it fires within (52 / 8) + 1 s of unscaled time;
    - the phases were seen in order.
  - `FinaleIgnoresPauseAndTimeScale`: the same run with `Time.timeScale = 0` set after `Play`, and pressing pause has no effect (`IsPaused` stays false).
  - `FinaleObjectsHaveShaders`: on Low and on Ultra, every renderer under the lighthouse, `StormRing` and `FinaleIslands` has a supported shader.
  - `IslandsOneToFourWinUnchanged`: Island 1's `GameManager.Win()` still runs `DawnSequence.Play(Action)`, and `HasFinale` is false there.
  - `FinaleInstallIsIdempotent` (EditMode): running the installer twice gives the same object count and the same silhouette asset hashes.
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement. `FinaleSequence.TestSpeed` is `internal static float`, 1 by default, and is reset in `OnDestroy`.
- [ ] **Step 4:** Run the full EditMode and PlayMode suites, including the Island 1–4 hash tests, and Validate on all 6 scenes.
- [ ] **Step 5:** Capture one shot per phase to the shots folder for the record.
- [ ] **Step 6:** Commit: `feat: Island 5 finale cinematic`.

---

### Task 5: Credits roll and return to the menu

**Files:**
- Create: `Assets/Game/Scripts/Hud/CreditsRoll.cs`.
- Create: `Assets/Game/Editor/UIBuilder.Credits.cs`. It builds the credits overlay into the Island 5 HUD with the existing UI kit fonts and styles.
- Create: `Assets/Game/Editor/CreditsSync.cs`. It copies `CREDITS.txt` to `Assets/Game/Resources/Credits.txt` and runs as part of `FinaleInstaller`.
- Modify: `HUD.cs`: `ShowWin` shows `CreditsRoll` instead of the result panel when `manager.HasFinale`.
- Test: `Assets/Game/Tests/PlayMode/CreditsRollTest.cs`, `Assets/Game/Tests/EditMode/CreditsSyncTests.cs`.

**Content, top to bottom (exact copy):**
1. "Lantern Keeper"
2. "Made by " + `Application.companyName`
3. "Music" heading, then each `CreditsFormat` entry under its section heading: title on one line, "by <author> · <licence>" below.
4. "Made with Unity"
5. "Thank you for keeping the light."

**Roll behaviour:**
- It scrolls at **40 px/s** at 1080p reference height, on unscaled time, over a darkened band. The dawn stays visible above and below.
- When the last line leaves the screen, it goes to `MainMenu`.
- **Skip:** any key, mouse button or gamepad button. It arms only after **1.0 s** and after every key has been released once.
- Both ways out call `MusicDirector`'s fade, then `SceneManager.LoadScene("MainMenu")`.

- [ ] **Step 1: Write the failing tests.**
  - `CreditsResourceMatchesRepoFile`: `Resources.Load<TextAsset>("Credits").text` equals `CREDITS.txt`, after normalising line endings.
  - `RollListsEveryCredit`: the roll's built text contains every parsed entry's title.
  - `HeldKeyDoesNotSkipCredits`: simulate a key held from before the roll starts. After 2 s it is still rolling. Release, then press: it loads `MainMenu`.
  - `SkipReturnsToMenu`: after 1.1 s, a press loads `MainMenu`.
  - `FinaleRecordsBestAndWon`: after a finale win on Island5, `GameSettings` shows Island 5 as won with a best time, and a second win also completes.
  - `ReturnToMenuStopsFinaleCue`: in MainMenu, no AudioSource is playing `finaleCue`, and the menu track is playing.
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement, and run `UIBuilder.Credits` plus `CreditsSync` on Island5.
- [ ] **Step 4:** Run the full suites, check `CardsFitAt130Percent`-style text fitting for the roll, and run Validate on all 6 scenes.
- [ ] **Step 5:** Commit: `feat: credits roll after the finale`.

---

### Task 6: Validation, perf, build, user gate

**Files:**
- Modify: `Assets/Game/Editor/SceneWiring.cs`. Island 5 gets these checks, with exact messages:
  - `"<scene> missing FinaleSequence"`
  - `"<scene> GameManager finale not wired"`
  - `"<scene> missing GreatLampBeam"`
  - `"<scene> missing CreditsRoll"`
  - `"<scene> silhouette '<n>' has no mesh"`
- Modify: `Assets/Game/Tests/PlayMode/BeaconReachabilityTest.cs`, only if the lighthouse collider needs the lamp target moved to the 4.0 m approach point from Task 2.

- [ ] **Step 1:** Add the validation. Confirm each message fires on a scratch copy, then delete the copy.
- [ ] **Step 2:** Run the full EditMode and PlayMode suites, and Validate on all 6 scenes. Expected: all pass, 0 problems.
- [ ] **Step 3: Perf.** One build, one probe on Island 5 at Low and Ultra, using the H1 re-measure method and settings. Append to `docs/look/perf/2026-10-09-island5-h1.md` under "H2". If either preset is above +0.3 ms over the H1 re-measure, stop and report.
- [ ] **Step 4: Release build.**
  - Build `Builds/Island5_H2/LanternKeeper.exe`, non-development.
  - Smoke test: run it 15 s, then grep the log for `NullReference`, `Exception`, "no clip" and "shader is null".
- [ ] **Step 5:** Commit `chore: Island 5 H2 validation and perf`.
- [ ] **Step 6: USER GATE (H2 end).** The user plays Island 5 to the finale and credits, checks the skip, and approves. Merge only when the user says "merge in main and push".
