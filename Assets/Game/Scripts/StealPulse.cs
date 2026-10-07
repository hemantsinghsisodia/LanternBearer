using UnityEngine;

namespace LanternKeeper
{
// Gameplay feedback on Island 4: the screen edge pulses dark when a Shade steals fuel. HUD adds this only where wind or lightning exists.
public class StealPulse : MonoBehaviour
{
    LowFuelFX edgeFx;
    bool searched;

    void OnEnable()
    {
        Shade.Stole += OnStole;
    }

    void OnDisable()
    {
        Shade.Stole -= OnStole;
    }

    void OnStole(float amount)
    {
        if (!searched)
        {
            searched = true;
            edgeFx = FindAnyObjectByType<LowFuelFX>();
        }

        if (edgeFx != null)
        {
            edgeFx.PulseEdge(1f);
        }
    }
}
}
