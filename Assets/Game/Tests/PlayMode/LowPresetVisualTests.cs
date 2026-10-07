using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
    // The F1 creature and beacon visuals on the Low graphics preset (Island 4 has all three). Game types are reached by reflection.
    public class LowPresetVisualTests
    {
        const string GraphicsKey = "LanternKeeperGraphics";

        int savedGraphics;
        float previousScale;
        float previousCaptureDelta;

        static Type GameType(string name)
        {
            Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp") ?? Type.GetType("LanternKeeper." + name + ", LanternKeeper.Logic");
            Assert.IsNotNull(type, name + " type not found");
            return type;
        }

        static T Get<T>(object target, string property)
        {
            PropertyInfo info = target.GetType().GetProperty(property);
            Assert.IsNotNull(info, property);
            return (T)info.GetValue(target);
        }

        static T Field<T>(object target, string field)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(info, field);
            return (T)info.GetValue(target);
        }

        [SetUp]
        public void SetUp()
        {
            savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
            previousScale = Time.timeScale;
            previousCaptureDelta = Time.captureDeltaTime;
            // Low is level 0.
            PlayerPrefs.SetInt(GraphicsKey, 0);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.SetInt(GraphicsKey, savedGraphics);
            Time.timeScale = previousScale;
            Time.captureDeltaTime = previousCaptureDelta;
        }

        IEnumerator LoadIsland4()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Island4");
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return null;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator MothWingAngleChangesOnLow()
        {
            yield return LoadIsland4();
            Assert.AreEqual("Low", GameType("GraphicsQuality").GetProperty("Current").GetValue(null).ToString());
            Component spawner = (Component)UnityEngine.Object.FindAnyObjectByType(GameType("MothSpawner"));
            Assert.IsNotNull(spawner, "MothSpawner");
            GameObject prefab = Field<GameObject>(spawner, "mothPrefab");
            Component visual = prefab.GetComponentInChildren(GameType("MothVisual"), true);
            Assert.IsNotNull(visual, "moth prefab uses MothVisual");
            Assert.AreEqual(1f, Shader.GetGlobalFloat("_LKMothLow"), "Low flag published to the moth shader");

            // The shader uses a plain sine on Low (glide 0): the angle must change from frame to frame.
            Type flap = GameType("MothFlap");
            MethodInfo angle = flap.GetMethod("Angle");
            float phase = Get<float>(visual, "Phase");
            float hz = Get<float>(visual, "FlapHz");
            float first = (float)angle.Invoke(null, new object[] { 0.01f, phase, hz, 0f });
            bool changed = false;
            for (int i = 1; i < 6; i++)
            {
                float next = (float)angle.Invoke(null, new object[] { 0.01f + i * 0.013f, phase, hz, 0f });
                changed |= Mathf.Abs(next - first) > 0.5f;
            }

            Assert.IsTrue(changed, "the wing angle changes over frames");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ShadeHemBreakupActiveOnLow()
        {
            yield return LoadIsland4();
            Component spawner = (Component)UnityEngine.Object.FindAnyObjectByType(GameType("ShadeSpawner"));
            Assert.IsNotNull(spawner, "ShadeSpawner");
            GameObject prefab = Field<GameObject>(spawner, "shadePrefab");
            Assert.IsNotNull(prefab.GetComponent(GameType("ShadeVisual")), "ShadeVisual on the shade prefab");
            Renderer body = prefab.transform.Find("Body").GetComponent<Renderer>();
            Material material = body.sharedMaterial;
            Assert.AreEqual("LanternKeeper/Shade", material.shader.name);
            Assert.Greater(material.GetFloat("_HemFrac"), 0.05f, "hem breakup fraction");
            Assert.AreEqual(1f, Shader.GetGlobalFloat("_LKShadeLow"), "Low flag published to the Shade shader");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LitBeaconGlowsWithEightMetreRingOnLow()
        {
            yield return LoadIsland4();
            Time.captureDeltaTime = 1f / 60f;
            Time.timeScale = 1f;
            Component[] beacons = (Component[])UnityEngine.Object.FindObjectsByType(GameType("Beacon"));
            Assert.Greater(beacons.Length, 0, "beacons on Island 4");
            Component beacon = null;
            for (int i = 0; i < beacons.Length && beacon == null; i++)
            {
                if (!Get<bool>(beacons[i], "IsLit"))
                {
                    beacon = beacons[i];
                }
            }

            Assert.IsNotNull(beacon, "an unlit beacon");
            Component lantern = (Component)UnityEngine.Object.FindAnyObjectByType(GameType("Lantern"));
            Assert.IsNotNull(lantern, "Lantern");
            Component manager = (Component)UnityEngine.Object.FindAnyObjectByType(GameType("GameManager"));
            // Island 4 opens paused behind its intro card; resume without marking the intro as seen.
            if (Get<bool>(manager, "IsPaused"))
            {
                manager.GetType().GetMethod("Resume").Invoke(manager, null);
            }
            Component visual = beacon.GetComponentInChildren(GameType("BeaconVisual"), true);
            Assert.IsNotNull(visual, "BeaconVisual");
            Assert.IsTrue((bool)GameType("Beacon").GetMethod("TryLight").Invoke(beacon, null), "TryLight");
            int guard = 0;
            while (Get<float>(visual, "MomentTime") < 1.3f && guard++ < 400)
            {
                yield return null;
            }

            yield return null;
            Assert.AreEqual(8f, Get<float>(visual, "RingRadius"), 0.01f, "ring radius");
            Assert.Greater(Get<float>(visual, "Glow"), 0f, "pane emission");
            Assert.AreEqual(1f, Shader.GetGlobalFloat("_LKMothLow"));
        }
    }
}
