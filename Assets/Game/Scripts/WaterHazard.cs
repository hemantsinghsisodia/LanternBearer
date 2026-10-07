using UnityEngine;

namespace LanternKeeper
{
public class WaterHazard : MonoBehaviour
{
    // Height above the safe floor that still counts as dry: shared by the player's ground check and the beacon ring.
    public const float DefaultSafeWaterMargin = 0.45f;

    public static float SurfaceY { get; private set; } = -100f;
    public static float HighWaterY { get; private set; } = -100f;
    public static float SafeFloorY => Mathf.Max(SurfaceY, HighWaterY);

    [SerializeField] float surfaceY;
    [SerializeField] float penalty = 10f;
    [SerializeField] float rescueCooldown = 1.25f;
    [SerializeField] float fadeSeconds = 0.4f;
    [SerializeField] float inputLockSeconds = 0.5f;
    [SerializeField] ParticleSystem splash;
    [SerializeField] PlayerController player;
    [SerializeField] Lantern lantern;
    [SerializeField] HUD hud;
    [SerializeField] CameraFollow cameraFollow;

    float nextRescue;
    bool rescuing;
    bool refsResolved;
    static bool loggedFallback;

    public float FadePeak { get; private set; }
    public bool IsRescuing => rescuing;
    public float Penalty => penalty;
    public float LastRescueTime { get; private set; } = -1000f;
    public event System.Action Rescued;

    public void SetSurface(float worldY)
    {
        surfaceY = worldY;
        SurfaceY = worldY;
    }

    public static void SetHighWater(float worldY)
    {
        HighWaterY = worldY;
    }

    void Awake()
    {
        SurfaceY = surfaceY;
        EnsureSplash();
    }

    void Start()
    {
        ResolveRefs();
    }

    void Update()
    {
        SurfaceY = surfaceY;
        if (rescuing || Time.time < nextRescue)
        {
            return;
        }

        if (GameManager.Instance != null && (GameManager.Instance.IsRoundOver || GameManager.Instance.IsDying))
        {
            return;
        }

        if (player == null)
        {
            return;
        }

        if (player.transform.position.y >= surfaceY - 0.05f)
        {
            return;
        }

        StartCoroutine(Rescue());
    }

    void ResolveRefs()
    {
        if (refsResolved)
        {
            return;
        }

        bool missing = player == null || lantern == null || hud == null || cameraFollow == null;
        refsResolved = true;
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.GetComponent<PlayerController>();
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

        if (hud == null)
        {
            hud = FindAnyObjectByType<HUD>();
        }

        if (cameraFollow == null)
        {
            Camera view = Camera.main;
            cameraFollow = view != null ? view.GetComponent<CameraFollow>() : null;
            if (cameraFollow == null)
            {
                cameraFollow = FindAnyObjectByType<CameraFollow>();
            }
        }

        if (missing && !loggedFallback)
        {
            loggedFallback = true;
            Debug.LogWarning("WaterHazard references were not wired. Resolved once.", this);
        }
    }

    System.Collections.IEnumerator Rescue()
    {
        rescuing = true;
        FadePeak = 0f;
        nextRescue = Time.time + rescueCooldown;
        ResolveRefs();
        if (player == null)
        {
            rescuing = false;
            yield break;
        }

        GameManager manager = GameManager.Instance;
        if (manager != null)
        {
            manager.SetControlsLocked(true);
        }

        if (lantern != null)
        {
            lantern.DrainFrozen = true;
        }

        Vector3 splashAt = player.transform.position;
        splashAt.y = surfaceY;
        PlaySplash(splashAt);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySplash(splashAt);
        }

        float half = Mathf.Max(0.05f, fadeSeconds * 0.5f);
        yield return Fade(hud, 0f, 1f, half);
        Teleport(lantern);
        yield return Fade(hud, 1f, 0f, half);
        if (Rescued != null)
        {
            Rescued.Invoke();
        }

        if (hud != null)
        {
            hud.SetFadeAlpha(0f);
        }

        float locked = fadeSeconds;
        while (locked < inputLockSeconds)
        {
            locked += Time.deltaTime;
            yield return null;
        }

        if (lantern != null)
        {
            lantern.DrainFrozen = false;
        }

        if (manager != null && !manager.IsDying)
        {
            manager.SetControlsLocked(false);
        }

        LastRescueTime = Time.time;
        rescuing = false;
    }

    System.Collections.IEnumerator Fade(HUD hud, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            if (hud != null)
            {
                hud.SetFadeAlpha(alpha);
            }

            if (alpha > FadePeak)
            {
                FadePeak = alpha;
            }

            yield return null;
        }
    }

    void Teleport(Lantern lantern)
    {
        if (player == null)
        {
            return;
        }

        Vector3 safe = player.LastSafePosition;
        float floor = SafeFloorY;
        if (safe.y < floor + 0.35f)
        {
            safe.y = floor + 1.6f;
        }

        CharacterController body = player.GetComponent<CharacterController>();
        if (body != null)
        {
            body.enabled = false;
        }

        player.transform.position = safe + Vector3.up * 0.12f;
        if (body != null)
        {
            body.enabled = true;
        }

        if (lantern != null)
        {
            float spend = Mathf.Min(penalty, lantern.Fuel);
            if (spend > 0f)
            {
                lantern.TrySpend(spend);
            }
        }

        if (cameraFollow != null)
        {
            cameraFollow.SnapBehind();
        }
    }

    void PlaySplash(Vector3 world)
    {
        EnsureSplash();
        if (splash == null)
        {
            return;
        }

        splash.transform.position = world + Vector3.up * 0.05f;
        splash.Play();
    }

    void EnsureSplash()
    {
        if (splash != null)
        {
            return;
        }

        Transform existing = transform.Find("Splash");
        if (existing != null)
        {
            splash = existing.GetComponent<ParticleSystem>();
        }

        if (splash != null)
        {
            ParticleQuality.ApplyTo(splash);
            return;
        }

        GameObject burst = new GameObject("Splash");
        burst.transform.SetParent(transform, false);
        splash = burst.AddComponent<ParticleSystem>();
        splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = splash.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.55f;
        main.startLifetime = 0.45f;
        main.startSpeed = 3.2f;
        main.startSize = 0.16f;
        main.startColor = new Color(0.72f, 0.88f, 1f, 0.85f);
        main.maxParticles = 48;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = splash.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
        ParticleSystem.ShapeModule shape = splash.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;
        ParticleSystemRenderer renderer = burst.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader != null)
        {
            Material material = new Material(shader);
            Color color = new Color(0.75f, 0.9f, 1f, 0.9f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            renderer.sharedMaterial = material;
        }

        splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleQuality.ApplyTo(splash);
    }
}
}
