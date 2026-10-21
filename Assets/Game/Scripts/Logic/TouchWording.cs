namespace LanternKeeper
{
// The on-screen wording that depends on the input in use (keyboard and mouse, or touch). Pure, so EditMode tests can cover it.
// The keyboard strings are the original ones and must not change.
public static class TouchWording
{
    public const string KeyboardLightLabel = "E  Light beacon";
    public const string TouchLightLabel = "Tap  Light";
    public const string KeyboardGreatLamp = "E  Light the great lamp";
    public const string TouchGreatLamp = "Tap  Light the great lamp";
    public const string TouchCardContinue = "Tap to continue";
    public const string KeyboardCreditsHint = "Press any key to skip";
    public const string TouchCreditsHint = "Tap to skip";
    public const string KeyboardIntroHint = "Press any key to begin";
    public const string KeyboardHelpHint = "Press any key to go back";

    // "E  Light beacon (-15)" or "Tap  Light (-15)".
    public static string BeaconLight(bool touch, int cost)
    {
        return (touch ? TouchLightLabel : KeyboardLightLabel) + " (-" + cost + ")";
    }

    public static string GreatLampLight(bool touch)
    {
        return touch ? TouchGreatLamp : KeyboardGreatLamp;
    }

    // The line under the intro card (intro true) or the How to Play card (intro false).
    public static string CardHint(bool touch, bool intro)
    {
        if (touch)
        {
            return TouchCardContinue;
        }

        return intro ? KeyboardIntroHint : KeyboardHelpHint;
    }

    public static string CreditsHint(bool touch)
    {
        return touch ? TouchCreditsHint : KeyboardCreditsHint;
    }

    // Settings > Controls rows. The touch list is shorter than the keyboard one (the extra label rows are hidden).
    public static string[] Controls(bool touch, string[] keyboardBindings)
    {
        return touch ? TouchBindings : keyboardBindings;
    }

    public static readonly string[] TouchBindings =
    {
        "Move — left stick",
        "Look — drag the right side",
        "Jump — Jump button",
        "Light a beacon — Light button",
        "Sprint — Sprint button (toggle)",
        "Pause — Pause button"
    };

    public static readonly string[] TouchControlHints =
    {
        "Hold the left side of the screen and push in a direction to walk. Push all the way to run.",
        "Drag a finger on the right side of the screen to turn the camera.",
        "Tap the large Jump button to jump into the air.",
        "Stand at a beacon and tap Light to light it.",
        "Tap Sprint to run until you tap it again; the lantern burns fuel quicker while you do.",
        "Tap Pause to pause the game, or to go back one step in a menu."
    };

    // The hint for Settings > Controls row `index`: the touch text for the first rows in touch mode, "" past the touch list.
    public static string ControlHint(bool touch, int index, string keyboardHint)
    {
        if (!touch)
        {
            return keyboardHint;
        }

        return index >= 0 && index < TouchControlHints.Length ? TouchControlHints[index] : "";
    }

    // Whether Controls row `index` is shown (all rows on the keyboard, only the touch rows in touch mode).
    public static bool ControlRowVisible(bool touch, int index)
    {
        return !touch || index < TouchBindings.Length;
    }
}
}
