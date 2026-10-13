using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class PreDawnTests
{
    [Test]
    public void ReachValues()
    {
        Assert.AreEqual(0.15f, PreDawn.ReachFor("island1"), 1e-6f);
        Assert.AreEqual(0.25f, PreDawn.ReachFor("island2"), 1e-6f);
        Assert.AreEqual(0.35f, PreDawn.ReachFor("island3"), 1e-6f);
        Assert.AreEqual(0.50f, PreDawn.ReachFor("island4"), 1e-6f);
        Assert.AreEqual(1f, PreDawn.ReachFor("island5"), 1e-6f);
        Assert.AreEqual(1f, PreDawn.ReachFor("menu"), 1e-6f);
        Assert.AreEqual(1f, PreDawn.ReachFor(null), 1e-6f);
    }

    // The scenes bake PreDawn.ReachFor (PreDawnInstaller or IslandBuilder). Read as text, nothing is opened or written.
    [Test]
    public void SceneReachMatchesTable()
    {
        for (int island = 1; island <= 5; island++)
        {
            string path = "Assets/Game/Scenes/Island" + island + ".unity";
            string text = System.IO.File.ReadAllText(path);
            System.Text.RegularExpressions.MatchCollection found = System.Text.RegularExpressions.Regex.Matches(text, @"^\s+reach: ([0-9.]+)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);
            float expected = PreDawn.ReachFor("island" + island);
            if (island == 5)
            {
                Assert.IsTrue(found.Count == 0 || float.Parse(found[0].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) == 1f, "Island 5 keeps the full sunrise");
                continue;
            }

            Assert.AreEqual(1, found.Count, "Island" + island + " has one serialized reach");
            Assert.AreEqual(expected, float.Parse(found[0].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-6f, "Island" + island);
        }
    }


    [Test]
    public void BlendStopsAtReach()
    {
        Assert.AreEqual(0f, PreDawn.Blend(0f, 0.45f), 1e-6f);
        Assert.AreEqual(0.45f, PreDawn.Blend(1f, 0.45f), 1e-6f);
        Assert.AreEqual(0.45f, PreDawn.Blend(2f, 0.45f), 1e-6f);
        Assert.AreEqual(0.225f, PreDawn.Blend(0.5f, 0.45f), 1e-6f);
        float last = 0f;
        for (int i = 0; i <= 20; i++)
        {
            float b = PreDawn.Blend(i / 20f, 0.65f);
            Assert.GreaterOrEqual(b, last);
            Assert.LessOrEqual(b, 0.65f + 1e-6f);
            last = b;
        }

        Assert.AreEqual(1f, PreDawn.Blend(1f, 1f), 1e-6f);
    }
}
}
