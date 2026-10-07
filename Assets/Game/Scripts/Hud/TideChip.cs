using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// Island 3's tide: a rising or falling arrow, a short level bar and a word, in the cool tide colour.
// Shares its slot with StormChip; only one is active on an island.
public class TideChip : MonoBehaviour
{
    public static readonly Color TideColour = new Color(0.55f, 0.78f, 0.91f, 1f);

    [SerializeField] private RectTransform arrow;
    [SerializeField] private Image barFill;
    [SerializeField] private ThemedLabel label;

    public bool Active { get { return gameObject.activeSelf; } }
    public float ArrowAngle { get { return arrow.localEulerAngles.z; } }
    public float Level { get { return barFill.rectTransform.anchorMax.x; } }
    public string Text { get { return label.Text.text; } }

    public void Configure(RectTransform arrowRect, Image fill, ThemedLabel word)
    {
        arrow = arrowRect;
        barFill = fill;
        label = word;
    }

    public void Show(bool active, bool rising, float level01)
    {
        if (gameObject.activeSelf != active)
        {
            gameObject.SetActive(active);
        }
        if (!active)
        {
            return;
        }
        arrow.localEulerAngles = new Vector3(0f, 0f, rising ? 0f : 180f);
        RectTransform fill = barFill.rectTransform;
        fill.anchorMax = new Vector2(Mathf.Clamp01(level01), 1f);
        fill.offsetMax = Vector2.zero;
        string word = rising ? "Rising" : "Falling";
        if (label.Text.text != word)
        {
            label.Text.text = word;
        }
    }
}
}
