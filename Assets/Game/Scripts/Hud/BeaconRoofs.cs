using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// One roof per beacon (up to MaxRoofs), amber when lit and dim brass otherwise. Roofs are cloned from an inactive template.
public class BeaconRoofs : MonoBehaviour
{
    public const int MaxRoofs = 12;
    private const float Gap = 4f;

    [SerializeField] private UITheme theme;
    [SerializeField] private Image template;
    [SerializeField] private float rowWidth = 272f;

    private readonly List<Image> roofs = new List<Image>();

    public IList<Image> Roofs { get { return roofs; } }

    public int VisibleCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < roofs.Count; i++)
            {
                count += roofs[i].gameObject.activeSelf ? 1 : 0;
            }
            return count;
        }
    }

    public int LitCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < roofs.Count; i++)
            {
                if (roofs[i].gameObject.activeSelf && roofs[i].color == theme.amber)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public void Configure(UITheme newTheme, Image roofTemplate, float width)
    {
        theme = newTheme;
        template = roofTemplate;
        rowWidth = width;
    }

    public void Show(int total, int lit)
    {
        int count = Mathf.Clamp(total, 0, MaxRoofs);
        int litCount = Mathf.Clamp(lit, 0, count);
        while (roofs.Count < count)
        {
            Image roof = Instantiate(template, transform);
            roof.name = "Roof" + roofs.Count;
            roofs.Add(roof);
        }
        Vector2 size = ((RectTransform)template.transform).sizeDelta;
        float pitch = count > 1 ? Mathf.Min(size.x + Gap, (rowWidth - size.x) / (count - 1)) : size.x;
        float width = Mathf.Min(size.x, pitch - 2f);
        float height = size.y * width / size.x;
        for (int i = 0; i < roofs.Count; i++)
        {
            Image roof = roofs[i];
            bool visible = i < count;
            roof.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }
            RectTransform rect = (RectTransform)roof.transform;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(i * pitch, 0f);
            roof.color = i < litCount ? theme.amber : theme.brassLine;
        }
    }
}
}
