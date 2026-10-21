using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LanternKeeper
{
// The great lamp on the summit of Island 5. In H1 it is a large storm lantern; lighting it is the win. It can be lit only once the
// SummitGate is Open (all beacons lit). Light() starts the win through GameManager.WinNow, whose WonGame event also plays the win
// stinger (MusicDirector), so the lamp does not play it itself. On Island 5 the visual is the lighthouse (LighthouseInstaller); TryLight also starts its GreatLampBeam.
public class GreatLamp : MonoBehaviour
{
    public const string PromptText = "E  Light the great lamp";

    [SerializeField] SummitGate gate;
    [SerializeField] float interactRange = 4.5f;
    [SerializeField] float lightIntensity = 8f;
    [SerializeField] float lightRange = 34f;

    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int BeamIntensityId = Shader.PropertyToID("_Intensity");
    static readonly int BeamLengthId = Shader.PropertyToID("_LenScale");

    Transform player;
    Transform flameRoot;
    Light lampLight;
    Renderer glass;
    Renderer beams;
    Renderer doorGlow;
    GreatLampBeam lampBeam;
    MaterialPropertyBlock beamsBlock;
    MaterialPropertyBlock block;
    bool promptShown;
    string hint;
    int hintCount = -1;
    bool igniting;
    float litScale;
    float flareStart = -1f;
    float flarePeak = 1f;
    float flareSeconds = 1f;

    public bool Lit { get; private set; }
    public Light LampLight => lampLight;
    public Transform Flame => flameRoot;

    // The finale flare: the lamp light is multiplied by `peak`, falling back to 1 over `seconds` (unscaled).
    public void Flare(float peak, float seconds)
    {
        flarePeak = Mathf.Max(1f, peak);
        flareSeconds = Mathf.Max(0.01f, seconds);
        flareStart = Time.unscaledTime;
    }

    float FlareFactor()
    {
        if (flareStart < 0f)
        {
            return 1f;
        }

        float k = Mathf.Clamp01((Time.unscaledTime - flareStart) / flareSeconds);
        return 1f + (flarePeak - 1f) * (1f - k) * (1f - k);
    }

    public void Configure(SummitGate summitGate)
    {
        gate = summitGate;
    }

    void Awake()
    {
        if (gate == null)
        {
            gate = FindAnyObjectByType<SummitGate>();
        }

        flameRoot = transform.Find("FlameRoot");
        if (flameRoot != null)
        {
            flameRoot.localScale = Vector3.zero;
        }

        lampLight = GetComponentInChildren<Light>(true);
        if (lampLight != null)
        {
            lampLight.enabled = false;
        }

        Transform glassTransform = transform.Find("BeaconGlass");
        glass = glassTransform != null ? glassTransform.GetComponent<Renderer>() : null;
        lampBeam = GetComponentInChildren<GreatLampBeam>(true);
        Transform doorGlowTransform = transform.Find("LighthouseDoorGlow");
        doorGlow = doorGlowTransform != null ? doorGlowTransform.GetComponent<Renderer>() : null;
        Transform beamsTransform = transform.Find("BeaconBeams");
        beams = beamsTransform != null ? beamsTransform.GetComponent<Renderer>() : null;
        if (beams != null)
        {
            beams.enabled = false;
        }
    }

    void OnDisable()
    {
        ClearPrompt();
    }

    // Refused (false) until the gate is open, and once lit.
    public bool TryLight()
    {
        if (Lit)
        {
            return false;
        }

        GameManager manager = GameManager.Instance;
        if (manager != null && (manager.IsPaused || manager.IsRoundOver || manager.IsDying))
        {
            return false;
        }

        if (gate == null || !gate.Open)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayFizzle();
            }

            return false;
        }

        if (manager != null && !manager.WinNow())
        {
            return false;
        }

        EnsureLit();
        return true;
    }

    // The visual lighting: the lit state, the beam, the flame and the ignite cue. A no-op if already lit. TryLight does it once the win has
    // started; FinaleSequence.Play calls it too, so the cinematic always shows the lamp lit (the win is already under way: no WinNow here).
    public void EnsureLit()
    {
        if (Lit)
        {
            return;
        }

        Lit = true;
        ClearPrompt();
        igniting = true;
        if (lampBeam != null)
        {
            lampBeam.Begin(GreatLampBeam.DefaultRampSeconds);
        }

        StartCoroutine(Ignite());
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCue(SoundCues.BeaconIgnite, transform.position);
        }
    }

    public void Light()
    {
        TryLight();
    }

    void Update()
    {
        UpdateGlow();
        if (Lit)
        {
            Flicker();
            return;
        }

        ResolvePlayer();
        GameManager manager = GameManager.Instance;
        if (player == null || manager == null || manager.IsRoundOver || manager.IsDying)
        {
            ClearPrompt();
            return;
        }

        bool near = Vector3.Distance(transform.position, player.position) <= interactRange;
        if (!near)
        {
            ClearPrompt();
            return;
        }

        bool open = gate != null && gate.Open;
        string line = PromptFor(open);
        manager.SetCustomPrompt(this, line);
        promptShown = true;
        if (open && !manager.IsPaused)
        {
            if (GameInput.InteractPressed())
            {
                TryLight();
            }
        }
    }

    // The dark-lamp hint is rebuilt only when the lit count changes.
    string PromptFor(bool open)
    {
        if (open)
        {
            return TouchWording.GreatLampLight(TouchMode.Active);
        }

        int lit = gate != null ? gate.LitCount : 0;
        if (hint == null || lit != hintCount)
        {
            hintCount = lit;
            hint = "The great lamp is dark (" + lit + "/" + (gate != null ? gate.Required : 8) + " beacons)";
        }

        return hint;
    }

    void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        GameObject found = GameObject.FindGameObjectWithTag("Player");
        player = found != null ? found.transform : null;
    }

    void ClearPrompt()
    {
        if (promptShown && GameManager.Instance != null)
        {
            GameManager.Instance.SetCustomPrompt(this, null);
        }

        promptShown = false;
    }

    // The glass wakes with a slow pulse once the gate is open, and burns steadily when lit.
    void UpdateGlow()
    {
        // The faint amber at the foot of the door says the lamp is waiting; it goes out when the lamp is lit.
        if (doorGlow != null)
        {
            doorGlow.enabled = !Lit;
        }

        if (glass == null)
        {
            return;
        }

        float glow = Lit ? 1f : ((gate != null && gate.Open) ? 0.3f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.2f) : 0f);
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        glass.GetPropertyBlock(block);
        block.SetFloat(GlowId, glow);
        glass.SetPropertyBlock(block);

        if (beams != null)
        {
            beams.enabled = Lit && litScale > 0.01f;
            if (beamsBlock == null)
            {
                beamsBlock = new MaterialPropertyBlock();
            }

            bool low = GraphicsQuality.Current == GraphicsLevel.Low;
            beams.GetPropertyBlock(beamsBlock);
            beamsBlock.SetFloat(BeamIntensityId, litScale * Mathf.Lerp(0.8f, 1f, Mathf.PerlinNoise(Time.unscaledTime * 5f, 0.3f)) * (low ? BeaconVisual.BeamIntensityScaleLow : 1f));
            beamsBlock.SetFloat(BeamLengthId, low ? BeaconVisual.BeamLengthScaleLow : 1f);
            beams.SetPropertyBlock(beamsBlock);
        }
    }

    IEnumerator Ignite()
    {
        if (lampLight != null)
        {
            lampLight.enabled = true;
            lampLight.color = new Color(1f, 0.62f, 0.26f);
            lampLight.range = lightRange;
        }

        const float growTime = 1.4f;
        float elapsed = 0f;
        while (elapsed < growTime)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / growTime));
            float flash = Mathf.Sin(Mathf.Clamp01(elapsed / 0.4f) * Mathf.PI);
            litScale = u * (1f + flash * 0.5f * (1f - u));
            if (flameRoot != null)
            {
                flameRoot.localScale = Vector3.one * litScale;
            }

            if (lampLight != null)
            {
                lampLight.intensity = Mathf.Lerp(lightIntensity, lightIntensity * 3f, flash * (1f - u)) * u * FlareFactor();
            }

            yield return null;
        }

        litScale = 1f;
        if (flameRoot != null)
        {
            flameRoot.localScale = Vector3.one;
        }

        igniting = false;
    }

    void Flicker()
    {
        if (flameRoot != null && !igniting)
        {
            float sway = Mathf.Sin(Time.unscaledTime * 1.5f) * 6f;
            flameRoot.localRotation = Quaternion.Euler(0f, 0f, sway);
        }

        if (lampLight != null && !igniting)
        {
            lampLight.intensity = lightIntensity * Mathf.Lerp(0.85f, 1.12f, Mathf.PerlinNoise(Time.unscaledTime * 6f, 0.7f)) * FlareFactor();
        }
    }
}
}
