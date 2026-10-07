using System;
using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
// The stalker of Island 4. It hunts in the dark, freezes in the lantern light and steals fuel on touch.
public class Shade : MonoBehaviour
{
    public static readonly List<Shade> All = new List<Shade>();
    public static event Action<float> Stole;

    [SerializeField] float fadeSeconds = 0.8f;
    [SerializeField] float dissolveSeconds = 0.6f;
    [SerializeField] float turnSpeed = 6f;
    [SerializeField] float avoidDistance = 2.4f;
    [SerializeField] float avoidRadius = 0.4f;
    // Terrain only counts as a wall when the ground ~1.5 m ahead rises more than this above the ground here.
    [SerializeField] float wallRise = 1.2f;
    [SerializeField] float bodyAlpha = 0.92f;
    [SerializeField] float droneVolume = 0.5f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static bool loggedFallback;
    const float WallLookAhead = 1.5f;
    const float StuckMinPlayerDistance = 3f;

    ShadeState state = ShadeState.Chase;
    float frozenSeconds;
    float stunTimer;
    float reformTimer;
    float appear;
    bool despawning;
    bool dissolving;
    bool subscribed;
    bool lightningSubscribed;
    bool targetsResolved;
    bool maskReady;
    bool droneRouted;
    float droneGain;
    AudioSource drone;
    int obstacleMask;
    Transform player;
    Lantern lantern;
    WaterHazard hazard;
    Renderer body;
    Renderer[] eyes;
    ParticleSystem smoke;
    MaterialPropertyBlock block;
    Vector3 baseScale;
    readonly ShadeSteering steer = new ShadeSteering();

    public ShadeState State => state;
    public bool Stunned => state == ShadeState.Stunned;
    public bool IsDespawning => despawning;

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
        steer.Reset(transform.forward);
        Transform bodyTransform = transform.Find("Body");
        body = bodyTransform != null ? bodyTransform.GetComponent<Renderer>() : null;
        Transform eyeRoot = transform.Find("Eyes");
        eyes = eyeRoot != null ? eyeRoot.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        Transform smokeTransform = transform.Find("Smoke");
        smoke = smokeTransform != null ? smokeTransform.GetComponent<ParticleSystem>() : null;
        if (smoke != null)
        {
            ParticleQuality.ApplyTo(smoke);
        }

        // A 3D hum that follows the Shade. It goes quiet while the Shade is frozen in the lantern light.
        drone = StormAudio.Make(gameObject, ProceduralAudio.ShadeDrone(), true, 1f);
        drone.Play();
        block = new MaterialPropertyBlock();
        appear = 0f;
        ApplyLook();
    }

    void OnEnable()
    {
        if (!All.Contains(this))
        {
            All.Add(this);
        }

        Subscribe();
        if (!lightningSubscribed)
        {
            lightningSubscribed = true;
            Lightning.Flashed += OnLightning;
        }
    }

    void OnDisable()
    {
        All.Remove(this);
        Unsubscribe();
        UnsubscribeLightning();
    }

    void OnDestroy()
    {
        Unsubscribe();
        UnsubscribeLightning();
    }

    void UnsubscribeLightning()
    {
        if (!lightningSubscribed)
        {
            return;
        }

        lightningSubscribed = false;
        Lightning.Flashed -= OnLightning;
    }

    void OnLightning()
    {
        Stun(ShadeLogic.StunSeconds);
    }

    void Start()
    {
        ResolveTargets();
        Subscribe();
    }

    public void Bind(Transform playerBody, Lantern playerLantern, WaterHazard waterHazard)
    {
        if (playerBody != null)
        {
            player = playerBody;
        }

        if (playerLantern != null)
        {
            lantern = playerLantern;
        }

        hazard = waterHazard;
        if (player != null && lantern != null)
        {
            targetsResolved = true;
        }
    }

    public void Stun(float seconds)
    {
        if (despawning || state == ShadeState.Reforming || seconds <= 0f)
        {
            return;
        }

        stunTimer = Mathf.Max(stunTimer, seconds);
        state = ShadeState.Stunned;
    }

    public void BeginDespawn()
    {
        if (despawning)
        {
            return;
        }

        despawning = true;
        dissolving = false;
    }

    void Subscribe()
    {
        if (subscribed || GameManager.Instance == null)
        {
            return;
        }

        subscribed = true;
        GameManager.Instance.WonGame += OnRoundEnded;
        GameManager.Instance.LostGame += OnRoundEnded;
    }

    void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        subscribed = false;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.WonGame -= OnRoundEnded;
            GameManager.Instance.LostGame -= OnRoundEnded;
        }
    }

    void OnRoundEnded()
    {
        BeginDespawn();
    }

    void ResolveTargets()
    {
        if (targetsResolved)
        {
            return;
        }

        targetsResolved = true;
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
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

        if (hazard == null)
        {
            hazard = FindAnyObjectByType<WaterHazard>();
        }

        if ((player == null || lantern == null) && !loggedFallback)
        {
            loggedFallback = true;
            Debug.LogWarning("Shade could not resolve the lantern once.", this);
        }
    }

    void Update()
    {
        GameManager manager = GameManager.Instance;
        if (manager != null && manager.IsPaused)
        {
            return;
        }

        float dt = Time.deltaTime;
        UpdateDrone(dt);
        if (manager != null && (manager.IsRoundOver || manager.IsDying))
        {
            BeginDespawn();
        }

        if (despawning)
        {
            Fade(0f, dt);
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

        if (state == ShadeState.Reforming)
        {
            TickReform(dt);
            return;
        }

        Fade(1f, dt);
        if (stunTimer > 0f)
        {
            stunTimer -= dt;
            state = ShadeState.Stunned;
            if (stunTimer > 0f)
            {
                return;
            }

            stunTimer = 0f;
        }

        Vector3 chest = transform.position + Vector3.up;
        float reveal = RevealMath.Evaluate(chest, lantern.transform.position, lantern.Radius);
        if (reveal >= 0.5f)
        {
            frozenSeconds += dt;
        }
        else
        {
            frozenSeconds = 0f;
        }

        state = ShadeLogic.StateFor(reveal, frozenSeconds);
        Move(dt);
        TryTouch();
    }

    void UpdateDrone(float dt)
    {
        if (!droneRouted)
        {
            droneRouted = StormAudio.Route(drone, false);
        }

        float target = state == ShadeState.Freeze || state == ShadeState.Reforming || despawning ? 0f : 1f;
        droneGain = Mathf.MoveTowards(droneGain, target, dt * 4f);
        drone.volume = droneVolume * droneGain * appear * StormAudio.MuteGain;
    }

    void Move(float dt)
    {
        float speed = ShadeLogic.SpeedFor(state);
        if (speed <= 0f)
        {
            steer.TickStuck(transform.position, dt, false);
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float playerDistance = toPlayer.magnitude;
        Vector3 desired = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : transform.forward;
        if (state == ShadeState.Retreat)
        {
            Vector3 away = transform.position - lantern.transform.position;
            away.y = 0f;
            desired = away.sqrMagnitude > 0.001f ? away.normalized : -desired;
        }

        desired = AvoidBeacons(desired);
        desired = AvoidObstacles(desired, dt);
        desired.y = 0f;
        if (desired.sqrMagnitude < 0.001f)
        {
            return;
        }

        desired.Normalize();
        // Stuck: no net progress for a while while meant to be travelling. Head off sideways for a moment, then resume.
        if (steer.TickStuck(transform.position, dt, playerDistance > StuckMinPlayerDistance))
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 leftDir = Quaternion.AngleAxis(-ShadeSteering.EscapeDegrees, Vector3.up) * desired;
            Vector3 rightDir = Quaternion.AngleAxis(ShadeSteering.EscapeDegrees, Vector3.up) * desired;
            steer.BeginEscape(desired, !PathBlocked(origin, leftDir, avoidDistance), !PathBlocked(origin, rightDir, avoidDistance));
        }

        if (steer.Escaping)
        {
            desired = steer.EscapeDirection;
        }

        // Turn toward the wanted direction at a limited rate instead of snapping, so balanced pulls do not jitter.
        Vector3 heading = steer.Turn(desired, dt);
        Vector3 next = transform.position + heading * (speed * dt);
        next = PushOutOfSafe(next);
        float ground;
        Vector3 normal;
        // Off the terrain counts as a wall: stay put and let the stuck escape turn the Shade around.
        if (!TerrainQuery.TrySample(next, out ground, out normal))
        {
            return;
        }

        // Glide over water: hover above whichever is higher, the bed or the current water surface.
        next.y = Mathf.Max(ground, WaterHazard.SurfaceY) + ShadeLogic.HoverHeight;
        transform.position = next;
        Quaternion look = Quaternion.LookRotation(heading, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Mathf.Clamp01(turnSpeed * dt));
    }

    void TryTouch()
    {
        float distance = Vector3.Distance(transform.position, player.position);
        float sinceRescue = hazard != null ? Time.time - hazard.LastRescueTime : 1000f;
        if (!ShadeLogic.CanSteal(state, distance, sinceRescue))
        {
            return;
        }

        if (hazard != null && hazard.IsRescuing)
        {
            return;
        }

        if (lantern.IsDepleted || InsideSafe(player.position))
        {
            return;
        }

        float amount = ShadeLogic.StealAmount(lantern.Fuel, GameSettings.ShadeSteal());
        if (amount <= 0f)
        {
            return;
        }

        if (lantern.TrySpend(amount))
        {
            Action<float> handler = Stole;
            if (handler != null)
            {
                handler(amount);
            }
        }

        // Dissolve where it stands, then re-form at the dark edge.
        state = ShadeState.Reforming;
        dissolving = true;
        reformTimer = ShadeLogic.ReformSeconds;
        frozenSeconds = 0f;
    }

    void TickReform(float dt)
    {
        if (dissolving)
        {
            Fade(0f, dt, dissolveSeconds);
            if (appear <= 0.001f)
            {
                dissolving = false;
            }
        }

        reformTimer -= dt;
        if (reformTimer > 0f)
        {
            return;
        }

        Vector3 spot;
        ShadeSpawner spawner = ShadeSpawner.Instance;
        if (spawner == null || !spawner.TryFindEdge(out spot))
        {
            reformTimer = 1f;
            return;
        }

        transform.position = spot;
        frozenSeconds = 0f;
        state = ShadeState.Chase;
    }

    void Fade(float target, float dt)
    {
        Fade(target, dt, fadeSeconds);
    }

    void Fade(float target, float dt, float seconds)
    {
        appear = Mathf.MoveTowards(appear, target, dt / Mathf.Max(0.05f, seconds));
        ApplyLook();
    }

    void ApplyLook()
    {
        if (body != null)
        {
            // Stunned by lightning: a pale glow so the frozen Shade reads clearly.
            Color tint = Stunned ? new Color(0.45f, 0.6f, 0.95f, 1f) : new Color(0.02f, 0.02f, 0.04f, 1f);
            tint.a = bodyAlpha * appear;
            block.SetColor(BaseColorId, tint);
            body.SetPropertyBlock(block);
            body.enabled = appear > 0.01f;
        }

        bool eyesOn = appear > 0.5f;
        for (int i = 0; i < eyes.Length; i++)
        {
            if (eyes[i] != null)
            {
                eyes[i].enabled = eyesOn;
            }
        }

        if (smoke != null)
        {
            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.enabled = appear > 0.05f;
            if (appear > 0.05f && !smoke.isPlaying)
            {
                smoke.Play();
            }
        }

        transform.localScale = baseScale;
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

    Vector3 AvoidObstacles(Vector3 desired, float dt)
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
        Vector3 origin = transform.position + Vector3.up * 1f;
        if (!Physics.SphereCast(origin, avoidRadius, flat, out hit, avoidDistance, ObstacleMask(), QueryTriggerInteraction.Ignore)
            || !IsWall(hit, flat))
        {
            steer.NoHit(dt);
            return desired;
        }

        // Keep going round the same side. Only switch when that side is blocked as well.
        Vector3 probe = steer.ProbeDirection(flat, hit.normal);
        bool sideBlocked = PathBlocked(origin, probe, avoidDistance * 0.6f);
        return steer.Avoid(flat, hit.normal, sideBlocked, dt);
    }

    // True when something solid blocks the way within distance along dir.
    bool PathBlocked(Vector3 origin, Vector3 dir, float distance)
    {
        // Off the terrain is as blocked as a wall: Move refuses to step there.
        float edgeGround;
        Vector3 edgeNormal;
        if (!TerrainQuery.TrySample(transform.position + dir * distance, out edgeGround, out edgeNormal))
        {
            return true;
        }

        RaycastHit hit;
        if (!Physics.SphereCast(origin, avoidRadius, dir, out hit, distance, ObstacleMask(), QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return IsWall(hit, dir);
    }

    // Props count as walls unless they are floor-like. Terrain only counts when the ground really rises steeply ahead,
    // since a hovering Shade glides up gentle slopes.
    bool IsWall(RaycastHit hit, Vector3 dir)
    {
        if (IgnoreObstacle(hit.collider))
        {
            return false;
        }

        if (hit.collider is TerrainCollider)
        {
            return TerrainRisesAhead(dir);
        }

        return hit.normal.y <= 0.65f;
    }

    bool TerrainRisesAhead(Vector3 dir)
    {
        float here;
        float ahead;
        Vector3 normal;
        Vector3 position = transform.position;
        if (!TerrainQuery.TrySample(position, out here, out normal))
        {
            return true;
        }

        if (!TerrainQuery.TrySample(position + dir * WallLookAhead, out ahead, out normal))
        {
            return true;
        }

        return ahead - here > wallRise;
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
            rim.y = position.y;
            position = rim;
        }

        return position;
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

    bool IgnoreObstacle(Collider collider)
    {
        if (collider == null || collider.isTrigger)
        {
            return true;
        }

        if (collider.GetComponentInParent<Shade>() != null || collider.GetComponentInParent<Moth>() != null)
        {
            return true;
        }

        Transform root = collider.transform.root;
        if (root != null && root.CompareTag("Player"))
        {
            return true;
        }

        return collider.CompareTag("Player");
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
}
}
