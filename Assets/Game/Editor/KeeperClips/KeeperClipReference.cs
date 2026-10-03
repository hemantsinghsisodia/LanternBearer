using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
[System.Serializable]
public class KeeperClipSample
{
    public float fraction;
    public float[] rotations;
}

[System.Serializable]
public class KeeperClipEntry
{
    public string name;
    public float length;
    public KeeperClipSample[] samples;
}

[System.Serializable]
public class KeeperClipData
{
    public string[] bones;
    public KeeperClipEntry[] clips;
}

// Samples clip lengths and local bone rotations from Keeper.fbx. Shared by the recorder menu item and the re-export guard test.
public static class KeeperClipSampler
{
    public const string FbxPath = "Assets/Game/Models/Keeper/Keeper.fbx";

    public static KeeperClipData Capture(string[] boneNames, string[] clipLeaves, float[] fractions, out string error)
    {
        error = null;
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null)
        {
            error = "Keeper.fbx not found";
            return null;
        }

        GameObject instance = Object.Instantiate(model);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Transform[] bones = new Transform[boneNames.Length];
            for (int b = 0; b < boneNames.Length; b++)
            {
                bones[b] = FindDeep(instance.transform, boneNames[b]);
                if (bones[b] == null)
                {
                    error = "Missing bone: " + boneNames[b];
                    return null;
                }
            }

            List<KeeperClipEntry> entries = new List<KeeperClipEntry>();
            for (int c = 0; c < clipLeaves.Length; c++)
            {
                AnimationClip clip = FindClip(clipLeaves[c]);
                if (clip == null)
                {
                    error = "Clip missing: " + clipLeaves[c];
                    return null;
                }

                KeeperClipEntry entry = new KeeperClipEntry();
                entry.name = clipLeaves[c];
                entry.length = clip.length;
                entry.samples = new KeeperClipSample[fractions.Length];
                for (int s = 0; s < fractions.Length; s++)
                {
                    clip.SampleAnimation(instance, fractions[s] * clip.length);
                    KeeperClipSample sample = new KeeperClipSample();
                    sample.fraction = fractions[s];
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

            KeeperClipData data = new KeeperClipData();
            data.bones = boneNames;
            data.clips = entries.ToArray();
            return data;
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

public static class KeeperClipReference
{
    const string JsonPath = "Assets/Game/Tests/EditMode/Data/KeeperClipReference.json";

    // Idle, Walk and Run drive the controller; the rest are gameplay clips. This rig has no jump clips.
    static readonly string[] ClipLeaves = { "Idle", "Walk", "Run", "Roll", "Interact", "HitRecieve", "HitRecieve_2", "Death" };
    static readonly string[] BoneNames = { "Hips", "Abdomen", "Torso", "Head", "UpperArm.R", "LowerArm.R", "Wrist.R", "UpperLeg.L", "LowerLeg.L" };
    static readonly float[] SampleFractions = { 0f, 0.25f, 0.5f, 0.75f, 1f };

    [MenuItem("Lantern Keeper/Keeper/Record Clip Reference")]
    static void Record()
    {
        string error;
        KeeperClipData data = KeeperClipSampler.Capture(BoneNames, ClipLeaves, SampleFractions, out error);
        if (data == null)
        {
            Debug.LogError("Keeper clip reference failed: " + error);
            return;
        }

        string full = Path.Combine(Application.dataPath, "..", JsonPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, JsonUtility.ToJson(data, true));
        AssetDatabase.ImportAsset(JsonPath);
        Debug.Log("Keeper clip reference written: " + JsonPath);
    }
}
}
