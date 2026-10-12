using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// Island 5 ("The Great Light"): the spire. Everything here is reached only when LevelConfig.spire is true, so Islands 1-4 build exactly as before.
// Heights are worked out in metres above sea level and normalised at the end (world Y = sea + metres).
public static partial class IslandBuilder
{
    public const float SpireWorldSize = 140f;
    public const float SpireHillHeight = 52f;
    const string SpireBiomePath = "Assets/Game/Levels/Biomes/Spire.asset";
    const string Island5LookPath = "Assets/Game/Art/Look/LookProfile_island5.asset";
    const string Island4LookPath = "Assets/Game/Art/Look/LookProfile_island4.asset";
    const string Island5SkyPath = "Assets/Game/Art/Look/Sky/NightSky_island5.mat";
    const string Island4SkyPath = "Assets/Game/Art/Look/Sky/NightSky_island4.mat";

    const float SpireLedgeMargin = 3f;         // flat tread kept each side of a ledge before the cliff starts
    const float SpireShoulder = 1.5f;          // blend from the walkway edge to the hillside
    const float SpireTrenchDepth = 10f;        // the summit moat is cut this far below the surface
    const float SpireLipSoftness = 0.55f;      // the cut's wall is this wide, ending exactly at the near/far point
    const float SpireInletFloor = -1.8f;       // slot floor of gaps 1-3, metres below the sea
    const float SpireSlotLength = 16f;         // how far gaps 2 and 3 run outward from the chord
    const float SpireSeaFloor = -10.2f;
    const float SpirePedestalRadius = SpireLayout.SummitPlateauRadius;    // summit disc (the lamp plateau); the moat ring is cut outside it
    const float SpireMoatOuter = 7.8f;         // the walkway inner edge at the gap 4 near end (about r 9.7 - 1.75) stays outside this
    const float SpireInlet2Angle = 3.75f;      // radians; the second tidal inlet, away from every walkway

    // Fast nearest-point lookup on the spiral. A 6 m grid of 2400 samples, refined by projecting onto the two adjacent segments.
    sealed class SpirePathGrid
    {
        public const int Samples = 2400;
        const float Cell = 6f;
        readonly Vector2[] points = new Vector2[Samples + 1];
        readonly List<int>[] cells;
        readonly int side;
        readonly float half;

        public SpirePathGrid(float worldSize)
        {
            half = worldSize * 0.5f;
            side = Mathf.CeilToInt(worldSize / Cell);
            cells = new List<int>[side * side];
            for (int i = 0; i <= Samples; i++)
            {
                points[i] = SpireLayout.PathPoint(i / (float)Samples);
                int index = CellZ(points[i].y) * side + CellX(points[i].x);
                if (cells[index] == null)
                {
                    cells[index] = new List<int>();
                }

                cells[index].Add(i);
            }
        }

        int CellX(float x)
        {
            return Mathf.Clamp((int)((x + half) / Cell), 0, side - 1);
        }

        int CellZ(float z)
        {
            return Mathf.Clamp((int)((z + half) / Cell), 0, side - 1);
        }

        public bool Nearest(Vector2 p, out float u, out float distance)
        {
            int cx = CellX(p.x);
            int cz = CellZ(p.y);
            float best = float.MaxValue;
            int bestIndex = -1;
            for (int dz = -1; dz <= 1; dz++)
            {
                int nz = cz + dz;
                if (nz < 0 || nz >= side)
                {
                    continue;
                }

                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = cx + dx;
                    if (nx < 0 || nx >= side)
                    {
                        continue;
                    }

                    List<int> list = cells[nz * side + nx];
                    if (list == null)
                    {
                        continue;
                    }

                    for (int k = 0; k < list.Count; k++)
                    {
                        float sqr = (points[list[k]] - p).sqrMagnitude;
                        if (sqr < best)
                        {
                            best = sqr;
                            bestIndex = list[k];
                        }
                    }
                }
            }

            if (bestIndex < 0 || best > Cell * Cell)
            {
                u = 0f;
                distance = 999f;
                return false;
            }

            float bestSqr = float.MaxValue;
            float bestU = bestIndex / (float)Samples;
            for (int s = bestIndex - 1; s <= bestIndex; s++)
            {
                if (s < 0 || s >= Samples)
                {
                    continue;
                }

                Vector2 a = points[s];
                Vector2 ab = points[s + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float sqr = (p - (a + ab * t)).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestU = (s + t) / Samples;
                }
            }

            u = bestU;
            distance = Mathf.Sqrt(bestSqr);
            return true;
        }
    }

    // Frame of one required gap: centre of the near-far chord, the chord direction and the direction away from the summit.
    struct SpireGapFrame
    {
        public Vector2 centre;
        public Vector2 along;
        public Vector2 outward;
        public float halfWidth;
    }

    sealed class SpireContext
    {
        public SpirePathGrid grid;
        public SpireGapFrame[] frames;
        public Vector2[] beaconXZ;
        public float[] beaconHeight;
        public float walkwayEndU;
    }

    static SpireContext NewSpireContext(float worldSize)
    {
        SpireContext c = new SpireContext();
        c.grid = new SpirePathGrid(worldSize);
        SpireGap[] gaps = SpireLayout.Gaps();
        c.frames = new SpireGapFrame[gaps.Length];
        for (int i = 0; i < gaps.Length; i++)
        {
            Vector2 chord = gaps[i].farXZ - gaps[i].nearXZ;
            SpireGapFrame f = new SpireGapFrame();
            f.centre = 0.5f * (gaps[i].nearXZ + gaps[i].farXZ);
            f.halfWidth = 0.5f * chord.magnitude;
            f.along = chord.normalized;
            Vector2 perp = new Vector2(-f.along.y, f.along.x);
            f.outward = Vector2.Dot(perp, f.centre) >= 0f ? perp : -perp;
            c.frames[i] = f;
        }

        c.beaconXZ = SpireLayout.BeaconXZ();
        float[] bu = SpireLayout.BeaconParameters();
        c.beaconHeight = new float[bu.Length];
        for (int i = 0; i < bu.Length; i++)
        {
            c.beaconHeight[i] = SpireLayout.PathHeight(bu[i]);
        }

        c.walkwayEndU = SpireLayout.GapSpanU(3).x;
        return c;
    }

    static float SpireRadiusAt(float u)
    {
        return SpireLayout.EndRadius + (SpireLayout.StartRadius - SpireLayout.EndRadius) * Mathf.Pow(1f - u, SpireLayout.RadiusPower);
    }

    // Tread-and-cliff profile between two ledges: 0..1 in, 0..1 out. Each terrace has a nearly flat tread and then a steep face.
    static float TerraceProfile(float t, float rise)
    {
        if (rise < 3f)
        {
            return Mathf.SmoothStep(0f, 1f, t);
        }

        int n = Mathf.Max(1, Mathf.RoundToInt(rise / 5.5f));
        float x = Mathf.Clamp01(t) * n;
        int k = Mathf.Min(n - 1, (int)x);
        float f = x - k;
        float step = 0.08f * Mathf.Clamp01(f / 0.55f) + 0.92f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, f));
        return (k + step) / n;
    }

    static readonly float[] spireNodeR = new float[5];
    static readonly float[] spireNodeH = new float[5];

    // The mountain before any walkway or gap is cut: ledges at the spiral's radius for the pixel's angle, terraced cliffs between them.
    static float SpireBaseHeight(Vector2 p, float r, float phi)
    {
        if (r >= SpireLayout.StartRadius)
        {
            float s = r - SpireLayout.StartRadius;
            float h = s <= 4f ? SpireLayout.PathBaseHeight * (1f - s / 4f) : -(s - 4f) * 0.55f;
            float wobble = (Mathf.PerlinNoise(p.x * 0.09f + 11f, p.y * 0.09f + 3f) - 0.5f) * 2.4f;
            h += wobble * Mathf.SmoothStep(0f, 1f, s / 3f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(7f, 11f, s)));
            return Mathf.Max(h, SpireSeaFloor);
        }

        float[] nodeR = spireNodeR;
        float[] nodeH = spireNodeH;
        int count = 1;
        nodeR[0] = SpireLayout.StartRadius;
        nodeH[0] = SpireLayout.PathBaseHeight;
        float turn = 2f * Mathf.PI * SpireLayout.Turns;
        for (int j = 0; j < 3; j++)
        {
            float u = Mathf.Min(1f, (phi + 2f * Mathf.PI * j) / turn);
            float rr = SpireRadiusAt(u);
            if (nodeR[count - 1] - rr < 0.4f)
            {
                continue;
            }

            nodeR[count] = rr;
            nodeH[count] = SpireLayout.PathHeightSmooth(u);
            count++;
        }

        if (nodeR[count - 1] > 0.4f)
        {
            nodeR[count] = 0f;
            nodeH[count] = SpireLayout.SummitHeight;
            count++;
        }

        int seg = 0;
        while (seg < count - 2 && r < nodeR[seg + 1])
        {
            seg++;
        }

        float length = nodeR[seg] - nodeR[seg + 1];
        float rise = nodeH[seg + 1] - nodeH[seg];
        float margin = Mathf.Min(SpireLedgeMargin, length * 0.3f);
        float t2 = Mathf.Clamp01((nodeR[seg] - r - margin) / Mathf.Max(0.01f, length - 2f * margin));
        return nodeH[seg] + rise * TerraceProfile(t2, rise);
    }

    static float GapEdgeMask(float along, float halfWidth)
    {
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfWidth - SpireLipSoftness, halfWidth, Mathf.Abs(along)));
    }

    static float GapInnerMask(float outward)
    {
        float inner = -(SpireLayout.PathWidth * 0.5f + SpireLedgeMargin);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner - 0.6f, inner, outward));
    }

    // A trench that follows the surface down the cliffs and fades out, so it always opens onto lower ground (no pit to be trapped in).
    static float GapPitMask(SpireGapFrame f, Vector2 p)
    {
        Vector2 d = p - f.centre;
        float along = Vector2.Dot(d, f.along);
        float outward = Vector2.Dot(d, f.outward);
        float taper = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.5f, 12.5f, outward));
        return GapEdgeMask(along, f.halfWidth) * GapInnerMask(outward) * taper;
    }

    static float SpireHeightMeters(SpireContext c, Vector2 p)
    {
        float r = p.magnitude;
        float phi = Mathf.Atan2(p.y, p.x);
        if (phi < 0f)
        {
            phi += 2f * Mathf.PI;
        }

        float h = SpireBaseHeight(p, r, phi);

        // Walkway: flat across, following PathHeight along, blended into the hillside.
        float walkway = 0f;
        float pathHeight = 0f;
        float u;
        float d;
        if (c.grid.Nearest(p, out u, out d) && u <= c.walkwayEndU)
        {
            walkway = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SpireLayout.PathWidth * 0.5f, SpireLayout.PathWidth * 0.5f + SpireShoulder, d));
            pathHeight = SpireLayout.PathHeight(u);

            // Beacon landings: the walkway widens to a round pad that follows PathHeight like the walkway, so the keeper stands
            // where the layout says and the pad never adds a bump.
            for (int b = 0; b < c.beaconXZ.Length; b++)
            {
                float bd = Vector2.Distance(p, c.beaconXZ[b]);
                if (bd < 4.2f)
                {
                    walkway = Mathf.Max(walkway, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.4f, 4.2f, bd)));
                }
            }
        }

        if (r < SpireLayout.StartRadius)
        {
            float rough = Mathf.PerlinNoise(p.x * 0.16f + 7.3f, p.y * 0.16f + 1.9f) - 0.5f;
            float fine = Mathf.PerlinNoise(p.x * 0.55f + 2f, p.y * 0.55f + 9f) - 0.5f;
            h += (rough * 1.6f + fine * 0.6f) * (1f - walkway);
        }

        if (walkway > 0f)
        {
            h = Mathf.Lerp(h, pathHeight, walkway);
        }

        // Summit disc under the great lamp: 40 m at the centre, 38.5 m (the path's end) at its edge.
        float dome = SpireLayout.SummitHeight - 1.5f * Mathf.SmoothStep(0f, 1f, r / SpirePedestalRadius);
        h = Mathf.Lerp(h, dome, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SpirePedestalRadius - 0.4f, SpirePedestalRadius + 0.05f, r)));

        // Gaps 1-3: water slots. Each is cut to below the sea (gap 1 runs out to the open sea, gaps 2 and 3 stop short of the walkway
        // below them), so a keeper who steps off lands in the water and is put back by the water hazard instead of being stuck in a pit.
        for (int g = 0; g < 3; g++)
        {
            SpireGapFrame f = c.frames[g];
            Vector2 dv = p - f.centre;
            float outward = Vector2.Dot(dv, f.outward);
            float mask = GapEdgeMask(Vector2.Dot(dv, f.along), f.halfWidth) * GapInnerMask(outward);
            if (g > 0)
            {
                mask *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SpireSlotLength - 1.2f, SpireSlotLength, outward));
            }

            if (mask > 0f)
            {
                h = Mathf.Lerp(h, Mathf.Min(h, SpireInletFloor), mask);
            }
        }

        // Gap 4: a moat ring round the summit disc plus a trench open to the outside (so it can be left by walking down).
        float trench = 0f;
        float outsidePedestal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SpirePedestalRadius + 0.05f, SpirePedestalRadius + 0.45f, r));
        float ring = outsidePedestal * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SpireMoatOuter - 0.35f, SpireMoatOuter, r)));
        trench = Mathf.Max(trench, Mathf.Max(ring, GapPitMask(c.frames[3], p) * outsidePedestal));
        h -= SpireTrenchDepth * trench;

        // The second tidal inlet on the low ring: a bay well clear of the walkway.
        {
            Vector2 axis = new Vector2(Mathf.Cos(SpireInlet2Angle), Mathf.Sin(SpireInlet2Angle));
            float radial = Vector2.Dot(p, axis);
            float lateral = Vector2.Dot(p, new Vector2(-axis.y, axis.x));
            float mask = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 5.2f, Mathf.Abs(lateral)))) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(33f, 36f, radial));
            if (mask > 0f)
            {
                h = Mathf.Lerp(h, Mathf.Min(h, -2.1f), mask);
            }
        }

        return Mathf.Max(h, SpireSeaFloor);
    }

    static float[,] SculptSpire(LevelConfig config, int resolution, float worldSize, List<Vector2> beacons, List<List<Vector2>> trails)
    {
        float hill = config.hillHeight;
        float sea = WaterFraction * hill;
        SpireContext context = NewSpireContext(worldSize);
        float[,] heights = new float[resolution, resolution];
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float metres = SpireHeightMeters(context, PixelToWorld(x, z, resolution, worldSize));
                heights[z, x] = Mathf.Clamp01((metres + sea) / hill);
            }
        }

        // One trail along the walkway, spawn to the last beacon area: props and the reachability data keep clear of it.
        List<Vector2> trail = new List<Vector2>();
        const int trailPoints = 360;
        for (int i = 0; i <= trailPoints; i++)
        {
            trail.Add(SpireLayout.PathPoint(context.walkwayEndU * i / trailPoints));
        }

        trails.Add(trail);
        return heights;
    }

    // Height-band palettes: low = wet dark rock, kelp and teal moss; mid = heather and ochre; high = black crag.
    static float[,,] PaintSpire(float[,] heights, int heightRes, int alphaRes, LevelConfig config, float worldSize)
    {
        float hill = config.hillHeight;
        float sea = WaterFraction * hill;
        float run = worldSize / (heightRes - 1);
        SpireContext context = NewSpireContext(worldSize);
        float[,,] maps = new float[alphaRes, alphaRes, 5];
        for (int z = 0; z < alphaRes; z++)
        {
            for (int x = 0; x < alphaRes; x++)
            {
                int hx = Mathf.Clamp(Mathf.RoundToInt(x / (float)(alphaRes - 1) * (heightRes - 1)), 0, heightRes - 1);
                int hz = Mathf.Clamp(Mathf.RoundToInt(z / (float)(alphaRes - 1) * (heightRes - 1)), 0, heightRes - 1);
                int hx2 = Mathf.Clamp(hx + 1, 0, heightRes - 1);
                int hz2 = Mathf.Clamp(hz + 1, 0, heightRes - 1);
                float metres = heights[hz, hx] * hill - sea;
                float slope = Mathf.Max(Mathf.Abs(heights[hz, hx2] - heights[hz, hx]), Mathf.Abs(heights[hz2, hx] - heights[hz, hx])) * hill / run;
                Vector2 world = PixelToWorld(x, z, alphaRes, worldSize);
                float n = Mathf.PerlinNoise(world.x * 0.13f + 4f, world.y * 0.13f);
                float n2 = Mathf.PerlinNoise(world.x * 0.045f + 9f, world.y * 0.06f + 2f);

                float cliff = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.4f, slope));
                float low = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 12f, metres));
                float high = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 27f, metres));
                float mid = Mathf.Clamp01(1f - low - high);

                float walk = 0f;
                float u;
                float d;
                if (context.grid.Nearest(world, out u, out d) && u <= context.walkwayEndU)
                {
                    walk = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SpireLayout.PathWidth * 0.5f - 0.15f, SpireLayout.PathWidth * 0.5f + 0.9f, d));
                }

                float sand = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 2.4f, metres))) * 1.2f + low * 0.1f * Mathf.Lerp(0.6f, 1.3f, n);
                float grass = (low * 0.85f + mid * 0.55f + high * 0.06f) * (1f - cliff) * Mathf.Lerp(0.75f, 1.2f, n2);
                float dirt = mid * (0.12f + 0.2f * cliff) * Mathf.Lerp(0.8f, 1.15f, n) + walk * 4f;
                float rock = cliff * (low * 0.8f + mid * 0.9f + high * 1.3f) + high * 0.9f + low * 0.12f;
                float moss = low * 0.55f * (1f - cliff * 0.6f) * Mathf.Lerp(0.6f, 1.25f, n2);
                // The sea floor goes dark so the edge of the terrain does not show through the water.
                float deep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, -4.5f, metres));
                sand *= 1f - deep;
                grass *= 1f - deep;
                moss *= 1f - deep;
                rock += deep * 3f;
                grass *= 1f - walk * 0.85f;
                moss *= 1f - walk * 0.85f;
                rock *= 1f - walk * 0.7f;
                sand *= 1f - walk * 0.5f;

                float sum = sand + grass + dirt + rock + moss;
                if (sum < 0.001f)
                {
                    rock = 1f;
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

    // Low pale cairns along the outer edge of the walkway every 5 m, so the spiral reads from the sea. No colliders (they sit on
    // the edge and must never block a step), none at the gaps or the beacons, and every fourth carries a faint cold-blue glint.
    static void PlaceSpireCairns(Stage stage)
    {
        Material pale = EnsureCairnMaterial("Assets/Game/Materials/Generated/PathCairn_island5.mat", false);
        Material glint = EnsureCairnMaterial("Assets/Game/Materials/Generated/PathCairnGlint_island5.mat", true);
        Transform root = Folder("PathCairns");
        const int samples = 1600;
        float endU = SpireLayout.GapSpanU(3).x - 0.004f;
        Vector2[] beacons = SpireLayout.BeaconXZ();
        float travelled = 0f;
        float nextAt = 3f;
        int count = 0;
        Vector2 previous = SpireLayout.PathPoint(0f);
        for (int i = 1; i <= samples; i++)
        {
            float u = endU * i / samples;
            Vector2 point = SpireLayout.PathPoint(u);
            travelled += Vector2.Distance(previous, point);
            previous = point;
            if (travelled < nextAt)
            {
                continue;
            }

            nextAt += 4.6f + 0.8f * ((count * 7) % 3) * 0.5f;
            bool skip = false;
            for (int g = 0; g < 3 && !skip; g++)
            {
                Vector2 span = SpireLayout.GapSpanU(g);
                skip = u > span.x - 0.006f && u < span.y + 0.006f;
            }

            for (int b = 0; b < beacons.Length && !skip; b++)
            {
                skip = Vector2.Distance(point, beacons[b]) < 3.6f;
            }

            Vector2 outward = point.sqrMagnitude > 0.01f ? point.normalized : Vector2.right;
            Vector2 spot = point + outward * (SpireLayout.PathWidth * 0.5f - 0.1f);
            float ground = GroundY(stage.terrain, spot.x, spot.y);
            if (skip || Mathf.Abs(ground - (stage.waterY + SpireLayout.PathHeight(u))) > 0.7f)
            {
                continue;
            }


            GameObject cairn = new GameObject("Cairn_" + count);
            cairn.transform.SetParent(root, false);
            cairn.transform.position = new Vector3(spot.x, ground, spot.y);
            float yaw = (count * 53) % 360;
            float[] radii = { 0.5f, 0.36f, 0.24f };
            float y = 0f;
            for (int s = 0; s < radii.Length; s++)
            {
                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stone.name = "Stone" + s;
                Object.DestroyImmediate(stone.GetComponent<Collider>());
                stone.transform.SetParent(cairn.transform, false);
                float height = radii[s] * 1.1f;
                stone.transform.localScale = new Vector3(radii[s] * 2.2f, height, radii[s] * 2f);
                stone.transform.localPosition = new Vector3(0.04f * s, y + height * 0.4f, 0f);
                stone.transform.localRotation = Quaternion.Euler(0f, yaw + s * 40f, 0f);
                y += height * 0.75f;
                MeshRenderer renderer = stone.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = s == radii.Length - 1 ? glint : pale;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            count++;
        }

        Debug.Log("Island5 path cairns: " + count);
    }

    static Material EnsureCairnMaterial(string path, bool glint)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", new Color(0.7f, 0.74f, 0.78f, 1f));
        material.SetFloat("_Smoothness", 0.1f);
        material.SetFloat("_Metallic", 0f);
        if (glint)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.2f, 0.55f, 1.2f, 1f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    // The terrain is a square on an endless sea, and its edge shows through the water. A dark floor just under the terrain's lowest
    // point carries the same sea bed on past the edge, so the water looks the same inside and outside the square.
    static void CreateSpireSeaFloor(Stage stage)
    {
        const string materialPath = "Assets/Game/Materials/Generated/SeaFloor_island5.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.SetColor("_BaseColor", new Color(0.03f, 0.04f, 0.05f, 1f));
        material.SetFloat("_Smoothness", 0f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "SeaFloor";
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.position = new Vector3(0f, 0.02f, 0f);
        floor.transform.localScale = new Vector3(120f, 1f, 120f);
        MeshRenderer renderer = floor.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static Vector3 SpireSpawn(Terrain terrain)
    {
        Vector2 start = SpireLayout.PathPoint(0f);
        return new Vector3(start.x, GroundY(terrain, start.x, start.y) + 0.05f, start.y);
    }

    // Firefly spots along the walkway, most on the low ring (dense fireflies there, fewer higher up), a little to either side.
    static void PlaceSpireFireflies(LevelConfig config, ArtKit art, Stage stage)
    {
        Transform parent = Folder("Fireflies");
        int count = Mathf.Max(0, config.fireflyCount);
        float endU = SpireLayout.GapSpanU(3).x;
        for (int i = 0; i < count; i++)
        {
            float u = 0.03f + (endU - 0.05f) * Mathf.Pow((i + 0.5f) / count, 1.35f);
            Vector2 point = SpireLayout.PathPoint(u);
            Vector2 tangent = SpireLayout.PathTangent(u);
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            float side = (i % 2 == 0 ? 1f : -1f) * (0.4f + 0.8f * ((i * 7) % 5) / 4f);
            point += normal * side;
            Vector3 pos = new Vector3(point.x, 0f, point.y);
            pos.y = GroundY(stage.terrain, pos.x, pos.z) + 1.35f;
            GameObject fly = PlacePrefab(art.firefly, parent, pos, Quaternion.identity);
            Firefly script = fly.GetComponent<Firefly>();
            if (script != null)
            {
                SerializedObject so = new SerializedObject(script);
                so.FindProperty("islandRadius").floatValue = config.islandRadius * 0.78f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            stage.fireflies++;
        }
    }

    // Gap0..Gap3 (each with Near and Far) at the SpireLayout.Gaps() ends, and the great lamp's slot, for the bridge and lamp work.
    static void PlaceSpireMarkers(Stage stage)
    {
        Transform root = Folder("SpireMarkers");
        SpireGap[] gaps = SpireLayout.Gaps();
        for (int i = 0; i < gaps.Length; i++)
        {
            Transform gap = Folder("Gap" + i);
            gap.SetParent(root, false);
            Transform near = Folder("Near");
            near.SetParent(gap, false);
            near.position = new Vector3(gaps[i].nearXZ.x, GroundY(stage.terrain, gaps[i].nearXZ.x, gaps[i].nearXZ.y), gaps[i].nearXZ.y);
            Transform far = Folder("Far");
            far.SetParent(gap, false);
            far.position = new Vector3(gaps[i].farXZ.x, GroundY(stage.terrain, gaps[i].farXZ.x, gaps[i].farXZ.y), gaps[i].farXZ.y);
            gap.position = 0.5f * (near.position + far.position);
            near.position = new Vector3(gaps[i].nearXZ.x, GroundY(stage.terrain, gaps[i].nearXZ.x, gaps[i].nearXZ.y), gaps[i].nearXZ.y);
            far.position = new Vector3(gaps[i].farXZ.x, GroundY(stage.terrain, gaps[i].farXZ.x, gaps[i].farXZ.y), gaps[i].farXZ.y);
        }

        Transform lamp = Folder("GreatLampSlot");
        lamp.SetParent(root, false);
        Vector2 lampXZ = SpireLayout.LampXZ();
        lamp.position = new Vector3(lampXZ.x, GroundY(stage.terrain, lampXZ.x, lampXZ.y), lampXZ.y);
    }

    // Placeholder set pieces: rock clusters flanking each gap. The sea arch, split chasm, collapsed stair and knife-edge ridge are polish.
    static void PlaceSpireSetPieces(ArtKit art, Stage stage)
    {
        if (art.rockA == null || art.rockB == null)
        {
            return;
        }

        string[] names = { "SeaArch", "SplitChasm", "CollapsedStair", "KnifeEdgeRidge" };
        Transform root = Folder("GapSetPieces");
        SpireContext context = NewSpireContext(stage.worldSize);
        System.Random random = new System.Random(5505 + 9);
        for (int g = 0; g < context.frames.Length; g++)
        {
            SpireGapFrame f = context.frames[g];
            Transform piece = Folder("Gap" + g + "_" + names[g]);
            piece.SetParent(root, false);
            float[] alongs = { -1f, 1f };
            float[] outs = { 5.4f, -5.4f, 7.6f };
            for (int a = 0; a < alongs.Length; a++)
            {
                for (int o = 0; o < outs.Length; o++)
                {
                    if (g == 3 && outs[o] < 0f)
                    {
                        continue;
                    }

                    Vector2 xz = f.centre + f.along * (alongs[a] * (f.halfWidth + 0.4f + 0.8f * o)) + f.outward * outs[o];
                    Vector3 pos = new Vector3(xz.x, GroundY(stage.terrain, xz.x, xz.y), xz.y);
                    if (pos.y < stage.waterY + 0.2f)
                    {
                        continue;
                    }

                    GameObject rock = PlacePrefab(((a + o) % 2 == 0) ? art.rockA : art.rockB, piece, pos, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                    float scale = 1.5f + (float)random.NextDouble() * 0.9f;
                    rock.transform.localScale = new Vector3(scale, scale * (g == 3 ? 1.9f : 1.4f), scale);
                }
            }
        }
    }

    // ---- Island 5 assets: config, biome, look ----

    [MenuItem("Lantern Keeper/Build Island 5")]
    public static void BuildIsland5()
    {
        LevelConfig config = CreateIsland5Config();
        Build(config);
        // Build wipes the bridges and rings (they are installed on top of the built scene), so a rebuild runs the installers too.
        for (int i = 0; i < Island5PostSteps.Length; i++)
        {
            Island5PostSteps[i].run();
        }
    }

    // The installers BuildIsland5 runs after Build, in order: bridges (with gate and lamp), then rings, then the lighthouse, then the performance pass,
    // then the music director (Build wipes it, like on Islands 1-4).
    public static readonly (string name, System.Action run)[] Island5PostSteps =
    {
        ("BridgeInstaller", BridgeInstaller.Install),
        ("RingInstaller", RingInstaller.Install),
        ("LighthouseInstaller", LighthouseInstaller.Install),
        ("Island5PerfInstaller", Island5PerfInstaller.Install),
        ("AudioInstaller", AudioInstaller.InstallIsland5)
    };

    public static LevelConfig CreateIsland5Config()
    {
        EnsureFolder("Assets/Game/Levels");
        EnsureFolder("Assets/Game/Levels/Biomes");
        EnsureSpireBiome();
        LevelConfig island5 = WriteConfig(LevelAssetPath("Island5"), "island5", "Island 5", "Island5", 45f, SpireHillHeight, 5505, 8, 26, 4, 0, new Color(0.05f, 0.11f, 0.13f, 1f), 0.014f, "", 0f, 90f, 0f, Island5Log(), SpireBiomePath);
        island5.islandTitle = "The Great Light";
        island5.introLines = Island5Intro();
        island5.spire = true;
        island5.plateauIsletBeacons = false;
        island5.windStrength = 0f;
        island5.shadeCount = 0;
        island5.lightning = false;
        island5.ridges = false;
        island5.lookProfile = EnsureIsland5Look();
        EditorUtility.SetDirty(island5);
        AssetDatabase.SaveAssets();
        return island5;
    }

    // Spire biome: the Heath entries (already nature-kit prefabs) with trees held to the mid ring and everything but rock kept off the cliffs.
    static Biome EnsureSpireBiome()
    {
        Biome biome = AssetDatabase.LoadAssetAtPath<Biome>(SpireBiomePath);
        if (biome == null)
        {
            biome = ScriptableObject.CreateInstance<Biome>();
            AssetDatabase.CreateAsset(biome, SpireBiomePath);
        }

        if (biome.entries != null && biome.entries.Length > 0)
        {
            return biome;
        }

        Biome heath = AssetDatabase.LoadAssetAtPath<Biome>(HeathBiomePath);
        List<BiomeEntry> entries = new List<BiomeEntry>();
        if (heath != null && heath.entries != null)
        {
            for (int i = 0; i < heath.entries.Length; i++)
            {
                BiomeEntry source = heath.entries[i];
                if (source == null || source.prefab == null)
                {
                    continue;
                }

                BiomeEntry entry = new BiomeEntry();
                entry.prefab = source.prefab;
                entry.category = source.category;
                entry.count = source.count;
                entry.scaleRange = source.scaleRange;
                entry.alignToSlope = source.alignToSlope;
                entry.limitHeight = source.limitHeight;
                entry.minHeight = source.minHeight;
                entry.maxHeight = source.maxHeight;
                entry.limitSlope = source.limitSlope;
                entry.minSlope = source.minSlope;
                entry.maxSlope = source.maxSlope;
                if (entry.category == BiomeCategory.Tree)
                {
                    // Wind-sculpted pines live on the mid ring only (10-25 m above the sea is 0.39-0.68 of the terrain).
                    entry.limitHeight = true;
                    entry.minHeight = 0.4f;
                    entry.maxHeight = 0.66f;
                }
                else if (entry.category == BiomeCategory.Sapling)
                {
                    entry.limitHeight = true;
                    entry.minHeight = 0.28f;
                    entry.maxHeight = 0.68f;
                }

                if (entry.category == BiomeCategory.Tree || entry.category == BiomeCategory.Sapling || entry.category == BiomeCategory.Undergrowth || entry.category == BiomeCategory.GroundCover)
                {
                    entry.limitSlope = true;
                    entry.minSlope = 0f;
                    entry.maxSlope = 0.45f;
                }

                entries.Add(entry);
            }
        }

        biome.entries = entries.ToArray();
        EditorUtility.SetDirty(biome);
        AssetDatabase.SaveAssets();
        return biome;
    }

    // Island 4's profile, darkened and pulled to a cold teal. Created once; later runs keep any hand edits.
    static LookProfile EnsureIsland5Look()
    {
        LookProfile look = AssetDatabase.LoadAssetAtPath<LookProfile>(Island5LookPath);
        if (look == null)
        {
            if (!AssetDatabase.CopyAsset(Island4LookPath, Island5LookPath))
            {
                Debug.LogError("Could not copy " + Island4LookPath);
                return null;
            }

            look = AssetDatabase.LoadAssetAtPath<LookProfile>(Island5LookPath);
            look.levelId = "island5";
            look.sky = new Color(0.05f, 0.09f, 0.13f, 1f);
            look.sea = new Color(0.06f, 0.12f, 0.15f, 1f);
            look.land = new Color(0.13f, 0.19f, 0.22f, 1f);
            look.moonRim = new Color(0.55f, 0.76f, 0.92f, 1f);
            look.extra = new Color(0.78f, 0.92f, 1f, 1f);
            look.moonOn = true;
            look.moon = new Color(0.72f, 0.86f, 1f, 1f);
            look.fogColour = new Color(0.05f, 0.11f, 0.13f, 1f);
            look.fogDensity = 0.014f;
            look.moonRimStrength = 0.85f;
            look.skyHorizon = new Color(0.2f, 0.34f, 0.4f, 1f);
            look.skyGround = new Color(0.03f, 0.05f, 0.06f, 1f);
            look.moonIntensity = 0.3f;
            look.ambientIntensity = 0.8f;
            look.rain = false;
            look.mist = false;
            look.grassRoot = new Color(0.05f, 0.2f, 0.17f, 1f);
            look.grassTip = new Color(0.22f, 0.55f, 0.45f, 1f);
            look.foliage = new Color(0.22f, 0.3f, 0.32f, 1f);
            look.bark = new Color(0.2f, 0.19f, 0.2f, 1f);
            look.rockTint = new Color(0.3f, 0.34f, 0.4f, 1f);
            look.treeFoliage = new Color(0.1f, 0.2f, 0.17f, 1f);
            EditorUtility.SetDirty(look);
        }

        if (AssetDatabase.LoadAssetAtPath<Material>(Island5SkyPath) == null)
        {
            AssetDatabase.CopyAsset(Island4SkyPath, Island5SkyPath);
        }

        AssetDatabase.SaveAssets();
        return look;
    }

    static string[] Island5Intro()
    {
        return new[]
        {
            "One path winds up the mountain to the Great Light. Follow it to the summit.",
            "Some gaps are too wide to jump. Light the beacon at the edge and a bridge of light carries you across.",
            "Light all eight beacons on the climb. Then the summit opens and the Great Light can wake.",
            "The storm only grows as you climb, and turning back will not slow it."
        };
    }

    static string[] Island5Log()
    {
        return new[]
        {
            "The lighthouse was raised by keepers long gone, stone by stone, up the mountain's one path. Each left a beacon behind, and a name cut into its foot.",
            "The storm came on the longest night and put the Great Light out. It has not eased since. The keepers who stayed lit what they could, and the dark took the rest.",
            "The last keeper's page is nailed to the lamp-room door: 'I could not relight it alone. I will wait. Whoever climbs this far, finish what I began.'"
        };
    }
}
}
