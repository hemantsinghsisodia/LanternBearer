using System.Collections.Generic;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LanternKeeper
{
// Validate Scene Wiring: the Phase E UI. Each problem is one message (and counts once).
public static partial class SceneWiring
{
    // HUD readouts and end-of-round panels that are still code-made or LiberationSans. They are Phase F's to restyle,
    // so the font check skips these children of the HUD canvas (and nothing else). Texts under a Phase E screen are always checked.
    static readonly HashSet<string> PhaseFHudTexts = new HashSet<string>
    {
        "StatusText", "TimerText", "PromptText", "FuelPenalty", "FpsReadout", "IslandLabel",
        "WinPanel", "LosePanel", "GraphicsPanel", "TideGauge", "TideToast", "LogToast", "IntroCard", "FuelCostGhost"
    };

    static int RequireUi(bool quiet)
    {
        string scene = EditorSceneManager.GetActiveScene().name;
        int problems = 0;
        bool isMenu = Object.FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include) != null;
        bool isIsland = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include) != null;

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
        }
        if ((isMenu || isIsland) && Object.FindAnyObjectByType<UserSettingsApplier>(FindObjectsInactive.Include) == null)
        {
            problems += Problem(scene + " missing UserSettingsApplier", quiet);
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
                if (System.Array.IndexOf(allowed, text.font) >= 0 || IsPhaseFReadout(text.transform, canvases[c].transform))
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

    static bool IsPhaseFReadout(Transform text, Transform canvas)
    {
        Transform top = text;
        while (top.parent != null && top.parent != canvas)
        {
            top = top.parent;
        }
        return PhaseFHudTexts.Contains(top.name);
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
