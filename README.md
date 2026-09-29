# Lantern Keeper

A small 3D night game made in Unity 6 with the Universal Render Pipeline. Walk a foggy island, keep your lantern lit, and light five beacons before the flame goes out.

## Requirements

- Unity **6000.6.3f1**
- The project uses URP and the Input System package

## Open and play

1. Open this folder in Unity Hub.
2. Open `Assets/Game/Scenes/MainMenu.unity`.
3. Press Play, then choose a difficulty and an island.

Island 2 unlocks after you win Island 1. The first island has 5 beacons. The second is larger and has 7.

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
| Esc or P | Pause. Resume, restart, or return to the main menu. The cursor unlocks while paused |
| R | Retry from the win or lose screen |
| M | Mute or unmute the music |

Walk into fireflies to refill the lantern. Lighting a beacon spends fuel. Moths gather on the light and drain it faster; sprint away to shake them off. Some stepping stones only appear inside the lantern light. Light every beacon and the sky turns to dawn. If the lantern goes out, you lose. Your best time is saved for each island.

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
  - pine_tree_01, fir_tree_01, pine_sapling_medium, pine_sapling_small, fir_sapling, fern_02, grass_medium_01, dandelion_01, celandine_01 — Rico Cilliers, Rob Tuytel
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

Nature Pack (free version) — personal/learning use only; author to be added.

### Water

ChuckCG, "Water Shader Addon Free" (GPL-2.0-or-later). The lake ripple normals and shore foam mask in `Assets/Game/Textures/Water/` were baked from that Blender material. Unity does not run the original node graph.

### Skies

Poly Haven CC0 HDRIs, by Greg Zaal and Jarod Guest:

- Qwantani Moonrise Pure Sky (night) — https://polyhaven.com/a/qwantani_moonrise_puresky
- Qwantani Dawn Pure Sky (dawn) — https://polyhaven.com/a/qwantani_dawn_puresky
