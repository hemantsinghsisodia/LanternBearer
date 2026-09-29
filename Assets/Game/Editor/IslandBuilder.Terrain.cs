using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
public static partial class IslandBuilder
{
    static void BuildSculptedTerrain(LevelConfig config, ArtKit art, string terrainPath, Stage stage)
    {
        const int resolution = 513;
        const int alphaResolution = 512;
        stage.worldSize = config.islandRadius * 2f;
        List<Vector2> beaconXZ = PlanBeaconXZ(config, out List<Vector2> isletXZ);
        for (int i = 0; i < isletXZ.Count; i++)
        {
            stage.islets.Add(new Vector3(isletXZ[i].x, 0f, isletXZ[i].y));
        }

        float[,] heights = SculptHeights(config, resolution, stage.worldSize, beaconXZ, stage.trails);
        for (int i = 0; i < beaconXZ.Count; i++)
        {
            stage.beaconSpots.Add(new Vector3(beaconXZ[i].x, 0f, beaconXZ[i].y));
        }

        TerrainData data = new TerrainData();
        data.heightmapResolution = resolution;
        data.size = new Vector3(stage.worldSize, config.hillHeight, stage.worldSize);
        UnityEditor.AssetDatabase.CreateAsset(data, terrainPath);
        data.size = new Vector3(stage.worldSize, config.hillHeight, stage.worldSize);
        data.SetHeights(0, 0, heights);
        data.alphamapResolution = alphaResolution;
        data.baseMapResolution = 512;
        TerrainLayer[] layers = new[] { art.sandLayer, art.grassLayer, art.dirtLayer, art.rockLayer, art.mossLayer };
        if (config.levelId == "island2")
        {
            layers = WarmKarooLayers(art);
        }

        data.terrainLayers = layers;
        float[,,] alphamaps = PaintTerrain(heights, resolution, alphaResolution, config, stage.worldSize, stage.trails);
        data.SetAlphamaps(0, 0, alphamaps);
        ApplyDetails(data, alphamaps, heights, resolution, config, stage.worldSize, art, stage.trails);
        UnityEditor.EditorUtility.SetDirty(data);

        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "Island";
        terrainObject.transform.position = new Vector3(-stage.worldSize * 0.5f, 0f, -stage.worldSize * 0.5f);
        stage.terrain = terrainObject.GetComponent<Terrain>();
        if (art.terrain != null)
        {
            stage.terrain.materialTemplate = art.terrain;
        }

        stage.terrain.drawInstanced = true;
        stage.terrain.basemapDistance = 280f;
        stage.terrain.heightmapPixelError = 4f;
        stage.terrain.detailObjectDistance = 48f;
        stage.terrain.detailObjectDensity = 0.85f;
        stage.waterY = WaterFraction * config.hillHeight;
        stage.spawn = new Vector3(0f, GroundY(stage.terrain, 0f, 0f) + 0.05f, 0f);
    }

    static List<Vector2> PlanBeaconXZ(LevelConfig config, out List<Vector2> islets)
    {
        islets = new List<Vector2>();
        int paths = Mathf.Max(0, config.hiddenPathCount);
        float start = (config.seed % 360) * Mathf.Deg2Rad;
        for (int i = 0; i < paths; i++)
        {
            float angle = start + i * (Mathf.PI * 2f / paths);
            float reach = 0.95f * config.islandRadius;
            islets.Add(new Vector2(Mathf.Cos(angle) * reach, Mathf.Sin(angle) * reach));
        }

        List<Vector2> spots = new List<Vector2>();
        int mainCount = Mathf.Max(0, config.beaconCount - islets.Count);
        float beaconStart = (config.seed % 360) * Mathf.Deg2Rad + 0.7f;
        for (int i = 0; i < mainCount; i++)
        {
            float angle = beaconStart + i * Mathf.PI * 2f / Mathf.Max(1, mainCount);
            float dist = config.islandRadius * (0.34f + 0.12f * (i % 3));
            spots.Add(new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist));
        }

        for (int i = 0; i < islets.Count && spots.Count < config.beaconCount; i++)
        {
            spots.Add(islets[i]);
        }

        return spots;
    }

    static float[,] SculptHeights(LevelConfig config, int resolution, float worldSize, List<Vector2> beacons, List<List<Vector2>> trails)
    {
        float[,] heights = new float[resolution, resolution];
        float ox = (config.seed % 500) * 0.13f;
        float oz = (config.seed % 320) * 0.17f;
        float cliffAngle = (config.seed % 360) * Mathf.Deg2Rad;
        float coveAngle = cliffAngle + 2.4f;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Vector2 world = PixelToWorld(x, z, resolution, worldSize);
                float dx = world.x / Mathf.Max(1f, config.islandRadius);
                float dz = world.y / Mathf.Max(1f, config.islandRadius);
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                float n1 = Mathf.PerlinNoise(dx * 1.7f + ox, dz * 1.7f + oz);
                float n2 = Mathf.PerlinNoise(dx * 3.4f + ox * 1.6f, dz * 3.4f + 5f);
                float inland = 0.58f + n1 * 0.22f + n2 * 0.08f;
                float shore = Mathf.SmoothStep(0.4f, 0.96f, dist);
                float h = Mathf.Lerp(inland, WaterFraction * 0.22f, shore);
                if (dist < 0.08f)
                {
                    h = Mathf.Lerp(h, 0.5f, 1f - dist / 0.08f);
                }

                float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(dx * 2.8f + ox, dz * 2.8f + oz) * 2f - 1f);
                float ridge2 = 1f - Mathf.Abs(Mathf.PerlinNoise(dx * 6.2f + 3f, dz * 6.2f + 1.4f) * 2f - 1f);
                float inlandMask = Mathf.SmoothStep(0.78f, 0.22f, dist);
                h += (ridge * 0.16f + ridge2 * 0.045f) * inlandMask;

                float ang = Mathf.Atan2(dz, dx);
                float cliff = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, cliffAngle * Mathf.Rad2Deg));
                if (cliff < 62f && dist > 0.48f)
                {
                    float face = Mathf.InverseLerp(0.5f, 0.72f, dist);
                    if (dist < 0.74f)
                    {
                        h = Mathf.Lerp(h, 0.74f, Mathf.SmoothStep(0f, 1f, face) * (1f - cliff / 62f));
                    }
                    else
                    {
                        h = Mathf.Lerp(h, WaterFraction * 0.18f, Mathf.SmoothStep(0.74f, 0.9f, dist));
                    }
                }

                float cove = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, coveAngle * Mathf.Rad2Deg));
                if (cove < 24f && dist > 0.58f && dist < 0.98f)
                {
                    float bite = (1f - cove / 24f) * Mathf.SmoothStep(0.58f, 0.7f, dist);
                    h = Mathf.Lerp(h, WaterFraction * 0.28f, bite);
                }

                if (h < WaterFraction + 0.16f && h > WaterFraction * 0.35f && dist > 0.55f)
                {
                    float dune = Mathf.PerlinNoise(world.x * 0.23f + ox, world.y * 0.23f + oz) - 0.5f;
                    h += dune * 0.045f;
                }

                heights[z, x] = Mathf.Clamp01(h);
            }
        }

        int paths = config.hiddenPathCount;
        float channelStart = (config.seed % 360) * Mathf.Deg2Rad;
        for (int i = 0; i < paths; i++)
        {
            float angle = channelStart + i * (Mathf.PI * 2f / paths);
            CarveChannel(heights, resolution, angle);
            AddIslet(heights, resolution, angle, config.islandRadius);
        }

        StampPlateaus(heights, resolution, worldSize, beacons);
        FlattenTrails(heights, resolution, worldSize, beacons, config, trails);
        return heights;
    }

    static void StampPlateaus(float[,] heights, int resolution, float worldSize, List<Vector2> beacons)
    {
        if (beacons.Count == 0)
        {
            return;
        }

        int count = Mathf.Min(2, beacons.Count);
        for (int i = beacons.Count - count; i < beacons.Count; i++)
        {
            Vector2 center = beacons[i];
            float radius = 4.2f;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Vector2 world = PixelToWorld(x, z, resolution, worldSize);
                    float dist = Vector2.Distance(world, center);
                    if (dist > radius)
                    {
                        continue;
                    }

                    float u = Mathf.SmoothStep(radius, radius * 0.45f, dist);
                    heights[z, x] = Mathf.Lerp(heights[z, x], 0.64f, u);
                }
            }
        }
    }

    static void FlattenTrails(float[,] heights, int resolution, float worldSize, List<Vector2> beacons, LevelConfig config, List<List<Vector2>> trails)
    {
        for (int i = 0; i < beacons.Count; i++)
        {
            List<Vector2> path = JitteredTrail(Vector2.zero, beacons[i], config.seed, i);
            float[] samples = new float[path.Count];
            for (int p = 0; p < path.Count; p++)
            {
                samples[p] = Mathf.Max(SampleHeight(heights, resolution, worldSize, path[p]), WaterFraction + 0.11f);
            }

            for (int pass = 0; pass < 4; pass++)
            {
                for (int p = 1; p < samples.Length; p++)
                {
                    float delta = samples[p] - samples[p - 1];
                    if (Mathf.Abs(delta) > 0.045f)
                    {
                        samples[p] = samples[p - 1] + Mathf.Sign(delta) * 0.045f;
                    }
                }
            }

            trails.Add(path);
            float width = 2.15f;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Vector2 world = PixelToWorld(x, z, resolution, worldSize);
                    float nearest = 999f;
                    float along = 0f;
                    float total = 0f;
                    for (int s = 1; s < path.Count; s++)
                    {
                        float length = Vector2.Distance(path[s - 1], path[s]);
                        float t;
                        float dist = DistanceToSegment(world, path[s - 1], path[s], out t);
                        if (dist < nearest)
                        {
                            nearest = dist;
                            along = total + length * t;
                        }

                        total += length;
                    }

                    if (nearest > width)
                    {
                        continue;
                    }

                    float u = total < 0.01f ? 0f : Mathf.Clamp01(along / total);
                    float current = heights[z, x];
                    float target = SamplePolyline(samples, u);
                    if (target < current - 0.035f)
                    {
                        target = current - 0.035f;
                    }

                    float influence = 1f - Mathf.SmoothStep(width * 0.2f, width, nearest);
                    heights[z, x] = Mathf.Lerp(current, target, influence);
                }
            }
        }
    }

    static List<Vector2> JitteredTrail(Vector2 from, Vector2 to, int seed, int index)
    {
        List<Vector2> points = new List<Vector2>();
        points.Add(from);
        int steps = 5;
        System.Random random = new System.Random(seed + 90 + index * 37);
        Vector2 dir = to - from;
        if (dir.sqrMagnitude < 0.01f)
        {
            dir = Vector2.right;
        }

        dir.Normalize();
        Vector2 side = new Vector2(-dir.y, dir.x);
        for (int i = 1; i < steps; i++)
        {
            float t = i / (float)steps;
            float jitter = ((float)random.NextDouble() - 0.5f) * 5.2f * Mathf.Sin(t * Mathf.PI);
            points.Add(Vector2.Lerp(from, to, t) + side * jitter);
        }

        points.Add(to);
        return points;
    }

    static float SamplePolyline(float[] samples, float u)
    {
        if (samples.Length == 0)
        {
            return WaterFraction + 0.12f;
        }

        float scaled = u * (samples.Length - 1);
        int index = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, samples.Length - 2);
        float t = scaled - index;
        return Mathf.Lerp(samples[index], samples[index + 1], t);
    }

    static float SampleHeight(float[,] heights, int resolution, float worldSize, Vector2 world)
    {
        float nx = Mathf.Clamp01(world.x / worldSize + 0.5f);
        float nz = Mathf.Clamp01(world.y / worldSize + 0.5f);
        int x = Mathf.Clamp(Mathf.RoundToInt(nx * (resolution - 1)), 0, resolution - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(nz * (resolution - 1)), 0, resolution - 1);
        return heights[z, x];
    }

    static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b, out float t)
    {
        Vector2 ab = b - a;
        float denom = ab.sqrMagnitude;
        t = denom < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, ab) / denom);
        return Vector2.Distance(point, a + ab * t);
    }

    static Vector2 PixelToWorld(int x, int z, int resolution, float worldSize)
    {
        float nx = x / (float)(resolution - 1);
        float nz = z / (float)(resolution - 1);
        return new Vector2((nx - 0.5f) * worldSize, (nz - 0.5f) * worldSize);
    }

    static float[,,] PaintTerrain(float[,] heights, int heightRes, int alphaRes, LevelConfig config, float worldSize, List<List<Vector2>> trails)
    {
        float[,,] maps = new float[alphaRes, alphaRes, 5];
        float run = worldSize / (heightRes - 1);
        float ox = config.seed * 0.01f;
        for (int z = 0; z < alphaRes; z++)
        {
            for (int x = 0; x < alphaRes; x++)
            {
                int hx = Mathf.Clamp(Mathf.RoundToInt(x / (float)(alphaRes - 1) * (heightRes - 1)), 0, heightRes - 1);
                int hz = Mathf.Clamp(Mathf.RoundToInt(z / (float)(alphaRes - 1) * (heightRes - 1)), 0, heightRes - 1);
                int hx2 = Mathf.Clamp(hx + 1, 0, heightRes - 1);
                int hz2 = Mathf.Clamp(hz + 1, 0, heightRes - 1);
                float h = heights[hz, hx];
                float slope = Mathf.Max(Mathf.Abs(heights[hz, hx2] - h), Mathf.Abs(heights[hz2, hx] - h)) * config.hillHeight / run;
                float neighbor = (heights[hz, hx] + heights[hz, hx2] + heights[hz2, hx] + heights[Mathf.Max(0, hz - 1), hx]) * 0.25f;
                float hollow = Mathf.Clamp01((neighbor - h) * 10f);
                Vector2 world = PixelToWorld(x, z, alphaRes, worldSize);
                float trail = TrailMask(world, trails, 1.7f);
                float n = Mathf.PerlinNoise(world.x * 0.11f + ox, world.y * 0.11f);
                float n2 = Mathf.PerlinNoise(world.x * 0.05f + 8f, world.y * 0.07f);
                float sand = Mathf.SmoothStep(WaterFraction + 0.14f, WaterFraction + 0.015f, h);
                float rock = Mathf.Max(Mathf.SmoothStep(0.42f, 1.05f, slope), Mathf.SmoothStep(0.7f, 0.86f, h) * 0.85f);
                float dirt = trail;
                float moss = Mathf.Clamp01(hollow * 0.85f + rock * 0.28f * (1f - sand));
                float grass = 1f;
                sand *= Mathf.Lerp(0.78f, 1.18f, n);
                grass *= Mathf.Lerp(0.82f, 1.16f, n2);
                rock *= Mathf.Lerp(0.85f, 1.12f, 1f - n);
                moss *= Mathf.Lerp(0.7f, 1.2f, n2);
                dirt *= Mathf.Lerp(0.9f, 1.05f, n);
                grass *= 1f - sand * 0.85f;
                grass *= 1f - rock * 0.75f;
                grass *= 1f - dirt * 0.8f;
                moss *= 1f - dirt * 0.55f;
                moss *= 1f - sand * 0.7f;
                if (config.levelId == "island2")
                {
                    sand *= 1.45f;
                    dirt *= 1.35f;
                    grass *= 0.62f;
                    moss *= 0.28f;
                    rock *= 0.92f;
                }

                float sum = sand + grass + dirt + rock + moss;
                if (sum < 0.001f)
                {
                    grass = 1f;
                    sum = 1f;
                }

                maps[z, x, 0] = sand / sum;
                maps[z, x, 1] = grass / sum;
                maps[z, x, 2] = dirt / sum;
                maps[z, x, 3] = rock / sum;
                maps[z, x, 4] = moss / sum;
            }
        }

        return maps;
    }

    static float TrailMask(Vector2 world, List<List<Vector2>> trails, float width)
    {
        float nearest = width;
        for (int i = 0; i < trails.Count; i++)
        {
            List<Vector2> path = trails[i];
            for (int s = 1; s < path.Count; s++)
            {
                float unused;
                float dist = DistanceToSegment(world, path[s - 1], path[s], out unused);
                if (dist < nearest)
                {
                    nearest = dist;
                }
            }
        }

        return 1f - Mathf.SmoothStep(width * 0.35f, width, nearest);
    }

    static void ApplyDetails(TerrainData data, float[,,] alphamaps, float[,] heights, int heightRes, LevelConfig config, float worldSize, ArtKit art, List<List<Vector2>> trails)
    {
        const int detailRes = 256;
        data.SetDetailResolution(detailRes, 16);
        List<DetailPrototype> prototypes = new List<DetailPrototype>();
        List<int[,]> layers = new List<int[,]>();
        if (config.biome != null && config.biome.entries != null)
        {
            AddBiomeDetails(data, prototypes, layers, alphamaps, heights, heightRes, config, worldSize, trails, detailRes);
        }
        else if (art.detailGrass != null)
        {
            int[,] grass = new int[detailRes, detailRes];
            int density = config.grassDetailDensity <= 0 ? 8 : Mathf.Clamp(config.grassDetailDensity, 1, 16);
            prototypes.Add(Billboard(art.detailGrass, new Color(0.32f, 0.52f, 0.2f), new Color(0.24f, 0.38f, 0.14f), 0.25f, 0.7f, 0.28f, 0.62f));
            for (int z = 0; z < detailRes; z++)
            {
                for (int x = 0; x < detailRes; x++)
                {
                    int ax = Mathf.Clamp(Mathf.RoundToInt(x / (float)(detailRes - 1) * (alphamaps.GetLength(1) - 1)), 0, alphamaps.GetLength(1) - 1);
                    int az = Mathf.Clamp(Mathf.RoundToInt(z / (float)(detailRes - 1) * (alphamaps.GetLength(0) - 1)), 0, alphamaps.GetLength(0) - 1);
                    float weight = alphamaps[az, ax, 1];
                    if (weight < 0.42f)
                    {
                        continue;
                    }

                    grass[z, x] = Mathf.Clamp(Mathf.RoundToInt(density * weight), 0, 16);
                }
            }

            layers.Add(grass);
        }

        if (art.detailReed != null)
        {
            prototypes.Add(Billboard(art.detailReed, new Color(0.45f, 0.5f, 0.22f), new Color(0.28f, 0.32f, 0.14f), 0.55f, 1.25f, 0.18f, 0.36f));
            int[,] reeds = new int[detailRes, detailRes];
            for (int z = 0; z < detailRes; z++)
            {
                for (int x = 0; x < detailRes; x++)
                {
                    int hx = Mathf.Clamp(Mathf.RoundToInt(x / (float)(detailRes - 1) * (heightRes - 1)), 0, heightRes - 1);
                    int hz = Mathf.Clamp(Mathf.RoundToInt(z / (float)(detailRes - 1) * (heightRes - 1)), 0, heightRes - 1);
                    float h = heights[hz, hx];
                    if (h > WaterFraction + 0.07f || h < WaterFraction * 0.45f)
                    {
                        continue;
                    }

                    reeds[z, x] = 5;
                }
            }

            layers.Add(reeds);
        }

        if (prototypes.Count == 0)
        {
            return;
        }

        data.detailPrototypes = prototypes.ToArray();
        for (int i = 0; i < layers.Count && i < prototypes.Count; i++)
        {
            data.SetDetailLayer(0, 0, i, layers[i]);
        }
    }

    static void AddBiomeDetails(TerrainData data, List<DetailPrototype> prototypes, List<int[,]> layers, float[,,] alphamaps, float[,] heights, int heightRes, LevelConfig config, float worldSize, List<List<Vector2>> trails, int detailRes)
    {
        bool namaqualand = config.levelId == "island2";
        BiomeEntry[] entries = config.biome.entries;
        for (int e = 0; e < entries.Length; e++)
        {
            BiomeEntry entry = entries[e];
            if (entry == null || entry.prefab == null)
            {
                continue;
            }

            if (entry.category != BiomeCategory.GroundCover && entry.category != BiomeCategory.Flower)
            {
                continue;
            }

            string prefabName = entry.prefab.name.ToLowerInvariant();
            bool moss = prefabName.Contains("moss");
            bool flower = entry.category == BiomeCategory.Flower;
            prototypes.Add(MeshDetail(entry.prefab, entry.scaleRange.x, entry.scaleRange.y, namaqualand));
            int[,] map = new int[detailRes, detailRes];
            int density = Mathf.Clamp(entry.count, 1, 8);
            for (int z = 0; z < detailRes; z++)
            {
                for (int x = 0; x < detailRes; x++)
                {
                    int hx = Mathf.Clamp(Mathf.RoundToInt(x / (float)(detailRes - 1) * (heightRes - 1)), 0, heightRes - 1);
                    int hz = Mathf.Clamp(Mathf.RoundToInt(z / (float)(detailRes - 1) * (heightRes - 1)), 0, heightRes - 1);
                    int ax = Mathf.Clamp(Mathf.RoundToInt(x / (float)(detailRes - 1) * (alphamaps.GetLength(1) - 1)), 0, alphamaps.GetLength(1) - 1);
                    int az = Mathf.Clamp(Mathf.RoundToInt(z / (float)(detailRes - 1) * (alphamaps.GetLength(0) - 1)), 0, alphamaps.GetLength(0) - 1);
                    float h = heights[hz, hx];
                    if (h < WaterFraction + 0.05f)
                    {
                        continue;
                    }

                    Vector2 world = PixelToWorld(x, z, detailRes, worldSize);
                    if (world.magnitude < 5f)
                    {
                        continue;
                    }

                    int hx2 = Mathf.Clamp(hx + 1, 0, heightRes - 1);
                    int hz2 = Mathf.Clamp(hz + 1, 0, heightRes - 1);
                    float run = worldSize / (heightRes - 1);
                    float slope = Mathf.Max(Mathf.Abs(heights[hz, hx2] - h), Mathf.Abs(heights[hz2, hx] - h)) * config.hillHeight / run;
                    float neighbor = (heights[hz, hx] + heights[hz, hx2] + heights[hz2, hx] + heights[Mathf.Max(0, hz - 1), hx]) * 0.25f;
                    float hollow = Mathf.Clamp01((neighbor - h) * 10f);
                    float sand = alphamaps[az, ax, 0];
                    float grass = alphamaps[az, ax, 1];
                    float trail = TrailMask(world, trails, 2.3f);
                    float edge = Mathf.Sin(Mathf.Clamp01(trail) * Mathf.PI);
                    int value = 0;
                    if (moss)
                    {
                        if (!namaqualand && slope > 0.28f && sand < 0.35f)
                        {
                            value = Mathf.RoundToInt(density * Mathf.Clamp01(slope));
                        }
                        else if (namaqualand && slope > 0.55f && sand < 0.25f)
                        {
                            value = 1;
                        }
                    }
                    else if (flower && !namaqualand)
                    {
                        float clump = Mathf.PerlinNoise(world.x * 0.23f, world.y * 0.23f);
                        if (edge > 0.35f && grass > 0.22f)
                        {
                            value = Mathf.RoundToInt(density * edge);
                        }
                        else if (grass > 0.5f && sand < 0.35f && slope < 0.45f && clump > 0.62f)
                        {
                            value = Mathf.Clamp(Mathf.RoundToInt(density * (clump - 0.4f) * 0.7f), 1, 4);
                        }
                    }
                    else if (flower && namaqualand)
                    {
                        float carpet = hollow * 1.15f + (1f - Mathf.Clamp01(slope)) * 0.2f;
                        float clump = Mathf.PerlinNoise(world.x * 0.19f + 4f, world.y * 0.19f);
                        if (sand < 0.55f && slope < 0.62f && carpet > 0.22f)
                        {
                            value = Mathf.RoundToInt(density * Mathf.Clamp01(carpet));
                        }
                        else if (sand < 0.62f && slope < 0.45f && h > WaterFraction + 0.08f && clump > 0.58f)
                        {
                            value = Mathf.Clamp(Mathf.RoundToInt(density * clump * 0.45f), 1, 3);
                        }
                    }
                    else if (!namaqualand)
                    {
                        if (grass > 0.38f && sand < 0.4f)
                        {
                            value = Mathf.RoundToInt(density * grass * (0.4f + edge * 0.8f));
                        }
                    }
                    else if (sand < 0.62f && slope < 0.7f && h > WaterFraction + 0.06f)
                    {
                        float cover = Mathf.Clamp01(hollow * 0.45f + 0.35f);
                        value = Mathf.RoundToInt(density * cover * 0.55f);
                    }

                    map[z, x] = Mathf.Clamp(value, 0, 8);
                }
            }

            layers.Add(map);
        }
    }

    static DetailPrototype MeshDetail(GameObject prefab, float minScale, float maxScale, bool warm)
    {
        if (minScale <= 0.01f)
        {
            minScale = 0.85f;
        }

        if (maxScale < minScale)
        {
            maxScale = minScale;
        }

        DetailPrototype prototype = new DetailPrototype();
        prototype.prototype = prefab;
        prototype.usePrototypeMesh = true;
        prototype.renderMode = DetailRenderMode.VertexLit;
        prototype.useInstancing = true;
        prototype.alignToGround = 0.75f;
        prototype.healthyColor = Color.white;
        prototype.dryColor = warm ? new Color(1f, 0.9f, 0.72f) : Color.white;
        prototype.minHeight = minScale;
        prototype.maxHeight = maxScale;
        prototype.minWidth = minScale;
        prototype.maxWidth = maxScale;
        prototype.noiseSpread = 0.35f;
        return prototype;
    }

    static TerrainLayer[] WarmKarooLayers(ArtKit art)
    {
        string folder = "Assets/Game/Levels/Layers/Karoo";
        if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Game/Levels/Layers"))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets/Game/Levels", "Layers");
            }

            UnityEditor.AssetDatabase.CreateFolder("Assets/Game/Levels/Layers", "Karoo");
        }

        return new[]
        {
            TintLayer(art.sandLayer, folder + "/Sand.terrainlayer", new Color(0.1f, 0.05f, 0.015f), new Color(1.12f, 0.9f, 0.62f)),
            TintLayer(art.grassLayer, folder + "/Grass.terrainlayer", new Color(0.12f, 0.08f, 0.02f), new Color(0.95f, 0.82f, 0.48f)),
            TintLayer(art.dirtLayer, folder + "/Dirt.terrainlayer", new Color(0.14f, 0.07f, 0.02f), new Color(1.08f, 0.78f, 0.46f)),
            art.rockLayer,
            TintLayer(art.mossLayer, folder + "/Moss.terrainlayer", new Color(0.05f, 0.05f, 0.02f), new Color(0.55f, 0.58f, 0.4f))
        };
    }

    static TerrainLayer TintLayer(TerrainLayer source, string path, Color min, Color max)
    {
        TerrainLayer layer = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            UnityEditor.EditorUtility.CopySerialized(source, layer);
            UnityEditor.AssetDatabase.CreateAsset(layer, path);
        }
        else
        {
            layer.diffuseTexture = source.diffuseTexture;
            layer.normalMapTexture = source.normalMapTexture;
            layer.maskMapTexture = source.maskMapTexture;
            layer.tileSize = source.tileSize;
        }

        layer.diffuseRemapMin = min;
        layer.diffuseRemapMax = max;
        UnityEditor.EditorUtility.SetDirty(layer);
        return layer;
    }

    static DetailPrototype Billboard(Texture2D texture, Color healthy, Color dry, float minHeight, float maxHeight, float minWidth, float maxWidth)
    {
        DetailPrototype prototype = new DetailPrototype();
        prototype.prototypeTexture = texture;
        prototype.usePrototypeMesh = false;
        prototype.renderMode = DetailRenderMode.GrassBillboard;
        prototype.healthyColor = healthy;
        prototype.dryColor = dry;
        prototype.minHeight = minHeight;
        prototype.maxHeight = maxHeight;
        prototype.minWidth = minWidth;
        prototype.maxWidth = maxWidth;
        prototype.noiseSpread = 0.45f;
        return prototype;
    }

    static void DressTrails(LevelConfig config, ArtKit art, Stage stage)
    {
        if (stage.trails.Count == 0 || art.pebble == null)
        {
            return;
        }

        Transform parent = Folder("TrailDressing");
        System.Random random = new System.Random(config.seed + 77);
        for (int i = 0; i < stage.trails.Count; i++)
        {
            List<Vector2> path = stage.trails[i];
            float traveled = 0f;
            float next = 5.5f;
            for (int s = 1; s < path.Count; s++)
            {
                Vector2 a = path[s - 1];
                Vector2 b = path[s];
                float length = Vector2.Distance(a, b);
                while (traveled + length >= next)
                {
                    float t = length < 0.001f ? 0f : (next - traveled) / length;
                    Vector2 point = Vector2.Lerp(a, b, Mathf.Clamp01(t));
                    Vector2 dir = b - a;
                    if (dir.sqrMagnitude < 0.001f)
                    {
                        dir = Vector2.right;
                    }

                    dir.Normalize();
                    Vector2 side = new Vector2(-dir.y, dir.x) * (random.Next(0, 2) == 0 ? 1f : -1f);
                    float offset = 1.35f + (float)random.NextDouble() * 1.1f;
                    Vector2 place = point + side * offset;
                    float y = GroundY(stage.terrain, place.x, place.y);
                    if (y > stage.waterY + 0.35f)
                    {
                        Vector3 position = new Vector3(place.x, y, place.y);
                        int roll = random.Next(0, 5);
                        GameObject prefab = roll == 0 ? art.log : roll == 1 ? art.stump : art.pebble;
                        if (prefab != null)
                        {
                            PlacePrefab(prefab, parent, position, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                        }
                    }

                    next += 6.5f + (float)random.NextDouble() * 3f;
                }

                traveled += length;
            }
        }
    }
}
}
