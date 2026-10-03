using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
// Records clip lengths and sampled local bone rotations from Keeper.fbx so a re-export can be checked against them.
// KeeperClipTests re-samples the same way; keep the two in step.
public static class KeeperClipReference
{
    const string FbxPath = "Assets/Game/Models/Keeper/Keeper.fbx";
    const string JsonPath = "Assets/Game/Tests/EditMode/Data/KeeperClipReference.json";

    // Idle, Walk and Run drive the controller; the rest are gameplay clips. This rig has no jump clips.
    static readonly string[] ClipLeaves = { "Idle", "Walk", "Run", "Roll", "Interact", "HitRecieve", "HitRecieve_2", "Death" };
    static readonly string[] BoneNames = { "Hips", "Abdomen", "Torso", "Head", "UpperArm.R", "LowerArm.R", "Wrist.R", "UpperLeg.L", "LowerLeg.L" };
    static readonly float[] SampleFractions = { 0f, 0.25f, 0.5f, 0.75f, 1f };

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

    [MenuItem("Lantern Keeper/Keeper/Record Clip Reference")]
    static void Record()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null)
        {
            Debug.LogError("Keeper clip reference: Keeper.fbx not found");
            return;
        }

        GameObject instance = Object.Instantiate(model);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Transform[] bones = new Transform[BoneNames.Length];
            for (int b = 0; b < BoneNames.Length; b++)
            {
                bones[b] = FindDeep(instance.transform, BoneNames[b]);
                if (bones[b] == null)
                {
                    Debug.LogError("Keeper clip reference: missing bone " + BoneNames[b]);
                    return;
                }
            }

            List<ClipEntry> entries = new List<ClipEntry>();
            for (int c = 0; c < ClipLeaves.Length; c++)
            {
                AnimationClip clip = FindClip(ClipLeaves[c]);
                if (clip == null)
                {
                    Debug.LogError("Keeper clip reference: missing clip " + ClipLeaves[c]);
                    return;
                }

                ClipEntry entry = new ClipEntry();
                entry.name = ClipLeaves[c];
                entry.length = clip.length;
                entry.samples = new Sample[SampleFractions.Length];
                for (int s = 0; s < SampleFractions.Length; s++)
                {
                    clip.SampleAnimation(instance, SampleFractions[s] * clip.length);
                    Sample sample = new Sample();
                    sample.fraction = SampleFractions[s];
                    sample.rotations = new float[bones.Length * 4];
                    for (int b = 0; b < bones.Length; b++)
                    {
                        Quaternion q = bones[b].localRotation;
                        sample.rotations[b * 4] = q.x;
                        sample.rotations[b * 4 + 1] = q.y;
                        sample.rotations[b * 4 + 2] = q.z;
                        sample.rotations[b * 4 + 3] = q.w;
                    }

                    entry.samples[s] = sample;
                }

                entries.Add(entry);
            }

            Reference reference = new Reference();
            reference.bones = BoneNames;
            reference.clips = entries.ToArray();
            string full = Path.Combine(Application.dataPath, "..", JsonPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, JsonUtility.ToJson(reference, true));
            AssetDatabase.ImportAsset(JsonPath);
            Debug.Log("Keeper clip reference written: " + JsonPath);
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
