# Phase G: Audio Pass — Design

Status: approved in conversation on 2026-10-08, pending spec review.

**Parent:** `2026-10-03-art-direction-design.md` (Phase A). Its tone applies: one warm light carried through a cold, dark world.

The visual pass (Phases A–F3) is complete. Audio is now the weakest part of the game.

## Goal
Replace the placeholder audio with CC0 recordings and music, chosen with the user. Add a reactive tension layer, UI sounds and short stingers, and mix everything to consistent loudness. Gameplay is unchanged.

## Current state (for reference)
- **Music.** `MusicPlayer` crossfades two CC0 tracks over 2 s, using `MusicLibrary` with `menuTrack` and `islandTrack`.
  - `calm_track-loop.ogg` is the menu track.
  - `003_Vaporware.mp3` is shared by all islands.
- **Effects.** About 20 sounds are synthesised at runtime by `ProceduralAudio` (876 lines) and played through `AudioManager` (627 lines), which has a source pool and loop sources.
- **Storm.** `StormAudio` drives Island 4's rain, wind and thunder timing.
- **Mixer.** `LanternMixer` has Music, SFX and Ambience groups, and a ducked snapshot used by pause.
- **Settings.** `UserSettings` has Master, Music, Effects and Ambience volumes. Music at 0 counts as mute, and the legacy mute migration is already done.
- **UI.** Menus and the Keeper's Log are silent. There are no stingers.

## 1. Structure
- **`SoundBank` and `SoundCue`.**
  - `SoundBank` is a ScriptableObject at `Assets/Game/Audio/SoundBank.asset`. It holds named cues.
  - Each `SoundCue` has:
    - clips (1–n variations, chosen at random without immediate repeats)
    - volume
    - a pitch range and a volume range
    - a mixer group: Music, SFX, Ambience or UI
    - 2D or 3D, with min and max distance
    - a priority
    - a synthesis fallback key
  - Cue names are constants in one `SoundCues` static class.
  - `AudioManager` plays cues by name: one-shot at a position, 2D, or as a loop on a given source.
  - If a cue has no clips, the existing `ProceduralAudio` clip for its fallback key plays instead. A missing recording never means silence.
- **Mixer.**
  - Keep `LanternMixer`.
  - Add a **UI** group under SFX, so UI follows the Effects slider.
  - Add a **Danger** snapshot or parameter that lowers Ambience by about 3 dB at full threat.
  - Existing sliders, mute and pause ducking are unchanged.
- **Music.**
  - `MusicLibrary` gains, per island: a track and a tension loop.
  - It gains three stingers: beacon lit, win and lose. The menu track stays.
  - A new `MusicDirector`:
    - plays the island track and tension loop sample-aligned (`PlayScheduled`);
    - sets the tension volume from a threat value;
    - plays stingers with a short music duck, about −6 dB for the stinger's length.
  - Menu and island crossfades keep `MusicPlayer`'s behaviour.
- **`ThreatMix` (pure Logic).**
  - Threat (0–1) is the maximum of three inputs:
    - **Moths draining:** 1 when at least one moth drains.
    - **Shade proximity:** a Shade within 12 m, falling from 1 at 4 m to 0 at 12 m.
    - **Low fuel:** fuel below 25%, rising from 0 at 25% to 1 at 5%.
  - Inside a lit beacon's safe ring, threat is 0.
  - Smoothing: rise over 1.5 s, fall over 4 s.
  - Gameplay reads nothing from it.
- **Ambience.**
  - Each island has an ambience bed: surf, wind and night insects, mixed per island.
  - Island 3 adds its tide sound.
  - Island 4 keeps `StormAudio`'s timing with recorded rain, gusts and thunder.

## 2. Content

| Group | Cues |
|---|---|
| Keeper | footsteps (grass, dirt, rock, shallow water; 3–4 variations each), roll, hit/stagger, cloth rustle on interact |
| Lantern | crackle loop, refuel, low-fuel sputter, death gutter |
| Creatures | firefly chime, firefly arrival, moth flutter, moth drain whisper, Shade drone (3D loop), Shade fuel-steal hit |
| Beacons | ignition, burning fire loop (3D, about 10 m) |
| World | ambience bed per island, tide (I3), rain, gusts, thunder (I4), water splash |
| UI | hover, click, back, pause open/close, Keeper's Log page turn, settings toggle |
| Music | menu, 4 island tracks, 4 tension loops |
| Stingers | beacon lit, win, lose (about 2–4 s each) |

- **Island moods for music:**
  - Island 1: gentle and inviting.
  - Island 2: deeper woods, mysterious.
  - Island 3: tidal, flowing.
  - Island 4: storm, tense.
- **Licence.** CC0 only. Every file has a line in `CREDITS.txt` giving file, title, author, source URL and licence.
- **Storage.** Files are stored as OGG under `Assets/Game/Audio/{Music,Stingers,Ambience,Sfx,UI}/`.

## 3. Sourcing workflow (each download needs the user's permission)
1. **Music shortlist.** Build `docs/audio/shortlist.html` in the repo. For each of the 12 music and stinger slots it lists 2–3 CC0 candidates, mostly from OpenGameArt, with:
   - a link to the track page, where it can be previewed in the browser
   - title and author
   - length
   - whether it loops
   - licence
   - a one-line description
2. **The user listens and picks** one per slot, or asks for more candidates.
3. **Effects and ambience.** The controller chooses CC0 sources, such as Kenney packs and Freesound CC0 recordings. It presents a single download list (file, source, licence, size). The user approves it before anything is downloaded.
4. **Processing.** After download, ffmpeg trims silence, converts to OGG and normalises loudness:
   - music and ambience: −23 LUFS integrated
   - effects: −18 LUFS
   - UI and stingers: −20 LUFS
   - true peak ≤ −1 dBTP

   Loops are cut at zero crossings and checked for seamless looping.

## 4. Mix
- **Tension loop.** At full threat it sits about 3 dB under the island track.
- **Priority cues.** These cut through, with the tension layer and ambience ducking briefly under them:
  - beacon ignition
  - lose stinger
  - Shade fuel steal
  - lantern death gutter
- **Footsteps.** Random variation of ±5% pitch and ±1.5 dB, with no immediate repeats.
- **3D sounds.** Creatures are audible to about 20 m. The Shade drone is low and rolls off with logarithmic falloff. The beacon fire loop reaches about 10 m.
- **Paused game.** Pause keeps today's ducking. UI cues play while `timeScale` is 0.

## 5. Testing and validation
- **EditMode:**
  - every `SoundCues` constant has a bank entry
  - every cue has a valid mixer group
  - every cue has either clips or a fallback key
  - `ThreatMix`:
    - each input alone
    - the safe ring forces 0
    - combined inputs take the maximum
    - clamped to 0–1
    - rise of 1.5 s and fall of 4 s
  - `MusicLibrary` is complete for every island
- **PlayMode:**
  - the island track and tension loop start together and stay in sync
  - threat rises while a moth drains and falls after entering a safe ring
  - the win stinger plays and ducks the music, then the music recovers
  - UI clicks play while paused
  - a cue with no clips falls back to synthesis with no errors
  - existing tests pass: music-mute migration, settings sliders, pause ducking and the rest of the suite
- **Validate Scene Wiring.**
  - Every island has a `MusicDirector` with the bank and library assigned.
  - No cue is empty unless it is marked synthesis-only.
- **Import settings.** Music and ambience use streaming Vorbis. Short effects use decompress-on-load. There are no per-frame allocations.
- **Performance.**
  - One probe pass per island on Low and Ultra against F3. Limit: +0.3 ms average.
  - If it fails, report the numbers before investigating.
  - Report the build size change. Expected about 30–60 MB; flag anything over 80 MB.
- **User checkpoints:**
  1. Music picks from the shortlist.
  2. Approval of the effects and ambience download list.
  3. Midway: a playable build with music, the tension layer and ambience on Island 1, before rollout to all islands.
  4. End: a playable non-development release build, approved before merge.

## Out of scope
- Voice acting.
- Gameplay changes.
- New audio settings UI (the existing sliders cover it).
- Paid or commercial assets.
- FMOD or Wwise.
