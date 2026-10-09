using UnityEngine;
using UnityEngine.EventSystems;
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
    private bool focused;
    [SerializeField] private bool tab;
    [SerializeField] private GameObject markEdge;
    [SerializeField] private GameObject markTint;
    [SerializeField] private bool isBack;

    public bool Primary { get { return primary; } }
    public ThemedLabel Label { get { return label; } }
    public UIVerticalGradient Gradient { get { return gradient; } }
    public GameObject FocusFrame { get { return focusFrame; } }
    public bool Marked { get { return marked; } }
    public bool Tab { get { return tab; } }
    public GameObject MarkEdge { get { return markEdge; } }
    public GameObject MarkTint { get { return markTint; } }
    public bool IsBack { get { return isBack; } }

    public void SetBack(bool back)
    {
        isBack = back;
    }

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

    // Turns the button into a left-edge tab: no box, plain text. Marked adds an amber text colour, an amber left edge
    // and a faint amber tint (both start hidden). Focus still shows the kit's outline.
    public void ConfigureTab(GameObject edge, GameObject tint)
    {
        tab = true;
        markEdge = edge;
        markTint = tint;
        ApplyColours();
        ShowMark(marked);
    }

    private void ShowMark(bool on)
    {
        if (markEdge != null)
        {
            markEdge.SetActive(on);
        }
        if (markTint != null)
        {
            markTint.SetActive(on);
        }
    }

    // Marks the button as the current choice (used by SectionList): its text turns amber.
    public void SetMarked(bool on)
    {
        marked = on;
        ShowMark(on);
        RefreshLabelColour();
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
        else if (tab)
        {
            Color clear = new Color(1f, 1f, 1f, 0f);
            block.normalColor = clear;
            block.highlightedColor = new Color(1f, 1f, 1f, 0.05f);
            block.selectedColor = clear;
            block.pressedColor = new Color(1f, 1f, 1f, 0.1f);
            block.disabledColor = clear;
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

    public override void OnPointerClick(PointerEventData eventData)
    {
        // Decide before the click runs: onClick may hide this very button, which would make the check fail afterwards.
        bool pressable = IsActive() && IsInteractable();
        base.OnPointerClick(eventData);
        if (pressable && eventData.button == PointerEventData.InputButton.Left)
        {
            PlayPress();
        }
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        bool pressable = IsActive() && IsInteractable();
        base.OnSubmit(eventData);
        if (pressable)
        {
            PlayPress();
        }
    }

    private void PlayPress()
    {
        UiSound.Play(isBack ? SoundCues.UiBack : SoundCues.UiClick);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        ShowFocus(state == SelectionState.Selected || state == SelectionState.Pressed);
    }

    // Amber text marks the current choice; a focused tab keeps plain text, since the focus glow is amber too.
    private void RefreshLabelColour()
    {
        if (label == null || theme == null)
        {
            return;
        }
        bool amberText = marked && !(tab && focused);
        label.SetColourOverride(amberText, theme.amber);
    }

    private void ShowFocus(bool on)
    {
        focused = on;
        RefreshLabelColour();
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
