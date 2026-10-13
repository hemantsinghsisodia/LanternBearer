using UnityEngine;

namespace LanternKeeper
{
// Island 5: switches the storm on as the keeper climbs. Wind and the Shade patrol start at Mid, lightning and rain at High.
// They sit in the scene switched off and are never switched off again (the ratchet). Also raises the music's tension floor and
// starts the summit hush.
public class RingHazards : MonoBehaviour
{
    [SerializeField] GameObject wind;
    [SerializeField] GameObject shadeSpawner;
    [SerializeField] GameObject lightning;
    [SerializeField] GameObject rain;
    [SerializeField] GameObject hunter;

    MusicDirector director;
    bool started;

    void OnEnable()
    {
        RingZones.ReachedRing += OnReached;
    }

    void OnDisable()
    {
        RingZones.ReachedRing -= OnReached;
    }

    void Start()
    {
        started = true;
        Apply(RingZones.Reached);
    }

    void OnReached(Ring ring)
    {
        if (started)
        {
            Apply(ring);
        }
    }

    void Apply(Ring ring)
    {
        if (ring >= Ring.Mid)
        {
            Switch(wind);
            Switch(shadeSpawner);
        }

        if (ring >= Ring.High)
        {
            Switch(lightning);
            Switch(rain);
            Switch(hunter);
        }

        if (director == null)
        {
            director = FindAnyObjectByType<MusicDirector>();
        }

        if (director != null)
        {
            director.SetTensionFloor(RingLogic.TensionFloor(ring));
            if (ring == Ring.Summit)
            {
                director.EnterSummitHush();
            }
        }
    }

    static void Switch(GameObject target)
    {
        if (target != null && !target.activeSelf)
        {
            target.SetActive(true);
        }
    }
}
}
