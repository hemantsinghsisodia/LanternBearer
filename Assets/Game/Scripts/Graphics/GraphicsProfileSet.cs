using UnityEngine;

namespace LanternKeeper
{
public class GraphicsProfileSet : ScriptableObject
{
    public GraphicsProfile low;
    public GraphicsProfile medium;
    public GraphicsProfile high;
    public GraphicsProfile ultra;

    public GraphicsProfile Get(GraphicsLevel level)
    {
        if (level == GraphicsLevel.Low)
        {
            return low;
        }

        if (level == GraphicsLevel.High)
        {
            return high;
        }

        if (level == GraphicsLevel.Ultra)
        {
            return ultra;
        }

        return medium;
    }
}
}
