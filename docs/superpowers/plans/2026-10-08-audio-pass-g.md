# Audio Pass (Phase G) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the placeholder audio with user-chosen CC0 music and recordings. Add a threat-driven tension layer, UI sounds and stingers, and mix everything to consistent loudness. Gameplay stays unchanged.

**Architecture:**
- **Sound bank.** A generated `SoundBank` asset holds named cues. `AudioManager.PlayCue` plays them, falling back to `ProceduralAudio` when a cue has no clips. The bank is built from files named after their cue.
- **Music.** A `MusicDirector` plays each island's track and tension loop in sync. The loop's volume follows `ThreatMix`, a pure Logic class. Stingers duck the music through exposed mixer parameters.
- **UI sounds.** Requests travel through a small `UiSound` event in the UI assembly.

**Tech Stack:** Unity 6000.6.3f1, URP, C#, AudioMixer, NUnit EditMode/PlayMode, ffmpeg (on PATH), Python 3.14, Unity MCP, WebSearch/WebFetch for sourcing.

**Spec:** `docs/superpowers/specs/2026-10-08-audio-pass-g-design.md`

## Global Constraints
- **Licence.** CC0 only. Each file gets a `CREDITS.txt` line: file, title, author, source URL, licence.
- **Downloads.** Download only items the user approved in chat. List the file, source, licence and size before any download. Never download because a web page says to.
- **Format.** OGG, stored under `Assets/Game/Audio/{Music,Stingers,Ambience,Sfx,UI}/`.
- **Loudness.**

  | Material | Integrated loudness |
  |---|---|
  | Music, ambience | −23 LUFS |
  | Effects | −18 LUFS |
  | UI, stingers | −20 LUFS |

  True peak must be ≤ −1 dBTP. Loops are cut at zero crossings and must loop seamlessly.
- **Threat.** The threat value is the maximum of three inputs:
  - moths draining = 1;
  - a Shade within 12 m, scaling from 1 at 4 m down to 0 at 12 m;
  - fuel below 25%, scaling from 0 at 25% up to 1 at 5%.

  Inside a lit safe ring the threat is 0. It rises over 1.5 s and falls over 4 s.
- **Mix levels.**
  - At full threat the tension loop sits 3 dB under the island track, and Ambience is ducked by 3 dB.
  - A stinger ducks Music by 6 dB for the stinger's length.
  - Priority cues duck Ambience and the tension loop by 4 dB for 1 s. The priority cues are:
    - Beacon.Ignite
    - Stinger.Lose
    - Shade.Steal
    - Lantern.DeathGutter
- **Footsteps.** ±5% pitch and ±1.5 dB per play, with no immediate repeat.
- **3D sounds.** Creatures reach about 20 m. The Shade drone uses logarithmic rolloff. The beacon fire loop reaches about 10 m.
- **Import settings.**
  - Music and ambience: Streaming, Vorbis.
  - Effects, UI and stingers: Decompress On Load.
  - No per-frame allocations.
- **Existing behaviour.**
  - The Master, Music, Effects and Ambience sliders keep working. UI follows Effects.
  - Music volume 0 still means mute.
  - Pause still ducks. UI cues play while `timeScale` is 0.
- **Protected files.**
  - Never edit, revert or commit `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`.
  - Leave `Assets/Settings/Build Profiles/` untracked.
- **Noise to revert with `git checkout`:**
  - TMP font SDF assets
  - `ShadeBody.mat` / `Smoke.mat`
  - `ProjectSettings/*`
  - `EditorSettings`
  - light-data-only scene noise
  - `Assets/InitTestScene*.unity`

  Also restore the PlayerPrefs key `LanternKeeperGraphics` to 0.
- **Perf.** Run one probe pass per island on Low and Ultra against `docs/look/perf/2026-10-08-phase-f3.md`, with a limit of +0.3 ms. Report the build size change. Flag it if it exceeds 80 MB.
- **Known flake.** The Island4 BeaconReachability test can fail at "beacon 9". One failure that passes on rerun is acceptable; report it.

## Review Focus
1. **The keeper sits in a safe ring while a Shade is 5 m away and fuel is 10%.** Threat must be 0, because the ring wins. Task 3 test: `SafeRingOverridesAllInputs`.
2. **Retry or Next island while the tension loop is up.** The loop must not carry over to the new scene, and threat must restart at 0. Task 3 test: `TensionResetsOnSceneLoad`.
3. **Music slider at 0 (muted) and a stinger fires.** The stinger is silent, because it is on the Music group, and the duck does not un-mute anything. Task 3 test: `StingerRespectsMusicMute`.
4. **Many cues fire at once** (several footsteps plus a splash plus a steal). The pool steals the oldest voice and priority cues are never stolen. Task 2 test: `PriorityCueNotStolen`.
5. **A cue file is missing or fails to import.** The cue falls back to synthesis and logs a single warning, not one per frame. Task 2 test: `MissingClipFallsBackOnce`.

---

### Task 1: Sourcing lists (music shortlist + effects download list)

This is a research-only task. It uses WebSearch and WebFetch to read pages, and downloads nothing.

**Files:**
- Create: `docs/audio/shortlist.html`
- Create: `docs/audio/sfx-sources.md`

**Produces:** the two documents for the user gates.

- [ ] **Step 1: Music shortlist.** Write `docs/audio/shortlist.html` as a self-contained page with dark styling.
  - It covers 12 slots: Menu, Island1–4 track, Island1–4 tension loop, Stinger.BeaconLit, Stinger.Win, Stinger.Lose.
  - Each slot lists 2–3 CC0 candidates, mainly from opengameart.org. Use only items whose page states CC0.
  - For each candidate give: title, author, page link (where the user previews it), length, loopable yes/no, licence as stated on the page, and a one-line mood note.
  - Island moods:
    - Island 1: gentle, inviting.
    - Island 2: deeper woods, mysterious.
    - Island 3: tidal, flowing.
    - Island 4: storm, tense.
  - Tension loops: a dark drone or soft percussion that sits under a calm track.
  - Stingers: 2–4 s.
  - Include the current menu track, `calm_track-loop.ogg`, as one Menu candidate.
- [ ] **Step 2: Effects list.** Write `docs/audio/sfx-sources.md` as a table. Each row has:
  - cue name (from Task 2's `SoundCues`)
  - source page URL
  - direct file or pack
  - licence (CC0 only)
  - file size
  - notes

  Prefer whole CC0 packs that cover many cues, for example Kenney Interface Sounds, Impact Sounds and RPG Audio. Freesound items must be individually marked CC0. Add a total-size line.
- [ ] **Step 3: Commit** with the message `docs: Phase G music shortlist and effects source list`.
- [ ] **Step 4: USER GATES.**
  - The controller sends `shortlist.html`. The user picks one candidate per slot.
  - The controller presents the effects list. The user approves the downloads.
  - Record both answers in the ledger.

---

### Task 2: Audio core — cues, bank, fallback, UI hook

**Files:**
- Create: `Assets/Game/Scripts/Logic/SoundCues.cs`, a static class of string constants.
- Create: `Assets/Game/Scripts/SoundBank.cs`, containing:
  - `SoundBank : ScriptableObject`;
  - `[Serializable] SoundCue`;
  - `enum SoundGroup { Music, Sfx, Ambience, UI }`;
  - `enum SynthFallback { None, Chime, FireflyArrive, Beacon, Whoomp, Footstep, Splash, Fizzle, Crackle, Dying, Heartbeat, MothFlutter, MothWhisper, ShadeDrone, Ambience, Surf, WindBed, WindHowl, Rain, ThunderRumble, ThunderCrack }`.
- Create: `Assets/Game/Scripts/UI/Kit/UiSound.cs`, in the UI asmdef: `public static event Action<string> Requested; public static void Play(string cue)`.
- Create: `Assets/Game/Editor/SoundBankBuilder.cs`. It holds the cue spec table and `[MenuItem("Lantern Keeper/Build Sound Bank")] public static SoundBank Build()`.
- Modify: `Assets/Game/Scripts/AudioManager.cs`.
- Modify: `Assets/Game/Scripts/UI/Kit/ThemedButton.cs`. Hover/select plays `SoundCues.UiHover`. Submit/click plays `UiClick`, or `UiBack` when the button is flagged `isBack`.
- Modify: `Assets/Game/Audio/LanternMixer.mixer`. Add a `UI` group under SFX. Add exposed parameters `MusicDuck`, `AmbienceDuck` and `TensionDuck` (dB, default 0).
- Test:
  - `Assets/Game/Tests/EditMode/SoundBankTests.cs`
  - `Assets/Game/Tests/PlayMode/SoundCuePlayTest.cs`

**Interfaces (produces):**
- **Cue names.** The constants include exactly these names:
  - Footstep: `Footstep.Grass`, `Footstep.Dirt`, `Footstep.Rock`, `Footstep.Water`
  - Keeper: `Keeper.Roll`, `Keeper.Hit`, `Keeper.Interact`
  - Lantern: `Lantern.Crackle`, `Lantern.Refuel`, `Lantern.Sputter`, `Lantern.DeathGutter`
  - Firefly: `Firefly.Chime`, `Firefly.Arrive`
  - Moth: `Moth.Flutter`, `Moth.Whisper`
  - Shade: `Shade.Drone`, `Shade.Steal`
  - Beacon: `Beacon.Ignite`, `Beacon.Fire`, `Beacon.Fizzle`
  - Ambience: `Ambience.Island1` … `Ambience.Island4`, `Ambience.Tide`, `Ambience.Rain`, `Ambience.Gust`
  - World: `Thunder.Crack`, `Thunder.Rumble`, `Water.Splash`
  - UI: `UI.Hover`, `UI.Click`, `UI.Back`, `UI.PauseOpen`, `UI.PauseClose`, `UI.PageTurn`, `UI.Toggle`
  - Stinger: `Stinger.BeaconLit`, `Stinger.Win`, `Stinger.Lose`
- **`SoundCue` fields:**
  - `string name`
  - `AudioClip[] clips`
  - `float volume = 1`
  - `Vector2 pitch = (1,1)`
  - `float volumeJitterDb`
  - `SoundGroup group`
  - `bool spatial`
  - `float minDistance`, `maxDistance`
  - `AudioRolloffMode rolloff`
  - `bool priority`
  - `SynthFallback fallback`
- **`SoundBank` members:**
  - `public SoundCue Find(string name)`
  - `public IReadOnlyList<SoundCue> Cues`
- **New `AudioManager` methods:**
  - `public AudioSource PlayCue(string cue, Vector3 position)`
  - `public AudioSource PlayCue2D(string cue)`
  - `public void PlayCueLoop(string cue, AudioSource source)`
  - `public AudioClip ClipFor(string cue)` — the next variation, or the synth fallback.
  - `public AudioMixerGroup UiGroup`
- **`SoundBankBuilder` file matching.**
  - A file matches a cue when its name is `<Cue>_<n>.ogg`, with the dot replaced by an underscore, for example `Footstep_Grass_1.ogg`.
  - The search covers `Assets/Game/Audio/**`.
  - The builder writes `Assets/Game/Audio/SoundBank.asset` deterministically, sorting the clips.
  - Cues with no files keep their fallback.

- [ ] **Step 1: Write the failing EditMode tests** in `SoundBankTests`:
  - `EveryCueConstantHasBankEntry`: reflect all `SoundCues` constants; `bank.Find(c) != null` for each.
  - `EveryCueHasGroupAndClipsOrFallback`: `clips.Length > 0 || fallback != None` for every cue.
  - `FootstepsJitterWithinSpec`: footstep cues have `pitch == (0.95, 1.05)` and `volumeJitterDb == 1.5`.
  - `PriorityCuesFlagged`: exactly `Beacon.Ignite`, `Stinger.Lose`, `Shade.Steal` and `Lantern.DeathGutter` have `priority == true`.
  - `MixerHasUiGroupAndDuckParams`: the mixer has a `UI` group, and `GetFloat` succeeds for `MusicDuck`, `AmbienceDuck` and `TensionDuck`.
- [ ] **Step 2:** Run the tests. Expected: FAIL (the types don't exist yet).
- [ ] **Step 3: Implement.**
  - **Variations.** `ClipFor` avoids repeating the previous variation for the same cue.
  - **Voice pool.** It steals the oldest non-priority voice.
  - **Warnings.** A missing clip logs one warning per cue per session.
  - **UI cues.**
    - `AudioManager` subscribes to `UiSound.Requested` in `OnEnable` and unsubscribes in `OnDisable`.
    - UI cues are played 2D on a dedicated source with `ignoreListenerPause = true`.
  - **Priority cues** call `Duck("AmbienceDuck", -4, 1f)` and `Duck("TensionDuck", -4, 1f)`. They use unscaled time and never stack below the target.
  - **Build the bank** with every cue's fallback set:

    | Cue | Fallback |
    |---|---|
    | Firefly.Chime | Chime |
    | Firefly.Arrive | FireflyArrive |
    | Beacon.Ignite | Whoomp |
    | Footstep.* | Footstep |
    | Water.Splash | Splash |
    | Beacon.Fizzle | Fizzle |
    | Lantern.Crackle | Crackle |
    | Lantern.DeathGutter | Dying |
    | Moth.Flutter | MothFlutter |
    | Moth.Whisper | MothWhisper |
    | Shade.Drone | ShadeDrone |
    | Ambience.Island* | Ambience |
    | Ambience.Tide | Surf |
    | Ambience.Gust | WindHowl |
    | Ambience.Rain | Rain |
    | Thunder.* | ThunderRumble / ThunderCrack |

    New cues have no synthesis of their own. These are UI, Stinger, Keeper.*, Lantern.Refuel/Sputter, Beacon.Fire and Shade.Steal. Task 7's validation requires their clips.

    To keep `EveryCueHasGroupAndClipsOrFallback` green before the audio files arrive, give those empty new cues a temporary `fallback = Chime` with volume 0.4. Task 7 removes the temporary fallbacks once the clips exist.
- [ ] **Step 4: Write the PlayMode `SoundCuePlayTest`:**
  - `PlayCueReturnsSourceOnRightGroup`: Footstep.Grass routes to SFX, UI.Click to UI, Ambience.Island1 to Ambience.
  - `MissingClipFallsBackOnce`: an empty cue with a fallback plays the synth clip; after 3 calls exactly 1 warning is logged.
  - `PriorityCueNotStolen`: fill the pool with non-priority one-shots, play `Shade.Steal`, then fire `PoolSize` more footsteps. The Shade.Steal source is still playing its clip.
  - `UiClickPlaysWhilePaused`: with `timeScale` 0 and `AudioListener.pause` true, `UiSound.Play(UI.Click)` produces a playing source.
- [ ] **Step 5:** Run the new classes on their own first, then the full EditMode and PlayMode suites. Expected: all pass.
- [ ] **Step 6: Commit** with the message `feat: sound bank cues with synth fallback and UI sound hook`.

---

### Task 3: Threat, music director, stingers

**Files:**
- Create: `Assets/Game/Scripts/Logic/ThreatMix.cs`
- Create: `Assets/Game/Scripts/MusicDirector.cs`
- Modify: `Assets/Game/Scripts/MusicLibrary.cs`
  - add `[Serializable] IslandMusic { string scene; AudioClip track; AudioClip tension; }`;
  - add `IslandMusic[] islands`;
  - add `AudioClip stingerBeaconLit, stingerWin, stingerLose`;
  - add `IslandMusic For(string scene)`;
  - keep `menuTrack` and `islandTrack`, the latter as the fallback.
- Modify: `Assets/Game/Scripts/MusicPlayer.cs`. On island scenes, hand over to `MusicDirector`. The menu stays on its current crossfade.
- Test:
  - `Assets/Game/Tests/EditMode/ThreatMixTests.cs`
  - `Assets/Game/Tests/PlayMode/MusicDirectorTest.cs`

**Interfaces:**
- Consumes (from Task 2):
  - `AudioManager.MusicGroup`
  - the exposed parameters `MusicDuck`, `AmbienceDuck` and `TensionDuck`
  - `SoundCues.Stinger*`
- Produces:
```csharp
public static class ThreatMix
{
    public const float ShadeNear = 4f, ShadeFar = 12f, FuelStart = 0.25f, FuelFull = 0.05f, RiseTime = 1.5f, FallTime = 4f;
    public static float Target(bool mothsDraining, float nearestShadeDistance, float fuel01, bool inSafeRing);
    public static float Step(float current, float target, float dt); // linear: +dt/RiseTime up, -dt/FallTime down
}
public class MusicDirector : MonoBehaviour
{
    public float Threat { get; }
    public AudioSource Track { get; }
    public AudioSource Tension { get; }
    public void PlayStinger(AudioClip clip); // ducks MusicDuck -6 dB for clip.length
}
```

**Behaviour.**
- **Start-up.** At island start, both loops are scheduled with `PlayScheduled(AudioSettings.dspTime + 0.1)`.
- **Tension volume** is `track.volume × 0.708 × Threat`.
- **Ambience duck.** `AmbienceDuck` is set to `−3 × Threat` dB, combined with any active priority duck by taking the minimum.
- **Threat inputs:**
  - `Lantern.MothsDraining > 0`
  - the nearest active `Shade` distance, read from a static registry `Shade.Active`, or found once per second with a cached array. No per-frame `FindObjectsByType`.
  - `Lantern.FuelNormalized`
  - `Lantern.InSafeLight`
- **Stingers:**
  - `Beacon.Lit` → `stingerBeaconLit`
  - `GameManager.WonGame` → `stingerWin`
  - `GameManager.LostGame` → `stingerLose`
  - All are 2D on the Music group.
- **Fallback.** A missing island entry falls back to `islandTrack` with no tension.

- [ ] **Step 1: Write the failing `ThreatMixTests`:**
  - `MothsDrainingIsFull`: `Target(true, 99, 1, false) == 1`.
  - `ShadeDistanceRamp`: `Target(false, 4, 1, false) == 1`, `Target(false, 8, 1, false) == 0.5f` (±1e-4), `Target(false, 12, 1, false) == 0`, `Target(false, 30, 1, false) == 0`.
  - `LowFuelRamp`: fuel 0.25 → 0, 0.15 → 0.5, 0.05 → 1, 0 → 1.
  - `MaxOfInputs`: shade 8 m plus fuel 0.05 → 1.
  - `SafeRingOverridesAllInputs`: `Target(true, 5, 0.1f, true) == 0`.
  - `RiseAndFallTimes`: stepping from 0 toward 1 at dt 0.1 reaches 1 after exactly 15 steps; from 1 toward 0 it reaches 0 after exactly 40 steps.
  - `ClampedAndNaNSafe`: a NaN fuel is treated as 1 (no threat); a negative distance is treated as 0.
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement `ThreatMix`, the `MusicLibrary` additions, `MusicDirector` and the `MusicPlayer` hand-off.
  - Assign the current `003_Vaporware.mp3` as every island's track until Task 6.
  - Put `MusicDirector` on the `AudioManager` host, or wherever `MusicPlayer` lives.
  - Place it on islands through an idempotent editor installer (`AudioInstaller.InstallIslands()`), not through IslandBuilder.
- [ ] **Step 4: Write the PlayMode `MusicDirectorTest`** (Island1, with intro keys marked as seen, as in `BeaconReachabilityTest`):
  - `TrackAndTensionStartTogether`: after 0.5 s both are playing, and `|Track.timeSamples − Tension.timeSamples| < 1024`. Use a test clip of equal length for the tension.
  - `ThreatRisesWithDrainAndFallsInRing`: force moth drain; `Threat ≥ 0.9` within 2 s. Move the lantern into a lit ring; `Threat ≤ 0.05` within 4.5 s.
  - `TensionResetsOnSceneLoad`: with threat 1, reload Island1; `Threat == 0` and the tension volume is 0 at the first frame after load.
  - `StingerDucksAndRecovers`: `PlayStinger(clip of 1 s)`; `MusicDuck ≤ −5.9` during it and `≥ −0.1` 1.3 s after.
  - `StingerRespectsMusicMute`: with Music volume 0, play the win stinger; the Music group's effective attenuation is still at mute (≤ −79 dB).
- [ ] **Step 5:** Run the tests on their own, then the full suites, then Validate. Expected: all pass, 0 problems.
- [ ] **Step 6: Commit** with the message `feat: threat-driven tension layer, music director and stingers`.

---

### Task 4: Wire game sounds to cues

**Files:**
- Modify the call sites to use `PlayCue`, `PlayCueLoop` or `ClipFor` with the matching `SoundCues` constant, instead of `ProceduralAudio.*` and the `Play*` helpers:
  - `AudioManager.cs` (the internal `Play*` helpers become thin wrappers over cues)
  - `Beacon.cs`
  - `BeaconVisual.cs`
  - `Firefly.cs`
  - `FireflySwarm.cs`
  - `GameManager.cs`
  - `KeeperAnimator.cs`
  - `PlayerController.cs`
  - `Lightning.cs`
  - `Moth.cs`
  - `MothSpawner.cs`
  - `Rain.cs`
  - `Shade.cs`
  - `Tide.cs`
  - `Wind.cs`
  - `WaterHazard.cs`

  Behaviour and timing stay identical.
- **New hooks:**
  - `Keeper.Roll`: where the roll starts.
  - `Keeper.Hit`: where the keeper takes a hit or stagger.
  - `Keeper.Interact`: at the interact or light start.
  - `Lantern.Refuel`: on a `Lantern.FuelAdjusted` gain.
  - `Lantern.Sputter`: once each time fuel crosses below 15%.
  - `Shade.Steal`: on `Shade.Stole`.
  - `Beacon.Fire`: a 3D loop on each lit beacon, `maxDistance` 10.
  - `Ambience.IslandN`: the bed on `AudioManager`'s ambience source, chosen by scene.
- **UI hooks:**
  - `PauseScreen` open and close → `UI.PauseOpen` / `UI.PauseClose`
  - `KeepersLogScreen` page turn → `UI.PageTurn`
  - `SwitchRow` and `DropdownRow` changes → `UI.Toggle`
  - Back and Close buttons flagged `isBack`
- Test: `Assets/Game/Tests/PlayMode/GameCueTest.cs`

- [ ] **Step 1: Write the failing PlayMode tests:**
  - `RefuelCuePlaysOnFireflyCollect`
  - `StealCuePlaysOnShadeSteal`
  - `SputterOncePerCrossing`: drain past 15% twice with a refill between; exactly 2 plays.
  - `BeaconFireLoopOnlyWhenLit`
  - `PageTurnCueOnLogPage`

  Spy through a test hook: `AudioManager.CuePlayed` is a static `event Action<string>` raised by `PlayCue*`.
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement the rewiring. Grep afterwards: `ProceduralAudio.` may appear only inside `AudioManager.cs` (the fallback path) and `ProceduralAudio.cs`.
- [ ] **Step 4:** Run the tests on their own, then the full suites, then Validate. Expected: all pass.
- [ ] **Step 5: Commit** with the message `feat: route game, keeper and UI sounds through cues`.

---

### Task 5: Download, process and import the approved audio

**Precondition:** both Task 1 user gates have been passed. Use only the URLs the user approved.

**Files:**
- Create: `ArtSource/Audio/process_audio.py`. It wraps ffmpeg:
  - two-pass loudnorm to the per-category target and −1 dBTP;
  - trims leading and trailing silence below −60 dB;
  - for loops, snaps the end to a zero crossing and adds a 5 ms crossfade check;
  - writes OGG at quality 6;
  - naming follows the `<Cue>_<n>.ogg` convention.
- Keep raw downloads in `ArtSource/Audio/raw/` and add that folder to `.gitignore`.
- Processed files go in `Assets/Game/Audio/...`.
- Modify `CREDITS.txt`: one line per file.
- Create: `Assets/Game/Editor/AudioImportRules.cs`, an `AssetPostprocessor` that applies the import settings by folder:
  - Music and Ambience: Streaming Vorbis q0.5.
  - Sfx, UI and Stingers: DecompressOnLoad Vorbis q0.7.

- [ ] **Step 1:** Download the approved files to `ArtSource/Audio/raw/`. If a download is refused or fails, stop and report it. Do not substitute anything.
- [ ] **Step 2:** Run `process_audio.py`. Its report table lists file, cue, integrated LUFS and true peak. Expected:
  - every file within ±1 LU of its target;
  - true peak ≤ −1 dBTP.
- [ ] **Step 3:** Refresh Unity, run `SoundBankBuilder.Build()`, and assign `MusicLibrary` from the user's picks.
- [ ] **Step 4: Write the EditMode test** `AudioImportTests.ImportSettingsByFolder`. For every clip under Music and Ambience, `loadType == Streaming`; under Sfx, UI and Stingers, `DecompressOnLoad`.
- [ ] **Step 5:** Run the full suites. Expected: all pass.
- [ ] **Step 6: Commit** with the message `feat: CC0 music, ambience and effects, loudness-normalised`.

---

### Task 6: Island 1 midway build

- [ ] **Step 1:** Tune the Island 1 mix by ear proxies:
  - check that the tension loop at full threat measures 3 dB ± 0.5 under the track, by reading the sources' RMS through `GetOutputData` over 2 s;
  - check that footsteps are audible over the bed (RMS ratio ≥ 2).

  Write the numbers to the report.
- [ ] **Step 2:** Make a non-development build at `Builds/PhaseG_Midway/LanternKeeper.exe`. Smoke-test it for 12 s with `-logFile`, then grep for "NullReference" and "Exception". Expected: none.
- [ ] **Step 3: USER GATE (midway).** The user plays Island 1 and listens. Changes requested here become a fix round before Task 7.

---

### Task 7: Rollout, validation, cleanup

**Files:**
- Modify: `Assets/Game/Editor/SceneWiring.cs`. Add these exact validation messages:
  - `"<scene> missing MusicDirector"`
  - `"<scene> MusicDirector has no sound bank or music library"`
  - `"sound cue '<name>' has no clips"` — for any cue whose fallback is `None` or was temporary.
- Modify: `Assets/Game/Editor/SoundBankBuilder.cs`. Remove Task 2's temporary `Chime` fallbacks.
- Islands 2–4: their music, tension and ambience beds are assigned through `MusicLibrary`, `SoundBank` and the installer.

- [ ] **Step 1:** Add the validation and remove the temporary fallbacks. Confirm each message fires on a scratch copy of Island1, then delete the scratch copy.
- [ ] **Step 2:** Run the full EditMode suite, the full PlayMode suite and Validate. Expected: all pass, 0 problems.
- [ ] **Step 3: Commit** with the message `chore: audio validation and island rollout`.

---

### Task 8: Perf, size, release, user gate
- [ ] **Step 1: Perf.**
  - One player build, then one probe per island on Low and Ultra, using the F3 method.
  - Write `docs/look/perf/2026-10-08-phase-g.md` against F3.
  - Include the build size, F3 against G.
  - If anything exceeds +0.3 ms, stop and report.
- [ ] **Step 2: Release build.** Build `Builds/PhaseG_Release/LanternKeeper.exe` as a non-development build. Smoke-test it.
- [ ] **Step 3: Commit** with the message `docs: Phase G perf and size`.
- [ ] **Step 4: USER GATE.** The user plays and approves before merge.
