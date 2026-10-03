using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
// Guards the Keeper.fbx re-export: clip lengths and sampled bone rotations must match the recorded reference
// (written by the Lantern Keeper/Keeper/Record Clip Reference menu item).
public class KeeperClipTests
{
    const float AngleToleranceDegrees = 2f;

    [Test]
    public void ClipsMatchReference()
    {
        string path = Path.Combine(Application.dataPath, "Game/Tests/EditMode/Data/KeeperClipReference.json");
        Assert.IsTrue(File.Exists(path), "Reference JSON missing: " + path);
        KeeperClipData reference = JsonUtility.FromJson<KeeperClipData>(File.ReadAllText(path));
        Assert.IsNotNull(reference);
        Assert.IsNotNull(reference.clips);
        Assert.IsNotEmpty(reference.clips);

        string[] leaves = new string[reference.clips.Length];
        for (int c = 0; c < leaves.Length; c++)
        {
            leaves[c] = reference.clips[c].name;
        }

        KeeperClipSample[] shape = reference.clips[0].samples;
        float[] fractions = new float[shape.Length];
        for (int s = 0; s < fractions.Length; s++)
        {
            fractions[s] = shape[s].fraction;
        }

        string error;
        KeeperClipData actual = KeeperClipSampler.Capture(reference.bones, leaves, fractions, out error);
        Assert.IsNotNull(actual, error);

        for (int c = 0; c < reference.clips.Length; c++)
        {
            KeeperClipEntry expected = reference.clips[c];
            KeeperClipEntry got = actual.clips[c];
            Assert.AreEqual(expected.length, got.length, 1e-3f, "Length of " + expected.name);
            for (int s = 0; s < expected.samples.Length; s++)
            {
                for (int b = 0; b < reference.bones.Length; b++)
                {
                    Quaternion e = Read(expected.samples[s].rotations, b);
                    Quaternion a = Read(got.samples[s].rotations, b);
                    float angle = Quaternion.Angle(e, a);
                    Assert.Less(angle, AngleToleranceDegrees,
                        expected.name + " bone " + reference.bones[b] + " at " + expected.samples[s].fraction + " differs by " + angle + " deg");
                }
            }
        }
    }

    static Quaternion Read(float[] values, int bone)
    {
        return new Quaternion(values[bone * 4], values[bone * 4 + 1], values[bone * 4 + 2], values[bone * 4 + 3]);
    }
}
}
