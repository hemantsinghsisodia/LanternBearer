using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// A Button in the forged-iron style. Primary: amber gradient, dark ink text. Plain: ink with brass.
// Focused, selected or hovered shows the focus outline child. onClick is the inherited Button.onClick.
public class ThemedButton : Button
{
    [SerializeField] private UITheme theme;
    [SerializeField] private bool primary;
    [SerializeField] private ThemedLabel label;
    [SerializeField] private GameObject focusFrame;
    [SerializeField] private UIVerticalGradient gradient;
    [SerializeField] private bool marked;

    public bool Primary { get { return primary; } }
    public ThemedLabel Label { get { return label; } }
    public UIVerticalGradient Gradient { get { return gradient; } }
    public GameObject FocusFrame { get { return focusFrame; } }
    public bool Marked { get { return marked; } }

    public void Configure(UITheme newTheme, bool isPrimary, ThemedLabel themedLabel, GameObject focus, UIVerticalGradient grad)
    {
        theme = newTheme;
        primary = isPrimary;
        label = themedLabel;
        focusFrame = focus;
        gradient = grad;
        ApplyColours();
        ShowFocus(false);
    }

    // Marks the button as the current choice (used by SectionList): its text turns amber.
    public void SetMarked(bool on)
    {
        marked = on;
        if (label != null && theme != null)
        {
            label.SetColourOverride(on, theme.amber);
        }
    }

    public void ApplyColours()
    {
        if (theme == null)
        {
            return;
        }
        ColorBlock block = colors;
        block.colorMultiplier = 1f;
        block.fadeDuration = 0.08f;
        if (primary)
        {
            block.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            block.highlightedColor = Color.white;
            block.selectedColor = Color.white;
            block.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            block.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            if (gradient != null)
            {
                gradient.SetColours(theme.amber, theme.amberDeep);
            }
        }
        else
        {
            Color ink = theme.inkPanel;
            ink.a = 1f;
            Color lit = Color.Lerp(ink, theme.brassLine, 0.35f);
            block.normalColor = ink;
            block.highlightedColor = Color.Lerp(ink, theme.brassLine, 0.18f);
            block.selectedColor = lit;
            block.pressedColor = Color.Lerp(ink, Color.black, 0.3f);
            block.disabledColor = new Color(ink.r, ink.g, ink.b, 0.5f);
        }
        colors = block;
        transition = Transition.ColorTint;
        Image img = targetGraphic as Image;
        if (img != null)
        {
            img.color = Color.white;
        }
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        ShowFocus(state == SelectionState.Selected || state == SelectionState.Pressed);
    }

    private void ShowFocus(bool on)
    {
        if (focusFrame != null && focusFrame.activeSelf != on)
        {
            focusFrame.SetActive(on);
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        if (theme == null)
        {
            theme = UnityEditor.AssetDatabase.LoadAssetAtPath<UITheme>("Assets/Game/Art/UI/UITheme.asset");
        }
    }
#endif
}
}
