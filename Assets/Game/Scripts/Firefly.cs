using System.Collections;
using UnityEngine;

namespace LanternKeeper
{
public class Firefly : MonoBehaviour
{
    [SerializeField] float bobHeight = 0.35f;
    [SerializeField] float bobSpeed = 2.2f;
    [SerializeField] float refillAmount = 20f;
    [SerializeField] float respawnDelay = 9f;
    [SerializeField] float islandRadius = 22f;
    [SerializeField] float hoverHeight = 1.35f;
    [SerializeField] float wanderRadius = 1.4f;

    Vector3 home;
    bool collected;
    ParticleSystem burst;

    void Awake()
    {
        home = transform.position;
        Transform burstTransform = transform.Find("PickupBurst");
        if (burstTransform != null)
        {
            burst = burstTransform.GetComponent<ParticleSystem>();
        }
    }

    void Start()
    {
        respawnDelay *= GameSettings.FireflyRespawnMultiplier;
    }

    void Update()
    {
        if (collected)
        {
            return;
        }

        float time = Time.time;
        float bob = Mathf.Sin((time * bobSpeed) + home.x) * bobHeight;
        float ox = (Mathf.PerlinNoise(home.x * 0.2f, time * 0.35f) - 0.5f) * 2f * wanderRadius;
        float oz = (Mathf.PerlinNoise(home.z * 0.2f, time * 0.35f + 4f) - 0.5f) * 2f * wanderRadius;
        transform.position = new Vector3(home.x + ox, home.y + bob, home.z + oz);
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected || other == null || !other.CompareTag("Player"))
        {
            return;
        }

        Lantern lantern = other.GetComponentInChildren<Lantern>();
        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

        if (lantern != null)
        {
            lantern.AddFuel(refillAmount);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFirefly(transform.position);
        }

        if (burst != null)
        {
            burst.Play();
        }

        collected = true;
        StartCoroutine(RespawnLater());
    }

    IEnumerator RespawnLater()
    {
        SetShown(false);
        yield return new WaitForSeconds(respawnDelay);

        home = PickHome();
        transform.position = home;
        collected = false;
        SetShown(true);
    }

    Vector3 PickHome()
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 spot = Random.insideUnitCircle * islandRadius;
            if (spot.magnitude < 6f)
            {
                if (spot.sqrMagnitude < 0.01f)
                {
                    spot = Vector2.right;
                }

                spot = spot.normalized * 6f;
            }

            Vector3 origin = new Vector3(spot.x, 240f, spot.y);
            RaycastHit hit;
            if (!Physics.Raycast(origin, Vector3.down, out hit, 480f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            if (hit.point.y <= WaterHazard.SurfaceY + 0.45f)
            {
                continue;
            }

            return hit.point + Vector3.up * hoverHeight;
        }

        return new Vector3(home.x, home.y, home.z);
    }

    void SetShown(bool shown)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i].gameObject.name == "PickupBurst")
            {
                continue;
            }

            renderers[i].enabled = shown;
        }

        Light[] lights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].enabled = shown;
        }

        Collider body = GetComponent<Collider>();
        if (body != null)
        {
            body.enabled = shown;
        }
    }
}
}
