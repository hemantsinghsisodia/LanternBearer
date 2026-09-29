using UnityEngine;

namespace LanternKeeper
{
public class LightRevealed : MonoBehaviour
{
    const float MoveEpsilonSqr = 0.0004f;
    const float RevealEpsilon = 0.002f;

    [SerializeField] Lantern lantern;

    Renderer[] renderers;
    Collider[] colliders;
    bool[] dissolve;
    Color[] fadeColors;
    MaterialPropertyBlock block;
    bool hasFade;
    Vector3 sampledPosition;
    float sampledRadius = -1f;
    float sampledIntensity = -1f;
    float appliedReveal = -1f;
    bool appliedShow;
    bool appliedSolid;
    bool appliedOnce;
    bool lanternResolved;
    bool haveSample;
    static bool loggedFallback;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int DissolveId = Shader.PropertyToID("_LKDissolve");

    public float Reveal { get; private set; }
    public bool CollidersEnabled { get; private set; }
    public static float SolidThreshold => RevealMath.SolidReveal;

    public static float EvaluateReveal(Vector3 point, Vector3 lanternPosition, float radius)
    {
        return RevealMath.Evaluate(point, lanternPosition, radius);
    }

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        block = new MaterialPropertyBlock();
        dissolve = new bool[renderers.Length];
        fadeColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material material = renderer.sharedMaterial;
            bool usesDissolve = material != null && material.HasProperty(DissolveId);
            dissolve[i] = usesDissolve;
            if (usesDissolve)
            {
                continue;
            }

            hasFade = true;
            if (material != null && material.HasProperty(BaseColorId))
            {
                fadeColors[i] = material.GetColor(BaseColorId);
            }
            else if (material != null && material.HasProperty(ColorId))
            {
                fadeColors[i] = material.GetColor(ColorId);
            }
            else
            {
                fadeColors[i] = Color.white;
            }
        }

        Apply(0f);
    }

    void Start()
    {
        ResolveLantern();
        if (lantern == null)
        {
            return;
        }

        sampledPosition = lantern.transform.position;
        sampledRadius = lantern.Radius;
        sampledIntensity = lantern.BaseIntensity;
        haveSample = true;
        float reveal = CurrentReveal();
        Reveal = reveal;
        Apply(reveal);
    }

    void ResolveLantern()
    {
        if (lantern != null || lanternResolved)
        {
            return;
        }

        lanternResolved = true;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            lantern = player.GetComponentInChildren<Lantern>();
        }

        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

        if (!loggedFallback)
        {
            loggedFallback = true;
            Debug.LogWarning("LightRevealed lantern was not wired. Resolved once.", this);
        }
    }

    void Update()
    {
        if (lantern == null)
        {
            return;
        }

        Vector3 position = lantern.transform.position;
        float radius = lantern.Radius;
        float intensity = lantern.BaseIntensity;
        bool moved = !haveSample
            || (position - sampledPosition).sqrMagnitude > MoveEpsilonSqr
            || Mathf.Abs(radius - sampledRadius) > 0.01f
            || Mathf.Abs(intensity - sampledIntensity) > 0.01f;
        if (!moved)
        {
            return;
        }

        haveSample = true;
        sampledPosition = position;
        sampledRadius = radius;
        sampledIntensity = intensity;
        float reveal = CurrentReveal();
        Reveal = reveal;
        Apply(reveal);
    }

    public void RefreshVisibility()
    {
        haveSample = false;
        float reveal = CurrentReveal();
        Reveal = reveal;
        Apply(reveal);
    }

    float CurrentReveal()
    {
        if (lantern == null)
        {
            return 0f;
        }

        return RevealMath.Evaluate(transform.position, lantern.transform.position, lantern.Radius);
    }

    void Apply(float reveal)
    {
        bool show = reveal > 0.001f;
        bool solid = reveal >= RevealMath.SolidReveal;
        bool revealSame = appliedOnce && Mathf.Abs(reveal - appliedReveal) < RevealEpsilon;
        if (appliedOnce && show == appliedShow && solid == appliedSolid && (!hasFade || revealSame))
        {
            return;
        }

        appliedOnce = true;
        appliedShow = show;
        appliedSolid = solid;
        appliedReveal = reveal;
        CollidersEnabled = solid;
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

        renderer.enabled = show;
        if (!show || dissolve == null || dissolve[i])
        {
            continue;
        }

        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        Color color = fadeColors[i];
        color.a *= reveal;
        renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                renderer.SetPropertyBlock(block);
            }
        }

        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = solid;
            }
        }
    }
}
}
