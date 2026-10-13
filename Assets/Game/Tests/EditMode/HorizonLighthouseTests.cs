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
        MethodInfo apply = Installer().GetMethod("Apply", new[] { typeof(Scene) });
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
            Assert.AreEqual(before, once, name + ": the saved scene is not what the installer makes (run Lantern Keeper > Install Horizon Lighthouse and save)");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void IslandFiveHasNoEmptyHorizonIslet()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath("Island5"), OpenSceneMode.Additive);
        try
        {
            Transform horizon = FindDeep(scene, "Horizon");
            Assert.IsNotNull(horizon, "Horizon root");
            foreach (Transform child in horizon)
            {
                // The lighthouse's islet is the only one with a single pine; Island 5 must not keep it.
                Assert.IsFalse(child.name.StartsWith("Islet") && child.childCount == 2, "an empty lighthouse islet is left: " + child.name);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void MenuLitLookIsTheAmberGreatLampBeam()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath("MainMenu"), OpenSceneMode.Additive);
        try
        {
            Transform pivot = FindDeep(scene, "LampPivot");
            Assert.IsNotNull(pivot, "MenuGreatLight lamp pivot");
            Assert.IsNull(FindDeep(scene, "BeamCore"), "the pale BeamCore card is gone");
            Assert.IsNull(FindDeep(scene, "BeamSoft"), "the pale BeamSoft card is gone");
            Assert.IsNull(pivot.GetComponent(Type.GetType("LanternKeeper.LighthouseBeam, Assembly-CSharp")), "the old sweeping beam script is gone");
            Transform beam = pivot.Find("GreatLightBeam");
            Assert.IsNotNull(beam, "GreatLightBeam under the pivot");
            Assert.IsNotNull(beam.GetComponent(Type.GetType("LanternKeeper.GreatLampBeam, Assembly-CSharp")), "GreatLampBeam on it");
            Assert.AreEqual("BeaconBeam", beam.GetComponent<Renderer>().sharedMaterial.name, "the finale's amber beam material");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    // The build tail: IslandBuilder.BuildInternal calls HorizonLighthouseInstaller.Apply after CreateHorizon, so a rebuild (Build, Build All,
    // BuildMainMenu) keeps the dark Great Light. A scenery lighthouse placed the way PlaceLighthouse does it must come out dark and without a beam.
    [TestCase(false)]
    [TestCase(true)]
    public void BuildTailLeavesTheDarkGreatLightAfterPlaceLighthouse(bool menu)
    {
        Type builder = Type.GetType("LanternKeeper.IslandBuilder, Assembly-CSharp-Editor");
        Assert.IsNotNull(builder, "IslandBuilder type");
        MethodInfo place = builder.GetMethod("PlaceLighthouse", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(place, "IslandBuilder.PlaceLighthouse");
        Type artType = builder.GetNestedType("ArtKit", BindingFlags.NonPublic);
        Assert.IsNotNull(artType, "IslandBuilder.ArtKit");
        object art = Activator.CreateInstance(artType);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        Assert.IsNotNull(shader, "URP Unlit shader");
        Material scratch = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        foreach (string field in new[] { "rock", "iron", "lamp", "beam" })
        {
            FieldInfo info = artType.GetField(field);
            Assert.IsNotNull(info, "ArtKit." + field);
            info.SetValue(art, scratch);
        }

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            GameObject horizon = new GameObject("Horizon");
            GameObject islet = new GameObject("Islet0");
            islet.transform.SetParent(horizon.transform, false);
            islet.transform.localPosition = new Vector3(170f, 0f, 0f);
            place.Invoke(null, new object[] { islet.transform, 3f, art });
            Type beamType = Type.GetType("LanternKeeper.LighthouseBeam, Assembly-CSharp");
            Assert.IsNotNull(horizon.GetComponentInChildren(beamType, true), "precondition: PlaceLighthouse makes the lit scenery tower");

            MethodInfo tail = Installer().GetMethod("Apply", new[] { typeof(Scene), typeof(bool), typeof(bool) });
            Assert.IsNotNull(tail, "HorizonLighthouseInstaller.Apply(Scene, bool, bool)");
            tail.Invoke(null, new object[] { scene, menu, false });

            foreach (Component beam in horizon.GetComponentsInChildren(beamType, true))
            {
                Assert.IsFalse(beam is Behaviour b && b.isActiveAndEnabled, "an active LighthouseBeam is left");
            }

            Assert.IsNotNull(FindDeep(scene, "GreatLight"), "the Great Light model container");
            Assert.AreEqual(menu, FindDeep(scene, "LampPivot") != null, "the lamp pivot stays only for the menu");
            Type menuType = Type.GetType("LanternKeeper.MenuGreatLight, Assembly-CSharp");
            Assert.AreEqual(menu, FindDeep(scene, "Lighthouse").GetComponent(menuType) != null, "MenuGreatLight only for the menu");
        }
        finally
        {
            if (setup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            UnityEngine.Object.DestroyImmediate(scratch);
        }
    }
}
}
