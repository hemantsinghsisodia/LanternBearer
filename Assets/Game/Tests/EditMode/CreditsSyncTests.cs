using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
// Resources/Credits.txt is the copy of CREDITS.txt that the credits roll reads at run time (CreditsSync, run from FinaleInstaller).
public class CreditsSyncTests
{
    static string Normalise(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    [Test]
    public void CreditsResourceMatchesRepoFile()
    {
        TextAsset asset = Resources.Load<TextAsset>("Credits");
        Assert.IsNotNull(asset, "Assets/Game/Resources/Credits.txt");
        string repo = File.ReadAllText(Path.Combine(Application.dataPath, "..", "CREDITS.txt"));
        Assert.AreEqual(Normalise(repo), Normalise(asset.text), "Resources/Credits.txt is stale: run Lantern Keeper > Install Finale (Island 5)");
    }
}
}
