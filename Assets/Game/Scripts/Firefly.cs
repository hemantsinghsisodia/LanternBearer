using System.Collections;
using UnityEngine;

namespace LanternKeeper
{
public class Firefly : MonoBehaviour
{
    [SerializeField] float bobHeight = 0.4f;
    [SerializeField] float bobSpeed = 2.2f;
    [SerializeField] float refillAmount = 20f;
    [SerializeField] float respawnDelay = 9f;
    [SerializeField] float islandRadius = 20f;
    [SerializeField] float hoverHeight = 1.3f;
    [SerializeField] AudioSource chimeSource;

    Vector3 home;
    bool collected;

    void Awake()
    {
        home = transform.position;
        if (chimeSource == null)
        {
            chimeSource = GetComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (collected)
        {
            return;
        }

        float bob = Mathf.Sin((Time.time * bobSpeed) + home.x) * bobHeight;
        transform.position = new Vector3(home.x, home.y + bob, home.z);
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

        if (chimeSource != null && chimeSource.clip != null)
        {
            chimeSource.Play();
        }

        collected = true;
        StartCoroutine(RespawnLater());
    }

    IEnumerator RespawnLater()
    {
        SetShown(false);
        yield return new WaitForSeconds(respawnDelay);

        Vector2 spot = Random.insideUnitCircle * islandRadius;
        if (spot.magnitude < 5f)
        {
            if (spot.sqrMagnitude < 0.01f)
            {
                spot = Vector2.right;
            }
            spot = spot.normalized * 5f;
        }

        home = new Vector3(spot.x, hoverHeight, spot.y);
        transform.position = home;
        collected = false;
        SetShown(true);
    }

    void SetShown(bool shown)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
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
