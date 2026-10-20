# Uploading Lantern Keeper 1.1.0 to itch.io

One itch.io page serves both the in-browser game and the Windows download. The itch.io dashboard changes now and then, so the points marked **Double-check** are worth a glance against the live UI.

## Files to upload
- `Builds/itch/LanternKeeper-1.1.0-web.zip`: the browser build. `index.html`, `Build/` and `TemplateData/` sit at the zip root, which itch.io requires.
- `Builds/itch/LanternKeeper-1.1.0-windows.zip`: the Windows build.

## 1. Create the project
1. Log in, then open the dashboard and choose **Create new project** (from the account menu, or `itch.io/game/new`).
2. Set **Title** to "Lantern Keeper" and fill in a short tagline.
3. Set **Kind of project** to **HTML**. This is what enables the browser player.

## 2. Upload the browser build
1. Under **Uploads**, click **Upload files** and pick `LanternKeeper-1.1.0-web.zip`.
2. When the upload finishes, tick **This file will be played in the browser** on that row.
3. Wait for the zip to be processed (a few seconds to a minute). itch.io checks that `index.html` is at the zip root.

## 3. Embed options
Set these in the **Embed options** section that appears for the HTML kind:
- **Viewport dimensions:** 1280 by 720.
- **Mobile friendly:** off. The game needs a keyboard and mouse.
- **Fullscreen button:** on. The game also has its own fullscreen button, but the itch.io one is a good fallback.
- **SharedArrayBuffer support:** leave off. The build doesn't use threads, so it isn't needed.
- Leave "Automatically start on page load" off, so the player gets the game's "Click to start" screen. That click also unlocks browser audio.
- Leave "Enable scrollbars" off.

**Double-check:** the embed size is the page's default. The player can still go fullscreen, and the game scales to the window.

## 4. Upload the Windows build
1. Click **Upload files** again and pick `LanternKeeper-1.1.0-windows.zip`.
2. On that row, tick the **Windows** platform. Leave "played in the browser" off.
3. Optionally label it "Windows 64-bit" in the display name field.

## 5. Pricing
Choose what fits: **No payments** (free), **Pay what you want** (with a minimum of $0), or a fixed price. Free or pay-what-you-want is the usual choice for a first release. You can change this at any time.

## 6. Page content
- **Description:** paste or adapt `docs/press/release-notes-1.1.0.md`. The itch.io editor takes Markdown-style formatting, but check how the headings render. A shorter "what is it" paragraph above the notes helps.
- **Screenshots:** upload from `docs/press/screenshots/2560/`. Pick 5 to 8. A good order: `01-menu`, `03-island1`, `05-island2`, `08-island5-spire`, `09-island5-bridge`, `11-island5-strike-ring`, `13-finale-islands`, `14-finale-sunrise`. The first one becomes the page's main image.
- **Cover image:** `14-finale-sunrise.jpg` or `15-finale-lighthouse.jpg`, cropped to **630 by 500**. The cover shows in feeds and search results, so keep the subject in the middle. itch.io recommends at least 630 by 500 and no more than 3 MB (GIFs allowed).

## 7. Classification and tags
- **Genre:** Adventure (Platformer also fits).
- **Tags (up to 10):** `3D`, `Atmospheric`, `Exploration`, `Singleplayer`, `Relaxing`, `Short`, `Story Rich`, `Lighthouse`, `Unity`, `First-Person`.
- **Release status:** Released. **Version:** 1.1.0.
- **Inputs:** Keyboard and mouse. Gamepad only if the game supports it.
- **Accessibility:** mention the Reduce flashing option if it is in the settings.
- **Community:** comments on, if you want feedback.

## 8. Publish
1. Leave **Visibility** on **Draft** or **Restricted** first and use **Save**.
2. Open the page with **View page**, click **Run game** and play for a minute: check the loading bar, the click-to-start screen, the music, Island 1 and the fullscreen button.
3. Download the Windows zip from the page and run it once.
4. When it is right, set **Visibility** to **Public** and save.

## Where to double-check
- Exact labels for the embed options and the platform checkboxes (itch.io renames these occasionally).
- Whether the first load is acceptable on a slow connection. The web zip is about 100 MB, so the first visit takes a while. Later visits use the browser cache.
- Browsers that already ran a test build keep any graphics choice they saved. The default of Medium applies only to fresh browsers.

## Rebuilding the browser build
Switch the Editor to the **WebGL** platform first (Lantern Keeper > Web > Switch To WebGL, or File > Build Profiles > Web > Switch Platform), wait for the reimport and compile to finish, then build. A WebGL player built while Windows is the active platform comes out with its lit surfaces invisible (terrain, trees, rocks and keeper wash into the fog colour, some objects turn magenta) and no build error. A build guard (`WebBuildGuard`) now refuses that build. Switch back to Windows afterwards.
