using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

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
    const float FloorSink = 300f;
    // The finale grade volume (warm balance, lifted shadows, mild bloom, a slight vignette): weight 0 until the sunrise, GradeTarget at its end.
    public const float GradeTarget = 1f;
    // The sun rises from just under the horizon to SunRiseDegrees above it, at the real sun bearing (FinaleCamera.SunBearing).
    const float SunSetDegrees = -2.5f, SunRiseDegrees = 6.5f;
    // The sea at the sunrise (deep blue-teal, lighter to the horizon) and the gold sun path.
    static readonly Color SeaNearColor = new Color(0.095f, 0.26f, 0.34f, 1f);
    static readonly Color SeaFarColor = new Color(0.173f, 0.416f, 0.478f, 1f);
    static readonly Color SeaHorizonColor = new Color(0.62f, 0.56f, 0.47f, 1f);
    static readonly Color SunPathColor = new Color(1f, 0.827f, 0.541f, 1f);

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
    // Finale only (not in Islands 1-4): the sunrise colour grade, blended in over the sunrise.
    [SerializeField] Volume finaleGrade;

    Coroutine running;
    Material sky;
    float skyExponent = 1.6f;
    float skyBand = 0.12f;
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
    public Volume FinaleGrade => finaleGrade;

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
            dawn.SkyTopOverride = new Color(0.1f, 0.17f, 0.72f, 1f);
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
            if (sky.HasProperty("_HorizonBand"))
            {
                skyBand = sky.GetFloat("_HorizonBand");
            }

            cloudColor = sky.HasProperty("_CloudColor") ? sky.GetColor("_CloudColor") : cloudColor;
        }

        MakeSeaSeamless();
        if (finaleGrade != null)
        {
            finaleGrade.weight = 0f;
        }

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

        // The sand floor 10 m under the water: a private dark teal copy of its material (it is no longer the cause of a brown sea, see Water.shader _FinaleSea).
        GameObject floor = GameObject.Find("SeaFloor");
        if (floor == null)
        {
            Debug.LogWarning("FinaleSequence: no SeaFloor in the scene; the sea may turn brown at the sunrise.", this);
        }

        Renderer floorRenderer = floor != null ? floor.GetComponent<Renderer>() : null;
        if (floorRenderer != null && floorRenderer.sharedMaterial != null && floorRenderer.sharedMaterial.HasProperty("_BaseColor"))
        {
            floorRenderer.material.SetColor("_BaseColor", new Color(0.02f, 0.05f, 0.07f, 1f));
            // The floor grows with the sea and sinks well under it: at 10 m it was an opaque plane that depth-clipped the god rays along straight
            // angled edges (the lighter band at the horizon). The sea is solid out there, so the floor is never seen; the water depth stays deep.
            Vector3 floorScale = floor.transform.localScale;
            floor.transform.localScale = new Vector3(floorScale.x * SeaScale, floorScale.y, floorScale.z * SeaScale);
            floor.transform.position += Vector3.down * FloorSink;
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
                instance.SetFloat("_FadeStart", 2400f);
                instance.SetFloat("_FadeEnd", 3900f);
                // The finale sea (Water.shader _FinaleSea): off until the sunrise blends it in.
                if (instance.HasProperty("_FinaleSea"))
                {
                    instance.SetFloat("_FinaleSea", 0f);
                    instance.SetFloat("_FinalePath", 0f);
                    instance.SetColor("_FinaleNear", SeaNearColor);
                    instance.SetColor("_FinaleFar", SeaFarColor);
                    instance.SetColor("_FinaleHorizon", SeaHorizonColor);
                    instance.SetColor("_FinalePathColor", SunPathColor);
                }
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

        float sunrise = Mathf.SmoothStep(0f, 1f, FinaleTimeline.Phase01(t, FinalePhase.Sunrise));
        // The sunrise sky is blue-violet high up, graded to gold and peach at the horizon: the exponent drops so the violet takes the upper sky, and the
        // horizon band (the pre-dawn first-light glow, here at full strength) holds the gold low down.
        if (t >= FinaleTimeline.SunriseStart && sky != null && sky.HasProperty("_Exponent"))
        {
            sky.SetFloat("_Exponent", Mathf.Lerp(skyExponent, 0.18f, sunrise));
            if (sky.HasProperty("_HorizonBand"))
            {
                sky.SetFloat("_HorizonBand", Mathf.Lerp(skyBand, 0.03f, sunrise));
            }
            if (sky.HasProperty("_HorizonBlend"))
            {
                sky.SetFloat("_HorizonBlend", sunrise);
            }

            if (sky.HasProperty("_CloudColor"))
            {
                sky.SetColor("_CloudColor", Color.Lerp(cloudColor, new Color(0.3f, 0.28f, 0.62f, cloudColor.a), sunrise));
            }

            // The sun: a disc and a warm halo at the real sun bearing, just under the horizon at first and rising a little.
            if (sky.HasProperty("_SunAlpha"))
            {
                float bearing = FinaleCamera.SunBearing * Mathf.Deg2Rad;
                float elevation = Mathf.Lerp(SunSetDegrees, SunRiseDegrees, sunrise) * Mathf.Deg2Rad;
                Vector3 toSun = new Vector3(Mathf.Sin(bearing) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Cos(bearing) * Mathf.Cos(elevation));
                sky.SetVector("_SunDir", new Vector4(toSun.x, toSun.y, toSun.z, 0f));
                sky.SetFloat("_SunAlpha", Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sunrise / 0.35f)));
            }
        }

        // The sea turns deep blue-teal with a gold path as the sun comes up; the grade warms the whole picture over the sunrise.
        if (t >= FinaleTimeline.SunriseStart)
        {
            if (seaMaterial != null && seaMaterial.HasProperty("_FinaleSea"))
            {
                seaMaterial.SetFloat("_FinaleSea", Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sunrise / 0.55f)));
                seaMaterial.SetFloat("_FinalePath", Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sunrise - 0.1f) / 0.7f)));
            }

            if (finaleGrade != null)
            {
                finaleGrade.weight = GradeTarget * sunrise;
            }
        }

        if (!sunriseStarted && t >= FinaleTimeline.SunriseStart)
        {
            sunriseStarted = true;
            if (seaMaterial != null)
            {
                // The shallows round the lighthouse stay a clear blue-teal; the rim lines and the sun path are gold (the finale sea's own colours do the rest).
                seaMaterial.SetColor("_DeepColor", new Color(0.015f, 0.08f, 0.18f, 0.92f));
                seaMaterial.SetColor("_ShallowColor", new Color(0.05f, 0.2f, 0.36f, 0.5f));
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
