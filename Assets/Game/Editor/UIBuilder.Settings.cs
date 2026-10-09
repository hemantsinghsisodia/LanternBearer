using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace LanternKeeper
{
// The Settings screen prefab, plus the helpers that put it and the UserSettingsApplier into scenes.
public static partial class UIBuilder
{
    public const string SettingsPrefabPath = "Assets/Game/Prefabs/UI/SettingsScreen.prefab";
    const string MixerAssetPath = "Assets/Game/Audio/LanternMixer.mixer";

    public static readonly string[] SettingsSections = { "Display", "Graphics", "Audio", "Accessibility", "Controls" };

    public static readonly string[] SettingsFlavours =
    {
        "How the night looks to you.",
        "What the lamp can afford.",
        "The sea, the wind, the flame.",
        "Make the way easier to see.",
        "How the keeper answers you."
    };

    // Height reserved for the hint line: two lines at 130% text.
    public const float HintHeight = 90f;

    // Tags a settings row with its hint id. Rows with no graphic get a clear one so the whole row reports pointer hover.
    static void AddHintId(GameObject row, string id)
    {
        if (row.GetComponent<Graphic>() == null)
        {
            Image hit = row.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
        }
        row.AddComponent<SettingsHintRow>().Configure(id);
    }

    static void EnsureFolders(string folder)
    {
        string[] parts = folder.Split('/');
        string path = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = path + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(path, parts[i]);
            }
            path = next;
        }
    }

    public static GameObject BuildSettingsScreen()
    {
        UITheme theme = LoadTheme();
        GameObject root = NewUI("SettingsScreen", null);
        Stretch(root);
        Image dim = root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.62f);
        dim.raycastTarget = true;

        GameObject panel = MakeInkPanel(root.transform, new Vector2(1360f, 780f));
        panel.name = "Panel";

        ThemedLabel title = AddLabel(panel.transform, "Title", "Settings", ThemedLabel.Role.Title, theme.titlePx, theme);
        Anchor(title.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -126), new Vector2(660, -30));

        ThemedLabel flavour = AddLabel(panel.transform, "Flavour", SettingsFlavours[0], ThemedLabel.Role.Flavour, theme.bodyPx, theme);
        Anchor(flavour.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(348, -178), new Vector2(-60, -126));

        GameObject listGo = MakeSectionList(panel.transform, SettingsSections);
        RectTransform listRect = (RectTransform)listGo.transform;
        listRect.anchorMin = new Vector2(0, 1);
        listRect.anchorMax = new Vector2(0, 1);
        listRect.pivot = new Vector2(0, 1);
        listRect.anchoredPosition = new Vector2(60, -190);
        listRect.sizeDelta = new Vector2(1240f, 450f);
        SectionList list = listGo.GetComponent<SectionList>();
        GameObject[] panels = list.SectionPanels;

        // Display
        DisplaySection displaySection = panels[0].AddComponent<DisplaySection>();
        GameObject brightnessGo = MakeSliderRow(panels[0].transform, "Brightness");
        AddHintId(brightnessGo, SettingsHints.Brightness);
        SliderRow brightness = brightnessGo.GetComponent<SliderRow>();
        GameObject cardGo = NewUI("TestCard", panels[0].transform);
        ((RectTransform)cardGo.transform).sizeDelta = new Vector2(760f, 130f);
        RawImage card = cardGo.AddComponent<RawImage>();
        card.color = Color.white;
        card.raycastTarget = false;
        MakeFrameLines(cardGo.transform, "Frame", 0f, 2f, theme.brassLine);
        ThemedLabel caption = AddLabel(panels[0].transform, "CardCaption",
            "Raise brightness until the sea is just visible against the cliff.", ThemedLabel.Role.Body, theme.bodyPx, theme);
        caption.Text.color = theme.textMuted;
        ((RectTransform)caption.transform).sizeDelta = new Vector2(900f, 44f);
        GameObject resolutionGo = MakeDropdownRow(panels[0].transform, "Resolution");
        AddHintId(resolutionGo, SettingsHints.Resolution);
        DropdownRow resolution = resolutionGo.GetComponent<DropdownRow>();
        GameObject windowGo = MakeSwitchRow(panels[0].transform, "Window mode", new[] { "Fullscreen", "Borderless", "Windowed" }, 1);
        AddHintId(windowGo, SettingsHints.WindowMode);
        SwitchRow window = windowGo.GetComponent<SwitchRow>();
        displaySection.Configure(brightness, card, resolution, window);

        // Graphics
        GraphicsSection graphicsSection = panels[1].AddComponent<GraphicsSection>();
        GameObject presetGo = MakeSwitchRow(panels[1].transform, "Preset", new[] { "Low", "Medium", "High", "Ultra" }, 1);
        AddHintId(presetGo, SettingsHints.Preset);
        SwitchRow preset = presetGo.GetComponent<SwitchRow>();
        GameObject renderScaleGo = MakeSwitchRow(panels[1].transform, "Render scale", SettingsMath.RenderScaleLabels(), 0);
        AddHintId(renderScaleGo, SettingsHints.RenderScale);
        SwitchRow renderScale = renderScaleGo.GetComponent<SwitchRow>();
        GameObject vsyncGo = MakeSwitchRow(panels[1].transform, "VSync", new[] { "Off", "On" }, 0);
        AddHintId(vsyncGo, SettingsHints.VSync);
        SwitchRow vsync = vsyncGo.GetComponent<SwitchRow>();
        GameObject fpsGo = MakeSwitchRow(panels[1].transform, "FPS counter", new[] { "Off", "On" }, 0);
        AddHintId(fpsGo, SettingsHints.FpsCounter);
        SwitchRow fps = fpsGo.GetComponent<SwitchRow>();
        graphicsSection.Configure(preset, renderScale, vsync, fps);

        // Audio
        AudioSection audioSection = panels[2].AddComponent<AudioSection>();
        GameObject masterGo = MakeSliderRow(panels[2].transform, "Master volume");
        AddHintId(masterGo, SettingsHints.MasterVolume);
        SliderRow master = masterGo.GetComponent<SliderRow>();
        GameObject musicGo = MakeSliderRow(panels[2].transform, "Music volume");
        AddHintId(musicGo, SettingsHints.MusicVolume);
        SliderRow music = musicGo.GetComponent<SliderRow>();
        GameObject effectsGo = MakeSliderRow(panels[2].transform, "Effects volume");
        AddHintId(effectsGo, SettingsHints.EffectsVolume);
        SliderRow effects = effectsGo.GetComponent<SliderRow>();
        GameObject ambienceGo = MakeSliderRow(panels[2].transform, "Ambience volume");
        AddHintId(ambienceGo, SettingsHints.AmbienceVolume);
        SliderRow ambience = ambienceGo.GetComponent<SliderRow>();
        audioSection.Configure(master, music, effects, ambience);

        // Accessibility
        AccessibilitySection accessibilitySection = panels[3].AddComponent<AccessibilitySection>();
        GameObject textSizeGo = MakeSwitchRow(panels[3].transform, "Text size", new[] { "100%", "115%", "130%" }, 0);
        AddHintId(textSizeGo, SettingsHints.TextSize);
        SwitchRow textSize = textSizeGo.GetComponent<SwitchRow>();
        GameObject reduceGo = MakeSwitchRow(panels[3].transform, "Reduce flashing", new[] { "Off", "On" }, 0);
        AddHintId(reduceGo, SettingsHints.ReduceFlashing);
        SwitchRow reduce = reduceGo.GetComponent<SwitchRow>();
        accessibilitySection.Configure(textSize, reduce);

        // Controls
        ControlsSection controlsSection = panels[4].AddComponent<ControlsSection>();
        ThemedLabel[] rows = new ThemedLabel[ControlsSection.Bindings.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            ThemedLabel row = AddLabel(panels[4].transform, "Binding" + i, ControlsSection.Bindings[i], ThemedLabel.Role.Body, theme.bodyPx, theme);
            ((RectTransform)row.transform).sizeDelta = new Vector2(760f, 40f);
            row.Text.raycastTarget = true;
            row.gameObject.AddComponent<SettingsHintRow>().Configure(SettingsHints.ControlId(i));
            rows[i] = row;
        }
        controlsSection.Configure(rows);

        GameObject backGo = MakeButton(panel.transform, "Back", false);
        backGo.name = "BackButton";
        backGo.GetComponent<ThemedButton>().SetBack(true);
        Anchor(backGo, new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 40), new Vector2(320, 40 + RowHeight));

        // One hint line along the bottom, right of Back: up to two lines at 130% text.
        ThemedLabel hint = AddLabel(panel.transform, "Hint", "", ThemedLabel.Role.Flavour, 20f, theme);
        hint.Text.textWrappingMode = TextWrappingModes.Normal;
        hint.Text.overflowMode = TextOverflowModes.Ellipsis;
        hint.Text.alignment = TextAlignmentOptions.MidlineLeft;
        Anchor(hint.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(348, 30), new Vector2(-60, 30 + HintHeight));

        SettingsScreen screen = root.AddComponent<SettingsScreen>();
        screen.Configure(list, backGo.GetComponent<Button>(), flavour, SettingsFlavours,
            displaySection, graphicsSection, audioSection, accessibilitySection, controlsSection);
        screen.ConfigureHint(hint);
        root.AddComponent<TextScaler>();

        EnsureFolders("Assets/Game/Prefabs/UI");
        GameObject saved = SavePrefab(root, SettingsPrefabPath);
        Object.DestroyImmediate(root);
        Undo.ClearAll();
        AssetDatabase.SaveAssets();
        Debug.Log("Lantern Keeper: built " + SettingsPrefabPath);
        return saved;
    }

    // Adds a UserSettingsApplier child under the scene's Systems object (creating Systems if needed).
    public static UserSettingsApplier AddUserSettingsApplier(Transform systems)
    {
        Transform existing = systems.Find("UserSettingsApplier");
        UserSettingsApplier applier = existing != null ? existing.GetComponent<UserSettingsApplier>() : null;
        if (applier == null)
        {
            GameObject go = new GameObject("UserSettingsApplier");
            go.transform.SetParent(systems, false);
            applier = go.AddComponent<UserSettingsApplier>();
        }
        SerializedObject so = new SerializedObject(applier);
        so.FindProperty("mixer").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
        so.ApplyModifiedPropertiesWithoutUndo();
        return applier;
    }

    // Idempotent: applier in every scene, and the hidden SettingsScreen under the menu canvas in MainMenu.
    public static void InstallSettingsInActiveScene()
    {
        GameObject systems = GameObject.Find("Systems");
        if (systems == null)
        {
            systems = new GameObject("Systems");
        }
        AddUserSettingsApplier(systems.transform);

        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu != null && menu.transform.Find("SettingsScreen") == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            if (prefab == null)
            {
                prefab = BuildSettingsScreen();
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, menu.transform);
            instance.name = "SettingsScreen";
            instance.SetActive(false);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("Lantern Keeper/Install Settings")]
    public static void InstallSettingsEverywhere()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play mode before installing settings.");
            return;
        }
        string[] scenes =
        {
            "Assets/Game/Scenes/MainMenu.unity",
            "Assets/Game/Scenes/Island1.unity",
            "Assets/Game/Scenes/Island2.unity",
            "Assets/Game/Scenes/Island3.unity",
            "Assets/Game/Scenes/Island4.unity"
        };
        for (int i = 0; i < scenes.Length; i++)
        {
            EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Single);
            InstallSettingsInActiveScene();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("Installed settings in " + scenes[i]);
        }
    }
}
}
