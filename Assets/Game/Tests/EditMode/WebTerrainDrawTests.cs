using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class WebTerrainDrawTests
{
    [Test]
    public void ApplyTurnsInstancedDrawingOff()
    {
        TerrainData data = new TerrainData();
        GameObject go = Terrain.CreateTerrainGameObject(data);
        try
        {
            Terrain terrain = go.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            Assert.AreEqual(1, WebTerrainDraw.Apply(new[] { terrain }));
            Assert.IsFalse(terrain.drawInstanced);
            Assert.AreEqual(0, WebTerrainDraw.Apply(new[] { terrain }), "second call changes nothing");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void DisabledLeavesTerrainsAlone()
    {
        TerrainData data = new TerrainData();
        GameObject go = Terrain.CreateTerrainGameObject(data);
        try
        {
            Terrain terrain = go.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            WebTerrainDraw.Disabled = true;
            Assert.AreEqual(0, WebTerrainDraw.Apply(new[] { terrain }));
            Assert.IsTrue(terrain.drawInstanced);
        }
        finally
        {
            WebTerrainDraw.Disabled = false;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(data);
        }
    }
}
}
