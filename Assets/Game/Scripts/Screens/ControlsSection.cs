using UnityEngine;

namespace LanternKeeper
{
// Settings > Controls: a read-only list of the bindings the game code reads
// (PlayerController, CameraFollow, Beacon, GameManager, MusicPlayer, HUD). The rows are plain labels.
// In touch mode the rows show the on-screen controls instead (TouchWording.TouchBindings); the extra rows are hidden. It follows the mode live.
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

    private void OnEnable()
    {
        TouchMode.OnChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        TouchMode.OnChanged -= Apply;
    }

    // Writes the keyboard or touch list into the rows and shows or hides them.
    public void Apply()
    {
        bool touch = TouchMode.Active;
        string[] lines = TouchWording.Controls(touch, Bindings);
        for (int i = 0; i < rows.Length; i++)
        {
            ThemedLabel row = rows[i];
            if (row == null)
            {
                continue;
            }

            bool visible = TouchWording.ControlRowVisible(touch, i);
            if (row.gameObject.activeSelf != visible)
            {
                row.gameObject.SetActive(visible);
            }

            if (visible && i < lines.Length && row.Text != null && row.Text.text != lines[i])
            {
                row.Text.text = lines[i];
            }
        }
    }
}
}
