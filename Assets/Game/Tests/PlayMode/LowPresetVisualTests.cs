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

        // Leave no Low behind for later PlayMode tests: restore the preset, republish the shader globals from it,
        // and reload a neutral scene so the lit beacon and any spawned creatures are gone.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.SetInt(GraphicsKey, savedGraphics);
            Time.timeScale = previousScale;
            Time.captureDeltaTime = previousCaptureDelta;
            GameType("MothVisual").GetMethod("PublishQuality").Invoke(null, null);
            GameType("ShadeVisual").GetMethod("PublishQuality").Invoke(null, null);
            float expected = savedGraphics == 0 ? 1f : 0f;
            AsyncOperation load = SceneManager.LoadSceneAsync("MainMenu");
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            // Scene objects can republish on enable; the globals must still match the restored preset.
            Assert.AreEqual(expected, Shader.GetGlobalFloat("_LKMothLow"), "_LKMothLow restored");
            Assert.AreEqual(expected, Shader.GetGlobalFloat("_LKShadeLow"), "_LKShadeLow restored");
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
        public IEnumerator MothWingFlapsOnLow()
        {
            yield return LoadIsland4();
            Assert.AreEqual("Low", GameType("GraphicsQuality").GetProperty("Current").GetValue(null).ToString());
            Component spawner = (Component)UnityEngine.Object.FindAnyObjectByType(GameType("MothSpawner"));
            Assert.IsNotNull(spawner, "MothSpawner");
            GameObject prefab = Field<GameObject>(spawner, "mothPrefab");
            GameObject moth = UnityEngine.Object.Instantiate(prefab, spawner.transform.position + Vector3.up * 3f, Quaternion.identity);
            Component visual = moth.GetComponentInChildren(GameType("MothVisual"), true);
            Assert.IsNotNull(visual, "moth prefab uses MothVisual");

            // Island 4 opens paused behind its intro card (time frozen, so the shader time is too); resume it.
            Time.timeScale = 1f;
            Component manager = (Component)UnityEngine.Object.FindAnyObjectByType(GameType("GameManager"));
            if (Get<bool>(manager, "IsPaused"))
            {
                manager.GetType().GetMethod("Resume").Invoke(manager, null);
            }

            // CPU-observable signal: the per-instance property block the shader reads (_FlapHz > 0), plus MothVisual.CurrentAngle,
            // a CPU mirror of the shader's wing angle on the shader's time base (Low: plain sine, no glide). It must sweep over real frames.
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < 12; i++)
            {
                yield return null;
                float angle = Get<float>(visual, "CurrentAngle");
                min = Mathf.Min(min, angle);
                max = Mathf.Max(max, angle);
            }

            Assert.AreEqual(1f, Shader.GetGlobalFloat("_LKMothLow"), "Low flag published to the moth shader");
            Renderer wing = Get<Renderer>(visual, "WingRenderer");
            Assert.IsNotNull(wing, "wing renderer");
            Assert.AreEqual("LanternKeeper/MothWing", wing.sharedMaterial.shader.name);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            wing.GetPropertyBlock(block);
            float hz = block.GetFloat("_FlapHz");
            Assert.Greater(hz, 0f, "_FlapHz in the property block");
            Assert.AreEqual(Get<float>(visual, "FlapHz"), hz, 0.0001f, "block matches the published flap rate");
            Assert.AreEqual(Get<float>(visual, "Phase"), block.GetFloat("_Phase"), 0.0001f, "block matches the published phase");
            Assert.Greater(max - min, 10f, "the wing angle sweeps across frames on Low");
            UnityEngine.Object.Destroy(moth);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ShadeUsesLowPathOnLow()
        {
            yield return LoadIsland4();
            Component spawner = (Component)UnityEngine.Object.FindAnyObjectByType(GameType("ShadeSpawner"));
            Assert.IsNotNull(spawner, "ShadeSpawner");
            GameObject prefab = Field<GameObject>(spawner, "shadePrefab");
            Assert.IsNotNull(prefab.GetComponent(GameType("ShadeVisual")), "ShadeVisual on the shade prefab");
            ParticleSystem prefabTrail = prefab.transform.Find("Smoke").GetComponent<ParticleSystem>();
            float baseLifetime = prefabTrail.main.startLifetime.constant;
            GameObject shade = UnityEngine.Object.Instantiate(prefab, spawner.transform.position + Vector3.up * 2f, Quaternion.identity);
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Renderer body = shade.transform.Find("Body").GetComponent<Renderer>();
            Assert.AreEqual("LanternKeeper/Shade", body.sharedMaterial.shader.name);
            // The hem breakup stays on (the cutout fraction is in the material); Low only drops noise octaves, selected by the global.
            Assert.Greater(body.sharedMaterial.GetFloat("_HemFrac"), 0.05f, "hem breakup fraction");
            Assert.AreEqual(1f, Shader.GetGlobalFloat("_LKShadeLow"), "Low flag published to the Shade shader");
            // A Low-only runtime effect on a live Shade: the smoke trail is thinned.
            ParticleSystem trail = shade.transform.Find("Smoke").GetComponent<ParticleSystem>();
            Assert.AreEqual(baseLifetime * 0.5f, trail.main.startLifetime.constant, 0.001f, "trail lifetime halved on Low");
            UnityEngine.Object.Destroy(shade);
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
