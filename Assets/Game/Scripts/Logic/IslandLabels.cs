namespace LanternKeeper
{
// One source string per island ("Island 4 <em dash> The Storm Cape"); the HUD shows the middle-dot form of it.
public static class IslandLabels
{
    public const string EmDash = " — ";
    public const string MiddleDot = " · ";

    public static string Compose(string displayName, string islandTitle)
    {
        string name = displayName == null ? "" : displayName;
        if (string.IsNullOrEmpty(islandTitle))
        {
            return name;
        }

        return name + EmDash + islandTitle;
    }

    public static string ToHud(string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return "";
        }

        return label.Replace(EmDash, MiddleDot);
    }
}
}
