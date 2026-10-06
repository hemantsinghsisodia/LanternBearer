using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
// Regression guard: rebuilding the biomes again must not touch them. RebuildBiome leaves entries that already use a Nature_ prefab alone
// and only re-targets deadwood (halving its count once), so a second run has to produce byte-identical biome assets.
public class BiomeRebuildIdempotencyTests
{
    static readonly string[] Biomes = { "PineForest", "Namaqualand", "Marsh", "Heath" };
    static readonly string[] Levels = { "island1", "island2", "island3", "island4" };

    static string BiomePath(string biome)
    {
        return "Assets/Game/Levels/Biomes/" + biome + ".asset";
    }

    // Unity saves assets with LF while a Windows checkout may hold CRLF, so compare content only.
    static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n");
    }

    static string[] SnapshotPaths()
    {
        return new[]
        {
            BiomePath("PineForest"), BiomePath("Namaqualand"), BiomePath("Marsh"), BiomePath("Heath"),
            "Assets/Game/Levels/Island1.asset", "Assets/Game/Levels/Island2.asset"
        };
    }

    // Restores every snapshotted file to its original text, so a failing run can't leave modified assets behind.
    static void Restore(string[] paths, string[] texts)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            if (Normalize(File.ReadAllText(paths[i])) != Normalize(texts[i]))
            {
                File.WriteAllText(paths[i], texts[i]);
            }

            AssetDatabase.ImportAsset(paths[i]);
        }
    }

    [Test]
    public void RebuildBiomeTwiceLeavesBiomeAssetsUnchanged()
    {
        // The importer lives in the Editor assembly, which this one can't reference.
        Type importer = Type.GetType("NatureKitImporter, Assembly-CSharp-Editor") ?? Type.GetType("LanternKeeper.NatureKitImporter, Assembly-CSharp-Editor");
        Assert.IsNotNull(importer, "NatureKitImporter type not found");
        MethodInfo rebuild = importer.GetMethod("RebuildBiome", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(rebuild, "NatureKitImporter.RebuildBiome not found");

        AssetDatabase.SaveAssets();
        string[] paths = SnapshotPaths();
        string[] before = new string[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            before[i] = File.ReadAllText(paths[i]);
        }

        try
        {
            for (int i = 0; i < Biomes.Length; i++)
            {
                rebuild.Invoke(null, new object[] { Biomes[i], Levels[i] });
            }

            AssetDatabase.SaveAssets();
            for (int i = 0; i < Biomes.Length; i++)
            {
                Assert.AreEqual(Normalize(before[i]), Normalize(File.ReadAllText(paths[i])), Biomes[i] + " biome asset changed after RebuildBiome");
            }
        }
        finally
        {
            Restore(paths, before);
        }
    }

    // The Poly Haven importer must not rewrite biomes that were already converted to the nature kit, nor re-link the islands.
    [Test]
    public void PolyHavenCreateBiomesLeavesConvertedBiomesUnchanged()
    {
        Type importer = Type.GetType("PolyHavenImporter, Assembly-CSharp-Editor") ?? Type.GetType("LanternKeeper.PolyHavenImporter, Assembly-CSharp-Editor");
        Assert.IsNotNull(importer, "PolyHavenImporter type not found");
        MethodInfo create = importer.GetMethod("CreateBiomes", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(create, "PolyHavenImporter.CreateBiomes not found");

        AssetDatabase.SaveAssets();
        string[] paths = SnapshotPaths();
        string[] before = new string[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            before[i] = File.ReadAllText(paths[i]);
        }

        try
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("Poly Haven import: skipped .*PineForest"));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("Poly Haven import: skipped .*Namaqualand"));
            create.Invoke(null, null);
            AssetDatabase.SaveAssets();
            for (int i = 0; i < paths.Length; i++)
            {
                Assert.AreEqual(Normalize(before[i]), Normalize(File.ReadAllText(paths[i])), paths[i] + " changed after PolyHavenImporter.CreateBiomes");
            }
        }
        finally
        {
            Restore(paths, before);
        }
    }
}
}
