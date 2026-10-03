using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
// Guards the Keeper.fbx re-export: clip lengths and sampled bone rotations must match the recorded reference
// (written by the Lantern Keeper/Keeper/Record Clip Reference menu item).
public class KeeperClipTests
{
    const string FbxPath = "Assets/Game/Models/Keeper/Keeper.fbx";
    const float AngleToleranceDegrees = 2f;

    [System.Serializable]
    class Sample
    {
        public float fraction;
        public float[] rotations;
    }

    [System.Serializable]
    class ClipEntry
    {
        public string name;
        public float length;
        public Sample[] samples;
    }

    [System.Serializable]
    class Reference
    {
        public string[] bones;
        public ClipEntry[] clips;
    }

    [Test]
    public void ClipsMatchReference()
    {
        string path = Path.Combine(Application.dataPath, "Game/Tests/EditMode/Data/KeeperClipReference.json");
        Assert.IsTrue(File.Exists(path), "Reference JSON missing: " + path);
        Reference reference = JsonUtility.FromJson<Reference>(File.ReadAllText(path));
        Assert.IsNotNull(reference);
        Assert.IsNotEmpty(reference.clips);

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        Assert.IsNotNull(model, "Keeper.fbx missing");
        GameObject instance = Object.Instantiate(model);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Transform[] bones = new Transform[reference.bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                bones[b] = FindDeep(instance.transform, reference.bones[b]);
                Assert.IsNotNull(bones[b], "Missing bone: " + reference.bones[b]);
            }

            for (int c = 0; c < reference.clips.Length; c++)
            {
                ClipEntry expected = reference.clips[c];
                AnimationClip clip = FindClip(expected.name);
                Assert.IsNotNull(clip, "Clip missing: " + expected.name);
                Assert.AreEqual(expected.length, clip.length, 1e-3f, "Length of " + expected.name);
                for (int s = 0; s < expected.samples.Length; s++)
                {
                    clip.SampleAnimation(instance, expected.samples[s].fraction * clip.length);
                    for (int b = 0; b < bones.Length; b++)
                    {
                        float[] r = expected.samples[s].rotations;
                        Quaternion e = new Quaternion(r[b * 4], r[b * 4 + 1], r[b * 4 + 2], r[b * 4 + 3]);
                        float angle = Quaternion.Angle(e, bones[b].localRotation);
                        Assert.Less(angle, AngleToleranceDegrees,
                            expected.name + " bone " + reference.bones[b] + " at " + expected.samples[s].fraction + " differs by " + angle + " deg");
                    }
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    static AnimationClip FindClip(string leaf)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview"))
            {
                continue;
            }

            int bar = clip.name.LastIndexOf('|');
            string name = bar >= 0 ? clip.name.Substring(bar + 1) : clip.name;
            if (name == leaf)
            {
                return clip;
            }
        }

        return null;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
}
