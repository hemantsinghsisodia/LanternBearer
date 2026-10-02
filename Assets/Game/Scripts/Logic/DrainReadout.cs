using System.Globalization;

namespace LanternKeeper
{
// Builds the HUD drain status line. Pure so the wording can be tested without a scene.
// drainRelative is the total drain against the difficulty base (escalation x modifiers x safe light).
public static class DrainReadout
{
    public static string Format(float drainRelative, int mothsDraining, int near, int total)
    {
        string line = "Drain x" + drainRelative.ToString("0.00", CultureInfo.InvariantCulture)
            + "   Moths " + near + "/" + total;
        if (mothsDraining >= 1)
        {
            line += "  • " + mothsDraining + (mothsDraining == 1 ? " moth on you" : " moths on you");
        }

        return line;
    }
}
}
