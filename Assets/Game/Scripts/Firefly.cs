using System.Collections;
using UnityEngine;

namespace LanternKeeper
{
public class Firefly : MonoBehaviour
{
    [SerializeField] float bobHeight = 0.35f;
    [SerializeField] float bobSpeed = 2.2f;
    [SerializeField] float refillAmount = 20f;
    [SerializeField] float respawnDelay = 9f;
    [SerializeField] float islandRadius = 22f;
    [SerializeField] float hoverHeight = 1.35f;
    [SerializeField] float wanderRadius = 1.4f;
    [SerializeField] float slopeLimit = 0.85f;
    [SerializeField] float waterMargin = 0.45f;
    [SerializeField] float propClearance = 2f;
    [SerializeField] float playerBiasDistance = 20f;
    [SerializeField] float heightTolerance = 2.4f;
    [SerializeField] float sampleRing = 4f;
    [SerializeField] int pickAttempts = 28;

    Vector3 home;
    bool collected;
    bool playerResolved;
    ParticleSystem burst;
    Transform player;

    public struct HomeReport
    {
        public int samples;
        public int water;
        public int steep;
        public int prop;
        public int cliff;
        public int offIsland;
        public int invalid;
        public int fallback;
    }

    void Awake()
    {
        home = transform.position;
        Transform burstTransform = transform.Find("PickupBurst");
        if (burstTransform != null)
        {
            burst = burstTransform.GetComponent<ParticleSystem>();
        }

        if (Application.isPlaying)
        {
            ParticleQuality.ApplyTo(burst);
            RegisterGlow();
        }
    }

    void OnEnable()
    {
        if (Application.isPlaying)
        {
            RegisterGlow();
        }
    }

    void OnDisable()
    {
        Transform glow = transform.Find("Glow");
        if (glow == null)
        {
            return;
        }

        LightQuality.UnregisterFirefly(glow.GetComponent<Light>());
    }

    void RegisterGlow()
    {
        Transform glow = transform.Find("Glow");
        if (glow == null)
        {
            return;
        }

        LightQuality.RegisterFirefly(glow.GetComponent<Light>());
    }

    void Start()
    {
        respawnDelay *= GameSettings.FireflyRespawnMultiplier;
    }

    void Update()
    {
        if (collected)
        {
            return;
        }

        float time = Time.time;
        float bob = Mathf.Sin((time * bobSpeed) + home.x) * bobHeight;
        float ox = (Mathf.PerlinNoise(home.x * 0.2f, time * 0.35f) - 0.5f) * 2f * wanderRadius;
        float oz = (Mathf.PerlinNoise(home.z * 0.2f, time * 0.35f + 4f) - 0.5f) * 2f * wanderRadius;
        transform.position = new Vector3(home.x + ox, home.y + bob, home.z + oz);
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected || other == null || !other.CompareTag("Player"))
        {
            return;
        }

        Lantern lantern = other.GetComponentInChildren<Lantern>();
        if (lantern == null)
        {
            lantern = other.transform.root.GetComponentInChildren<Lantern>();
        }

        if (lantern != null)
        {
            lantern.AddFuel(refillAmount);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFirefly(transform.position);
        }

        if (burst != null)
        {
            burst.Play();
        }

        collected = true;
        StartCoroutine(RespawnLater());
    }

    IEnumerator RespawnLater()
    {
        SetShown(false);
        yield return new WaitForSeconds(respawnDelay);

        home = PickHome();
        transform.position = home;
        collected = false;
        SetShown(true);
    }

    Vector3 PickHome()
    {
        CachePlayer();
        System.Collections.Generic.List<Vector3> far = new System.Collections.Generic.List<Vector3>();
        System.Collections.Generic.List<Vector3> any = new System.Collections.Generic.List<Vector3>();
        Vector3 playerPosition = player != null ? player.position : Vector3.zero;
        for (int attempt = 0; attempt < pickAttempts; attempt++)
        {
            Vector3 hover;
            if (!TryCandidate(out hover))
            {
                continue;
            }

            any.Add(hover);
            if (player == null || FlatDistance(hover, playerPosition) >= playerBiasDistance)
            {
                far.Add(hover);
            }

            if (far.Count >= 6)
            {
                break;
            }
        }

        System.Collections.Generic.List<Vector3> pool = far.Count > 0 ? far : any;
        if (pool.Count > 0)
        {
            return ChooseFarButNotExtreme(pool, playerPosition, player != null);
        }

        Vector3 previous = home;
        if (Classify(previous) == HomeFail.None)
        {
            Debug.LogWarning("Firefly.PickHome found no new point; keeping the previous home.");
            return previous;
        }

        Vector3 searched;
        if (SearchGrid(out searched))
        {
            Debug.LogWarning("Firefly.PickHome used a grid fallback after random picks failed.");
            return searched;
        }

        Debug.LogWarning("Firefly.PickHome found nothing valid; keeping the previous home.");
        return previous;
    }

    public HomeReport AuditHomes(int samples)
    {
        HomeReport report = new HomeReport();
        report.samples = samples;
        Vector3 saved = home;
        for (int i = 0; i < samples; i++)
        {
            Vector3 before = home;
            Vector3 picked = PickHome();
            if (Almost(picked, before))
            {
                report.fallback++;
            }

            Count(Classify(picked), ref report);
            if (Classify(picked) == HomeFail.None)
            {
                home = picked;
            }
        }

        report.invalid = report.water + report.steep + report.prop + report.cliff + report.offIsland;
        home = saved;
        return report;
    }

    enum HomeFail
    {
        None,
        OffIsland,
        Water,
        Steep,
        Prop,
        Cliff
    }

    bool TryCandidate(out Vector3 hover)
    {
        hover = home;
        Vector2 spot = Random.insideUnitCircle * islandRadius;
        if (spot.magnitude < 6f)
        {
            if (spot.sqrMagnitude < 0.01f)
            {
                spot = Vector2.right;
            }

            spot = spot.normalized * Random.Range(6f, Mathf.Max(6.5f, islandRadius));
        }

        float height;
        Vector3 normal;
        Vector3 ground = new Vector3(spot.x, 0f, spot.y);
        if (!TerrainQuery.TrySample(ground, out height, out normal))
        {
            return false;
        }

        ground.y = height;
        if (ClassifyGround(ground, normal) != HomeFail.None)
        {
            return false;
        }

        hover = ground + Vector3.up * hoverHeight;
        return true;
    }

    HomeFail Classify(Vector3 hover)
    {
        Vector3 ground = hover - Vector3.up * hoverHeight;
        float height;
        Vector3 normal;
        if (!TerrainQuery.TrySample(ground, out height, out normal))
        {
            return HomeFail.OffIsland;
        }

        ground.y = height;
        return ClassifyGround(ground, normal);
    }

    HomeFail ClassifyGround(Vector3 ground, Vector3 normal)
    {
        Vector2 flat = new Vector2(ground.x, ground.z);
        if (flat.magnitude > islandRadius + 0.2f)
        {
            return HomeFail.OffIsland;
        }

        if (ground.y <= WaterHazard.SafeFloorY + waterMargin)
        {
            return HomeFail.Water;
        }

        if (normal.y < slopeLimit)
        {
            return HomeFail.Steep;
        }

        if (BlockedByProp(ground))
        {
            return HomeFail.Prop;
        }

        if (!LocallyPlayable(ground))
        {
            return HomeFail.Cliff;
        }

        return HomeFail.None;
    }

    bool LocallyPlayable(Vector3 ground)
    {
        float sum = 0f;
        int count = 0;
        int wet = 0;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 2f / 8f;
            Vector3 sample = ground + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * sampleRing;
            float height;
            Vector3 normal;
            if (!TerrainQuery.TrySample(sample, out height, out normal))
            {
                wet++;
                continue;
            }

            sum += height;
            count++;
            if (height < WaterHazard.SafeFloorY + waterMargin)
            {
                wet++;
            }
        }

        if (count == 0 || wet >= 3)
        {
            return false;
        }

        float average = sum / count;
        return Mathf.Abs(ground.y - average) <= heightTolerance;
    }

    bool BlockedByProp(Vector3 ground)
    {
        Collider[] hits = Physics.OverlapSphere(ground + Vector3.up * 0.6f, propClearance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.isTrigger || hit is TerrainCollider)
            {
                continue;
            }

            if (hit.GetComponent<Terrain>() != null)
            {
                continue;
            }

            string objectName = hit.gameObject.name;
            if (objectName == "Ground" || objectName.Contains("Water"))
            {
                continue;
            }

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            Transform root = hit.transform.root;
            if (hit.CompareTag("Player") || (root != null && root.CompareTag("Player")))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    bool SearchGrid(out Vector3 hover)
    {
        hover = home;
        float step = 3.2f;
        for (float x = -islandRadius; x <= islandRadius; x += step)
        {
            for (float z = -islandRadius; z <= islandRadius; z += step)
            {
                if (new Vector2(x, z).magnitude > islandRadius)
                {
                    continue;
                }

                float height;
                Vector3 normal;
                Vector3 ground = new Vector3(x, 0f, z);
                if (!TerrainQuery.TrySample(ground, out height, out normal))
                {
                    continue;
                }

                ground.y = height;
                if (ClassifyGround(ground, normal) != HomeFail.None)
                {
                    continue;
                }

                hover = ground + Vector3.up * hoverHeight;
                return true;
            }
        }

        return false;
    }

    Vector3 ChooseFarButNotExtreme(System.Collections.Generic.List<Vector3> pool, Vector3 playerPosition, bool hasPlayer)
    {
        if (!hasPlayer || pool.Count == 1)
        {
            return pool[0];
        }

        pool.Sort(delegate(Vector3 a, Vector3 b)
        {
            return FlatDistance(b, playerPosition).CompareTo(FlatDistance(a, playerPosition));
        });

        int index = pool.Count >= 3 ? 1 : 0;
        return pool[index];
    }

    void CachePlayer()
    {
        if (player != null || playerResolved)
        {
            return;
        }

        playerResolved = true;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    static void Count(HomeFail fail, ref HomeReport report)
    {
        if (fail == HomeFail.Water)
        {
            report.water++;
        }
        else if (fail == HomeFail.Steep)
        {
            report.steep++;
        }
        else if (fail == HomeFail.Prop)
        {
            report.prop++;
        }
        else if (fail == HomeFail.Cliff)
        {
            report.cliff++;
        }
        else if (fail == HomeFail.OffIsland)
        {
            report.offIsland++;
        }
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static bool Almost(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude < 0.01f;
    }

    void SetShown(bool shown)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i].gameObject.name == "PickupBurst")
            {
                continue;
            }

            renderers[i].enabled = shown;
        }

        Light[] lights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            LightQuality.SetFireflyShown(lights[i], shown);
        }

        Collider body = GetComponent<Collider>();
        if (body != null)
        {
            body.enabled = shown;
        }
    }
}
}
