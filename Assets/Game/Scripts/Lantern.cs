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

    readonly Dictionary<UnityEngine.Object, float> modifiers = new Dictionary<UnityEngine.Object, float>();
    readonly List<UnityEngine.Object> staleModifiers = new List<UnityEngine.Object>();

    float fuel;
    float difficultyDrain = 1f;
    bool depleted;

    public float Fuel => fuel;
    public float MaxFuel => maxFuel;
    public float FuelNormalized => maxFuel <= 0f ? 0f : fuel / maxFuel;
    public float Radius => lanternLight != null ? lanternLight.range : maxRange;
    public float BaseIntensity { get; private set; }

    public event Action<float> FuelChanged;
    public event Action FuelDepleted;

    void Awake()
    {
        if (lanternLight == null)
        {
            lanternLight = GetComponentInChildren<Light>();
        }

        fuel = maxFuel;
        ApplyLight();
    }

    void Start()
    {
        difficultyDrain = GameSettings.DrainMultiplier;
        if (FuelChanged != null)
        {
            FuelChanged.Invoke(FuelNormalized);
        }
    }

    void Update()
    {
        if (depleted)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsRoundOver)
        {
            return;
        }

        float drain = drainPerSecond * difficultyDrain * ModifierProduct() * Time.deltaTime;
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

    public void SetDrainModifier(UnityEngine.Object source, float multiplier)
    {
        if (source == null)
        {
            return;
        }

        if (multiplier <= 1.001f)
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

        SetFuel(fuel + amount);
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

        SetFuel(fuel - amount);
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
        fuel = Mathf.Clamp(value, 0f, maxFuel);
        ApplyLight();
        if (FuelChanged != null)
        {
            FuelChanged.Invoke(FuelNormalized);
        }
    }

    void ApplyLight()
    {
        if (lanternLight == null)
        {
            return;
        }

        float amount = FuelNormalized;
        BaseIntensity = Mathf.Lerp(minIntensity, maxIntensity, amount);
        lanternLight.intensity = BaseIntensity;
        lanternLight.range = Mathf.Lerp(minRange, maxRange, amount);
        lanternLight.enabled = amount > 0.01f;
    }
}
}
