using UnityEngine;
using UnityEngine.Audio;

namespace LanternKeeper
{
// Shared helpers for the storm sources: mixer routing the way Tide does it, and the M key mute.
public static class StormAudio
{
    // 0 while the player has muted with M, otherwise 1. Multiply every storm volume by it.
    public static float MuteGain => MusicPlayer.IsMuted ? 0f : 1f;

    public static AudioSource Make(GameObject host, AudioClip clip, bool loop, float spatialBlend)
    {
        AudioSource source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = spatialBlend;
        source.volume = 0f;
        source.clip = clip;
        if (spatialBlend > 0f)
        {
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 22f;
        }

        return source;
    }

    // Returns true once the source sits on its mixer group, so callers can stop retrying.
    public static bool Route(AudioSource source, bool ambience)
    {
        AudioManager manager = AudioManager.Instance;
        if (source == null || manager == null)
        {
            return false;
        }

        AudioMixerGroup group = ambience ? manager.AmbienceGroup : manager.SfxGroup;
        if (group == null)
        {
            return false;
        }

        source.outputAudioMixerGroup = group;
        return true;
    }
}
}
