using UnityEngine;

namespace LanternKeeper
{
// Rain on Island 4: a particle box that follows the camera, plus a quiet patter bed.
// Emission scales with the graphics preset through ParticleQuality, which LightQuality applies to every
// particle system in the scene (0.5x on Low, 1.5x on Ultra). Rain does not rescale it, so Low is not halved twice.
// The patter drops while the player is under cover or inside a safe ring.
public class Rain : MonoBehaviour
{
    [SerializeField] ParticleSystem system;
    [SerializeField] PlayerController player;
    [SerializeField] float height = 14f;
    [SerializeField] float patterVolume = 0.2f;
    [SerializeField] float shelteredFactor = 0.35f;

    AudioSource patter;
    bool patterRouted;
    float coverGain = 1f;
    Transform cameraTransform;

    void Awake()
    {
        patter = StormAudio.Make(gameObject, AudioManager.CueClip(SoundCues.AmbienceRain), true, 0f);
        patter.Play();
    }

    void Start()
    {
        // Registers with ParticleQuality now so the preset applies even before LightQuality sweeps the scene.
        ParticleQuality.ApplyTo(system);
    }

    void LateUpdate()
    {
        if (cameraTransform == null)
        {
            Camera main = Camera.main;
            cameraTransform = main != null ? main.transform : null;
        }

        if (cameraTransform != null)
        {
            transform.position = cameraTransform.position + Vector3.up * height;
        }

        UpdatePatter();
    }

    void UpdatePatter()
    {
        if (!patterRouted)
        {
            patterRouted = StormAudio.Route(patter, true);
        }

        Wind wind = Wind.Instance;
        bool covered = (wind != null && wind.Sheltered) || InSafeRing();
        coverGain = Mathf.MoveTowards(coverGain, covered ? shelteredFactor : 1f, Time.deltaTime * 1.5f);
        patter.volume = patterVolume * coverGain * StormAudio.MuteGain;
    }

    bool InSafeRing()
    {
        if (player == null)
        {
            return false;
        }

        Vector3 position = player.transform.position;
        for (int i = 0; i < Beacon.All.Count; i++)
        {
            if (Beacon.All[i] != null && Beacon.All[i].ContainsSafe(position))
            {
                return true;
            }
        }

        return false;
    }
}
}
