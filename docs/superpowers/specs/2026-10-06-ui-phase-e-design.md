# Visual Pass Phase E: Menus, Settings and Keeper's Log — Design

Status: approved in conversation on 2026-10-06, pending spec review.

**Parent:** `2026-10-03-art-direction-design.md` (Phase A).

Its rules are binding here:
- the "forged iron and amber" UI style;
- the fixed signal colours;
- readability rule 3: HUD and UI text is at least 18 px at 1080p;
- the UI technology decision: uGUI and TextMeshPro, authored prefabs styled from `UITheme`, and no UI Toolkit.

## Goal
Restyle every menu screen in the forged-iron and amber look and add one Settings screen with the essential options. Gameplay and the in-game HUD are unchanged. The HUD redesign belongs to Phase F.

## Current state (for reference)
- **Main menu (`MainMenu.cs`, 467 lines):**
  - plain dark buttons for Play, Difficulty, Music, Graphics, Keeper's Log and Quit;
  - an island list with best times;
  - the default TMP font.
- **Keeper's Log:** plain text over the 3D scene.
- **Pause:** built in code inside `HUD.cs` (2,178 lines), with Resume, Restart, How to Play, Graphics and Main Menu.
- **Graphics menu:** `Graphics/GraphicsMenu.cs` (553 lines), a separate screen.
- **`UITheme.asset` and fonts:** they exist, with Cormorant Garamond, Spectral and Inter SDF assets, but nothing reads them yet.
- **Audio:** `AudioManager` has an `AudioMixer` with Music, Sfx and Ambience groups.
- **Lightning:** it drives `ColorAdjustments.postExposure` pulses.

## 1. Screens and navigation (user choices: menu C, settings B, log A)

**Main menu: a ledger with an island list.** It is one framed ink panel on the left with the live island scene behind it.

The panel has a left column and a right column:
- **Left column:**
  - the title in Cormorant Garamond;
  - the flavour line in Spectral italic;
  - **Play** (the amber primary button), **Keeper's Log**, **Settings** and **Quit**.
- **Right column: the island list.** Each row shows:
  - one small beacon "roof" icon per beacon on that island, with as many amber as Keeper's Log pages found there (pages are found per beacon);
  - the island name;
  - the best time;
  - a lock state for unavailable islands.

  The selected row is amber, and Play starts it. The default selection is the latest unlocked island.
- **Difficulty** becomes a 3-way switch (Easy, Normal, Hard) in the island list header.
- The **Music on/off** button is removed. Music volume replaces it.

**Settings: a side list.** The section list runs down the left, and the selected section's rows sit on the right under a one-line Spectral italic flavour line.

| Section | Rows |
|---|---|
| Display | Brightness calibration, with a test card showing a dark cliff edge and the sea that should be "just visible" at the correct value. Resolution (a dropdown of supported resolutions). Window mode: Fullscreen, Borderless or Windowed. |
| Graphics | The existing graphics options, restyled. Behaviour is unchanged. |
| Audio | Master, Music, Effects and Ambience volume, each 0–100%. |
| Accessibility | Text size: 100%, 115% or 130%. Reduce flashing: On or Off. |
| Controls | A read-only list of the keyboard and gamepad bindings. |

- Settings opens from the main menu and from pause.
- **Back** returns to whichever screen opened it.
- Changes apply immediately and are saved. There is no Apply button.

**Keeper's Log: an open book.**
- It is a two-page parchment spread, with a centre gutter and a red ribbon.
- Each island gets one spread: the island title, then "pages N of M".
- Entries are written in Spectral italic, ink on parchment.
- Pages not yet found show "— a torn page, not yet found —".
- **Prev** and **Next** turn the spread. So do the gamepad bumpers and the left and right arrow keys. A turn is a short fade of about 0.2 s.

**Pause.**
- The same ink and brass panel.
- **Resume** is the amber primary button, followed by **Restart**, **How to Play**, **Settings** and **Main Menu**. **Graphics** is replaced by Settings.

**Navigation (all screens):**
- Mouse, keyboard and gamepad all work.
- Every screen has a first-selected element and explicit navigation, with no dead ends.
- The focused element shows a brass outline and an amber glow.
- Escape or gamepad B always goes back one level.

## 2. Component kit and build

**`UITheme`** gains slots for:
- panel ink;
- brass line;
- amber primary and its gradient;
- parchment base, edge and ink-text colours;
- focus outline and glow;
- body, label and title text sizes.

UI code reads its look only from `UITheme`.

**Kit** (`Assets/Game/Scripts/UI/Kit/`). Each component styles itself from `UITheme` in `OnEnable`.

| Component | What it does |
|---|---|
| `InkPanel` | A dark panel with a double brass frame line. |
| `ThemedButton` | Primary (amber) or plain, plus the focus outline and glow. |
| `SliderRow` | A label on the left and a slider on the right. |
| `SwitchRow` | A label and a 2- or 3-way segmented choice. |
| `DropdownRow` | A label and a dropdown. |
| `SectionList` | The side list of sections. It shows the selected section's panel and hides the rest. |
| `ParchmentSpread` | Two pages, the ribbon, Prev/Next and the page-turn fade. |
| `TextScaler` | Applies the accessibility text scale to every TMP label beneath it. |

**Screens** (`Assets/Game/Scripts/UI/Screens/`). Each has one purpose:
- `MainMenuScreen` and `IslandList`. These replace the menu presentation in `MainMenu.cs`. Level loading and unlock logic stay where they are.
- `SettingsScreen`, with one small class per section: `DisplaySection`, `GraphicsSection`, `AudioSection`, `AccessibilitySection` and `ControlsSection`.
  - `GraphicsSection` wraps `GraphicsMenu`'s existing option logic, which is left unchanged, and only replaces its presentation.
- `KeepersLogScreen`.
- `PauseScreen`. The pause panel code moves out of `HUD.cs`, and `HUD` keeps a reference to `PauseScreen`. The rest of `HUD.cs` is untouched until Phase F.

**Builder:**
- The editor menu item **Lantern Keeper → Build UI** generates the screen prefabs under `Assets/Game/Prefabs/UI/` from the kit.
- `IslandBuilder` and the menu scene builder place these prefabs.
- The prefabs can be hand-tuned afterwards. Rebuilding overwrites them in place and keeps their GUIDs.

**Settings data (`GameSettings`, PlayerPrefs).** New values:

| Setting | Values |
|---|---|
| `MasterVolume`, `MusicVolume`, `EffectsVolume`, `AmbienceVolume` | 0–1, default 0.8 |
| `Brightness` | −1 to +1, default 0 |
| `Resolution` | Width × height and refresh rate |
| `WindowMode` | Fullscreen, Borderless or Windowed |
| `TextScale` | 1.0, 1.15 or 1.3 |
| `ReduceFlashing` | Boolean, default off |

**Migration:** if the old music on/off key is "off", `MusicVolume` starts at 0.

**Wiring:**
- **Volumes:** sent to the mixer through exposed parameters, `MasterVol`, `MusicVol`, `SfxVol` and `AmbienceVol`, which are added to the existing mixer. The conversion is `dB = linear > 0.0001 ? 20·log10(linear) : −80`.
- **Brightness:** added to `ColorAdjustments.postExposure` as a base offset of ±1 EV across the slider range. Lightning's pulse adds on top of the user base, not the authored base.
- **Reduce flashing:**
  - caps the lightning exposure pulse at 35% of its normal peak;
  - caps `Flash01`-driven world brightening at 35%;
  - halves the low-fuel pulse frequency.

  These signals keep their meaning without strobing.
- **Text scale:** a global value that `TextScaler` reads. It applies to menus, pause and the log. It applies to the HUD too, but only where HUD text already uses TMP; the HUD restyle itself is Phase F.

## 3. Testing and validation

**EditMode tests:**
- **`UITheme`:** every slot is assigned; the signal colours exactly match the parent spec; body text is at least 18 px at 1080p at a text scale of 1.0.
- **Pure logic** (Logic assembly, `SettingsMath`):
  - volume to dB, including 0 → −80;
  - brightness clamping and its EV mapping;
  - text-scale options;
  - the reduce-flashing caps.
- **`GameSettings`:**
  - save and load round-trip for every new key;
  - music on/off migration.
- **Built prefabs:**
  - the required components are present and wired;
  - only theme fonts are used, with no `LiberationSans` or TMP default font on any visible label;
  - every screen has a first-selected element, and every selectable has a navigation target or explicit `None` at the panel edge.

**PlayMode tests:**
- The existing smoke test.
- `MenuNavigationTest`, using simulated keyboard input. Each step asserts the active screen.
  - main menu → Settings → each section → Back
  - main menu → Keeper's Log → Next and Prev → Back
  - island → pause → Settings → Back → Resume

  Back always returns to the opener.
- `SettingsEffectTest`:
  - changing Music volume changes the mixer's `MusicVol`;
  - changing Brightness changes the volume's `postExposure` base;
  - Reduce flashing lowers the lightning peak.

**Validate Scene Wiring:**
- MainMenu and Islands 1–4 use the new screen prefabs.
- No visible TMP text uses a non-theme font.

**Look:**
- 1080p captures of each screen on Low and Ultra, plus Settings and the Log at 130% text.
- `docs/look/compare/phase-e.html`, covering the capture tool's screens set before and after: main menu, pause, journal and HUD.

**User checkpoints:**
1. Midway: screenshots of the restyled main menu and Settings before the Log and pause are built.
2. End: a playable non-development build before merge.

**Performance:** UI changes must not regress the D2 perf numbers by more than 0.3 ms on any island or preset.

## Out of scope
- The HUD redesign (fuel lantern icon, drain icons) and the remaining `HUD.cs` breakup: Phase F.
- Key rebinding.
- Moths, Shades and beacons: Phase F.
- Localisation.
