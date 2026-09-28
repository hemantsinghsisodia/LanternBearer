using System;
using UnityEngine;

namespace LanternKeeper
{
public enum BiomeCategory
{
    Tree,
    Sapling,
    Deadwood,
    Rock,
    Undergrowth,
    GroundCover,
    Flower,
    SetPiece
}

[Serializable]
public class BiomeEntry
{
    public GameObject prefab;
    public BiomeCategory category;
    public int count = 1;
    public Vector2 scaleRange = Vector2.one;
    public bool alignToSlope;
    public bool limitHeight;
    public float minHeight;
    public float maxHeight = 1f;
    public bool limitSlope;
    public float minSlope;
    public float maxSlope = 1f;
}

[CreateAssetMenu(fileName = "Biome", menuName = "Lantern Keeper/Biome")]
public class Biome : ScriptableObject
{
    public BiomeEntry[] entries = new BiomeEntry[0];
}
}
