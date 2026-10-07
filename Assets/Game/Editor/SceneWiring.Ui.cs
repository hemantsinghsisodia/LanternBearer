using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LanternKeeper
{
// Validate Scene Wiring: the Phase E UI. Each problem is one message (and counts once).
public static partial class SceneWiring
{
    static int RequireUi(bool quiet)
    {
        string scene = EditorSceneManager.GetActiveScene().name;
        int problems = 0;
        bool isMenu = Object.FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include) != null;
        bool isIsland = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include) != null || Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include) != null;

        if (isMenu)
        {
            problems += RequireScreen<MainMenuScreen>(scene, "MainMenuScreen", quiet);
            problems += RequireScreen<SettingsScreen>(scene, "SettingsScreen", quiet);
            problems += RequireScreen<KeepersLogScreen>(scene, "KeepersLogScreen", quiet);
        }
        if (isIsland)
        {
            problems += RequireScreen<PauseScreen>(scene, "PauseScreen", quiet);
            problems += RequireScreen<SettingsScreen>(scene, "SettingsScreen", quiet);
            problems += RequireGameHud(scene, quiet);
        }
        if ((isMenu || isIsland) && Object.FindAnyObjectByType<UserSettingsApplier>(FindObjectsInactive.Include) == null)
        {
            problems += Problem(scene + " missing UserSettingsApplier", quiet);
        }

        if (isIsland)
        {
            problems += RequireNoRuntimeHudText(scene, quiet);
        }

        UITheme theme = UIBuilder.LoadTheme();
        TMP_FontAsset[] allowed = theme != null
            ? new[] { theme.titleFont, theme.flavourFont, theme.uiFont, theme.uiFontStrong }
            : new TMP_FontAsset[0];
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int c = 0; c < canvases.Length; c++)
        {
            if (canvases[c].transform.parent != null)
            {
                continue;
            }
            TMP_Text[] texts = canvases[c].GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (System.Array.IndexOf(allowed, text.font) >= 0)
                {
                    continue;
                }
                string font = text.font != null ? text.font.name : "none";
                problems += Problem(scene + " text '" + PathUnder(text.transform, canvases[c].transform) + "' uses non-theme font " + font, quiet);
            }
        }
        return problems;
    }

    static int RequireScreen<T>(string scene, string screenName, bool quiet) where T : Component
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return found.Length > 0 ? 0 : Problem(scene + " missing " + screenName + " prefab", quiet);
    }

    static int RequireGameHud(string scene, bool quiet)
    {
        HUD hud = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include);
        bool found = hud != null && hud.transform.Find("GameHud") != null;
        return found ? 0 : Problem(scene + " missing GameHud prefab", quiet);
    }

    // Any text left over from the old code-made readouts (they were made in code at runtime, not baked into the prefab) is a problem.
    static int RequireNoRuntimeHudText(string scene, bool quiet)
    {
        int problems = 0;
        HUD hud = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include);
        if (hud == null)
        {
            return 0;
        }
        for (int i = 0; i < UIBuilder.LegacyHudChildren.Length; i++)
        {
            Transform legacy = hud.transform.Find(UIBuilder.LegacyHudChildren[i]);
            if (legacy == null)
            {
                continue;
            }
            TMP_Text[] texts = legacy.GetComponentsInChildren<TMP_Text>(true);
            for (int t = 0; t < texts.Length; t++)
            {
                problems += Problem(scene + " HUD text '" + PathUnder(texts[t].transform, hud.transform) + "' built at runtime", quiet);
            }
        }
        return problems;
    }

    static string PathUnder(Transform t, Transform canvas)
    {
        string path = t.name;
        for (Transform walk = t.parent; walk != null && walk != canvas; walk = walk.parent)
        {
            path = walk.name + "/" + path;
        }
        return path;
    }

    static int Problem(string message, bool quiet)
    {
        if (!quiet)
        {
            Debug.LogWarning(message);
        }
        return 1;
    }
}
}
