using UnityEngine;

namespace LanternKeeper
{
public static class TerrainQuery
{
    public static bool TrySample(Vector3 world, out float height, out Vector3 normal)
    {
        height = world.y;
        normal = Vector3.up;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
        {
            return false;
        }

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        if (size.x <= 0.01f || size.z <= 0.01f)
        {
            return false;
        }

        float u = (world.x - origin.x) / size.x;
        float v = (world.z - origin.z) / size.z;
        if (u < 0f || v < 0f || u > 1f || v > 1f)
        {
            return false;
        }

        height = terrain.SampleHeight(world) + origin.y;
        normal = terrain.terrainData.GetInterpolatedNormal(u, v);
        return true;
    }

    public static float Height(Vector3 world, float fallback)
    {
        float height;
        Vector3 normal;
        if (!TrySample(world, out height, out normal))
        {
            return fallback;
        }

        return height;
    }

    public static float IslandRadius()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
        {
            return 28f;
        }

        return terrain.terrainData.size.x * 0.5f;
    }
}
}
