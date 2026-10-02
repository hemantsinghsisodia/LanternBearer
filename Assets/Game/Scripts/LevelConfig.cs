using UnityEngine;

namespace LanternKeeper
{
[CreateAssetMenu(fileName = "Level", menuName = "Lantern Keeper/Level Config")]
public class LevelConfig : ScriptableObject
{
    public string levelId = "island1";
    public string displayName = "Island 1";
    public string islandTitle = "";
    public string sceneName = "Island1";
    public float islandRadius = 28f;
    public float hillHeight = 9f;
    public int seed = 1101;
    public int beaconCount = 5;
    public int fireflyCount = 14;
    public int mothCount = 4;
    public int hiddenPathCount = 1;
    public Color fogColor = new Color(0.4f, 0.52f, 0.56f, 1f);
    public float fogDensity = 0.012f;
    public int grassDetailDensity = 8;
    public string nextLevelScene = "";
    public Biome biome;
    public bool hasCliff;
    public float tideAmplitude;
    public float tidePeriod = 90f;
    public float tidePhase;
    public string[] logEntries = new string[0];
    public string[] introLines = new string[0];
    public float windStrength;
    public int shadeCount;
    public bool lightning;
    public bool ridges;
}
}
