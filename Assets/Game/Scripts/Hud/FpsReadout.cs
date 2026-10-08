using System;
using System.Text;
using UnityEngine;

namespace LanternKeeper
{
// The FPS counter, top-right under the timer. Sits on an always-active holder and shows or hides its readout child
// from the LanternKeeperFps PlayerPrefs switch (SettingsMath.FpsPrefsKey, which the graphics panel sets). Counts frames over half a second.
public class FpsReadout : MonoBehaviour
{
    private const float Window = 0.5f;
    private const float PollInterval = 0.25f;

    [SerializeField] private GameObject readout;
    [SerializeField] private ThemedLabel label;

    private readonly StringBuilder builder = new StringBuilder(12);
    private float accumulated;
    private int frames;
    private int shownFps = int.MinValue;
    private float nextPoll;
    private Func<bool> enabledSource = () => PlayerPrefs.GetInt(SettingsMath.FpsPrefsKey, 0) == 1;

    public bool Shown { get { return readout.activeSelf; } }
    public string Text { get { return label.Text.text; } }

    public void Configure(GameObject readoutRoot, ThemedLabel fpsLabel)
    {
        readout = readoutRoot;
        label = fpsLabel;
    }

    public void SetEnabledSource(Func<bool> source)
    {
        enabledSource = source;
    }

    public void Refresh()
    {
        bool show = enabledSource();
        if (readout.activeSelf == show)
        {
            return;
        }
        readout.SetActive(show);
        if (show)
        {
            accumulated = 0f;
            frames = 0;
            shownFps = int.MinValue;
        }
    }

    // Public so tests can drive it without a player loop.
    public void Tick(float unscaledDelta)
    {
        if (!readout.activeSelf || unscaledDelta <= 0f)
        {
            return;
        }
        accumulated += unscaledDelta;
        frames++;
        if (accumulated < Window)
        {
            return;
        }
        float average = accumulated / frames;
        int fps = average > 0.0001f ? Mathf.RoundToInt(1f / average) : 0;
        accumulated = 0f;
        frames = 0;
        if (fps == shownFps)
        {
            return;
        }
        shownFps = fps;
        builder.Length = 0;
        builder.Append(fps);
        builder.Append(" FPS");
        label.Text.text = builder.ToString();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextPoll)
        {
            nextPoll = Time.unscaledTime + PollInterval;
            Refresh();
        }
        Tick(Time.unscaledDeltaTime);
    }
}
}
