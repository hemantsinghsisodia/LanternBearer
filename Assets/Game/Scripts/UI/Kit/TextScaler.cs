using UnityEngine;

namespace LanternKeeper
{
// Put on a screen root. Follows UserSettings.TextScale and re-applies basePx x Current to every
// ThemedLabel underneath whenever settings change. ExecuteAlways so it also works in edit mode.
[ExecuteAlways]
public class TextScaler : MonoBehaviour
{
    public static float Current
    {
        get { return UserSettings.TextScale; }
    }

    private void OnEnable()
    {
        UserSettings.Changed += Reapply;
        Reapply();
    }

    private void OnDisable()
    {
        UserSettings.Changed -= Reapply;
    }

    private void OnDestroy()
    {
        UserSettings.Changed -= Reapply;
    }

    public void Reapply()
    {
        ThemedLabel[] labels = GetComponentsInChildren<ThemedLabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].Apply();
        }
    }
}
}
