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
            float degrees = dh * 360f;
            Assert.IsFalse(degrees >= 20f && degrees <= 50f, "island" + i + " dirt hue " + degrees + " is amber");
            Assert.Greater(dv, gv, "island" + i + " dirt value must exceed grass value");
        }
    }
}
}
