using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// The in-game HUD prefab (layout A: top-left cluster). Widgets are placed here and only read state at run time;
// InstallGameHud puts it on each island's HUD canvas; HUD.cs drives it.
public static partial class UIBuilder
{
    public const string GameHudPrefabPath = "Assets/Game/Prefabs/UI/GameHud.prefab";

    const float HudMargin = 24f;
    const float HudPanelWidth = 300f;
    const float HudPanelHeight = 116f;
    const float HudPanelPad = 14f;
    const float HudRowGap = 8f;
    const float HudChipRowHeight = 44f;
    static readonly Color HudBarBack = new Color(0.227f, 0.204f, 0.157f, 1f);
    static readonly Color HudEmber = new Color(0.91f, 0.455f, 0.231f, 1f);

    public static GameObject BuildGameHud()
    {
        UITheme theme = LoadTheme();
        Dictionary<string, Sprite> icons = HudIcons.Ensure();
        GameObject root = NewUI("GameHud", null);
        Stretch(root);

        // ---- top-left cluster: fuel gauge and beacon roofs on an ink panel ----
        GameObject panel = MakeInkPanel(root.transform, new Vector2(HudPanelWidth, HudPanelHeight));
        panel.name = "Cluster";
        PlaceTopLeft(panel, HudMargin, HudMargin, HudPanelWidth, HudPanelHeight);

        GameObject gaugeGo = NewUI("FuelGauge", panel.transform);
        PlaceTopLeft(gaugeGo, HudPanelPad, HudPanelPad, HudPanelWidth - 2f * HudPanelPad, 56f);
        const float lanternX = 26f;
        const float lanternY = 28f;
        Image glow = NewIcon("Glow", gaugeGo.transform, icons["Icon_Glow"], theme.amber, new Vector2(84f, 84f));
        PlaceCentre(glow.gameObject, lanternX, lanternY);
        Image lantern = NewIcon("Lantern", gaugeGo.transform, icons["Icon_Lantern"], theme.textMuted, new Vector2(40f, 40f));
        PlaceCentre(lantern.gameObject, lanternX, lanternY);
        // The flame sits on the floor of the glass; it scales from its base.
        Image flame = NewIcon("Flame", gaugeGo.transform, icons["Icon_Flame"], theme.amber, new Vector2(20f, 24f));
        flame.preserveAspect = false;
        RectTransform flameRect = flame.rectTransform;
        flameRect.anchorMin = new Vector2(0f, 1f);
        flameRect.anchorMax = new Vector2(0f, 1f);
        flameRect.pivot = new Vector2(0.5f, 0f);
        flameRect.anchoredPosition = new Vector2(lanternX, -(lanternY + 20f - 0.24f * 40f));

        Image barBack = NewImage("BarBack", gaugeGo.transform, HudBarBack, false);
        RectTransform barRect = PlaceTopLeft(barBack.gameObject, 58f, lanternY - 3f, 120f, 6f);
        Image barFill = NewImage("Fill", barBack.transform, theme.amber, false);
        Stretch(barFill.gameObject);
        MakeFrameLines(barBack.transform, "BarFrame", 0f, 1f, theme.brassLine);
        barRect.pivot = new Vector2(0f, 1f);

        ThemedLabel change = AddLabel(gaugeGo.transform, "Change", "−10", ThemedLabel.Role.Number, 22f, theme);
        change.Text.alignment = TextAlignmentOptions.MidlineLeft;
        PlaceTopLeft(change.gameObject, 188f, lanternY - 15f, 84f, 30f);
        change.gameObject.SetActive(false);

        FuelGaugeWidget gauge = gaugeGo.AddComponent<FuelGaugeWidget>();
        gauge.Configure(theme, lantern, flame, glow, barFill, change);

        GameObject roofsGo = NewUI("BeaconRoofs", panel.transform);
        float roofsWidth = HudPanelWidth - 2f * HudPanelPad;
        PlaceTopLeft(roofsGo, HudPanelPad, 76f, roofsWidth, 24f);
        Image roofTemplate = NewIcon("RoofTemplate", roofsGo.transform, icons["Icon_Roof"], theme.brassLine, new Vector2(24f, 19f));
        RectTransform templateRect = roofTemplate.rectTransform;
        templateRect.anchorMin = new Vector2(0f, 0.5f);
        templateRect.anchorMax = new Vector2(0f, 0.5f);
        templateRect.pivot = new Vector2(0f, 0.5f);
        templateRect.anchoredPosition = Vector2.zero;
        roofTemplate.gameObject.SetActive(false);
        BeaconRoofs roofs = roofsGo.AddComponent<BeaconRoofs>();
        roofs.Configure(theme, roofTemplate, roofsWidth);

        // ---- under the panel: drain icons, then the tide / storm chip in one shared slot ----
        float drainY = HudMargin + HudPanelHeight + HudRowGap;
        float slotY = drainY + HudChipRowHeight + HudRowGap;
        BuildDrainIcons(root.transform, theme, icons, drainY);
        BuildTideChip(root.transform, theme, icons, slotY);
        BuildStormChip(root.transform, theme, icons, slotY);

        // ---- top-centre island name, top-right timer and FPS ----
        BuildIslandLabel(root.transform, theme);
        BuildTimer(root.transform, theme);
        BuildFps(root.transform, theme);

        // ---- overlays above the HUD: toasts, the intro card and the win / lose panel ----
        BuildToasts(root.transform, theme);
        BuildIntroCard(root.transform, theme);
        BuildResultPanel(root.transform, theme, icons);

        root.AddComponent<TextScaler>();

        EnsureFolders("Assets/Game/Prefabs/UI");
        GameObject saved = SavePrefab(root, GameHudPrefabPath);
        Object.DestroyImmediate(root);
        Undo.ClearAll();
        AssetDatabase.SaveAssets();
        Debug.Log("Lantern Keeper: built " + GameHudPrefabPath);
        return saved;
    }


    // Code-made readouts the prefab replaces (and the old graphics panel). Removed by name from an island's HUD canvas.
    internal static readonly string[] LegacyHudChildren =
    {
        "FuelMeter", "BeaconDots", "TimerText", "StatusText", "FuelPenalty", "GraphicsPanel", "FpsReadout", "IslandLabel", "TideGauge",
        "WinPanel", "LosePanel", "TideToast", "LogToastBack", "LogToast"
    };

    public static void InstallGameHudInActiveScene()
    {
        HUD hud = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include);
        if (hud == null)
        {
            return;
        }
        InstallGameHud(hud);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // On an island's HUD canvas: removes the old readouts and the dead GraphicsMenu component, places GameHud.prefab behind
    // the prompt and panels, wires HUD's widget fields and the compass's theme and roof icon. Idempotent.
    public static void InstallGameHud(HUD hud)
    {
        Transform canvas = hud.transform;
        RemoveDeadComponents(hud.gameObject);
        for (int i = 0; i < LegacyHudChildren.Length; i++)
        {
            Transform legacy = canvas.Find(LegacyHudChildren[i]);
            if (legacy != null)
            {
                Object.DestroyImmediate(legacy.gameObject);
            }
        }

        // The interact prompt is a scene text; it uses the theme's UI font like everything else on the HUD canvas.
        Transform promptTransform = canvas.Find("PromptText");
        TMP_Text promptText = promptTransform != null ? promptTransform.GetComponent<TMP_Text>() : null;
        UITheme promptTheme = LoadTheme();
        if (promptText != null && promptTheme != null && promptTheme.uiFont != null && promptText.font != promptTheme.uiFont)
        {
            promptText.font = promptTheme.uiFont;
            EditorUtility.SetDirty(promptText);
        }

        Transform existing = canvas.Find("GameHud");
        if (existing == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameHudPrefabPath);
            if (prefab == null)
            {
                prefab = BuildGameHud();
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            instance.name = "GameHud";
            existing = instance.transform;
        }

        // Above the fade and death overlays (which are first), below the prompt, compass markers, panels and pause.
        int index = 0;
        Transform fade = canvas.Find("FadeOverlay");
        Transform death = canvas.Find("DeathOverlay");
        if (fade != null)
        {
            index = Mathf.Max(index, fade.GetSiblingIndex() + 1);
        }
        if (death != null)
        {
            index = Mathf.Max(index, death.GetSiblingIndex() + 1);
        }
        if (existing.GetSiblingIndex() < index)
        {
            index--;
        }
        existing.SetSiblingIndex(index);

        SerializedObject so = new SerializedObject(hud);
        SetObject(so, "fuelGauge", existing.GetComponentInChildren<FuelGaugeWidget>(true));
        SetObject(so, "roofs", existing.GetComponentInChildren<BeaconRoofs>(true));
        SetObject(so, "drainIcons", existing.GetComponentInChildren<DrainIcons>(true));
        SetObject(so, "timerLabel", existing.GetComponentInChildren<TimerLabel>(true));
        SetObject(so, "islandLabel", existing.GetComponentInChildren<IslandLabel>(true));
        SetObject(so, "tideChip", existing.GetComponentInChildren<TideChip>(true));
        SetObject(so, "stormChip", existing.GetComponentInChildren<StormChip>(true));
        SetObject(so, "introCard", existing.GetComponentInChildren<IntroCard>(true));
        SetObject(so, "resultPanel", existing.GetComponentInChildren<ResultPanel>(true));
        SetObject(so, "toasts", existing.GetComponentInChildren<Toasts>(true));
        so.ApplyModifiedPropertiesWithoutUndo();

        BeaconCompass compass = hud.GetComponent<BeaconCompass>();
        if (compass != null)
        {
            SerializedObject compassObject = new SerializedObject(compass);
            SetObject(compassObject, "theme", LoadTheme());
            SetObject(compassObject, "roofIcon", AssetDatabase.LoadAssetAtPath<Sprite>(HudIcons.Folder + "/Icon_Roof.png"));
            compassObject.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(hud);
    }

    // The old GraphicsMenu component no longer has a MonoBehaviour behind it, so Unity does not count it as a missing script.
    static void RemoveDeadComponents(GameObject go)
    {
        SerializedObject so = new SerializedObject(go);
        SerializedProperty components = so.FindProperty("m_Component");
        for (int i = components.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty entry = components.GetArrayElementAtIndex(i).FindPropertyRelative("component");
            if (entry.objectReferenceValue == null)
            {
                int before = components.arraySize;
                components.DeleteArrayElementAtIndex(i);
                if (components.arraySize == before)
                {
                    components.DeleteArrayElementAtIndex(i); // the first delete only nulls an object reference
                }
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetObject(SerializedObject so, string property, Object value)
    {
        SerializedProperty prop = so.FindProperty(property);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
        }
    }

    [MenuItem("Lantern Keeper/Install Game HUD")]
    public static void InstallGameHudEverywhere()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play mode before installing the game HUD.");
            return;
        }
        string[] scenes =
        {
            "Assets/Game/Scenes/Island1.unity",
            "Assets/Game/Scenes/Island2.unity",
            "Assets/Game/Scenes/Island3.unity",
            "Assets/Game/Scenes/Island4.unity"
        };
        for (int i = 0; i < scenes.Length; i++)
        {
            EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Single);
            InstallGameHudInActiveScene();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("Installed game HUD in " + scenes[i]);
        }
    }

    // ---------- layout helpers ----------

    static RectTransform PlaceTopLeft(GameObject go, float x, float y, float w, float h)
    {
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    static void PlaceCentre(GameObject go, float cx, float cy)
    {
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx, -cy);
    }

    static Image NewIcon(string name, Transform parent, Sprite sprite, Color colour, Vector2 size)
    {
        Image image = NewImage(name, parent, colour, false);
        image.sprite = sprite;
        image.preserveAspect = true;
        ((RectTransform)image.transform).sizeDelta = size;
        return image;
    }

    static Image NewRowIcon(string name, Transform parent, Sprite sprite, Color colour, Vector2 size)
    {
        Image image = NewIcon(name, parent, sprite, colour, size);
        LayoutElement element = image.gameObject.AddComponent<LayoutElement>();
        element.minWidth = size.x;
        element.minHeight = size.y;
        element.preferredWidth = size.x;
        element.preferredHeight = size.y;
        return image;
    }

    static HorizontalLayoutGroup AddRowLayout(GameObject go, float spacing, int padX, int padY)
    {
        HorizontalLayoutGroup row = go.AddComponent<HorizontalLayoutGroup>();
        row.spacing = spacing;
        row.padding = new RectOffset(padX, padX, padY, padY);
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        return row;
    }

    // A row that hugs its children, with an ink backing behind them. The backing ignores the layout.
    static GameObject NewChip(string name, Transform parent, UITheme theme, float spacing, out Image backing)
    {
        GameObject chip = NewUI(name, parent);
        AddRowLayout(chip, spacing, 12, 6);
        ContentSizeFitter fitter = chip.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Color ink = theme.inkPanel;
        ink.a = 0.62f;
        backing = NewImage("Backing", chip.transform, ink, false);
        backing.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Stretch(backing.gameObject);
        return chip;
    }

    static ThemedLabel NewChipLabel(Transform parent, string name, string text, ThemedLabel.Role role, float px, UITheme theme)
    {
        ThemedLabel label = AddLabel(parent, name, text, role, px, theme);
        label.Text.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }

    // ---------- widgets ----------

    static void BuildDrainIcons(Transform parent, UITheme theme, Dictionary<string, Sprite> icons, float y)
    {
        Image backing;
        GameObject row = NewChip("DrainIcons", parent, theme, 16f, out backing);
        PlaceTopLeft(row, HudMargin, y, 0f, HudChipRowHeight);

        GameObject moth = NewUI("Moth", row.transform);
        AddRowLayout(moth, 4f, 0, 0);
        NewRowIcon("Icon", moth.transform, icons["Icon_Moth"], theme.drainViolet, new Vector2(28f, 28f));
        ThemedLabel mothCount = NewChipLabel(moth.transform, "Count", "×2", ThemedLabel.Role.Number, 22f, theme);
        moth.SetActive(false);

        GameObject shield = NewRowIcon("Shield", row.transform, icons["Icon_Shield"], theme.amber, new Vector2(26f, 28f)).gameObject;
        shield.SetActive(false);
        GameObject ember = NewRowIcon("Ember", row.transform, icons["Icon_Ember"], HudEmber, new Vector2(22f, 28f)).gameObject;
        ember.SetActive(false);
        ThemedLabel multiplier = NewChipLabel(row.transform, "Multiplier", "×1.3", ThemedLabel.Role.Number, 20f, theme);
        multiplier.gameObject.SetActive(false);
        backing.enabled = false;

        DrainIcons drain = row.AddComponent<DrainIcons>();
        drain.Configure(moth, mothCount, shield, ember, multiplier, backing);
    }

    static void BuildTideChip(Transform parent, UITheme theme, Dictionary<string, Sprite> icons, float y)
    {
        Image backing;
        GameObject chip = NewChip("TideChip", parent, theme, 10f, out backing);
        PlaceTopLeft(chip, HudMargin, y, 0f, HudChipRowHeight);
        Image arrow = NewRowIcon("Arrow", chip.transform, icons["Icon_TideArrow"], TideChip.TideColour, new Vector2(24f, 24f));

        GameObject barHost = NewUI("Bar", chip.transform);
        LayoutElement element = barHost.AddComponent<LayoutElement>();
        element.minWidth = 64f;
        element.minHeight = 6f;
        element.preferredWidth = 64f;
        element.preferredHeight = 6f;
        Image barBack = barHost.AddComponent<Image>();
        barBack.color = HudBarBack;
        barBack.raycastTarget = false;
        Image fill = NewImage("Fill", barHost.transform, TideChip.TideColour, false);
        Stretch(fill.gameObject);
        MakeFrameLines(barHost.transform, "BarFrame", 0f, 1f, theme.brassLine);

        ThemedLabel word = NewChipLabel(chip.transform, "Word", "Rising", ThemedLabel.Role.Label, 20f, theme);
        TideChip tide = chip.AddComponent<TideChip>();
        tide.Configure(arrow.rectTransform, fill, word);
        chip.SetActive(false);
    }

    static void BuildStormChip(Transform parent, UITheme theme, Dictionary<string, Sprite> icons, float y)
    {
        Image backing;
        GameObject chip = NewChip("StormChip", parent, theme, 10f, out backing);
        PlaceTopLeft(chip, HudMargin, y, 0f, HudChipRowHeight);

        GameObject gauge = NewUI("WindGauge", chip.transform);
        LayoutElement element = gauge.AddComponent<LayoutElement>();
        element.minWidth = 36f;
        element.minHeight = 36f;
        element.preferredWidth = 36f;
        element.preferredHeight = 36f;
        Color dim = theme.lightningBlue;
        dim.a = 0.25f;
        Image ringBack = NewIcon("ArcBack", gauge.transform, icons["Icon_Ring"], dim, new Vector2(36f, 36f));
        Stretch(ringBack.gameObject);
        Image arc = NewIcon("Arc", gauge.transform, icons["Icon_Ring"], theme.lightningBlue, new Vector2(36f, 36f));
        Stretch(arc.gameObject);
        arc.type = Image.Type.Filled;
        arc.fillMethod = Image.FillMethod.Radial360;
        arc.fillOrigin = (int)Image.Origin360.Top;
        arc.fillAmount = 0f;
        Image arrow = NewIcon("Arrow", gauge.transform, icons["Icon_WindArrow"], theme.lightningBlue, new Vector2(20f, 20f));
        PlaceCentre(arrow.gameObject, 18f, 18f);
        RectTransform arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.anchoredPosition = Vector2.zero;

        ThemedLabel word = NewChipLabel(chip.transform, "Word", "Wind", ThemedLabel.Role.Label, 20f, theme);

        GameObject thunder = NewUI("Thunder", chip.transform);
        AddRowLayout(thunder, 6f, 0, 0);
        Image bolt = NewRowIcon("Bolt", thunder.transform, icons["Icon_Bolt"], theme.lightningBlue, new Vector2(22f, 28f));
        NewChipLabel(thunder.transform, "ThunderWord", "Thunder", ThemedLabel.Role.Label, 20f, theme);
        thunder.SetActive(false);

        StormChip storm = chip.AddComponent<StormChip>();
        storm.Configure(arrowRect, arc, word, thunder, bolt);
        chip.SetActive(false);
    }

    static void BuildIslandLabel(Transform parent, UITheme theme)
    {
        GameObject holder = NewUI("IslandLabel", parent);
        RectTransform holderRect = (RectTransform)holder.transform;
        holderRect.anchorMin = new Vector2(0.5f, 1f);
        holderRect.anchorMax = new Vector2(0.5f, 1f);
        holderRect.pivot = new Vector2(0.5f, 1f);
        holderRect.anchoredPosition = new Vector2(0f, -HudMargin);
        holderRect.sizeDelta = Vector2.zero;
        Image backing;
        GameObject chip = NewChip("Chip", holder.transform, theme, 0f, out backing);
        RectTransform chipRect = (RectTransform)chip.transform;
        chipRect.anchorMin = new Vector2(0.5f, 1f);
        chipRect.anchorMax = new Vector2(0.5f, 1f);
        chipRect.pivot = new Vector2(0.5f, 1f);
        chipRect.anchoredPosition = Vector2.zero;
        ThemedLabel label = NewChipLabel(chip.transform, "Text", "Island 1 · The Last Light", ThemedLabel.Role.Title, 30f, theme);
        label.Text.alignment = TextAlignmentOptions.Center;
        IslandLabel island = holder.AddComponent<IslandLabel>();
        island.Configure(label, chip);
    }

    static void BuildTimer(Transform parent, UITheme theme)
    {
        Image backing;
        GameObject chip = NewChip("TimerLabel", parent, theme, 0f, out backing);
        RectTransform rect = (RectTransform)chip.transform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-HudMargin, -HudMargin);
        rect.sizeDelta = Vector2.zero;
        ThemedLabel label = NewChipLabel(chip.transform, "Text", "00:00", ThemedLabel.Role.Number, 30f, theme);
        label.Text.alignment = TextAlignmentOptions.MidlineRight;
        TimerLabel timer = chip.AddComponent<TimerLabel>();
        timer.Configure(label);
    }

    static void BuildFps(Transform parent, UITheme theme)
    {
        GameObject holder = NewUI("Fps", parent);
        RectTransform holderRect = (RectTransform)holder.transform;
        holderRect.anchorMin = new Vector2(1f, 1f);
        holderRect.anchorMax = new Vector2(1f, 1f);
        holderRect.pivot = new Vector2(1f, 1f);
        holderRect.anchoredPosition = new Vector2(-HudMargin, -(HudMargin + 54f + 6f));
        holderRect.sizeDelta = Vector2.zero;
        Image backing;
        GameObject chip = NewChip("Readout", holder.transform, theme, 0f, out backing);
        RectTransform chipRect = (RectTransform)chip.transform;
        chipRect.anchorMin = new Vector2(1f, 1f);
        chipRect.anchorMax = new Vector2(1f, 1f);
        chipRect.pivot = new Vector2(1f, 1f);
        chipRect.anchoredPosition = Vector2.zero;
        ThemedLabel label = NewChipLabel(chip.transform, "Text", "60 FPS", ThemedLabel.Role.Number, 20f, theme);
        label.Text.alignment = TextAlignmentOptions.MidlineRight;
        chip.SetActive(false);
        FpsReadout fps = holder.AddComponent<FpsReadout>();
        fps.Configure(chip, label);
    }
}
}
