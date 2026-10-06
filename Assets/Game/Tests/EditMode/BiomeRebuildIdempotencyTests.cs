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

    [Test]
    public void RebuildBiomeTwiceLeavesBiomeAssetsUnchanged()
    {
        // The importer lives in the Editor assembly, which this one can't reference.
        Type importer = Type.GetType("LanternKeeper.NatureKitImporter, Assembly-CSharp-Editor");
        Assert.IsNotNull(importer, "NatureKitImporter type not found");
        MethodInfo rebuild = importer.GetMethod("RebuildBiome", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(rebuild, "NatureKitImporter.RebuildBiome not found");

        AssetDatabase.SaveAssets();
        string[] before = new string[Biomes.Length];
        for (int i = 0; i < Biomes.Length; i++)
        {
            before[i] = File.ReadAllText(BiomePath(Biomes[i]));
        }

        for (int i = 0; i < Biomes.Length; i++)
        {
            rebuild.Invoke(null, new object[] { Biomes[i], Levels[i] });
        }

        AssetDatabase.SaveAssets();
        for (int i = 0; i < Biomes.Length; i++)
        {
            Assert.AreEqual(before[i], File.ReadAllText(BiomePath(Biomes[i])), Biomes[i] + " biome asset changed after RebuildBiome");
        }
    }
}
}
