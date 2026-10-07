using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
// Draws the HUD icons as white-on-transparent PNGs (so Image.color tints them) from small shape functions, 4x4 supersampled.
// Deterministic: the same code writes the same bytes, and a file is only rewritten when its bytes would change.
public static class HudIcons
{
    public const string Folder = "Assets/Game/Art/UI/Hud";
    public const int Size = 128;

    public static readonly string[] Names =
    {
        "Icon_Lantern", "Icon_Flame", "Icon_Glow", "Icon_Roof", "Icon_Moth", "Icon_Shield", "Icon_Ember",
        "Icon_TideArrow", "Icon_WindArrow", "Icon_Ring", "Icon_Bolt"
    };

    // Alpha (0..1) of the shape at a point in the unit square, y up.
    delegate float Shape(float x, float y);

    // Same silhouette as the main-menu roof (UIBuilder.EnsureRoofSprite).
    static readonly Vector2[] RoofPoly =
    {
        new Vector2(0.5f, 1f), new Vector2(1f, 0.45f), new Vector2(0.85f, 0.45f), new Vector2(0.85f, 0f),
        new Vector2(0.15f, 0f), new Vector2(0.15f, 0.45f), new Vector2(0f, 0.45f)
    };

    static readonly Vector2[] MothPoly =
    {
        new Vector2(0.5f, 0.6f), new Vector2(1f, 1f), new Vector2(0.85f, 0.3f), new Vector2(0.5f, 0f),
        new Vector2(0.15f, 0.3f), new Vector2(0f, 1f)
    };

    static readonly Vector2[] ShieldPoly =
    {
        new Vector2(0.5f, 1f), new Vector2(1f, 0.8f), new Vector2(0.9f, 0.3f), new Vector2(0.5f, 0f),
        new Vector2(0.1f, 0.3f), new Vector2(0f, 0.8f)
    };

    static readonly Vector2[] TideArrowPoly =
    {
        new Vector2(0.5f, 1f), new Vector2(0.95f, 0.5f), new Vector2(0.65f, 0.5f), new Vector2(0.65f, 0f),
        new Vector2(0.35f, 0f), new Vector2(0.35f, 0.5f), new Vector2(0.05f, 0.5f)
    };

    static readonly Vector2[] WindArrowPoly =
    {
        new Vector2(0.5f, 1f), new Vector2(0.9f, 0.1f), new Vector2(0.5f, 0.3f), new Vector2(0.1f, 0.1f)
    };

    static readonly Vector2[] BoltPoly =
    {
        new Vector2(0.62f, 1f), new Vector2(0.18f, 0.42f), new Vector2(0.46f, 0.42f), new Vector2(0.34f, 0f),
        new Vector2(0.82f, 0.62f), new Vector2(0.54f, 0.62f)
    };

    static readonly Vector2[] CapPoly =
    {
        new Vector2(0.5f, 0.98f), new Vector2(0.84f, 0.80f), new Vector2(0.16f, 0.80f)
    };

    public static string PathFor(string name)
    {
        return Folder + "/" + name + ".png";
    }

    // Makes sure every icon exists, is current and imported as a sprite. Returns name -> sprite.
    public static Dictionary<string, Sprite> Ensure()
    {
        Directory.CreateDirectory(Folder);
        Dictionary<string, Sprite> result = new Dictionary<string, Sprite>();
        bool wrote = false;
        for (int i = 0; i < Names.Length; i++)
        {
            string path = PathFor(Names[i]);
            byte[] bytes = Render(Names[i]);
            if (!File.Exists(path) || !SameBytes(File.ReadAllBytes(path), bytes))
            {
                File.WriteAllBytes(path, bytes);
                wrote = true;
            }
        }
        if (wrote)
        {
            AssetDatabase.Refresh();
        }
        for (int i = 0; i < Names.Length; i++)
        {
            string path = PathFor(Names[i]);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && NeedsImport(importer))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = Size;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            result[Names[i]] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return result;
    }

    static bool NeedsImport(TextureImporter importer)
    {
        return importer.textureType != TextureImporterType.Sprite
            || importer.mipmapEnabled
            || !importer.alphaIsTransparency
            || importer.maxTextureSize != Size
            || importer.textureCompression != TextureImporterCompression.Uncompressed;
    }

    static bool SameBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    public static byte[] Render(string name)
    {
        int w = Size;
        int h = Size;
        Shape shape;
        switch (name)
        {
            case "Icon_Lantern": shape = Lantern; break;
            case "Icon_Flame": shape = (x, y) => Drop(x, y, 0.30f, 0.7f); break;
            case "Icon_Ember": shape = (x, y) => Drop(x, y, 0.36f, 0.9f); break;
            case "Icon_Glow": shape = Glow; break;
            case "Icon_Roof": shape = Poly(RoofPoly); h = 104; break;
            case "Icon_Moth": shape = (x, y) => Mathf.Max(Poly(MothPoly)(x, y), Body(x, y)); break;
            case "Icon_Shield": shape = Poly(ShieldPoly); break;
            case "Icon_TideArrow": shape = Poly(TideArrowPoly); break;
            case "Icon_WindArrow": shape = Poly(WindArrowPoly); break;
            case "Icon_Ring": shape = Ring; break;
            case "Icon_Bolt": shape = Poly(BoltPoly); break;
            default: throw new ArgumentException("Unknown icon " + name);
        }
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        const int n = 4;
        for (int py = 0; py < h; py++)
        {
            for (int px = 0; px < w; px++)
            {
                float sum = 0f;
                for (int sy = 0; sy < n; sy++)
                {
                    for (int sx = 0; sx < n; sx++)
                    {
                        sum += shape((px + (sx + 0.5f) / n) / w, (py + (sy + 0.5f) / n) / h);
                    }
                }
                pixels[py * w + px] = new Color(1f, 1f, 1f, sum / (n * n));
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);
        return bytes;
    }

    static Shape Poly(Vector2[] poly)
    {
        return (x, y) => Inside(poly, x, y) ? 1f : 0f;
    }

    static bool Inside(Vector2[] poly, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > y) != (poly[j].y > y)
                && x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    // Round at the bottom, pointed at the top.
    static float Drop(float x, float y, float halfWidth, float bulge)
    {
        float t = (y - 0.04f) / 0.92f;
        if (t < 0f || t > 1f)
        {
            return 0f;
        }
        float hw = halfWidth * Mathf.Sin(Mathf.PI * Mathf.Pow(t, bulge));
        return Mathf.Abs(x - 0.5f) < hw ? 1f : 0f;
    }

    static float Body(float x, float y)
    {
        return Mathf.Abs(x - 0.5f) < 0.045f && y > 0.04f && y < 0.7f ? 1f : 0f;
    }

    // Storm lantern: cap, glass in an iron frame (the glass is a faint tint), base.
    static float Lantern(float x, float y)
    {
        if (Inside(CapPoly, x, y))
        {
            return 1f;
        }
        if (x >= 0.14f && x <= 0.86f && y >= 0.08f && y <= 0.22f)
        {
            return 1f;
        }
        if (x >= 0.22f && x <= 0.78f && y > 0.22f && y < 0.80f)
        {
            float edge = Mathf.Min(Mathf.Min(x - 0.22f, 0.78f - x), Mathf.Min(y - 0.22f, 0.80f - y));
            return edge < 0.055f ? 1f : 0.2f;
        }
        return 0f;
    }

    static float Glow(float x, float y)
    {
        float r = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) / 0.5f;
        float a = Mathf.Clamp01(1f - r);
        return a * a;
    }

    static float Ring(float x, float y)
    {
        float r = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
        return r <= 0.5f && r >= 0.38f ? 1f : 0f;
    }
}
}
