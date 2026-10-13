using UnityEngine;

namespace LanternKeeper
{
// The Great Light tower on the main menu's horizon: dark with the cold glint until Island 5 has been won, then its lamp and
// turning beam (the litRoot: lamp, light and beam meshes under the tower's lantern room) come on.
public class MenuGreatLight : MonoBehaviour
{
    [SerializeField] GameObject litRoot;

    public bool IsLit { get; private set; }

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
    }
}
}
