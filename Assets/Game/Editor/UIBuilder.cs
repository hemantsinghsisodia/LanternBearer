using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// Builds the themed uGUI kit (and, in later tasks, the menu screens). Every factory returns the new GameObject,
// parented under `parent`, with `theme` and the theme fonts already assigned.
public static partial class UIBuilder
{
    public const string ThemePath = "Assets/Game/Art/UI/UITheme.asset";
    const float RowHeight = 56f;
    const float RowWidth = 760f;

    [MenuItem("Lantern Keeper/Build UI")]
    public static void BuildAll()
    {
        BuildSettingsScreen();
        BuildMainMenuScreen();
    }

    public static UITheme LoadTheme()
    {
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme == null)
        {
            throw new System.InvalidOperationException("Missing UITheme asset at " + ThemePath);
        }
        return theme;
    }

    // ---------- shared helpers ----------

    static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static RectTransform Stretch(GameObject go)
    {
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform Anchor(GameObject go, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    static Image NewImage(string name, Transform parent, Color colour, bool raycast)
    {
        GameObject go = NewUI(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = raycast;
        return image;
    }

    // Four thin edge images forming a rectangular outline. inset is positive inward (negative draws outside).
    static Graphic[] MakeFrameLines(Transform parent, string name, float inset, float thickness, Color colour)
    {
        GameObject frame = NewUI(name, parent);
        Stretch(frame);
        float i = inset;
        float t = thickness;
        Image top = NewImage("Top", frame.transform, colour, false);
        Anchor(top.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(i, -i - t), new Vector2(-i, -i));
        Image bottom = NewImage("Bottom", frame.transform, colour, false);
        Anchor(bottom.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(i, i), new Vector2(-i, i + t));
        Image left = NewImage("Left", frame.transform, colour, false);
        Anchor(left.gameObject, new Vector2(0, 0), new Vector2(0, 1), new Vector2(i, i), new Vector2(i + t, -i));
        Image right = NewImage("Right", frame.transform, colour, false);
        Anchor(right.gameObject, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-i - t, i), new Vector2(-i, -i));
        return new Graphic[] { top, bottom, left, right };
    }

    // Focus outline (bright brass) with a soft amber glow behind it. Starts inactive.
    static GameObject MakeFocusFrame(Transform parent, UITheme theme)
    {
        GameObject focus = NewUI("Focus", parent);
        Stretch(focus);
        Image glow = NewImage("Glow", focus.transform, theme.focusGlow, false);
        Anchor(glow.gameObject, Vector2.zero, Vector2.one, new Vector2(-6, -6), new Vector2(6, 6));
        MakeFrameLines(focus.transform, "Outline", -3f, 3f, theme.focusOutline);
        focus.SetActive(false);
        return focus;
    }

    static ThemedLabel AddLabel(Transform parent, string name, string text, ThemedLabel.Role role, float px, UITheme theme,
        ThemedLabel.Tint tint = ThemedLabel.Tint.Auto)
    {
        GameObject go = NewUI(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = ThemedLabel.FontFor(theme, role);
        tmp.text = text;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        ThemedLabel label = go.AddComponent<ThemedLabel>();
        label.Configure(theme, tmp, role, px, tint);
        return label;
    }

    // ---------- kit factories ----------

    public static GameObject MakeInkPanel(Transform parent, Vector2 size)
    {
        UITheme theme = LoadTheme();
        GameObject go = NewUI("InkPanel", parent);
        ((RectTransform)go.transform).sizeDelta = size;
        Image bg = go.AddComponent<Image>();
        bg.color = theme.inkPanel;
        Graphic[] outer = MakeFrameLines(go.transform, "Frame", 0f, 2f, theme.brassLine);
        Graphic[] inner = MakeFrameLines(go.transform, "FrameInner", 4f, 1f, theme.brassLine);
        Graphic[] lines = new Graphic[outer.Length + inner.Length];
        outer.CopyTo(lines, 0);
        inner.CopyTo(lines, outer.Length);
        InkPanel panel = go.AddComponent<InkPanel>();
        panel.Configure(theme, bg, lines);
        return go;
    }

    public static GameObject MakeLabel(Transform parent, string text, ThemedLabel.Role role, float px)
    {
        UITheme theme = LoadTheme();
        ThemedLabel label = AddLabel(parent, "Label", text, role, px, theme);
        ((RectTransform)label.transform).sizeDelta = new Vector2(400f, px * 1.6f);
        return label.gameObject;
    }

    public static GameObject MakeButton(Transform parent, string text, bool primary)
    {
        UITheme theme = LoadTheme();
        GameObject go = NewUI(primary ? "PrimaryButton" : "Button", parent);
        ((RectTransform)go.transform).sizeDelta = new Vector2(260f, RowHeight);
        Image image = go.AddComponent<Image>();
        UIVerticalGradient gradient = null;
        if (primary)
        {
            gradient = go.AddComponent<UIVerticalGradient>();
        }
        else
        {
            MakeFrameLines(go.transform, "Frame", 0f, 2f, theme.brassLine);
        }
        GameObject focus = MakeFocusFrame(go.transform, theme);
        ThemedLabel label = AddLabel(go.transform, "Text", text, ThemedLabel.Role.Label, theme.labelPx, theme,
            primary ? ThemedLabel.Tint.OnAmber : ThemedLabel.Tint.Auto);
        Stretch(label.gameObject);
        label.Text.alignment = TextAlignmentOptions.Center;
        ThemedButton button = go.AddComponent<ThemedButton>();
        button.targetGraphic = image;
        button.Configure(theme, primary, label, focus, gradient);
        return go;
    }

    // A left-edge tab: plain text on no box. See ThemedButton.ConfigureTab.
    static GameObject MakeTab(Transform parent, string text, UITheme theme)
    {
        GameObject go = NewUI("Tab", parent);
        ((RectTransform)go.transform).sizeDelta = new Vector2(260f, RowHeight);
        Image image = go.AddComponent<Image>();
        Color tintColour = theme.amber;
        tintColour.a = 0.07f;
        Image tint = NewImage("MarkTint", go.transform, tintColour, false);
        Stretch(tint.gameObject);
        Image edge = NewImage("MarkEdge", go.transform, theme.amber, false);
        Anchor(edge.gameObject, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(2, 0));
        GameObject focus = MakeFocusFrame(go.transform, theme);
        ThemedLabel label = AddLabel(go.transform, "Text", text, ThemedLabel.Role.Label, theme.labelPx, theme);
        Anchor(label.gameObject, Vector2.zero, Vector2.one, new Vector2(20, 0), Vector2.zero);
        ThemedButton button = go.AddComponent<ThemedButton>();
        button.targetGraphic = image;
        button.Configure(theme, false, label, focus, null);
        button.ConfigureTab(edge.gameObject, tint.gameObject);
        return go;
    }

    public static GameObject MakeSliderRow(Transform parent, string labelText)
    {
        UITheme theme = LoadTheme();
        GameObject row = NewUI("SliderRow", parent);
        ((RectTransform)row.transform).sizeDelta = new Vector2(RowWidth, RowHeight);

        ThemedLabel label = AddLabel(row.transform, "Label", labelText, ThemedLabel.Role.Label, theme.labelPx, theme);
        Anchor(label.gameObject, new Vector2(0, 0), new Vector2(0.38f, 1), Vector2.zero, Vector2.zero);
        ThemedLabel value = AddLabel(row.transform, "Value", "0%", ThemedLabel.Role.Number, theme.labelPx, theme);
        value.Text.alignment = TextAlignmentOptions.MidlineRight;
        Anchor(value.gameObject, new Vector2(0.88f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);

        GameObject sliderGo = NewUI("Slider", row.transform);
        Anchor(sliderGo, new Vector2(0.40f, 0.5f), new Vector2(0.86f, 0.5f), new Vector2(0, -16), new Vector2(0, 16));
        Slider slider = sliderGo.AddComponent<Slider>();
        // Transparent full-size raycast target so clicking anywhere on the track jumps the value.
        Image trackHit = sliderGo.AddComponent<Image>();
        trackHit.color = new Color(0f, 0f, 0f, 0f);
        trackHit.raycastTarget = true;

        Image background = NewImage("Background", sliderGo.transform, theme.brassLine, false);
        Anchor(background.gameObject, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -3), new Vector2(0, 3));

        GameObject fillArea = NewUI("Fill Area", sliderGo.transform);
        Anchor(fillArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(10, -3), new Vector2(-10, 3));
        Image fill = NewImage("Fill", fillArea.transform, theme.focusOutline, false);
        ((RectTransform)fill.transform).sizeDelta = Vector2.zero;

        GameObject handleArea = NewUI("Handle Slide Area", sliderGo.transform);
        Anchor(handleArea, Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));
        Image handle = NewImage("Handle", handleArea.transform, Color.white, true);
        ((RectTransform)handle.transform).sizeDelta = new Vector2(20, 0);

        slider.fillRect = (RectTransform)fill.transform;
        slider.handleRect = (RectTransform)handle.transform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.8f;
        ColorBlock colours = slider.colors;
        colours.normalColor = theme.textPrimary;
        colours.highlightedColor = theme.focusOutline;
        colours.selectedColor = theme.focusOutline;
        colours.pressedColor = theme.textMuted;
        colours.fadeDuration = 0.08f;
        slider.colors = colours;

        SliderRow sliderRow = row.AddComponent<SliderRow>();
        sliderRow.Configure(theme, labelText, slider, label.Text, value.Text);
        SliderCommitRelay relay = sliderGo.AddComponent<SliderCommitRelay>();
        relay.Configure(sliderRow);
        return row;
    }

    public static GameObject MakeSwitchRow(Transform parent, string labelText, string[] options, int index)
    {
        UITheme theme = LoadTheme();
        GameObject row = NewUI("SwitchRow", parent);
        ((RectTransform)row.transform).sizeDelta = new Vector2(RowWidth, RowHeight);
        Image hit = row.AddComponent<Image>();
        hit.color = new Color(0, 0, 0, 0);

        ThemedLabel label = AddLabel(row.transform, "Label", labelText, ThemedLabel.Role.Label, theme.labelPx, theme);
        Anchor(label.gameObject, new Vector2(0, 0), new Vector2(0.38f, 1), Vector2.zero, Vector2.zero);

        GameObject left = MakeButton(row.transform, "<", false);
        left.name = "Left";
        Anchor(left, new Vector2(0.40f, 0.5f), new Vector2(0.40f, 0.5f), new Vector2(0, -22), new Vector2(48, 22));
        GameObject right = MakeButton(row.transform, ">", false);
        right.name = "Right";
        Anchor(right, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-48, -22), new Vector2(0, 22));
        DisableNavigation(left.GetComponent<Selectable>());
        DisableNavigation(right.GetComponent<Selectable>());

        ThemedLabel value = AddLabel(row.transform, "Value", options.Length > 0 ? options[0] : "", ThemedLabel.Role.Number, theme.labelPx, theme);
        value.Text.alignment = TextAlignmentOptions.Center;
        Anchor(value.gameObject, new Vector2(0.40f, 0), new Vector2(1, 1), new Vector2(56, 0), new Vector2(-56, 0));

        GameObject focus = MakeFocusFrame(row.transform, theme);
        focus.transform.SetAsFirstSibling();

        SwitchRow switchRow = row.AddComponent<SwitchRow>();
        switchRow.targetGraphic = hit;
        switchRow.Configure(theme, labelText, options, index, label.Text, value.Text,
            left.GetComponent<Button>(), right.GetComponent<Button>(), focus);
        return row;
    }

    static void DisableNavigation(Selectable selectable)
    {
        Navigation nav = selectable.navigation;
        nav.mode = Navigation.Mode.None;
        selectable.navigation = nav;
    }

    public static GameObject MakeDropdownRow(Transform parent, string labelText)
    {
        UITheme theme = LoadTheme();
        GameObject row = NewUI("DropdownRow", parent);
        ((RectTransform)row.transform).sizeDelta = new Vector2(RowWidth, RowHeight);

        ThemedLabel label = AddLabel(row.transform, "Label", labelText, ThemedLabel.Role.Label, theme.labelPx, theme);
        Anchor(label.gameObject, new Vector2(0, 0), new Vector2(0.38f, 1), Vector2.zero, Vector2.zero);

        TMP_DefaultControls.Resources resources = new TMP_DefaultControls.Resources();
        GameObject dd = TMP_DefaultControls.CreateDropdown(resources);
        dd.name = "Dropdown";
        dd.transform.SetParent(row.transform, false);
        Anchor(dd, new Vector2(0.40f, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -22), new Vector2(0, 22));
        StyleDropdown(dd, theme);

        TMP_Dropdown dropdown = dd.GetComponent<TMP_Dropdown>();
        DropdownRow dropdownRow = row.AddComponent<DropdownRow>();
        dropdownRow.Configure(theme, labelText, dropdown, label.Text);
        return row;
    }

    static void StyleDropdown(GameObject dd, UITheme theme)
    {
        Color ink = theme.inkPanel;
        ink.a = 1f;
        Color lit = Color.Lerp(ink, theme.brassLine, 0.35f);

        Image bg = dd.GetComponent<Image>();
        bg.sprite = null;
        bg.color = Color.white;
        TMP_Dropdown dropdown = dd.GetComponent<TMP_Dropdown>();
        dropdown.targetGraphic = bg;
        ColorBlock block = dropdown.colors;
        block.normalColor = ink;
        block.highlightedColor = lit;
        block.selectedColor = lit;
        block.pressedColor = Color.Lerp(ink, Color.black, 0.3f);
        block.fadeDuration = 0.08f;
        dropdown.colors = block;
        MakeFrameLines(dd.transform, "Frame", 0f, 2f, theme.brassLine);

        // Caption and arrow
        Transform caption = dd.transform.Find("Label");
        StyleTemplateLabel(caption.gameObject, theme, ThemedLabel.Role.Body, theme.labelPx);
        Anchor(caption.gameObject, Vector2.zero, Vector2.one, new Vector2(14, 2), new Vector2(-40, -2));
        Transform arrow = dd.transform.Find("Arrow");
        if (arrow != null)
        {
            Object.DestroyImmediate(arrow.gameObject);
        }
        ThemedLabel chevron = AddLabel(dd.transform, "Arrow", "v", ThemedLabel.Role.Label, theme.labelPx, theme);
        chevron.Text.alignment = TextAlignmentOptions.Center;
        chevron.Text.color = theme.textMuted;
        Anchor(chevron.gameObject, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-36, 0), new Vector2(-4, 0));

        // Dropdown list template
        Transform template = dd.transform.Find("Template");
        Image templateBg = template.GetComponent<Image>();
        templateBg.sprite = null;
        templateBg.color = ink;
        MakeFrameLines(template, "Frame", 0f, 2f, theme.brassLine);
        Transform item = template.Find("Viewport/Content/Item");
        Image itemBg = item.Find("Item Background").GetComponent<Image>();
        itemBg.sprite = null;
        itemBg.color = Color.white;
        Toggle toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = itemBg;
        ColorBlock itemColours = toggle.colors;
        itemColours.normalColor = ink;
        itemColours.highlightedColor = lit;
        itemColours.selectedColor = lit;
        itemColours.pressedColor = Color.Lerp(ink, Color.black, 0.3f);
        itemColours.fadeDuration = 0.08f;
        toggle.colors = itemColours;
        Image check = item.Find("Item Checkmark").GetComponent<Image>();
        check.sprite = null;
        check.color = theme.textMuted;
        StyleTemplateLabel(item.Find("Item Label").gameObject, theme, ThemedLabel.Role.Body, theme.labelPx);
        Transform scrollbar = template.Find("Scrollbar");
        if (scrollbar != null)
        {
            Image sbBg = scrollbar.GetComponent<Image>();
            sbBg.sprite = null;
            sbBg.color = theme.brassLine;
            Transform sbHandle = scrollbar.Find("Sliding Area/Handle");
            if (sbHandle != null)
            {
                Image h = sbHandle.GetComponent<Image>();
                h.sprite = null;
                h.color = theme.textMuted;
            }
        }
    }

    static void StyleTemplateLabel(GameObject go, UITheme theme, ThemedLabel.Role role, float px)
    {
        TMP_Text tmp = go.GetComponent<TMP_Text>();
        tmp.font = ThemedLabel.FontFor(theme, role);
        tmp.raycastTarget = false;
        ThemedLabel themed = go.GetComponent<ThemedLabel>();
        if (themed == null)
        {
            themed = go.AddComponent<ThemedLabel>();
        }
        themed.Configure(theme, tmp, role, px);
    }

    public static GameObject MakeSectionList(Transform parent, string[] sectionNames)
    {
        UITheme theme = LoadTheme();
        GameObject root = NewUI("SectionList", parent);
        ((RectTransform)root.transform).sizeDelta = new Vector2(1100f, 560f);
        System.Collections.Generic.List<SectionSelectRelay> relayList = new System.Collections.Generic.List<SectionSelectRelay>();
        Button[] buttons = new Button[sectionNames.Length];
        GameObject[] panels = new GameObject[sectionNames.Length];

        GameObject panelHost = NewUI("Panels", root.transform);
        Anchor(panelHost, Vector2.zero, Vector2.one, new Vector2(280, 0), Vector2.zero);

        for (int i = 0; i < sectionNames.Length; i++)
        {
            GameObject b = MakeTab(root.transform, sectionNames[i], theme);
            SectionSelectRelay relay = b.AddComponent<SectionSelectRelay>();
            relayList.Add(relay);
            b.name = "Section_" + sectionNames[i];
            Anchor(b, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -(i + 1) * RowHeight - i * 8f),
                new Vector2(260, -i * (RowHeight + 8f)));
            buttons[i] = b.GetComponent<Button>();

            GameObject panel = NewUI("Panel_" + sectionNames[i], panelHost.transform);
            Stretch(panel);
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            panels[i] = panel;
        }

        SectionList list = root.AddComponent<SectionList>();
        list.Configure(theme, buttons, panels);
        for (int i = 0; i < relayList.Count; i++)
        {
            relayList[i].Configure(list, i);
        }
        return root;
    }

    public static GameObject MakeParchmentSpread(Transform parent, Vector2 size)
    {
        UITheme theme = LoadTheme();
        GameObject root = NewUI("ParchmentSpread", parent);
        ((RectTransform)root.transform).sizeDelta = size;
        Image edge = root.AddComponent<Image>();
        edge.color = theme.parchmentEdge;
        Image paper = NewImage("Paper", root.transform, theme.parchment, false);
        Anchor(paper.gameObject, Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
        Image spine = NewImage("Spine", root.transform, theme.parchmentEdge, false);
        Anchor(spine.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(-1, 40), new Vector2(1, -40));

        GameObject pagesGo = NewUI("Pages", root.transform);
        Stretch(pagesGo);
        CanvasGroup group = pagesGo.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        ThemedLabel.Tint onPaper = ThemedLabel.Tint.OnParchment;
        ThemedLabel title = AddLabel(pagesGo.transform, "LeftTitle", "Title", ThemedLabel.Role.Title, 40f, theme, onPaper);
        Anchor(title.gameObject, new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(60, -110), new Vector2(-40, -40));
        ThemedLabel sub = AddLabel(pagesGo.transform, "LeftSub", "Subtitle", ThemedLabel.Role.Flavour, theme.bodyPx, theme, onPaper);
        Anchor(sub.gameObject, new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(60, -150), new Vector2(-40, -112));
        ThemedLabel leftBody = AddLabel(pagesGo.transform, "LeftBody", "", ThemedLabel.Role.Body, theme.bodyPx, theme, onPaper);
        Anchor(leftBody.gameObject, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(60, 90), new Vector2(-40, -170));
        ThemedLabel rightBody = AddLabel(pagesGo.transform, "RightBody", "", ThemedLabel.Role.Body, theme.bodyPx, theme, onPaper);
        Anchor(rightBody.gameObject, new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(40, 90), new Vector2(-60, -60));
        foreach (ThemedLabel body in new[] { leftBody, rightBody })
        {
            body.Text.textWrappingMode = TextWrappingModes.Normal;
            body.Text.overflowMode = TextOverflowModes.Overflow;
            body.Text.alignment = TextAlignmentOptions.TopLeft;
        }
        sub.Text.textWrappingMode = TextWrappingModes.Normal;
        title.Text.textWrappingMode = TextWrappingModes.Normal;
        title.Text.overflowMode = TextOverflowModes.Overflow;
        title.Text.alignment = TextAlignmentOptions.BottomLeft;
        sub.Text.overflowMode = TextOverflowModes.Overflow;

        Image ribbon = NewImage("Ribbon", root.transform, theme.ribbon, false);
        Anchor(ribbon.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-70, -90), new Vector2(-30, 0));

        GameObject prev = MakeButton(root.transform, "Previous", false);
        prev.name = "Prev";
        Anchor(prev, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 24), new Vector2(220, 24 + RowHeight));
        GameObject next = MakeButton(root.transform, "Next", false);
        next.name = "Next";
        Anchor(next, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-220, 24), new Vector2(-40, 24 + RowHeight));

        ParchmentSpread spread = root.AddComponent<ParchmentSpread>();
        spread.Configure(theme, title.Text, sub.Text, leftBody.Text, rightBody.Text,
            prev.GetComponent<Button>(), next.GetComponent<Button>(), ribbon, group);
        return root;
    }
}
}
