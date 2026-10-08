using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
    public class StormCapeSmokeTest
    {
        const float RunSeconds = 60f;
        const float MaxSubmergedSeconds = 1f;
        const float SubmergeDepth = 0.5f;

        // Mirrors GameSettings.IntroKey; this assembly can't reference Assembly-CSharp.
        const string IntroKey = "LanternKeeperIntro_island4";

        readonly List<string> problems = new List<string>();
        bool hadIntroKey;
        int previousIntroValue;

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
            if (hadIntroKey)
            {
                PlayerPrefs.SetInt(IntroKey, previousIntroValue);
            }
            else
            {
                PlayerPrefs.DeleteKey(IntroKey);
            }

            PlayerPrefs.Save();
        }

        // The game prints informational diagnostics (quality presets, audio routing), which
        // LogAssert.NoUnexpectedReceived would also reject. Collect only warnings, errors,
        // asserts and exceptions, and fail on any of them.
        void OnLog(string message, string stackTrace, LogType type)
        {
            // A cue without a recording falls back to synthesis and says so once; that is not a problem.
            if (type != LogType.Log && !message.StartsWith("Sound cue has no clip"))
            {
                problems.Add("[" + type + "] " + message);
            }
        }

        [UnityTest]
        public IEnumerator IslandRunsSixtySeconds()
        {
            problems.Clear();
            Application.logMessageReceived += OnLog;

            // The first-visit intro card pauses the game, so mark it seen before the scene loads.
            hadIntroKey = PlayerPrefs.HasKey(IntroKey);
            previousIntroValue = PlayerPrefs.GetInt(IntroKey, 0);
            PlayerPrefs.SetInt(IntroKey, 1);
            SceneManager.LoadScene("Island4");
            yield return null;
            yield return null;

            Time.timeScale = 4f;

            Assert.IsTrue(CountOfType("Wind") > 0, "Wind missing");
            Assert.IsTrue(CountOfType("Lightning") > 0, "Lightning missing");
            Assert.IsTrue(CountOfType("ShadeSpawner") > 0, "ShadeSpawner missing");

            Transform player = FindPlayer();
            Assert.IsNotNull(player, "PlayerController not found");
            PropertyInfo surface = FindType("WaterHazard").GetProperty("SurfaceY", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(surface, "WaterHazard.SurfaceY not found");

            // SurfaceY defaults to -100, which would make the submersion check below pass vacuously.
            Assert.Greater((float)surface.GetValue(null), -50f, "WaterHazard.SurfaceY was never set on Island4; the submersion check would be meaningless");
            Assert.Greater(CountOfType("WaterHazard"), 0, "No WaterHazard found on Island4");

            float elapsed = 0f;
            float submerged = 0f;
            float worstSubmerged = 0f;
            int maxShades = 0;
            while (elapsed < RunSeconds)
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;

                float surfaceY = (float)surface.GetValue(null);
                if (player.position.y < surfaceY - SubmergeDepth)
                {
                    submerged += dt;
                    if (submerged > worstSubmerged)
                    {
                        worstSubmerged = submerged;
                    }
                }
                else
                {
                    submerged = 0f;
                }

                int shades = CountOfType("Shade");
                if (shades > maxShades)
                {
                    maxShades = shades;
                }
            }

            Assert.Greater(maxShades, 0, "No Shade appeared during the run");
            Assert.LessOrEqual(worstSubmerged, MaxSubmergedSeconds, "Player stayed below the water surface too long");
            Assert.IsEmpty(problems, "Warnings or errors were logged: " + string.Join(" | ", problems));
        }

        static System.Type FindType(string name)
        {
            System.Type type = System.Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, "Type LanternKeeper." + name + " not found in Assembly-CSharp");
            return type;
        }

        static int CountOfType(string typeName)
        {
            int count = 0;
            MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].GetType().Name == typeName)
                {
                    count++;
                }
            }
            return count;
        }

        static Transform FindPlayer()
        {
            MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].GetType().Name == "PlayerController")
                {
                    return all[i].transform;
                }
            }
            return null;
        }
    }
}
