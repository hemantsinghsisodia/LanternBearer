using System.IO;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
// Paints tileable, flat ground textures from an island palette: three tones blended by soft low-frequency noise.
public static class GroundTextureBuilder
{
    public const int Size = 512;
    const float NormalStrength = 0.15f;
    const string Root = "Assets/Game/Art/Environment/Ground";

    public static string FolderFor(string levelId)
    {
        return Root + "/" + levelId;
    }

    public static (Texture2D albedo, Texture2D normal) Build(string levelId, string layerName, Color baseColour, int seed)
    {
        string folder = FolderFor(levelId);
        EnsureFolder(folder);
        float[] noise = BuildNoise(seed);
        Color[] tones = LookMapping.GroundTones(baseColour);

        Color32[] albedo = new Color32[Size * Size];
        for (int i = 0; i < albedo.Length; i++)
        {
            float t = Mathf.Clamp01(noise[i]);
            Color c = t < 0.5f ? Color.Lerp(tones[0], tones[1], t * 2f) : Color.Lerp(tones[1], tones[2], (t - 0.5f) * 2f);
            albedo[i] = c;
        }

        Color32[] normal = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float l = noise[y * Size + (x + Size - 1) % Size];
                float r = noise[y * Size + (x + 1) % Size];
                float d = noise[((y + Size - 1) % Size) * Size + x];
                float u = noise[((y + 1) % Size) * Size + x];
                Vector3 n = new Vector3((l - r) * NormalStrength * 8f, (d - u) * NormalStrength * 8f, 1f).normalized;
                normal[y * Size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
            }
        }

        Texture2D a = Write(folder + "/" + layerName + "_albedo.png", albedo, false);
        Texture2D n2 = Write(folder + "/" + layerName + "_normal.png", normal, true);
        return (a, n2);
    }

    static Texture2D Write(string path, Color32[] pixels, bool isNormal)
    {
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, isNormal);
        tex.SetPixels32(pixels);
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        string full = Path.GetFullPath(path);
        if (!File.Exists(full) || !BytesEqual(File.ReadAllBytes(full), png))
        {
            File.WriteAllBytes(full, png);
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        bool changed = false;
        TextureImporterType type = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type)
        {
            importer.textureType = type;
            changed = true;
        }

        if (!isNormal && !importer.sRGBTexture)
        {
            importer.sRGBTexture = true;
            changed = true;
        }

        if (importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static bool BytesEqual(byte[] a, byte[] b)
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

    // Two octaves of tileable value noise: 4x4 cells (about a quarter of the texture) and 8x8 at lower weight.
    static float[] BuildNoise(int seed)
    {
        float[] a = Lattice(4, seed);
        float[] b = Lattice(8, seed * 7 + 13);
        float[] result = new float[Size * Size];
        float min = float.MaxValue;
        float max = float.MinValue;
        for (int i = 0; i < result.Length; i++)
        {
            float v = a[i] * 0.7f + b[i] * 0.3f;
            result[i] = v;
            min = Mathf.Min(min, v);
            max = Mathf.Max(max, v);
        }

        float range = Mathf.Max(0.0001f, max - min);
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (result[i] - min) / range;
        }

        return result;
    }

    static float[] Lattice(int cells, int seed)
    {
        float[] grid = new float[cells * cells];
        System.Random rng = new System.Random(seed);
        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] = (float)rng.NextDouble();
        }

        float[] result = new float[Size * Size];
        float cellSize = Size / (float)cells;
        for (int y = 0; y < Size; y++)
        {
            float fy = y / cellSize;
            int y0 = Mathf.FloorToInt(fy);
            float ty = Smooth(fy - y0);
            int y1 = (y0 + 1) % cells;
            y0 %= cells;
            for (int x = 0; x < Size; x++)
            {
                float fx = x / cellSize;
                int x0 = Mathf.FloorToInt(fx);
                float tx = Smooth(fx - x0);
                int x1 = (x0 + 1) % cells;
                x0 %= cells;
                float top = Mathf.Lerp(grid[y0 * cells + x0], grid[y0 * cells + x1], tx);
                float bottom = Mathf.Lerp(grid[y1 * cells + x0], grid[y1 * cells + x1], tx);
                result[y * Size + x] = Mathf.Lerp(top, bottom, ty);
            }
        }

        return result;
    }

    static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
}
