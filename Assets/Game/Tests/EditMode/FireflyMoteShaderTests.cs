using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
// Renders one lit firefly mote and measures its on-screen footprint. The mote's size comes from its object-to-world scale,
// so the footprint must not depend on how far the mote is from the world origin (it once did: the shader read the wrong matrix
// entries, a mote far from the origin became metres wide and washed the screen out on Islands 2 and 3).
public class FireflyMoteShaderTests
{
    const string MaterialPath = "Assets/Game/Materials/Generated/FireflyMote.mat";
    const int Size = 256;

    GameObject quad;
    GameObject camObject;
    RenderTexture target;

    [TearDown]
    public void TearDown()
    {
        if (quad != null)
        {
            Object.DestroyImmediate(quad);
        }

        if (camObject != null)
        {
            Object.DestroyImmediate(camObject);
        }

        if (target != null)
        {
            target.Release();
            Object.DestroyImmediate(target);
        }
    }

    // Number of pixels clearly lit by a mote at the given world position, seen from 15 m, scaled to the given size.
    int Footprint(Vector3 position, float scale)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Assert.IsNotNull(material, "FireflyMote material");
        quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.position = position;
        quad.transform.localScale = Vector3.one * scale;
        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetFloat("_Phase", 0.3f);
        block.SetFloat("_Period", 1e6f);
        block.SetFloat("_Fade", 1f);
        block.SetVector("_DriftFreq", Vector4.zero);
        block.SetVector("_DriftPhase", Vector4.zero);
        renderer.SetPropertyBlock(block);

        camObject = new GameObject("MoteTestCamera");
        Camera camera = camObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.fieldOfView = 58f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 200f;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camObject.transform.position = position + new Vector3(0f, 0f, -15f);
        camObject.transform.rotation = Quaternion.identity;

        target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D readback = new Texture2D(Size, Size, TextureFormat.RGB24, false);
        readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        readback.Apply();
        RenderTexture.active = previous;
        int lit = 0;
        foreach (Color32 pixel in readback.GetPixels32())
        {
            if (pixel.g > 24)
            {
                lit++;
            }
        }

        Object.DestroyImmediate(readback);
        return lit;
    }

    [Test]
    public void MoteFootprintDoesNotGrowWithDistanceFromTheWorldOrigin()
    {
        int nearOrigin = Footprint(new Vector3(0.5f, 1f, 0.5f), 0.15f);
        TearDown();
        int farFromOrigin = Footprint(new Vector3(18f, 6f, 4f), 0.15f);
        Assert.Greater(nearOrigin, 0, "the mote draws at all");
        Assert.Less(farFromOrigin, Size * Size / 20, "a mote 19 m from the origin stays a small point, not a screen-wide glow");
        Assert.AreEqual(nearOrigin, farFromOrigin, nearOrigin * 0.35f + 4f, "same footprint wherever the mote is");
    }

    [Test]
    public void MoteFootprintFollowsItsScale()
    {
        // 10 px minimum on screen: a 0.15 m mote at 15 m is close to that floor, 1.5 m is far above it.
        int small = Footprint(new Vector3(18f, 6f, 4f), 0.15f);
        TearDown();
        int large = Footprint(new Vector3(18f, 6f, 4f), 1.5f);
        Assert.Greater(large, small * 3, "a bigger mote covers more pixels");
    }
}
}
