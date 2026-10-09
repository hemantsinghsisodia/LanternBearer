using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LanternKeeper
{
// Marks a Settings row with the id its hint is looked up by (SettingsHints). The row also reports pointer hover, so the hint
// line can follow the mouse as well as keyboard and gamepad focus.
public class SettingsHintRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string hintId = "";

    // (row, hovering). Subscribers filter by hierarchy.
    public static event Action<SettingsHintRow, bool> HoverChanged;

    public string HintId { get { return hintId; } }

    public void Configure(string id)
    {
        hintId = id;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Raise(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Raise(false);
    }

    private void OnDisable()
    {
        Raise(false);
    }

    private void Raise(bool hovering)
    {
        Action<SettingsHintRow, bool> handler = HoverChanged;
        if (handler != null)
        {
            handler(this, hovering);
        }
    }
}
}
