using UnityEngine;

namespace LanternKeeper
{
// Settings > Controls: a read-only list of the bindings the game code reads
// (PlayerController, CameraFollow, Beacon, GameManager, MusicPlayer, HUD). The rows are plain labels.
public class ControlsSection : MonoBehaviour
{
    // Keep in step with the input code. Gamepad only drives the camera and dismisses cards; there is no stick movement.
    public static readonly string[] Bindings =
    {
        "Move — W A S D",
        "Sprint — Left Shift or Right Shift (hold)",
        "Jump — Space",
        "Light a beacon — E",
        "Look — Mouse, Left / Right arrow keys, Right stick",
        "Pause or back — Esc, P",
        "Restart the island — R",
        "Mute music — M",
        "Dismiss a card — Any key, click, A or Start"
    };

    [SerializeField] private ThemedLabel[] rows = new ThemedLabel[0];

    public ThemedLabel[] Rows { get { return rows; } }

    public void Configure(ThemedLabel[] labels)
    {
        rows = labels;
    }
}
}
