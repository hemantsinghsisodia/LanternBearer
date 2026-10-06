using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// The pause screen prefab (ink panel on a dim) and its placement on each island's HUD canvas.
public static partial class UIBuilder
{
    public const string PausePrefabPath = "Assets/Game/Prefabs/UI/PauseScreen.prefab";
    const float PausePanelWidth = 520f;
    const float PausePanelHeight = 580f;
    const float PauseButtonWidth = 340f;
    const float PauseButtonPitch = RowHeight + 14f;

    public static GameObject BuildPauseScreen()
    {
        UITheme theme = LoadTheme();
        GameObject root = NewUI("PauseScreen", null);
        Stretch(root);

        // "Panel" is the part that shows and hides; the root stays active so PauseScreen can watch GameManager.
        GameObject visual = NewUI("Panel", root.transform);
        Stretch(visual);
        Color dimColour = theme.inkPanel;
        dimColour.a = 0.62f;
        Image dim = visual.AddComponent<Image>();
        dim.color = dimColour;
        dim.raycastTarget = true;

        GameObject ink = MakeInkPanel(visual.transform, new Vector2(PausePanelWidth, PausePanelHeight));
        ink.name = "InkPanel";

        ThemedLabel title = AddLabel(ink.transform, "Title", "Paused", ThemedLabel.Role.Title, theme.titlePx, theme);
        title.Text.alignment = TextAlignmentOptions.Center;
        Anchor(title.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30f, -120f), new Vector2(-30f, -30f));
        ThemedLabel subtitle = AddLabel(ink.transform, "IslandSubtitle", "Island 1 · The Last Light", ThemedLabel.Role.Flavour, theme.bodyPx, theme);
        subtitle.Text.alignment = TextAlignmentOptions.Center;
        Anchor(subtitle.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30f, -178f), new Vector2(-30f, -124f));

        string[] names = { "ResumeButton", "RestartButton", "HowToButton", "SettingsButton", "MenuButton" };
        string[] texts = { "Resume", "Restart", "How to Play", "Settings", "Main Menu" };
        ThemedButton[] buttons = new ThemedButton[5];
        for (int i = 0; i < buttons.Length; i++)
        {
            GameObject go = MakeButton(ink.transform, texts[i], i == 0);
            go.name = names[i];
            float top = -200f - i * PauseButtonPitch;
            Anchor(go, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-PauseButtonWidth * 0.5f, top - RowHeight), new Vector2(PauseButtonWidth * 0.5f, top));
            buttons[i] = go.GetComponent<ThemedButton>();
        }

        PauseScreen screen = root.AddComponent<PauseScreen>();
        screen.Configure(visual, title, subtitle, buttons[0], buttons[1], buttons[2], buttons[3], buttons[4]);
        root.AddComponent<TextScaler>();

        EnsureFolders("Assets/Game/Prefabs/UI");
        GameObject saved = SavePrefab(root, PausePrefabPath);
        Object.DestroyImmediate(root);
        Undo.ClearAll();
        AssetDatabase.SaveAssets();
        Debug.Log("Lantern Keeper: built " + PausePrefabPath);
        return saved;
    }

    // On an island's HUD canvas: removes the old code-built PausePanel, adds the PauseScreen and a hidden SettingsScreen,
    // wires HUD.pauseScreen, and puts a TextScaler (plain-text mode) on the canvas root. Idempotent.
    public static void InstallPauseInActiveScene()
    {
        HUD hud = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include);
        if (hud == null)
        {
            return;
        }
        InstallPause(hud);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // Used by IslandBuilder's HUD creation too, so rebuilt islands get the same result.
    public static void InstallPause(HUD hud)
    {
        Transform canvas = hud.transform;
        Transform legacy = canvas.Find("PausePanel");
        if (legacy != null)
        {
            Object.DestroyImmediate(legacy.gameObject);
        }

        Transform pauseTransform = canvas.Find("PauseScreen");
        if (pauseTransform == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefabPath);
            if (prefab == null)
            {
                prefab = BuildPauseScreen();
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            instance.name = "PauseScreen";
            pauseTransform = instance.transform;
        }

        Transform settingsTransform = canvas.Find("SettingsScreen");
        if (settingsTransform == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            if (prefab == null)
            {
                prefab = BuildSettingsScreen();
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            instance.name = "SettingsScreen";
            instance.SetActive(false);
            settingsTransform = instance.transform;
        }

        PauseScreen pause = pauseTransform.GetComponent<PauseScreen>();
        pause.SetSettings(settingsTransform.GetComponent<SettingsScreen>());
        EditorUtility.SetDirty(pause);

        TextScaler scaler = hud.GetComponent<TextScaler>();
        if (scaler == null)
        {
            scaler = hud.gameObject.AddComponent<TextScaler>();
        }
        scaler.ScalePlainText = true;
        EditorUtility.SetDirty(scaler);

        // The status line ("Drain x2.25 · Moths 2/2 • 2 moths on you") is about 420 px at 130% text; keep its box wide enough.
        Transform status = canvas.Find("StatusText");
        if (status != null)
        {
            RectTransform statusRect = (RectTransform)status;
            if (!Mathf.Approximately(statusRect.sizeDelta.x, HUD.StatusBoxWidth))
            {
                statusRect.sizeDelta = new Vector2(HUD.StatusBoxWidth, statusRect.sizeDelta.y);
                EditorUtility.SetDirty(statusRect);
            }
        }

        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("pauseScreen").objectReferenceValue = pause;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Lantern Keeper/Install Pause")]
    public static void InstallPauseEverywhere()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play mode before installing the pause screen.");
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
            InstallPauseInActiveScene();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("Installed pause in " + scenes[i]);
        }
    }
}
}
