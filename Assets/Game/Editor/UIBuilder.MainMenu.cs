using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// The main menu screen prefab (ledger: actions on the left, island list on the right) and its scene placement.
public static partial class UIBuilder
{
    public const string MainMenuPrefabPath = "Assets/Game/Prefabs/UI/MainMenuScreen.prefab";
    public const string RoofIconPath = "Assets/Game/Art/UI/RoofIcon.png";
    const int RoofSlots = 9;
    const float MenuPanelWidth = 1100f;
    const float MenuPanelHeight = 640f;
    const float MenuColumnX = 40f;
    const float MenuColumnWidth = 320f;
    const float IslandColumnX = 430f;
    const float IslandRowWidth = 630f;
    const float IslandRowHeight = 72f;
    const float IslandRowPitch = 84f;

    // A small beacon roof silhouette (the mockup's clip polygon), white on transparent so Image.color tints it.
    static Sprite EnsureRoofSprite()
    {
        if (!File.Exists(RoofIconPath))
        {
            const int w = 40;
            const int h = 32;
            Vector2[] poly =
            {
                new Vector2(0.5f, 1f), new Vector2(1f, 0.45f), new Vector2(0.85f, 0.45f), new Vector2(0.85f, 0f),
                new Vector2(0.15f, 0f), new Vector2(0.15f, 0.45f), new Vector2(0f, 0.45f)
            };
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 4; sy++)
                    {
                        for (int sx = 0; sx < 4; sx++)
                        {
                            Vector2 p = new Vector2((x + (sx + 0.5f) / 4f) / w, (y + (sy + 0.5f) / 4f) / h);
                            if (InsidePolygon(poly, p))
                            {
                                hit++;
                            }
                        }
                    }
                    pixels[y * w + x] = new Color(1f, 1f, 1f, hit / 16f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(RoofIconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(RoofIconPath);
        }
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(RoofIconPath);
        if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(RoofIconPath);
    }

    static bool InsidePolygon(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y)
                && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    static ThemedButton PlaceAction(Transform parent, string text, bool primary, string objectName, float bottom, out GameObject go)
    {
        go = MakeButton(parent, text, primary);
        go.name = objectName;
        Anchor(go, new Vector2(0, 0), new Vector2(0, 0), new Vector2(MenuColumnX, bottom), new Vector2(MenuColumnX + MenuColumnWidth, bottom + RowHeight));
        return go.GetComponent<ThemedButton>();
    }

    static IslandRow MakeIslandRow(Transform parent, int index, Sprite roof, UITheme theme)
    {
        GameObject go = MakeTab(parent, "Island " + (index + 1), theme);
        go.name = "IslandRow_" + (index + 1);
        float top = -136f - index * IslandRowPitch;
        Anchor(go, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, top - IslandRowHeight), new Vector2(0, top));
        ThemedButton button = go.GetComponent<ThemedButton>();

        Image[] roofs = new Image[RoofSlots];
        for (int i = 0; i < RoofSlots; i++)
        {
            Image img = NewImage("Roof" + i, go.transform, theme.amber, false);
            img.sprite = roof;
            RectTransform rt = (RectTransform)img.transform;
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(IslandRow.RoofStartX + i * IslandRow.RoofPitch, 0f);
            rt.sizeDelta = new Vector2(20f, 16f);
            roofs[i] = img;
        }

        // The beacon count follows the last visible roof (IslandRow positions it); the name sits after the 9 roof slots
        // and the count; the best time is right-aligned; the lock text takes the name's place.
        ThemedLabel count = AddLabel(go.transform, "Count", "0/0", ThemedLabel.Role.Number, theme.labelPx, theme);
        RectTransform countRect = (RectTransform)count.transform;
        countRect.anchorMin = new Vector2(0, 0.5f);
        countRect.anchorMax = new Vector2(0, 0.5f);
        countRect.pivot = new Vector2(0, 0.5f);
        countRect.sizeDelta = new Vector2(72f, 40f);
        Anchor(button.Label.gameObject, Vector2.zero, Vector2.one, new Vector2(320f, 0), new Vector2(-130f, 0));
        ThemedLabel lockLabel = AddLabel(go.transform, "Locked", "Island " + (index + 1) + " · locked", ThemedLabel.Role.Label, theme.labelPx, theme);
        Anchor(lockLabel.gameObject, Vector2.zero, Vector2.one, new Vector2(20f, 0), new Vector2(-20f, 0));
        lockLabel.gameObject.SetActive(false);
        ThemedLabel time = AddLabel(go.transform, "Time", "--:--", ThemedLabel.Role.Number, theme.labelPx, theme);
        time.Text.alignment = TextAlignmentOptions.MidlineRight;
        Anchor(time.gameObject, Vector2.zero, Vector2.one, new Vector2(500f, 0), new Vector2(-18f, 0));

        IslandRow row = go.AddComponent<IslandRow>();
        row.Configure(button, roofs, time, lockLabel, count);
        return row;
    }

    public static GameObject BuildMainMenuScreen()
    {
        UITheme theme = LoadTheme();
        Sprite roof = EnsureRoofSprite();
        GameObject root = NewUI("MainMenuScreen", null);
        Stretch(root);

        GameObject panel = MakeInkPanel(root.transform, new Vector2(MenuPanelWidth, MenuPanelHeight));
        panel.name = "Panel";
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0, 0.5f);
        panelRect.anchorMax = new Vector2(0, 0.5f);
        panelRect.pivot = new Vector2(0, 0.5f);
        panelRect.anchoredPosition = new Vector2(60f, 0f);

        // Left column
        ThemedLabel title = AddLabel(panel.transform, "Title", "Lantern Keeper", ThemedLabel.Role.Title, theme.titlePx, theme);
        title.Text.textWrappingMode = TextWrappingModes.Normal;
        title.Text.overflowMode = TextOverflowModes.Overflow;
        title.Text.alignment = TextAlignmentOptions.TopLeft;
        Anchor(title.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(MenuColumnX, -210f), new Vector2(MenuColumnX + MenuColumnWidth, -30f));
        ThemedLabel flavour = AddLabel(panel.transform, "Flavour", "Light the beacons before the flame dies.", ThemedLabel.Role.Flavour, theme.bodyPx, theme);
        flavour.Text.textWrappingMode = TextWrappingModes.Normal;
        flavour.Text.overflowMode = TextOverflowModes.Overflow;
        flavour.Text.alignment = TextAlignmentOptions.TopLeft;
        Anchor(flavour.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(MenuColumnX, -320f), new Vector2(MenuColumnX + MenuColumnWidth, -222f));

        float step = RowHeight + 14f;
        GameObject goPlay, goLog, goSettings, goQuit;
        ThemedButton quit = PlaceAction(panel.transform, "Quit", false, "QuitButton", 40f, out goQuit);
        ThemedButton settings = PlaceAction(panel.transform, "Settings", false, "SettingsButton", 40f + step, out goSettings);
        ThemedButton log = PlaceAction(panel.transform, "Keeper's Log", false, "LogButton", 40f + 2f * step, out goLog);
        ThemedButton play = PlaceAction(panel.transform, "Play", true, "PlayButton", 40f + 3f * step, out goPlay);

        // Divider
        Image divider = NewImage("Divider", panel.transform, theme.brassLine, false);
        Color dividerColour = theme.brassLine;
        dividerColour.a = 0.7f;
        divider.color = dividerColour;
        Anchor(divider.gameObject, new Vector2(0, 0), new Vector2(0, 1), new Vector2(MenuColumnX + MenuColumnWidth + 30f, 40f), new Vector2(MenuColumnX + MenuColumnWidth + 32f, -40f));

        // Right column: header then the rows
        GameObject listGo = NewUI("IslandList", panel.transform);
        RectTransform listRect = (RectTransform)listGo.transform;
        listRect.anchorMin = new Vector2(0, 1);
        listRect.anchorMax = new Vector2(0, 1);
        listRect.pivot = new Vector2(0, 1);
        listRect.anchoredPosition = new Vector2(IslandColumnX, -34f);
        listRect.sizeDelta = new Vector2(IslandRowWidth, 560f);

        ThemedLabel header = AddLabel(listGo.transform, "Header", "Islands", ThemedLabel.Role.Flavour, theme.bodyPx, theme);
        Anchor(header.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50f), new Vector2(0, 0));

        SwitchRow difficulty = MakeSwitchRow(listGo.transform, "Difficulty", new[] { "Easy", "Normal", "Hard" }, 1).GetComponent<SwitchRow>();
        Anchor(difficulty.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -116f), new Vector2(0, -60f));

        IslandRow[] rows = new IslandRow[4];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = MakeIslandRow(listGo.transform, i, roof, theme);
        }
        IslandList list = listGo.AddComponent<IslandList>();
        list.Configure(theme, rows, difficulty);

        MainMenuScreen screen = root.AddComponent<MainMenuScreen>();
        screen.Configure(panel, play, log, settings, quit, list, difficulty);
        root.AddComponent<TextScaler>();

        EnsureFolders("Assets/Game/Prefabs/UI");
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, MainMenuPrefabPath);
        Object.DestroyImmediate(root);
        Undo.ClearAll();
        AssetDatabase.SaveAssets();
        Debug.Log("Lantern Keeper: built " + MainMenuPrefabPath);
        return saved;
    }

    // Replaces the old code-built menu column (a direct child named Panel) with the MainMenuScreen prefab. Idempotent.
    public static void InstallMainMenuInActiveScene()
    {
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null)
        {
            return;
        }
        Transform legacy = menu.transform.Find("Panel");
        if (legacy != null)
        {
            Object.DestroyImmediate(legacy.gameObject);
        }
        if (menu.transform.Find("MainMenuScreen") == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabPath);
            if (prefab == null)
            {
                prefab = BuildMainMenuScreen();
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, menu.transform);
            instance.name = "MainMenuScreen";
            instance.transform.SetAsFirstSibling();
        }
        InstallLogInActiveScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("Lantern Keeper/Install Main Menu")]
    public static void InstallMainMenu()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play mode before installing the main menu.");
            return;
        }
        EditorSceneManager.OpenScene("Assets/Game/Scenes/MainMenu.unity", OpenSceneMode.Single);
        InstallMainMenuInActiveScene();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Installed the main menu screen in MainMenu.");
    }
}
}
