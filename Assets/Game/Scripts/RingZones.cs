using System;
using UnityEngine;

namespace LanternKeeper
{
// Island 5: tracks the highest height ring the keeper has stood in. Height is the keeper's grounded feet above sea level, so a jump
// never counts, and a new ring needs half a second standing in it (RingTracker). The ring never goes back down during a run;
// a scene reload resets it.
public class RingZones : MonoBehaviour
{
    const float AutoSea = -999f;

    [SerializeField] PlayerController player;
    // Sea level in world Y. Left at the default it is taken from the water surface when the scene starts.
    [SerializeField] float seaLevel = AutoSea;

    readonly RingTracker tracker = new RingTracker();
    float sea;

    public static Ring Reached { get; private set; }
    public static event Action<Ring> ReachedRing;
    public static RingZones Instance { get; private set; }

    public float Sea => sea;
    public float CurrentHeight => player != null ? player.transform.position.y - sea : 0f;
    public float LastGroundedHeight => tracker.LastGroundedHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Reached = Ring.Low;
        ReachedRing = null;
        Instance = null;
    }

    void Awake()
    {
        Instance = this;
        Reached = Ring.Low;
        tracker.Reset();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Reached = Ring.Low;
        }
    }

    void Start()
    {
        sea = seaLevel > AutoSea + 1f ? seaLevel : Mathf.Max(0f, WaterHazard.SurfaceY);
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }
    }

    void Update()
    {
        if (player == null)
        {
            return;
        }

        GameManager manager = GameManager.Instance;
        if (manager != null && (manager.IsPaused || manager.IsRoundOver))
        {
            return;
        }

        if (tracker.Step(player.IsGrounded, player.transform.position.y - sea, Time.deltaTime))
        {
            Reached = tracker.Reached;
            Action<Ring> handler = ReachedRing;
            if (handler != null)
            {
                handler(Reached);
            }
        }
    }
}
}
