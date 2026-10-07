using TMPro;
using UnityEngine;

namespace LanternKeeper
{
// The "E  Light beacon" prompt under the crosshair: cost and affordability text, plus the red shake after a failed light.
sealed class HudPrompt
{
    const float ShakeDuration = 0.3f;

    static readonly Color Normal = new Color(1f, 0.9f, 0.7f, 1f);
    static readonly Color Warning = new Color(1f, 0.38f, 0.28f, 1f);
    static readonly Color Flash = new Color(1f, 0.05f, 0.04f, 1f);
    static readonly Color ShakeTint = new Color(1f, 0.28f, 0.16f, 1f);

    readonly TMP_Text text;
    bool shaking;
    bool afford;
    bool homeReady;
    int cost = int.MinValue;
    string line;
    float shakeRemaining;
    Vector2 home;

    public HudPrompt(TMP_Text promptText)
    {
        text = promptText;
    }

    public void LightFailed()
    {
        shakeRemaining = ShakeDuration;
    }

    public void Hide()
    {
        if (text != null)
        {
            text.gameObject.SetActive(false);
        }
    }

    public void Refresh(GameManager manager, Lantern lantern)
    {
        if (text == null)
        {
            return;
        }

        Beacon nearest = manager.NearestBeacon;
        bool show = manager.ShowInteractPrompt && nearest != null;
        if (text.gameObject.activeSelf != show)
        {
            text.gameObject.SetActive(show);
        }

        RectTransform rect = text.rectTransform;
        if (!homeReady)
        {
            home = rect.anchoredPosition;
            homeReady = true;
        }

        if (!show)
        {
            rect.anchoredPosition = home;
            return;
        }

        if (rect.sizeDelta.x < 520f)
        {
            rect.sizeDelta = new Vector2(640f, Mathf.Max(52f, rect.sizeDelta.y));
        }

        int nextCost = Mathf.RoundToInt(nearest.FuelCost);
        bool canAfford = lantern != null && lantern.Fuel >= nearest.FuelCost;
        if (line == null || nextCost != cost || canAfford != afford)
        {
            cost = nextCost;
            afford = canAfford;
            line = canAfford ? "E  Light beacon (-" + cost + ")" : "Not enough light (-" + cost + ")";
            if (text.text != line)
            {
                text.text = line;
            }
        }

        if (shakeRemaining > 0f)
        {
            shakeRemaining -= Time.deltaTime;
            shaking = true;
            float remaining = Mathf.Clamp01(shakeRemaining / ShakeDuration);
            rect.anchoredPosition = home + new Vector2(Mathf.Sin(Time.unscaledTime * 52f) * 18f * remaining, 0f);
            text.color = Color.Lerp(Flash, ShakeTint, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 36f));
            return;
        }

        shakeRemaining = 0f;
        if (shaking || rect.anchoredPosition != home)
        {
            shaking = false;
            rect.anchoredPosition = home;
        }

        Color color = canAfford ? Normal : Warning;
        if (text.color != color)
        {
            text.color = color;
        }
    }
}
}
