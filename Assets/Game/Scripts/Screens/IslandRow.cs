using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// What one island row shows.
public struct IslandInfo
{
    public string name;
    public int beacons;
    public int found;
    public float bestTime;
    public bool unlocked;
}

// One row of the island list: roof icons, the name, the best time and a lock state. A tab-style ThemedButton.
public class IslandRow : MonoBehaviour
{
    [SerializeField] private ThemedButton button;
    [SerializeField] private Image[] roofs = new Image[0];
    [SerializeField] private ThemedLabel timeLabel;
    [SerializeField] private ThemedLabel lockLabel;
    [SerializeField] private ThemedLabel countLabel;

    public const float RoofStartX = 20f;
    public const float RoofPitch = 24f;

    public ThemedButton Button { get { return button; } }
    public Image[] Roofs { get { return roofs; } }
    public ThemedLabel TimeLabel { get { return timeLabel; } }
    public ThemedLabel LockLabel { get { return lockLabel; } }
    public ThemedLabel CountLabel { get { return countLabel; } }

    public void Configure(ThemedButton rowButton, Image[] roofImages, ThemedLabel time, ThemedLabel locked, ThemedLabel count)
    {
        countLabel = count;
        button = rowButton;
        roofs = roofImages;
        timeLabel = time;
        lockLabel = locked;
    }

    public void Show(IslandInfo info, bool selected, Color amber, Color off, Color dim, Color timeColour)
    {
        bool unlocked = info.unlocked;
        button.interactable = unlocked;
        button.Label.gameObject.SetActive(unlocked);
        lockLabel.gameObject.SetActive(!unlocked);
        timeLabel.gameObject.SetActive(unlocked);
        if (unlocked)
        {
            button.Label.Text.text = info.name;
            timeLabel.Text.text = GameManager.FormatTime(info.bestTime);
        }
        else
        {
            lockLabel.Text.text = info.name + " · locked";
            lockLabel.SetColourOverride(true, dim);
        }
        timeLabel.SetColourOverride(true, selected ? amber : timeColour);
        int lit = unlocked ? IslandList.AmberRoofs(info.found, info.beacons) : 0;
        int count = unlocked ? Mathf.Min(info.beacons, roofs.Length) : 0;
        if (countLabel != null)
        {
            // "found/beacons" sits right after the last roof, so the roofs read as beacons, not a rating.
            countLabel.gameObject.SetActive(unlocked && info.beacons > 0);
            countLabel.Text.text = lit + "/" + Mathf.Max(0, info.beacons);
            countLabel.SetColourOverride(true, timeColour);
            RectTransform rt = (RectTransform)countLabel.transform;
            rt.anchoredPosition = new Vector2(RoofStartX + count * RoofPitch + 6f, 0f);
        }
        for (int i = 0; i < roofs.Length; i++)
        {
            roofs[i].gameObject.SetActive(i < count);
            roofs[i].color = i < lit ? amber : off;
        }
        button.SetMarked(selected && unlocked);
    }
}
}
