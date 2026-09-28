using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LanternKeeper
{
public class Beacon : MonoBehaviour
{
    public static readonly List<Beacon> All = new List<Beacon>();

    [SerializeField] float interactRange = 2.8f;
    [SerializeField] float fuelCost = 15f;
    [SerializeField] float safeRadius = 8f;
    [SerializeField] float settleIntensity = 2.8f;
    [SerializeField] float settleRange = 16f;
    [SerializeField] float columnHeight = 6f;
    [SerializeField] float columnBaseY = 1.35f;
    [SerializeField] Light beaconLight;
    [SerializeField] ParticleSystem fire;
    [SerializeField] ParticleSystem embers;
    [SerializeField] Transform lightColumn;
    [SerializeField] AudioSource whooshSource;

    public bool IsLit { get; private set; }
    public float SafeRadius => IsLit ? safeRadius : 0f;

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
            Transform flame = transform.Find("Flame");
            if (flame != null)
            {
                fire = flame.GetComponent<ParticleSystem>();
            }
        }

        if (embers == null)
        {
            Transform burst = transform.Find("Embers");
            if (burst != null)
            {
                embers = burst.GetComponent<ParticleSystem>();
            }
        }

        if (lightColumn == null)
        {
            Transform column = transform.Find("LightColumn");
            if (column != null)
            {
                lightColumn = column;
            }
        }

        if (whooshSource == null)
        {
            whooshSource = GetComponent<AudioSource>();
        }

        if (beaconLight != null)
        {
            beaconLight.enabled = false;
        }

        StopQuiet(fire);
        StopQuiet(embers);
        if (lightColumn != null)
        {
            lightColumn.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (!All.Contains(this))
        {
            All.Add(this);
        }
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Start()
    {
        fuelCost = GameSettings.BeaconFuelCost;
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

    public bool BlocksMoth(Vector3 worldPosition)
    {
        if (!IsLit)
        {
            return false;
        }

        Vector3 flat = worldPosition - transform.position;
        flat.y = 0f;
        return flat.magnitude < safeRadius;
    }

    public void TryLight()
    {
        if (IsLit)
        {
            return;
        }

        Lantern lantern = FindAnyObjectByType<Lantern>();
        if (lantern == null || !lantern.TrySpend(fuelCost))
        {
            return;
        }

        IsLit = true;
        playerNear = false;
        NotifyNearby(false);
        StartCoroutine(Ignite());

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterBeaconLit(this);
        }
    }

    IEnumerator Ignite()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBeacon(transform.position);
        }
        else if (whooshSource != null && whooshSource.clip != null)
        {
            whooshSource.Play();
        }

        if (embers != null)
        {
            embers.Play();
        }

        if (fire != null)
        {
            fire.Play();
        }

        if (lightColumn != null)
        {
            lightColumn.gameObject.SetActive(true);
        }

        if (beaconLight != null)
        {
            beaconLight.enabled = true;
            beaconLight.color = new Color(1f, 0.62f, 0.28f);
        }

        float growTime = 1.05f;
        float elapsed = 0f;
        while (elapsed < growTime)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / growTime));
            float height = Mathf.Lerp(0.08f, columnHeight, u);
            if (lightColumn != null)
            {
                lightColumn.localScale = new Vector3(0.55f, height, 0.55f);
                lightColumn.localPosition = new Vector3(0f, columnBaseY + height, 0f);
            }

            if (beaconLight != null)
            {
                float flash = Mathf.Sin(Mathf.Clamp01(elapsed / 0.35f) * Mathf.PI);
                beaconLight.intensity = Mathf.Lerp(settleIntensity, 12f, flash * (1f - u));
                beaconLight.range = Mathf.Lerp(5f, settleRange, u);
            }

            yield return null;
        }

        if (lightColumn != null)
        {
            lightColumn.localScale = new Vector3(0.55f, columnHeight, 0.55f);
            lightColumn.localPosition = new Vector3(0f, columnBaseY + columnHeight, 0f);
        }

        if (beaconLight != null)
        {
            beaconLight.enabled = true;
            beaconLight.intensity = settleIntensity;
            beaconLight.range = settleRange;
        }
    }

    void NotifyNearby(bool nearby)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetBeaconNearby(this, nearby);
        }
    }

    static void StopQuiet(ParticleSystem system)
    {
        if (system == null)
        {
            return;
        }

        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
}
