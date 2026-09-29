using UnityEngine;

namespace LanternKeeper
{
public class LightRevealed : MonoBehaviour
{
    const float MoveEpsilonSqr = 0.0004f;

    [SerializeField] Lantern lantern;

    Renderer[] renderers;
    Collider[] colliders;
    MaterialPropertyBlock block;
    Vector3 sampledPosition;
    float sampledRadius = -1f;
    float sampledIntensity = -1f;
    float appliedAlpha = -1f;
    bool appliedShow;
    bool appliedSolid;
    bool appliedOnce;
    bool lanternResolved;
    bool haveSample;
    static bool loggedFallback;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        block = new MaterialPropertyBlock();
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
        Apply(CurrentAlpha());
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
        Apply(CurrentAlpha());
    }

    public void RefreshVisibility()
    {
        haveSample = false;
        Apply(CurrentAlpha());
    }

    float CurrentAlpha()
    {
        if (lantern == null)
        {
            return 0f;
        }

        float radius = lantern.Radius;
        if (radius < 0.2f)
        {
            return 0f;
        }

        float distance = Vector3.Distance(transform.position, lantern.transform.position);
        if (distance >= radius)
        {
            return 0f;
        }

        return Mathf.Clamp01(1.15f * (1f - distance / radius));
    }

    void Apply(float alpha)
    {
        bool show = alpha > 0.05f;
        bool solid = alpha > 0.32f;
        bool alphaSame = appliedOnce && Mathf.Abs(alpha - appliedAlpha) < 0.002f;
        if (appliedOnce && show == appliedShow && solid == appliedSolid && (!show || alphaSame))
        {
            return;
        }

        appliedOnce = true;
        appliedShow = show;
        appliedSolid = solid;
        appliedAlpha = alpha;
        if (renderers != null)
        {
            Color color = new Color(0.82f, 0.76f, 0.62f, alpha);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = show;
                if (!show)
                {
                    continue;
                }

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
