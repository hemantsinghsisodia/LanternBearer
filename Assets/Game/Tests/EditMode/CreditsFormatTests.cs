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
        Assert.IsTrue(entries.Exists(e => e.title == "Liberation Sans" && e.author == "Google Corporation, Red Hat, Inc." && e.heading == "Fonts" && e.licence == "SIL OFL 1.1"));
        Assert.IsTrue(entries.Exists(e => e.title == "Cormorant Garamond" && e.licence == "SIL OFL 1.1"));
        Assert.IsTrue(entries.Exists(e => e.title == "Spectral" && e.licence == "SIL OFL 1.1"));
        Assert.IsFalse(entries.Exists(e => e.heading.Contains("Made for") || e.heading.Contains("Editor")));
    }

    [Test]
    public void ParsedTitlesAreExactlyTheCredits()
    {
        var expected = new List<string>
        {
            "First Light Particles", "Ambient Relaxing Loop", "Mysterious Ambience (song21)", "Underwater Theme",
            "The World Fell Silent (loop version)", "Epic Endgame Cinematic", "Dark Cavern Ambient (file 002, continuous loop)",
            "Fantasy Orchestral Theme", "Lost in a bad place (horror ambience loop)", "Dark Sci-Fi Audio Pack: Airy",
            "Paranoid Truth", "Horror Atmosphere (Post Apocalyptic Wastelands)",
            "Victory Fanfare Short", "Game Over IV",
            "Interface Sounds", "Impact Sounds", "50 RPG sound effects", "100 CC0 SFX #2", "80 CC0 creature SFX",
            "80 CC0 RPG SFX", "40 CC0 water splash and slime SFX", "Fantozzi's Footsteps (Grass/Sand & Stone)",
            "Fire Crackling (fire-1)", "Gasoline fire #1", "Pleasing Bell Sound Effect", "Beach Ocean Waves", "Rain (loopable)",
            "Crickets Ambient Noise - loopable", "Short wind sound", "wind whoosh loop", "Tawny Owl #1", "Strong Wind in a Village",
            "Hooded Adventurer", "Models", "Terrain textures", "HDRIs", "Stylized Nature MegaKit",
            "Liberation Sans", "Cormorant Garamond", "Spectral", "Inter",
        };
        var actual = RepoEntries().ConvertAll(e => e.title);
        CollectionAssert.AreEqual(expected, actual);
    }

    [Test]
    public void ProseLinesAreNotCredits()
    {
        const string raw = "---\nCharacter (CC0 1.0)\n---\n\n\"Thing\" by Someone, via Site\nhttps://x.example/a\nAnimations used by the keeper are the clips.\n\nA note made by hand.\n";
        var entries = CreditsFormat.Parse(raw);
        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("Thing", entries[0].title);
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
