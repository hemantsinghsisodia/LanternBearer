using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// The Keeper's Log screen prefab: a dim backdrop and the open parchment book with a Close button.
public static partial class UIBuilder
{
    public const string LogPrefabPath = "Assets/Game/Prefabs/UI/KeepersLogScreen.prefab";
    const float LogBookWidth = 1400f;
    const float LogBookHeight = 800f;

    public static GameObject BuildLogScreen()
    {
        UITheme theme = LoadTheme();
        GameObject root = NewUI("KeepersLogScreen", null);
        Stretch(root);
        Color dim = theme.inkPanel;
        dim.a = 0.88f;
        Image backdrop = NewImage("Backdrop", root.transform, dim, true);
        Stretch(backdrop.gameObject);

        GameObject book = MakeParchmentSpread(root.transform, new Vector2(LogBookWidth, LogBookHeight));
        RectTransform bookRect = (RectTransform)book.transform;
        bookRect.anchorMin = new Vector2(0.5f, 0.5f);
        bookRect.anchorMax = new Vector2(0.5f, 0.5f);
        bookRect.pivot = new Vector2(0.5f, 0.5f);
        bookRect.anchoredPosition = Vector2.zero;
        ParchmentSpread spread = book.GetComponent<ParchmentSpread>();

        GameObject closeGo = MakeButton(book.transform, "Close", true);
        closeGo.name = "CloseButton";
        Anchor(closeGo, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-90f, 24f), new Vector2(90f, 24f + RowHeight));
        Button close = closeGo.GetComponent<Button>();

        KeepersLogScreen screen = root.AddComponent<KeepersLogScreen>();
        screen.Configure(spread, close, spread.NextButton);
        root.AddComponent<TextScaler>();

        EnsureFolders("Assets/Game/Prefabs/UI");
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, LogPrefabPath);
        Object.DestroyImmediate(root);
        Undo.ClearAll();
        AssetDatabase.SaveAssets();
        Debug.Log("Lantern Keeper: built " + LogPrefabPath);
        return saved;
    }

    // Replaces the old scrolling log panel (a direct child named LogPanel) with the hidden KeepersLogScreen. Idempotent.
    public static void InstallLogInActiveScene()
    {
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null)
        {
            return;
        }
        Transform legacy = menu.transform.Find("LogPanel");
        if (legacy != null)
        {
            Object.DestroyImmediate(legacy.gameObject);
        }
        if (menu.transform.Find("KeepersLogScreen") == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LogPrefabPath);
            if (prefab == null)
            {
                prefab = BuildLogScreen();
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, menu.transform);
            instance.name = "KeepersLogScreen";
            instance.SetActive(false);
        }
    }
}
}
