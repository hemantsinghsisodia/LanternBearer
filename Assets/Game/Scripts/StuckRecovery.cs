using UnityEngine;

namespace LanternKeeper
{
// Safety net on the keeper (PlayerController adds it at runtime, so every island has it): if move input has been held for more than
// HeldSeconds, the keeper has not moved, and the capsule is actually inside a solid (not just leaning on a wall), it is freed. A
// convex collider is depenetrated with Physics.ComputePenetration; anything else (a non-convex mesh, or a push that did not work)
// moves the keeper to the nearest walkable point: on Island 5 the nearest SpireLayout path point, elsewhere the last safe position.
// The check only runs when those three conditions hold, at most once per HeldSeconds, and allocates nothing.
[DisallowMultipleComponent]
public class StuckRecovery : MonoBehaviour
{
    public const float HeldSeconds = 1.5f;
    public const float MovedMetres = 0.35f;
    const float MinDepth = 0.03f;
    const float FootLift = 0.15f;
    const float EmbedRadiusFactor = 0.9f;
    const float MaxPush = 1.5f;

    static readonly Collider[] hits = new Collider[16];

    CharacterController controller;
    PlayerController player;
    Vector3 anchor;
    float heldFor;

    public int Recoveries { get; private set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        player = GetComponent<PlayerController>();
        anchor = transform.position;
    }

    void LateUpdate()
    {
        if (controller == null || player == null)
        {
            return;
        }

        if (!player.MoveInputHeld || !controller.enabled)
        {
            heldFor = 0f;
            anchor = transform.position;
            return;
        }

        heldFor += Time.deltaTime;
        if ((transform.position - anchor).sqrMagnitude > MovedMetres * MovedMetres)
        {
            heldFor = 0f;
            anchor = transform.position;
            return;
        }

        if (heldFor < HeldSeconds)
        {
            return;
        }

        // Held, not moving: look once, then wait another HeldSeconds before looking again.
        heldFor = 0f;
        anchor = transform.position;
        if (Embedded())
        {
            Free();
        }
    }

    // Capsule of the controller (feet lifted so the ground never counts), slightly slimmer so a wall the controller merely leans on
    // (it rests a skin width off) is not an overlap. Returns the number of non-trigger colliders it overlaps, in `hits`.
    int Overlaps(float radiusFactor)
    {
        return Overlaps(transform.position, radiusFactor);
    }

    int Overlaps(Vector3 position, float radiusFactor)
    {
        float radius = controller.radius * radiusFactor;
        Vector3 centre = position + transform.TransformVector(controller.center);
        float half = controller.height * 0.5f;
        Vector3 bottom = centre + Vector3.down * half + Vector3.up * (radius + FootLift);
        Vector3 top = centre + Vector3.up * half - Vector3.up * radius;
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, hits, ~0, QueryTriggerInteraction.Ignore);
        int solid = 0;
        for (int i = 0; i < count; i++)
        {
            Collider other = hits[i];
            if (other == null || other == controller || other.transform.IsChildOf(transform))
            {
                continue;
            }

            hits[solid++] = other;
        }

        return solid;
    }

    bool Embedded()
    {
        return Overlaps(EmbedRadiusFactor) > 0;
    }

    void Free()
    {
        int count = Overlaps(EmbedRadiusFactor);
        Vector3 push = Vector3.zero;
        bool needsFallback = false;
        for (int i = 0; i < count; i++)
        {
            Collider other = hits[i];
            MeshCollider mesh = other as MeshCollider;
            if (mesh != null && !mesh.convex)
            {
                needsFallback = true;
                continue;
            }

            Vector3 direction;
            float depth;
            if (Physics.ComputePenetration(controller, transform.position, transform.rotation, other, other.transform.position, other.transform.rotation, out direction, out depth) && depth > MinDepth)
            {
                push += direction * (depth + 0.02f);
            }
            else
            {
                needsFallback = true;
            }
        }

        if (!needsFallback && push.sqrMagnitude > 0.0004f)
        {
            if (push.magnitude > MaxPush)
            {
                push = push.normalized * MaxPush;
            }

            Teleport(transform.position + push);
            if (!Embedded())
            {
                Recoveries++;
                Debug.Log("StuckRecovery: pushed the keeper out of a solid at " + transform.position.ToString("F1"));
                return;
            }
        }

        Vector3 safe;
        if (FindWalkable(out safe))
        {
            Teleport(safe);
            Recoveries++;
            Debug.Log("StuckRecovery: moved the stuck keeper to the nearest walkable point " + transform.position.ToString("F1"));
        }
    }

    void Teleport(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
        player.ClearKnock();
        Physics.SyncTransforms();
        anchor = position;
    }

    // Island 5: the nearest SpireLayout path point at about the keeper's height, outside the gap spans. Elsewhere: the last safe position.
    bool FindWalkable(out Vector3 result)
    {
        result = player.LastSafePosition;
        Vector3 pathPoint;
        if (FindAnyObjectByType<GreatLamp>() != null && FindSpirePoint(out pathPoint))
        {
            result = pathPoint;
            return true;
        }

        Vector3 near;
        if (FindFreeSpotNear(out near))
        {
            result = near;
        }

        return true;
    }

    // The nearest path point within PathReach metres at about the keeper's height, outside the gap spans.
    bool FindSpirePoint(out Vector3 result)
    {
        const float PathReach = 12f;
        result = transform.position;
        Vector2 here = new Vector2(transform.position.x, transform.position.z);
        float bestSqr = float.MaxValue;
        bool found = false;
        const int steps = 600;
        for (int i = 0; i <= steps; i++)
        {
            float u = i / (float)steps;
            if (InGap(u))
            {
                continue;
            }

            Vector2 point = SpireLayout.PathPoint(u);
            float sqr = (point - here).sqrMagnitude;
            if (sqr >= bestSqr)
            {
                continue;
            }

            float height;
            Vector3 normal;
            if (!TerrainQuery.TrySample(new Vector3(point.x, 0f, point.y), out height, out normal) || Mathf.Abs(height - transform.position.y) > 5f)
            {
                continue;
            }

            bestSqr = sqr;
            result = new Vector3(point.x, height + 0.1f, point.y);
            found = true;
        }

        return found && bestSqr <= PathReach * PathReach;
    }

    // The nearest spot on firm ground within a few metres whose capsule touches nothing: rings of 16 directions, 0.6 m apart.
    bool FindFreeSpotNear(out Vector3 result)
    {
        result = transform.position;
        for (int ring = 1; ring <= 5; ring++)
        {
            float distance = 0.6f * ring;
            for (int k = 0; k < 16; k++)
            {
                float angle = k * Mathf.PI * 2f / 16f;
                Vector3 probe = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                RaycastHit ground;
                if (!Physics.Raycast(probe + Vector3.up * 1.2f, Vector3.down, out ground, 3f, ~0, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.7f)
                {
                    continue;
                }

                Vector3 spot = ground.point + Vector3.up * 0.05f;
                if (Overlaps(spot, EmbedRadiusFactor) == 0)
                {
                    result = spot;
                    return true;
                }
            }
        }

        return false;
    }

    static bool InGap(float u)
    {
        for (int g = 0; g < 4; g++)
        {
            Vector2 span = SpireLayout.GapSpanU(g);
            if (u > span.x && u < span.y)
            {
                return true;
            }
        }

        return false;
    }
}
}
