using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
// Plain data asset: the fixed camera poses used for the look baseline of one island.
public class LookShotList : ScriptableObject
{
    public string levelId;
    public List<LookShot> shots = new List<LookShot>();
}
}
