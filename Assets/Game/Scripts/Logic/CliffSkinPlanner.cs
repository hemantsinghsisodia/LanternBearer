using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
public struct SkinPoint
{
    public Vector2 position;   // plan (x, z), already offset outward from the step edge
    public Vector2 normal;     // outward in plan, unit length (towards the low side)
    public float foot;         // terrain height at the base of the step (metres, relative to the terrain origin)
    public float rim;          // terrain height at the top of the step
}

public class SkinStrip
{
    public List<SkinPoint> points = new List<SkinPoint>();
    public bool closed;
    public float length;       // plan length of the (offset) polyline in metres
}

// Pure planner for the cliff skin. The islands' cliffs are single-cell height steps (up to about 6 m between neighbouring heightmap cells),
// so the terrain mesh shows them as one-cell-wide vertical ribs. The skin is a smooth wall laid just outside those steps.
//
// Method:
// 1. Step edges: every pair of 4-neighbour cells whose heights differ by more than the threshold is a unit segment on the dual grid
//    (cell corners), directed so that the high cell lies on its left.
// 2. The segments are chained into polylines (left turn preferred at junctions), resampled by arc length and smoothed in plan with
//    repeated box filters. Foot and rim heights ride along: foot is min-filtered and rim max-filtered first so the skin never ends
//    short of the real step, then both are smoothed.
// 3. The polyline is pushed outward (towards the low side) by ProudOffset, so the skin sits clear of the aliased teeth of the face.
public static class CliffSkinPlanner
{
    public const float StepThresholdM = 0.8f;
    public const float ProudOffset = 0.25f;     // plan distance from the step edge to the skin (the face's outermost tooth is about 0.06 m beyond the edge)
    public const float Spacing = 0.15f;         // resampled point spacing, metres
    public const int SmoothRadius = 2;          // box-filter half width in samples (two passes make a triangular kernel)
    public const int SmoothPasses = 3;
    public const int HeightRadius = 2;          // min / max filter half width for foot and rim, in samples
    public const float MinLength = 2f;          // shorter chains are single-cell noise and are skipped

    struct Edge
    {
        public int startKey;
        public int endKey;
        public Vector2 start;     // corner positions, world plan
        public Vector2 end;
        public float foot;
        public float rim;
    }

    public static List<SkinStrip> Plan(float[,] heights01, float worldSize, float heightScale, Vector2 originXZ, float stepThresholdM)
    {
        List<SkinStrip> result = new List<SkinStrip>();
        if (heights01 == null)
        {
            return result;
        }

        int rows = heights01.GetLength(0);
        int cols = heights01.GetLength(1);
        if (rows < 2 || cols < 2)
        {
            return result;
        }

        float cell = worldSize / (cols - 1);
        float threshold01 = stepThresholdM / heightScale;
        int stride = cols + 2;
        List<Edge> edges = new List<Edge>();

        for (int z = 0; z < rows; z++)
        {
            for (int x = 0; x < cols; x++)
            {
                float a = heights01[z, x];
                if (x + 1 < cols)
                {
                    float b = heights01[z, x + 1];
                    if (Mathf.Abs(a - b) > threshold01)
                    {
                        bool aHigh = a > b;
                        // Vertical segment between the two cells; direction +z puts the lower-x cell on the left.
                        int c0 = Key(x, z - 1, stride);
                        int c1 = Key(x, z, stride);
                        edges.Add(MakeEdge(aHigh ? c0 : c1, aHigh ? c1 : c0, Corner(x, aHigh ? z - 1 : z, originXZ, cell), Corner(x, aHigh ? z : z - 1, originXZ, cell), Mathf.Min(a, b) * heightScale, Mathf.Max(a, b) * heightScale));
                    }
                }

                if (z + 1 < rows)
                {
                    float b = heights01[z + 1, x];
                    if (Mathf.Abs(a - b) > threshold01)
                    {
                        bool bHigh = b > a;
                        // Horizontal segment; direction +x puts the higher-z cell on the left.
                        int c0 = Key(x - 1, z, stride);
                        int c1 = Key(x, z, stride);
                        edges.Add(MakeEdge(bHigh ? c0 : c1, bHigh ? c1 : c0, Corner(bHigh ? x - 1 : x, z, originXZ, cell), Corner(bHigh ? x : x - 1, z, originXZ, cell), Mathf.Min(a, b) * heightScale, Mathf.Max(a, b) * heightScale));
                    }
                }
            }
        }

        if (edges.Count == 0)
        {
            return result;
        }

        Dictionary<int, List<int>> outgoing = new Dictionary<int, List<int>>();
        HashSet<int> hasIncoming = new HashSet<int>();
        for (int i = 0; i < edges.Count; i++)
        {
            List<int> list;
            if (!outgoing.TryGetValue(edges[i].startKey, out list))
            {
                list = new List<int>(2);
                outgoing[edges[i].startKey] = list;
            }

            list.Add(i);
            hasIncoming.Add(edges[i].endKey);
        }

        bool[] used = new bool[edges.Count];
        // Open chains first (start vertex without an incoming edge), then whatever remains forms loops.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                if (used[i] || (pass == 0 && hasIncoming.Contains(edges[i].startKey)))
                {
                    continue;
                }

                List<int> chain = Walk(i, edges, outgoing, used, out bool closed);
                SkinStrip strip = BuildStrip(chain, edges, closed, cell);
                if (strip != null)
                {
                    result.Add(strip);
                }
            }
        }

        return result;
    }

    static int Key(int i, int j, int stride)
    {
        return (j + 1) * stride + (i + 1);
    }

    static Vector2 Corner(int i, int j, Vector2 origin, float cell)
    {
        return new Vector2(origin.x + (i + 0.5f) * cell, origin.y + (j + 0.5f) * cell);
    }

    static Edge MakeEdge(int startKey, int endKey, Vector2 start, Vector2 end, float foot, float rim)
    {
        Edge e = new Edge();
        e.startKey = startKey;
        e.endKey = endKey;
        e.start = start;
        e.end = end;
        e.foot = foot;
        e.rim = rim;
        return e;
    }

    static List<int> Walk(int first, List<Edge> edges, Dictionary<int, List<int>> outgoing, bool[] used, out bool closed)
    {
        List<int> chain = new List<int>();
        int current = first;
        closed = false;
        int startKey = edges[first].startKey;
        while (true)
        {
            used[current] = true;
            chain.Add(current);
            int end = edges[current].endKey;
            if (end == startKey)
            {
                closed = true;
                break;
            }

            List<int> next;
            if (!outgoing.TryGetValue(end, out next))
            {
                break;
            }

            Vector2 d1 = edges[current].end - edges[current].start;
            int best = -1;
            int bestRank = 4;
            for (int k = 0; k < next.Count; k++)
            {
                int candidate = next[k];
                if (used[candidate])
                {
                    continue;
                }

                Vector2 d2 = edges[candidate].end - edges[candidate].start;
                float cross = d1.x * d2.y - d1.y * d2.x;
                int rank = cross > 1e-6f ? 0 : (cross < -1e-6f ? 2 : 1);
                if (rank < bestRank)
                {
                    bestRank = rank;
                    best = candidate;
                }
            }

            if (best < 0)
            {
                break;
            }

            current = best;
        }

        return chain;
    }

    static SkinStrip BuildStrip(List<int> chain, List<Edge> edges, bool closed, float cell)
    {
        // Raw polyline through the edge midpoints (plus both end corners of an open chain), with step heights.
        List<Vector2> raw = new List<Vector2>();
        List<float> rawFoot = new List<float>();
        List<float> rawRim = new List<float>();
        if (!closed)
        {
            raw.Add(edges[chain[0]].start);
            rawFoot.Add(edges[chain[0]].foot);
            rawRim.Add(edges[chain[0]].rim);
        }

        for (int i = 0; i < chain.Count; i++)
        {
            Edge e = edges[chain[i]];
            raw.Add((e.start + e.end) * 0.5f);
            rawFoot.Add(e.foot);
            rawRim.Add(e.rim);
        }

        if (!closed)
        {
            Edge last = edges[chain[chain.Count - 1]];
            raw.Add(last.end);
            rawFoot.Add(last.foot);
            rawRim.Add(last.rim);
        }

        int rawCount = raw.Count;
        float[] arc = new float[rawCount + 1];
        for (int i = 1; i < rawCount; i++)
        {
            arc[i] = arc[i - 1] + Vector2.Distance(raw[i - 1], raw[i]);
        }

        arc[rawCount] = arc[rawCount - 1] + (closed ? Vector2.Distance(raw[rawCount - 1], raw[0]) : 0f);
        float total = arc[rawCount];
        if (total < MinLength)
        {
            return null;
        }

        int n = closed ? Mathf.Max(8, Mathf.RoundToInt(total / Spacing)) : Mathf.Max(8, Mathf.RoundToInt(total / Spacing) + 1);
        float step = closed ? total / n : total / (n - 1);
        Vector2[] pos = new Vector2[n];
        float[] foot = new float[n];
        float[] rim = new float[n];
        int seg = 0;
        for (int i = 0; i < n; i++)
        {
            float s = i * step;
            while (seg < rawCount - 1 && arc[seg + 1] < s)
            {
                seg++;
            }

            int a = seg;
            int b = closed ? (seg + 1) % rawCount : Mathf.Min(seg + 1, rawCount - 1);
            float span = arc[seg + 1] - arc[seg];
            float t = span > 1e-6f ? Mathf.Clamp01((s - arc[seg]) / span) : 0f;
            pos[i] = Vector2.Lerp(raw[a], raw[b], t);
            foot[i] = Mathf.Lerp(rawFoot[a], rawFoot[b], t);
            rim[i] = Mathf.Lerp(rawRim[a], rawRim[b], t);
        }

        // Smooth the plan polyline.
        for (int pass = 0; pass < SmoothPasses; pass++)
        {
            pos = Box(pos, SmoothRadius, closed);
        }

        // Foot may only go down and rim only up through the filters, so the skin always spans the real step.
        foot = Box(Box(Extreme(foot, HeightRadius, closed, false), 1, closed), 1, closed);
        rim = Box(Box(Extreme(rim, HeightRadius, closed, true), 1, closed), 1, closed);

        SkinStrip strip = new SkinStrip();
        strip.closed = closed;
        for (int i = 0; i < n; i++)
        {
            Vector2 prev = pos[closed ? (i - 1 + n) % n : Mathf.Max(i - 1, 0)];
            Vector2 next = pos[closed ? (i + 1) % n : Mathf.Min(i + 1, n - 1)];
            Vector2 tangent = (next - prev).normalized;
            Vector2 outward = new Vector2(tangent.y, -tangent.x);
            SkinPoint p = new SkinPoint();
            p.normal = outward;
            p.position = pos[i] + outward * ProudOffset;
            p.foot = foot[i];
            p.rim = rim[i];
            strip.points.Add(p);
        }

        float length = 0f;
        for (int i = 1; i < n; i++)
        {
            length += Vector2.Distance(strip.points[i - 1].position, strip.points[i].position);
        }

        if (closed)
        {
            length += Vector2.Distance(strip.points[n - 1].position, strip.points[0].position);
        }

        strip.length = length;
        return strip;
    }

    // Box filter with wrap (closed) or point reflection (open) at the ends, so straight runs keep their ends.
    static Vector2[] Box(Vector2[] source, int radius, bool closed)
    {
        int n = source.Length;
        Vector2[] result = new Vector2[n];
        float inv = 1f / (2 * radius + 1);
        for (int i = 0; i < n; i++)
        {
            Vector2 sum = Vector2.zero;
            for (int k = -radius; k <= radius; k++)
            {
                sum += Reflect(source, i + k, closed);
            }

            result[i] = sum * inv;
        }

        return result;
    }

    static float[] Box(float[] source, int radius, bool closed)
    {
        int n = source.Length;
        float[] result = new float[n];
        float inv = 1f / (2 * radius + 1);
        for (int i = 0; i < n; i++)
        {
            float sum = 0f;
            for (int k = -radius; k <= radius; k++)
            {
                sum += Index(source, i + k, closed);
            }

            result[i] = sum * inv;
        }

        return result;
    }

    static float[] Extreme(float[] source, int radius, bool closed, bool max)
    {
        int n = source.Length;
        float[] result = new float[n];
        for (int i = 0; i < n; i++)
        {
            float best = source[i];
            for (int k = -radius; k <= radius; k++)
            {
                float v = Index(source, i + k, closed);
                best = max ? Mathf.Max(best, v) : Mathf.Min(best, v);
            }

            result[i] = best;
        }

        return result;
    }

    static float Index(float[] source, int i, bool closed)
    {
        int n = source.Length;
        if (closed)
        {
            return source[((i % n) + n) % n];
        }

        return source[Mathf.Clamp(i, 0, n - 1)];
    }

    static Vector2 Reflect(Vector2[] source, int i, bool closed)
    {
        int n = source.Length;
        if (closed)
        {
            return source[((i % n) + n) % n];
        }

        if (i < 0)
        {
            return 2f * source[0] - source[Mathf.Min(-i, n - 1)];
        }

        if (i >= n)
        {
            return 2f * source[n - 1] - source[Mathf.Max(2 * (n - 1) - i, 0)];
        }

        return source[i];
    }
}
}
