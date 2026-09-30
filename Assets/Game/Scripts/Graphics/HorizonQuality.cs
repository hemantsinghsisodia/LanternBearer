using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Applies distant mountains, extra ridges, horizon haze, and the camera far clip.
// Saved active states, haze values, and far clips are cached once and are the Medium baseline.
// Haze changes go to a runtime material instance. The saved material is never edited.
public class HorizonQuality : MonoBehaviour
{
    const float MinLowFar = 700f;
    const float FineBandScale = 2f;
    const string BandProperty = "_BandScale";

    static readonly int BandScaleId = Shader.PropertyToID(BandProperty);

    [SerializeField] GameObject[] mountainRanges;
    [SerializeField] GameObject[] extraRidges;
    [SerializeField] GameObject[] hazeLayers;
    [SerializeField] Camera[] cameras;

    static bool fallbackWarned;

    bool baselineCached;
    bool searched;
    bool[] rangeActive;
    bool[] ridgeActive;
    bool[] hazeActive;
    Renderer[] hazeRenderers;
    Material[] hazeInstances;
    float[] hazeBand;
    Camera[] cameraSlots;
    float[] cameraFar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= Ensure;
        SceneManager.sceneLoaded += Ensure;
        Ensure(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void Ensure(Scene scene, LoadSceneMode mode)
    {
        if (!IsHorizonScene(scene) || FindAnyObjectByType<HorizonQuality>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject[] ranges = FindMountainRanges();
        GameObject[] haze = FindHazeLayers();
        if (ranges.Length == 0 && haze.Length == 0)
        {
            return;
        }

        GameObject host = GameObject.Find("GraphicsAppliers");
        if (host == null)
        {
            host = new GameObject("GraphicsAppliers");
        }

        bool wasActive = host.activeSelf;
        host.SetActive(false);
        HorizonQuality quality = host.AddComponent<HorizonQuality>();
        quality.mountainRanges = ranges;
        quality.extraRidges = FindExtraRidges();
        quality.hazeLayers = haze;
        quality.cameras = FindGameplayCameras();
        host.SetActive(wasActive);
    }

    public static GameObject[] FindMountainRanges()
    {
        return FindHorizonChildren(0);
    }

    public static GameObject[] FindExtraRidges()
    {
        return FindHorizonChildren(1);
    }

    public static GameObject[] FindHazeLayers()
    {
        return FindHorizonChildren(2);
    }

    public static Camera[] FindGameplayCameras()
    {
        Camera[] all = FindObjectsByType<Camera>(FindObjectsInactive.Include);
        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (IsGameplayCamera(all[i]))
            {
                count++;
            }
        }

        Camera[] found = new Camera[count];
        int cursor = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (!IsGameplayCamera(all[i]))
            {
                continue;
            }

            found[cursor] = all[i];
            cursor++;
        }

        return found;
    }

    static bool IsHorizonScene(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded && (scene.name.StartsWith("Island") || scene.name == "MainMenu");
    }

    static GameObject[] FindHorizonChildren(int kind)
    {
        GameObject horizon = GameObject.Find("Horizon");
        if (horizon == null)
        {
            return new GameObject[0];
        }

        Transform root = horizon.transform;
        int count = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            if (Matches(root.GetChild(i).name, kind))
            {
                count++;
            }
        }

        GameObject[] found = new GameObject[count];
        int cursor = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!Matches(child.name, kind))
            {
                continue;
            }

            found[cursor] = child.gameObject;
            cursor++;
        }

        SortByName(found);
        return found;
    }

    static bool Matches(string objectName, int kind)
    {
        if (kind == 1)
        {
            return objectName.StartsWith("Ridge");
        }

        if (kind == 2)
        {
            return objectName.StartsWith("Haze");
        }

        return objectName.StartsWith("Range");
    }

    static void SortByName(GameObject[] found)
    {
        for (int i = 1; i < found.Length; i++)
        {
            GameObject item = found[i];
            int j = i;
            while (j > 0 && string.CompareOrdinal(found[j - 1].name, item.name) > 0)
            {
                found[j] = found[j - 1];
                j--;
            }

            found[j] = item;
        }
    }

    static bool IsGameplayCamera(Camera camera)
    {
        return camera != null && camera.CompareTag("MainCamera");
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveRefs();
        CacheBaseline();
        GraphicsQuality.QualityChanged += Apply;
        Apply(GraphicsQuality.Profile);
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= Apply;
    }

    void OnDestroy()
    {
        if (!Application.isPlaying || hazeInstances == null)
        {
            return;
        }

        for (int i = 0; i < hazeInstances.Length; i++)
        {
            Material instance = hazeInstances[i];
            if (instance == null)
            {
                continue;
            }

            Renderer renderer = hazeRenderers != null && i < hazeRenderers.Length ? hazeRenderers[i] : null;
            Material shared = renderer != null ? renderer.sharedMaterial : null;
            if (instance != shared)
            {
                Destroy(instance);
            }
        }
    }

    void ResolveRefs()
    {
        bool mountainsReady = HasObjects(mountainRanges);
        bool hazeReady = HasObjects(hazeLayers);
        bool camerasReady = HasCameras(cameras);
        bool ridgesReady = HasObjects(extraRidges) || FindExtraRidges().Length == 0;
        if ((mountainsReady && hazeReady && camerasReady && ridgesReady) || searched)
        {
            return;
        }

        searched = true;
        if (!mountainsReady)
        {
            mountainRanges = FindMountainRanges();
        }

        if (!ridgesReady)
        {
            extraRidges = FindExtraRidges();
        }

        if (!hazeReady)
        {
            hazeLayers = FindHazeLayers();
        }

        if (!camerasReady)
        {
            cameras = FindGameplayCameras();
        }

        if (!fallbackWarned)
        {
            fallbackWarned = true;
            Debug.LogWarning("HorizonQuality is missing horizon references; using the Horizon objects in the scene.", this);
        }
    }

    static bool HasObjects(GameObject[] objects)
    {
        if (objects == null || objects.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    static bool HasCameras(Camera[] listed)
    {
        if (listed == null || listed.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < listed.Length; i++)
        {
            if (listed[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    void CacheBaseline()
    {
        if (baselineCached)
        {
            return;
        }

        rangeActive = CaptureActive(mountainRanges);
        ridgeActive = CaptureActive(extraRidges);
        hazeActive = CaptureActive(hazeLayers);
        CacheHazeMaterials();
        CacheCameras(CollectCameras());
        baselineCached = true;
    }

    static bool[] CaptureActive(GameObject[] objects)
    {
        if (objects == null)
        {
            return new bool[0];
        }

        bool[] active = new bool[objects.Length];
        for (int i = 0; i < objects.Length; i++)
        {
            active[i] = objects[i] != null && objects[i].activeSelf;
        }

        return active;
    }

    void CacheHazeMaterials()
    {
        int count = hazeLayers != null ? hazeLayers.Length : 0;
        hazeRenderers = new Renderer[count];
        hazeInstances = new Material[count];
        hazeBand = new float[count];
        for (int i = 0; i < count; i++)
        {
            GameObject layer = hazeLayers[i];
            if (layer == null)
            {
                continue;
            }

            Renderer renderer = layer.GetComponent<Renderer>();
            hazeRenderers[i] = renderer;
            if (renderer == null)
            {
                continue;
            }

            Material shared = renderer.sharedMaterial;
            hazeBand[i] = ReadBand(shared);
            if (shared == null)
            {
                continue;
            }

            Material instance = new Material(shared);
            instance.name = shared.name;
            renderer.material = instance;
            hazeInstances[i] = instance;
        }
    }

    static float ReadBand(Material material)
    {
        if (material == null || !material.HasProperty(BandScaleId))
        {
            return 1f;
        }

        return material.GetFloat(BandScaleId);
    }

    void CacheCameras(Camera[] listed)
    {
        cameraSlots = listed;
        cameraFar = new float[listed.Length];
        for (int i = 0; i < listed.Length; i++)
        {
            cameraFar[i] = listed[i] != null ? listed[i].farClipPlane : 900f;
        }
    }

    Camera[] CollectCameras()
    {
        Camera main = Camera.main;
        int extra = main != null && !ContainsCamera(cameras, main) ? 1 : 0;
        Camera[] tagged = FindGameplayCameras();
        int count = (cameras != null ? cameras.Length : 0) + extra;
        for (int i = 0; i < tagged.Length; i++)
        {
            if (!ContainsCamera(cameras, tagged[i]) && tagged[i] != main)
            {
                count++;
            }
        }

        Camera[] listed = new Camera[count];
        int cursor = 0;
        if (cameras != null)
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                listed[cursor] = cameras[i];
                cursor++;
            }
        }

        if (extra == 1)
        {
            listed[cursor] = main;
            cursor++;
        }

        for (int i = 0; i < tagged.Length; i++)
        {
            if (ContainsCamera(listed, tagged[i]))
            {
                continue;
            }

            listed[cursor] = tagged[i];
            cursor++;
        }

        return listed;
    }

    static bool ContainsCamera(Camera[] listed, Camera camera)
    {
        if (listed == null || camera == null)
        {
            return false;
        }

        for (int i = 0; i < listed.Length; i++)
        {
            if (listed[i] == camera)
            {
                return true;
            }
        }

        return false;
    }

    void Apply(GraphicsProfile profile)
    {
        if (!Application.isPlaying || profile == null)
        {
            return;
        }

        ResolveRefs();
        CacheBaseline();
        if (!baselineCached)
        {
            return;
        }

        bool medium = profile.level == GraphicsLevel.Medium;
        ApplyRanges(profile, medium);
        ApplyRidges(profile, medium);
        ApplyHaze(profile, medium);
        ApplyCameras(profile, medium);
        LogApplied(profile);
    }

    void ApplyRanges(GraphicsProfile profile, bool medium)
    {
        if (mountainRanges == null)
        {
            return;
        }

        for (int i = 0; i < mountainRanges.Length; i++)
        {
            // Low keeps the nearest ring (Range0) so the shore still meets the sky.
            // The farther rings are the hidden far mountain range.
            bool keepSaved = medium || profile.distantMountains || i == 0;
            SetActive(mountainRanges[i], keepSaved && Baseline(rangeActive, i));
        }
    }

    void ApplyRidges(GraphicsProfile profile, bool medium)
    {
        if (extraRidges == null)
        {
            return;
        }

        for (int i = 0; i < extraRidges.Length; i++)
        {
            bool on = medium ? Baseline(ridgeActive, i) : profile.extraRidges;
            SetActive(extraRidges[i], on);
        }
    }

    void ApplyHaze(GraphicsProfile profile, bool medium)
    {
        if (hazeLayers == null)
        {
            return;
        }

        int keep = profile.hazeLayers < 1 ? 1 : profile.hazeLayers;
        for (int i = 0; i < hazeLayers.Length; i++)
        {
            bool on = medium ? Baseline(hazeActive, i) : i < keep;
            SetActive(hazeLayers[i], on);
            if (hazeInstances == null || i >= hazeInstances.Length || hazeInstances[i] == null)
            {
                continue;
            }

            float band = medium || !profile.finerHaze ? hazeBand[i] : FineBandScale;
            if (!Mathf.Approximately(hazeInstances[i].GetFloat(BandScaleId), band))
            {
                hazeInstances[i].SetFloat(BandScaleId, band);
            }
        }
    }

    void ApplyCameras(GraphicsProfile profile, bool medium)
    {
        Camera[] listed = CollectCameras();
        if (!SameCameras(listed))
        {
            float[] previousFar = cameraFar;
            Camera[] previous = cameraSlots;
            cameraSlots = listed;
            cameraFar = new float[listed.Length];
            for (int i = 0; i < listed.Length; i++)
            {
                int known = IndexOf(previous, listed[i]);
                cameraFar[i] = known >= 0 && previousFar != null && known < previousFar.Length
                    ? previousFar[known]
                    : (listed[i] != null ? listed[i].farClipPlane : 900f);
            }
        }

        GraphicsProfile reference = MediumProfile();
        float mediumFar = reference != null ? reference.cameraFarPlane : 900f;
        for (int i = 0; i < cameraSlots.Length; i++)
        {
            Camera camera = cameraSlots[i];
            if (camera == null)
            {
                continue;
            }

            float far = cameraFar[i];
            if (!medium)
            {
                far = ScaleFar(cameraFar[i], mediumFar, profile.cameraFarPlane);
                if (!profile.distantMountains)
                {
                    far = Mathf.Max(far, MinLowFar);
                }
            }

            if (!Mathf.Approximately(camera.farClipPlane, far))
            {
                camera.farClipPlane = far;
            }
        }
    }

    bool SameCameras(Camera[] listed)
    {
        if (cameraSlots == null || listed == null || listed.Length != cameraSlots.Length)
        {
            return false;
        }

        for (int i = 0; i < listed.Length; i++)
        {
            if (listed[i] != cameraSlots[i])
            {
                return false;
            }
        }

        return true;
    }

    int IndexOf(Camera[] listed, Camera camera)
    {
        if (listed == null || camera == null)
        {
            return -1;
        }

        for (int i = 0; i < listed.Length; i++)
        {
            if (listed[i] == camera)
            {
                return i;
            }
        }

        return -1;
    }

    static float ScaleFar(float baseline, float mediumValue, float presetValue)
    {
        if (mediumValue <= 0.0001f)
        {
            return baseline;
        }

        if (Mathf.Approximately(baseline, mediumValue))
        {
            return presetValue;
        }

        float scaled = baseline * (presetValue / mediumValue);
        return scaled;
    }

    static GraphicsProfile MediumProfile()
    {
        GraphicsProfileSet set = Resources.Load<GraphicsProfileSet>("GraphicsProfileSet");
        if (set == null)
        {
            return null;
        }

        return set.Get(GraphicsLevel.Medium);
    }

    static bool Baseline(bool[] active, int index)
    {
        return active != null && index >= 0 && index < active.Length && active[index];
    }

    static void SetActive(GameObject target, bool on)
    {
        if (target != null && target.activeSelf != on)
        {
            target.SetActive(on);
        }
    }

    void LogApplied(GraphicsProfile profile)
    {
        string scene = gameObject.scene.name;
        string ranges = FormatActive(mountainRanges);
        string ridges = FormatActive(extraRidges);
        int hazeOn = 0;
        int hazeCount = hazeLayers != null ? hazeLayers.Length : 0;
        string bands = "";
        string sharedBands = "";
        if (hazeLayers != null)
        {
            for (int i = 0; i < hazeLayers.Length; i++)
            {
                if (hazeLayers[i] != null && hazeLayers[i].activeSelf)
                {
                    hazeOn++;
                }

                float band = hazeInstances != null && i < hazeInstances.Length && hazeInstances[i] != null
                    ? hazeInstances[i].GetFloat(BandScaleId)
                    : 0f;
                float shared = hazeRenderers != null && i < hazeRenderers.Length ? ReadBand(SharedOf(hazeRenderers[i])) : 0f;
                if (i > 0)
                {
                    bands += ",";
                    sharedBands += ",";
                }

                bands += band.ToString("0.###");
                sharedBands += shared.ToString("0.###");
            }
        }

        string fars = "";
        if (cameraSlots != null)
        {
            for (int i = 0; i < cameraSlots.Length; i++)
            {
                if (i > 0)
                {
                    fars += ",";
                }

                Camera camera = cameraSlots[i];
                fars += camera != null ? camera.name + "=" + camera.farClipPlane.ToString("0.###") : "null";
            }
        }

        Debug.Log(
            "HorizonQuality " + scene
            + " " + profile.level
            + " ranges " + ranges
            + " ridges " + ridges
            + " hazeActive " + hazeOn + "/" + hazeCount
            + " band " + bands
            + " sharedBand " + sharedBands
            + " far " + fars
            + " tris " + TriangleSummary(),
            this);
    }

    static Material SharedOf(Renderer renderer)
    {
        return renderer != null ? renderer.sharedMaterial : null;
    }

    static string FormatActive(GameObject[] objects)
    {
        if (objects == null || objects.Length == 0)
        {
            return "none";
        }

        string text = "";
        for (int i = 0; i < objects.Length; i++)
        {
            if (i > 0)
            {
                text += ",";
            }

            GameObject item = objects[i];
            text += item != null ? item.name + ":" + (item.activeSelf ? "1" : "0") : "null";
        }

        return text;
    }

    string TriangleSummary()
    {
        return "ranges " + TriangleCount(mountainRanges) + " ridges " + TriangleCount(extraRidges) + " haze " + TriangleCount(hazeLayers);
    }

    static int TriangleCount(GameObject[] objects)
    {
        if (objects == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] == null)
            {
                continue;
            }

            MeshFilter filter = objects[i].GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh != null)
            {
                total += (int)(mesh.GetIndexCount(0) / 3);
            }
        }

        return total;
    }
}
}
