using UnityEngine;

namespace LanternKeeper
{
public enum Difficulty
{
    Easy = 0,
    Normal = 1,
    Hard = 2
}

public static class GameSettings
{
    const string DifficultyKey = "LanternKeeperDifficulty";

    public static Difficulty Current
    {
        get
        {
            int value = PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Normal);
            if (value < 0 || value > 2)
            {
                value = (int)Difficulty.Normal;
            }

            return (Difficulty)value;
        }
        set
        {
            PlayerPrefs.SetInt(DifficultyKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    public static void CycleDifficulty()
    {
        int next = ((int)Current + 1) % 3;
        Current = (Difficulty)next;
    }

    public static float DrainMultiplier
    {
        get
        {
            if (Current == Difficulty.Easy)
            {
                return 0.75f;
            }

            if (Current == Difficulty.Hard)
            {
                return 1.3f;
            }

            return 1f;
        }
    }

    public static float BeaconFuelCost
    {
        get
        {
            if (Current == Difficulty.Easy)
            {
                return 10f;
            }

            if (Current == Difficulty.Hard)
            {
                return 20f;
            }

            return 15f;
        }
    }

    public static float MothCountMultiplier
    {
        get
        {
            if (Current == Difficulty.Easy)
            {
                return 0.5f;
            }

            if (Current == Difficulty.Hard)
            {
                return 1.5f;
            }

            return 1f;
        }
    }

    public static float FireflyRespawnMultiplier
    {
        get
        {
            if (Current == Difficulty.Hard)
            {
                return 1.7f;
            }

            return 1f;
        }
    }

    public static string BestTimeKey(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
        {
            levelId = "level";
        }

        return "LanternKeeperBest_" + levelId;
    }

    public static float GetBestTime(string levelId)
    {
        string key = BestTimeKey(levelId);
        if (!PlayerPrefs.HasKey(key))
        {
            return -1f;
        }

        return PlayerPrefs.GetFloat(key);
    }

    public static void TryRecordBest(string levelId, float seconds)
    {
        if (seconds < 0f)
        {
            return;
        }

        float previous = GetBestTime(levelId);
        if (previous >= 0f && seconds >= previous)
        {
            return;
        }

        PlayerPrefs.SetFloat(BestTimeKey(levelId), seconds);
        PlayerPrefs.Save();
    }

    public static void MarkWon(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
        {
            return;
        }

        PlayerPrefs.SetInt(WonKey(levelId), 1);
        PlayerPrefs.Save();
    }

    public static bool HasWon(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
        {
            return false;
        }

        return PlayerPrefs.GetInt(WonKey(levelId), 0) == 1;
    }

    public static bool IsLevelUnlocked(string levelId)
    {
        if (levelId == "island2")
        {
            return HasWon("island1");
        }

        return true;
    }

    static string WonKey(string levelId)
    {
        return "LanternKeeperWon_" + levelId;
    }
}
}
