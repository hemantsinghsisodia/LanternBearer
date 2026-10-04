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
    // A graphics preset forces one keeper LOD set and the rim keyword on both sets.
    // This assembly can't reference Assembly-CSharp, so the game types are reached by reflection.
    public class KeeperLodTest
    {
        const string NoRimKeyword = "_LK_KEEPER_NO_RIM";
        readonly List<string> problems = new List<string>();
        int previousLevel;
        bool hadPref;

        [SetUp]
        public void SetUp()
        {
            problems.Clear();
            Application.logMessageReceived += OnLog;
            hadPref = PlayerPrefs.HasKey("LanternKeeperGraphics");
            previousLevel = PlayerPrefs.GetInt("LanternKeeperGraphics", 1);
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            // Put the in-memory preset back too, not just the saved pref.
            SetLevel(Enum.GetName(Type.GetType("LanternKeeper.GraphicsLevel, Assembly-CSharp"), previousLevel));
            if (hadPref)
            {
                PlayerPrefs.SetInt("LanternKeeperGraphics", previousLevel);
            }
            else
            {
                PlayerPrefs.DeleteKey("LanternKeeperGraphics");
            }

            PlayerPrefs.Save();
        }

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                problems.Add("[" + type + "] " + message);
            }
        }

        [UnityTest]
        public IEnumerator PresetSwitchSwapsLod()
        {
            SceneManager.LoadScene("Island1");
            yield return null;
            yield return null;

            MonoBehaviour keeper = FindOne("KeeperAnimator");
            Assert.IsNotNull(keeper, "KeeperAnimator missing on Island1");
            LODGroup group = keeper.GetComponentInChildren<LODGroup>(true);
            Assert.IsNotNull(group, "Keeper has no LODGroup");
            Assert.AreEqual(2, group.lodCount, "Keeper LODGroup must have 2 levels");
            Animator animator = keeper.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "Keeper Animator missing");

            SetLevel("Ultra");
            yield return null;
            yield return null;
            AssertSet(keeper, animator, 0, true);
            yield return null;
            AssertSwayWritten();

            SetLevel("Low");
            yield return null;
            yield return null;
            AssertSet(keeper, animator, 1, false);
            yield return null;
            AssertSwayWritten();

            SetLevel("High");
            yield return null;
            yield return null;
            AssertSet(keeper, animator, 0, true);
            yield return null;
            AssertSwayWritten();

            Assert.IsEmpty(problems, "Errors were logged: " + string.Join(" | ", problems));
        }

        void AssertSet(MonoBehaviour keeper, Animator animator, int lod, bool rim)
        {
            SkinnedMeshRenderer[] skins = keeper.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int shown = 0;
            int hidden = 0;
            int rimChecked = 0;
            for (int i = 0; i < skins.Length; i++)
            {
                bool isLod0 = skins[i].name.EndsWith("_LOD0");
                bool isLod1 = skins[i].name.EndsWith("_LOD1");
                if (!isLod0 && !isLod1)
                {
                    continue;
                }

                bool wanted = (lod == 0) == isLod0;
                Assert.AreEqual(wanted, skins[i].isVisible, skins[i].name + " visibility for forced LOD" + lod);
                if (wanted)
                {
                    shown++;
                }
                else
                {
                    hidden++;
                }

                Material[] materials = skins[i].materials;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m].shader.name != "LanternKeeper/KeeperLit")
                    {
                        continue;
                    }

                    rimChecked++;
                    Assert.AreEqual(!rim, materials[m].IsKeywordEnabled(NoRimKeyword), skins[i].name + " rim keyword (rim expected " + rim + ")");
                }
            }

            Assert.AreEqual(4, shown, "Four renderers should show for LOD" + lod);
            Assert.AreEqual(4, hidden, "Four renderers should hide for LOD" + lod);
            Assert.Greater(rimChecked, 0, "No KeeperLit materials found");
            Assert.IsTrue(animator.enabled && animator.isActiveAndEnabled, "Animator stopped");
            Assert.IsNotNull(keeper.GetComponentInChildren<Animator>().runtimeAnimatorController, "Animator lost its controller");
            Assert.IsNotNull(FindOne("LanternFlame"), "LanternFlame missing");
            int sway = Shader.PropertyToID("_LKKeeperSway");
            Shader.SetGlobalVector(sway, new Vector4(-99f, -99f, -99f, -99f));
        }

        static void AssertSwayWritten()
        {
            Assert.AreNotEqual(-99f, Shader.GetGlobalVector(Shader.PropertyToID("_LKKeeperSway")).x, "_LKKeeperSway is no longer written");
        }

        static void SetLevel(string name)
        {
            Type quality = Type.GetType("LanternKeeper.GraphicsQuality, Assembly-CSharp");
            Type level = Type.GetType("LanternKeeper.GraphicsLevel, Assembly-CSharp");
            Assert.IsNotNull(quality, "GraphicsQuality not found");
            quality.GetMethod("Set", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { Enum.Parse(level, name) });
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
