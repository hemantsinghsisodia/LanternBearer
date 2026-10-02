using UnityEngine;

namespace LanternKeeper
{
// Keeps the Shade population at StormTuning.ShadeTarget: the base count plus one per three beacons lit, up to the cap.
public class ShadeSpawner : MonoBehaviour
{
    const int MaxAttempts = 36;

    public static ShadeSpawner Instance { get; private set; }

    [SerializeField] GameObject shadePrefab;
    [SerializeField] int baseCount = 2;
    [SerializeField] float islandRadius;
    [SerializeField] float minPlayerDistance = 25f;
    [SerializeField] Lantern lantern;
    [SerializeField] PlayerController player;
    [SerializeField] WaterHazard hazard;
    [SerializeField] HUD hud;

    bool refsResolved;
    bool warnedMissingPrefab;
    bool warnedNoEdge;
    float edgeRetryTimer;
    static bool loggedFallback;

    public int Cap => GameSettings.MaxShades();
    public int AliveCount => Shade.LivingCount();

    void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            return;
        }

        Instance = this;
    }

    void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate ShadeSpawner destroyed.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        ResolveRefs();
        TopUp();
    }

    void Update()
    {
        TopUp();
    }

    void TopUp()
    {
        // A failed edge search retries about once a second, like Shade.TickReform.
        if (edgeRetryTimer > 0f)
        {
            edgeRetryTimer -= Time.deltaTime;
            return;
        }

        GameManager manager = GameManager.Instance;
        if (manager != null && (manager.IsRoundOver || manager.IsDying))
        {
            return;
        }

        int lit = manager != null ? manager.LitCount : 0;
        int target = StormTuning.ShadeTarget(baseCount, lit, Cap);
        int guard = target + 1;
        while (AliveCount < target && guard > 0)
        {
            guard--;
            Vector3 position;
            if (!TryFindEdge(out position))
            {
                edgeRetryTimer = 1f;
                if (!warnedNoEdge)
                {
                    warnedNoEdge = true;
                    Debug.LogWarning("ShadeSpawner could not find a dark-edge spawn.");
                }

                return;
            }

            Spawn(position);
        }
    }

    void ResolveRefs()
    {
        if (refsResolved)
        {
            return;
        }

        refsResolved = true;
        bool missing = lantern == null || player == null || hazard == null || hud == null;
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
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

        if (hud == null)
        {
            hud = FindAnyObjectByType<HUD>();
        }

        if (missing && !loggedFallback)
        {
            loggedFallback = true;
            Debug.LogWarning("ShadeSpawner references were not wired. Resolved once.", this);
        }
    }

    void Spawn(Vector3 position)
    {
        if (shadePrefab == null)
        {
            if (!warnedMissingPrefab)
            {
                warnedMissingPrefab = true;
                Debug.LogError("ShadeSpawner has no shade prefab assigned.");
            }

            return;
        }

        ResolveRefs();
        GameObject shadeObject = Instantiate(shadePrefab, position, Quaternion.identity, transform);
        shadeObject.name = "Shade";
        Shade shade = shadeObject.GetComponent<Shade>();
        if (shade != null)
        {
            shade.Bind(player != null ? player.transform : null, lantern, hazard, hud);
        }
    }

    // Picks a dark point near the shore, at least minPlayerDistance from the player. The search is bounded:
    // if no point is far enough it takes the farthest candidate it saw.
    public bool TryFindEdge(out Vector3 position)
    {
        ResolveRefs();
        float shore = FindShoreDistance(ResolveRadius());
        bool hasPlayer = player != null;
        Vector3 playerFlat = hasPlayer ? player.transform.position : Vector3.zero;
        playerFlat.y = 0f;
        position = Vector3.zero;
        Vector3 farthest = Vector3.zero;
        float farthestDistance = -1f;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = shore * Random.Range(0.78f, 0.94f);
            Vector3 flat = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            if (InsideLitZone(flat))
            {
                continue;
            }

            float ground;
            Vector3 normal;
            if (!TerrainQuery.TrySample(flat, out ground, out normal))
            {
                continue;
            }

            if (ground < WaterHazard.SafeFloorY + 0.7f || normal.y < 0.45f)
            {
                continue;
            }

            Vector3 candidate = new Vector3(flat.x, ground + ShadeLogic.HoverHeight, flat.z);
            float playerDistance = hasPlayer ? Vector3.Distance(flat, playerFlat) : 999f;
            if (!hasPlayer || playerDistance >= minPlayerDistance)
            {
                position = candidate;
                return true;
            }

            if (playerDistance > farthestDistance)
            {
                farthest = candidate;
                farthestDistance = playerDistance;
            }
        }

        if (farthestDistance >= 0f)
        {
            position = farthest;
            return true;
        }

        return false;
    }

    float FindShoreDistance(float radius)
    {
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 2f / 8f;
            float lo = 6f;
            float hi = radius;
            for (int step = 0; step < 8; step++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector3 flat = new Vector3(Mathf.Cos(angle) * mid, 0f, Mathf.Sin(angle) * mid);
                float ground;
                Vector3 normal;
                bool land = TerrainQuery.TrySample(flat, out ground, out normal) && ground > WaterHazard.SafeFloorY + 0.7f;
                if (land)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            sum += lo;
            count++;
        }

        if (count == 0)
        {
            return radius * 0.7f;
        }

        return sum / count;
    }

    float ResolveRadius()
    {
        if (islandRadius > 5f)
        {
            return islandRadius;
        }

        return TerrainQuery.IslandRadius();
    }

    static bool InsideLitZone(Vector3 flat)
    {
        for (int i = 0; i < Beacon.All.Count; i++)
        {
            Beacon beacon = Beacon.All[i];
            if (beacon != null && beacon.BlocksMoth(flat + Vector3.up))
            {
                return true;
            }
        }

        return false;
    }
}
}
