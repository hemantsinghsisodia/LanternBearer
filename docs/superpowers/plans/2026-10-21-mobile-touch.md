# Mobile browser: touch controls for the web build

## Context
The web build (main `50fbfe1`) runs in desktop browsers but needs a keyboard: every gameplay input is a direct `Keyboard`/`Mouse`/`Gamepad` poll. On a phone nothing can move the keeper, the 16:9 letterboxed page is tiny, and prompts say "E". Goal: the same web build plays well on phones and tablets with touch, and the touch buttons appear only when touch is being used.

**User decisions:**
- **Layout:** stick + drag-look.
  - A floating joystick on the left half (it appears where the thumb lands).
  - Dragging on the right half turns the camera.
  - Jump, Light and Sprint buttons sit bottom-right; Pause sits top-right.
- **Detection:** auto, switching live.
  - Touch mode starts on if the device looks like a phone or tablet.
  - Any touch switches it on, and a keyboard press or a real mouse move switches it off.
- **Portrait:** show a "Rotate your phone" overlay. Landscape fills the screen.

## What exists (reuse)
- **Movement hooks (public statics):**
  - `PlayerController.ExternalMove` / `ExternalSprint` / `ExternalJump` (`Assets/Game/Scripts/PlayerController.cs:64,122-123,248-263`). Movement is camera-relative and clamped to magnitude 1, so an analog stick vector fits as is.
  - PlayMode tests already depend on these names, so don't rename them.
- **Input System 1.20 only.** The EventSystem uses `InputSystemUIInputModule` in every scene, so menus, sliders and buttons are already tappable.
- **Camera:** the custom orbit camera `CameraFollow` (`Scripts/CameraFollow.cs`) has `ReadLook` (`:192-220`), `MenuOpen()` (`:345`) and `ApplyCursor` (`:356-372`).
- **Default graphics level:** `GraphicsDefaults.For` (`Scripts/Logic/GraphicsDefaults.cs:13`) is the single default for graphics quality, read at `GraphicsQuality.cs:33`.
- **Template:** `Assets/WebGLTemplates/LanternKeeper/index.html` has the 16:9 frame, the start overlay, the fullscreen button and `unlockAudio`.

## Design

### 1. Input routing (small, keyboard behaviour unchanged)
- **`GameInput` (new static class, `Scripts/Input/GameInput.cs`)** is one place for the button-style actions keyboard and touch share:
  - `InteractPressed()`: E or the touch Light button;
  - `PausePressed()`: Esc/P or the touch Pause button;
  - `JumpPressed()`;
  - `SprintHeld()`.
  - Touch sets one-frame flags, which are consumed per frame like `ExternalJump`.
- **Call sites switch to `GameInput`:**
  - `Beacon.cs:254`, `GreatLamp.cs:198`;
  - `GameManager.HandleKeys` (`:245-261`), for pause;
  - `PlayerController` jump and sprint, which keep the `External*` hooks for tests.
- **`CameraFollow` gains `ExternalLookDelta`** (`Vector2`, in degrees, added to yaw/pitch once and then cleared) for the drag-look. The existing `UseExternalYaw` stays for tests.
- **In touch mode,** `ApplyCursor` skips the cursor lock.

### 2. Touch mode detection (`Scripts/Input/TouchMode.cs`)
- **Pure logic, unit-tested.** `TouchMode.Initial(isMobilePlatform, browserSaysTouch)` returns the starting mode. `TouchMode.Next(current, touchedThisFrame, keyPressedThisFrame, mouseMovedWithoutRecentTouch)` returns the mode for the next frame. A mouse move within 0.5 s of a touch is ignored, so emulated mouse events from a touch don't flip the mode.
- **Runtime sources:**
  - `Application.isMobilePlatform` (true for mobile browsers on WebGL);
  - a new `Assets/Game/Plugins/WebGL/TouchDetect.jslib` with `LK_IsTouchDevice()`, which checks `navigator.maxTouchPoints > 0 && matchMedia('(pointer: coarse)').matches`. Under `#if UNITY_WEBGL && !UNITY_EDITOR`; elsewhere it is false.
  - `Touchscreen.current`, `Keyboard.current.anyKey` and `Mouse.current.delta` each frame.
- **Exposed as `TouchMode.Active`** (a static bool) and an `OnChanged` event, so the overlay and prompts update live.

### 3. On-screen controls (`Scripts/Touch/TouchControlsOverlay.cs` + `VirtualStick.cs`)
- **No scene edits.** A `[RuntimeInitializeOnLoadMethod]` bootstrap creates one `DontDestroyOnLoad` uGUI canvas (ScaleWithScreenSize 1920x1080, match 0.5, sorting above the HUD) built in code. It only exists on WebGL and in the editor.
- **Multi-touch with EnhancedTouch** (`EnhancedTouchSupport.Enable()`). Each finger is claimed by the region where it started. This allows the stick and the look-drag at the same time, and taps on buttons never also turn the camera.
- **Left-half floating stick:**
  - the base appears at the touch-down point;
  - radius 110 reference px, dead zone 0.12;
  - the output goes to `ExternalMove`.
  - Pushing past 90% of the radius for 0.4 s sets an auto-sprint hint, in addition to the Sprint button. **Ruling: the Sprint button is a toggle**, which is easier on a phone.
- **Right-half drag-look:**
  - the pixel delta is converted to degrees with a sensitivity of 0.15 deg/px (scaled by screen DPI so it feels the same across phones);
  - the result goes to `CameraFollow.ExternalLookDelta`.
- **Buttons:** big round 140 px (Jump), 120 px (Light, Sprint) and 90 px (Pause). They use the HUD palette and lantern icons.
  - The Light button pulses when the interact prompt is showing (from `GameManager.ShowInteractPrompt`), and is dimmed otherwise.
- **Safe area:** the layout sits inside `Screen.safeArea` (notches).
- **Visibility:** shown only when `TouchMode.Active`, in an island scene, and not `CameraFollow.MenuOpen()`, `ControlsLocked`, intro card, credits or finale. It fades within 0.15 s.

### 4. Touch wording
- **Interact prompt:** "E  Light the beacon" becomes "Tap  Light" (with the button icon) in touch mode, in `GameManager.ShowInteractPrompt` and `GreatLamp.cs:12`.
- **Cards and credits:**
  - the intro and how-to cards say "Tap to continue";
  - the credits hint says "Tap to skip" (`CreditsRoll.cs:26`). Confirm a tap skips; add a touch press check if it doesn't.
- **Settings > Controls** (`ControlsSection.Bindings`) shows the touch list in touch mode.
- **Main menu:** the Quit button is already hidden on the web.

### 5. Web page (template)
Touch devices are detected in JS with the same check as the jslib, which adds a `lk-touch` class to `<body>`.
- **`body.lk-touch`:**
  - the frame fills `100dvw x 100dvh` (no 16:9 letterbox, no 38 px footer);
  - the "Best in a desktop browser" line is hidden;
  - `touch-action: none` and `overscroll-behavior: none` on the stage (no page scroll or pinch-zoom while playing);
  - `-webkit-user-select: none`.
- **Rotate overlay:** under `@media (orientation: portrait)` and `body.lk-touch`, it covers the game with a "Rotate your phone to landscape" card and an icon. Unity keeps running underneath, and the game is paused through `SendMessage` while the overlay is up.
- **Start tap:** it also tries `stage.requestFullscreen()` and then `screen.orientation.lock('landscape')`, each in try/catch.
  - **iPhone Safari has no element fullscreen,** so the page just fills the view there. Note this in the docs.
- **Viewport meta:** add `viewport-fit=cover` for notches.

### 6. Mobile defaults
- **Graphics:** `GraphicsDefaults.For(platform, isMobile)` returns the Web Low level for mobile browsers and keeps Medium elsewhere. Update `GraphicsDefaultsTests`.
- **Saved choices win:** a player's saved choice still overrides the default.

## Tasks (subagent-driven, Sonnet implementers and reviewers)
1. **Input routing:** `GameInput`, `ExternalLookDelta` and the cursor-lock skip. EditMode tests for the one-frame flag consumption. Existing PlayMode movement tests stay green.
2. **`TouchMode` logic, jslib and runtime tracker:** EditMode tests for `Initial`/`Next`, including the emulated-mouse guard.
3. **Overlay:** stick, look-drag, buttons, safe area and visibility rules.
   - EditMode tests for the stick math (dead zone, clamp, auto-sprint timing) and the finger-claim rules.
   - PlayMode tests with `InputTestFixture`/simulated `Touchscreen`:
     - a drag on the left moves the keeper;
     - a drag on the right turns the camera;
     - Jump jumps;
     - Light lights a beacon in range;
     - a keyboard press hides the overlay.
4. **Wording:** prompts, cards, credits and the Controls list in touch mode, with tests.
5. **Template and mobile default:** CSS/JS (touch class, full-screen fill, rotate overlay, fullscreen and orientation on start) and `GraphicsDefaults` mobile Low.
6. **Build and verify:** a release WebGL build (WebGL active, then switch back), re-zip for itch, and a Windows build check.

## Verification
- **Automated:** the EditMode suite, the PlayMode subset (new touch tests plus the existing movement and credits tests) and Validate.
- **Desktop browser** at 127.0.0.1:8130: no touch UI, keyboard and mouse unchanged.
- **Chrome DevTools device emulation** (iPhone/Pixel, landscape and portrait):
  - the overlay appears;
  - the rotate card appears in portrait;
  - a stick, look and buttons run through Island 1 and lighting a beacon works;
  - a keyboard press hides the overlay.
- **Real phone on the same Wi-Fi:** I run the test server bound to the LAN IP (`--bind 0.0.0.0`; you may need to allow Python through Windows Firewall) and you open `http://<PC-IP>:8130` on your phone. Play Island 1, then try Island 5 for performance.
- **Windows build unchanged:** the overlay never appears without touch.

## Risks / notes
- **Phone performance and memory:** the 106 MB data file and wasm heap may be heavy for older phones, especially iOS Safari memory limits. Web Low helps. If it's still too slow, a follow-up could add a lower render scale for mobile.
- **iPhone:** no true fullscreen and no orientation lock. The page fills the view and the rotate card handles portrait.
- **Constraints:**
  - never touch `UniversalRenderPipelineGlobalSettings.asset`;
  - `Build Profiles/` stays untracked;
  - web builds are made with WebGL active;
  - no scene text-edits (the overlay is created in code);
  - Islands 1–4 terrain hashes are unchanged;
  - merge only on "merge in main and push".
