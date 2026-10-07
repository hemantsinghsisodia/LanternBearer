using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// Island 4's storm: a wind arrow inside a strength arc, a "Wind"/"Gust" word, and a thunder bolt while thunder is due.
// Restyled from StormHUD's wind gauge and thunder glyph. Shares its slot with TideChip.
public class StormChip : MonoBehaviour
{
    [SerializeField] private RectTransform arrow;
    [SerializeField] private Image arc;
    [SerializeField] private ThemedLabel label;
    [SerializeField] private GameObject thunder;
    [SerializeField] private Image bolt;

    public bool Active { get { return gameObject.activeSelf; } }
    public float ArrowAngle { get { return arrow.localEulerAngles.z; } }
    public float ArcFill { get { return arc.fillAmount; } }
    public bool ThunderVisible { get { return thunder.activeSelf; } }
    public float ThunderAlpha { get { return bolt.color.a; } }
    public string Text { get { return label.Text.text; } }

    public void Configure(RectTransform arrowRect, Image strengthArc, ThemedLabel word, GameObject thunderGroup, Image boltImage)
    {
        arrow = arrowRect;
        arc = strengthArc;
        label = word;
        thunder = thunderGroup;
        bolt = boltImage;
    }

    // windAngleDeg is the signed angle from the camera's forward to the wind direction (Vector3.SignedAngle about up);
    // the arrow turns the other way on screen, like StormHUD did.
    public void Show(bool active, float windAngleDeg, float windStrength01, float thunder01)
    {
        if (gameObject.activeSelf != active)
        {
            gameObject.SetActive(active);
        }
        if (!active)
        {
            return;
        }
        arrow.localEulerAngles = new Vector3(0f, 0f, -windAngleDeg);
        arc.fillAmount = Mathf.Clamp01(windStrength01);
        string word = windStrength01 > 0.01f ? "Gust" : "Wind";
        if (label.Text.text != word)
        {
            label.Text.text = word;
        }
        bool showThunder = thunder01 > 0.01f;
        thunder.SetActive(showThunder);
        if (showThunder)
        {
            Color c = bolt.color;
            c.a = Mathf.Clamp01(thunder01);
            bolt.color = c;
        }
    }
}
}
