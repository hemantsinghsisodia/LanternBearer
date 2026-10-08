using UnityEngine;

namespace LanternKeeper
{
// Runs the wind cycle on windy islands and pushes the player through PlayerController.ExternalPush.
public class Wind : MonoBehaviour
{
    const float ShelterRange = 4f;
    const float ChestHeight = 1.0f;
    static readonly int WindId = Shader.PropertyToID("_LKWind");

    [SerializeField] float strength = 1f;
    [SerializeField] float prevailingDegrees = 250f;
    [SerializeField] int seed = 4404;
    [SerializeField] PlayerController player;
    [SerializeField] WaterHazard hazard;

    [SerializeField] float bedVolume = 0.16f;
    [SerializeField] float howlVolume = 0.5f;
    [SerializeField] float howlDistance = 12f;

    WindCycle cycle;
    float strengthScale;
    AudioSource bed;
    AudioSource howl;
    bool bedRouted;
    bool howlRouted;
    float shelterGain = 1f;

    public static Wind Instance { get; private set; }

    public WindPhase Phase => cycle != null ? cycle.Phase : WindPhase.Calm;
    public Vector3 Direction => cycle != null ? cycle.Direction : Vector3.forward;
    public float Strength01 => cycle != null ? cycle.Strength01 : 0f;
    public bool Sheltered { get; private set; }
    public float TimeToNextWarning => cycle != null ? cycle.TimeToNextWarning : 0f;
    public float WarningEndsIn => cycle != null ? cycle.WarningEndsIn : 0f;

    void Awake()
    {
        Instance = this;
        bed = StormAudio.Make(gameObject, AudioManager.CueClip(SoundCues.AmbienceWindBed), true, 0f);
        bed.Play();
        GameObject howlObject = new GameObject("WindHowl");
        howlObject.transform.SetParent(transform, false);
        howl = StormAudio.Make(howlObject, AudioManager.CueClip(SoundCues.AmbienceGust), true, 1f);
        howl.Play();
    }

    void Start()
    {
        bool hard = GameSettings.Current == Difficulty.Hard;
        strengthScale = strength * GameSettings.GustMultiplier();
        cycle = new WindCycle(seed, prevailingDegrees, strengthScale, hard);
    }

    void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        PlayerController.ExternalPush = Vector3.zero;
        Shader.SetGlobalVector(WindId, Vector4.zero);
    }

    void Update()
    {
        if (cycle == null)
        {
            return;
        }

        GameManager manager = GameManager.Instance;
        bool paused = manager != null && manager.IsPaused;
        bool dying = manager != null && (manager.IsDying || manager.IsRoundOver);
        if (!paused)
        {
            cycle.Tick(Time.deltaTime);
        }

        Vector3 dir = cycle.Direction;
        float s01 = cycle.Strength01;

        // Low keeps the grass still: the bend global stays zero.
        bool bendGrass = GraphicsQuality.Current != GraphicsLevel.Low;
        Shader.SetGlobalVector(WindId, bendGrass ? new Vector4(dir.x, dir.z, s01, 0f) : Vector4.zero);

        if (player == null)
        {
            PlayerController.ExternalPush = Vector3.zero;
            UpdateAudio(dir, s01);
            return;
        }

        bool rescuing = hazard != null && hazard.IsRescuing;
        bool inSafeRing = InSafeRing(player.transform.position);
        if (!WindCycle.PushAllowed(paused, dying, rescuing, inSafeRing) || s01 <= 0f)
        {
            Sheltered = false;
            PlayerController.ExternalPush = Vector3.zero;
            UpdateAudio(dir, s01);
            return;
        }

        Sheltered = IsSheltered(dir);
        PlayerController.ExternalPush = WindCycle.Push(dir, s01, strengthScale, !player.IsGrounded, Sheltered);
        UpdateAudio(dir, s01);
    }

    // A low bed always runs. The howl comes from upwind and swells with the warning pulse and the gust.
    void UpdateAudio(Vector3 dir, float s01)
    {
        if (!bedRouted)
        {
            bedRouted = StormAudio.Route(bed, true);
        }

        if (!howlRouted)
        {
            howlRouted = StormAudio.Route(howl, false);
        }

        float mute = StormAudio.MuteGain;
        shelterGain = Mathf.MoveTowards(shelterGain, Sheltered ? 0.5f : 1f, Time.deltaTime * 2f);
        bed.volume = bedVolume * (0.5f + 0.5f * s01) * mute;
        float howlLevel = 0f;
        if (Phase == WindPhase.Warning)
        {
            howlLevel = 0.25f + 0.2f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
        }
        else if (Phase == WindPhase.Gust)
        {
            howlLevel = 0.25f + 0.75f * s01;
        }

        howl.volume = howlVolume * howlLevel * shelterGain * mute;
        if (player != null)
        {
            howl.transform.position = player.transform.position - dir * howlDistance + Vector3.up;
        }
    }

    static bool InSafeRing(Vector3 position)
    {
        for (int i = 0; i < Beacon.All.Count; i++)
        {
            if (Beacon.All[i] != null && Beacon.All[i].ContainsSafe(position))
            {
                return true;
            }
        }

        return false;
    }

    // A static, non-trigger collider within 4 m upwind of the player's chest blocks most of the push.
    // The player is on the Default layer, so its own colliders are skipped by hierarchy, not by layer. Terrain is ignored so slopes do not count as cover.
    bool IsSheltered(Vector3 dir)
    {
        Vector3 origin = player.transform.position + Vector3.up * ChestHeight;
        RaycastHit[] hits = Physics.RaycastAll(origin, -dir, ShelterRange, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i].collider;
            if (hit != null && hit.attachedRigidbody == null && !(hit is TerrainCollider) && !hit.transform.IsChildOf(player.transform))
            {
                return true;
            }
        }

        return false;
    }
}
}
