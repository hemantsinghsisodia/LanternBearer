using System;
using UnityEngine;

namespace LanternKeeper
{
// The run timer, top-right. The coordinator passes GameManager.FormatTime as the formatter (this assembly cannot see GameManager);
// until then it uses the same mm:ss rule.
public class TimerLabel : MonoBehaviour
{
    [SerializeField] private ThemedLabel label;

    private Func<float, string> format = DefaultFormat;

    public string Text { get { return label.Text.text; } }

    public void Configure(ThemedLabel timerLabel)
    {
        label = timerLabel;
    }

    public void SetFormatter(Func<float, string> formatter)
    {
        format = formatter ?? DefaultFormat;
    }

    public static string DefaultFormat(float seconds)
    {
        if (seconds < 0f)
        {
            return "--:--";
        }
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
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
