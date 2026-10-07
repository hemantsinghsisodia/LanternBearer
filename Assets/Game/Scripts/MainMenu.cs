using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
public class MainMenu : MonoBehaviour
{
    static readonly string[] FallbackIds = { "island1", "island2", "island3", "island4" };
    static readonly string[] FallbackScenes = { "Island1", "Island2", "Island3", "Island4" };
    static readonly string[] FallbackNames = { "Island 1", "Island 2", "Island 3", "Island 4" };

    [SerializeField] LevelConfig[] levels = new LevelConfig[0];

    public int LevelCount
    {
        get { return levels != null && levels.Length > 0 ? levels.Length : FallbackIds.Length; }
    }

    string LevelId(int index)
    {
        if (levels != null && levels.Length > 0)
        {
            return levels[index] != null ? levels[index].levelId : "";
        }

        return FallbackIds[index];
    }

    string SceneName(int index)
    {
        if (levels != null && levels.Length > 0)
        {
            return levels[index] != null ? levels[index].sceneName : "";
        }

        return FallbackScenes[index];
    }

    string DisplayName(int index)
    {
        if (levels != null && levels.Length > 0)
        {
            return levels[index] != null ? levels[index].displayName : "";
        }

        return FallbackNames[index];
    }

    // Beacons on the island, from its LevelConfig. A level with no config shows no roofs.
    int BeaconCount(int index)
    {
        if (levels != null && levels.Length > 0 && levels[index] != null)
        {
            return Mathf.Max(0, levels[index].beaconCount);
        }

        return 0;
    }

    // Everything one island row shows.
    public IslandInfo Info(int index)
    {
        IslandInfo info = new IslandInfo();
        info.name = DisplayName(index);
        info.beacons = BeaconCount(index);
        info.found = GameSettings.GetLogCount(LevelId(index));
        info.bestTime = GameSettings.GetBestTime(LevelId(index));
        info.unlocked = GameSettings.IsLevelUnlocked(LevelId(index));
        return info;
    }

    // The Keeper's Log data source: one entry per island with its header, its entries and how many are found.
    public IslandLog[] LogIslands()
    {
        IslandLog[] islands = new IslandLog[LevelCount];
        for (int i = 0; i < islands.Length; i++)
        {
            LevelConfig level = levels != null && levels.Length > 0 ? levels[i] : null;
            islands[i].header = level != null
                ? IslandLabels.ToHud(IslandLabels.Compose(level.displayName, level.islandTitle))
                : DisplayName(i);
            islands[i].entries = level != null && level.logEntries != null ? level.logEntries : new string[0];
            islands[i].found = Mathf.Min(GameSettings.GetLogCount(LevelId(i)), islands[i].entries.Length);
        }
        return islands;
    }

    public void PlayLevel(int index)
    {
        if (index < 0 || index >= LevelCount)
        {
            return;
        }

        if (!GameSettings.IsLevelUnlocked(LevelId(index)))
        {
            return;
        }

        string scene = SceneName(index);
        if (string.IsNullOrEmpty(scene))
        {
            return;
        }

        SceneManager.LoadScene(scene);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
}
