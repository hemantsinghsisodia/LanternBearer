using UnityEngine;
using UnityEngine.InputSystem;

namespace LanternKeeper
{
public class Beacon : MonoBehaviour
{
    [SerializeField] float interactRange = 2.8f;
    [SerializeField] float fuelCost = 15f;
    [SerializeField] Light beaconLight;
    [SerializeField] ParticleSystem fire;
    [SerializeField] AudioSource whooshSource;

    public bool IsLit { get; private set; }

    Transform player;
    bool playerNear;

    void Awake()
    {
        if (beaconLight == null)
        {
            beaconLight = GetComponentInChildren<Light>(true);
        }

        if (fire == null)
        {
            fire = GetComponentInChildren<ParticleSystem>(true);
        }

        if (whooshSource == null)
        {
            whooshSource = GetComponent<AudioSource>();
        }

        if (beaconLight != null)
        {
            beaconLight.enabled = false;
        }

        if (fire != null)
        {
            fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    void Update()
    {
        if (player == null || IsLit)
        {
            if (playerNear)
            {
                playerNear = false;
                NotifyNearby(false);
            }
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsRoundOver)
        {
            return;
        }

        bool near = Vector3.Distance(transform.position, player.position) <= interactRange;
        if (near != playerNear)
        {
            playerNear = near;
            NotifyNearby(near);
        }

        if (!near)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            TryLight();
        }
    }

    void TryLight()
    {
        Lantern lantern = FindAnyObjectByType<Lantern>();
        if (lantern == null || !lantern.TrySpend(fuelCost))
        {
            return;
        }

        IsLit = true;
        playerNear = false;
        NotifyNearby(false);

        if (beaconLight != null)
        {
            beaconLight.enabled = true;
        }

        if (fire != null)
        {
            fire.Play();
        }

        if (whooshSource != null && whooshSource.clip != null)
        {
            whooshSource.Play();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterBeaconLit(this);
        }
    }

    void NotifyNearby(bool nearby)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetBeaconNearby(this, nearby);
        }
    }
}
}
