using System.Text;
using UnityEngine;

namespace LanternKeeper
{
// The island intro card: ink panel with a brass frame, title, story lines and a footer prompt. It fades in and out over
// FadeSeconds on unscaled time. The coordinator decides when it shows (first visit, or How to Play from the pause menu).
public class IntroCard : MonoBehaviour
{
    public const float FadeSeconds = 0.3f;

    [SerializeField] private CanvasGroup group;
    [SerializeField] private GameObject panel;
    [SerializeField] private ThemedLabel title;
    [SerializeField] private ThemedLabel body;
    [SerializeField] private ThemedLabel footer;

    private bool wanted;

    private const int TipsThatFit = 5;
    private const float ExtraTipHeight = 72f;
    private float baseHeight = -1f;

    public bool IsShowing { get { return wanted; } }
    public float Alpha { get { return group != null ? group.alpha : 0f; } }
    public bool PanelActive { get { return panel.activeSelf; } }
    public ThemedLabel Title { get { return title; } }
    public ThemedLabel Body { get { return body; } }
    public ThemedLabel Footer { get { return footer; } }

    public void Configure(CanvasGroup canvasGroup, GameObject cardPanel, ThemedLabel titleLabel, ThemedLabel bodyLabel, ThemedLabel footerLabel)
    {
        group = canvasGroup;
        panel = cardPanel;
        title = titleLabel;
        body = bodyLabel;
        footer = footerLabel;
    }

    private void OnEnable()
    {
        if (!wanted && panel != null)
        {
            panel.SetActive(false);
        }
    }

    public void Show(string titleText, string[] lines, string footerText)
    {
        title.Text.text = titleText ?? "";
        body.Text.text = BuildBody(lines);
        GrowPanelFor(lines);
        footer.Text.text = footerText ?? "";
        if (!wanted)
        {
            wanted = true;
            panel.SetActive(true);
            group.alpha = 0f;
        }
    }

    // The card is sized for five tips. Each extra tip makes the panel taller (the body box stretches with it) so the text
    // keeps its size instead of shrinking or overflowing at a large text scale.
    private void GrowPanelFor(string[] lines)
    {
        RectTransform rect = panel.transform as RectTransform;
        if (rect == null)
        {
            return;
        }

        if (baseHeight < 0f)
        {
            baseHeight = rect.sizeDelta.y;
        }

        int extra = lines != null ? Mathf.Max(0, lines.Length - TipsThatFit) : 0;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, baseHeight + extra * ExtraTipHeight);
    }

    public void Hide()
    {
        wanted = false;
    }

    private void Update()
    {
        Tick(Time.unscaledDeltaTime);
    }

    // Moves the fade by dt seconds; tests call it with fixed steps.
    public void Tick(float dt)
    {
        float target = wanted ? 1f : 0f;
        if (!wanted && !panel.activeSelf)
        {
            return;
        }
        group.alpha = Mathf.MoveTowards(group.alpha, target, Mathf.Max(0f, dt) / FadeSeconds);
        if (!wanted && group.alpha <= 0f)
        {
            panel.SetActive(false);
        }
    }

    public static string BuildBody(string[] lines)
    {
        if (lines == null)
        {
            return "";
        }
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }
            builder.Append("<indent=1.3em><line-indent=-1.3em>•  ").Append(lines[i]).Append("</line-indent></indent>");
        }
        return builder.ToString();
    }
}
}
