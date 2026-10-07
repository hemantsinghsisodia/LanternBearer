using System;
using UnityEngine;

namespace LanternKeeper
{
// The run timer, top-right. Formats with HudMath.FormatTime unless the coordinator supplies another formatter.
public class TimerLabel : MonoBehaviour
{
    [SerializeField] private ThemedLabel label;

    private Func<float, string> format = HudMath.FormatTime;

    public string Text { get { return label.Text.text; } }

    public void Configure(ThemedLabel timerLabel)
    {
        label = timerLabel;
    }

    public void SetFormatter(Func<float, string> formatter)
    {
        format = formatter ?? HudMath.FormatTime;
    }

    public void Show(float seconds)
    {
        string text = format(seconds);
        if (label.Text.text != text)
        {
            label.Text.text = text;
        }
    }
}
}
