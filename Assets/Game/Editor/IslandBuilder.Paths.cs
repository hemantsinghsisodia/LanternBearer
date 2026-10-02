using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
public static partial class IslandBuilder
{
    // Hidden paths: a light-gated line of flat stones across the water, with always-solid bank steps
    // climbing each shore so every rise stays below the player's step offset (0.4 m).
    const float StoneHalfHeight = 0.07f;
    const float BankHalfHeight = 0.25f;
    const float PathMaxRise = 0.33f;
    const float PathMaxSpacing = 0.82f;
    const float PathMinSpacing = 0.4f;
    const float GentleSlope = 0.7f;
    static readonly float[] LandingOffsets = { 0f, 10f, -10f, 20f, -20f, 30f, -30f, 40f, -40f, 50f, -50f, 60f, -60f, 70f, -70f };
    const float HighClearance = 1.5f;
    const float SupportGap = 0.6f;

    class PathPlan
    {
        public bool valid;
        public float score;
        public float offset;
        public int bankSteps;
        public float maxClearance;   // highest stone top above the terrain or water beneath it
        public int highStones;       // stones more than HighClearance above what is beneath them
        public readonly List<Vector3> stones = new List<Vector3>();   // x, top, z; emitted from the main island end to the islet end
    }

    static void PlacePaths(LevelConfig config, ArtKit art, Stage stage)
    {
        Transform parent = Folder("HiddenPaths");
        for (int i = 0; i < stage.islets.Count; i++)
        {
            PathPlan best = null;
            for (int c = 0; c < LandingOffsets.Length; c++)
            {
                PathPlan plan = PlanPath(config, stage, stage.islets[i], LandingOffsets[c]);
                if (plan == null)
                {
                    continue;
                }

                if (best == null || plan.score < best.score - 0.0001f)
                {
                    best = plan;
                }
            }

            if (best == null)
            {
                Debug.LogWarning("PlacePaths: " + config.levelId + " path " + i + " found no water crossing; no path built.");
                continue;
            }

            if (!best.valid)
            {
                Debug.LogWarning("PlacePaths: " + config.levelId + " path " + i + " has no gentle landing (best offset " + best.offset + " deg); no path built.");
                continue;
            }

            Debug.Log("PlacePaths: " + config.levelId + " path " + i + " offset=" + best.offset + " stones=" + best.stones.Count + " bankSteps=" + best.bankSteps + " maxClearance=" + best.maxClearance.ToString("0.0") + " highStones=" + best.highStones);
            BuildPath(art, stage, parent, best);
        }
    }

    static float PathGround(Stage stage, Vector2 p)
    {
        return GroundY(stage.terrain, p.x, p.y);
    }

    // Walks a line from the islet centre toward the main island, finds where it leaves the islet
    // (islet water edge) and where it climbs out of the water again (main water edge), plans a landing
    // stone at each end and fills the stone line between them. The score prefers few elevated stones
    // and low ones, so a bank that needs a tall flying stair loses to a gentler one.
    static PathPlan PlanPath(LevelConfig config, Stage stage, Vector3 islet3, float offset)
    {
        Vector2 islet = new Vector2(islet3.x, islet3.z);
        Vector2 inward = -islet;
        if (inward.sqrMagnitude < 0.01f)
        {
            inward = Vector2.down;
        }

        inward.Normalize();
        float rad = offset * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(
            inward.x * Mathf.Cos(rad) - inward.y * Mathf.Sin(rad),
            inward.x * Mathf.Sin(rad) + inward.y * Mathf.Cos(rad));

        float edgeY = stage.highWaterY + 0.05f;
        float half = stage.worldSize * 0.5f - 1f;
        const float probeStep = 0.25f;
        float tA = 0f;
        float tB = -1f;
        bool inWater = PathGround(stage, islet) < edgeY;
        float previous = 0f;
        for (float t = probeStep; t < config.islandRadius * 2f; t += probeStep)
        {
            Vector2 p = islet + dir * t;
            if (Mathf.Abs(p.x) > half || Mathf.Abs(p.y) > half)
            {
                break;
            }

            float g = PathGround(stage, p);
            if (!inWater)
            {
                if (g < edgeY)
                {
                    inWater = true;
                    tA = RefineEdge(stage, islet, dir, previous, t, edgeY);
                }
            }
            else if (g >= edgeY)
            {
                tB = RefineEdge(stage, islet, dir, previous, t, edgeY);
                break;
            }

            previous = t;
        }

        if (tB < 0f || tB - tA < 0.3f)
        {
            return null;
        }

        float top0 = stage.highWaterY + 0.12f + StoneHalfHeight;
        float mainS;
        float mainTop;
        float isletS;
        float isletTop;
        PathPlan plan = new PathPlan();
        plan.offset = offset;
        plan.score = 1000f + Mathf.Abs(offset);
        if (!PlanLanding(stage, islet + dir * tB, dir, top0, out mainS, out mainTop)
            || !PlanLanding(stage, islet + dir * tA, -dir, top0, out isletS, out isletTop))
        {
            return plan;
        }

        // The stone line runs between the two landing stones. Heights fall from each end by at most
        // PathMaxRise per stone and are floored at the level of the light-gated stones, so no rise
        // exceeds the player's step height.
        float tIslet = tA - isletS;
        float tMain = tB + mainS;
        float span = tMain - tIslet;
        int minCount = Mathf.CeilToInt(Mathf.Abs(mainTop - isletTop) / PathMaxRise - 0.001f);
        int count = Mathf.Max(Mathf.CeilToInt(span / PathMaxSpacing), minCount);
        if (span < PathMinSpacing || count > Mathf.FloorToInt(span / PathMinSpacing))
        {
            return plan;
        }

        for (int k = count; k >= 0; k--)
        {
            float y = Mathf.Max(top0, Mathf.Max(isletTop - PathMaxRise * k, mainTop - PathMaxRise * (count - k)));
            Vector2 p = islet + dir * (tIslet + span * k / count);
            plan.stones.Add(new Vector3(p.x, y, p.y));
            if (y > top0 + 0.02f)
            {
                plan.bankSteps++;
            }

            float clearance = y - Mathf.Max(PathGround(stage, p), stage.waterY);
            plan.maxClearance = Mathf.Max(plan.maxClearance, clearance);
            if (clearance > HighClearance)
            {
                plan.highStones++;
            }
        }

        plan.valid = true;
        plan.score = plan.bankSteps + 0.01f * Mathf.Abs(offset) + 2f * plan.maxClearance + 0.5f * plan.highStones;
        return plan;
    }

    // Bisects between a dry and a wet sample for the point where the ground crosses the edge height.
    static float RefineEdge(Stage stage, Vector2 origin, Vector2 dir, float a, float b, float edgeY)
    {
        bool aboveA = PathGround(stage, origin + dir * a) >= edgeY;
        for (int i = 0; i < 8; i++)
        {
            float mid = (a + b) * 0.5f;
            bool aboveMid = PathGround(stage, origin + dir * mid) >= edgeY;
            if (aboveMid == aboveA)
            {
                a = mid;
            }
            else
            {
                b = mid;
            }
        }

        return (a + b) * 0.5f;
    }

    static float BankSlope(Stage stage, Vector2 start, Vector2 inland, float from, float to)
    {
        float worst = 0f;
        for (float t = from; t < to; t += 0.3f)
        {
            float rise = PathGround(stage, start + inland * (t + 0.3f)) - PathGround(stage, start + inland * t);
            worst = Mathf.Max(worst, rise / 0.3f);
        }

        return worst;
    }

    // Finds where a bank meets walkable ground: the first spot inland of the water edge `start` that is
    // dry and has a gentle slope ahead. `s` is the inland distance of the last stone (negative = out over
    // the water) and `top` its top height. A bank that rises no more than the level stones needs none, so
    // the landing stone is the level stone at the water edge; a taller bank gets a stone as high as the
    // landing ground, set just outside a sheer wall or 0.3 m short of a slope.
    static bool PlanLanding(Stage stage, Vector2 start, Vector2 inland, float top0, out float s, out float top)
    {
        s = 0f;
        top = top0;
        float edgeY = stage.highWaterY + 0.05f;
        float landing = -1f;
        for (float t = 0f; t <= 25f; t += 0.1f)
        {
            if (PathGround(stage, start + inland * t) >= edgeY + 0.1f && BankSlope(stage, start, inland, t, t + 3f) <= GentleSlope)
            {
                landing = t;
                break;
            }
        }

        if (landing < 0f)
        {
            return false;
        }

        float height = PathGround(stage, start + inland * landing);
        if (height - top0 <= 0.1f)
        {
            return true;
        }

        top = height;
        s = landing - 0.3f;
        while (s > -30f && PathGround(stage, start + inland * s) > height + 0.05f)
        {
            s -= 0.05f;
        }

        return true;
    }

    static void BuildPath(ArtKit art, Stage stage, Transform parent, PathPlan plan)
    {
        float edgeY = stage.highWaterY + 0.05f;
        float top0 = stage.highWaterY + 0.12f + StoneHalfHeight;
        for (int i = 0; i < plan.stones.Count; i++)
        {
            // Stones over open water at the base level are revealed by the lantern. The two end stones, any
            // stone over dry ground and any stone raised above the base level (mesa landings) are always
            // visible and solid, so the landings never depend on light and nobody drops onto a hidden stone.
            Vector3 stone = plan.stones[i];
            bool end = i == 0 || i == plan.stones.Count - 1;
            bool raised = stone.y > top0 + 0.02f;
            if (!end && !raised && PathGround(stage, new Vector2(stone.x, stone.z)) < edgeY)
            {
                CreateHiddenStone(art, stage, parent, new Vector3(stone.x, stone.y - StoneHalfHeight, stone.z));
            }
            else
            {
                CreateBankStep(art, stage, parent, stone);
            }
        }
    }

    static GameObject CreateStoneBody(Material material, Transform parent, string name, Vector3 center, float halfHeight)
    {
        GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stone.name = name;
        stone.transform.SetParent(parent, true);
        stone.transform.position = center;
        stone.transform.localScale = new Vector3(1.15f, halfHeight, 1.15f);
        stone.GetComponent<Renderer>().sharedMaterial = material;

        // The primitive's capsule collider would be a ball at this scale (top 0.5 m above the visible disc).
        // A flat box on the disc lets the player walk across. The cylinder mesh is radius 0.5, height 2, so a
        // local 0.9 x 2 x 0.9 box keeps its corners inside the disc and its top on the visible top.
        Object.DestroyImmediate(stone.GetComponent<Collider>());
        BoxCollider flat = stone.AddComponent<BoxCollider>();
        flat.center = Vector3.zero;
        flat.size = new Vector3(0.9f, 2f, 0.9f);
        return stone;
    }

    static void CreateHiddenStone(ArtKit art, Stage stage, Transform parent, Vector3 pos)
    {
        GameObject stone = CreateStoneBody(art.path, parent, "SteppingStone", pos, StoneHalfHeight);
        stone.AddComponent<LightRevealed>();
        stage.stones++;
        if (pos.y > stage.highWaterY + 0.13f)
        {
            return;
        }

        GameObject foam = GameObject.CreatePrimitive(PrimitiveType.Quad);
        foam.name = "StoneFoam";
        foam.transform.SetParent(parent, true);
        foam.transform.position = pos + new Vector3(0f, 0.06f, 0f);
        foam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        foam.transform.localScale = new Vector3(2.35f, 2.35f, 1f);
        foam.GetComponent<Renderer>().sharedMaterial = art.foam;
        foam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        StripColliders(foam);
        foam.AddComponent<LightRevealed>();
    }

    // Always visible and solid: plain lit stone (the path material fades out by lantern distance on its own)
    // and no LightRevealed. A thicker slab sunk into the bank so it reads as part of it. A step hanging more
    // than SupportGap above whatever is beneath it gets a visual-only rock pillar down to the terrain or the
    // water line.
    static void CreateBankStep(ArtKit art, Stage stage, Transform parent, Vector3 stepTop)
    {
        Vector3 center = new Vector3(stepTop.x, stepTop.y - BankHalfHeight, stepTop.z);
        CreateStoneBody(art.bankStone, parent, "BankStep", center, BankHalfHeight);

        float underside = stepTop.y - 2f * BankHalfHeight;
        float beneath = Mathf.Max(PathGround(stage, new Vector2(stepTop.x, stepTop.z)), stage.waterY);
        if (underside - beneath > SupportGap)
        {
            CreateBankSupport(art, parent, new Vector3(stepTop.x, beneath - 0.25f, stepTop.z), underside + 0.05f);
        }
    }

    // A tapered pillar built from stacked cylinders, wider at the base. Visual only: all colliders removed.
    static void CreateBankSupport(ArtKit art, Transform parent, Vector3 bottom, float topY)
    {
        float height = topY - bottom.y;
        int segments = Mathf.Max(1, Mathf.CeilToInt(height / 1.2f));
        float segment = height / segments;
        GameObject pillar = new GameObject("BankSupport");
        pillar.transform.SetParent(parent, true);
        pillar.transform.position = new Vector3(bottom.x, bottom.y, bottom.z);
        for (int i = 0; i < segments; i++)
        {
            float radius = Mathf.Lerp(0.6f, 0.34f, (i + 0.5f) / segments);
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = "Rock";
            part.transform.SetParent(pillar.transform, false);
            part.transform.position = new Vector3(bottom.x, bottom.y + segment * (i + 0.5f), bottom.z);
            part.transform.rotation = Quaternion.Euler(0f, (i * 47 + Mathf.RoundToInt(bottom.x * 13f)) % 360, 0f);
            part.transform.localScale = new Vector3(radius * 2f, segment * 0.5f + 0.04f, radius * 2f);
            part.GetComponent<Renderer>().sharedMaterial = art.rock;
        }

        StripColliders(pillar);
    }
}
}
