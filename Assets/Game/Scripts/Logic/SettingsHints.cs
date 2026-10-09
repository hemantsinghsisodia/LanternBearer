namespace LanternKeeper
{
// Every Settings hint line, in one place. Keyed by row id; Preset and Render scale also by value index.
// Pure data (no scene access). The ids are what SettingsHintRow carries on each row in the SettingsScreen prefab.
public static class SettingsHints
{
    public const string Brightness = "brightness";
    public const string Resolution = "resolution";
    public const string WindowMode = "window-mode";
    public const string Preset = "preset";
    public const string RenderScale = "render-scale";
    public const string VSync = "vsync";
    public const string FpsCounter = "fps-counter";
    public const string MasterVolume = "master-volume";
    public const string MusicVolume = "music-volume";
    public const string EffectsVolume = "effects-volume";
    public const string AmbienceVolume = "ambience-volume";
    public const string TextSize = "text-size";
    public const string ReduceFlashing = "reduce-flashing";
    // Controls rows follow the order of ControlsSection.Bindings.
    public const string ControlPrefix = "control-";

    // Shown when nothing is selected.
    public const string Neutral = "";
    // Shown on the Controls tab, whose rows are read-only.
    public const string ControlsTab = "These controls are fixed and cannot be rebound yet.";

    public static readonly string[] SettingRowIds =
    {
        Brightness, Resolution, WindowMode,
        Preset, RenderScale, VSync, FpsCounter,
        MasterVolume, MusicVolume, EffectsVolume, AmbienceVolume,
        TextSize, ReduceFlashing
    };

    // Checked against GraphicsProfileSetup.WriteProfile:
    // Low: render scale 0.5, shadows to 8 m on one cascade, no post-processing, no water ripples or foam, sparse short grass, glow lights capped.
    // Medium: full resolution, soft shadows to 50 m, light post-processing, 4 lights per object.
    // High: shadows to 70 m, 4x anti-aliasing, grass to 30 m, beacon shadows, extra water detail layer, 6 lights per object.
    // Ultra: shadows to 100 m at 4096, SMAA plus 4x MSAA, grass to 45 m, soft beacon shadows, water reflections, film grain, 8 lights per object.
    // Fog is not part of any preset, so no text claims it.
    public static readonly string[] PresetHints =
    {
        "Fastest. Half-resolution rendering, short shadow and grass range, no post-processing or water ripples, fewer lights. For older or integrated graphics.",
        "Balanced. Full resolution with soft shadows and light post-processing. Good for most laptops.",
        "Sharper image. Longer shadow and grass range, 4x anti-aliasing, beacon shadows, extra water detail and more lights.",
        "Most detailed. Farthest soft shadows, extra anti-aliasing, grass drawn farthest, water reflections and film grain. Needs a strong graphics card."
    };

    // Index matches SettingsMath.RenderScales: 100%, 125%, 150%, 200%.
    public static readonly string[] RenderScaleHints =
    {
        "Renders at your screen's resolution.",
        "Renders above your screen's resolution and scales down for a sharper, less jagged image. Costs some frame rate.",
        "Renders above your screen's resolution and scales down for a sharper, less jagged image. Costs some frame rate.",
        "Renders at twice your resolution (4K on a 1080p screen). Sharpest image, but much heavier on the graphics card."
    };

    public static readonly string[] ControlHints =
    {
        "Walk around the island.",
        "Hold to run faster; the lantern burns fuel quicker while you do.",
        "Jump into the air.",
        "Stand at a beacon and press to light it.",
        "Turn the camera with the mouse, the arrow keys or the right stick.",
        "Pause the game, or go back one step in a menu.",
        "Start the current island again.",
        "Switch the music off or back on.",
        "Close a card or the intro and carry on."
    };

    // True when the id has one text per value (Preset, Render scale).
    public static bool IsValueDependent(string id)
    {
        return id == Preset || id == RenderScale;
    }

    // Number of value-specific texts for the id, or 1 for a row with a single text.
    public static int ValueCount(string id)
    {
        if (id == Preset)
        {
            return PresetHints.Length;
        }
        if (id == RenderScale)
        {
            return RenderScaleHints.Length;
        }
        return 1;
    }

    public static string ControlId(int index)
    {
        return ControlPrefix + index;
    }

    // The hint for a row. valueIndex only matters for Preset and Render scale (out of range is clamped).
    // Returns Neutral for an unknown id.
    public static string Get(string id, int valueIndex)
    {
        switch (id)
        {
            case Brightness:
                return "Lightens or darkens the whole night; raise it until the sea just shows against the cliff on the test card.";
            case Resolution:
                return "The size of the game image, in pixels. Pick your screen's native size for the sharpest picture.";
            case WindowMode:
                return "Fullscreen takes over the display, Borderless fills the screen but switches windows faster, Windowed lets you resize it.";
            case Preset:
                return PresetHints[Clamp(valueIndex, PresetHints.Length)];
            case RenderScale:
                return RenderScaleHints[Clamp(valueIndex, RenderScaleHints.Length)];
            case VSync:
                return "Locks the frame rate to your monitor's refresh rate to prevent tearing. Turn off for the lowest input delay.";
            case FpsCounter:
                return "Shows the current frames per second in the corner.";
            case MasterVolume:
                return "The overall volume of everything: music, effects and ambience together.";
            case MusicVolume:
                return "The island music, including the beacon stingers and the tension layer. Press M in the game to mute it.";
            case EffectsVolume:
                return "Footsteps, the lantern, beacons, creatures and menu clicks.";
            case AmbienceVolume:
                return "The sea, the wind, the rain and the other sounds of the night.";
            case TextSize:
                return "Makes the text larger in every menu and on the in-game display.";
            case ReduceFlashing:
                return "Softens lightning flashes and slows the screen-edge pulses from moths and low fuel.";
        }
        if (id != null && id.StartsWith(ControlPrefix, System.StringComparison.Ordinal))
        {
            int index;
            if (int.TryParse(id.Substring(ControlPrefix.Length), out index) && index >= 0 && index < ControlHints.Length)
            {
                return ControlHints[index];
            }
        }
        return Neutral;
    }

    static int Clamp(int value, int count)
    {
        return value < 0 ? 0 : (value >= count ? count - 1 : value);
    }
}
}
