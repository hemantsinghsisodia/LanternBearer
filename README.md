# Lantern Keeper

A small 3D night game made in Unity 6 with the Universal Render Pipeline. Walk a foggy island, keep your lantern lit, and light five beacons before the flame goes out.

## Requirements

- Unity **6000.6.3f1**
- The project uses URP and the Input System package

## Open and play

1. Open this folder in Unity Hub.
2. Open `Assets/Game/Scenes/LanternKeeper.unity`.
3. Press Play.

## Controls

| Key | Action |
| --- | --- |
| WASD or arrow keys | Move |
| E | Light a beacon when you are standing next to it |
| R | Restart the round |

Walk into fireflies to refill the lantern. Lighting a beacon spends fuel. Light all five beacons to win. If the lantern goes out, you lose. Your best time is saved on this computer.

## Project layout

- `Assets/Game/Scenes/` — the playable scene
- `Assets/Game/Scripts/` — player, lantern, fireflies, beacons, and HUD
- `Assets/Game/Prefabs/` — firefly and beacon prefabs
- `Assets/Game/Materials/` — island and glow materials
- `Packages/com.coplaydev.unity-mcp/` — local copy of MCP for Unity, used to drive the editor from Cursor
