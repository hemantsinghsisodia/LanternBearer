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

        readonly List<string> problems = new List<string>();

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
        }

        // The game prints informational diagnostics (quality presets, audio routing), which
        // LogAssert.NoUnexpectedReceived would also reject. Collect only warnings, errors,
        // asserts and exceptions, and fail on any of them.
        void OnLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Log)
            {
                problems.Add("[" + type + "] " + message);
            }
        }

        [UnityTest]
        public IEnumerator IslandRunsSixtySeconds()
        {
            problems.Clear();
            Application.logMessageReceived += OnLog;
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
            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (System.Type type in assembly.GetTypes())
                {
                    if (type.Name == name && type.Namespace == "LanternKeeper")
                    {
                        return type;
                    }
                }
            }
            return null;
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
