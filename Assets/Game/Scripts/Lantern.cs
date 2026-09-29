using System;
using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
public class Lantern : MonoBehaviour
{
    [SerializeField] float maxFuel = 100f;
    [SerializeField] float drainPerSecond = 1.6f;
    [SerializeField] Light lanternLight;
    [SerializeField] float minIntensity = 0.35f;
    [SerializeField] float maxIntensity = 4.2f;
    [SerializeField] float minRange = 3.5f;
    [SerializeField] float maxRange = 14f;
    [SerializeField] float proximityDimStrength = 0.65f;

    readonly Dictionary<UnityEngine.Object, float> modifiers = new Dictionary<UnityEngine.Object, float>();
    readonly List<UnityEngine.Object> staleModifiers = new List<UnityEngine.Object>();

    float fuel;
    float difficultyDrain = 1f;
    float escalation = 1f;
    float loggedEscalation = -1f;
    float proximityDim;
    float deathIntensity = -1f;
    float broadcastFuel = float.NaN;
    float broadcastTime = -10f;
    bool depleted;
    bool drainFrozen;
    bool inSafeLight;
    PlayerController owner;

    public float Fuel => fuel;
    public float MaxFuel => maxFuel;
    public float FuelNormalized => maxFuel <= 0f ? 0f : fuel / maxFuel;
    public float Radius => lanternLight != null ? lanternLight.range : maxRange;
    public float BaseIntensity { get; private set; }
    public float BaseDrainPerSecond => drainPerSecond;
    public float DifficultyDrain => difficultyDrain;
    public float EscalationMultiplier => escalation;
    public float CurrentDrainMultiplier { get; private set; }
    public bool InSafeLight => inSafeLight;
    public bool DrainFrozen { get { return drainFrozen; } set { drainFrozen = value; } }
    public bool IsDepleted => depleted;
    public float ProximityDim => proximityDim;
    public bool DeathLightActive => deathIntensity >= 0f;
    public float DeathLight => deathIntensity;
    public float ProximityScale => Mathf.Lerp(1f, 1f - proximityDimStrength, proximityDim);

    public event Action<float> FuelChanged;
    public event Action FuelDepleted;

    void Awake()
    {
        if (lanternLight == null)
        {
            lanternLight = GetComponentInChildren<Light>();
        }

        fuel = maxFuel;
        owner = GetComponentInParent<PlayerController>();
        ApplyLight();
    }

    void Start()
    {
        difficultyDrain = GameSettings.DrainMultiplier;
        broadcastFuel = fuel;
        broadcastTime = Time.unscaledTime;
        if (FuelChanged != null)
        {
            FuelChanged.Invoke(FuelNormalized);
        }
    }

    void Update()
    {
        RefreshDrainScale();
        if (drainFrozen || (GameManager.Instance != null && GameManager.Instance.IsDying))
        {
            ApplyLight();
            return;
        }

        if (depleted)
        {
            ApplyLight();
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsRoundOver)
        {
            ApplyLight();
            return;
        }

        float drain = drainPerSecond * CurrentDrainMultiplier * Time.deltaTime;
        SetFuel(fuel - drain);
        if (fuel > 0f)
        {
            return;
        }

        depleted = true;
        fuel = 0f;
        ApplyLight();
        if (FuelChanged != null)
        {
            FuelChanged.Invoke(0f);
        }

        if (FuelDepleted != null)
        {
            FuelDepleted.Invoke();
        }
    }

    public void SetProximityDim(float amount)
    {
        proximityDim = Mathf.Clamp01(amount);
    }

    public void SetDeathIntensity(float intensity)
    {
        deathIntensity = Mathf.Max(0f, intensity);
        ApplyLight();
    }

    void RefreshDrainScale()
    {
        int lit = GameManager.Instance != null ? GameManager.Instance.LitCount : 0;
        escalation = 1f + lit * GameSettings.BeaconDrainBonusPerLit;
        inSafeLight = InsideAnySafeZone();
        float safe = inSafeLight ? GameSettings.SafeZoneDrainMultiplier : 1f;
        CurrentDrainMultiplier = difficultyDrain * escalation * ModifierProduct() * safe;
        if (Mathf.Abs(escalation - loggedEscalation) > 0.001f)
        {
            loggedEscalation = escalation;
            Debug.Log("Drain multiplier x" + escalation.ToString("0.00")
                + " full x" + CurrentDrainMultiplier.ToString("0.00")
                + " difficulty x" + difficultyDrain.ToString("0.00"));
        }
    }

    bool InsideAnySafeZone()
    {
        Vector3 position = owner != null ? owner.transform.position : transform.position;

        for (int i = 0; i < Beacon.All.Count; i++)
        {
            Beacon beacon = Beacon.All[i];
            if (beacon != null && beacon.ContainsSafe(position))
            {
                return true;
            }
        }

        return false;
    }

    public void SetDrainModifier(UnityEngine.Object source, float multiplier)
    {
        if (source == null)
        {
            return;
        }

        if (multiplier < 0f || Mathf.Abs(multiplier - 1f) <= 0.001f)
        {
            modifiers.Remove(source);
            return;
        }

        modifiers[source] = multiplier;
    }

    public float GetDrainModifier(UnityEngine.Object source)
    {
        if (source == null)
        {
            return 1f;
        }

        float value;
        if (!modifiers.TryGetValue(source, out value))
        {
            return 1f;
        }

        return value;
    }

    public void AddFuel(float amount)
    {
        if (amount <= 0f || depleted)
        {
            return;
        }

        CommitFuel(fuel + amount, true);
    }

    public bool TrySpend(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (fuel < amount || depleted)
        {
            return false;
        }

        CommitFuel(fuel - amount, true);
        if (fuel <= 0f)
        {
            fuel = 0f;
        }

        return true;
    }

    float ModifierProduct()
    {
        float product = 1f;
        staleModifiers.Clear();
        foreach (KeyValuePair<UnityEngine.Object, float> pair in modifiers)
        {
            if (pair.Key == null)
            {
                staleModifiers.Add(pair.Key);
                continue;
            }

            product *= pair.Value;
        }

        for (int i = 0; i < staleModifiers.Count; i++)
        {
            modifiers.Remove(staleModifiers[i]);
        }

        return product;
    }

    void SetFuel(float value)
    {
        CommitFuel(value, false);
    }

    void CommitFuel(float value, bool immediate)
    {
        fuel = Mathf.Clamp(value, 0f, maxFuel);
        ApplyLight();
        PublishFuel(immediate || fuel <= 0f);
    }

    void PublishFuel(bool immediate)
    {
        if (FuelChanged == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        bool changed = float.IsNaN(broadcastFuel) || Mathf.Abs(fuel - broadcastFuel) > 0.05f;
        bool cooled = now - broadcastTime >= 0.05f;
        if (!immediate && !(changed && cooled))
        {
            return;
        }

        broadcastFuel = fuel;
        broadcastTime = now;
        FuelChanged.Invoke(FuelNormalized);
    }

    void ApplyLight()
    {
        if (lanternLight == null)
        {
            return;
        }

        if (deathIntensity >= 0f)
        {
            lanternLight.intensity = deathIntensity;
            lanternLight.enabled = deathIntensity > 0.02f;
            return;
        }

        float amount = FuelNormalized;
        BaseIntensity = Mathf.Lerp(minIntensity, maxIntensity, amount);
        lanternLight.intensity = BaseIntensity * ProximityScale;
        lanternLight.range = Mathf.Lerp(minRange, maxRange, amount);
        lanternLight.enabled = amount > 0.01f;
    }
}
}
