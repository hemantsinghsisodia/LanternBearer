using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
public class Moth : MonoBehaviour
{
    public static readonly List<Moth> All = new List<Moth>();
    public static float LowestClearance = float.PositiveInfinity;
    public static int SolidOverlapFrames;
    public static int SampledFrames;

    [SerializeField] float flySpeed = 7.2f;
    [SerializeField] float closeDistance = 1.5f;
    [SerializeField] float closeDrain = 1.5f;
    [SerializeField] float fleeSeconds = 3.2f;
    [SerializeField] float terrainClearance = 1.4f;
    [SerializeField] float fadeSeconds = 1f;
    [SerializeField] float avoidDistance = 2.4f;
    [SerializeField] float avoidRadius = 0.4f;

    enum Mode
    {
        Hunt,
        Flee
    }

    Mode mode;
    Vector3 velocity;
    Vector3 baseScale;
    float sprintTimer;
    float fleeTimer;
    float appear;
    bool despawning;
    Transform player;
    PlayerController body;
    Lantern lantern;
    Transform wings;
    AudioSource flutter;
    Light glow;
    int obstacleMask;
    bool maskReady;

    public bool IsDespawning => despawning;
    public float Appear => appear;

    public static void ResetStats()
    {
        LowestClearance = float.PositiveInfinity;
        SolidOverlapFrames = 0;
        SampledFrames = 0;
    }

    public static int LivingCount()
    {
        int count = 0;
        for (int i = 0; i < All.Count; i++)
        {
            if (All[i] != null && !All[i].despawning)
            {
                count++;
            }
        }

        return count;
    }

    void Awake()
    {
        baseScale = transform.localScale;
        if (baseScale.sqrMagnitude < 0.0001f)
        {
            baseScale = Vector3.one * 0.35f;
        }

        wings = transform.Find("Wings");
        flutter = GetComponent<AudioSource>();
        appear = 0f;
        transform.localScale = baseScale * 0.05f;
        EnsureGlow();
    }

    void OnEnable()
    {
        if (!All.Contains(this))
        {
            All.Add(this);
        }
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Start()
    {
        FindTargets();
        if (AudioManager.Instance != null && flutter != null)
        {
            AudioManager.Instance.PlayLoop(flutter, ProceduralAudio.MothFlutter(), 0.18f);
        }
    }

    void OnDestroy()
    {
        if (lantern != null)
        {
            lantern.SetDrainModifier(this, 1f);
        }
    }

    void Update()
    {
        Simulate(Time.deltaTime);
    }

    public void Simulate(float dt)
    {
        if (dt < 0f)
        {
            dt = 0f;
        }

        FindTargets();
        bool ending = GameManager.Instance != null && (GameManager.Instance.IsRoundOver || GameManager.Instance.IsDying);
        if (ending && !despawning)
        {
            BeginDespawn();
        }

        TickFade(dt);
        if (despawning)
        {
            SetClose(false);
            if (appear <= 0.001f)
            {
                Destroy(gameObject);
            }

            return;
        }

        if (player == null || lantern == null)
        {
            return;
        }

        if (mode == Mode.Hunt)
        {
            Hunt(dt);
        }
        else
        {
            Flee(dt);
        }

        EnforceHeight();
        transform.position = PushOutOfSafe(transform.position);
        RecordProbe();

        if (wings != null)
        {
            float flap = Mathf.Sin(Time.time * 42f) * 32f * appear;
            wings.localRotation = Quaternion.Euler(flap, 0f, 0f);
        }
    }

    public void BeginDespawn()
    {
        if (despawning)
        {
            return;
        }

        despawning = true;
        SetClose(false);
    }

    void TickFade(float dt)
    {
        float target = despawning ? 0f : 1f;
        float speed = 1f / Mathf.Max(0.05f, fadeSeconds);
        appear = Mathf.MoveTowards(appear, target, speed * dt);
        transform.localScale = baseScale * Mathf.Max(0.02f, appear);
        if (glow != null)
        {
            glow.intensity = 1.1f * appear;
            glow.enabled = appear > 0.04f;
        }

        if (flutter != null)
        {
            flutter.volume = 0.18f * appear;
        }
    }

    void EnsureGlow()
    {
        Transform existing = transform.Find("Glow");
        if (existing != null)
        {
            glow = existing.GetComponent<Light>();
        }

        if (glow != null)
        {
            return;
        }

        GameObject lightObject = new GameObject("Glow");
        lightObject.transform.SetParent(transform, false);
        glow = lightObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.72f, 0.78f, 1f);
        glow.range = 2.4f;
        glow.intensity = 0f;
        glow.shadows = LightShadows.None;
    }

    void FindTargets()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                body = playerObject.GetComponent<PlayerController>();
            }
        }

        if (lantern == null && player != null)
        {
            lantern = player.GetComponentInChildren<Lantern>();
        }

        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }
    }

    void Hunt(float dt)
    {
        Vector3 target = lantern.transform.position;
        Vector3 to = target - transform.position;
        float distance = to.magnitude;
        Vector3 desired = to.sqrMagnitude > 0.001f ? to.normalized : Vector3.forward;
        float noise = Mathf.PerlinNoise(transform.position.x * 0.4f, Time.time * 0.8f) - 0.5f;
        float noise2 = Mathf.PerlinNoise(transform.position.z * 0.4f, Time.time * 0.8f + 3f) - 0.5f;
        desired += new Vector3(noise, noise2 * 0.25f, noise2);
        desired = AvoidBeacons(desired);
        desired = AvoidObstacles(desired);
        Commit(desired, flySpeed, dt);

        bool close = distance <= closeDistance && !InsideSafe(transform.position);
        SetClose(close);

        bool sprintingAway = body != null && body.IsSprinting && body.HorizontalSpeed > flySpeed;
        if (sprintingAway)
        {
            sprintTimer += dt;
        }
        else
        {
            sprintTimer = 0f;
        }

        if (sprintTimer >= 1f)
        {
            mode = Mode.Flee;
            fleeTimer = fleeSeconds;
            sprintTimer = 0f;
            SetClose(false);
        }
    }

    void Flee(float dt)
    {
        fleeTimer -= dt;
        Vector3 away = transform.position - player.position;
        away.y = 0.35f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = Vector3.forward;
        }

        Vector3 desired = AvoidObstacles(away.normalized);
        Commit(desired, flySpeed + 2.5f, dt);
        SetClose(false);
        if (fleeTimer <= 0f)
        {
            mode = Mode.Hunt;
        }
    }

    void Commit(Vector3 desired, float speed, float dt)
    {
        if (desired.sqrMagnitude > 0.001f)
        {
            desired.Normalize();
        }

        velocity = Vector3.Lerp(velocity, desired * speed, 2.4f * Mathf.Max(dt, 0.01f));
        Vector3 next = transform.position + velocity * dt;
        next.y = Mathf.Max(next.y, MinHeight(next));
        next = PushOutOfSafe(next);
        transform.position = next;
    }

    Vector3 AvoidBeacons(Vector3 desired)
    {
        for (int i = 0; i < Beacon.All.Count; i++)
        {
            Beacon beacon = Beacon.All[i];
            if (beacon == null || !beacon.IsLit)
            {
                continue;
            }

            Vector3 away = transform.position - beacon.transform.position;
            away.y = 0f;
            float dist = away.magnitude;
            float radius = beacon.ZoneRadius;
            if (dist < radius + 2.5f && dist > 0.01f)
            {
                float push = 1f - Mathf.Clamp01((dist - radius) / 2.5f);
                desired += away.normalized * (3.2f * push);
            }
        }

        return desired;
    }

    Vector3 AvoidObstacles(Vector3 desired)
    {
        Vector3 flat = desired;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.001f)
        {
            flat = transform.forward;
            flat.y = 0f;
        }

        if (flat.sqrMagnitude < 0.001f)
        {
            return desired;
        }

        flat.Normalize();
        RaycastHit hit;
        if (!Physics.SphereCast(transform.position, avoidRadius, flat, out hit, avoidDistance, ObstacleMask(), QueryTriggerInteraction.Ignore))
        {
            return desired;
        }

        if (IgnoreObstacle(hit.collider) || hit.normal.y > 0.65f)
        {
            return desired;
        }

        Vector3 side = Vector3.Cross(Vector3.up, hit.normal);
        if (side.sqrMagnitude < 0.01f)
        {
            side = Vector3.Cross(Vector3.up, flat);
        }

        if (Vector3.Dot(side, flat) < 0f)
        {
            side = -side;
        }

        return side.normalized + Vector3.up * 0.45f + hit.normal * 0.35f;
    }

    Vector3 PushOutOfSafe(Vector3 position)
    {
        for (int i = 0; i < Beacon.All.Count; i++)
        {
            Beacon beacon = Beacon.All[i];
            if (beacon == null || !beacon.BlocksMoth(position))
            {
                continue;
            }

            Vector3 away = position - beacon.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
            {
                away = Vector3.forward;
            }

            Vector3 rim = beacon.transform.position + away.normalized * (beacon.ZoneRadius + 0.4f);
            rim.y = Mathf.Max(position.y, MinHeight(rim));
            position = rim;
        }

        return position;
    }

    void EnforceHeight()
    {
        float floor = MinHeight(transform.position);
        if (transform.position.y < floor)
        {
            Vector3 lifted = transform.position;
            lifted.y = floor;
            transform.position = lifted;
        }
    }

    float MinHeight(Vector3 world)
    {
        float terrain = TerrainQuery.Height(world, world.y - terrainClearance);
        float aboveTerrain = terrain + terrainClearance;
        float aboveWater = WaterHazard.SurfaceY + terrainClearance;
        return Mathf.Max(aboveTerrain, aboveWater);
    }

    void RecordProbe()
    {
        if (appear < 0.85f)
        {
            return;
        }

        float terrain = TerrainQuery.Height(transform.position, transform.position.y);
        float clearance = transform.position.y - terrain;
        if (clearance < LowestClearance)
        {
            LowestClearance = clearance;
        }

        SampledFrames++;
        if (InsideSolid())
        {
            SolidOverlapFrames++;
        }
    }

    bool InsideSolid()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 0.22f, ObstacleMask(), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!IgnoreObstacle(hits[i]) && !(hits[i] is TerrainCollider))
            {
                return true;
            }
        }

        return false;
    }

    bool IgnoreObstacle(Collider collider)
    {
        if (collider == null || collider.isTrigger)
        {
            return true;
        }

        if (collider.GetComponentInParent<Moth>() != null)
        {
            return true;
        }

        Transform root = collider.transform.root;
        if (root != null && root.CompareTag("Player"))
        {
            return true;
        }

        if (collider.CompareTag("Player"))
        {
            return true;
        }

        return false;
    }

    int ObstacleMask()
    {
        if (maskReady)
        {
            return obstacleMask;
        }

        obstacleMask = ~0;
        int water = LayerMask.NameToLayer("Water");
        int ignore = LayerMask.NameToLayer("Ignore Raycast");
        int ui = LayerMask.NameToLayer("UI");
        if (water >= 0)
        {
            obstacleMask &= ~(1 << water);
        }

        if (ignore >= 0)
        {
            obstacleMask &= ~(1 << ignore);
        }

        if (ui >= 0)
        {
            obstacleMask &= ~(1 << ui);
        }

        maskReady = true;
        return obstacleMask;
    }

    bool InsideSafe(Vector3 worldPosition)
    {
        for (int i = 0; i < Beacon.All.Count; i++)
        {
            Beacon beacon = Beacon.All[i];
            if (beacon != null && beacon.BlocksMoth(worldPosition))
            {
                return true;
            }
        }

        return false;
    }

    void SetClose(bool close)
    {
        if (lantern == null)
        {
            return;
        }

        lantern.SetDrainModifier(this, close ? closeDrain : 1f);
    }
}
}
