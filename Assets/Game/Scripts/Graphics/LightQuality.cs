using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Applies beacon shadows, the Low glow-light cap, and the particle count multiplier.
// Saved light and particle values are cached once and are the Medium baseline.
// The lantern shadow and the chest fill light stay with KeeperQuality.
public class LightQuality : MonoBehaviour
{
    const string BeaconLightName = "BeaconLight";
    const string GlowName = "Glow";
    const float GlowInterval = 0.25f;

    sealed class GlowSlot
    {
        public Light light;
        public bool gameplayEnabled;
        public bool allowed;
    }

    struct BeaconBaseline
    {
        public LightShadows shadows;
        public bool hadData;
        public int tier;
        public bool addedData;
    }

    [SerializeField] Light[] beaconLights;
    [SerializeField] Light[] fireflyLights;

    static readonly List<GlowSlot> moths = new List<GlowSlot>(8);
    static readonly List<GlowSlot> flies = new List<GlowSlot>(16);
    static bool mothCapActive;
    static bool flyCapActive;
    static bool fallbackWarned;
    static bool playerWarned;
    static LightQuality live;

    bool baselineCached;
    bool searched;
    bool particlesTracked;
    bool playerSearched;
    float glowTimer;
    Transform player;
    BeaconBaseline[] beaconBaseline;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= Ensure;
        SceneManager.sceneLoaded += Ensure;
        Ensure(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void Ensure(Scene scene, LoadSceneMode mode)
    {
        if (!IsIsland(scene) || FindAnyObjectByType<LightQuality>() != null)
        {
            return;
        }

        Light[] beacons = FindBeaconLights();
        Light[] fireflies = FindFireflyLights();
        if (beacons.Length == 0 && fireflies.Length == 0)
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
        LightQuality quality = host.AddComponent<LightQuality>();
        quality.beaconLights = beacons;
        quality.fireflyLights = fireflies;
        host.SetActive(wasActive);
    }

    public static int RegisterMoth(Light light)
    {
        if (!Application.isPlaying || light == null)
        {
            return -1;
        }

        int index = FindSlot(moths, light);
        if (index >= 0)
        {
            return index;
        }

        index = ReuseSlot(moths);
        GlowSlot slot = index >= 0 ? moths[index] : new GlowSlot();
        slot.light = light;
        slot.gameplayEnabled = light.enabled;
        slot.allowed = !mothCapActive;
        if (index < 0)
        {
            moths.Add(slot);
            index = moths.Count - 1;
        }

        if (live != null)
        {
            live.RefreshGlowCaps(GraphicsQuality.Profile);
        }

        return index;
    }

    public static void UnregisterMoth(int index)
    {
        if (index < 0 || index >= moths.Count)
        {
            return;
        }

        moths[index].light = null;
        moths[index].allowed = true;
    }

    public static bool MothAllowed(int index)
    {
        if (!mothCapActive)
        {
            return true;
        }

        if (index < 0 || index >= moths.Count || moths[index].light == null)
        {
            return true;
        }

        return moths[index].allowed;
    }

    public static void RegisterFirefly(Light light)
    {
        if (!Application.isPlaying || light == null)
        {
            return;
        }

        int index = FindSlot(flies, light);
        if (index >= 0)
        {
            return;
        }

        index = ReuseSlot(flies);
        GlowSlot slot = index >= 0 ? flies[index] : new GlowSlot();
        slot.light = light;
        slot.gameplayEnabled = light.enabled;
        slot.allowed = !flyCapActive;
        if (index < 0)
        {
            flies.Add(slot);
        }
    }

    public static void UnregisterFirefly(Light light)
    {
        int index = FindSlot(flies, light);
        if (index < 0)
        {
            return;
        }

        flies[index].light = null;
        flies[index].allowed = true;
        flies[index].gameplayEnabled = true;
    }

    public static void SetFireflyShown(Light light, bool shown)
    {
        if (light == null)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            light.enabled = shown;
            return;
        }

        RegisterFirefly(light);
        int index = FindSlot(flies, light);
        if (index < 0)
        {
            light.enabled = shown;
            return;
        }

        flies[index].gameplayEnabled = shown;
        ApplyFirefly(flies[index]);
    }

    public static Light[] FindBeaconLights()
    {
        Beacon[] listed = CopyBeacons();
        int count = 0;
        for (int i = 0; i < listed.Length; i++)
        {
            if (GetBeaconLight(listed[i]) != null)
            {
                count++;
            }
        }

        Light[] lights = new Light[count];
        int cursor = 0;
        for (int i = 0; i < listed.Length; i++)
        {
            Light light = GetBeaconLight(listed[i]);
            if (light == null)
            {
                continue;
            }

            lights[cursor] = light;
            cursor++;
        }

        return lights;
    }

    public static Light[] FindFireflyLights()
    {
        Firefly[] found = FindObjectsByType<Firefly>(FindObjectsInactive.Include);
        int count = 0;
        for (int i = 0; i < found.Length; i++)
        {
            if (GetFireflyLight(found[i]) != null)
            {
                count++;
            }
        }

        Light[] lights = new Light[count];
        int cursor = 0;
        for (int i = 0; i < found.Length; i++)
        {
            Light light = GetFireflyLight(found[i]);
            if (light == null)
            {
                continue;
            }

            lights[cursor] = light;
            cursor++;
        }

        return lights;
    }

    public static Light GetBeaconLight(Beacon beacon)
    {
        if (beacon == null)
        {
            return null;
        }

        Light[] lights = beacon.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].name == BeaconLightName)
            {
                return lights[i];
            }
        }

        return null;
    }

    public static Light GetFireflyLight(Firefly firefly)
    {
        if (firefly == null)
        {
            return null;
        }

        Transform glow = firefly.transform.Find(GlowName);
        if (glow != null)
        {
            Light named = glow.GetComponent<Light>();
            if (named != null)
            {
                return named;
            }
        }

        Light[] lights = firefly.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].name == GlowName)
            {
                return lights[i];
            }
        }

        return null;
    }

    static bool IsIsland(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded && scene.name.StartsWith("Island");
    }

    static Beacon[] CopyBeacons()
    {
        int liveCount = 0;
        for (int i = 0; i < Beacon.All.Count; i++)
        {
            if (Beacon.All[i] != null)
            {
                liveCount++;
            }
        }

        if (liveCount > 0)
        {
            Beacon[] listed = new Beacon[liveCount];
            int cursor = 0;
            for (int i = 0; i < Beacon.All.Count; i++)
            {
                if (Beacon.All[i] == null)
                {
                    continue;
                }

                listed[cursor] = Beacon.All[i];
                cursor++;
            }

            return listed;
        }

        return FindObjectsByType<Beacon>(FindObjectsInactive.Include);
    }

    static int FindSlot(List<GlowSlot> slots, Light light)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].light == light)
            {
                return i;
            }
        }

        return -1;
    }

    static int ReuseSlot(List<GlowSlot> slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].light == null)
            {
                return i;
            }
        }

        return -1;
    }

    static void ApplyFirefly(GlowSlot slot)
    {
        if (slot == null || slot.light == null)
        {
            return;
        }

        bool on = slot.gameplayEnabled && (!flyCapActive || slot.allowed);
        if (slot.light.enabled != on)
        {
            slot.light.enabled = on;
        }
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        live = this;
        ResolveRefs();
        RegisterSerializedFireflies();
        TrackSceneParticles();
        CacheBeaconBaselines();
        ResolvePlayer();
        GraphicsQuality.QualityChanged += Apply;
        Apply(GraphicsQuality.Profile);
    }

    void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveRefs();
        RegisterSerializedFireflies();
        CacheBeaconBaselines();
        RefreshGlowCaps(GraphicsQuality.Profile);
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= Apply;
        if (live == this)
        {
            live = null;
            mothCapActive = false;
            flyCapActive = false;
        }
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        glowTimer += Time.unscaledDeltaTime;
        if (glowTimer < GlowInterval)
        {
            return;
        }

        glowTimer = 0f;
        RefreshGlowCaps(GraphicsQuality.Profile);
    }

    void ResolveRefs()
    {
        int beaconCount = FindObjectsByType<Beacon>(FindObjectsInactive.Include).Length;
        int fireflyCount = FindObjectsByType<Firefly>(FindObjectsInactive.Include).Length;
        bool beaconsReady = beaconCount == 0 || HasLights(beaconLights);
        bool fliesReady = fireflyCount == 0 || HasLights(fireflyLights);
        if ((beaconsReady && fliesReady) || searched)
        {
            return;
        }

        searched = true;
        if (!beaconsReady)
        {
            beaconLights = FindBeaconLights();
        }

        if (!fliesReady)
        {
            fireflyLights = FindFireflyLights();
        }

        if (!fallbackWarned)
        {
            fallbackWarned = true;
            Debug.LogWarning("LightQuality is missing light references; using the beacons and fireflies in the scene.", this);
        }
    }

    static bool HasLights(Light[] lights)
    {
        if (lights == null || lights.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    void RegisterSerializedFireflies()
    {
        if (fireflyLights == null)
        {
            return;
        }

        for (int i = 0; i < fireflyLights.Length; i++)
        {
            RegisterFirefly(fireflyLights[i]);
        }
    }

    void TrackSceneParticles()
    {
        if (particlesTracked)
        {
            return;
        }

        particlesTracked = true;
        ParticleSystem[] systems = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleQuality.ApplyTo(systems[i]);
        }
    }

    void ResolvePlayer()
    {
        if (player != null || playerSearched)
        {
            return;
        }

        playerSearched = true;
        PlayerController keeper = KeeperQuality.FindGameplayKeeper();
        if (keeper != null)
        {
            player = keeper.transform;
            return;
        }

        if (!playerWarned)
        {
            playerWarned = true;
            Debug.LogWarning("LightQuality has no player for the glow light cap.", this);
        }
    }

    void CacheBeaconBaselines()
    {
        if (baselineCached || beaconLights == null)
        {
            return;
        }

        beaconBaseline = new BeaconBaseline[beaconLights.Length];
        for (int i = 0; i < beaconLights.Length; i++)
        {
            Light light = beaconLights[i];
            if (light == null || IsKeeperLight(light))
            {
                continue;
            }

            UniversalAdditionalLightData data = light.GetComponent<UniversalAdditionalLightData>();
            BeaconBaseline baseline = new BeaconBaseline();
            baseline.shadows = light.shadows;
            baseline.hadData = data != null;
            baseline.tier = data != null
                ? data.additionalLightsShadowResolutionTier
                : UniversalAdditionalLightData.AdditionalLightsShadowDefaultResolutionTier;
            beaconBaseline[i] = baseline;
        }

        baselineCached = true;
    }

    static bool IsKeeperLight(Light light)
    {
        return light != null && (light.name == "LanternLight" || light.name == "ChestFill");
    }

    void Apply(GraphicsProfile profile)
    {
        if (!Application.isPlaying || profile == null)
        {
            return;
        }

        ResolveRefs();
        RegisterSerializedFireflies();
        TrackSceneParticles();
        CacheBeaconBaselines();
        ResolvePlayer();
        ApplyBeaconShadows(profile);
        ParticleQuality.Reapply(profile.particleCountMultiplier);
        RefreshGlowCaps(profile);
        glowTimer = 0f;
        LogApplied(profile);
    }

    void ApplyBeaconShadows(GraphicsProfile profile)
    {
        if (!baselineCached || beaconLights == null || beaconBaseline == null)
        {
            return;
        }

        int mode = BeaconMode(profile);
        for (int i = 0; i < beaconLights.Length; i++)
        {
            Light light = beaconLights[i];
            if (light == null || IsKeeperLight(light) || i >= beaconBaseline.Length)
            {
                continue;
            }

            BeaconBaseline baseline = beaconBaseline[i];
            if (mode == 0)
            {
                if (light.shadows != baseline.shadows)
                {
                    light.shadows = baseline.shadows;
                }

                RestoreTier(light, ref baseline);
                beaconBaseline[i] = baseline;
                continue;
            }

            if (mode == 2)
            {
                SetShadow(light, ref baseline, LightShadows.Soft);
            }
            else
            {
                SetShadow(light, ref baseline, LightShadows.Hard);
            }

            beaconBaseline[i] = baseline;
        }
    }

    static int BeaconMode(GraphicsProfile profile)
    {
        if (profile.beaconShadowMode == 1 || profile.beaconShadowMode == 2)
        {
            return profile.beaconShadowMode;
        }

        if (!profile.pointLightShadows)
        {
            return 0;
        }

        return profile.level == GraphicsLevel.Ultra ? 2 : 1;
    }

    static void SetShadow(Light light, ref BeaconBaseline baseline, LightShadows shadows)
    {
        UniversalAdditionalLightData data = light.GetComponent<UniversalAdditionalLightData>();
        if (data == null)
        {
            data = light.GetUniversalAdditionalLightData();
            baseline.addedData = true;
        }

        data.additionalLightsShadowResolutionTier = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
        if (light.shadows != shadows)
        {
            light.shadows = shadows;
        }
    }

    static void RestoreTier(Light light, ref BeaconBaseline baseline)
    {
        UniversalAdditionalLightData data = light.GetComponent<UniversalAdditionalLightData>();
        if (data == null)
        {
            baseline.addedData = false;
            return;
        }

        if (baseline.addedData || !baseline.hadData)
        {
            Object.Destroy(data);
            baseline.addedData = false;
            return;
        }

        data.additionalLightsShadowResolutionTier = baseline.tier;
    }

    void RefreshGlowCaps(GraphicsProfile profile)
    {
        int mothCap = profile != null ? profile.glowLightCapMoths : 0;
        int flyCap = profile != null ? profile.glowLightCapFireflies : 0;
        mothCapActive = mothCap > 0;
        flyCapActive = flyCap > 0;
        float distance = profile != null ? profile.glowLightCapDistance : 0f;
        float limitSqr = distance <= 0f ? float.PositiveInfinity : distance * distance;
        if (player == null)
        {
            ResolvePlayer();
        }

        SelectNearest(moths, mothCap, limitSqr, false);
        SelectNearest(flies, flyCap, limitSqr, true);
        for (int i = 0; i < flies.Count; i++)
        {
            ApplyFirefly(flies[i]);
        }

        for (int i = 0; i < moths.Count; i++)
        {
            GlowSlot slot = moths[i];
            if (slot.light == null)
            {
                continue;
            }

            if (!mothCapActive)
            {
                // TickFade owns the fade. Only put back lights the cap turned off while the moth was still visible.
                if (!slot.light.enabled && slot.light.intensity > 0.05f)
                {
                    slot.light.enabled = true;
                }

                continue;
            }

            if (!slot.allowed && slot.light.enabled)
            {
                slot.light.enabled = false;
            }
        }
    }

    void SelectNearest(List<GlowSlot> slots, int cap, float limitSqr, bool shownOnly)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].allowed = cap <= 0;
        }

        if (cap <= 0)
        {
            return;
        }

        Vector3 origin = player != null ? player.position : Vector3.zero;
        bool useDistance = player != null;
        int picked = 0;
        while (picked < cap)
        {
            int best = -1;
            float bestDistance = limitSqr;
            for (int i = 0; i < slots.Count; i++)
            {
                Light light = slots[i].light;
                if (light == null || slots[i].allowed)
                {
                    continue;
                }

                if (shownOnly && !slots[i].gameplayEnabled)
                {
                    continue;
                }

                float distance = 0f;
                if (useDistance)
                {
                    distance = (light.transform.position - origin).sqrMagnitude;
                }

                if (distance <= bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            if (best < 0)
            {
                break;
            }

            slots[best].allowed = true;
            picked++;
        }
    }

    void LogApplied(GraphicsProfile profile)
    {
        StringBuilder text = new StringBuilder(512);
        text.Append("LightQuality ").Append(profile.level);
        text.Append(" beaconMode=").Append(BeaconMode(profile));
        text.Append(" mult=").Append(profile.particleCountMultiplier.ToString("0.###"));
        UniversalRenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        text.Append(" urp=").Append(asset != null ? asset.name : "none");
        text.Append(" addShadows=").Append(asset != null && asset.supportsAdditionalLightShadows);
        AppendBeacons(text);
        AppendGlow(text, "firefly", flies);
        AppendGlow(text, "moth", moths);
        AppendLights(text);
        ParticleQuality.AppendNamed(text, "Sparks", 1);
        ParticleQuality.AppendNamed(text, "Smoke", 1);
        ParticleQuality.AppendNamed(text, "PickupBurst", 1);
        ParticleQuality.AppendNamed(text, "Splash", 1);
        ParticleQuality.AppendNamed(text, "Dust", 1);
        Debug.Log(text.ToString(), this);
    }

    void AppendBeacons(StringBuilder text)
    {
        int lit = 0;
        int unlit = 0;
        int casting = 0;
        if (beaconLights != null)
        {
            for (int i = 0; i < beaconLights.Length; i++)
            {
                Light light = beaconLights[i];
                if (light == null)
                {
                    continue;
                }

                if (light.enabled)
                {
                    lit++;
                }
                else
                {
                    unlit++;
                }

                if (light.enabled && light.shadows != LightShadows.None)
                {
                    casting++;
                }

                text.Append(" | beacon").Append(i);
                text.Append(" ").Append(light.shadows.ToString());
                text.Append(light.enabled ? "/on" : "/off");
                UniversalAdditionalLightData data = light.GetComponent<UniversalAdditionalLightData>();
                if (data != null && light.shadows != LightShadows.None)
                {
                    text.Append(" tier=").Append(data.additionalLightsShadowResolutionTier);
                }
            }
        }

        text.Append(" | beacons lit=").Append(lit).Append(" unlit=").Append(unlit).Append(" casting=").Append(casting);
    }

    static void AppendGlow(StringBuilder text, string label, List<GlowSlot> slots)
    {
        int enabled = 0;
        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].light == null)
            {
                continue;
            }

            total++;
            if (slots[i].light.enabled)
            {
                enabled++;
            }
        }

        text.Append(" | ").Append(label).Append(" ").Append(enabled).Append("/").Append(total);
    }

    static void AppendLights(StringBuilder text)
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        int enabled = 0;
        int casting = 0;
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || !light.enabled)
            {
                continue;
            }

            enabled++;
            if (light.shadows != LightShadows.None)
            {
                casting++;
            }
        }

        text.Append(" | enabledLights=").Append(enabled).Append(" shadowLights=").Append(casting);
    }
}
}
