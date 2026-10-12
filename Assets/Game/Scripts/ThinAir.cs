using UnityEngine;

namespace LanternKeeper
{
// Island 5: from the High ring the air is thin. The lantern burns faster and throws a shorter light. Ratchets with the ring, so
// walking back down does not undo it; a scene reload does.
public class ThinAir : MonoBehaviour
{
    Lantern lantern;
    bool applied;

    public bool Applied => applied;

    void OnDisable()
    {
        Release();
    }

    void Update()
    {
        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

        if (lantern == null)
        {
            return;
        }

        if (RingLogic.ThinAirActive(RingZones.Reached))
        {
            // Re-asserted each frame so nothing that resets the lantern state can drop it.
            lantern.SetDrainModifier(this, RingLogic.ThinAirDrain);
            lantern.RadiusScale = RingLogic.ThinAirRadius;
            applied = true;
        }
    }

    void Release()
    {
        if (lantern != null && applied)
        {
            lantern.SetDrainModifier(this, 1f);
            lantern.RadiusScale = 1f;
        }

        applied = false;
    }
}
}
