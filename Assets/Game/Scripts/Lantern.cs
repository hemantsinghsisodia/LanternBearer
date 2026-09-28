using System;
using UnityEngine;

namespace LanternKeeper
{
public class Lantern : MonoBehaviour
{
    [SerializeField] float maxFuel = 100f;
    [SerializeField] float drainPerSecond = 1.6f;
    [SerializeField] Light lanternLight;
    [SerializeField] float minIntensity = 0.2f;
    [SerializeField] float maxIntensity = 4f;
    [SerializeField] float minRange = 2.5f;
    [SerializeField] float maxRange = 14f;

    float fuel;
    bool depleted;

    public float Fuel => fuel;
    public float MaxFuel => maxFuel;
    public float FuelNormalized => maxFuel <= 0f ? 0f : fuel / maxFuel;

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

        SetFuel(fuel - drainPerSecond * Time.deltaTime);
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
        return true;
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
        lanternLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, amount);
        lanternLight.range = Mathf.Lerp(minRange, maxRange, amount);
        lanternLight.enabled = amount > 0.01f;
    }
}
}
