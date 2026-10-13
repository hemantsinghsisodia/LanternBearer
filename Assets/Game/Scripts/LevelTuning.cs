using UnityEngine;

namespace LanternKeeper
{
// The per-level numbers the scene needs at runtime that no other component owns (Island 5 scarcity): the beacon refund and the lantern
// drain multiplier. The scene object is filled by LightningHunterInstaller from the level's LevelConfig, the single source (a Validate
// check compares them). Islands 1-4 have none, so they refund nothing and drain at 1x.
public class LevelTuning : MonoBehaviour
{
    [SerializeField] float beaconRefund01;
    [SerializeField] float drainMultiplier = 1f;

    public static LevelTuning Instance { get; private set; }
    public static float BeaconRefund01 => Instance != null ? Instance.beaconRefund01 : 0f;
    public static float DrainMultiplier => Instance != null ? Instance.drainMultiplier : 1f;

    Lantern lantern;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Apply();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Applied through the lantern's existing drain modifier path.
    void Apply()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        lantern = player != null ? player.GetComponentInChildren<Lantern>() : FindAnyObjectByType<Lantern>();
        if (lantern != null)
        {
            lantern.SetDrainModifier(this, drainMultiplier);
        }
    }
}
}
