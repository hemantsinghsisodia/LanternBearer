using UnityEngine;

namespace LanternKeeper
{
public class WaterScroll : MonoBehaviour
{
    [SerializeField] Vector2 speed = new Vector2(0.012f, 0.007f);
    [SerializeField] Renderer target;

    Vector2 offset;

    void Awake()
    {
        if (target == null)
        {
            target = GetComponent<Renderer>();
        }
    }

    void Update()
    {
        if (target == null)
        {
            return;
        }

        offset += speed * Time.deltaTime;
        Material material = target.material;
        if (material == null)
        {
            return;
        }

        material.SetTextureOffset("_BumpMap", offset);
        material.SetTextureOffset("_BaseMap", offset * 0.45f);
    }
}
}
