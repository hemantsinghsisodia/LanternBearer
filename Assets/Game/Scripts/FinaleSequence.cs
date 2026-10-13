using System;
using System.Collections;
using UnityEngine;

namespace LanternKeeper
{
// The Island 5 finale cinematic (spec section 5). GameManager.Win() calls Play(FinishWin) instead of DawnSequence.Play when the scene has one;
// onComplete (FinishWin -> WonGame) runs at FinaleTimeline.Total. Everything runs on unscaled time, so the pause menu and timeScale do not
// touch it (pause is blocked while GameManager.DawnPlaying, and PlayerController ignores input once the round is over).
//
//   0 s    MusicDirector.PlayFinale (the cue's deep chord), a lamp flare (x4 over 1.2 s), an ember burst, the camera takes over.
//   6 s    the storm ring starts to open from the bearing the beam is pointing at (fully open by 18 s); the camera pulls back and up.
//   18 s   the silhouettes of Islands 1-4 fade in, and each flares its beacons at FinaleTimeline.IslandFlareTime.
//   36 s   the sunrise: DawnSequence.Play(null, SunriseLength, unscaled). The dawn owns the sky, fog, sun and water from here.
//   52 s   onComplete. The camera holds its final frame.
//
// Island5Look stops writing the sky and fog once the round is over (GameManager.Win sets it before Play), so the storm opening and the
// sunrise never fight it. The storm ring is its own material; it does not touch the sky.
public class FinaleSequence : MonoBehaviour
{
    // Test hook: the finale clock runs this many times faster (1 in the game). Reset in OnDestroy.
    internal static float TestSpeed = 1f;

    // A frame hitch cannot skip more than this many real seconds of the finale.
    const float MaxFrameSeconds = 0.25f;
    const float LampFlarePeak = 4f;
    const float LampFlareSeconds = 1.2f;
    const float SeaFar = 4000f;
    const float SeaScale = 8f;
    const float HaloSize = 16f;

    [SerializeField] GreatLamp lamp;
    [SerializeField] GreatLampBeam beam;
    [SerializeField] FinaleCamera finaleCamera;
    [SerializeField] StormRing storm;
    [SerializeField] FinaleClouds clouds;
    // A soft amber additive card at the lantern room, so the lit lamp reads from the finale camera distances.
    [SerializeField] Transform halo;
    [SerializeField] FinaleIslands islands;
    [SerializeField] DawnSequence dawn;
    [SerializeField] ParticleSystem embers;

    Coroutine running;
    Material sky;
    float skyExponent = 1.6f;
    Color cloudColor = Color.gray;
    Material seaMaterial;
    bool sunriseStarted;
    bool stormStarted;
    float gapBearing;

    public bool IsPlaying { get; private set; }
    // Finale seconds since Play.
    public float Elapsed { get; private set; }
    public FinalePhase Phase { get; private set; }
    public bool Finished { get; private set; }

    void OnDestroy()
    {
        TestSpeed = 1f;
    }

    void Resolve()
    {
        if (lamp == null) lamp = FindAnyObjectByType<GreatLamp>();
        if (beam == null && lamp != null) beam = lamp.GetComponentInChildren<GreatLampBeam>(true);
        if (finaleCamera == null) finaleCamera = GetComponent<FinaleCamera>();
        if (storm == null) storm = GetComponentInChildren<StormRing>(true);
        if (clouds == null) clouds = GetComponentInChildren<FinaleClouds>(true);
        if (islands == null) islands = GetComponentInChildren<FinaleIslands>(true);
        if (dawn == null) dawn = FindAnyObjectByType<DawnSequence>();
    }

    public void Play(Action onComplete)
    {
        if (IsPlaying || Finished)
        {
            return;
        }

        Resolve();
        IsPlaying = true;
        running = StartCoroutine(Run(onComplete));
    }

    IEnumerator Run(Action onComplete)
    {
        float speed = Mathf.Max(0.01f, TestSpeed);
        Elapsed = 0f;
        Phase = FinalePhase.Ignition;

        MusicDirector music = FindAnyObjectByType<MusicDirector>();
        if (music != null)
        {
            music.PlayFinale();
        }

        if (lamp != null)
        {
            // The lamp is always lit in the cinematic (a no-op when the keeper lit it), then it flares.
            lamp.EnsureLit();
            lamp.Flare(LampFlarePeak, LampFlareSeconds);
        }

        if (embers != null)
        {
            if (lamp != null && lamp.Flame != null)
            {
                embers.transform.position = lamp.Flame.position;
            }

            embers.Play();
        }

        if (finaleCamera != null)
        {
            finaleCamera.Begin(this);
        }

        // The finale sunrise is clearer than the island dawns: the fog thins right out, and the warm grade and sea tint are held back,
        // so the sky keeps its blue-violet to gold gradient and the island its form.
        if (dawn != null)
        {
            dawn.FogDensityScale = 0.01f;
            dawn.WaterWarmth = 1f;
            dawn.WaterTintOverride = new Color(0.55f, 0.85f, 1.05f, 1f);
            dawn.DawnGradeWeight = 0f;
            dawn.SkyTopOverride = new Color(0.16f, 0.15f, 0.46f, 1f);
            dawn.HorizonOverride = new Color(1f, 0.74f, 0.46f, 1f);
            dawn.AmbientOverride = new Color(0.30f, 0.38f, 0.62f, 1f);
            dawn.ExposureScale = 0.6f;
        }

        // The fireflies are big green blobs from the air.
        FireflySwarm[] swarms = FindObjectsByType<FireflySwarm>(FindObjectsInactive.Exclude);
        for (int i = 0; i < swarms.Length; i++)
        {
            swarms[i].gameObject.SetActive(false);
        }

        // So are the moths (glowing white dots all over the sea): no more spawns, and the living ones go.
        Moth[] moths = FindObjectsByType<Moth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < moths.Length; i++)
        {
            moths[i].gameObject.SetActive(false);
        }

        sky = RenderSettings.skybox;
        if (sky != null && sky.HasProperty("_Exponent"))
        {
            skyExponent = sky.GetFloat("_Exponent");
            cloudColor = sky.HasProperty("_CloudColor") ? sky.GetColor("_CloudColor") : cloudColor;
        }

        MakeSeaSeamless();

        // The Rain follows the camera; the wall tears open at the end of it.
        Rain rain = FindAnyObjectByType<Rain>();

        while (Elapsed < FinaleTimeline.Total)
        {
            Elapsed = Mathf.Min(FinaleTimeline.Total, Elapsed + Mathf.Min(Time.unscaledDeltaTime, MaxFrameSeconds) * speed);
            Phase = FinaleTimeline.PhaseAt(Elapsed);
            Step(Elapsed, speed, rain);
            yield return null;
        }

        // The dawn is released a frame or two after its last step; wait for it so the sky is final when WonGame fires.
        float guard = 0f;
        while (dawn != null && dawn.IsPlaying && guard < 2f)
        {
            guard += Time.unscaledDeltaTime;
            yield return null;
        }

        Elapsed = FinaleTimeline.Total;
        Phase = FinalePhase.Credits;
        IsPlaying = false;
        Finished = true;
        if (islands != null)
        {
            islands.Tick(FinaleTimeline.Total);
        }

        if (finaleCamera != null)
        {
            // One last placement at the final time, then it holds.
            finaleCamera.Place(FinaleTimeline.Total);
            finaleCamera.End();
        }

        if (onComplete != null)
        {
            onComplete.Invoke();
        }
    }

    // The sea is a 1280 m square: from the finale camera its edge shows as a line and a corner. The far plane goes out and the plane scales up, so the
    // water (which fades to the fog colour, the dawn horizon colour) runs to the horizon.
    void MakeSeaSeamless()
    {
        Camera camera = Camera.main;
        if (camera != null && camera.farClipPlane < SeaFar)
        {
            camera.farClipPlane = SeaFar;
        }

        // The sand floor 10 m under the water shows through it and turns the sea brown in the dawn light: a private dark teal copy of its material.
        // (The floor itself stays, it keeps the water depth even.)
        GameObject floor = GameObject.Find("SeaFloor");
        if (floor == null)
        {
            Debug.LogWarning("FinaleSequence: no SeaFloor in the scene; the sea may turn brown at the sunrise.", this);
        }

        Renderer floorRenderer = floor != null ? floor.GetComponent<Renderer>() : null;
        if (floorRenderer != null && floorRenderer.sharedMaterial != null && floorRenderer.sharedMaterial.HasProperty("_BaseColor"))
        {
            floorRenderer.material.SetColor("_BaseColor", new Color(0.02f, 0.05f, 0.07f, 1f));
            // Out past the floor the water would blend over the bright sky (brown): the floor grows with the sea.
            Vector3 floorScale = floor.transform.localScale;
            floor.transform.localScale = new Vector3(floorScale.x * SeaScale, floorScale.y, floorScale.z * SeaScale);
        }

        GameObject water = GameObject.Find("Water");
        if (water == null)
        {
            Debug.LogWarning("FinaleSequence: no Water in the scene; the horizon edge will show.", this);
        }

        if (water != null)
        {
            Vector3 scale = water.transform.localScale;
            water.transform.localScale = new Vector3(SeaScale, scale.y, SeaScale);
            // The water fades to the fog colour in a ring (520-630 m); out here that would turn most of the visible sea gold, so the ring moves out
            // to where the sea meets the horizon (a private material instance; the shared asset is not touched).
            Renderer renderer = water.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_FadeStart"))
            {
                Material instance = renderer.material;
                seaMaterial = instance;
                instance.SetFloat("_FadeStart", 2800f);
                instance.SetFloat("_FadeEnd", 3800f);
            }
        }
    }

    void Step(float t, float speed, Rain rain)
    {
        if (halo != null && lamp != null && lamp.Flame != null)
        {
            // The halo follows the lantern room, faces the camera and breathes a little.
            Camera main = Camera.main;
            if (!halo.gameObject.activeSelf)
            {
                halo.gameObject.SetActive(true);
            }

            halo.position = lamp.Flame.position;
            if (main != null)
            {
                halo.rotation = main.transform.rotation;
            }

            halo.localScale = Vector3.one * HaloSize * (0.92f + 0.08f * Mathf.Sin(Time.unscaledTime * 3.1f));
        }

        if (t >= FinaleTimeline.BeamStart && storm != null)
        {
            if (!stormStarted)
            {
                stormStarted = true;
                // The gap opens from the bearing the beam points at when the storm starts to open.
                Vector3 beamDirection = beam != null ? beam.transform.right : Vector3.right;
                gapBearing = StormRing.Bearing01(beamDirection);
                if (clouds != null)
                {
                    clouds.SetBeamBearing(gapBearing);
                }
            }

            storm.SetOpen(FinaleTimeline.Phase01(t, FinalePhase.BeamAndStorm), gapBearing);
            // The dramatic cloud banks take over from the ring as the storm starts to break.
            if (clouds != null)
            {
                storm.SetFade(1f - FinaleCloudMath.CrossFade(t));
            }

            if (islands != null)
            {
                islands.SinkInnerHorizon(FinaleTimeline.Phase01(t, FinalePhase.BeamAndStorm) * 3f);
            }

            if (rain != null && rain.gameObject.activeSelf && t >= FinaleTimeline.BeamStart + 3f)
            {
                rain.gameObject.SetActive(false);
            }
        }

        if (islands != null)
        {
            islands.Tick(t);
        }

        if (clouds != null)
        {
            clouds.Tick(t);
        }

        // The sunrise sky keeps a blue-violet to gold gradient: the exponent drops so the gold does not cover the whole view.
        if (t >= FinaleTimeline.SunriseStart && sky != null && sky.HasProperty("_Exponent"))
        {
            sky.SetFloat("_Exponent", Mathf.Lerp(skyExponent, 0.3f, Mathf.SmoothStep(0f, 1f, FinaleTimeline.Phase01(t, FinalePhase.Sunrise))));
            if (sky.HasProperty("_CloudColor"))
            {
                sky.SetColor("_CloudColor", Color.Lerp(cloudColor, new Color(0.46f, 0.38f, 0.62f, cloudColor.a), Mathf.SmoothStep(0f, 1f, FinaleTimeline.Phase01(t, FinalePhase.Sunrise))));
            }
        }

        if (!sunriseStarted && t >= FinaleTimeline.SunriseStart)
        {
            sunriseStarted = true;
            // The sea turns deep blue-teal for the sunrise (the dawn light warms the night colours to brown otherwise).
            if (seaMaterial != null)
            {
                seaMaterial.SetColor("_DeepColor", new Color(0.015f, 0.08f, 0.18f, 0.92f));
                seaMaterial.SetColor("_ShallowColor", new Color(0.05f, 0.2f, 0.36f, 0.5f));
                // The sun path is the water's moon streak, drawn in _RimColor: gold for the sunrise.
                seaMaterial.SetColor("_RimColor", new Color(1f, 0.83f, 0.54f, 1f));
            }

            if (dawn != null)
            {
                dawn.Play(null, FinaleTimeline.SunriseLength / speed, true);
            }
        }
    }
}
}
