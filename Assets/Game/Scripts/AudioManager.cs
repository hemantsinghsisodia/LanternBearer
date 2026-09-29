using UnityEngine;

namespace LanternKeeper
{
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    AudioSource oneShots;
    AudioSource ambience;
    AudioSource heartbeat;
    Lantern lantern;
    bool heartOn;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        oneShots.spatialBlend = 0f;

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

    void Update()
    {
        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

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

        AudioSource.PlayClipAtPoint(clip, position, volume);
    }
}
}
