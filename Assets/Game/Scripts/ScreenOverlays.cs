using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// The full-screen fade and death-vignette images that HUD drives for GameManager and WaterHazard.
sealed class ScreenOverlays
{
    static Sprite whiteSprite;
    static Sprite vignetteSprite;

    Image fade;
    Image death;

    public float FadeAlpha => fade != null ? fade.color.a : 0f;
    public float DeathAmount { get; private set; }

    // Uses the scene's images when present and makes missing ones under the canvas.
    public void Bind(Transform canvas, Image sceneFade, Image sceneDeath)
    {
        fade = sceneFade != null ? sceneFade : (fade != null ? fade : Make(canvas, "FadeOverlay", null));
        death = sceneDeath != null ? sceneDeath : (death != null ? death : Make(canvas, "DeathOverlay", Vignette()));
        fade.sprite = White();
        fade.type = Image.Type.Simple;
    }

    public void SetFade(float alpha)
    {
        Color color = Color.black;
        color.a = Mathf.Clamp01(alpha);
        fade.color = color;
        fade.raycastTarget = false;
        fade.enabled = color.a > 0.01f;
    }

    public void SetDeath(float amount)
    {
        DeathAmount = Mathf.Clamp01(amount);
        death.sprite = Vignette();
        death.type = Image.Type.Simple;
        death.preserveAspect = false;
        Color color = Color.black;
        color.a = DeathAmount;
        death.color = color;
        death.raycastTarget = false;
        death.enabled = DeathAmount > 0.01f;
        if (DeathAmount > 0.45f)
        {
            Color black = Color.black;
            black.a = Mathf.InverseLerp(0.45f, 1f, DeathAmount);
            fade.color = black;
            fade.enabled = black.a > 0.01f;
        }
    }

    static Image Make(Transform parent, string name, Sprite sprite)
    {
        GameObject overlay = new GameObject(name, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(parent, false);
        overlay.transform.SetAsFirstSibling();
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = overlay.GetComponent<Image>();
        image.sprite = sprite != null ? sprite : White();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
        return image;
    }

    static Sprite White()
    {
        if (whiteSprite != null)
        {
            return whiteSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        whiteSprite.name = "HUDWhite";
        return whiteSprite;
    }

    static Sprite Vignette()
    {
        if (vignetteSprite != null)
        {
            return vignetteSprite;
        }

        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f;
                float dy = (y + 0.5f) / size - 0.5f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                pixels[y * size + x] = new Color(0f, 0f, 0f, Mathf.SmoothStep(0.02f, 0.7f, radius));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false);
        vignetteSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        vignetteSprite.name = "DeathVignette";
        return vignetteSprite;
    }
}
}
