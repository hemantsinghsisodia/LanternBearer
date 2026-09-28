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
| WASD or arrow keys | Move |
| Shift | Sprint. This burns lantern fuel faster |
| E | Light a beacon when you are standing next to it |
| R | Restart the round |

Walk into fireflies to refill the lantern. Lighting a beacon spends fuel. Moths gather on the light and drain it faster; sprint away to shake them off. Some stepping stones only appear inside the lantern light. Light every beacon and the sky turns to dawn. If the lantern goes out, you lose. Your best time is saved for each island.

## Project layout

- `Assets/Game/Scenes/` — the playable scene
- `Assets/Game/Scripts/` — player, lantern, fireflies, beacons, and HUD
- `Assets/Game/Prefabs/` — firefly and beacon prefabs
- `Assets/Game/Materials/` — island and glow materials
- `Packages/com.coplaydev.unity-mcp/` — local copy of MCP for Unity, used to drive the editor from Cursor
