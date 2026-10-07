using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
    // The beacon lighting moment on Island1. The game types are reached by reflection (this assembly can't reference Assembly-CSharp).
    public class BeaconLightMomentTest
    {
        readonly List<string> problems = new List<string>();
        float previousScale;
        float previousCaptureDelta;
        float safeAtLit = -1f;
        bool litFired;
        Delegate litHandler;
        EventInfo litEvent;

        static Type GameType(string name)
        {
            Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, name + " type not found");
            return type;
        }

        [SetUp]
        public void SetUp()
        {
            problems.Clear();
            safeAtLit = -1f;
            litFired = false;
            Application.logMessageReceived += OnLog;
            previousScale = Time.timeScale;
            previousCaptureDelta = Time.captureDeltaTime;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = previousScale;
            Time.captureDeltaTime = previousCaptureDelta;
            if (litEvent != null && litHandler != null)
            {
                litEvent.RemoveEventHandler(null, litHandler);
            }

            litEvent = null;
            litHandler = null;
        }

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                problems.Add("[" + type + "] " + message);
            }
        }

        // Runs for every Beacon.Lit: records SafeRadius at the moment the event fires.
        public void OnLit(object beacon)
        {
            litFired = true;
            safeAtLit = (float)GameType("Beacon").GetProperty("SafeRadius").GetValue(beacon);
        }

        IEnumerator LoadIsland1()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Island1");
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return null;
        }

        static Component NearestUnlitBeacon()
        {
            Type beaconType = GameType("Beacon");
            Component[] all = (Component[])UnityEngine.Object.FindObjectsByType(beaconType);
            Assert.Greater(all.Length, 0, "no beacons in Island1");
            return all[0];
        }

        static Component VisualOf(Component beacon)
        {
            Component visual = beacon.GetComponentInChildren(GameType("BeaconVisual"), true);
            Assert.IsNotNull(visual, "BeaconVisual under the beacon");
            return visual;
        }

        static T Get<T>(Component component, string property)
        {
            PropertyInfo info = component.GetType().GetProperty(property);
            Assert.IsNotNull(info, property);
            return (T)info.GetValue(component);
        }

        void Subscribe()
        {
            litEvent = GameType("Beacon").GetEvent("Lit");
            litHandler = Delegate.CreateDelegate(litEvent.EventHandlerType, this, GetType().GetMethod("OnLit"));
            litEvent.AddEventHandler(null, litHandler);
        }

        // Fixed 1/60 s per frame, so the moment time does not depend on how slowly the editor renders.
        void FixedSteps()
        {
            Time.captureDeltaTime = 1f / 60f;
            Time.timeScale = 1f;
        }

        IEnumerator LightIt(Component beacon)
        {
            Subscribe();
            FixedSteps();
            bool lit = (bool)GameType("Beacon").GetMethod("TryLight").Invoke(beacon, null);
            Assert.IsTrue(lit, "TryLight failed (no fuel?)");
            yield return null;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SafeOnSameFrameAsLit()
        {
            yield return LoadIsland1();
            Component beacon = NearestUnlitBeacon();
            Assert.AreEqual(0f, Get<float>(beacon, "SafeRadius"), "unlit beacon is not safe");
            yield return LightIt(beacon);
            Assert.IsTrue(litFired, "Beacon.Lit did not fire");
            Assert.AreEqual(8f, safeAtLit, 1e-4f, "SafeRadius at the moment Lit fired");
            Assert.AreEqual(8f, Get<float>(beacon, "SafeRadius"), 1e-4f);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LightFullBy1_2s()
        {
            yield return LoadIsland1();
            Component beacon = NearestUnlitBeacon();
            Component visual = VisualOf(beacon);
            Assert.AreEqual(-1f, Get<float>(visual, "MomentTime"), "no moment before lighting");
            yield return LightIt(beacon);
            Assert.IsTrue(Get<bool>(visual, "IsMomentPlaying"), "moment starts with Beacon.Lit");
            float early = -1f;
            int guard = 0;
            while (Get<float>(visual, "MomentTime") < 1.2f && guard++ < 400)
            {
                yield return null;
                if (early < 0f && Get<float>(visual, "MomentTime") >= 0.5f)
                {
                    early = Get<float>(visual, "LightFactor");
                }
            }

            Assert.Less(early, 0.9f, "the light is still ramping at 0.5 s");
            Assert.Greater(early, 0.1f);
            Assert.AreEqual(1f, Get<float>(visual, "LightFactor"), 1e-3f, "light is full by 1.2 s");
            Assert.Greater(Get<int>(visual, "EmbersEmitted"), 0, "embers rose");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator RingReaches8mBy1_2s()
        {
            yield return LoadIsland1();
            Component beacon = NearestUnlitBeacon();
            Component visual = VisualOf(beacon);
            yield return LightIt(beacon);
            int guard = 0;
            while (Get<float>(visual, "MomentTime") < 1.2f && guard++ < 400)
            {
                yield return null;
            }

            yield return null;
            Assert.AreEqual(8f, Get<float>(visual, "RingRadius"), 0.01f, "ring radius value");
            Transform ringTransform = beacon.transform.Find("SafeRing");
            Assert.IsNotNull(ringTransform, "SafeRing child");
            MeshRenderer ring = ringTransform.GetComponent<MeshRenderer>();
            MeshFilter ringFilter = ringTransform.GetComponent<MeshFilter>();
            Assert.IsTrue(ring.enabled, "ring visible");
            Vector3[] verts = ringFilter.sharedMesh.vertices;
            Vector3 centre = beacon.transform.position;
            float sum = 0f;
            for (int i = 0; i < verts.Length; i++)
            {
                sum += Vector2.Distance(new Vector2(verts[i].x, verts[i].z), new Vector2(centre.x, centre.z));
            }

            Assert.AreEqual(8f, sum / verts.Length, 0.05f, "mean ring radius (centre line)");
            Color[] colours = ringFilter.sharedMesh.colors;
            float maxAlpha = 0f;
            for (int i = 0; i < colours.Length; i++)
            {
                maxAlpha = Mathf.Max(maxAlpha, colours[i].a);
                Assert.GreaterOrEqual(colours[i].a, 0f);
            }

            Assert.Greater(maxAlpha, 0.5f, "some of the ring is visible on walkable ground");

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator AlreadyLitShowsSteadyState()
        {
            yield return LoadIsland1();
            Component beacon = NearestUnlitBeacon();
            Component visual = VisualOf(beacon);
            yield return LightIt(beacon);
            int guard = 0;
            while (Get<float>(visual, "MomentTime") < 1.5f && guard++ < 400)
            {
                yield return null;
            }

            GameObject host = beacon.gameObject;
            host.SetActive(false);
            host.SetActive(true);
            Assert.IsTrue((bool)GameType("Beacon").GetProperty("IsLit").GetValue(beacon), "still lit");
            Assert.IsFalse(Get<bool>(visual, "IsMomentPlaying"), "no moment replays on enable");
            Assert.AreEqual(-1f, Get<float>(visual, "MomentTime"), "no moment time on enable");
            Assert.AreEqual(8f, Get<float>(visual, "RingRadius"), 1e-4f, "ring at 8 m immediately");
            Assert.AreEqual(1f, Get<float>(visual, "LightFactor"), 1e-4f, "light steady immediately");
            ParticleSystem embers = Get<ParticleSystem>(visual, "Embers");
            Assert.IsFalse(embers.isEmitting, "no embers burst");
            Assert.AreEqual(0, embers.particleCount, "no embers");
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            Assert.AreEqual(-1f, Get<float>(visual, "MomentTime"), "still no moment after frames");
            Assert.AreEqual(0, embers.particleCount, "still no embers");
            Assert.Greater(Get<float>(visual, "Glow"), 0.5f, "panes glow");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
