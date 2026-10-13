using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper.Tests
{
// Beacons lit in a test write LanternKeeperLit_<id>. Tests call Save in SetUp and Restore in TearDown so the editor save is untouched.
public static class LitKeyGuard
{
    static readonly Dictionary<string, int> saved = new Dictionary<string, int>();

    public static void Save()
    {
        saved.Clear();
        for (int i = 1; i <= 5; i++)
        {
            string key = "LanternKeeperLit_island" + i;
            saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : -1;
        }
    }

    public static void Restore()
    {
        foreach (KeyValuePair<string, int> pair in saved)
        {
            if (pair.Value < 0)
            {
                PlayerPrefs.DeleteKey(pair.Key);
            }
            else
            {
                PlayerPrefs.SetInt(pair.Key, pair.Value);
            }
        }

        saved.Clear();
        PlayerPrefs.Save();
    }
}
}
