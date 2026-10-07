using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LanternKeeper
{
// What is left of the old graphics panel: the FPS counter switch (Settings > Graphics writes it, the HUD's FpsReadout reads it)
// and the Tab-key focus helper the pause screen uses.
public static class GraphicsMenu
{
    public const string FpsKey = "LanternKeeperFps";

    public static bool FpsEnabled
    {
        get { return PlayerPrefs.GetInt(FpsKey, 0) == 1; }
        set
        {
            PlayerPrefs.SetInt(FpsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void HandleTab(Button[] order)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || order == null || !keyboard.tabKey.wasPressedThisFrame)
        {
            return;
        }

        EventSystem system = EventSystem.current;
        if (system == null || system.currentSelectedGameObject == null)
        {
            return;
        }

        int current = -1;
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] != null && order[i].gameObject == system.currentSelectedGameObject)
            {
                current = i;
                break;
            }
        }

        if (current < 0)
        {
            return;
        }

        int step = keyboard.shiftKey.isPressed ? -1 : 1;
        for (int n = 1; n <= order.Length; n++)
        {
            int index = current + step * n;
            index %= order.Length;
            if (index < 0)
            {
                index += order.Length;
            }

            Button next = order[index];
            if (next != null && next.gameObject.activeInHierarchy && next.interactable)
            {
                system.SetSelectedGameObject(next.gameObject);
                return;
            }
        }
    }
}
}
