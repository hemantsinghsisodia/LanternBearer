using UnityEngine;

namespace LanternKeeper
{
[CreateAssetMenu(fileName = "MusicLibrary", menuName = "Lantern Keeper/Music Library")]
public class MusicLibrary : ScriptableObject
{
    public AudioClip menuTrack;
    public AudioClip islandTrack;
}
}
