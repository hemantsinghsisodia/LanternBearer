using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// Settings > Display: brightness (with a test card), resolution and window mode. Changes apply at once through UserSettings.
public class DisplaySection : MonoBehaviour
{
    const int CardWidth = 128;
    const int CardHeight = 64;

    static readonly FullScreenMode[] Modes =
    {
        FullScreenMode.ExclusiveFullScreen,
        FullScreenMode.FullScreenWindow,
        FullScreenMode.Windowed
    };

    [SerializeField] private SliderRow brightnessRow;
    [SerializeField] private RawImage testCard;
    [SerializeField] private DropdownRow resolutionRow;
    [SerializeField] private SwitchRow windowRow;

    private readonly List<Resolution> choices = new List<Resolution>();
    private Texture2D cardTexture;

    public SliderRow BrightnessRow { get { return brightnessRow; } }
    public RawImage TestCard { get { return testCard; } }
    public DropdownRow ResolutionRow { get { return resolutionRow; } }
    public SwitchRow WindowRow { get { return windowRow; } }

    public void Configure(SliderRow brightness, RawImage card, DropdownRow resolution, SwitchRow window)
    {
        brightnessRow = brightness;
        testCard = card;
        resolutionRow = resolution;
        windowRow = window;
    }

    private void OnEnable()
    {
        brightnessRow.SetValueFormatter(FormatBrightness);
        brightnessRow.ValueChanged += OnBrightness;
        resolutionRow.IndexChanged += OnResolution;
        windowRow.IndexChanged += OnWindow;
        Refresh();
    }

    private void OnDisable()
    {
        brightnessRow.ValueChanged -= OnBrightness;
        resolutionRow.IndexChanged -= OnResolution;
        windowRow.IndexChanged -= OnWindow;
    }

    private void OnDestroy()
    {
        if (cardTexture != null)
        {
            Destroy(cardTexture);
        }
    }

    public void Refresh()
    {
        float ev = SettingsMath.BrightnessToEv(UserSettings.Brightness);
        brightnessRow.SetValueWithoutNotify((ev + 1f) * 0.5f);
        PaintCard(ev);
        FillResolutions();
        windowRow.SetIndexWithoutNotify(ModeIndex(UserSettings.WindowMode));
    }

    static string FormatBrightness(float slider)
    {
        float ev = slider * 2f - 1f;
        return ev.ToString("+0.00;-0.00;0.00", System.Globalization.CultureInfo.InvariantCulture) + " EV";
    }

    static int ModeIndex(FullScreenMode mode)
    {
        for (int i = 0; i < Modes.Length; i++)
        {
            if (Modes[i] == mode)
            {
                return i;
            }
        }
        // Maximized windows and anything else count as windowed.
        return 2;
    }

    private void OnBrightness(float slider)
    {
        float ev = slider * 2f - 1f;
        UserSettings.Brightness = ev;
        PaintCard(ev);
    }

    private void OnWindow(int index)
    {
        UserSettings.WindowMode = Modes[Mathf.Clamp(index, 0, Modes.Length - 1)];
    }

    private void OnResolution(int index)
    {
        if (index < 0 || index >= choices.Count)
        {
            return;
        }
        Resolution picked = choices[index];
        UserSettings.SetResolution(picked.width, picked.height, Mathf.RoundToInt((float)picked.refreshRateRatio.value));
    }

    private void FillResolutions()
    {
        choices.Clear();
        Resolution[] supported = Screen.resolutions;
        Resolution desktop = Screen.currentResolution;
        HashSet<string> seen = new HashSet<string>();
        List<string> labels = new List<string>();
        if (supported != null)
        {
            for (int i = 0; i < supported.Length; i++)
            {
                string label = Label(supported[i]);
                if (seen.Add(label))
                {
                    choices.Add(supported[i]);
                    labels.Add(label);
                }
            }
        }
        if (choices.Count == 0)
        {
            choices.Add(desktop);
            labels.Add(Label(desktop));
        }
        Resolution current = UserSettings.ResolveResolution(supported, desktop);
        int selected = labels.IndexOf(Label(current));
        resolutionRow.SetOptions(labels, selected < 0 ? labels.Count - 1 : selected);
    }

    static string Label(Resolution r)
    {
        return r.width + " x " + r.height + " @ " + Mathf.RoundToInt((float)r.refreshRateRatio.value) + " Hz";
    }

    // A dark sky, a black cliff edge and a sea that is only just lighter than the cliff at 0 EV.
    // Brightness scales the picture by 2^EV so the card answers the slider even where the world has no post-processing.
    private void PaintCard(float ev)
    {
        if (testCard == null)
        {
            return;
        }
        if (cardTexture == null)
        {
            cardTexture = new Texture2D(CardWidth, CardHeight, TextureFormat.RGBA32, false);
            cardTexture.name = "BrightnessTestCard";
            cardTexture.filterMode = FilterMode.Bilinear;
            cardTexture.wrapMode = TextureWrapMode.Clamp;
            testCard.texture = cardTexture;
        }
        float gain = Mathf.Pow(2f, ev);
        Color[] pixels = new Color[CardWidth * CardHeight];
        for (int y = 0; y < CardHeight; y++)
        {
            float v = y / (float)(CardHeight - 1);
            for (int x = 0; x < CardWidth; x++)
            {
                float u = x / (float)(CardWidth - 1);
                float cliffEdge = 0.55f - 0.35f * v + 0.04f * Mathf.Sin(v * 17f);
                float horizon = 0.42f;
                float level;
                if (u < cliffEdge)
                {
                    level = 0.025f;
                }
                else if (v < horizon)
                {
                    level = 0.075f + 0.02f * Mathf.Sin(u * 90f + v * 40f);
                }
                else
                {
                    level = 0.11f + 0.05f * (v - horizon);
                }
                float c = Mathf.Clamp01(level * gain);
                pixels[y * CardWidth + x] = new Color(c * 0.86f, c * 0.95f, c, 1f);
            }
        }
        cardTexture.SetPixels(pixels);
        cardTexture.Apply(false);
    }
}
}
