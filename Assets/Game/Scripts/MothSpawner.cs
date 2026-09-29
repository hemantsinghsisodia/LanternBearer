using System.Collections;
using UnityEngine;

namespace LanternKeeper
{
public class MothSpawner : MonoBehaviour
{
    public static MothSpawner Instance { get; private set; }

    [SerializeField] int baseCount = 4;
    [SerializeField] GameObject mothPrefab;
    [SerializeField] float extraSpawnDelay = 16f;
    [SerializeField] float islandRadius;
    [SerializeField] float minPlayerDistance = 25f;
    [SerializeField] int maxMoths;
    [SerializeField] Lantern lantern;

    int litSeen;
    int spawned;
    bool warnedMissingPrefab;
    bool lanternResolved;
    AudioSource whisper;
    Transform player;
    PlayerController playerBody;
    static bool loggedFallback;

    public int SpawnedCount => spawned;
    public int AliveCount => Moth.LivingCount();
    public AudioSource ProximitySource => whisper;
    public float NearestDistance { get; private set; }

    public int Cap
    {
        get { return maxMoths > 0 ? maxMoths : GameSettings.MaxMoths; }
    }

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
            Debug.LogWarning("Duplicate MothSpawner destroyed.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureWhisper();
    }

    void Start()
    {
        ResolveLantern();
        StartCoroutine(SpawnOverTime());
    }

    void ResolveLantern()
    {
        if (lanternResolved)
        {
            return;
        }

        bool missing = lantern == null;
        lanternResolved = true;
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                playerBody = playerObject.GetComponent<PlayerController>();
                if (lantern == null)
                {
                    lantern = playerObject.GetComponentInChildren<Lantern>();
                }
            }
        }

        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

        if (player == null && lantern != null)
        {
            playerBody = lantern.GetComponentInParent<PlayerController>();
            if (playerBody != null)
            {
                player = playerBody.transform;
            }
        }

        if (missing && !loggedFallback)
        {
            loggedFallback = true;
            Debug.LogWarning("MothSpawner lantern was not wired. Resolved once.", this);
        }
    }

    void Update()
    {
        int lit = GameManager.Instance != null ? GameManager.Instance.LitCount : 0;
        bool ended = GameManager.Instance != null && (GameManager.Instance.IsRoundOver || GameManager.Instance.IsDying);
        if (!ended)
        {
            while (litSeen < lit)
            {
                litSeen++;
                TrySpawnExtra();
            }
        }
        else
        {
            DespawnAll();
        }

        UpdateProximity();
    }

    IEnumerator SpawnOverTime()
    {
        int count = Mathf.RoundToInt(baseCount * GameSettings.MothCountMultiplier);
        if (count < 0)
        {
            count = 0;
        }

        count = Mathf.Min(count, Cap);
        int first = Mathf.Min(2, count);
        for (int i = 0; i < first; i++)
        {
            TrySpawnEdge();
        }

        for (int i = first; i < count; i++)
        {
            yield return new WaitForSeconds(extraSpawnDelay);
            if (GameManager.Instance != null && (GameManager.Instance.IsRoundOver || GameManager.Instance.IsDying))
            {
                yield break;
            }

            TrySpawnEdge();
        }
    }

    void TrySpawnExtra()
    {
        if (GameManager.Instance != null && (GameManager.Instance.IsRoundOver || GameManager.Instance.IsDying))
        {
            return;
        }

        if (AliveCount >= Cap || spawned >= Cap)
        {
            return;
        }

        TrySpawnEdge();
    }

    void TrySpawnEdge()
    {
        if (spawned >= Cap)
        {
            return;
        }

        Vector3 position;
        if (!TryDarkEdge(out position))
        {
            Debug.LogWarning("MothSpawner could not find a dark-edge spawn.");
            return;
        }

        Spawn(position);
    }

    bool TryDarkEdge(out Vector3 position)
    {
        float shore = FindShoreDistance(ResolveRadius());
        Vector3 playerFlat = PlayerFlat();
        bool hasPlayer = HasPlayer();
        position = Vector3.zero;
        Vector3 farthest = Vector3.zero;
        float farthestDistance = -1f;
        for (int attempt = 0; attempt < 36; attempt++)
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

            if (ground < WaterHazard.SurfaceY + 0.7f || normal.y < 0.45f)
            {
                continue;
            }

            Vector3 candidate = new Vector3(flat.x, ground + 1.8f, flat.z);
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
                bool land = TerrainQuery.TrySample(flat, out ground, out normal) && ground > WaterHazard.SurfaceY + 0.7f;
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

    void Spawn(Vector3 spawnPosition)
    {
        GameObject mothObject;
        if (mothPrefab != null)
        {
            mothObject = Instantiate(mothPrefab, spawnPosition, Quaternion.identity, transform);
        }
        else
        {
            if (!warnedMissingPrefab)
            {
                warnedMissingPrefab = true;
                Debug.LogError("MothSpawner has no moth prefab assigned.");
            }

            mothObject = BuildRuntimeMoth(spawnPosition);
            if (mothObject == null)
            {
                return;
            }
        }

        mothObject.name = "Moth";
        Moth moth = mothObject.GetComponent<Moth>();
        if (moth != null)
        {
            ResolveLantern();
            moth.Bind(player, playerBody, lantern);
        }

        spawned++;
    }

    GameObject BuildRuntimeMoth(Vector3 spawnPosition)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            Debug.LogError("MothSpawner could not build a runtime moth visual.");
            return null;
        }

        GameObject root = new GameObject("Moth");
        root.transform.SetParent(transform, false);
        root.transform.position = spawnPosition;
        root.transform.localScale = Vector3.one * 0.35f;

        GameObject wings = new GameObject("Wings");
        wings.transform.SetParent(root.transform, false);
        MeshFilter filter = wings.AddComponent<MeshFilter>();
        filter.sharedMesh = WingMesh();
        MeshRenderer renderer = wings.AddComponent<MeshRenderer>();
        Material material = new Material(shader);
        Color body = new Color(0.12f, 0.13f, 0.16f, 1f);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", body);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", body);
        }

        renderer.sharedMaterial = material;
        root.AddComponent<AudioSource>();
        root.AddComponent<Moth>();
        return root;
    }

    static Mesh wingMesh;

    static Mesh WingMesh()
    {
        if (wingMesh != null)
        {
            return wingMesh;
        }

        wingMesh = new Mesh();
        wingMesh.name = "MothWings";
        wingMesh.vertices = new[]
        {
            new Vector3(0f, 0f, 0.22f),
            new Vector3(-0.42f, 0f, -0.08f),
            new Vector3(0f, 0.02f, -0.02f),
            new Vector3(0.42f, 0f, -0.08f)
        };
        wingMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        wingMesh.RecalculateNormals();
        return wingMesh;
    }

    void DespawnAll()
    {
        for (int i = Moth.All.Count - 1; i >= 0; i--)
        {
            if (Moth.All[i] != null)
            {
                Moth.All[i].BeginDespawn();
            }
        }
    }

    void UpdateProximity()
    {
        Vector3 origin = lantern != null ? lantern.transform.position : transform.position;
        float nearest = 999f;
        int counted = 0;
        for (int i = 0; i < Moth.All.Count; i++)
        {
            Moth moth = Moth.All[i];
            if (moth == null || moth.IsDespawning)
            {
                continue;
            }

            counted++;
            float distance = Vector3.Distance(origin, moth.transform.position);
            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        NearestDistance = counted > 0 ? nearest : 999f;
        float closeness = 0f;
        if (counted > 0)
        {
            closeness = 1f - Mathf.InverseLerp(1.2f, 9f, nearest);
        }

        if (lantern != null)
        {
            lantern.SetProximityDim(closeness);
        }

        if (whisper == null)
        {
            return;
        }

        if (whisper.clip == null)
        {
            whisper.clip = ProceduralAudio.MothWhisper();
        }

        whisper.volume = closeness * 0.62f;
        if (closeness > 0.03f)
        {
            if (!whisper.isPlaying)
            {
                whisper.Play();
            }
        }
        else if (whisper.isPlaying)
        {
            whisper.Stop();
        }
    }

    void EnsureWhisper()
    {
        Transform existing = transform.Find("MothWhisper");
        GameObject host = existing != null ? existing.gameObject : new GameObject("MothWhisper");
        if (existing == null)
        {
            host.transform.SetParent(transform, false);
        }

        whisper = host.GetComponent<AudioSource>();
        if (whisper == null)
        {
            whisper = host.AddComponent<AudioSource>();
        }

        whisper.playOnAwake = false;
        whisper.loop = true;
        whisper.spatialBlend = 0f;
        whisper.volume = 0f;
        whisper.clip = ProceduralAudio.MothWhisper();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.RouteSfx(whisper);
        }
    }

    float ResolveRadius()
    {
        if (islandRadius > 5f)
        {
            return islandRadius;
        }

        return TerrainQuery.IslandRadius();
    }

    bool HasPlayer()
    {
        return player != null;
    }

    Vector3 PlayerFlat()
    {
        if (player == null)
        {
            return Vector3.zero;
        }

        Vector3 position = player.position;
        position.y = 0f;
        return position;
    }

    static bool InsideLitZone(Vector3 flat)
    {
        Vector3 point = flat;
        for (int i = 0; i < Beacon.All.Count; i++)
        {
            Beacon beacon = Beacon.All[i];
            if (beacon != null && beacon.BlocksMoth(point + Vector3.up))
            {
                return true;
            }
        }

        return false;
    }
}
}
