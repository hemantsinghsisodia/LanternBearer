using System;
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
    [SerializeField] float settleIntensity = 3.4f;
    [SerializeField] float settleRange = 16f;
    [SerializeField] Light beaconLight;
    [SerializeField] ParticleSystem fire;
    [SerializeField] ParticleSystem embers;
    [SerializeField] ParticleSystem smoke;
    [SerializeField] Transform flameRoot;
    [SerializeField] AudioSource whooshSource;

    public static event Action LightFailed;

    public bool IsLit { get; private set; }
    public float SafeRadius => IsLit ? safeRadius : 0f;
    public float FuelCost => fuelCost;

    public bool IsPlayerInRange
    {
        get
        {
            if (player == null || IsLit || !isActiveAndEnabled)
            {
                return false;
            }

            return Vector3.Distance(transform.position, player.position) <= interactRange;
        }
    }

    public float DistanceToPlayer
    {
        get
        {
            if (player == null)
            {
                return float.MaxValue;
            }

            return Vector3.Distance(transform.position, player.position);
        }
    }

    Transform player;
    bool playerNear;
    bool igniting;

    void Awake()
    {
        fuelCost = GameSettings.BeaconFuelCost;
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
            Transform burst = transform.Find("Flare");
            if (burst == null)
            {
                burst = transform.Find("Embers");
            }

            if (burst != null)
            {
                embers = burst.GetComponent<ParticleSystem>();
            }
        }

        if (smoke == null)
        {
            Transform wisp = transform.Find("Smoke");
            if (wisp != null)
            {
                smoke = wisp.GetComponent<ParticleSystem>();
            }
        }

        if (flameRoot == null)
        {
            Transform flames = transform.Find("FlameRoot");
            if (flames != null)
            {
                flameRoot = flames;
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
        if (flameRoot != null)
        {
            flameRoot.localScale = Vector3.zero;
        }

        Transform legacyColumn = transform.Find("LightColumn");
        if (legacyColumn != null)
        {
            legacyColumn.gameObject.SetActive(false);
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
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetBeaconNearby(this, false);
        }
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
        if (IsLit)
        {
            FlickerAndSway();
        }

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

        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
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

    public bool TryLight()
    {
        if (IsLit)
        {
            return false;
        }

        if (GameManager.Instance != null && (GameManager.Instance.IsPaused || GameManager.Instance.IsRoundOver))
        {
            return false;
        }

        Lantern lantern = FindAnyObjectByType<Lantern>();
        if (lantern == null || !lantern.TrySpend(fuelCost))
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayFizzle();
            }

            if (LightFailed != null)
            {
                LightFailed.Invoke();
            }

            return false;
        }

        IsLit = true;
        playerNear = false;
        NotifyNearby(false);
        igniting = true;
        StartCoroutine(Ignite());

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterBeaconLit(this);
        }

        return true;
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

        if (smoke != null)
        {
            smoke.Play();
        }

        if (flameRoot != null)
        {
            flameRoot.gameObject.SetActive(true);
            flameRoot.localScale = Vector3.zero;
        }

        if (beaconLight != null)
        {
            beaconLight.enabled = true;
            beaconLight.color = new Color(1f, 0.58f, 0.22f);
        }

        float growTime = 0.9f;
        float elapsed = 0f;
        while (elapsed < growTime)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / growTime));
            float flash = Mathf.Sin(Mathf.Clamp01(elapsed / 0.28f) * Mathf.PI);
            float scale = Mathf.Lerp(0f, 1f, u) * (1f + flash * 0.55f * (1f - u));
            if (flameRoot != null)
            {
                flameRoot.localScale = Vector3.one * scale;
            }

            if (beaconLight != null)
            {
                beaconLight.intensity = Mathf.Lerp(settleIntensity, 14f, flash * (1f - u));
                beaconLight.range = Mathf.Lerp(4f, settleRange, u);
            }

            yield return null;
        }

        if (flameRoot != null)
        {
            flameRoot.localScale = Vector3.one;
        }

        if (beaconLight != null)
        {
            beaconLight.enabled = true;
            beaconLight.intensity = settleIntensity;
            beaconLight.range = settleRange;
        }

        igniting = false;
    }

    void FlickerAndSway()
    {
        if (flameRoot != null && flameRoot.localScale.sqrMagnitude > 0.01f)
        {
            float sway = Mathf.Sin(Time.time * 1.7f) * 8f;
            float gust = Mathf.Sin(Time.time * 0.7f + 0.4f) * 3f;
            flameRoot.localRotation = Quaternion.Euler(gust * 0.35f, 0f, sway + gust);
        }

        if (igniting || beaconLight == null || !beaconLight.enabled)
        {
            return;
        }

        float noise = Mathf.PerlinNoise(Time.time * 7.5f, 0.35f);
        beaconLight.intensity = settleIntensity * Mathf.Lerp(0.78f, 1.15f, noise);
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
