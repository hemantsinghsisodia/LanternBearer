using System;
using UnityEngine;

namespace LanternKeeper
{
[CreateAssetMenu(fileName = "MusicLibrary", menuName = "Lantern Keeper/Music Library")]
public class MusicLibrary : ScriptableObject
{
    [Serializable]
    public class IslandMusic
    {
        public string scene;
        public AudioClip track;
        public AudioClip tension;
        // Island 5 only: the summit hush plays this once the keeper reaches the top. Null elsewhere.
        public AudioClip hush;
    }

    public AudioClip menuTrack;
    public IslandMusic[] islands;
    public AudioClip stingerBeaconLit;
    public AudioClip stingerWin;
    public AudioClip stingerLose;
    // Island 5 ending cinematic: unlooped, plays from the lamp ignition.
    public AudioClip finaleCue;

    public IslandMusic For(string scene)
    {
        if (islands == null)
        {
            return null;
        }

        for (int i = 0; i < islands.Length; i++)
        {
            if (islands[i] != null && islands[i].scene == scene && islands[i].track != null)
            {
                return islands[i];
            }
        }

        return null;
    }
}
}
