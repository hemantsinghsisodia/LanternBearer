using UnityEngine;

namespace LanternKeeper
{
public class Moth : MonoBehaviour
{
    [SerializeField] float flySpeed = 7.2f;
    [SerializeField] float closeDistance = 1.5f;
    [SerializeField] float closeDrain = 1.5f;
    [SerializeField] float fleeSeconds = 3.2f;

    enum Mode
    {
        Hunt,
        Flee
    }

    Mode mode;
    Vector3 velocity;
    float sprintTimer;
    float fleeTimer;
    Transform player;
    PlayerController body;
    Lantern lantern;
    Transform wings;
    AudioSource flutter;

    void Awake()
    {
        wings = transform.Find("Wings");
        flutter = GetComponent<AudioSource>();
    }

    void Start()
    {
        FindTargets();
        if (AudioManager.Instance != null && flutter != null)
        {
            AudioManager.Instance.PlayLoop(flutter, ProceduralAudio.MothFlutter(), 0.28f);
        }
    }

    void OnDestroy()
    {
        if (lantern != null)
        {
            lantern.SetDrainModifier(this, 1f);
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsRoundOver)
        {
            SetClose(false);
            return;
        }

        FindTargets();
        if (player == null || lantern == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (mode == Mode.Hunt)
        {
            Hunt(dt);
        }
        else
        {
            Flee(dt);
        }

        if (wings != null)
        {
            float flap = Mathf.Sin(Time.time * 42f) * 32f;
            wings.localRotation = Quaternion.Euler(flap, 0f, 0f);
        }
    }

    void FindTargets()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                body = playerObject.GetComponent<PlayerController>();
            }
        }

        if (lantern == null && player != null)
        {
            lantern = player.GetComponentInChildren<Lantern>();
        }

        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }
    }

    void Hunt(float dt)
    {
        Vector3 target = lantern.transform.position;
        Vector3 to = target - transform.position;
        float distance = to.magnitude;
        Vector3 desired = to.sqrMagnitude > 0.001f ? to.normalized : Vector3.forward;
        float noise = Mathf.PerlinNoise(transform.position.x * 0.4f, Time.time * 0.8f) - 0.5f;
        float noise2 = Mathf.PerlinNoise(transform.position.z * 0.4f, Time.time * 0.8f + 3f) - 0.5f;
        desired += new Vector3(noise, noise2 * 0.25f, noise2);
        desired = AvoidBeacons(desired);
        if (desired.sqrMagnitude > 0.001f)
        {
            desired.Normalize();
        }

        velocity = Vector3.Lerp(velocity, desired * flySpeed, 2.4f * dt);
        Vector3 next = transform.position + velocity * dt;
        if (!InsideSafe(next))
        {
            transform.position = next;
        }
        else
        {
            velocity = AvoidBeacons(Vector3.up + desired) * flySpeed;
        }

        bool close = distance <= closeDistance;
        SetClose(close);

        bool sprintingAway = body != null && body.IsSprinting && body.HorizontalSpeed > flySpeed;
        if (sprintingAway)
        {
            sprintTimer += dt;
        }
        else
        {
            sprintTimer = 0f;
        }

        if (sprintTimer >= 1f)
        {
            mode = Mode.Flee;
            fleeTimer = fleeSeconds;
            sprintTimer = 0f;
            SetClose(false);
        }
    }

    void Flee(float dt)
    {
        fleeTimer -= dt;
        Vector3 away = transform.position - player.position;
        away.y = 0.35f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = Vector3.forward;
        }

        velocity = Vector3.Lerp(velocity, away.normalized * (flySpeed + 2.5f), 3f * dt);
        transform.position += velocity * dt;
        SetClose(false);
        if (fleeTimer <= 0f)
        {
            mode = Mode.Hunt;
        }
    }

    Vector3 AvoidBeacons(Vector3 desired)
    {
        for (int i = 0; i < Beacon.All.Count; i++)
        {
            Beacon beacon = Beacon.All[i];
            if (beacon == null || !beacon.IsLit)
            {
                continue;
            }

            Vector3 away = transform.position - beacon.transform.position;
            away.y = 0f;
            float dist = away.magnitude;
            float radius = beacon.SafeRadius;
            if (dist < radius + 2.5f && dist > 0.01f)
            {
                float push = 1f - Mathf.Clamp01((dist - radius) / 2.5f);
                desired += away.normalized * (2.4f * push);
            }
        }

        return desired;
    }

    bool InsideSafe(Vector3 worldPosition)
    {
        for (int i = 0; i < Beacon.All.Count; i++)
        {
            Beacon beacon = Beacon.All[i];
            if (beacon != null && beacon.BlocksMoth(worldPosition))
            {
                return true;
            }
        }

        return false;
    }

    void SetClose(bool close)
    {
        if (lantern == null)
        {
            return;
        }

        lantern.SetDrainModifier(this, close ? closeDrain : 1f);
    }
}
}
