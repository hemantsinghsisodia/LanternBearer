using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class GroundTextureTests
{
    [Test]
    public void DirtPathReadsInMoonlight()
    {
        for (int i = 1; i <= 4; i++)
        {
            LookProfile p = AssetDatabase.LoadAssetAtPath<LookProfile>("Assets/Game/Art/Look/LookProfile_island" + i + ".asset");
            Assert.IsNotNull(p, "missing LookProfile_island" + i);
            Color.RGBToHSV(GroundPalette.LayerBase(p, "dirt"), out float dh, out float ds, out float dv);
            Color.RGBToHSV(GroundPalette.LayerBase(p, "grass"), out float gh, out float gs, out float gv);
            Assert.Greater(dv, gv, "island" + i + " dirt value must exceed grass value");
        }
    }

    static float Lum(Color32 c)
    {
        return (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f;
    }

    static float MeanLum(Color32[] px)
    {
        float sum = 0f;
        for (int i = 0; i < px.Length; i++)
        {
            sum += Lum(px[i]);
        }

        return sum / px.Length;
    }

    [Test]
    public void GeneratedTexturesTileSeamlessly()
    {
        LookProfile p = AssetDatabase.LoadAssetAtPath<LookProfile>("Assets/Game/Art/Look/LookProfile_island1.asset");
        int n = GroundTexturePainter.Size;
        foreach (string layer in GroundPalette.Layers)
        {
            GroundTexturePainter.Generate(layer, GroundPalette.LayerBase(p, layer), 7, out Color32[] albedo, out Color32[] normal);
            float seam = 0f;
            float inner = 0f;
            for (int y = 0; y < n; y++)
            {
                seam += Mathf.Abs(Lum(albedo[y * n]) - Lum(albedo[y * n + n - 1]));
                inner += Mathf.Abs(Lum(albedo[y * n + 255]) - Lum(albedo[y * n + 256]));
            }

            for (int x = 0; x < n; x++)
            {
                seam += Mathf.Abs(Lum(albedo[x]) - Lum(albedo[(n - 1) * n + x]));
                inner += Mathf.Abs(Lum(albedo[255 * n + x]) - Lum(albedo[256 * n + x]));
            }

            Assert.Less(seam, inner * 2f + 0.5f, layer + " albedo has a visible seam");
            Assert.AreEqual(n * n, normal.Length);
        }
    }

    [Test]
    public void GeneratedTexturesAreDeterministic()
    {
        LookProfile p = AssetDatabase.LoadAssetAtPath<LookProfile>("Assets/Game/Art/Look/LookProfile_island1.asset");
        GroundTexturePainter.Generate("dirt", GroundPalette.LayerBase(p, "dirt"), 11, out Color32[] a1, out Color32[] n1);
        GroundTexturePainter.Generate("dirt", GroundPalette.LayerBase(p, "dirt"), 11, out Color32[] a2, out Color32[] n2);
        CollectionAssert.AreEqual(a1, a2);
        CollectionAssert.AreEqual(n1, n2);
    }

    [Test]
    public void GeneratedDirtStaysLighterThanGrassOnAverage()
    {
        for (int i = 1; i <= 4; i++)
        {
            LookProfile p = AssetDatabase.LoadAssetAtPath<LookProfile>("Assets/Game/Art/Look/LookProfile_island" + i + ".asset");
            GroundTexturePainter.Generate("dirt", GroundPalette.LayerBase(p, "dirt"), 3, out Color32[] dirt, out _);
            GroundTexturePainter.Generate("grass", GroundPalette.LayerBase(p, "grass"), 4, out Color32[] grass, out _);
            Assert.Greater(MeanLum(dirt), MeanLum(grass), "island" + i + " painted dirt must stay lighter than the grass ground");
        }
    }

    [Test]
    public void GeneratedNormalMapsHaveRelief()
    {
        LookProfile p = AssetDatabase.LoadAssetAtPath<LookProfile>("Assets/Game/Art/Look/LookProfile_island1.asset");
        foreach (string layer in GroundPalette.Layers)
        {
            GroundTexturePainter.Generate(layer, GroundPalette.LayerBase(p, layer), 5, out _, out Color32[] normal);
            float sum = 0f;
            for (int i = 0; i < normal.Length; i++)
            {
                sum += Mathf.Abs(normal[i].r - 127.5f) + Mathf.Abs(normal[i].g - 127.5f);
            }

            Assert.Greater(sum / normal.Length, 2f, layer + " normal map is nearly flat");
        }
    }
}
}
