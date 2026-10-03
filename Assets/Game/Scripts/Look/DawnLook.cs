using UnityEngine;

namespace LanternKeeper
{
// Dawn colours shared by every island. Plain data: nothing reads it at runtime yet.
[CreateAssetMenu(fileName = "DawnLook", menuName = "Lantern Keeper/Dawn Look")]
public class DawnLook : ScriptableObject
{
    public Color dawnTop = LookPalette.FromHex("7E9CCB");
    public Color dawnHorizon = LookPalette.FromHex("F4B38A");
    public Color dawnGround = LookPalette.FromHex("C98A7A");
    public Color dawnLight = LookPalette.FromHex("FFD9B0");
    public float dawnAmbientBoost = 0.28f;
}
}
