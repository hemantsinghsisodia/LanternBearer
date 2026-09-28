using System.Collections;
using UnityEngine;

namespace LanternKeeper
{
public class MothSpawner : MonoBehaviour
{
    [SerializeField] int baseCount = 4;
    [SerializeField] GameObject mothPrefab;
    [SerializeField] float extraSpawnDelay = 16f;

    void Start()
    {
        StartCoroutine(SpawnOverTime());
    }

    IEnumerator SpawnOverTime()
    {
        int count = Mathf.RoundToInt(baseCount * GameSettings.MothCountMultiplier);
        if (count < 0)
        {
            count = 0;
        }

        int first = Mathf.Min(2, count);
        for (int i = 0; i < first; i++)
        {
            SpawnAround(i, Mathf.Max(count, 1));
        }

        for (int i = first; i < count; i++)
        {
            yield return new WaitForSeconds(extraSpawnDelay);
            if (GameManager.Instance != null && GameManager.Instance.IsRoundOver)
            {
                yield break;
            }

            SpawnAround(i, count);
        }
    }

    void SpawnAround(int index, int count)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;
        Lantern lantern = player != null ? player.GetComponentInChildren<Lantern>() : FindAnyObjectByType<Lantern>();
        float radius = 12.5f;
        if (lantern != null)
        {
            radius = Mathf.Max(8f, lantern.Radius * 0.95f);
        }

        float angle = (index + 0.35f) * Mathf.PI * 2f / count;
        Vector3 pos = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        pos.y = origin.y + 1.7f;
        Spawn(pos);
    }

    void Spawn(Vector3 position)
    {
        GameObject mothObject;
        if (mothPrefab != null)
        {
            mothObject = Instantiate(mothPrefab, position, Quaternion.identity, transform);
        }
        else
        {
            mothObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mothObject.name = "Moth";
            mothObject.transform.SetParent(transform, false);
            mothObject.transform.position = position;
            mothObject.transform.localScale = Vector3.one * 0.22f;
            Collider body = mothObject.GetComponent<Collider>();
            if (body != null)
            {
                Destroy(body);
            }

            mothObject.AddComponent<Moth>();
            mothObject.AddComponent<AudioSource>();
        }

        mothObject.name = "Moth";
    }
}
}
