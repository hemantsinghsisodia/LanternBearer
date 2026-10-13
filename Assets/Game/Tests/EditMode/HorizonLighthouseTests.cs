using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper.Tests
{
// The distant scenery lighthouse is the Great Light's tower: dark with a faint glint on Islands 1-4 and the menu, gone on Island 5.
public class HorizonLighthouseTests
{
    static string ScenePath(string name)
    {
        return "Assets/Game/Scenes/" + name + ".unity";
    }

    static Type Installer()
    {
        Type type = Type.GetType("LanternKeeper.HorizonLighthouseInstaller, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "HorizonLighthouseInstaller type");
        return type;
    }

    static Transform FindDeep(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }
        }

        return null;
    }

    static string Signature(Scene scene)
    {
        Transform house = FindDeep(scene, "Lighthouse");
        if (house == null)
        {
            return "none";
        }

        StringBuilder text = new StringBuilder();
        foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
        {
            Renderer r = t.GetComponent<Renderer>();
            text.Append(t.name).Append('|').Append(t.localPosition.ToString("F3")).Append('|').Append(t.localScale.ToString("F3")).Append('|')
                .Append(t.gameObject.activeSelf).Append('|').Append(r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "-").Append('\n');
        }

        return text.ToString();
    }

    [TestCase("Island1")]
    [TestCase("Island2")]
    [TestCase("Island3")]
    [TestCase("Island4")]
    public void IslandsOneToFourHaveDarkGreatLight(string name)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath(name), OpenSceneMode.Additive);
        try
        {
            Type beamType = Type.GetType("LanternKeeper.LighthouseBeam, Assembly-CSharp");
            Assert.IsNotNull(beamType, "LighthouseBeam type");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component beam in root.GetComponentsInChildren(beamType, true))
                {
                    Assert.IsFalse(beam is Behaviour b && b.isActiveAndEnabled, name + ": an active LighthouseBeam is left");
                }

                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    Assert.AreNotEqual("LampLight", t.name, name + ": the lamp light is left");
                    MeshFilter filter = t.GetComponent<MeshFilter>();
                    Assert.IsFalse(filter != null && filter.sharedMesh != null && filter.sharedMesh.name == "LighthouseBeam", name + ": a beam mesh is left");
                }
            }

            Transform house = FindDeep(scene, "Lighthouse");
            Assert.IsNotNull(house, name + ": no Lighthouse");
            bool stone = false;
            Renderer glass = null;
            Bounds bounds = new Bounds();
            bool first = true;
            foreach (Renderer r in house.GetComponentsInChildren<Renderer>(true))
            {
                stone |= r.name == "LighthouseStone";
                if (r.name == "BeaconGlass")
                {
                    glass = r;
                }

                if (first)
                {
                    bounds = r.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            Assert.IsTrue(stone, name + ": no LighthouseStone mesh");
            Assert.IsNotNull(glass, name + ": no BeaconGlass");
            Assert.AreEqual("LighthouseGlass", glass.sharedMaterial.name);
            Assert.That(bounds.size.y, Is.InRange(13f, 17f), name + ": tower height");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void IslandFiveHasNoSceneryLighthouse()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath("Island5"), OpenSceneMode.Additive);
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    Assert.AreNotEqual("Lighthouse", t.name, "the scenery Lighthouse is still on Island 5");
                }
            }

            Assert.IsNotNull(FindDeep(scene, "GreatLamp"), "the real GreatLamp must stay");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void MenuHasTheDarkTowerAndAGreatLightComponent()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath("MainMenu"), OpenSceneMode.Additive);
        try
        {
            Type type = Type.GetType("LanternKeeper.MenuGreatLight, Assembly-CSharp");
            Assert.IsNotNull(type, "MenuGreatLight type");
            Transform house = FindDeep(scene, "Lighthouse");
            Assert.IsNotNull(house);
            Assert.IsNotNull(house.GetComponent(type), "MenuGreatLight on the Lighthouse");
            Assert.IsNotNull(FindDeep(scene, "LighthouseStone"));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [TestCase("Island1")]
    [TestCase("Island5")]
    [TestCase("MainMenu")]
    public void InstallerIsIdempotent(string name)
    {
        MethodInfo apply = Installer().GetMethod("Apply", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(apply, "HorizonLighthouseInstaller.Apply(Scene)");
        Scene scene = EditorSceneManager.OpenScene(ScenePath(name), OpenSceneMode.Additive);
        try
        {
            string before = Signature(scene);
            apply.Invoke(null, new object[] { scene });
            string once = Signature(scene);
            apply.Invoke(null, new object[] { scene });
            string twice = Signature(scene);
            Assert.AreEqual(once, twice, "the second run changed the scene");
            Assert.AreEqual(before, once, "the saved scene is not what the installer makes");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
}
