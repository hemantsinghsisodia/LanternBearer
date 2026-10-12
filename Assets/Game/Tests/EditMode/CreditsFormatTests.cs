using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class CreditsFormatTests
{
    static List<CreditEntry> RepoEntries()
    {
        string raw = File.ReadAllText(Path.Combine(Application.dataPath, "..", "CREDITS.txt"));
        return CreditsFormat.Parse(raw);
    }

    [Test]
    public void ParsesRepoCredits()
    {
        var entries = RepoEntries();
        Assert.IsNotEmpty(entries);
        Assert.IsTrue(entries.Exists(e => e.title == "First Light Particles" && e.author == "Yoiyami"));
        foreach (var e in entries)
        {
            string all = e.heading + "|" + e.title + "|" + e.author + "|" + e.licence;
            Assert.IsNotNull(e.heading);
            Assert.IsNotNull(e.title);
            Assert.IsNotNull(e.author);
            Assert.IsNotNull(e.licence);
            StringAssert.DoesNotContain("http", all);
            StringAssert.DoesNotContain("Assets/", all);
            StringAssert.DoesNotContain("MCP", all);
            StringAssert.DoesNotContain("Unity", all);
            Assert.IsTrue(e.licence.StartsWith("CC0") || e.licence == "SIL OFL 1.1", e.licence);
        }
    }

    [Test]
    public void ParsesEverySection()
    {
        var entries = RepoEntries();
        Assert.IsTrue(entries.Exists(e => e.title == "Hooded Adventurer" && e.author == "Quaternius" && e.licence == "CC0"));
        Assert.IsTrue(entries.Exists(e => e.heading == "Poly Haven" && e.title == "Models" && e.author.Contains("Rico Cilliers")));
        Assert.IsTrue(entries.Exists(e => e.heading == "Poly Haven" && e.title == "Terrain textures"));
        Assert.IsTrue(entries.Exists(e => e.heading == "Poly Haven" && e.title == "HDRIs" && e.author.Contains("Greg Zaal")));
        Assert.IsTrue(entries.Exists(e => e.title == "Stylized Nature MegaKit" && e.author == "Quaternius"));
        Assert.IsTrue(entries.Exists(e => e.title == "Inter" && e.licence == "SIL OFL 1.1"));
        Assert.IsTrue(entries.Exists(e => e.title == "Liberation Sans" && e.author.Contains("Red Hat, Inc.") && e.licence == "SIL OFL 1.1"));
        Assert.IsTrue(entries.Exists(e => e.title == "Cormorant Garamond" && e.licence == "SIL OFL 1.1"));
        Assert.IsTrue(entries.Exists(e => e.title == "Spectral" && e.licence == "SIL OFL 1.1"));
        Assert.IsFalse(entries.Exists(e => e.heading.Contains("Made for") || e.heading.Contains("Editor")));
    }

    [Test]
    public void PolyHavenAuthorsAreDeduplicated()
    {
        var models = RepoEntries().Find(e => e.heading == "Poly Haven" && e.title == "Models");
        string[] names = models.author.Split(new[] { ", " }, System.StringSplitOptions.None);
        Assert.AreEqual(names.Length, new HashSet<string>(names).Count);
        Assert.AreEqual("Rico Cilliers", names[0]);
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
