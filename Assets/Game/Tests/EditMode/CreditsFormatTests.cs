using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class CreditsFormatTests
{
    [Test]
    public void ParsesRepoCredits()
    {
        string raw = File.ReadAllText(Path.Combine(Application.dataPath, "..", "CREDITS.txt"));
        var entries = CreditsFormat.Parse(raw);
        Assert.IsNotEmpty(entries);
        Assert.IsTrue(entries.Exists(e => e.title == "First Light Particles" && e.author == "Yoiyami"));
        foreach (var e in entries)
        {
            foreach (string f in new[] { e.heading, e.title, e.author, e.licence })
            {
                Assert.IsNotNull(f);
                StringAssert.DoesNotContain("http", f);
                StringAssert.DoesNotContain("Assets/", f);
            }
            StringAssert.StartsWith("CC0", e.licence);
        }
    }

    [Test]
    public void ParseIgnoresBlankAndUnknownLines()
    {
        Assert.DoesNotThrow(() =>
        {
            Assert.AreEqual(0, CreditsFormat.Parse(null).Count);
            Assert.AreEqual(0, CreditsFormat.Parse("").Count);
            Assert.AreEqual(0, CreditsFormat.Parse("\n\n???\n  by by by\n---\n---\nLicence:\n  \n").Count);
        });
    }
}
}
