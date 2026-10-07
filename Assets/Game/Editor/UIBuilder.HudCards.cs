using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// The overlays inside GameHud.prefab: toasts, the island intro card and the win / lose panel. Each sits on its own
// sorting canvas so it draws above the pause and settings screens on the HUD canvas.
public static partial class UIBuilder
{
    static void AddOverlayCanvas(GameObject go, int order, bool raycaster)
    {
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
        if (raycaster)
        {
            go.AddComponent<GraphicRaycaster>();
        }
    }

    static CanvasGroup AddPassiveGroup(GameObject go)
    {
        CanvasGroup group = go.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    static void IgnoreLayout(GameObject go)
    {
        go.AddComponent<LayoutElement>().ignoreLayout = true;
    }

    static VerticalLayoutGroup AddColumnLayout(GameObject go, float spacing, RectOffset padding)
    {
        VerticalLayoutGroup column = go.AddComponent<VerticalLayoutGroup>();
        column.spacing = spacing;
        column.padding = padding;
        column.childAlignment = TextAnchor.UpperCenter;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = false;
        column.childForceExpandHeight = false;
        ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return column;
    }

    static LayoutElement Sized(GameObject go, float width, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = go.AddComponent<LayoutElement>();
        }
        element.preferredWidth = width;
        element.preferredHeight = height;
        element.minHeight = height;
        return element;
    }

    static void Wrap(ThemedLabel label, TextAlignmentOptions alignment)
    {
        label.Text.textWrappingMode = TextWrappingModes.Normal;
        label.Text.overflowMode = TextOverflowModes.Overflow;
        label.Text.alignment = alignment;
    }

    static void BuildToasts(Transform parent, UITheme theme)
    {
        GameObject root = NewUI("Toasts", parent);
        Stretch(root);
        CanvasGroup group = AddPassiveGroup(root);
        AddOverlayCanvas(root, 10, false);

        GameObject box = NewUI("Box", root.transform);
        RectTransform boxRect = (RectTransform)box.transform;
        boxRect.anchorMin = new Vector2(0.5f, 1f);
        boxRect.anchorMax = new Vector2(0.5f, 1f);
        boxRect.pivot = new Vector2(0.5f, 1f);
        boxRect.anchoredPosition = new Vector2(0f, -110f);
        boxRect.sizeDelta = new Vector2(460f, 60f);
        AddColumnLayout(box, 4f, new RectOffset(28, 28, 16, 18));
        Image backing = NewImage("Backing", box.transform, theme.inkPanel, false);
        IgnoreLayout(backing.gameObject);
        Stretch(backing.gameObject);
        GameObject frameHost = NewUI("FrameHost", box.transform);
        IgnoreLayout(frameHost);
        Stretch(frameHost);
        Graphic[] outer = MakeFrameLines(frameHost.transform, "Frame", 0f, 2f, theme.brassLine);
        Graphic[] inner = MakeFrameLines(frameHost.transform, "FrameInner", 4f, 1f, theme.brassLine);
        Graphic[] lines = new Graphic[outer.Length + inner.Length];
        outer.CopyTo(lines, 0);
        inner.CopyTo(lines, outer.Length);

        ThemedLabel header = AddLabel(box.transform, "Header", "Keeper's Log", ThemedLabel.Role.Label, 20f, theme);
        Wrap(header, TextAlignmentOptions.Center);
        header.gameObject.SetActive(false);
        ThemedLabel body = AddLabel(box.transform, "Body", "", ThemedLabel.Role.Body, 24f, theme);
        Wrap(body, TextAlignmentOptions.Center);

        Toasts toasts = root.AddComponent<Toasts>();
        toasts.Configure(theme, group, boxRect, backing, lines, header, body);
        box.SetActive(false);
    }

    static void BuildIntroCard(Transform parent, UITheme theme)
    {
        GameObject root = NewUI("IntroCard", parent);
        Stretch(root);
        CanvasGroup group = AddPassiveGroup(root);
        AddOverlayCanvas(root, 20, false);

        GameObject card = MakeInkPanel(root.transform, new Vector2(900f, 680f));
        card.name = "Panel";
        RectTransform cardRect = (RectTransform)card.transform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;

        ThemedLabel title = AddLabel(card.transform, "Title", "", ThemedLabel.Role.Title, 46f, theme);
        title.Text.alignment = TextAlignmentOptions.Center;
        Anchor(title.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40f, -120f), new Vector2(-40f, -36f));
        ThemedLabel body = AddLabel(card.transform, "Body", "", ThemedLabel.Role.Flavour, 24f, theme);
        Wrap(body, TextAlignmentOptions.TopLeft);
        body.Text.richText = true;
        body.Text.enableAutoSizing = true;
        body.Text.paragraphSpacing = 10f;
        body.SetColourOverride(true, theme.textPrimary);
        Anchor(body.gameObject, Vector2.zero, Vector2.one, new Vector2(56f, 100f), new Vector2(-56f, -128f));
        ThemedLabel footer = AddLabel(card.transform, "Footer", "", ThemedLabel.Role.Body, 22f, theme);
        footer.Text.alignment = TextAlignmentOptions.Center;
        footer.SetColourOverride(true, theme.textMuted);
        Anchor(footer.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(40f, 36f), new Vector2(-40f, 76f));

        IntroCard intro = root.AddComponent<IntroCard>();
        intro.Configure(group, card, title, body, footer);
        card.SetActive(false);
    }

    static void BuildResultPanel(Transform parent, UITheme theme, Dictionary<string, Sprite> icons)
    {
        GameObject root = NewUI("ResultPanel", parent);
        Stretch(root);
        AddOverlayCanvas(root, 30, true);

        GameObject visual = NewUI("Panel", root.transform);
        Stretch(visual);
        Color dimColour = theme.inkPanel;
        dimColour.a = 0.62f;
        Image dim = visual.AddComponent<Image>();
        dim.color = dimColour;
        dim.raycastTarget = true;

        const float contentWidth = 600f;
        GameObject ink = MakeInkPanel(visual.transform, new Vector2(680f, 400f));
        ink.name = "InkPanel";
        RectTransform inkRect = (RectTransform)ink.transform;
        inkRect.anchorMin = new Vector2(0.5f, 0.5f);
        inkRect.anchorMax = new Vector2(0.5f, 0.5f);
        inkRect.pivot = new Vector2(0.5f, 0.5f);
        inkRect.anchoredPosition = Vector2.zero;
        IgnoreLayout(ink.transform.Find("Frame").gameObject);
        IgnoreLayout(ink.transform.Find("FrameInner").gameObject);
        AddColumnLayout(ink, 10f, new RectOffset(40, 40, 40, 44));

        ThemedLabel title = AddLabel(ink.transform, "Title", "", ThemedLabel.Role.Title, 48f, theme);
        title.Text.alignment = TextAlignmentOptions.Center;
        Sized(title.gameObject, contentWidth, 92f);
        ThemedLabel subtitle = AddLabel(ink.transform, "IslandSubtitle", "", ThemedLabel.Role.Flavour, 24f, theme);
        subtitle.Text.alignment = TextAlignmentOptions.Center;
        Sized(subtitle.gameObject, contentWidth, 48f);
        ThemedLabel loseLine = AddLabel(ink.transform, "LoseLine", "", ThemedLabel.Role.Flavour, 28f, theme);
        loseLine.Text.alignment = TextAlignmentOptions.Center;
        loseLine.SetColourOverride(true, theme.textPrimary);
        Sized(loseLine.gameObject, contentWidth, 64f);
        loseLine.gameObject.SetActive(false);

        ThemedLabel time = AddLabel(ink.transform, "Time", "", ThemedLabel.Role.Number, 30f, theme);
        time.Text.alignment = TextAlignmentOptions.Center;
        Sized(time.gameObject, contentWidth, 56f);
        ThemedLabel best = AddLabel(ink.transform, "Best", "", ThemedLabel.Role.Number, 26f, theme);
        best.Text.alignment = TextAlignmentOptions.Center;
        best.SetColourOverride(true, theme.textMuted);
        Sized(best.gameObject, contentWidth, 50f);
        ThemedLabel newBest = AddLabel(ink.transform, "NewBest", ResultPanel.NewBestText, ThemedLabel.Role.Label, 26f, theme);
        newBest.Text.alignment = TextAlignmentOptions.Center;
        newBest.SetColourOverride(true, theme.amber);
        Sized(newBest.gameObject, contentWidth, 50f);
        newBest.gameObject.SetActive(false);

        const float roofWidth = 420f;
        GameObject roofsGo = NewUI("BeaconRoofs", ink.transform);
        Sized(roofsGo, roofWidth, 36f);
        Image roofTemplate = NewIcon("RoofTemplate", roofsGo.transform, icons["Icon_Roof"], theme.brassLine, new Vector2(36f, 28f));
        RectTransform templateRect = roofTemplate.rectTransform;
        templateRect.anchorMin = new Vector2(0f, 0.5f);
        templateRect.anchorMax = new Vector2(0f, 0.5f);
        templateRect.pivot = new Vector2(0f, 0.5f);
        templateRect.anchoredPosition = Vector2.zero;
        roofTemplate.gameObject.SetActive(false);
        BeaconRoofs roofs = roofsGo.AddComponent<BeaconRoofs>();
        roofs.Configure(theme, roofTemplate, roofWidth, true);

        GameObject spacer = NewUI("Spacer", ink.transform);
        Sized(spacer, 10f, 10f);
        string[] names = { "NextButton", "RetryButton", "MenuButton" };
        string[] texts = { "Next island", "Retry", "Main Menu" };
        ThemedButton[] buttons = new ThemedButton[3];
        for (int i = 0; i < buttons.Length; i++)
        {
            GameObject go = MakeButton(ink.transform, texts[i], i == 0);
            go.name = names[i];
            Sized(go, 340f, RowHeight);
            buttons[i] = go.GetComponent<ThemedButton>();
        }

        ResultPanel result = root.AddComponent<ResultPanel>();
        result.Configure(theme, visual, title, subtitle, loseLine, time, best, newBest, roofs, buttons[0], buttons[1], buttons[2]);
        visual.SetActive(false);
    }
}
}
