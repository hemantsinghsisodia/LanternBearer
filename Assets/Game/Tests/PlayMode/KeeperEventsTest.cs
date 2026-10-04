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
    // Drives the keeper's Actions layer and weights through the real game events.
    // This assembly can't reference Assembly-CSharp, so the game types are reached by reflection.
    public class KeeperEventsTest
    {
        const float InteractLength = 1.5833f;

        // Mirrors GameSettings.IntroKey; this assembly can't reference Assembly-CSharp.
        const string IntroKey = "LanternKeeperIntro_island4";

        readonly List<string> problems = new List<string>();
        bool hadIntroKey;
        int previousIntroValue;

        [SetUp]
        public void SetUp()
        {
            problems.Clear();
            Application.logMessageReceived += OnLog;
            hadIntroKey = PlayerPrefs.HasKey(IntroKey);
            previousIntroValue = PlayerPrefs.GetInt(IntroKey, 0);
            PlayerPrefs.SetInt(IntroKey, 1);
        }

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

        // Informational logs and warnings are ignored: the game prints diagnostics. Errors and exceptions fail the test.
        void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                problems.Add("[" + type + "] " + message);
            }
        }

        [UnityTest]
        public IEnumerator ActionsLayerFollowsGameEvents()
        {
            SceneManager.LoadScene("Island4");
            yield return null;
            yield return null;

            MonoBehaviour keeper = FindOne("KeeperAnimator");
            Assert.IsNotNull(keeper, "KeeperAnimator missing on Island4");
            Animator animator = keeper.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "Keeper Animator missing");
            int actions = animator.GetLayerIndex("Actions");
            Assert.GreaterOrEqual(actions, 0, "Keeper.controller missing layer Actions");
            Assert.GreaterOrEqual(animator.GetLayerIndex("LanternArm"), 0, "Keeper.controller missing layer LanternArm");
            Assert.GreaterOrEqual(animator.GetLayerIndex("Lean"), 0, "Keeper.controller missing layer Lean");

            // 1. A steal staggers within 3 frames.
            RaiseStole();
            bool staggered = false;
            for (int i = 0; i < 3 && !staggered; i++)
            {
                yield return null;
                staggered = InState(animator, actions, "Stagger");
            }

            Assert.IsTrue(staggered, "Stagger not entered within 3 frames of Shade.Stole");

            // Let the stagger finish so the next gesture starts from Empty.
            yield return WaitForState(animator, actions, "Empty", 2f);
            Assert.IsTrue(Settled(animator, actions, "Empty"), "Actions layer did not return to Empty after Stagger");

            // 2. Lighting a beacon plays Light and lowers the arm weight, which recovers afterwards.
            MonoBehaviour beacon = FindUnlitBeacon();
            Assert.IsNotNull(beacon, "No unlit Beacon on Island4");
            MethodInfo tryLight = beacon.GetType().GetMethod("TryLight", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsTrue((bool)tryLight.Invoke(beacon, null), "TryLight refused");
            bool lightPlayed = false;
            for (int i = 0; i < 6 && !lightPlayed; i++)
            {
                yield return null;
                lightPlayed = InState(animator, actions, "Light");
            }

            Assert.IsTrue(lightPlayed, "Light state not entered after Beacon.TryLight. " + Describe(animator, actions, keeper));
            float armDropped = 1f;
            for (int i = 0; i < 30 && armDropped >= 0.5f; i++)
            {
                yield return null;
                armDropped = ArmWeight(keeper);
            }

            Assert.Less(armDropped, 0.5f, "ArmWeight did not drop during the Light gesture");

            // 3. A paused game freezes both weights (and the animator).
            float armBefore = ArmWeight(keeper);
            float leanBefore = LeanWeight(keeper);
            Time.timeScale = 0f;
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(armBefore, ArmWeight(keeper), 0.0001f, "ArmWeight moved while paused");
            Assert.AreEqual(leanBefore, LeanWeight(keeper), 0.0001f, "LeanWeight moved while paused");
            Time.timeScale = 1f;

            float waited = 0f;
            while (waited < InteractLength + 0.5f && ArmWeight(keeper) < 0.99f)
            {
                yield return null;
                waited += Time.deltaTime;
            }

            Assert.GreaterOrEqual(ArmWeight(keeper), 0.99f, "ArmWeight did not return to 1 after the Light gesture");
            yield return WaitForState(animator, actions, "Empty", 2f);

            // 4. A water rescue plays ShakeOff.
            MonoBehaviour hazard = FindOne("WaterHazard");
            Assert.IsNotNull(hazard, "WaterHazard missing on Island4");
            MonoBehaviour player = FindOne("PlayerController");
            CharacterController body = player.GetComponent<CharacterController>();
            float surface = (float)FindType("WaterHazard").GetProperty("SurfaceY", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            body.enabled = false;
            Vector3 submerged = player.transform.position;
            submerged.y = surface - 1f;
            player.transform.position = submerged;
            body.enabled = true;
            bool shook = false;
            for (int i = 0; i < 180 && !shook; i++)
            {
                yield return null;
                shook = InState(animator, actions, "ShakeOff");
            }

            Assert.IsTrue(shook, "ShakeOff not entered after a water rescue");
            yield return WaitForState(animator, actions, "Empty", 3f);

            // 5. Death wins over an overlapping gesture and a later steal.
            MonoBehaviour second = FindUnlitBeacon();
            if (second != null)
            {
                tryLight.Invoke(second, null);
                yield return null;
                yield return null;
            }

            RaiseStole();
            yield return null;
            RaiseDeathStarted();
            bool died = false;
            for (int i = 0; i < 120 && !died; i++)
            {
                yield return null;
                died = InState(animator, actions, "Die");
            }

            Assert.IsTrue(died, "Die not entered after DeathStarted");
            RaiseStole();
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            Assert.IsTrue(InState(animator, actions, "Die"), "A steal after death left the Die state");
            Assert.IsEmpty(problems, "Errors were logged: " + string.Join(" | ", problems));
        }

        [UnityTest]
        public IEnumerator ReloadedSceneLeavesNoDeadSubscribers()
        {
            SceneManager.LoadScene("Island4");
            yield return null;
            yield return null;
            SceneManager.LoadScene("Island4");
            yield return null;
            yield return null;

            // 6. After a reload no destroyed keeper may still be subscribed to the static events.
            Delegate stole = StaticEvent("Shade", "Stole");
            if (stole != null)
            {
                foreach (Delegate d in stole.GetInvocationList())
                {
                    UnityEngine.Object target = d.Target as UnityEngine.Object;
                    if (d.Target is UnityEngine.Object)
                    {
                        Assert.IsTrue(target != null, "Shade.Stole still calls a destroyed object");
                    }
                }
            }

            Delegate lit = StaticEvent("Beacon", "Lit");
            if (lit != null)
            {
                foreach (Delegate d in lit.GetInvocationList())
                {
                    if (d.Target is UnityEngine.Object)
                    {
                        Assert.IsTrue((UnityEngine.Object)d.Target != null, "Beacon.Lit still calls a destroyed object");
                    }
                }
            }

            RaiseStole();
            yield return null;
            yield return null;
            Assert.IsEmpty(problems, "Errors were logged after reload: " + string.Join(" | ", problems));
        }

        [UnityTest]
        public IEnumerator NoWindMeansNoLean()
        {
            // 7. Island1 has no Wind component.
            SceneManager.LoadScene("Island1");
            yield return null;
            yield return null;
            Assert.IsNull(FindOne("Wind"), "Island1 unexpectedly has Wind");
            MonoBehaviour keeper = FindOne("KeeperAnimator");
            Assert.IsNotNull(keeper, "KeeperAnimator missing on Island1");
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Assert.AreEqual(0f, LeanWeight(keeper), 0f, "LeanWeight must be 0 without Wind");
            Assert.IsEmpty(problems, "Errors were logged on Island1: " + string.Join(" | ", problems));
        }

        static IEnumerator WaitForState(Animator animator, int layer, string state, float seconds)
        {
            float waited = 0f;
            while (waited < seconds && !Settled(animator, layer, state))
            {
                yield return null;
                waited += Time.deltaTime;
            }
        }

        static string Describe(Animator animator, int layer, MonoBehaviour keeper)
        {
            string[] names = { "Empty", "Stagger", "Light", "Die", "ShakeOff" };
            string current = "?";
            for (int i = 0; i < names.Length; i++)
            {
                if (animator.GetCurrentAnimatorStateInfo(layer).IsName(names[i]))
                {
                    current = names[i];
                }
            }

            return "current=" + current + " inTransition=" + animator.IsInTransition(layer) + " dead=" + animator.GetBool("Dead") + " arm=" + ArmWeight(keeper);
        }

        // Fully in the state, with no transition running toward or away from it.
        static bool Settled(Animator animator, int layer, string state)
        {
            return animator.GetCurrentAnimatorStateInfo(layer).IsName(state) && !animator.IsInTransition(layer);
        }

        static bool InState(Animator animator, int layer, string state)
        {
            if (animator.GetCurrentAnimatorStateInfo(layer).IsName(state))
            {
                return true;
            }

            return animator.IsInTransition(layer) && animator.GetNextAnimatorStateInfo(layer).IsName(state);
        }

        static float ArmWeight(MonoBehaviour keeper)
        {
            return (float)keeper.GetType().GetProperty("ArmWeight", BindingFlags.Public | BindingFlags.Instance).GetValue(keeper);
        }

        static float LeanWeight(MonoBehaviour keeper)
        {
            return (float)keeper.GetType().GetProperty("LeanWeight", BindingFlags.Public | BindingFlags.Instance).GetValue(keeper);
        }

        static Delegate StaticEvent(string typeName, string eventName)
        {
            FieldInfo field = FindType(typeName).GetField(eventName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, typeName + "." + eventName + " event field not found");
            return field.GetValue(null) as Delegate;
        }

        static void RaiseStole()
        {
            Delegate stole = StaticEvent("Shade", "Stole");
            Assert.IsNotNull(stole, "Nothing subscribes to Shade.Stole");
            stole.DynamicInvoke(1f);
        }

        static void RaiseDeathStarted()
        {
            object manager = FindType("GameManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            Assert.IsNotNull(manager, "GameManager.Instance missing");
            FieldInfo field = FindType("GameManager").GetField("DeathStarted", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "GameManager.DeathStarted event field not found");
            Delegate handler = field.GetValue(manager) as Delegate;
            Assert.IsNotNull(handler, "Nothing subscribes to GameManager.DeathStarted");
            handler.DynamicInvoke();
        }

        static MonoBehaviour FindUnlitBeacon()
        {
            MonoBehaviour[] all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].GetType().Name == "Beacon")
                {
                    bool lit = (bool)all[i].GetType().GetProperty("IsLit", BindingFlags.Public | BindingFlags.Instance).GetValue(all[i]);
                    if (!lit)
                    {
                        return all[i];
                    }
                }
            }

            return null;
        }

        static Type FindType(string name)
        {
            Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, "Type LanternKeeper." + name + " not found in Assembly-CSharp");
            return type;
        }

        static MonoBehaviour FindOne(string typeName)
        {
            MonoBehaviour[] all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].GetType().Name == typeName)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
