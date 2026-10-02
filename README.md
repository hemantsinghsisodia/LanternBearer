# Lantern Keeper

A small 3D night game made in Unity 6 with the Universal Render Pipeline. Walk a foggy island, keep your lantern lit, and light five beacons before the flame goes out.

## Requirements

- Unity **6000.6.3f1**
- The project uses URP and the Input System package

## Open and play

1. Open this folder in Unity Hub.
2. Open `Assets/Game/Scenes/MainMenu.unity`.
3. Press Play, then choose a difficulty and an island.

Island 2 unlocks after you win Island 1, and Island 3 unlocks after you win Island 2. The first island has 5 beacons, the second is larger and has 7, and the third is a drowned marsh with 9.

**Island 3: the tide.** The water rises and falls about 0.9 m every 90 seconds. A gauge under the drain readout shows the level, whether it is rising or falling, and the seconds until it turns, and a "Tide turning" notice appears just before it changes. Sandbars between the outer islets are walkable at low tide and flooded at high tide. Only lantern-revealed stepping stones stay above water at high tide, so you can wait for the tide or spend light to cross. Falling in respawns you on ground that stays above the high-tide line, and fireflies never settle below it. A low surf sound swells with the water.

**Keeper's Log.** Each beacon you light shows a short page from the previous keeper's story for that island, as a notice that fades after a few seconds without pausing the game. Pages you have read are saved, and the **Keeper's Log** button on the main menu lists them.

## Controls

| Key | Action |
| --- | --- |
| WASD | Move, relative to the camera |
| Mouse | Orbit the camera while the cursor is locked |
| Left / Right arrows | Rotate the camera |
| Right stick | Orbit the camera |
| Shift | Sprint. This burns lantern fuel faster |
| Space | Jump. Two beacons sit on raised ground |
| E | Light a beacon when you are standing next to it. The prompt shows the fuel cost |
| Esc or P | Pause. Resume, restart, change graphics, or return to the main menu. The cursor unlocks while paused. Esc or P on the graphics panel returns to pause and leaves the game paused |
| R | Retry from the win or lose screen |
| M | Mute or unmute the music |

Walk into fireflies to refill the lantern. Moths still drain it when they get close; sprint away to shake them off. Some stepping stones only appear inside the lantern light. Light every beacon and the sky turns to dawn. Lighting a beacon spends fuel and raises the drain: Easy adds 5% of the base drain per lit beacon, Normal adds 8%, and Hard adds 11%, on top of that difficulty's drain multiplier. Each lit beacon also calls one more moth, up to 8, 12, or 16 moths. Moths spawn at the dark outer edge of the island, fade in, and stay about 1.4 m above the ground. They steer around trees, rocks, and cliffs. Near a moth the lantern dims, and one whisper grows louder the closer they get. They fade out when the round ends.

A lit beacon keeps a soft ring about 8 m across. Standing inside it shows "Safe light", slows the lantern to 35% drain, and keeps moths out. Overlapping rings do not stack.

Fireflies only settle again on gentle ground inside the island, away from water, steep slopes, cliffs, and nearby props, and they prefer a spot away from you.

Falling in the water plays a splash, fades the screen, and returns you to the last flat safe patch with the camera behind you. It costs 10 fuel, shown as "-10" on the meter. Your input pauses for a short moment.

When the lantern reaches zero, the light gutters and a vignette closes over about 1.5 seconds before the lose panel. Pause stays closed during that sequence. R does not restart until the panel is up, and it ignores the first half-second so a stray press does not reload the island. You cannot win once the fuel is gone.

Your best time is saved for each island.

## Graphics presets

Open **Graphics** from the main menu, or from the pause menu. The choice applies immediately, with no scene reload, and it is saved. A preset picked on the main menu is already active when Island 1 or Island 2 loads. While the graphics panel is open from pause, the game stays paused. Esc or P closes that panel and returns to the pause buttons. Esc or P on the pause panel still resumes.

Medium is the default and matches the look the islands were built with.

| Preset | What it changes |
| --- | --- |
| Low | Render scale 0.5, no anti-aliasing, HDR and post-processing off, shadows to 8 m, grass drawn at 8 m at quarter density, water ripples and foam off, keeper rim off, distant mountains hidden |
| Medium | Today's look: full resolution, the current anti-aliasing, grass at 20 m, four shadow cascades, one haze layer |
| High | MSAA 4x, grass to 30 m, longer shadows, a second water ripple layer, extra mountain ridges |
| Ultra | High, plus SMAA on the camera, grass to 45 m, the longest shadows, stronger water glint, a lantern shadow on the keeper, a farther horizon, and the high-detail hero tree meshes. `UltraHeroSwitch` shows those meshes only on Ultra; every other preset keeps the LOD meshes |

VSync is off until you turn it on. The VSync button saves `LanternKeeperVSync` and applies it immediately. The FPS counter is off by default. It is saved as `LanternKeeperFps`, sits in the corner, and updates about twice a second from the average frame time, including while the game is paused.

The preset is stored in PlayerPrefs as `LanternKeeperGraphics` (0 Low, 1 Medium, 2 High, 3 Ultra). Starting the game with `-lkquality=0` through `-lkquality=3` overrides the saved choice for that session until a preset is picked in the menu.

### Measured cost

Development player, 800×600 windowed, VSync off, about 20 seconds of the scripted keeper walk. Frame time is the wall-clock time with VSync off. FPS below is that frame time turned into frames per second.

| Level | Island 1 avg | Island 1 FPS | Island 2 avg | Island 2 FPS |
| --- | --- | --- | --- | --- |
| Low | 2.33 ms | 429 | 2.23 ms | 448 |
| Medium | 2.66 ms | 376 | 2.55 ms | 392 |
| High | 2.98 ms | 336 | 2.69 ms | 372 |
| Ultra | 3.97 ms | 252 | 3.01 ms | 332 |

Ultra grass stays at density 1, because the terrain's detail density is already at Unity's maximum of 1. The extra cost on Ultra includes the hero tree meshes.

To reproduce, build a Development player and run it with `-lkperf -lkquality=<0..3> -lkscene=Island1` (or `Island2`) `-lkvsync=0 -lkseconds=20`. Add `-lkshot=<png path>` for one still of the spawn view, or `-lkview=beacon` for a lit beacon. The probe does nothing unless `-lkperf` is present.

## Project layout

- `Assets/Game/Scenes/` — the playable scene
- `Assets/Game/Scripts/` — player, lantern, fireflies, beacons, and HUD
- `Assets/Game/Prefabs/` — firefly and beacon prefabs
- `Assets/Game/Materials/` — island and glow materials
- `Packages/com.coplaydev.unity-mcp/` — local copy of MCP for Unity, used to drive the editor from Cursor

## Credits

### Music

- Calm Track by pmiller (CC0, OpenGameArt). https://opengameart.org/content/calm-track
- Calm Piano 1 / Vaporware by The Cynic Project / cynicmusic.com (CC0). https://opengameart.org/content/calm-piano-1-vaporware

- Keeper character: "Hooded Adventurer" by Quaternius, via Poly Pizza (https://poly.pizza/m/y9KWOVG21R). Licence CC0 1.0. The imported file is `Assets/Game/Models/Keeper/Keeper.fbx`.
- Poly Haven models (CC0), used on the islands:
  - pine_tree_01, fir_tree_01, pine_sapling_medium, pine_sapling_small, fir_sapling, fir_sapling_medium, fern_02, grass_medium_01, dandelion_01, celandine_01, jacaranda_tree, island_tree_02, nettle_plant, anthurium_botany_01, weed_plant_02 — Rico Cilliers, Rob Tuytel
  - tree_small_02, shrub_01, shrub_03, boulder_01 — Rico Cilliers
  - rock_07, rock_09 — Jenelle van Heerden
  - periwinkle_plant — Amal Kumar
  - tree_stump_01, tree_stump_02, dead_tree_trunk, pine_roots, moss_01 — Rob Tuytel
  - dead_tree_trunk_02 — Jenelle van Heerden, Rico Cilliers
  - dry_branches_medium_01, shrub_02, shrub_04, grass_medium_02, grass_bermuda_01, shrub_sorrel_01 — Rico Cilliers
  - rock_moss_set_02 — Kless Gyzen
  - namaqualand_boulder_02, dry_quiver_leaf — Greg Zaal, Rico Cilliers
  - namaqualand_boulder_03, namaqualand_boulder_05 — Dario Barresi, Jenelle van Heerden
  - namaqualand_cliff_02, quiver_tree_02 — Dario Barresi, Rico Cilliers
  - namaqualand_boulder_04, didelta_spinosa, leipoldtia_schultzei, flower_heliophila — Jenelle van Heerden
  - namaqualand_boulder_06, namaqualand_boulders_01, namaqualand_rocks_01, namaqualand_stones_01, bark_debris_01 — Greg Zaal, Jenelle van Heerden
  - namaqualand_cliff_01 — Jenelle van Heerden, Rico Cilliers
  - quiver_tree_01, dead_quiver_trunk — Dario Barresi, James Ray Cock, Rico Cilliers
  - wild_rooibos_bush, cheiridopsis_succulent, crystalline_iceplant, flower_gazania — James Ray Cock, Jenelle van Heerden
  - flower_ursinia, flower_empodium, flower_stinkkruid — Jenelle van Heerden, Rico Cilliers
- Terrain textures, all CC0 from Poly Haven:
  - Coast Sand 01 by Rob Tuytel (shore)
  - Forrest Ground 01 by Rob Tuytel (forest ground)
  - Stony Dirt Path by eye-candy.xyz (trail)
  - Rock Face 03 by Dario Barresi and Rico Cilliers (cliff)
  - Forest Leaves 02 by Rob Tuytel (moss)

### Foliage

Terrain grass is an authored tuft: `Assets/Game/Meshes/GrassTuft.asset` with `Assets/Game/Textures/Grass/GrassBlade.png`. Both were made for this project.

### Water

The lake ripple normals and shore foam mask in `Assets/Game/Textures/Water/` (`WaterRippleNormalA`, `WaterRippleNormalB`, `WaterFoam`) are generated in the project by `WaterTextureGenerator` (menu: Lantern Keeper/Generate Water Textures).

### Skies

Poly Haven CC0 HDRIs, by Greg Zaal and Jarod Guest:

- Qwantani Moonrise Pure Sky (night) — https://polyhaven.com/a/qwantani_moonrise_puresky
- Qwantani Dawn Pure Sky (dawn) — https://polyhaven.com/a/qwantani_dawn_puresky
