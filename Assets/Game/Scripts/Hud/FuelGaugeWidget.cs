using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// The lantern icon, its flame, the fuel bar and the "-10" / "+5" change flash. Named FuelGaugeWidget because
// LanternKeeper.FuelGauge is already the pure smoothing helper in the Logic assembly.
public class FuelGaugeWidget : MonoBehaviour
{
    public static readonly Color FlameRed = new Color(0.91f, 0.26f, 0.12f, 1f);
    private const float PulseGrowth = 0.12f;

    [SerializeField] private UITheme theme;
    [SerializeField] private Image lantern;
    [SerializeField] private Image flame;
    [SerializeField] private Image glow;
    [SerializeField] private Image barFill;
    [SerializeField] private ThemedLabel changeLabel;

    private readonly HudMath.FuelFlash flash = new HudMath.FuelFlash();
    private float maxFuel = 1f;
    private Color baseBar;
    private bool haveBase;
    private bool softFlash;

    public float MaxFuel { get { return maxFuel; } }
    public float FlameScale { get { return flame.rectTransform.localScale.x; } }
    public Color FlameColour { get { return flame.color; } }
    public float GlowAlpha { get { return glow.color.a; } }
    public float BarFill { get { return barFill.rectTransform.anchorMax.x; } }
    public Color BarColour { get { return barFill.color; } }
    public Color ChangeColour { get { return changeLabel.Text.color; } }
    public bool ChangeVisible { get { return changeLabel.gameObject.activeSelf; } }
    public string ChangeText { get { return changeLabel.Text.text; } }

    public void Configure(UITheme newTheme, Image lanternImage, Image flameImage, Image glowImage, Image fill, ThemedLabel change)
    {
        theme = newTheme;
        lantern = lanternImage;
        flame = flameImage;
        glow = glowImage;
        barFill = fill;
        changeLabel = change;
    }

    public void Show(float fuel01, float newMaxFuel, bool reduceFlashing)
    {
        maxFuel = newMaxFuel;
        softFlash = reduceFlashing;
        HudMath.FlameLook look = HudMath.Flame(fuel01, Time.unscaledTime, reduceFlashing);
        float scale = look.Scale * (1f + PulseGrowth * look.Pulse);
        flame.rectTransform.localScale = new Vector3(scale, scale, 1f);
        Color colour = Color.Lerp(theme.amber, FlameRed, look.Redness);
        flame.color = colour;
        Color glowColour = colour;
        glowColour.a = Mathf.Clamp01(look.Glow * 0.5f * (1f + 0.4f * look.Pulse));
        glow.color = glowColour;
        baseBar = colour;
        haveBase = true;
        ApplyBar(Time.unscaledTime);
        RectTransform fillRect = barFill.rectTransform;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(fuel01), 1f);
        fillRect.offsetMax = Vector2.zero;
    }

    // Coalesced with changes made within a second of each other; shown for about a second.
    public void Changed(float delta)
    {
        Changed(delta, Time.unscaledTime);
    }

    public void Changed(float delta, float now)
    {
        if (!haveBase)
        {
            baseBar = barFill.color;
            haveBase = true;
        }
        flash.Add(delta, now);
        ApplyBar(now);
        if (flash.RoundsToZero)
        {
            changeLabel.gameObject.SetActive(false);
            return;
        }
        changeLabel.Text.text = flash.Text;
        changeLabel.SetColourOverride(true, flash.IsNegative ? FlameRed : theme.amber);
        changeLabel.gameObject.SetActive(true);
    }

    // Tints the bar fill toward the change colour right after a change, fading back to the base over the flash window.
    public void ApplyBar(float now)
    {
        if (!haveBase)
        {
            return;
        }
        Color result = baseBar;
        if (flash.Visible(now) && !flash.RoundsToZero)
        {
            float fade = flash.Fade(now);
            Color target = flash.IsNegative ? FlameRed : theme.amber;
            result = Color.Lerp(baseBar, target, (softFlash ? 0.3f : 0.7f) * fade);
            if (!softFlash)
            {
                result = Color.Lerp(result, Color.white, 0.35f * fade);
            }
        }
        barFill.color = result;
    }

    private void Update()
    {
        ApplyBar(Time.unscaledTime);
        if (changeLabel != null && changeLabel.gameObject.activeSelf && !flash.Visible(Time.unscaledTime))
        {
            changeLabel.gameObject.SetActive(false);
        }
    }
}
}
