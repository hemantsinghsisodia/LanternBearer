using UnityEngine;

namespace LanternKeeper
{
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    const int PoolSize = 12;

    [SerializeField] Lantern lantern;

    AudioSource oneShots;
    AudioSource ambience;
    AudioSource heartbeat;
    AudioSource[] pool;
    int poolCursor;
    bool heartOn;
    bool lanternResolved;

    public int PoolChildCount => poolRoot != null ? poolRoot.childCount : 0;

    Transform poolRoot;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate AudioManager destroyed.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        oneShots.spatialBlend = 0f;
        CreatePool();

        ambience = gameObject.AddComponent<AudioSource>();
        ambience.playOnAwake = false;
        ambience.loop = true;
        ambience.spatialBlend = 0f;
        ambience.volume = 0.22f;
        ambience.clip = ProceduralAudio.Ambience();
        ambience.Play();

        heartbeat = gameObject.AddComponent<AudioSource>();
        heartbeat.playOnAwake = false;
        heartbeat.loop = true;
        heartbeat.spatialBlend = 0f;
        heartbeat.volume = 0.45f;
        heartbeat.clip = ProceduralAudio.Heartbeat();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        ResolveLantern();
    }

    void ResolveLantern()
    {
        if (lantern != null || lanternResolved)
        {
            return;
        }

        lanternResolved = true;
        lantern = FindAnyObjectByType<Lantern>();
        if (lantern != null)
        {
            Debug.LogWarning("AudioManager lantern was not wired. Resolved once.", this);
        }
    }

    void CreatePool()
    {
        GameObject root = new GameObject("OneShotPool");
        root.transform.SetParent(transform, false);
        poolRoot = root.transform;
        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            GameObject child = new GameObject("PooledSource");
            child.transform.SetParent(poolRoot, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.pitch = 1f;
            source.volume = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 500f;
            source.ignoreListenerPause = false;
            pool[i] = source;
        }
    }

    void Update()
    {
        bool low = lantern != null && lantern.FuelNormalized > 0f && lantern.FuelNormalized < 0.2f
            && (GameManager.Instance == null || !GameManager.Instance.IsRoundOver);
        if (low == heartOn)
        {
            return;
        }

        heartOn = low;
        if (low)
        {
            heartbeat.Play();
        }
        else
        {
            heartbeat.Stop();
        }
    }

    public void PlayFirefly(Vector3 position)
    {
        PlayOneShot(ProceduralAudio.FireflyChime(), position, 0.8f);
    }

    public void PlayBeacon(Vector3 position)
    {
        PlayOneShot(ProceduralAudio.BeaconWhoosh(), position, 0.9f);
    }

    public void PlayFootstep(Vector3 position)
    {
        PlayOneShot(ProceduralAudio.Footstep(), position, 0.32f);
    }

    public void PlaySplash(Vector3 position)
    {
        PlayOneShot(ProceduralAudio.Splash(), position, 0.85f);
    }

    public void PlayDying()
    {
        if (oneShots == null)
        {
            return;
        }

        oneShots.PlayOneShot(ProceduralAudio.LanternDying(), 0.65f);
    }

    public void PlayFizzle()
    {
        if (oneShots == null)
        {
            return;
        }

        oneShots.PlayOneShot(ProceduralAudio.Fizzle(), 0.7f);
    }

    public void PlayLoop(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null || clip == null)
        {
            return;
        }

        source.clip = clip;
        source.loop = true;
        source.spatialBlend = 1f;
        source.volume = volume;
        source.playOnAwake = false;
        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    public void PlayOneShot(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource source = NextSource();
        source.Stop();
        source.clip = clip;
        source.transform.position = position;
        source.volume = volume;
        source.pitch = 1f;
        source.loop = false;
        source.spatialBlend = 1f;
        source.Play();
    }

    AudioSource NextSource()
    {
        int count = pool.Length;
        for (int n = 0; n < count; n++)
        {
            int index = (poolCursor + n) % count;
            if (!pool[index].isPlaying)
            {
                poolCursor = (index + 1) % count;
                return pool[index];
            }
        }

        AudioSource stolen = pool[poolCursor];
        poolCursor = (poolCursor + 1) % count;
        return stolen;
    }
}
}
