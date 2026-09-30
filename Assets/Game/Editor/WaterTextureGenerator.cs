using System.IO;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
public static class WaterTextureGenerator
{
    const int Size = 1024;
    const int Seed = 0x4C4B5741;
    const string NormalAPath = "Assets/Game/Textures/Water/WaterRippleNormalA.png";
    const string NormalBPath = "Assets/Game/Textures/Water/WaterRippleNormalB.png";
    const string FoamPath = "Assets/Game/Textures/Water/WaterFoam.png";

    // Fine, tileable ripples. Periods are whole cycles across the map so the wrap is exact.
    const int NormalAPeriodX = 44;
    const int NormalAPeriodY = 64;
    const int NormalAOctaves = 5;
    const float NormalAPersistence = 0.5f;
    const float NormalAStrengthX = 4.05f;
    const float NormalAStrengthY = 5.62f;

    const int NormalBPeriodX = 58;
    const int NormalBPeriodY = 82;
    const int NormalBOctaves = 5;
    const float NormalBPersistence = 0.54f;
    const float NormalBStrengthX = 3.25f;
    const float NormalBStrengthY = 4.55f;

    const int FoamCellsA = 8;
    const int FoamCellsB = 14;
    const float FoamBias = 0.31f;
    const float FoamGain = 1.8f;
    const float FoamPower = 1.5f;
    const float FoamRedScale = 0.42f;
    const float FoamRedBias = 0.40f;

    [MenuItem("Lantern Keeper/Generate Water Textures")]
    public static void Generate()
    {
        WriteNormal(NormalAPath, Seed + 11, NormalAPeriodX, NormalAPeriodY, NormalAOctaves, NormalAPersistence, NormalAStrengthX, NormalAStrengthY);
        WriteNormal(NormalBPath, Seed + 7907, NormalBPeriodX, NormalBPeriodY, NormalBOctaves, NormalBPersistence, NormalBStrengthX, NormalBStrengthY);
        WriteFoam(FoamPath, Seed + 401);
        AssetDatabase.SaveAssets();
        Debug.Log("Generated tileable water textures at " + Size + " with seed " + Seed + ".");
    }

    static void WriteNormal(string path, int seed, int periodX, int periodY, int octaves, float persistence, float strengthX, float strengthY)
    {
        float[] height = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                height[y * Size + x] = RippleHeight(x, y, seed, periodX, periodY, octaves, persistence);
            }
        }

        Color32[] pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            int yUp = (y + 1) % Size;
            int yDown = (y + Size - 1) % Size;
            for (int x = 0; x < Size; x++)
            {
                int xRight = (x + 1) % Size;
                int xLeft = (x + Size - 1) % Size;
                float left = height[y * Size + xLeft];
                float right = height[y * Size + xRight];
                float down = height[yDown * Size + x];
                float up = height[yUp * Size + x];
                float dx = (left - right) * strengthX;
                float dy = (down - up) * strengthY;
                float inv = 1f / Mathf.Sqrt(dx * dx + dy * dy + 1f);
                byte r = Encode(dx * inv);
                byte g = Encode(dy * inv);
                byte b = Encode(inv);
                pixels[y * Size + x] = new Color32(r, g, b, 255);
            }
        }

        SavePng(path, pixels, true);
    }

    static void WriteFoam(string path, int seed)
    {
        Color32[] pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float broad = SoftCell(x, y, FoamCellsA, seed, 1.05f, 1.15f);
                float fine = SoftCell(x, y, FoamCellsB, seed + 211, 1.0f, 1.3f);
                float cloud = Fbm(x, y, 3, 4, 5, 0.55f, seed + 17);
                float detail = Fbm(x, y, 8, 10, 3, 0.5f, seed + 900);
                float foam = broad * 0.22f + fine * 0.16f + cloud * 0.48f + detail * 0.14f;
                foam = Mathf.Clamp01((foam - FoamBias) * FoamGain);
                foam = Mathf.Pow(foam, FoamPower);
                byte mask = (byte)Mathf.RoundToInt(foam * 255f);
                float red = Mathf.Clamp01(foam * FoamRedScale + FoamRedBias);
                byte r = (byte)Mathf.RoundToInt(red * 255f);
                pixels[y * Size + x] = new Color32(r, mask, mask, 255);
            }
        }

        SavePng(path, pixels, false);
    }

    static float RippleHeight(int x, int y, int seed, int periodX, int periodY, int octaves, float persistence)
    {
        float broad = Fbm(x, y, periodX, periodY, octaves, persistence, seed);
        int crossX = Mathf.Max(4, periodY / 2);
        int crossY = Mathf.Max(4, periodX);
        float cross = Fbm(x, y, crossX, crossY, octaves, persistence * 0.9f, seed + 4099);
        float streak = 0.5f + 0.5f * Mathf.Sin((x * 29 + y * 7) * (Mathf.PI * 2f / Size));
        streak += 0.35f * (0.5f + 0.5f * Mathf.Sin((x * 13 - y * 23) * (Mathf.PI * 2f / Size)));
        streak /= 1.35f;
        return broad * 0.62f + cross * 0.28f + streak * 0.10f;
    }

    static float SoftCell(int x, int y, int cells, int seed, float reach, float power)
    {
        float dist = Cellular(x, y, cells, seed);
        float blob = Mathf.Clamp01(1f - dist / reach);
        return Mathf.Pow(blob, power);
    }

    static float Cellular(int x, int y, int cells, int seed)
    {
        float fx = (x + 0.5f) / Size * cells;
        float fy = (y + 0.5f) / Size * cells;
        int cx = Mathf.FloorToInt(fx);
        int cy = Mathf.FloorToInt(fy);
        float best = 8f;
        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                int nx = Mod(cx + ox, cells);
                int ny = Mod(cy + oy, cells);
                float px = cx + ox + Hash01(nx, ny, seed);
                float py = cy + oy + Hash01(nx, ny, seed + 59);
                float dx = fx - px;
                float dy = fy - py;
                float d = dx * dx + dy * dy;
                if (d < best)
                {
                    best = d;
                }
            }
        }

        return Mathf.Sqrt(best);
    }

    static float Fbm(int x, int y, int periodX, int periodY, int octaves, float persistence, int seed)
    {
        float sum = 0f;
        float amp = 1f;
        float norm = 0f;
        int px = periodX;
        int py = periodY;
        for (int octave = 0; octave < octaves; octave++)
        {
            if (px > Size || py > Size || px < 1 || py < 1)
            {
                break;
            }

            float u = (x + 0.5f) / Size * px;
            float v = (y + 0.5f) / Size * py;
            sum += amp * ValueNoise(u, v, px, py, seed + octave * 101);
            norm += amp;
            amp *= persistence;
            px *= 2;
            py *= 2;
        }

        return norm > 0f ? sum / norm : 0f;
    }

    static float ValueNoise(float u, float v, int periodX, int periodY, int seed)
    {
        int x0 = Mathf.FloorToInt(u);
        int y0 = Mathf.FloorToInt(v);
        float tx = Fade(u - x0);
        float ty = Fade(v - y0);
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        float v00 = Hash01(Mod(x0, periodX), Mod(y0, periodY), seed);
        float v10 = Hash01(Mod(x1, periodX), Mod(y0, periodY), seed);
        float v01 = Hash01(Mod(x0, periodX), Mod(y1, periodY), seed);
        float v11 = Hash01(Mod(x1, periodX), Mod(y1, periodY), seed);
        return Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), ty);
    }

    static float Fade(float t)
    {
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint n = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            n = (n ^ (n >> 13)) * 1274126177u;
            n ^= n >> 16;
            return (n & 16777215u) / 16777215f;
        }
    }

    static int Mod(int value, int period)
    {
        int m = value % period;
        return m < 0 ? m + period : m;
    }

    static byte Encode(float component)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt((component * 0.5f + 0.5f) * 255f), 0, 255);
    }

    static void SavePng(string path, Color32[] pixels, bool normalMap)
    {
        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);
        File.WriteAllBytes(path, png);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("Water texture imported without a TextureImporter: " + path);
            return;
        }

        bool dirty = false;
        TextureImporterType type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type)
        {
            importer.textureType = type;
            dirty = true;
        }

        if (importer.convertToNormalmap)
        {
            importer.convertToNormalmap = false;
            dirty = true;
        }

        if (importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            dirty = true;
        }

        if (!importer.mipmapEnabled)
        {
            importer.mipmapEnabled = true;
            dirty = true;
        }

        if (importer.wrapMode != TextureWrapMode.Repeat)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            dirty = true;
        }

        if (importer.filterMode != FilterMode.Bilinear)
        {
            importer.filterMode = FilterMode.Bilinear;
            dirty = true;
        }

        if (importer.maxTextureSize != 1024)
        {
            importer.maxTextureSize = 1024;
            dirty = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            dirty = true;
        }

        if (importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = false;
            dirty = true;
        }

        if (dirty)
        {
            importer.SaveAndReimport();
        }
    }
}
}
