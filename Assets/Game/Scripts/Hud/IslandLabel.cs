using UnityEngine;

namespace LanternKeeper
{
// The island name, top-centre. Empty text hides it (and its backing).
public class IslandLabel : MonoBehaviour
{
    [SerializeField] private ThemedLabel label;
    [SerializeField] private GameObject chip;

    public string Text { get { return label.Text.text; } }

    public void Configure(ThemedLabel islandLabel, GameObject chipRoot)
    {
        label = islandLabel;
        chip = chipRoot;
    }

    public void Show(string text)
    {
        string value = text ?? string.Empty;
        if (label.Text.text != value)
        {
            label.Text.text = value;
        }
        chip.SetActive(value.Length > 0);
    }
}
}
