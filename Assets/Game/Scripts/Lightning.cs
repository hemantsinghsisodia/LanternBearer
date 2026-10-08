using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LanternKeeper
{
// Runs the lightning schedule on Island 4. The flash is a directional light plus a short exposure pulse.
// Stones, unlit beacons and Shades react through the Flashed event and the Flash01 value.
public class Lightning : MonoBehaviour
{
    [SerializeField] int seed = 4404;
    [SerializeField] Light flashLight;
    [SerializeField] Volume volume;
    [SerializeField] float peakIntensity = 3f;
    [SerializeField] float peakExposure = 1.5f;
    [SerializeField] float rumbleVolume = 0.7f;
    [SerializeField] float crackVolume = 0.9f;

    LightningSchedule schedule;
    ColorAdjustments color;
    float baseExposure;
    bool exposureReady;
    bool pulseAllowed = true;
    bool subscribed;
    AudioSource thunder;
    bool thunderRouted;
    LightningPhase lastPhase = LightningPhase.Waiting;

    public static Lightning Instance { get; private set; }
    public static event Action Flashed;

    public LightningPhase Phase => schedule != null ? schedule.Phase : LightningPhase.Waiting;
    public float Flash01 => schedule != null ? schedule.Flash01 : 0f;
    // World brightening (stones, beacons, water, rim light) follows this, so Reduce flashing caps it too.
    public static float CurrentFlash => Instance != null ? Instance.Flash01 * FlashCap : 0f;
    static float FlashCap => SettingsMath.FlashCap(UserSettings.ReduceFlashing);

    void Awake()
    {
        Instance = this;
        thunder = StormAudio.Make(gameObject, null, false, 0f);
        if (flashLight != null)
        {
            flashLight.intensity = 0f;
            flashLight.enabled = false;
        }
    }

    void OnEnable()
    {
        Instance = this;
        if (!subscribed)
        {
            subscribed = true;
            GraphicsQuality.QualityChanged += OnQuality;
        }
    }

    void Start()
    {
        schedule = new LightningSchedule(seed, GameSettings.LightningInterval());
        BindExposure();
        ApplyProfile(GraphicsQuality.Profile);
    }

    void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (subscribed)
        {
            subscribed = false;
            GraphicsQuality.QualityChanged -= OnQuality;
        }

        if (exposureReady && color != null)
        {
            color.postExposure.overrideState = false;
            color.postExposure.value = baseExposure;
        }
    }

    // Test hook: starts a flash now, skipping the thunder lead.
    internal void DebugFlash()
    {
        if (schedule != null)
        {
            schedule.ForceFlash();
            Action handler = Flashed;
            if (handler != null)
            {
                handler();
            }
        }
    }

    void BindExposure()
    {
        if (volume == null)
        {
            return;
        }

        // A rebuilt scene can leave the shared profile holding destroyed components; reading volume.profile would clone them and throw.
        VolumeProfile shared = volume.sharedProfile;
        if (shared == null || !LowFuelFX.ProfileAlive(shared))
        {
            return;
        }

        VolumeProfile runtime = volume.profile;
        if (runtime != null && runtime.TryGet(out color) && color != null)
        {
            baseExposure = color.postExposure.value;
            exposureReady = true;
        }
    }

    void OnQuality(GraphicsProfile profile)
    {
        ApplyProfile(profile);
    }

    // Low has no post-processing, so no exposure pulse. Only Ultra lets the flash light cast shadows.
    void ApplyProfile(GraphicsProfile profile)
    {
        pulseAllowed = profile == null || (profile.level != GraphicsLevel.Low && profile.postProcessing != GraphicsPostProcessing.Off);
        if (flashLight != null)
        {
            bool ultra = profile != null && profile.level == GraphicsLevel.Ultra;
            flashLight.shadows = ultra ? LightShadows.Soft : LightShadows.None;
        }
    }

    // The rumble starts with the thunder lead, the crack lands with the flash.
    void PlayThunder(LightningPhase phase, bool flashed)
    {
        if (!thunderRouted)
        {
            thunderRouted = StormAudio.Route(thunder, false);
        }

        if (phase == LightningPhase.Thunder && lastPhase != LightningPhase.Thunder)
        {
            PlayClip(AudioManager.CueClip(SoundCues.ThunderRumble), rumbleVolume);
        }

        if (flashed)
        {
            PlayClip(AudioManager.CueClip(SoundCues.ThunderCrack), crackVolume);
        }

        lastPhase = phase;
    }

    void PlayClip(AudioClip clip, float volume)
    {
        if (clip != null)
        {
            thunder.PlayOneShot(clip, volume);
        }
    }

    void Update()
    {
        if (schedule == null)
        {
            return;
        }

        GameManager manager = GameManager.Instance;
        bool paused = manager != null && manager.IsPaused;
        bool roundOver = manager != null && manager.IsRoundOver;
        if (!paused)
        {
            float startsIn = -1f;
            float endsIn = -1f;
            Wind wind = Wind.Instance;
            if (wind != null)
            {
                startsIn = wind.TimeToNextWarning;
                endsIn = wind.WarningEndsIn;
            }

            schedule.Tick(Time.deltaTime, roundOver, startsIn, endsIn);
            PlayThunder(schedule.Phase, schedule.FlashedThisTick);
            if (schedule.FlashedThisTick)
            {
                Action handler = Flashed;
                if (handler != null)
                {
                    handler();
                }
            }
        }

        float cap = FlashCap;
        float flash = schedule.Flash01 * cap;
        thunder.volume = StormAudio.MuteGain;
        if (flashLight != null)
        {
            flashLight.enabled = flash > 0.001f;
            flashLight.intensity = flash * peakIntensity;
        }

        if (exposureReady && color != null)
        {
            // Only the effects volume is written. The override is released between flashes so the look grade shows through.
            // The pulse rides on the authored base plus the user's brightness, and returns to it afterwards (flash is already capped).
            float pulse = pulseAllowed ? flash * peakExposure : 0f;
            color.postExposure.overrideState = pulse > 0.001f;
            color.postExposure.value = baseExposure + UserSettingsApplier.UserEv + pulse;
        }
    }
}
}
