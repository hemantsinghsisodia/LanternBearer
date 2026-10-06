using UnityEngine;

namespace LanternKeeper
{
// Settings > Accessibility: text size (100/115/130%) and Reduce flashing.
public class AccessibilitySection : MonoBehaviour
{
    [SerializeField] private SwitchRow textSizeRow;
    [SerializeField] private SwitchRow reduceFlashingRow;

    public SwitchRow TextSizeRow { get { return textSizeRow; } }
    public SwitchRow ReduceFlashingRow { get { return reduceFlashingRow; } }

    public void Configure(SwitchRow textSize, SwitchRow reduceFlashing)
    {
        textSizeRow = textSize;
        reduceFlashingRow = reduceFlashing;
    }

    private void OnEnable()
    {
        textSizeRow.IndexChanged += OnTextSize;
        reduceFlashingRow.IndexChanged += OnReduceFlashing;
        Refresh();
    }

    private void OnDisable()
    {
        textSizeRow.IndexChanged -= OnTextSize;
        reduceFlashingRow.IndexChanged -= OnReduceFlashing;
    }

    public void Refresh()
    {
        float scale = UserSettings.TextScale;
        float[] scales = SettingsMath.TextScales;
        int best = 0;
        for (int i = 1; i < scales.Length; i++)
        {
            if (Mathf.Abs(scales[i] - scale) < Mathf.Abs(scales[best] - scale))
            {
                best = i;
            }
        }
        textSizeRow.SetIndexWithoutNotify(best);
        reduceFlashingRow.SetIndexWithoutNotify(UserSettings.ReduceFlashing ? 1 : 0);
    }

    private static void OnTextSize(int index)
    {
        float[] scales = SettingsMath.TextScales;
        UserSettings.TextScale = scales[Mathf.Clamp(index, 0, scales.Length - 1)];
    }

    private static void OnReduceFlashing(int index)
    {
        UserSettings.ReduceFlashing = index == 1;
    }
}
}
