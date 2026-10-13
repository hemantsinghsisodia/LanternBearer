using UnityEngine;

namespace LanternKeeper
{
// The Great Light tower on the main menu's horizon: dark with the cold glint until Island 5 has been won, then its lamp and
// turning amber beam (the litRoot: lamp, light and the GreatLampBeam under the tower's lantern room, the same look as the finale) come on.
public class MenuGreatLight : MonoBehaviour
{
    [SerializeField] GameObject litRoot;
    [SerializeField] GreatLampBeam beam;

    public bool IsLit { get; private set; }
    public GreatLampBeam Beam => beam;

    void Awake()
    {
        Refresh();
    }

    void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        IsLit = GameSettings.HasWon("island5");
        if (litRoot != null)
        {
            litRoot.SetActive(IsLit);
        }

        if (IsLit && beam != null && !beam.Running)
        {
            beam.Begin(1.5f);
        }
    }
}
}
