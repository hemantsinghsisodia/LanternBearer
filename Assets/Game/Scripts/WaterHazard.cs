using UnityEngine;

namespace LanternKeeper
{
public class WaterHazard : MonoBehaviour
{
    public static float SurfaceY { get; private set; } = -100f;

    [SerializeField] float surfaceY;
    [SerializeField] float penalty = 10f;
    [SerializeField] float rescueCooldown = 1.25f;

    float nextRescue;

    public void SetSurface(float worldY)
    {
        surfaceY = worldY;
        SurfaceY = worldY;
    }

    void Awake()
    {
        SurfaceY = surfaceY;
    }

    void Update()
    {
        SurfaceY = surfaceY;
        if (Time.time < nextRescue)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsRoundOver)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            return;
        }

        if (playerObject.transform.position.y >= surfaceY - 0.05f)
        {
            return;
        }

        PlayerController controller = playerObject.GetComponent<PlayerController>();
        CharacterController body = playerObject.GetComponent<CharacterController>();
        Vector3 safe = playerObject.transform.position;
        if (controller != null)
        {
            safe = controller.LastSafePosition;
        }

        if (safe.y < surfaceY + 0.2f)
        {
            safe.y = surfaceY + 1.5f;
        }

        if (body != null)
        {
            body.enabled = false;
        }

        playerObject.transform.position = safe + Vector3.up * 0.1f;
        if (body != null)
        {
            body.enabled = true;
        }

        Lantern lantern = playerObject.GetComponentInChildren<Lantern>();
        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

        if (lantern != null)
        {
            float spend = Mathf.Min(penalty, lantern.Fuel);
            if (spend > 0f)
            {
                lantern.TrySpend(spend);
            }
        }

        nextRescue = Time.time + rescueCooldown;
    }
}
}
