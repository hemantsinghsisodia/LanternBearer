# Visual Pass Phase F2: In-game HUD and Cards — Design

Status: approved in conversation on 2026-10-08, pending spec review.

**Parents:**
- `2026-10-03-art-direction-design.md` (Phase A). It sets the HUD rules: the lantern fuel icon, beacon roofs that light up amber, and drain icons in place of the "Drain xN" readout. Readability rules 1–4 apply, including HUD text of at least 18 px at 1080p.
- `2026-10-06-ui-phase-e-design.md` (Phase E). It provides the UI kit, `UITheme`, the text-size setting, the installer pattern and the Phase F HUD allow-lists that this phase removes.

## Goal
Redesign the in-game HUD and restyle its cards in the forged-iron and amber style, and break up `HUD.cs`. Gameplay is unchanged.

## Current state (for reference)
- **Top-left:**
  - an orange disc for fuel
  - a faint row of beacon dots
  - the text "Drain x1.00 · Moths 0/2", in low contrast against the corner vignette
  - floating red "-10" fuel penalties
- **Top-centre:** the island name, in plain TMP.
- **Top-right:** the timer.
- **Compass:** amber edge triangles with distances (`BeaconCompass`).
- **Tide (Island 3):** label and arrow text.
- **Toasts:** tide and log-page toasts.
- **Cards:** the intro card and the win/lose panels.
- **Code:**
  - `HUD.cs` is 1,896 lines and builds most of these with `MakeRuntimeText`.
  - It also builds a hidden legacy graphics panel (marked "Phase F2: remove").
  - `MothHUD.cs` (267) and `StormHUD.cs` (228) handle part of it.
  - Phase E left allow-lists for the HUD readouts in `SceneWiring.Ui.cs` (`PhaseFHudTexts`) and in `UiLayoutTests`.

## 1. The always-on HUD (layout A: top-left cluster)
**Top-left panel:** a small translucent ink panel with a thin brass frame (`InkPanel`).
- **Lantern fuel gauge.**
  - The icon is a storm lantern in the same shape as the keeper's and the beacons' lanterns.
  - Its flame size and glow follow the fuel fraction. The flame shifts redder below 30%, and pulses softly below 15%. The pulse rate halves when Reduce flashing is on.
  - A thin amber bar sits beside the icon.
  - Fuel changes flash the bar and show "−10" or "+5" next to it for about 1 s. This replaces the floating penalty text.
- **Beacon roofs.** One roof per beacon on the island, sitting under the gauge. A roof is amber once its beacon is lit, otherwise it is dim brass, using the same icon as the main menu's island rows.

**Drain icons** (under the panel, replacing the drain text):

| Icon | Colour | Shows when |
|---|---|---|
| Moth ×N | violet `#B48CFF` | moths are draining you |
| Shield | amber | inside a lit beacon's safe ring |
| Ember | — | sprint is adding drain |
| "×1.3" (small) | — | the difficulty drain multiplier is above 1 |

Each icon is hidden when its condition doesn't apply.

**Other HUD elements:**
- **Island name (top-centre):** Cormorant, subtle.
- **Timer (top-right):** Inter numbers.
- **Compass markers:**
  - `BeaconCompass` is restyled: a small roof icon plus the distance in Inter, on a soft dark backing.
  - Its logic is unchanged: direction, distance and edge clamping.
- **Tide chip (Island 3):** a compact chip under the drain icons with a rising or falling arrow and a short bar, in the cool tide colour.
- **Readability:** every HUD element has an ink backing or a soft text shadow, so nothing fades into the vignette. Text is at least 18 px at 1080p at 100% and follows the text-size setting (`TextScaler`).

## 2. Cards, toasts and code
**Intro card:**
- The ink panel with its brass frame.
- The title "Island N · Name" in Cormorant.
- The story text in Spectral italic.
- The footer prompt in Inter. The wording is unchanged.
- It fades in and out over 0.3 s, using unscaled time.

**Win panel:**
- The title "The shore is lit".
- Your time, your best time, and a "New best!" marker when it applies.
- The beacons shown as lit roofs.
- Buttons: **Next island** (primary), **Retry**, **Main Menu**. On the last island, Next island is hidden.

**Lose panel:**
- The title "The flame went out".
- One fixed line in Spectral italic: "Your lantern went dark." `GameManager.Lose()` has a single trigger (fuel at 0). The sea and Shades only take fuel, so no per-cause line is needed and no gameplay code changes for this.
- Buttons: **Retry** (primary), **Main Menu**.

Both panels use the kit buttons with keyboard and gamepad focus. The first button is selected.

**Toasts:**
- Small ink toasts near the top that fade in and out.
- Log-page toasts are parchment-tinted to tie them to the Keeper's Log book.
- They queue and never overlap.

**Code split.** `HUD.cs` becomes a thin coordinator, under about 400 lines, that reads `GameManager` and the other game state and drives one component per job:

| Component | Job |
|---|---|
| `FuelGauge` | Lantern icon, fuel bar, change flash |
| `BeaconRoofs` | One roof per beacon, lit or dim |
| `DrainIcons` | Moth ×N, shield, ember, multiplier |
| `TimerLabel` | Run timer |
| `IslandLabel` | Island name |
| `TideChip` | Island 3 tide arrow and bar |
| `IntroCard` | Island intro card |
| `ResultPanel` | Win and lose panels |
| `Toasts` | Queued toasts |
| `FpsReadout` | FPS counter |

- `MothHUD` and `StormHUD` are folded into these or reduced to their non-presentation duties.
- The HUD prefab is built by the Phase E **Build UI** builder (`UIBuilder`) and placed by the island installer. There are no full level rebuilds.
- **Removed:**
  - the legacy hidden graphics panel and `GraphicsMenu`'s unreachable presentation;
  - every "Phase F2: remove" item;
  - the Phase E HUD allow-lists (`PhaseFHudTexts` and the `UiLayoutTests` HUD exemptions).
- **F1 review leftovers** (small and safe only):
  - the dead `Wings`, `Flare` and `LightColumn` lookups in `Moth.cs`, `Beacon.cs` and `LightQuality.cs`;
  - the stale `MoonRimFeature.cs:139` comment;
  - making `InstallShade` idempotent.

  These touch gameplay files only to delete dead lookups. They don't change behaviour.

## 3. Testing and validation
**EditMode tests:**
- **Pure Logic `HudMath`:**
  - fuel fraction to flame scale, glow and colour (redder below 0.30, pulse below 0.15, pulse rate halved when reduce flashing is on);
  - drain-icon visibility from a state record (moth count, in a safe ring, sprinting, multiplier).
- **Beacon roofs:** the roof count equals the beacon count, and lit roofs match lit beacons.
- **HUD and card prefabs:**
  - the prefabs are wired;
  - only theme fonts are used;
  - all text is at least 18 px at 1.0.
- **Layout at 130%:** nothing overflows at 1920×1080 or 1280×720, including the HUD panel, toasts and cards. The Phase F HUD allow-lists are gone and these checks pass without them.

**PlayMode tests:**
- `HudLiveTest` on Island 1:
  - draining fuel shrinks the gauge;
  - lighting a beacon turns its roof amber;
  - a draining moth shows the moth icon with the right count;
  - standing inside a lit ring shows the shield.
- `ResultPanelTest`:
  - win and lose panels appear;
  - their buttons work;
  - focus starts on the primary button.
- Existing tests pass: navigation, pause, beacons, settings and reachability. The known Island 4 flake is ignored.

**Validate Scene Wiring:**
- every island has the HUD prefab;
- no `MakeRuntimeText` HUD text remains in island scenes;
- no non-theme font appears on any HUD text.

**Look:**
- 1080p captures of the HUD on every island, Low and Ultra, and at 130%;
- the intro card, win panel, lose panel and a toast;
- `docs/look/compare/phase-f2.html` with before and after.

**Performance:**
- One probe pass per island, Low and Ultra, against F1.
- Limit: no more than +0.3 ms average.
- If it fails, report the numbers to the user before any investigation.

**User checkpoints:**
1. Midway: screenshots of the new HUD in play, before the cards are built.
2. End: a playable non-development build, approved before merge.

## Out of scope
- Gameplay changes.
- New HUD features or information.
- Controller-glyph art for prompts.
- Localisation.
