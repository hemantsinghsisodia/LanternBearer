using TMPro;
using UnityEngine;

namespace LanternKeeper
{
// Wraps a TMP_Text. Role picks font and colour from the theme; basePx is multiplied by TextScaler.Current.
public class ThemedLabel : MonoBehaviour
{
    public enum Role { Title, Flavour, Body, Label, Number }
    public enum Tint { Auto, OnParchment, OnAmber }

    [SerializeField] private UITheme theme;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Role role = Role.Body;
    [SerializeField] private Tint tint = Tint.Auto;
    [SerializeField] private float basePx = 22f;
    [SerializeField] private bool hasColourOverride;
    [SerializeField] private Color colourOverride = Color.white;

    public TMP_Text Text { get { return text; } }
    public Role CurrentRole { get { return role; } }
    public float BasePx { get { return basePx; } }

    public static TMP_FontAsset FontFor(UITheme t, Role r)
    {
        switch (r)
        {
            case Role.Title: return t.titleFont;
            case Role.Flavour: return t.flavourFont;
            case Role.Label: return t.uiFontStrong;
            case Role.Number: return t.uiFontStrong;
            default: return t.uiFont;
        }
    }

    public static Color ColourFor(UITheme t, Role r, Tint tint)
    {
        if (tint == Tint.OnParchment)
        {
            return t.inkText;
        }
        if (tint == Tint.OnAmber)
        {
            Color ink = t.inkPanel;
            ink.a = 1f;
            return ink;
        }
        return r == Role.Flavour ? t.textMuted : t.textPrimary;
    }

    public void Configure(UITheme newTheme, TMP_Text tmp, Role newRole, float px, Tint newTint = Tint.Auto)
    {
        theme = newTheme;
        text = tmp;
        role = newRole;
        basePx = px;
        tint = newTint;
        Apply(1f);
    }

    public void SetColourOverride(bool on, Color colour)
    {
        hasColourOverride = on;
        colourOverride = colour;
        Apply();
    }

    public void SetBasePx(float px)
    {
        basePx = px;
        Apply();
    }

    public void Apply()
    {
        Apply(TextScaler.Current);
    }

    public void Apply(float scale)
    {
        if (theme == null || text == null)
        {
            return;
        }
        text.font = FontFor(theme, role);
        text.color = hasColourOverride ? colourOverride : ColourFor(theme, role, tint);
        text.fontSize = basePx * scale;
    }

    private void OnEnable()
    {
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (theme == null)
        {
            theme = UnityEditor.AssetDatabase.LoadAssetAtPath<UITheme>("Assets/Game/Art/UI/UITheme.asset");
        }
    }
#endif
}
}
