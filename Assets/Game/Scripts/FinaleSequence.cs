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

    [SerializeField] GreatLamp lamp;
    [SerializeField] GreatLampBeam beam;
    [SerializeField] FinaleCamera finaleCamera;
    [SerializeField] StormRing storm;
    [SerializeField] FinaleIslands islands;
    [SerializeField] DawnSequence dawn;
    [SerializeField] ParticleSystem embers;

    Coroutine running;
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

    void Step(float t, float speed, Rain rain)
    {
        if (t >= FinaleTimeline.BeamStart && storm != null)
        {
            if (!stormStarted)
            {
                stormStarted = true;
                // The gap opens from the bearing the beam points at when the storm starts to open.
                Vector3 beamDirection = beam != null ? beam.transform.right : Vector3.right;
                gapBearing = StormRing.Bearing01(beamDirection);
            }

            storm.SetOpen(FinaleTimeline.Phase01(t, FinalePhase.BeamAndStorm), gapBearing);
            if (islands != null)
            {
                islands.SinkInnerHorizon(FinaleTimeline.Phase01(t, FinalePhase.BeamAndStorm) * 1.6f);
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

        if (!sunriseStarted && t >= FinaleTimeline.SunriseStart)
        {
            sunriseStarted = true;
            if (dawn != null)
            {
                dawn.Play(null, FinaleTimeline.SunriseLength / speed, true);
            }
        }
    }
}
}
