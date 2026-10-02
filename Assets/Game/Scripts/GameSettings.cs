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

    public static float BeaconDrainBonusPerLit
    {
        get
        {
            if (Current == Difficulty.Easy)
            {
                return 0.05f;
            }

            if (Current == Difficulty.Hard)
            {
                return 0.11f;
            }

            return 0.08f;
        }
    }

    public static float GustMultiplier()
    {
        return StormTuning.GustMultiplier((int)Current);
    }

    public static int MaxShades()
    {
        return StormTuning.MaxShades((int)Current);
    }

    public static float ShadeSteal()
    {
        return StormTuning.ShadeSteal((int)Current);
    }

    public static Vector2 LightningInterval()
    {
        return StormTuning.LightningInterval((int)Current);
    }

    public static float SafeZoneDrainMultiplier
    {
        get { return 0.35f; }
    }

    public static int MaxMoths
    {
        get
        {
            if (Current == Difficulty.Easy)
            {
                return 8;
            }

            if (Current == Difficulty.Hard)
            {
                return 16;
            }

            return 12;
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

        if (levelId == "island3")
        {
            return HasWon("island2");
        }

        if (levelId == "island4")
        {
            return HasWon("island3");
        }

        return true;
    }

    public static int GetLogCount(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
        {
            return 0;
        }

        return Mathf.Max(0, PlayerPrefs.GetInt(LogKey(levelId), 0));
    }

    public static void RecordLogRead(string levelId, int count)
    {
        if (string.IsNullOrEmpty(levelId) || count <= GetLogCount(levelId))
        {
            return;
        }

        PlayerPrefs.SetInt(LogKey(levelId), count);
        PlayerPrefs.Save();
    }

    static string LogKey(string levelId)
    {
        return "LanternKeeperLog_" + levelId;
    }

    public static bool HasSeenIntro(string levelId)
    {
        return PlayerPrefs.GetInt(IntroKey(levelId), 0) == 1;
    }

    public static void MarkIntroSeen(string levelId)
    {
        PlayerPrefs.SetInt(IntroKey(levelId), 1);
        PlayerPrefs.Save();
    }

    static string IntroKey(string levelId)
    {
        return "LanternKeeperIntro_" + levelId;
    }

    static string WonKey(string levelId)
    {
        return "LanternKeeperWon_" + levelId;
    }
}
}
