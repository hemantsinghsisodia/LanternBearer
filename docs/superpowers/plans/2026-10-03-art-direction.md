# Visual Pass Phase A (Art Direction) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record the approved art direction in the repo, along with:
- the concept boards
- a baseline capture tool and the "before" capture set
- the `LookProfile` and `UITheme` data containers, populated
- the three UI fonts

None of this makes a visible change to the game.

**Architecture:**
- **New `LanternKeeper.Look` assembly.**
  - Holds `LookProfile` and `UITheme` (ScriptableObjects).
  - References `LanternKeeper.Logic` and `Unity.TextMeshPro`. Auto-referenced, so `Assembly-CSharp` and the editor scripts can see it.
  - The EditMode test assembly also references it, so the tests can load and check the real assets.
- **Signal colours.** Fixed signal colours and hex helpers go in `LanternKeeper.Logic` (`LookPalette`), as pure static data.
- **Capture tool.** An editor-only tool in `Assembly-CSharp-Editor`. It:
  1. generates a fixed shot list per island once
  2. captures those shots in play mode on Low and Ultra
  3. restores all editor state afterwards

**Tech Stack:** Unity 6000.6.3f1, URP, C#, TextMeshPro, Unity Test Framework 1.8.0 (NUnit), Unity MCP.

**Spec:** `docs/superpowers/specs/2026-10-03-art-direction-design.md` (read it alongside this plan).

## Global Constraints
- **Code style.** Follow `Scripts/GameSettings.cs`:
  - `namespace LanternKeeper` and Allman braces
  - `[SerializeField]` private fields, except on plain data assets, where public fields are fine
  - explicit `if`/`return`
  - comment density matching the surrounding code
- **No visible change to the game.** Nothing in `Assembly-CSharp` reads `LookProfile` or `UITheme` yet. Build All Levels must still validate with 0 problems. Islands 1–4 must play exactly as before.
- **Signal colours** (verbatim from the spec):

  | Colour | Hex |
  |---|---|
  | Lantern amber | `#FFB15C` |
  | Glow core | `#FFD38A` |
  | Drain violet | `#B48CFF` |
  | Lightning pale blue | `#BCD2FF` |
  | Firefly green | `#B8FFB0` |

- **Island palettes** (verbatim from the spec, sRGB hex):

  | Island | Sky | Sea | Land | Moon rim | Extra | Moon |
  |---|---|---|---|---|---|---|
  | island1 | `#0C1630` | `#17304D` | `#163027` | `#6FB3C8` | none | on, `#EEF6FF` |
  | island2 | `#0D1124` | `#151D33` | `#1B2A2A` | `#8A9CC4` | `#1D2233` (slate cliff) | on, `#E6ECFF` |
  | island3 | `#0A1A1C` | `#132A28` | `#1B2E24` | `#7FB8A2` | `#7FB8A2` (mist) | on, `#D8F0D0` |
  | island4 | `#120F22` | `#1C1A30` | `#2A2638` | `#B8B2E0` | `#D6DCFF` (lightning) | **off** |

  Flags:
  - island1: none
  - island2: valley mist
  - island3: ground mist
  - island4: rain
- **Fonts:** Cormorant Garamond (titles), Spectral (flavour, italic), Inter (UI and numbers). All are SIL OFL. Use static TTFs, not variable fonts.
- **Paths:**
  - art: `Assets/Game/Art/`
  - concept boards: `docs/look/concepts/`
  - baseline: `docs/look/baseline/<YYYY-MM-DD>/`
- **Baseline capture:** 1920×1080 on Low and Ultra.
  - Shots per island: 7 on islands 1–3, 8 on island 4 (adds the Shade close-up).
  - Screens: main menu, HUD, pause, journal.
- **Unity environment:**
  - The editor is open with the Unity MCP. Never use batchmode.
  - The editor does not tick while unfocused. Set `Application.runInBackground` temporarily and restore it.
  - Revert build and test-runner noise in `ProjectSettings/*.asset` with `git checkout`.
- **Commits** end with the trailer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Never push.
- **Downloads need the user's approval.** Font downloads happen only after the controller has asked the user and they agreed.

## Review Focus
1. **The capture tool fails or is interrupted midway.** The editor must come back unchanged: graphics preset, PlayerPrefs (intro-seen keys, difficulty), timeScale, runInBackground, open scene, play mode off, and no dirty scenes. Pinned by `LookCaptureState` restore-in-`finally` and Task 4 Step 6's forced-failure check.
2. **The intro card pauses the game during capture.** The tool must mark the intro as seen for each island, and restore each key's previous state (absent or set) afterwards. Pinned by Task 4 Step 6's PlayerPrefs check.
3. **Font assets are missing glyphs the game already prints.**
   - The glyphs: `—` (island labels), `·` (HUD label), `’ “ ” …` (log text), and digits.
   - If they're missing, TextMeshPro silently draws boxes.
   - Pinned by `UIThemeTests.FontsHaveRequiredGlyphs` (Task 3).
4. **Hex colours stored with the wrong colour space or precision.** A palette value that drifts from the spec's hex breaks the "one meaning per colour" rule. Pinned by `LookProfileTests.IslandPalettesMatchSpec` and `UIThemeTests.SignalColoursMatchSpec`, which compare `ColorUtility.ToHtmlStringRGB` against the spec hex.
5. **The shot list changes between runs, so before/after comparisons don't line up.** Shot poses must be generated once and saved. Capture must only read the saved list. Pinned by Task 4 Step 2's re-run check (the asset is unchanged after a second capture).

---

### Task 1: Concept boards

**Files:**
- Create: `docs/look/concepts/` with one standalone `.html` per approved board:
  - `art-style.html`
  - `darkness.html`
  - `island-palettes.html`
  - `keeper-silhouette.html`
  - `lantern.html`
  - `moths.html`
  - `shades.html`
  - `beacons.html`
  - `ui-theme.html`
- Create: `docs/look/concepts/index.md`

**Interfaces:**
- Consumes: the brainstorm fragments in `.superpowers/brainstorm/1124-1790969820/content/` (same file names; skip `waiting-*.html`). Also the companion frame CSS in `C:\Users\heman\AppData\Roaming\Claude\local-agent-mode-sessions\483262b0-6e5d-4a19-ab3e-7d35d666b344\93b0327d-06d1-4a36-982a-6a969b141607\rpm\plugin_01JETPTMWTgidsotmEhVkQTG\skills\brainstorming\scripts\frame-template.html`.
- Produces: standalone boards that render in any browser with no server.

- [ ] **Step 1: Wrap each fragment in a standalone page.** For each fragment, produce a full document:
  - `<!doctype html>`, charset and viewport metas
  - the frame template's `<style>` block copied in (only the CSS: no helper script, no WebSocket code)
  - a dark `body` background
  - the fragment's content unchanged

  Remove the `onclick="toggleSelect(this)"` attributes. Add a "Chosen" badge (a small amber label) to the card for the approved option on each board:
  - art-style: painterly
  - darkness: balanced
  - island-palettes: all four approved
  - keeper: wanderer
  - lantern: storm
  - moths: dusty
  - shades: smoke
  - beacons: lamp
  - ui-theme: iron
- [ ] **Step 2: Write `index.md`.** One row per board: file, the decision, and one line of rationale. Take these from the spec's Section 1. Link to the spec.
- [ ] **Step 3: Verify.** Open three boards (`art-style.html`, `island-palettes.html`, `ui-theme.html`) in the browser pane, or with any local file viewer. Expected: each renders with no server, shows the "Chosen" badge on the right card, and has no console errors.
- [ ] **Step 4: Commit.**
  ```bash
  git add docs/look/concepts
  git commit -m "docs: Phase A concept boards for the visual pass"
  ```

### Task 2: LookPalette, LookProfile and the four island profiles

**Files:**
- Create: `Assets/Game/Scripts/Logic/LookPalette.cs`
- Create: `Assets/Game/Scripts/Look/LanternKeeper.Look.asmdef`
  - references: `LanternKeeper.Logic`, `Unity.TextMeshPro`
  - `autoReferenced: true`
- Create: `Assets/Game/Scripts/Look/LookProfile.cs`
- Create: `Assets/Game/Art/Look/LookProfile_island1.asset` … `LookProfile_island4.asset`
- Modify: `Assets/Game/Scripts/LevelConfig.cs` (add `public LookProfile lookProfile;`)
- Modify: `Assets/Game/Levels/Island1.asset` … `Island4.asset` (assign the profiles)
- Modify: `Assets/Game/Editor/SceneWiring.cs`. In the validation report, add: "every LevelConfig referenced by the build scenes has a lookProfile whose levelId matches".
- Modify: `Assets/Game/Tests/EditMode/LanternKeeper.Tests.EditMode.asmdef` (add references `LanternKeeper.Look` and `Unity.TextMeshPro`)
- Test: `Assets/Game/Tests/EditMode/LookProfileTests.cs`

**Interfaces:**
- Produces, `LookPalette` (static class, namespace `LanternKeeper`):
  - `public const string LanternAmber = "FFB15C"`, `GlowCore = "FFD38A"`, `DrainViolet = "B48CFF"`, `LightningBlue = "BCD2FF"`, `FireflyGreen = "B8FFB0"`
  - `public static Color FromHex(string hex)`: sRGB, alpha 1. Throws `ArgumentException` on malformed input.
  - `public static string ToHex(Color c)`: uppercase, RGB only, no `#`.
- Produces, `LookProfile : ScriptableObject` (`[CreateAssetMenu(menuName = "Lantern Keeper/Look Profile")]`), public fields:
  - `string levelId`
  - `Color sky`, `sea`, `land`, `moonRim`, `extra`
  - `bool moonOn`, `Color moon`
  - `Color fogColour`, `float fogDensity`
  - `float moonRimStrength`
  - `bool mist`, `bool rain`
  - `float gradeSaturation`, `float gradeContrast`, `float gradeExposure`
  - `public bool Validate(out string problem)`: returns false with a reason when:
    - `levelId` is empty
    - `fogDensity` is outside `[0, 0.1]`
    - `moonRimStrength` is outside `[0, 2]`
    - the grade values are outside these ranges: saturation `[-100, 100]`, contrast `[-100, 100]`, exposure `[-3, 3]`
    - `moonOn` is true and the moon alpha is 0
- Asset values:
  - Colours: from the Global Constraints palette table.
  - `fogColour`: the island's sea colour.
  - `fogDensity`:
    - island1 0.012
    - island2 0.014
    - island3 0.020
    - island4 0.016 (matches the current Island 4 fog)
  - `moonRimStrength`: 1.0 everywhere, except island4 0.6 (no moon; the rim is lightning-lit).
  - Grade: saturation 0, contrast 0, exposure 0. Phase B tunes these.

- [ ] **Step 1: Write the failing tests** in `LookProfileTests.cs`:
  - `FromHexRoundTrips`: `ToHex(FromHex("FFB15C")) == "FFB15C"` for all five `LookPalette` constants.
  - `FromHexRejectsMalformed`: `Assert.Throws<ArgumentException>` for `"FFB15"`, `"GGGGGG"` and `""`.
  - `IslandPalettesMatchSpec`:
    - Load the four assets via `AssetDatabase.LoadAssetAtPath<LookProfile>("Assets/Game/Art/Look/LookProfile_island1.asset")` and so on.
    - For each island, assert `ToHex(sky/sea/land/moonRim/extra/moon)` equals the Global Constraints table, and that `moonOn` is true for islands 1–3 and false for island 4.
    - Assert `mist` for island2 and island3 and `rain` for island4. All other flags are false.
  - `ProfilesValidate`: each asset's `Validate(out p)` is true. Also, a `ScriptableObject.CreateInstance<LookProfile>()` with `fogDensity = 0.5f` returns false.
  - `LevelConfigsReferenceTheirProfile`: each `Assets/Game/Levels/IslandN.asset` `LevelConfig` has a non-null `lookProfile` with `lookProfile.levelId == config.levelId`. `LevelConfig` lives in Assembly-CSharp, so load it as `ScriptableObject` and read the field via `SerializedObject(...).FindProperty("lookProfile")`.
- [ ] **Step 2: Run the EditMode tests.** Expected: compile failure or FAIL, because `LookPalette` and `LookProfile` don't exist yet.
- [ ] **Step 3: Implement** `LookPalette`, the `LanternKeeper.Look` asmdef, `LookProfile`, the four assets, the `LevelConfig` field and assignments, and the SceneWiring check.
  - Create the assets from code (a one-off `execute_code` call) or by hand, but commit the `.asset` files.
  - The SceneWiring check is only about data, so it doesn't change any scene.
- [ ] **Step 4: Run the EditMode tests.** Expected: all `LookProfileTests` pass, and all 50 existing tests still pass. Run Build All Levels then Validate Scene Wiring: 0 problems. `git diff --stat` on `Assets/Game/Scenes` shows no semantic changes; revert regenerated scene and terrain noise.
- [ ] **Step 5: Commit.**
  ```bash
  git commit -m "feat: LookProfile data per island with spec palettes"
  ```

### Task 3: Fonts and UITheme

**Files:**
- Create: `Assets/Game/Art/UI/Fonts/`:
  - `CormorantGaramond-Bold.ttf`, `CormorantGaramond-SemiBold.ttf`
  - `Spectral-Italic.ttf`, `Spectral-Regular.ttf`
  - `Inter-Regular.ttf`, `Inter-SemiBold.ttf`
  - `OFL-CormorantGaramond.txt`, `OFL-Spectral.txt`, `OFL-Inter.txt`
- Create: TMP SDF assets next to them, `<FontName> SDF.asset`, one per TTF.
- Create: `Assets/Game/Scripts/Look/UITheme.cs`
- Create: `Assets/Game/Art/UI/UITheme.asset`
- Modify: `CREDITS.txt` (a "Fonts (SIL Open Font License 1.1)" section in the existing style, with family, author and source URL per font)
- Test: `Assets/Game/Tests/EditMode/UIThemeTests.cs`

**Interfaces:**
- Consumes: `LookPalette` (Task 2).
- Produces, `UITheme : ScriptableObject` (`[CreateAssetMenu(menuName = "Lantern Keeper/UI Theme")]`):
  - Fonts: `TMP_FontAsset titleFont` (Cormorant Garamond Bold), `flavourFont` (Spectral Italic), `uiFont` (Inter Regular), `uiFontStrong` (Inter SemiBold).
  - Colours:
    - signal colours `amber`, `glowCore`, `drainViolet`, `lightningBlue`, `fireflyGreen`
    - panels and text: `inkPanel = #0D1020` (alpha 0.88), `brassLine = #6B5A44`, `textPrimary = #F3E6CF`, `textMuted = #C8B9A0`
  - `int minHudFontPx = 18`
  - Sprites: `Sprite panelFrame`, `buttonFrame`, `iconLantern`, `iconBeacon`, `iconMoth`, `iconShield`, `iconEmber`. These may be null in Phase A; Phase E and F fill them in.
  - `public bool Validate(out string problem)`: fails if any font is null, or if `minHudFontPx < 18`. Sprites aren't checked.

- [ ] **Step 0 (controller): Get approval for the download.** The controller asks the user before dispatching this task.
  - The files: the static TTFs listed above, plus each family's `OFL.txt`.
  - The source: the Google Fonts GitHub repository `https://github.com/google/fonts/tree/main/ofl/` (folders `cormorantgaramond`, `spectral`, `inter`), or the family ZIPs from fonts.google.com, using the files under `static/`.
  - The size: about 3 MB in total.
  - If the user declines, they place the files in `Assets/Game/Art/UI/Fonts/` themselves.
- [ ] **Step 1: Write the failing tests** in `UIThemeTests.cs`:
  - `ThemeValidates`: load `Assets/Game/Art/UI/UITheme.asset`. `Validate(out p)` is true.
  - `SignalColoursMatchSpec`: `LookPalette.ToHex(theme.amber) == LookPalette.LanternAmber`, and the same for the four other signal colours.
  - `FontsHaveRequiredGlyphs`: for each of the four fonts:
    - `font.HasCharacters("—·’“”…0123456789ABCabc", out List<char> missing, false, true)` is true. `tryAddCharacter` is true, so dynamic atlases can add the glyphs.
    - the message lists `missing`
  - `FontsAreStaticNotVariable`: each `font.sourceFontFile` name contains no `[` (variable fonts are named like `Inter[opsz,wght].ttf`).
- [ ] **Step 2: Run the EditMode tests.** Expected: FAIL (UITheme is missing).
- [ ] **Step 3: Import and create.**
  - Place the TTFs and licences.
  - Generate the TMP SDF assets with `TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic)` and save them as assets.
  - Create `UITheme.asset` with the fonts and colours above.
  - Update `CREDITS.txt`.
- [ ] **Step 4: Run the EditMode tests.** Expected: all `UIThemeTests` pass, plus all earlier tests.
- [ ] **Step 5: Commit.**
  ```bash
  git commit -m "feat: UITheme with OFL fonts (Cormorant Garamond, Spectral, Inter)"
  ```

### Task 4: Baseline capture tool and the "before" set

**Files:**
- Create: `Assets/Game/Editor/Look/LookShot.cs`: a `[Serializable]` struct with `string label`, `Vector3 position`, `Vector3 euler`, `float fov`.
- Create: `Assets/Game/Editor/Look/LookShotList.cs`: a ScriptableObject with `string levelId` and `List<LookShot> shots`.
- Create: `Assets/Game/Editor/Look/LookShotGenerator.cs`: menu item `Lantern Keeper/Look/Generate Shot Lists`.
- Create: `Assets/Game/Editor/Look/LookCaptureState.cs`: snapshot and restore of editor state.
- Create: `Assets/Game/Editor/Look/LookBaselineCapture.cs`: menu item `Lantern Keeper/Look/Capture Look Baseline`.
- Create: `Assets/Game/Art/Look/Shots/LookShots_island1.asset` … `_island4.asset`
- Create: `docs/look/baseline/<YYYY-MM-DD>/…` (the captured output) and `docs/look/baseline/<YYYY-MM-DD>/frametimes.md`

**Interfaces:**
- Consumes:
  - `GraphicsQuality.Set(GraphicsLevel)` and `GraphicsQuality.Current` (`Scripts/Graphics/GraphicsQuality.cs`)
  - the PlayerPrefs keys `LanternKeeperIntro_<levelId>` and `LanternKeeperDifficulty`
  - `Moth`, `Shade`, `PlayerController` and `CameraFollow` (Assembly-CSharp, which is visible to the editor assembly)
- Produces:
  - `LookShotList` assets
  - `LookCaptureState`:
    - `static LookCaptureState Snapshot()`, which records: graphics level, the PlayerPrefs above (present and value), timeScale, runInBackground, active scene path, and play-mode state
    - `void Restore()`
  - `LookBaselineCapture.Run(string outputDir)`

- [ ] **Step 1: Shot-list generator.** For each island scene (`Assets/Game/Scenes/Island1..4.unity`), compute the poses below from scene data. Every shot uses fov 50, except grass-at-feet, which uses 60.
  - **spawn:** the gameplay camera's start pose, i.e. its transform after `CameraFollow` initialises in the first play frame. Alternatively the scene-saved camera transform, if that matches.
  - **beacon:** 7 m from the beacon nearest the player, 2.5 m up, looking at the beacon's top.
  - **shoreline:** the first point on a ray from the island centre toward +X where terrain height crosses the water level. Camera 6 m inland, 2 m up, looking out to sea.
  - **cliff:** the steepest terrain sample (largest height delta over 2 m on an 8 m grid). Camera 10 m away horizontally, at mid-height, looking at it.
  - **water:** 1.2 m above the water, 4 m offshore from the shoreline point, pitched 25° down.
  - **grass:** 1.5 m from the player spawn, 0.6 m up, looking at the player's feet.
  - **moth:** 1.5 m in front of the spawn camera. Capture places a moth 2 m ahead of the camera.
  - **shade** (island4 only): the same pose as moth. Capture places a Shade 4 m ahead.

  Save each list to `Assets/Game/Art/Look/Shots/LookShots_<levelId>.asset`. Overwriting needs an explicit "Regenerate" confirmation dialog, so lists never change by accident.
- [ ] **Step 2: Capture routine.** Write `LookBaselineCapture.Run`. It's an `EditorApplication.update`-driven state machine (no async), wrapped so that `LookCaptureState.Restore()` runs in a `finally`/`OnFailure` path. The sequence:
  1. `Snapshot()`.
  2. Set `runInBackground = true`.
  3. For each island:
     a. Set `LanternKeeperIntro_<levelId> = 1`.
     b. Open the scene and enter play mode.
     c. For preset in [Low, Ultra]: `GraphicsQuality.Set(preset)`, then wait 60 frames.
     d. Disable `CameraFollow`. For each shot:
        - set the main camera pose and fov
        - for moth/shade shots: move the first live `Moth` (or `Shade`) to the placement and set `enabled = false`, to freeze its AI
        - wait 5 frames
        - render the main camera into a 1920×1080 `RenderTexture`. Set the camera's `targetTexture`, call `Render()`, read it back with `ReadPixels`, and encode PNG to `<out>/<island>/<preset>/<label>.png`.
     e. Frame time: return the camera to the spawn pose and re-enable `CameraFollow`. Average `Time.unscaledDeltaTime` over 300 frames after 60 warm-up frames. Append a row to `frametimes.md` with island, preset, average ms and fps.
  4. Screens (Game view, not render-to-texture, so the overlay UI is included):
     - **main menu:** open `MainMenu.unity` in play mode, wait 60 frames, capture `ScreenCapture.CaptureScreenshotAsTexture()`.
     - **journal:** invoke the Keeper's Log button's `onClick`, then capture.
     - **HUD:** Island 1 in play mode at spawn, captured.
     - **pause:** call `GameManager.Instance.Pause()` (or simulate Esc), then capture.
     - Do each on Low and Ultra. Save under `<out>/screens/<preset>/<screen>.png`. If the Game view isn't 1920×1080, record its actual size in `frametimes.md`, since the user sets the Game view to 1920×1080 before running.
  5. Exit play mode and `Restore()`.
- [ ] **Step 3: Run it.** Run `Lantern Keeper/Look/Capture Look Baseline` with output `docs/look/baseline/<today>/`. Expected:
  - 29 shots × 2 presets = 58 world PNGs
  - 4 screens × 2 presets = 8 screen PNGs
  - `frametimes.md` with 8 island-and-preset rows
- [ ] **Step 4: Verify the restore.**
  - `git status --short` shows only `docs/look/baseline/…` and the new tool and asset files.
  - The graphics preset, the intro keys (their previous state each), difficulty, timeScale and runInBackground all match their values from before the run. Log them before and after.
- [ ] **Step 5: Re-run stability.** Run the capture a second time into a scratch folder outside the repo.
  - `LookShots_*.asset` must be unchanged (`git diff --quiet` on them).
  - Images must be visually equivalent to the first run. Compare spawn and beacon PNG pairs by mean absolute pixel difference, which must be under 3/255. Animated water, grass and particles may differ slightly, so equivalence here is visual, not bit-exact.
- [ ] **Step 6: Forced-failure restore check.** Temporarily throw from the capture on island2, Ultra. Confirm the editor returns to play-mode off with the snapshot state restored, as in Step 4. Then remove the forced throw.
- [ ] **Step 7: Look at the images.** Open three Ultra images (island1 spawn, island4 shade, the main menu) to confirm they're correct (not black, not blank, with the right framing). Fix poses if a shot is obviously broken: inside terrain, or facing the sky.
- [ ] **Step 8: Regression.** Run all EditMode tests and the PlayMode smoke test. Run Build All Levels then Validate Scene Wiring: 0 problems. Revert scene, terrain and ProjectSettings noise.
- [ ] **Step 9: Commit.**
  ```bash
  git add Assets/Game/Editor/Look Assets/Game/Art/Look/Shots docs/look/baseline
  git commit -m "feat: Look baseline capture tool and Phase A before-set"
  ```
