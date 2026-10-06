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
    // Every beacon must be reachable on foot. The keeper starts at the spawn and follows each trail (the TrailGuide
    // transforms the builder stores in the scene) with PlayerController.ExternalMove, then walks on to the beacon.
    // A failure names the island and the beacon. The game types are reached by reflection (this assembly can't reference Assembly-CSharp).
    public class BeaconReachabilityTest
    {
        const float ArriveRadius = 2f;
        const float WaypointRadius = 0.7f;
        const float PlaybackScale = 2f;
        const float MoveSpeed = 6f;

        static readonly string[] Islands = { "Island1", "Island2", "Island3", "Island4" };

        readonly List<string> problems = new List<string>();
        readonly List<string> blockers = new List<string>();
        float previousScale;
        float previousMaxDelta;
        bool yawWasExternal;

        static Type GameType(string name)
        {
            Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, name + " type not found");
            return type;
        }

        static void SetStatic(string typeName, string field, object value)
        {
            Type type = GameType(typeName);
            FieldInfo info = type.GetField(field, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(info, typeName + "." + field + " not found");
            info.SetValue(null, value);
        }

        [SetUp]
        public void SetUp()
        {
            problems.Clear();
            blockers.Clear();
            Application.logMessageReceived += OnLog;
            previousScale = Time.timeScale;
            previousMaxDelta = Time.maximumDeltaTime;
            yawWasExternal = (bool)GameType("CameraFollow").GetField("UseExternalYaw", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = previousScale;
            Time.maximumDeltaTime = previousMaxDelta;
            SetStatic("PlayerController", "ExternalMove", Vector2.zero);
            SetStatic("CameraFollow", "UseExternalYaw", yawWasExternal);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator EveryBeaconReachable([ValueSource(nameof(Islands))] string island)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(island);
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return null;

            Type playerType = GameType("PlayerController");
            Component player = (Component)UnityEngine.Object.FindFirstObjectByType(playerType);
            Assert.IsNotNull(player, island + ": PlayerController missing");
            CharacterController controller = player.GetComponent<CharacterController>();

            GameObject beaconsRoot = GameObject.Find("Beacons");
            GameObject guide = GameObject.Find("TrailGuide");
            Assert.IsNotNull(beaconsRoot, island + ": Beacons folder missing");
            Assert.IsNotNull(guide, island + ": TrailGuide missing (rebuild the level)");
            Assert.Greater(beaconsRoot.transform.childCount, 0, island + ": no beacons");
            Assert.AreEqual(beaconsRoot.transform.childCount, guide.transform.childCount, island + ": one trail per beacon");

            SetStatic("CameraFollow", "ExternalYaw", 0f);
            SetStatic("CameraFollow", "UseExternalYaw", true);
            Time.maximumDeltaTime = 0.05f;
            Time.timeScale = PlaybackScale;

            Vector3 spawn = player.transform.position;
            for (int b = 0; b < beaconsRoot.transform.childCount; b++)
            {
                Transform beacon = beaconsRoot.transform.GetChild(b);
                Transform trail = guide.transform.GetChild(b);
                List<Vector3> route = new List<Vector3>();
                foreach (Transform point in trail)
                {
                    route.Add(point.position);
                }

                route.Add(beacon.position);
                float length = 0f;
                for (int i = 1; i < route.Count; i++)
                {
                    length += Vector3.Distance(route[i - 1], route[i]);
                }

                // Back to the spawn for every beacon.
                controller.enabled = false;
                player.transform.position = spawn;
                controller.enabled = true;
                SetStatic("PlayerController", "ExternalMove", Vector2.zero);
                yield return null;
                yield return null;

                float limit = length / (MoveSpeed * 0.5f) + 10f;
                float elapsed = 0f;
                int waypoint = 1;
                bool arrived = false;
                float closest = float.MaxValue;
                Vector3 stuckFrom = player.transform.position;
                float stuckSince = 0f;
                float lastJump = -10f;
                int jumps = 0;
                while (elapsed < limit)
                {
                    Vector3 position = player.transform.position;
                    Vector2 flat = new Vector2(position.x, position.z);
                    float toBeacon = Vector2.Distance(flat, new Vector2(beacon.position.x, beacon.position.z));
                    closest = Mathf.Min(closest, toBeacon);
                    if (toBeacon < ArriveRadius)
                    {
                        arrived = true;
                        break;
                    }

                    while (waypoint < route.Count - 1 && Vector2.Distance(flat, new Vector2(route[waypoint].x, route[waypoint].z)) < WaypointRadius)
                    {
                        waypoint++;
                    }

                    Vector3 target = route[waypoint];
                    Vector2 direction = new Vector2(target.x - position.x, target.z - position.z);
                    SetStatic("PlayerController", "ExternalMove", direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero);
                    yield return null;
                    elapsed += Time.deltaTime;
                    if ((player.transform.position - stuckFrom).magnitude > 0.3f)
                    {
                        stuckFrom = player.transform.position;
                        stuckSince = elapsed;
                    }
                    else if (elapsed - stuckSince > 0.6f)
                    {
                        // A terrain step above the 0.4 m step offset takes a jump, which the player has. A prop in the way is a blocker.
                        if (elapsed - lastJump > 0.8f)
                        {
                            lastJump = elapsed;
                            jumps++;
                            string blocker = PropInTheWay(player.transform.position, direction.normalized, beacon);
                            if (blocker != null)
                            {
                                blockers.Add(island + ": beacon " + (b + 1) + " blocked by " + blocker + " at " + player.transform.position.ToString("F1"));
                            }

                            SetStatic("PlayerController", "ExternalJump", true);
                        }

                        if (elapsed - stuckSince > 4f)
                        {
                            break;
                        }
                    }
                }

                SetStatic("PlayerController", "ExternalMove", Vector2.zero);
                Vector3 end = player.transform.position;
                string state = arrived ? "" : ". Game state: " + GameState();
                Assert.IsTrue(arrived, island + ": beacon " + (b + 1) + " (" + beacon.position.ToString("F1") + ") not reached. Keeper stopped at "
                    + end.ToString("F1") + ", closest " + closest.ToString("F1") + " m, waypoint " + waypoint + "/" + (route.Count - 1) + ", " + elapsed.ToString("F1") + " s of " + limit.ToString("F1") + " s" + state);
                Debug.Log("BeaconReachability " + island + " beacon " + (b + 1) + " reached in " + elapsed.ToString("F1") + " s (path " + length.ToString("F1") + " m, " + jumps + " jumps)");
            }

            Assert.IsEmpty(blockers, string.Join(" | ", blockers));
            Assert.IsEmpty(problems, island + ": errors were logged: " + string.Join(" | ", problems));
        }

        // A non-terrain collider (prop or rock; the hidden-path stones are the way, not an obstacle) just ahead of the keeper, or null. The beacon itself and the keeper don't count.
        static string PropInTheWay(Vector3 position, Vector2 direction, Transform beacon)
        {
            Vector3 ahead = position + new Vector3(direction.x, 0f, direction.y) * 0.6f + Vector3.up * 0.9f;
            Collider[] hits = Physics.OverlapSphere(ahead, 0.7f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                bool stone = hits[i].transform.parent != null && hits[i].transform.parent.name == "HiddenPaths";
                if (hits[i] is TerrainCollider || stone || hits[i].transform.IsChildOf(beacon) || hits[i].GetComponentInParent<CharacterController>() != null)
                {
                    continue;
                }

                return hits[i].transform.root == hits[i].transform ? hits[i].name : hits[i].transform.parent.name + "/" + hits[i].name;
            }

            return null;
        }

        // Pause, lock, dying and round-over flags of the GameManager, to tell a blocked path from a stopped game.
        static string GameState()
        {
            Type type = GameType("GameManager");
            object manager = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            if (manager == null)
            {
                return "no GameManager";
            }

            string[] flags = { "IsPaused", "ControlsLocked", "IsDying", "IsRoundOver" };
            string text = "";
            for (int i = 0; i < flags.Length; i++)
            {
                PropertyInfo info = type.GetProperty(flags[i], BindingFlags.Public | BindingFlags.Instance);
                text += flags[i] + "=" + (info != null ? info.GetValue(manager) : "?") + " ";
            }

            return text;
        }

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                problems.Add("[" + type + "] " + message);
            }
        }
    }
}
