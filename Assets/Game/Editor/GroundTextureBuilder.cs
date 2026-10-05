using System.IO;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
// Paints tileable ground textures from an island palette. Each layer is three close tones blended by soft low-frequency noise,
// then painted over with surface detail at several scales (pebbles and trodden streaks on dirt, grain and ripples on sand,
// cracks and facets on rock, clumps and litter on grass and moss). A height map carries the relief and the normal map is derived from it.
// Everything wraps at the texture edges and is seeded, so a rebuild is byte-identical.
public static class GroundTextureBuilder
{
    const string Root = "Assets/Game/Art/Environment/Ground";

    public static string FolderFor(string levelId)
    {
        return Root + "/" + levelId;
    }

    // World size in metres that one 512 px tile covers. Smaller than the old shared layers so pebbles, cracks and clumps show at gameplay distance.
    public static Vector2 TileFor(string layerName)
    {
        switch (layerName)
        {
            case "sand":
                return new Vector2(5f, 5f);
            case "dirt":
                return new Vector2(3.5f, 3.5f);
            case "rock":
                return new Vector2(4f, 4f);
            default:
                return new Vector2(4f, 4f);
        }
    }

    public static (Texture2D albedo, Texture2D normal) Build(string levelId, string layerName, Color baseColour, int seed)
    {
        string folder = FolderFor(levelId);
        EnsureFolder(folder);
        GroundTexturePainter.Generate(layerName, baseColour, seed, out Color32[] albedo, out Color32[] normal);
        Texture2D a = Write(folder + "/" + layerName + "_albedo.png", albedo, false);
        Texture2D n2 = Write(folder + "/" + layerName + "_normal.png", normal, true);
        return (a, n2);
    }

    static Texture2D Write(string path, Color32[] pixels, bool isNormal)
    {
        Texture2D tex = new Texture2D(GroundTexturePainter.Size, GroundTexturePainter.Size, TextureFormat.RGBA32, false, isNormal);
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
