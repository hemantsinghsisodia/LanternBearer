using UnityEngine;

namespace LanternKeeper
{
public class Tide : MonoBehaviour
{
    [SerializeField] Transform waterRoot;
    [SerializeField] WaterHazard hazard;
    [SerializeField] float baseY;
    [SerializeField] float amplitude = 0.9f;
    [SerializeField] float period = 90f;
    [SerializeField] float phase = -0.9f;
    [SerializeField] float surfVolume = 0.18f;

    AudioSource surf;
    bool surfRouted;

    public static Tide Instance { get; private set; }
    public static float HighWaterY => WaterHazard.HighWaterY;

    public float Amplitude => amplitude;
    public float CurrentY { get; private set; }
    public float Normalized => amplitude <= 0.0001f ? 0.5f : Mathf.Clamp01((CurrentY - baseY) / (2f * amplitude) + 0.5f);
    public bool IsRising => Mathf.Cos(Angle()) >= 0f;
    public float SecondsToTurn
    {
        get
        {
            float angle = Angle();
            float half = Mathf.PI * 0.5f;
            float turn = Mathf.Ceil((angle - half) / Mathf.PI) * Mathf.PI + half;
            if (turn - angle < 0.0001f)
            {
                turn += Mathf.PI;
            }

            return (turn - angle) * Mathf.Max(1f, period) / (2f * Mathf.PI);
        }
    }

    void Awake()
    {
        Instance = this;
        CurrentY = baseY + amplitude * Mathf.Sin(Angle());
        WaterHazard.SetHighWater(baseY + amplitude);
        CreateSurf();
    }

    void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
            WaterHazard.SetHighWater(-100f);
        }
    }

    void OnEnable()
    {
        Instance = this;
        WaterHazard.SetHighWater(baseY + amplitude);
    }

    void Update()
    {
        CurrentY = baseY + amplitude * Mathf.Sin(Angle());
        if (waterRoot != null)
        {
            Vector3 position = waterRoot.position;
            if (!Mathf.Approximately(position.y, CurrentY))
            {
                position.y = CurrentY;
                waterRoot.position = position;
            }
        }

        if (hazard != null)
        {
            hazard.SetSurface(CurrentY);
        }

        UpdateSurf();
    }

    float Angle()
    {
        return 2f * Mathf.PI * Time.time / Mathf.Max(1f, period) + phase;
    }

    void CreateSurf()
    {
        if (surf != null)
        {
            return;
        }

        surf = gameObject.AddComponent<AudioSource>();
        surf.playOnAwake = false;
        surf.loop = true;
        surf.spatialBlend = 0f;
        surf.volume = 0f;
        surf.clip = AudioManager.CueClip(SoundCues.AmbienceTide);
        surf.Play();
    }

    void UpdateSurf()
    {
        if (surf == null)
        {
            return;
        }

        if (!surfRouted && AudioManager.Instance != null && AudioManager.Instance.AmbienceGroup != null)
        {
            surf.outputAudioMixerGroup = AudioManager.Instance.AmbienceGroup;
            surfRouted = true;
        }

        float rate = Mathf.Abs(Mathf.Cos(Angle()));
        surf.volume = surfVolume * (0.35f + 0.65f * Normalized) * (0.7f + 0.3f * rate);
    }
}
}
