using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// Ink-coloured panel with a thin double brass frame (two outlines, inset 0 and 4 px).
// UIBuilder creates the Frame child; this component keeps the colours in step with the theme.
[RequireComponent(typeof(Image))]
public class InkPanel : MonoBehaviour
{
    [SerializeField] private UITheme theme;
    [SerializeField] private Image background;
    [SerializeField] private Graphic[] frameLines;

    public UITheme Theme { get { return theme; } }

    public void Configure(UITheme newTheme, Image bg, Graphic[] lines)
    {
        theme = newTheme;
        background = bg;
        frameLines = lines;
        Apply();
    }

    public void Apply()
    {
        if (theme == null)
        {
            return;
        }
        if (background != null)
        {
            background.color = theme.inkPanel;
        }
        if (frameLines != null)
        {
            for (int i = 0; i < frameLines.Length; i++)
            {
                if (frameLines[i] != null)
                {
                    frameLines[i].color = theme.brassLine;
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (theme == null)
        {
            theme = UnityEditor.AssetDatabase.LoadAssetAtPath<UITheme>("Assets/Game/Art/UI/UITheme.asset");
        }
    }
#endif
}
}
