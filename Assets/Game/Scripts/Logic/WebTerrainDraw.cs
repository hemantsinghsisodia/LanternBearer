using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// WebGL only. With Terrain.drawInstanced on, Unity builds the terrain shaders with an instance array whose size follows the number
// of visible terrain patches (TerrainArray[32], [64], [96] ... [256] in the GLSL). Every time the camera sees enough more patches to
// reach the next size, the browser re-links the terrain programs (the shadow caster, the depth pass and the two big lit passes). A link
// of the 65 KB lit shader costs 50 to 500 ms on the single browser thread: a hitch every few seconds while walking.
// Drawing the patches one by one keeps one program per pass for good. Windows and the Editor are unchanged.
public static class WebTerrainDraw
{
    // Test hook: the measurement probe can turn the fix off to compare.
    public static bool Disabled;

    public static int Apply(Terrain[] terrains)
    {
        if (Disabled || terrains == null)
        {
            return 0;
        }

        int changed = 0;
        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i] != null && terrains[i].drawInstanced)
            {
                terrains[i].drawInstanced = false;
                changed++;
            }
        }

        return changed;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(Terrain.activeTerrains);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply(Terrain.activeTerrains);
    }
#endif
}
}
