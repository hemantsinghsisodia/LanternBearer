using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// Soft camera-facing mist cards near water level and in the valleys. Positions are deterministic from the island seed.
[ExecuteAlways]
public class GroundMist : MonoBehaviour
{
    const int FullCardCount = 40;
    const int SeedOffset = 70;

    [SerializeField] float density = 1f;
    [SerializeField] int seed = 1101;
    [SerializeField] float radius = 40f;
    [SerializeField] float waterY;
    [SerializeField] Material cardMaterial;
    [SerializeField] Color tint = new Color(0.12f, 0.2f, 0.28f, 0.18f);

    readonly List<Transform> cards = new List<Transform>();
    float qualityScale = 1f;
    Camera cam;

    void OnEnable()
    {
        GraphicsQuality.QualityChanged += HandleQualityChanged;
        qualityScale = Application.isPlaying ? ScaleFor(GraphicsQuality.Profile) : 1f;
        Rebuild();
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= HandleQualityChanged;
        Clear();
    }

    void HandleQualityChanged(GraphicsProfile graphics)
    {
        qualityScale = ScaleFor(graphics);
        Rebuild();
    }

    // High and Ultra 1.0, Medium 0.5, Low 0.
    static float ScaleFor(GraphicsProfile graphics)
    {
        if (graphics == null)
        {
            return 1f;
        }

        switch (graphics.level)
        {
            case GraphicsLevel.Low:
                return 0f;
            case GraphicsLevel.Medium:
                return 0.5f;
            default:
                return 1f;
        }
    }

    void Rebuild()
    {
        Clear();
        int count = Mathf.RoundToInt(FullCardCount * Mathf.Clamp01(density) * qualityScale);
        System.Random random = new System.Random(seed + SeedOffset);
        for (int i = 0; i < count; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float dist = Mathf.Sqrt((float)random.NextDouble()) * radius;
            float height = 0.35f + (float)random.NextDouble() * 0.9f;
            float size = 9f + (float)random.NextDouble() * 9f;
            float alpha = 0.6f + (float)random.NextDouble() * 0.4f;

            GameObject card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = "MistCard";
            card.hideFlags = HideFlags.HideAndDontSave;
            Collider collider = card.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            card.transform.SetParent(transform, false);
            card.transform.position = new Vector3(Mathf.Cos(angle) * dist, waterY + height, Mathf.Sin(angle) * dist);
            card.transform.localScale = new Vector3(size, size * 0.35f, 1f);
            Renderer renderer = card.GetComponent<Renderer>();
            renderer.sharedMaterial = cardMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, tint.a * alpha));
            renderer.SetPropertyBlock(block);
            cards.Add(card.transform);
        }
    }

    void Clear()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
            {
                DestroyImmediate(cards[i].gameObject);
            }
        }

        cards.Clear();
    }

    void LateUpdate()
    {
        if (cards.Count == 0)
        {
            return;
        }

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null)
            {
                return;
            }
        }

        Quaternion facing = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].rotation = facing;
        }
    }
}
}
