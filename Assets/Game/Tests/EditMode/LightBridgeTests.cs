using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Timings = LanternKeeper.BridgeTimings;

namespace LanternKeeper.Tests
{
// Timing, growth and safe-light geometry of the Island 5 light bridges (pure logic), plus the shader and the installed assets.
public class LightBridgeTests
{
    [Test]
    public void BridgeTimings()
    {
        Assert.AreEqual(1.0f, Timings.BeamSeconds, 1e-4f, "the beam arc is 1 s");
        Assert.AreEqual(1.5f, Timings.GrowSeconds, 1e-4f, "the grow is 1.5 s");
        Assert.LessOrEqual(Timings.BeamSeconds + Timings.GrowSeconds, 2.5f + 1e-4f, "beacon lit to walkable bridge");
        Assert.AreEqual(Timings.TotalSeconds, Timings.BeamSeconds + Timings.GrowSeconds, 1e-4f);
    }

    [Test]
    public void ProgressIsZeroDuringTheBeamAndOneAtTheEnd()
    {
        Assert.AreEqual(0f, Timings.Progress(0f), 1e-4f);
        Assert.AreEqual(0f, Timings.Progress(1.0f), 1e-4f);
        Assert.AreEqual(0.5f, Timings.Progress(1.75f), 1e-3f);
        Assert.AreEqual(1f, Timings.Progress(2.5f), 1e-4f);
        Assert.AreEqual(1f, Timings.Progress(9f), 1e-4f);
        Assert.AreEqual(0.5f, Timings.BeamHead(0.5f), 1e-4f);
        Assert.AreEqual(1f, Timings.BeamHead(3f), 1e-4f);
    }

    [Test]
    public void PlanksGrowFromBothEndsAndMeetInTheMiddle()
    {
        Assert.IsFalse(Timings.PlankVisible(0.5f, 0.1f), "the middle is last");
        Assert.IsTrue(Timings.PlankVisible(0.02f, 0.1f), "the near end is first");
        Assert.IsTrue(Timings.PlankVisible(0.98f, 0.1f), "the far end is first");
        Assert.IsFalse(Timings.PlankVisible(0.3f, 0.4f));
        Assert.IsTrue(Timings.PlankVisible(0.3f, 0.6f));
        Assert.IsTrue(Timings.PlankVisible(0.5f, 1f), "complete");
    }

    static BridgeZone Zone()
    {
        return new BridgeZone { start = new Vector3(0f, 10f, 0f), end = new Vector3(12f, 15f, 0f), halfWidth = 1.7f };
    }

    [Test]
    public void ZoneContainsTheDeckAndItsMargin()
    {
        BridgeZone zone = Zone();
        Assert.IsTrue(zone.Contains(new Vector3(6f, 12.6f, 0f)), "mid deck");
        Assert.IsTrue(zone.Contains(new Vector3(6f, 12.6f, 2.5f)), "inside the margin");
        Assert.IsFalse(zone.Contains(new Vector3(6f, 12.6f, 3.5f)), "outside the margin");
        Assert.IsFalse(zone.Contains(new Vector3(6f, 25f, 0f)), "high above is another level");
        Assert.IsFalse(zone.Contains(new Vector3(6f, 5f, 0f)), "far below is another level");
        Assert.IsTrue(zone.Contains(new Vector3(-1.5f, 10f, 0f)), "just past the near end");
        Assert.IsFalse(zone.Contains(new Vector3(-4f, 10f, 0f)), "well past the near end");
    }

    [Test]
    public void PushOutLeavesTheZone()
    {
        BridgeZone zone = Zone();
        Vector3 inside = new Vector3(6f, 12.6f, 0.4f);
        Vector3 pushed = zone.PushOut(inside);
        Assert.IsFalse(zone.Contains(pushed), "pushed outside");
        Assert.AreEqual(inside.y, pushed.y, 1e-4f);
        Assert.AreEqual(Vector3.zero, zone.Repel(new Vector3(6f, 12.6f, 40f)), "no push far away");
        Assert.Greater(zone.Repel(new Vector3(6f, 12.6f, 3.5f)).z, 0f, "pushed away from the deck");
        Vector3 outside = new Vector3(6f, 12.6f, 9f);
        Assert.AreEqual(outside, zone.PushOut(outside), "outside is unchanged");
        Vector3 onTheLine = new Vector3(6f, 12.6f, 0f);
        Assert.IsFalse(zone.Contains(zone.PushOut(onTheLine)), "even exactly on the centreline");
    }

    [Test]
    public void ShaderIsAdditiveInstancedWithNoShadowOrDepthPass()
    {
        Shader shader = Shader.Find("LanternKeeper/LightBridge");
        Assert.IsNotNull(shader);
        Assert.IsTrue(shader.isSupported);
        string text = System.IO.File.ReadAllText("Assets/Game/Shaders/LightBridge.shader");
        StringAssert.Contains("Blend One One", text);
        StringAssert.Contains("multi_compile_instancing", text);
        StringAssert.Contains("ZWrite Off", text);
        StringAssert.DoesNotContain("\"LightMode\" = \"ShadowCaster\"", text);
        StringAssert.DoesNotContain("\"LightMode\" = \"DepthOnly\"", text);
        StringAssert.DoesNotContain("\"LightMode\" = \"DepthNormals\"", text);
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(text, @"^\s*Pass\b", System.Text.RegularExpressions.RegexOptions.Multiline).Count, "one forward pass only");
    }

    [Test]
    public void InstalledAssetsExist()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Generated/LightBridge.mat"));
        Material deck = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Generated/LightBridge.mat");
        Assert.AreEqual("LanternKeeper/LightBridge", deck.shader.name);
        Assert.IsTrue(deck.enableInstancing, "GPU instancing");
        GameObject lantern = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Gameplay/BridgeLantern.prefab");
        Assert.IsNotNull(lantern);
        Assert.IsNotNull(lantern.transform.Find("FlameRoot"), "the lantern has a flame");
        Assert.IsNotNull(lantern.transform.Find("BeaconGlass"), "the lantern has glass");
        foreach (MonoBehaviour behaviour in lantern.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Assert.AreNotEqual("Beacon", behaviour == null ? "" : behaviour.GetType().Name, "a bridge lantern is not a beacon");
        }
    }
}
}
