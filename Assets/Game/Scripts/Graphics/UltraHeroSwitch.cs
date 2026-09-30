using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Trees carry the normal LOD meshes and a hidden high-detail child.
// Ultra shows the hero mesh. Every other preset keeps the LODGroup.
public class UltraHeroSwitch : MonoBehaviour
{
    [SerializeField] LODGroup lodGroup;
    [SerializeField] GameObject hero;

    GameObject[] lodObjects;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyAll(IsUltra(GraphicsQuality.Profile));
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAll(IsUltra(GraphicsQuality.Profile));
    }

    public static void ApplyAll(bool ultra)
    {
        UltraHeroSwitch[] switches = Object.FindObjectsByType<UltraHeroSwitch>(FindObjectsInactive.Include);
        for (int i = 0; i < switches.Length; i++)
        {
            switches[i].Apply(ultra);
        }
    }

    void OnEnable()
    {
        GraphicsQuality.QualityChanged += OnQuality;
        Apply(IsUltra(GraphicsQuality.Profile));
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= OnQuality;
    }

    void OnQuality(GraphicsProfile profile)
    {
        Apply(IsUltra(profile));
    }

    public void Apply(bool ultra)
    {
        Cache();
        if (hero != null && hero.activeSelf != ultra)
        {
            hero.SetActive(ultra);
        }

        if (lodObjects != null)
        {
            for (int i = 0; i < lodObjects.Length; i++)
            {
                if (lodObjects[i] != null && lodObjects[i].activeSelf == ultra)
                {
                    lodObjects[i].SetActive(!ultra);
                }
            }
        }

        if (lodGroup != null)
        {
            lodGroup.enabled = !ultra;
            if (!ultra)
            {
                lodGroup.ForceLOD(-1);
            }
        }
    }

    void Cache()
    {
        if (lodGroup == null)
        {
            lodGroup = GetComponent<LODGroup>();
        }

        if (hero == null)
        {
            Transform child = transform.Find("Hero");
            if (child != null)
            {
                hero = child.gameObject;
            }
        }

        if (lodGroup == null || lodObjects != null)
        {
            return;
        }

        LOD[] lods = lodGroup.GetLODs();
        List<GameObject> found = new List<GameObject>();
        for (int i = 0; i < lods.Length; i++)
        {
            Renderer[] renderers = lods[i].renderers;
            if (renderers == null)
            {
                continue;
            }

            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] == null)
                {
                    continue;
                }

                GameObject host = renderers[r].gameObject;
                if (!found.Contains(host))
                {
                    found.Add(host);
                }
            }
        }

        lodObjects = found.ToArray();
    }

    static bool IsUltra(GraphicsProfile profile)
    {
        return profile != null && profile.level == GraphicsLevel.Ultra;
    }
}
}
