using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// Island 5's storm surge: "Storm rising: N m below" in the same chip style as StormChip (the FinaleInstaller-era HUD chip, cloned by
// StormSurgeInstaller into the Island 5 HUD one row under the wind chip). Hidden until the surge is within 25 m of the keeper.
public class SurgeChip : MonoBehaviour
{
    static readonly Color Warning = new Color(1f, 0.42f, 0.32f, 1f);

    [SerializeField] ThemedLabel label;
    [SerializeField] Image backing;

    Color baseColour;
    bool haveBase;

    public bool Active { get { return gameObject.activeSelf; } }
    public string Text { get { return label != null ? label.Text.text : string.Empty; } }
    public Color TextColour { get { return label != null ? label.Text.color : Color.clear; } }

    public void Configure(ThemedLabel word, Image back)
    {
        label = word;
        backing = back;
    }

    // `urgency01` 0 (far) to 1 (on the keeper): the text warms towards red as the surge closes in.
    public void Show(bool active, string text, float urgency01)
    {
        if (gameObject.activeSelf != active)
        {
            gameObject.SetActive(active);
        }

        if (!active || label == null)
        {
            return;
        }

        if (!haveBase)
        {
            haveBase = true;
            baseColour = label.Text.color;
        }

        if (label.Text.text != text)
        {
            label.Text.text = text;
        }

        label.Text.color = Color.Lerp(baseColour, Warning, Mathf.Clamp01(urgency01));
    }
}
}
