using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// Moth xN, shield, ember and the drain multiplier. Each shows only while its condition holds.
public class DrainIcons : MonoBehaviour
{
    [SerializeField] private GameObject mothGroup;
    [SerializeField] private ThemedLabel mothCount;
    [SerializeField] private GameObject shield;
    [SerializeField] private GameObject ember;
    [SerializeField] private ThemedLabel multiplier;
    [SerializeField] private Image backing;

    private int shownMoths = -1;

    public bool MothVisible { get { return mothGroup.activeSelf; } }
    public string MothText { get { return mothCount.Text.text; } }
    public bool ShieldVisible { get { return shield.activeSelf; } }
    public bool EmberVisible { get { return ember.activeSelf; } }
    public bool MultiplierVisible { get { return multiplier.gameObject.activeSelf; } }
    public string MultiplierText { get { return multiplier.Text.text; } }

    public void Configure(GameObject moth, ThemedLabel count, GameObject shieldIcon, GameObject emberIcon, ThemedLabel multiplierLabel, Image back)
    {
        mothGroup = moth;
        mothCount = count;
        shield = shieldIcon;
        ember = emberIcon;
        multiplier = multiplierLabel;
        backing = back;
    }

    public void Show(HudMath.DrainIconsView v)
    {
        mothGroup.SetActive(v.Moth);
        if (v.Moth)
        {
            if (v.MothCount != shownMoths)
            {
                shownMoths = v.MothCount;
                mothCount.Text.text = "×" + v.MothCount;
            }
        }
        shield.SetActive(v.Shield);
        ember.SetActive(v.Ember);
        multiplier.gameObject.SetActive(v.Multiplier);
        if (v.Multiplier)
        {
            multiplier.Text.text = v.MultiplierText;
        }
        backing.enabled = v.Moth || v.Shield || v.Ember || v.Multiplier;
    }
}
}
