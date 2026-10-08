using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace LanternKeeper.Tests
{
public class SoundBankTests
{
    const string BankPath = "Assets/Game/Audio/SoundBank.asset";
    const string MixerPath = "Assets/Game/Audio/LanternMixer.mixer";

    static SoundBank Bank()
    {
        SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
        Assert.IsNotNull(bank, "Run Lantern Keeper > Build Sound Bank");
        return bank;
    }

    static List<string> AllCues()
    {
        List<string> names = new List<string>();
        FieldInfo[] fields = typeof(SoundCues).GetFields(BindingFlags.Public | BindingFlags.Static);
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].IsLiteral && fields[i].FieldType == typeof(string))
            {
                names.Add((string)fields[i].GetRawConstantValue());
            }
        }

        return names;
    }

    [TearDown]
    public void TearDown()
    {
        Undo.ClearAll();
    }

    [Test]
    public void EveryCueConstantHasBankEntry()
    {
        SoundBank bank = Bank();
        List<string> names = AllCues();
        Assert.AreEqual(43, names.Count);
        for (int i = 0; i < names.Count; i++)
        {
            Assert.IsNotNull(bank.Find(names[i]), "Missing bank entry: " + names[i]);
        }
    }

    [Test]
    public void EveryCueHasGroupAndClipsOrFallback()
    {
        SoundBank bank = Bank();
        for (int i = 0; i < bank.Cues.Count; i++)
        {
            SoundCue cue = bank.Cues[i];
            Assert.IsTrue(cue.clips.Length > 0 || cue.fallback != SynthFallback.None, "No clips or fallback: " + cue.name);
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundGroup), cue.group), cue.name);
        }
    }

    [Test]
    public void FootstepsJitterWithinSpec()
    {
        SoundBank bank = Bank();
        string[] steps = { SoundCues.FootstepGrass, SoundCues.FootstepDirt, SoundCues.FootstepRock, SoundCues.FootstepWater };
        for (int i = 0; i < steps.Length; i++)
        {
            SoundCue cue = bank.Find(steps[i]);
            Assert.AreEqual(new Vector2(0.95f, 1.05f), cue.pitch, steps[i]);
            Assert.AreEqual(1.5f, cue.volumeJitterDb, 1e-5f, steps[i]);
        }
    }

    [Test]
    public void PriorityCuesFlagged()
    {
        SoundBank bank = Bank();
        HashSet<string> expected = new HashSet<string>
        {
            SoundCues.BeaconIgnite, SoundCues.StingerLose, SoundCues.ShadeSteal, SoundCues.LanternDeathGutter
        };
        for (int i = 0; i < bank.Cues.Count; i++)
        {
            Assert.AreEqual(expected.Contains(bank.Cues[i].name), bank.Cues[i].priority, bank.Cues[i].name);
        }
    }

    [Test]
    public void MixerHasUiGroupAndDuckParams()
    {
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        Assert.IsNotNull(mixer);
        AudioMixerGroup[] ui = mixer.FindMatchingGroups("Master/SFX/UI");
        Assert.AreEqual(1, ui.Length, "UI group under SFX");
        float value;
        Assert.IsTrue(mixer.GetFloat("MusicDuck", out value));
        Assert.IsTrue(mixer.GetFloat("AmbienceDuck", out value));
        Assert.IsTrue(mixer.GetFloat("TensionDuck", out value));
        Assert.IsTrue(mixer.GetFloat("MusicVol", out value));
        Assert.IsTrue(mixer.GetFloat("SfxVol", out value));
        Assert.IsNotNull(mixer.FindSnapshot("Ducked"));
    }
}
}
