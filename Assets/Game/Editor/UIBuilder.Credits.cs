using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// The credits roll overlay on the Island 5 HUD canvas (FinaleInstaller calls it). It is a scene object, not part of GameHud.prefab, so
// Islands 1-4 are untouched: "CreditsRoll" holds a darkened band (RectMask2D) with the scrolling column, and an inactive "Templates"
// node with one label per line style that CreditsRoll clones from at run time. Idempotent: the object is rebuilt each run.
public static partial class UIBuilder
{
    public const string CreditsRootName = "CreditsRoll";
    const float CreditsBandBottom = 0.14f;
    const float CreditsBandTop = 0.86f;
    const float CreditsColumnWidth = 1100f;

    public static CreditsRoll InstallCredits(HUD hud)
    {
        Transform canvas = hud.transform;
        Transform old = canvas.Find(CreditsRootName);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        UITheme theme = LoadTheme();
        GameObject root = NewUI(CreditsRootName, canvas);
        Stretch(root);
        CanvasGroup group = AddPassiveGroup(root);
        group.alpha = 0f;
        AddOverlayCanvas(root, 25, false);

        GameObject panel = NewUI("Panel", root.transform);
        Stretch(panel);

        GameObject band = NewUI("Band", panel.transform);
        Anchor(band, new Vector2(0f, CreditsBandBottom), new Vector2(1f, CreditsBandTop), Vector2.zero, Vector2.zero);
        Color dim = theme.inkPanel;
        dim.a = 0.62f;
        Image backing = band.AddComponent<Image>();
        backing.color = dim;
        backing.raycastTarget = false;
        band.AddComponent<RectMask2D>();

        GameObject column = NewUI("Column", band.transform);
        RectTransform columnRect = (RectTransform)column.transform;
        columnRect.anchorMin = new Vector2(0.5f, 1f);
        columnRect.anchorMax = new Vector2(0.5f, 1f);
        columnRect.pivot = new Vector2(0.5f, 1f);
        columnRect.sizeDelta = new Vector2(CreditsColumnWidth, 100f);
        VerticalLayoutGroup layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = column.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Hairlines mark the band's edges (drawn over the scrolling text).
        Image topLine = NewImage("TopLine", band.transform, theme.brassLine, false);
        Anchor(topLine.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), Vector2.zero);
        Image bottomLine = NewImage("BottomLine", band.transform, theme.brassLine, false);
        Anchor(bottomLine.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f));

        GameObject templates = NewUI("Templates", panel.transform);
        templates.SetActive(false);
        ThemedLabel gameTitle = CreditsTemplate(templates.transform, "GameTitle", ThemedLabel.Role.Title, 88f, theme, 130f);
        ThemedLabel madeBy = CreditsTemplate(templates.transform, "MadeBy", ThemedLabel.Role.Flavour, 34f, theme, 52f);
        ThemedLabel heading = CreditsTemplate(templates.transform, "Heading", ThemedLabel.Role.Label, 32f, theme, 52f);
        heading.SetColourOverride(true, theme.amber);
        ThemedLabel entry = CreditsTemplate(templates.transform, "Entry", ThemedLabel.Role.Body, 30f, theme, 44f);
        ThemedLabel byline = CreditsTemplate(templates.transform, "Byline", ThemedLabel.Role.Flavour, 22f, theme, 34f);
        ThemedLabel closing = CreditsTemplate(templates.transform, "Closing", ThemedLabel.Role.Title, 46f, theme, 76f);

        CreditsRoll roll = root.AddComponent<CreditsRoll>();
        roll.Configure(group, panel, (RectTransform)band.transform, columnRect, gameTitle, madeBy, heading, entry, byline, closing);
        panel.SetActive(false);

        SerializedObject so = new SerializedObject(hud);
        SerializedProperty property = so.FindProperty("credits");
        if (property != null)
        {
            property.objectReferenceValue = roll;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hud);
        return roll;
    }

    static ThemedLabel CreditsTemplate(Transform parent, string name, ThemedLabel.Role role, float px, UITheme theme, float height)
    {
        ThemedLabel label = AddLabel(parent, name, "", role, px, theme);
        Wrap(label, TextAlignmentOptions.Center);
        label.Text.overflowMode = TextOverflowModes.Overflow;
        // The text sets the height (it grows with the text scale); height is only the floor.
        LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        return label;
    }
}
}
