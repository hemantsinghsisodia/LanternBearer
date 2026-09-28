using UnityEngine;

namespace LanternKeeper
{
public class LightRevealed : MonoBehaviour
{
    Renderer[] renderers;
    Collider[] colliders;
    MaterialPropertyBlock block;
    Lantern lantern;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        Apply(0f);
    }

    void Update()
    {
        RefreshVisibility();
    }

    public void RefreshVisibility()
    {
        Apply(CurrentAlpha());
    }

    float CurrentAlpha()
    {
        if (lantern == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                lantern = player.GetComponentInChildren<Lantern>();
            }
        }

        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

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
                if (!show)
                {
                    continue;
                }

                if (block == null)
                {
                    block = new MaterialPropertyBlock();
                }

                renderer.GetPropertyBlock(block);
                Color color = new Color(0.82f, 0.76f, 0.62f, alpha);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
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
