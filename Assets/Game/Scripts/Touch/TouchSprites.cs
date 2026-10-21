using System;
using UnityEngine;

namespace LanternKeeper
{
/// <summary>Procedural 128 px white sprites for the touch controls (tinted at runtime). Built once, lazily.</summary>
static class TouchSprites
{
    const int Size = 128;
    // Pixels per unit in the -1..1 sprite space, for edge anti-aliasing.
    const float Px = Size * 0.5f;

    static Sprite disc;
    static Sprite ring;
    static Sprite jump;
    static Sprite flame;
    static Sprite sprint;
    static Sprite pause;

    public static Sprite Disc { get { return Get(ref disc, DiscAlpha); } }
    public static Sprite Ring { get { return Get(ref ring, RingAlpha); } }
    public static Sprite Jump { get { return Get(ref jump, JumpAlpha); } }
    public static Sprite Flame { get { return Get(ref flame, FlameAlpha); } }
    public static Sprite Sprint { get { return Get(ref sprint, SprintAlpha); } }
    public static Sprite Pause { get { return Get(ref pause, PauseAlpha); } }

    static Sprite Get(ref Sprite slot, Func<float, float, float> alpha)
    {
        if (slot == null)
        {
            slot = Make(alpha);
        }

        return slot;
    }

    static Sprite Make(Func<float, float, float> alphaAt)
    {
        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;
        Color32[] pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float fx = (x + 0.5f) / Size * 2f - 1f;
                float fy = (y + 0.5f) / Size * 2f - 1f;
                float a = Mathf.Clamp01(alphaAt(fx, fy));
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    static float Cover(float signedDistance)
    {
        return 0.5f - signedDistance * Px;
    }

    static float DiscAlpha(float x, float y)
    {
        return Cover(Mathf.Sqrt(x * x + y * y) - 0.98f);
    }

    static float RingAlpha(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        return Mathf.Min(Cover(r - 0.98f), Cover(0.88f - r));
    }

    static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax;
        float dy = by - ay;
        float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
        float cx = ax + dx * t - px;
        float cy = ay + dy * t - py;
        return Mathf.Sqrt(cx * cx + cy * cy);
    }

    static float Stroke(float px, float py, float ax, float ay, float bx, float by)
    {
        return Cover(SegmentDistance(px, py, ax, ay, bx, by) - 0.09f);
    }

    static float JumpAlpha(float x, float y)
    {
        float a = 0f;
        float[] tops = { 0.5f, 0.05f };
        for (int i = 0; i < tops.Length; i++)
        {
            float c = tops[i];
            a = Mathf.Max(a, Stroke(x, y, -0.5f, c - 0.45f, 0f, c));
            a = Mathf.Max(a, Stroke(x, y, 0f, c, 0.5f, c - 0.45f));
        }

        return a;
    }

    static float SprintAlpha(float x, float y)
    {
        float a = 0f;
        float[] tips = { 0.45f, 0f };
        for (int i = 0; i < tips.Length; i++)
        {
            float c = tips[i];
            a = Mathf.Max(a, Stroke(x, y, c - 0.45f, 0.5f, c, 0f));
            a = Mathf.Max(a, Stroke(x, y, c, 0f, c - 0.45f, -0.5f));
        }

        return a;
    }

    static float PauseAlpha(float x, float y)
    {
        float a = 0f;
        float[] centres = { -0.24f, 0.24f };
        for (int i = 0; i < centres.Length; i++)
        {
            float sd = Mathf.Max(Mathf.Abs(x - centres[i]) - 0.11f, Mathf.Abs(y) - 0.5f);
            a = Mathf.Max(a, Cover(sd));
        }

        return a;
    }

    static float FlameAlpha(float x, float y)
    {
        float t = (y + 0.75f) / 1.6f;
        if (t < 0f || t > 1f)
        {
            return 0f;
        }

        float width = 0.46f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f)), 0.9f) * (1f - 0.35f * t);
        return (width - Mathf.Abs(x)) * Px + 0.5f;
    }
}
}
