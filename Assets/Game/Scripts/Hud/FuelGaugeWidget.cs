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

    public float MaxFuel { get { return maxFuel; } }
    public float FlameScale { get { return flame.rectTransform.localScale.x; } }
    public Color FlameColour { get { return flame.color; } }
    public float GlowAlpha { get { return glow.color.a; } }
    public float BarFill { get { return barFill.rectTransform.anchorMax.x; } }
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
        HudMath.FlameLook look = HudMath.Flame(fuel01, Time.unscaledTime, reduceFlashing);
        float scale = look.Scale * (1f + PulseGrowth * look.Pulse);
        flame.rectTransform.localScale = new Vector3(scale, scale, 1f);
        Color colour = Color.Lerp(theme.amber, FlameRed, look.Redness);
        flame.color = colour;
        Color glowColour = colour;
        glowColour.a = Mathf.Clamp01(look.Glow * 0.5f * (1f + 0.4f * look.Pulse));
        glow.color = glowColour;
        barFill.color = colour;
        RectTransform fillRect = barFill.rectTransform;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(fuel01), 1f);
        fillRect.offsetMax = Vector2.zero;
    }

    // Coalesced with changes made within a second of each other; shown for about a second.
    public void Changed(float delta)
    {
        flash.Add(delta, Time.unscaledTime);
        changeLabel.Text.text = flash.Text;
        changeLabel.SetColourOverride(true, delta < 0f ? FlameRed : theme.amber);
        changeLabel.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (changeLabel != null && changeLabel.gameObject.activeSelf && !flash.Visible(Time.unscaledTime))
        {
            changeLabel.gameObject.SetActive(false);
        }
    }
}
}
