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
    }

    public AudioClip menuTrack;
    // Fallback for an island with no entry in `islands`.
    public AudioClip islandTrack;
    public IslandMusic[] islands;
    public AudioClip stingerBeaconLit;
    public AudioClip stingerWin;
    public AudioClip stingerLose;

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
