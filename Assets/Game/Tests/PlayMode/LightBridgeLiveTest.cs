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
    // Island 5 light bridges, the summit gate and the great lamp, live. The game types are reached by reflection (this assembly
    // can't reference Assembly-CSharp). Every frame advances the game by the same step (captureDeltaTime), so timings do not depend on
    // how slowly the editor renders.
    public class LightBridgeLiveTest
    {
        const float Frame = 1f / 60f;
        static readonly string[] IntKeys =
        {
            "LanternKeeperIntro_island1", "LanternKeeperIntro_island5", "LanternKeeperWon_island1", "LanternKeeperWon_island5"
        };
        static readonly string[] FloatKeys = { "LanternKeeperBest_island1", "LanternKeeperBest_island5" };

        readonly Dictionary<string, int> savedInts = new Dictionary<string, int>();
        readonly Dictionary<string, float> savedFloats = new Dictionary<string, float>();
        float previousCaptureDelta;
        float previousMaxDelta;
        float previousScale;

        static Type GameType(string name)
        {
            Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, name + " type not found");
            return type;
        }

        static UnityEngine.Object Find(string typeName)
        {
            return UnityEngine.Object.FindAnyObjectByType(GameType(typeName));
        }

        static object Get(object target, string member)
        {
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                return property.GetValue(target);
            }

            FieldInfo field = type.GetField(member, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, type.Name + "." + member + " not found");
            return field.GetValue(target);
        }

        static object Call(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(info, target.GetType().Name + "." + method + " not found");
            return info.Invoke(target, args);
        }

        static object CallStatic(string typeName, string method, params object[] args)
        {
            MethodInfo info = GameType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(info, typeName + "." + method + " not found");
            return info.Invoke(null, args);
        }

        static object StaticProperty(string typeName, string property)
        {
            return GameType(typeName).GetProperty(property, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }

        [SetUp]
        public void SetUp()
        {
            savedInts.Clear();
            savedFloats.Clear();
            for (int i = 0; i < IntKeys.Length; i++)
            {
                savedInts[IntKeys[i]] = PlayerPrefs.HasKey(IntKeys[i]) ? PlayerPrefs.GetInt(IntKeys[i]) : -1;
            }

            for (int i = 0; i < FloatKeys.Length; i++)
            {
                savedFloats[FloatKeys[i]] = PlayerPrefs.HasKey(FloatKeys[i]) ? PlayerPrefs.GetFloat(FloatKeys[i]) : float.NaN;
            }

            // A first-visit intro card pauses the game.
            PlayerPrefs.SetInt("LanternKeeperIntro_island1", 1);
            PlayerPrefs.SetInt("LanternKeeperIntro_island5", 1);
            previousCaptureDelta = Time.captureDeltaTime;
            previousMaxDelta = Time.maximumDeltaTime;
            previousScale = Time.timeScale;
            Time.captureDeltaTime = Frame;
            Time.maximumDeltaTime = 0.05f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (KeyValuePair<string, int> pair in savedInts)
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

            foreach (KeyValuePair<string, float> pair in savedFloats)
            {
                if (float.IsNaN(pair.Value))
                {
                    PlayerPrefs.DeleteKey(pair.Key);
                }
                else
                {
                    PlayerPrefs.SetFloat(pair.Key, pair.Value);
                }
            }

            PlayerPrefs.Save();
            Time.captureDeltaTime = previousCaptureDelta;
            Time.maximumDeltaTime = previousMaxDelta;
            Time.timeScale = previousScale;
        }

        static IEnumerator Load(string scene)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(scene);
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return null;
        }

        // Game seconds, counted from the frame step.
        static IEnumerator Wait(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        static Component Beacon(int index)
        {
            Transform root = GameObject.Find("Beacons").transform;
            return root.GetChild(index).GetComponent(GameType("Beacon"));
        }

        static Component Bridge(int gap)
        {
            return GameObject.Find("LightBridge_Gap" + gap).GetComponent(GameType("LightBridge"));
        }

        static void Refuel()
        {
            Component lantern = (Component)Find("Lantern");
            Call(lantern, "AddFuel", 100f);
        }

        static bool LightBeacon(int index)
        {
            Refuel();
            return (bool)Call(Beacon(index), "TryLight");
        }

        static void Teleport(Vector3 position)
        {
            Component player = (Component)Find("PlayerController");
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
        }

        static Vector3 Mid(Component bridge)
        {
            return (Vector3)Get(bridge, "MidPoint");
        }

        // Whether the downward ray at the middle of the bridge hits a collider of that bridge.
        static bool MidSpanHitsBridge(Component bridge)
        {
            Vector3 origin = Mid(bridge) + Vector3.up * 3f;
            RaycastHit hit;
            return Physics.Raycast(origin, Vector3.down, out hit, 6f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.transform.IsChildOf(bridge.transform);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator LightingNearBeaconBuildsWalkableBridge()
        {
            yield return Load("Island5");
            Component bridge = Bridge(0);
            Assert.AreEqual(0, (int)StaticProperty("LightBridge", "BuiltCount"), "no bridge is built at the start");
            Assert.IsFalse((bool)Get(bridge, "Walkable"));
            Assert.IsTrue(LightBeacon(1), "beacon 1 lights");
            float elapsed = 0f;
            while (!(bool)Get(bridge, "Walkable") && elapsed < 3.5f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Assert.IsTrue((bool)Get(bridge, "Walkable"), "walkable after " + elapsed.ToString("F2") + " s");
            Assert.LessOrEqual(elapsed, 2.5f + 0.1f, "walkable within 2.5 s of lighting");
            Assert.AreEqual(1, (int)StaticProperty("LightBridge", "BuiltCount"));
            Assert.IsTrue(MidSpanHitsBridge(bridge), "a downward ray at mid-span hits the bridge collider");
            Assert.AreEqual(1f, (float)Get(bridge, "Progress01"), 1e-4f);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator CannotStepOntoGrowingBridge()
        {
            yield return Load("Island5");
            Component bridge = Bridge(0);
            Assert.IsTrue(LightBeacon(1));
            yield return Wait(1.5f);
            Assert.IsFalse((bool)Get(bridge, "Walkable"), "still growing at 1.5 s");
            Assert.IsFalse(MidSpanHitsBridge(bridge), "the mid-span ray hits nothing of the bridge before it is complete");
            Collider[] colliders = (Collider[])Get(bridge, "Colliders");
            Assert.Greater(colliders.Length, 0);
            for (int i = 0; i < colliders.Length; i++)
            {
                Assert.IsFalse(colliders[i].enabled, "collider " + colliders[i].name + " is off while growing");
            }

            float progress = (float)Get(bridge, "Progress01");
            Assert.Greater(progress, 0f);
            Assert.Less(progress, 1f);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator BridgeBlocksShadeAndIsSafe()
        {
            yield return Load("Island5");
            Component bridge = Bridge(0);
            Assert.IsTrue(LightBeacon(1));
            yield return Wait(2.8f);
            Assert.IsTrue((bool)Get(bridge, "Walkable"), "bridge walkable");

            Vector3 start = (Vector3)Get(bridge, "StartPoint");
            Vector3 end = (Vector3)Get(bridge, "EndPoint");
            Vector3 mid = Mid(bridge);
            Component lantern = (Component)Find("Lantern");
            Teleport(mid + Vector3.up * 0.3f);
            yield return Wait(1.0f);
            Refuel();
            Assert.IsTrue((bool)Get(lantern, "InSafeLight"), "the keeper on the bridge is in safe light");

            // A Shade starts just short of the near end and is steered at the keeper (it always chases the player).
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Gameplay/Shade.prefab");
            Assert.IsNotNull(prefab);
            Vector3 back = (start - end);
            back.y = 0f;
            Vector3 spawn = start + back.normalized * 3.5f;
            RaycastHit ground;
            if (Physics.Raycast(spawn + Vector3.up * 5f, Vector3.down, out ground, 20f))
            {
                spawn = ground.point;
            }

            GameObject shade = UnityEngine.Object.Instantiate(prefab, spawn, Quaternion.identity);
            float closest = float.MaxValue;
            float elapsed = 0f;
            while (elapsed < 5f)
            {
                yield return null;
                elapsed += Time.deltaTime;
                Refuel();
                Teleport(mid + Vector3.up * 0.3f);
                Assert.IsFalse((bool)Call(bridge, "ZoneContains", shade.transform.position),
                    "the Shade entered the bridge zone at " + shade.transform.position.ToString("F1") + " after " + elapsed.ToString("F1") + " s");
                closest = Mathf.Min(closest, Vector3.Distance(shade.transform.position, mid));
            }

            Assert.Greater(closest, 2f, "the Shade never got close to the keeper on the bridge");
            Assert.AreEqual(0, (int)Get(lantern, "MothsDraining"), "no moth drains on the bridge");
            Assert.IsTrue((bool)Get(lantern, "InSafeLight"), "still safe");
            UnityEngine.Object.Destroy(shade);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator SummitSpanWaitsForAllEight()
        {
            yield return Load("Island5");
            Component manager = (Component)Find("GameManager");
            Assert.IsTrue((bool)Get(manager, "DeferWinToGreatLamp"), "Island 5 defers its win to the great lamp");
            Component gate = (Component)Find("SummitGate");
            Component lamp = (Component)Find("GreatLamp");
            Component span = Bridge(3);
            int beacons = GameObject.Find("Beacons").transform.childCount;
            Assert.AreEqual(8, beacons);

            for (int i = 0; i < 7; i++)
            {
                Assert.IsTrue(LightBeacon(i), "beacon " + i);
            }

            yield return Wait(4f);
            Assert.AreEqual(7, (int)Get(gate, "LitCount"));
            Assert.IsFalse((bool)Get(gate, "Open"));
            Assert.IsFalse((bool)Get(span, "Triggered"), "the summit span is not started with 7 beacons");
            Assert.IsFalse((bool)Get(span, "Walkable"));
            Call(lamp, "Light");
            Assert.IsFalse((bool)Get(lamp, "Lit"), "the lamp is refused with 7 beacons");
            Assert.IsFalse((bool)Get(manager, "IsRoundOver"));

            Assert.IsTrue(LightBeacon(7), "the 8th beacon");
            Assert.IsTrue((bool)Get(gate, "Open"), "the gate opens");
            Assert.IsFalse((bool)Get(manager, "IsRoundOver"), "lighting every beacon does not win on Island 5");
            yield return Wait(1.0f);
            Assert.IsFalse((bool)Get(span, "Walkable"), "the columns are still rising");
            yield return Wait(BridgeSeconds());
            Assert.IsTrue((bool)Get(span, "Walkable"), "the summit span is built after the columns and the grow");
            Assert.IsTrue(MidSpanHitsBridge(span), "summit mid-span ray");
            Assert.IsFalse((bool)Get(manager, "Won"));

            // The Island 5 finale runs 52 s on unscaled time; the test speed makes it 6.5 s.
            GameType("FinaleSequence").GetField("TestSpeed", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 8f);
            Call(lamp, "Light");
            Assert.IsTrue((bool)Get(lamp, "Lit"), "lamp lit after Light()");
            float waited = 0f;
            while (!(bool)Get(manager, "Won") && waited < 40f)
            {
                yield return null;
                waited += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Frame;
                Refuel();
            }

            Assert.IsTrue((bool)Get(manager, "Won"), "lighting the great lamp wins");
        }

        static float BridgeSeconds()
        {
            return 1.2f + 1.5f + 0.4f;
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator OtherIslandsStillWinImmediately()
        {
            yield return Load("Island1");
            Component manager = (Component)Find("GameManager");
            Assert.IsFalse((bool)Get(manager, "DeferWinToGreatLamp"));
            Assert.IsNull(Find("SummitGate"));
            int count = (int)Get(manager, "BeaconsToWin");
            Transform root = GameObject.Find("Beacons").transform;
            for (int i = 0; i < count; i++)
            {
                Refuel();
                Assert.IsTrue((bool)Call(root.GetChild(i).GetComponent(GameType("Beacon")), "TryLight"), "beacon " + i);
                if (i < count - 1)
                {
                    yield return null;
                }
            }

            yield return null;
            Assert.IsTrue((bool)Get(manager, "IsRoundOver"), "the last beacon wins on Island 1 as before");
            float waited = 0f;
            while (!(bool)Get(manager, "Won") && waited < 40f)
            {
                yield return null;
                waited += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Frame;
            }

            Assert.IsTrue((bool)Get(manager, "Won"));
        }

        static IEnumerator BuildBridge0()
        {
            yield return Load("Island5");
            Assert.IsTrue(LightBeacon(1));
            yield return Wait(2.8f);
            Assert.IsTrue((bool)Get(Bridge(0), "Walkable"), "bridge 0 built");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator SideJumpOffBridgeIsBlocked()
        {
            yield return BuildBridge0();
            Component bridge = Bridge(0);
            Vector3[] points = (Vector3[])Get(bridge, "Points");
            Vector3 a = points[0];
            Vector3 b = points[points.Length - 1];
            float halfWidth = (float)Get(bridge, "Width") * 0.5f;
            Vector3 along = b - a;
            along.y = 0f;
            along.Normalize();
            Vector2 side = new Vector2(-along.z, along.x);
            Teleport(Mid(bridge) + Vector3.up * 0.3f);
            yield return Wait(0.5f);
            SetStatic("CameraFollow", "ExternalYaw", 0f);
            SetStatic("CameraFollow", "UseExternalYaw", true);
            float highest = 0f;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Teleport(Mid(bridge) + Vector3.up * 0.3f);
                yield return Wait(0.3f);
                float elapsed = 0f;
                while (elapsed < 1.6f)
                {
                    Refuel();
                    SetStatic("PlayerController", "ExternalMove", side * sign);
                    SetStatic("PlayerController", "ExternalJump", true);
                    yield return null;
                    elapsed += Time.deltaTime;
                    Vector3 p = ((Component)Find("PlayerController")).transform.position;
                    float t = Mathf.Clamp01(Vector2.Dot(new Vector2(p.x - a.x, p.z - a.z), new Vector2(along.x, along.z)) / new Vector2(b.x - a.x, b.z - a.z).magnitude);
                    Vector3 onLine = Vector3.Lerp(a, b, t);
                    float lateral = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(onLine.x, onLine.z));
                    highest = Mathf.Max(highest, lateral);
                    Assert.LessOrEqual(lateral, halfWidth + 0.35f, "the keeper left the deck sideways (side " + sign + ", " + elapsed.ToString("F2") + " s, lateral " + lateral.ToString("F2") + ")");
                    Assert.GreaterOrEqual(p.y, onLine.y - 0.3f, "the keeper fell below the deck");
                }
            }

            SetStatic("PlayerController", "ExternalMove", Vector2.zero);
            SetStatic("PlayerController", "ExternalJump", false);
            SetStatic("CameraFollow", "UseExternalYaw", false);
            Assert.Greater(highest, halfWidth - 0.6f, "the keeper did reach the rail while jumping sideways");
        }

        static void SetStatic(string typeName, string field, object value)
        {
            FieldInfo info = GameType(typeName).GetField(field, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(info, typeName + "." + field);
            info.SetValue(null, value);
        }

        static string DescribeMoths()
        {
            string text = "";
            foreach (UnityEngine.Object found in UnityEngine.Object.FindObjectsByType(GameType("Moth"), FindObjectsSortMode.None))
            {
                Component moth = (Component)found;
                text += "[moth " + moth.name + " at " + moth.transform.position.ToString("F1") + "] ";
            }

            return text;
        }

        static object PrivateCall(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info, target.GetType().Name + "." + method);
            return info.Invoke(target, args);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator ShadeInsideBridgeZoneIsPushedOutAndOneBelowIsLeftAlone()
        {
            yield return BuildBridge0();
            Component bridge = Bridge(0);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Gameplay/Shade.prefab");
            Vector3 mid = Mid(bridge);
            GameObject shade = UnityEngine.Object.Instantiate(prefab, mid + Vector3.up * 1.0f, Quaternion.identity);
            yield return null;
            Component shadeComponent = shade.GetComponent(GameType("Shade"));
            Vector3 inside = mid + Vector3.up * 1.0f;
            Assert.IsTrue((bool)Call(bridge, "ZoneContains", inside), "the test position is inside the zone");
            Vector3 pushed = (Vector3)PrivateCall(shadeComponent, "PushOutOfSafe", inside);
            Assert.IsFalse((bool)Call(bridge, "ZoneContains", pushed), "the Shade is pushed out of the bridge zone, ended at " + pushed.ToString("F1"));
            Assert.IsTrue((bool)PrivateCall(shadeComponent, "InsideSafe", inside), "InsideSafe sees the bridge");
            Vector3 below = mid + Vector3.down * 5f;
            Assert.AreEqual(below, (Vector3)PrivateCall(shadeComponent, "PushOutOfSafe", below), "a Shade 5 m below the deck is left alone");
            Assert.IsFalse((bool)PrivateCall(shadeComponent, "InsideSafe", below));
            UnityEngine.Object.Destroy(shade);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator MothNextToKeeperOnBridgeDoesNotDrain()
        {
            yield return BuildBridge0();
            Component bridge = Bridge(0);
            Component lantern = (Component)Find("Lantern");
            Vector3 mid = Mid(bridge);
            Teleport(mid + Vector3.up * 0.3f);
            yield return Wait(0.5f);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Gameplay/Moth.prefab");
            GameObject moth = UnityEngine.Object.Instantiate(prefab, mid + Vector3.up * 1.3f + Vector3.right * 0.8f, Quaternion.identity);
            Component mothComponent = moth.GetComponent(GameType("Moth"));
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                Refuel();
                yield return null;
                elapsed += Time.deltaTime;
                Assert.AreEqual(0, (int)Get(lantern, "MothsDraining"), "no moth drains the keeper on the bridge (" + elapsed.ToString("F2") + " s). " + DescribeMoths());
                Assert.IsFalse((bool)Call(bridge, "ZoneContains", moth.transform.position), "the moth stays out of the bridge zone, at " + moth.transform.position.ToString("F1"));
            }

            Assert.IsNotNull(mothComponent);
            UnityEngine.Object.Destroy(moth);
        }
    }
}
