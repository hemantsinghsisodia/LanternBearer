using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
// Builds Assets/Game/Audio/SoundBank.asset from the cue table below and the clips found under Assets/Game/Audio.
// A clip belongs to a cue when its file is <Cue>_<n>.ogg with the dot replaced by an underscore (Footstep_Grass_1.ogg).
// Cues with no files keep their synth fallback. The result is deterministic: clips are sorted by path.
public static class SoundBankBuilder
{
    public const string BankPath = "Assets/Game/Audio/SoundBank.asset";
    const string AudioRoot = "Assets/Game/Audio";
    const float TempVolume = 0.4f;

    struct Spec
    {
        public string name;
        public SoundGroup group;
        public float volume;
        public bool spatial;
        public SynthFallback fallback;
        public bool priority;
        public float pitchMin;
        public float pitchMax;
        public float jitterDb;
        public float maxDistance;
        public bool synthOnly;
    }

    static Spec S(string name, SoundGroup group, float volume, bool spatial, SynthFallback fallback)
    {
        Spec spec = new Spec();
        spec.name = name;
        spec.group = group;
        spec.volume = volume;
        spec.spatial = spatial;
        spec.fallback = fallback;
        spec.pitchMin = 1f;
        spec.pitchMax = 1f;
        spec.maxDistance = 500f;
        return spec;
    }

    static Spec Step(string name)
    {
        Spec spec = S(name, SoundGroup.Sfx, 0.32f, true, SynthFallback.Footstep);
        spec.pitchMin = 0.95f;
        spec.pitchMax = 1.05f;
        spec.jitterDb = 1.5f;
        return spec;
    }

    static Spec Synth(Spec spec)
    {
        spec.synthOnly = true;
        return spec;
    }

    static Spec Prio(Spec spec)
    {
        spec.priority = true;
        return spec;
    }

    static Spec Dist(Spec spec, float maxDistance)
    {
        spec.maxDistance = maxDistance;
        return spec;
    }

    // Cues with no synthesis of their own carry a temporary quiet chime until their clips arrive (Task 7 removes it).
    static Spec Temp(string name, SoundGroup group, bool spatial, bool priority)
    {
        Spec spec = S(name, group, TempVolume, spatial, SynthFallback.Chime);
        spec.priority = priority;
        return spec;
    }

    static List<Spec> Table()
    {
        List<Spec> t = new List<Spec>();
        t.Add(Step(SoundCues.FootstepGrass));
        t.Add(Step(SoundCues.FootstepDirt));
        t.Add(Step(SoundCues.FootstepRock));
        t.Add(Step(SoundCues.FootstepWater));

        // Temporary soft body thud until the real hit sound arrives.
        t.Add(S(SoundCues.KeeperHit, SoundGroup.Sfx, 0.35f, true, SynthFallback.Footstep));
        t.Add(Temp(SoundCues.KeeperInteract, SoundGroup.Sfx, true, false));

        t.Add(S(SoundCues.LanternCrackle, SoundGroup.Sfx, 1f, false, SynthFallback.Crackle));
        t.Add(Synth(S(SoundCues.LanternHeartbeat, SoundGroup.Sfx, 0.45f, false, SynthFallback.Heartbeat)));
        t.Add(Temp(SoundCues.LanternRefuel, SoundGroup.Sfx, false, false));
        t.Add(Temp(SoundCues.LanternSputter, SoundGroup.Sfx, false, false));
        t.Add(Prio(S(SoundCues.LanternDeathGutter, SoundGroup.Sfx, 0.65f, false, SynthFallback.Dying)));

        t.Add(S(SoundCues.FireflyChime, SoundGroup.Sfx, 0.8f, true, SynthFallback.Chime));
        t.Add(Synth(S(SoundCues.FireflyArrive, SoundGroup.Sfx, 0.5f, true, SynthFallback.FireflyArrive)));

        t.Add(S(SoundCues.MothFlutter, SoundGroup.Sfx, 1f, true, SynthFallback.MothFlutter));
        t.Add(Synth(S(SoundCues.MothWhisper, SoundGroup.Sfx, 1f, true, SynthFallback.MothWhisper)));

        t.Add(Synth(S(SoundCues.ShadeDrone, SoundGroup.Sfx, 1f, true, SynthFallback.ShadeDrone)));
        t.Add(Temp(SoundCues.ShadeSteal, SoundGroup.Sfx, false, true));

        t.Add(Prio(S(SoundCues.BeaconIgnite, SoundGroup.Sfx, 0.9f, true, SynthFallback.Whoomp)));
        t.Add(Dist(S(SoundCues.BeaconFire, SoundGroup.Sfx, 0.5f, true, SynthFallback.Crackle), 10f));
        t.Add(S(SoundCues.BeaconWhoosh, SoundGroup.Sfx, 0.9f, true, SynthFallback.Beacon));
        t.Add(S(SoundCues.BeaconFizzle, SoundGroup.Sfx, 0.7f, false, SynthFallback.Fizzle));

        t.Add(S(SoundCues.AmbienceIsland1, SoundGroup.Ambience, 0.22f, false, SynthFallback.Ambience));
        t.Add(S(SoundCues.AmbienceIsland2, SoundGroup.Ambience, 0.22f, false, SynthFallback.Ambience));
        t.Add(S(SoundCues.AmbienceIsland3, SoundGroup.Ambience, 0.22f, false, SynthFallback.Ambience));
        t.Add(S(SoundCues.AmbienceIsland4, SoundGroup.Ambience, 0.22f, false, SynthFallback.Ambience));
        t.Add(S(SoundCues.AmbienceTide, SoundGroup.Ambience, 1f, false, SynthFallback.Surf));
        t.Add(S(SoundCues.AmbienceRain, SoundGroup.Ambience, 1f, false, SynthFallback.Rain));
        t.Add(S(SoundCues.AmbienceWindBed, SoundGroup.Ambience, 1f, false, SynthFallback.WindBed));
        t.Add(S(SoundCues.AmbienceGust, SoundGroup.Ambience, 1f, false, SynthFallback.WindHowl));

        t.Add(S(SoundCues.ThunderCrack, SoundGroup.Ambience, 1f, false, SynthFallback.ThunderCrack));
        t.Add(S(SoundCues.ThunderRumble, SoundGroup.Ambience, 1f, false, SynthFallback.ThunderRumble));
        t.Add(S(SoundCues.WaterSplash, SoundGroup.Sfx, 0.85f, true, SynthFallback.Splash));

        t.Add(Temp(SoundCues.UiHover, SoundGroup.UI, false, false));
        t.Add(Temp(SoundCues.UiClick, SoundGroup.UI, false, false));
        t.Add(Temp(SoundCues.UiBack, SoundGroup.UI, false, false));
        t.Add(Temp(SoundCues.UiPauseOpen, SoundGroup.UI, false, false));
        t.Add(Temp(SoundCues.UiPauseClose, SoundGroup.UI, false, false));
        t.Add(Temp(SoundCues.UiPageTurn, SoundGroup.UI, false, false));
        t.Add(Temp(SoundCues.UiToggle, SoundGroup.UI, false, false));

        t.Add(Temp(SoundCues.StingerBeaconLit, SoundGroup.Music, false, false));
        t.Add(Temp(SoundCues.StingerWin, SoundGroup.Music, false, false));
        t.Add(Prio(Temp(SoundCues.StingerLose, SoundGroup.Music, false, false)));
        return t;
    }

    [MenuItem("Lantern Keeper/Build Sound Bank")]
    public static SoundBank Build()
    {
        Dictionary<string, List<string>> files = FindFiles();
        List<SoundCue> cues = new List<SoundCue>();
        List<Spec> table = Table();
        for (int i = 0; i < table.Count; i++)
        {
            Spec spec = table[i];
            SoundCue cue = new SoundCue();
            cue.name = spec.name;
            cue.group = spec.group;
            cue.volume = spec.volume;
            cue.pitch = new Vector2(spec.pitchMin, spec.pitchMax);
            cue.volumeJitterDb = spec.jitterDb;
            cue.spatial = spec.spatial;
            cue.minDistance = 1f;
            cue.maxDistance = spec.maxDistance;
            cue.rolloff = AudioRolloffMode.Logarithmic;
            cue.priority = spec.priority;
            cue.synthOnly = spec.synthOnly;
            cue.fallback = spec.fallback;
            List<AudioClip> clips = new List<AudioClip>();
            List<string> paths;
            if (files.TryGetValue(spec.name, out paths))
            {
                paths.Sort(System.StringComparer.Ordinal);
                for (int p = 0; p < paths.Count; p++)
                {
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[p]);
                    if (clip != null)
                    {
                        clips.Add(clip);
                    }
                }
            }

            cue.clips = clips.ToArray();
            cues.Add(cue);
        }

        SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<SoundBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
        }

        bank.SetCues(cues);
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        Debug.Log("Built sound bank with " + cues.Count + " cues at " + BankPath);
        return bank;
    }

    // Maps cue name to the files that feed it (<Cue with underscores>_<n>.ogg anywhere under Assets/Game/Audio).
    static Dictionary<string, List<string>> FindFiles()
    {
        Dictionary<string, List<string>> map = new Dictionary<string, List<string>>();
        if (!AssetDatabase.IsValidFolder(AudioRoot))
        {
            return map;
        }

        Dictionary<string, string> byStem = new Dictionary<string, string>();
        List<Spec> table = Table();
        for (int i = 0; i < table.Count; i++)
        {
            byStem[table[i].name.Replace('.', '_')] = table[i].name;
        }

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!path.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string file = Path.GetFileNameWithoutExtension(path);
            int cut = file.LastIndexOf('_');
            if (cut <= 0)
            {
                continue;
            }

            int number;
            string stem = file.Substring(0, cut);
            string cueName;
            if (!int.TryParse(file.Substring(cut + 1), out number) || !byStem.TryGetValue(stem, out cueName))
            {
                continue;
            }

            List<string> list;
            if (!map.TryGetValue(cueName, out list))
            {
                list = new List<string>();
                map[cueName] = list;
            }

            list.Add(path);
        }

        return map;
    }
}
}
