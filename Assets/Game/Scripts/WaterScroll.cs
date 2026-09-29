using UnityEngine;

namespace LanternKeeper
{
public class WaterScroll : MonoBehaviour
{
    static readonly int BumpId = Shader.PropertyToID("_BumpMap");
    static readonly int BaseId = Shader.PropertyToID("_BaseMap");

    [SerializeField] Vector2 speed = new Vector2(0.012f, 0.007f);
    [SerializeField] Renderer target;

    Material runtimeMaterial;
    Vector2 offset;

    void Awake()
    {
        if (target == null)
        {
            target = GetComponent<Renderer>();
        }

        if (target != null)
        {
            runtimeMaterial = target.material;
        }
    }

    void Update()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        offset += speed * Time.deltaTime;
        runtimeMaterial.SetTextureOffset(BumpId, offset);
        runtimeMaterial.SetTextureOffset(BaseId, offset * 0.45f);
    }
}
}
