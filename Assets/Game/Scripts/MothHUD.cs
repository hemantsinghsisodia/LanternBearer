using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// Moth drain feedback on the HUD, created by HUD only on islands that have a MothSpawner.
// While any moth is draining the lantern: the drain text goes violet and pulses, a violet rim pulses
// around the fuel gauge, and pale specks drift off its edge. Nothing advances while the game is paused.
public class MothHUD : MonoBehaviour
{
    const int SpeckCount = 12;
    const float PulseHz = 2f;
    const float SpawnInterval = 0.15f;
    const float SpeckLife = 0.8f;
    const float RimFade = 0.3f;

    static readonly Color DrainColor = new Color(0.78f, 0.55f, 1f, 1f);
    static readonly Color RimColor = new Color(0.78f, 0.55f, 1f, 1f);
    static readonly Color SpeckColor = new Color(0.92f, 0.9f, 1f, 1f);
    static Sprite ringSprite;

    Lantern lantern;
    RectTransform meter;
    TMP_Text statusText;
    Image rim;
    Image[] specks;
    RectTransform[] speckRects;
    float[] speckAge;
    Vector2[] speckVelocity;
    float rimAmount;
    float pulseClock;
    float spawnClock;
    int cursor;

    public void Bind(Lantern playerLantern, RectTransform fuelMeter, TMP_Text status)
    {
        lantern = playerLantern;
        meter = fuelMeter;
        statusText = status;
        Build();
    }

    void Build()
    {
        if (meter == null || rim != null)
        {
            return;
        }

        GameObject rimObject = new GameObject("MothRim", typeof(RectTransform), typeof(Image));
        rimObject.transform.SetParent(meter, false);
        // Behind the gauge track and fill so only the outer ring shows.
        rimObject.transform.SetSiblingIndex(Mathf.Min(1, meter.childCount - 1));
        RectTransform rimRect = rimObject.GetComponent<RectTransform>();
        rimRect.anchorMin = new Vector2(0.5f, 0.5f);
        rimRect.anchorMax = new Vector2(0.5f, 0.5f);
        rimRect.anchoredPosition = Vector2.zero;
        rimRect.sizeDelta = new Vector2(124f, 124f);
        rim = rimObject.GetComponent<Image>();
        rim.sprite = RingSprite();
        rim.raycastTarget = false;
        rim.color = new Color(RimColor.r, RimColor.g, RimColor.b, 0f);
        rimObject.SetActive(false);

        specks = new Image[SpeckCount];
        speckRects = new RectTransform[SpeckCount];
        speckAge = new float[SpeckCount];
        speckVelocity = new Vector2[SpeckCount];
        for (int i = 0; i < SpeckCount; i++)
        {
            GameObject speck = new GameObject("MothSpeck", typeof(RectTransform), typeof(Image));
            speck.transform.SetParent(meter, false);
            RectTransform rect = speck.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            Image image = speck.GetComponent<Image>();
            image.sprite = HUD.Circle();
            image.raycastTarget = false;
            image.color = SpeckColor;
            speck.SetActive(false);
            specks[i] = image;
            speckRects[i] = rect;
            speckAge[i] = SpeckLife;
        }
    }

    void Update()
    {
        if (lantern == null || rim == null)
        {
            return;
        }

        GameManager manager = GameManager.Instance;
        if (manager != null && manager.IsPaused)
        {
            return;
        }

        float dt = Time.deltaTime;
        bool draining = lantern.MothsDraining >= 1 && !(manager != null && (manager.IsRoundOver || manager.IsDying));
        pulseClock += dt;
        float pulse = 0.5f + 0.5f * Mathf.Sin(pulseClock * PulseHz * Mathf.PI * 2f);
        TickStatus(draining, pulse);
        TickRim(draining, pulse, dt);
        TickSpecks(draining, dt);
    }

    // Violet pulsing text while draining. HUD repaints the base colour when the draining count changes.
    void TickStatus(bool draining, float pulse)
    {
        if (statusText == null || !draining)
        {
            return;
        }

        Color color = DrainColor;
        color.a = 0.72f + 0.28f * pulse;
        statusText.color = color;
    }

    void TickRim(bool draining, float pulse, float dt)
    {
        rimAmount = Mathf.MoveTowards(rimAmount, draining ? 1f : 0f, dt / RimFade);
        bool show = rimAmount > 0.001f;
        if (rim.gameObject.activeSelf != show)
        {
            rim.gameObject.SetActive(show);
        }

        if (!show)
        {
            return;
        }

        float alpha = rimAmount * (0.4f + 0.5f * pulse);
        rim.color = new Color(RimColor.r, RimColor.g, RimColor.b, alpha);
        float scale = 1f + 0.06f * pulse * rimAmount;
        rim.rectTransform.localScale = new Vector3(scale, scale, 1f);
    }

    void TickSpecks(bool draining, float dt)
    {
        if (draining)
        {
            spawnClock += dt;
            if (spawnClock >= SpawnInterval)
            {
                spawnClock -= SpawnInterval;
                Spawn();
            }
        }
        else
        {
            spawnClock = SpawnInterval;
        }

        for (int i = 0; i < SpeckCount; i++)
        {
            if (speckAge[i] >= SpeckLife)
            {
                continue;
            }

            speckAge[i] += dt;
            if (speckAge[i] >= SpeckLife)
            {
                specks[i].gameObject.SetActive(false);
                continue;
            }

            float t = speckAge[i] / SpeckLife;
            speckRects[i].anchoredPosition += speckVelocity[i] * dt;
            Color color = SpeckColor;
            color.a = (1f - t) * 0.9f;
            specks[i].color = color;
        }
    }

    void Spawn()
    {
        // First free slot, scanning from the last one used. If all are live, skip this speck.
        int slot = -1;
        for (int n = 0; n < SpeckCount; n++)
        {
            int index = (cursor + n) % SpeckCount;
            if (speckAge[index] >= SpeckLife)
            {
                slot = index;
                break;
            }
        }

        if (slot < 0)
        {
            return;
        }

        cursor = (slot + 1) % SpeckCount;
        float angle = Random.value * Mathf.PI * 2f;
        Vector2 outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float size = Random.Range(4f, 6f);
        speckRects[slot].sizeDelta = new Vector2(size, size);
        speckRects[slot].anchoredPosition = outward * 46f;
        speckVelocity[slot] = outward * Random.Range(10f, 18f) + new Vector2(0f, Random.Range(10f, 16f));
        speckAge[slot] = 0f;
        specks[slot].gameObject.SetActive(true);
    }

    static Sprite RingSprite()
    {
        if (ringSprite != null)
        {
            return ringSprite;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "MothRim";
        texture.wrapMode = TextureWrapMode.Clamp;
        float center = (size - 1) * 0.5f;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                // Soft ring peaking at 0.88 of the radius, about 0.1 wide.
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.88f) / 0.11f);
                byte alpha = (byte)Mathf.RoundToInt(a * a * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        ringSprite.name = "MothRim";
        return ringSprite;
    }
}
}
