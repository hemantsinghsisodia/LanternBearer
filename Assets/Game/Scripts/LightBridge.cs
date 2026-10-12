using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// An Island 5 light bridge across one gap of the spire. Lighting the beacon on the near side sends a beam arc (BeamSeconds) that
// kindles the bridge lantern on the far side, then the planks grow from both ends (GrowSeconds). The deck collider and the invisible
// side rails exist only when the bridge is Walkable, so a growing bridge cannot be stepped onto. A built bridge is permanent and counts
// as safe light: Lantern.InSafeLight, moths and Shades use AnySafe / PushOut / Repel next to the beacon checks.
// The summit span has no beacon trigger: SummitGate starts it once all beacons are lit and the columns have risen.
// The look is built here at runtime from the start and end points (the scene stores only those and the materials).
public class LightBridge : MonoBehaviour
{
    public const float BeamSeconds = BridgeTimings.BeamSeconds;
    public const float GrowSeconds = BridgeTimings.GrowSeconds;
    public const float TotalSeconds = BridgeTimings.TotalSeconds;
    const float KindleSeconds = 0.6f;
    const float SettleSeconds = 0.5f;
    const float DeckThickness = 0.5f;
    const float RailHeight = 1.6f;
    const float EndOverlap = 0.1f;
    const float JointOverlap = 0.35f;

    static readonly int GrowId = Shader.PropertyToID("_Grow");
    static readonly int KindleId = Shader.PropertyToID("_Kindle");
    static readonly int PlankCountId = Shader.PropertyToID("_PlankCount");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int BeamIntensityId = Shader.PropertyToID("_Intensity");
    static readonly int BeamLengthId = Shader.PropertyToID("_LenScale");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public static readonly List<LightBridge> All = new List<LightBridge>();

    public enum BridgePhase { Waiting, Beam, Grow, Done }

    public Beacon NearBeacon;
    // Null for the four required gaps: the far side is a bridge lantern (or the summit plateau).
    public Beacon FarBeacon;
    public bool Required = true;
    public bool SummitSpan;

    // The walkway line from the near lip to the far lip: two points for a straight span, more for the curved summit span.
    [SerializeField] Vector3[] points = new Vector3[0];
    [SerializeField] float width = 3.4f;
    [SerializeField] Transform lanternRoot;
    [SerializeField] Material deckMaterial;
    [SerializeField] Material emberMaterial;
    [SerializeField] Material beamMaterial;

    BridgePhase phase = BridgePhase.Waiting;
    float since;
    float kindle;
    float doneFor;
    bool built;
    BridgeZone[] zones = new BridgeZone[0];
    float length;
    float[] cumulative = new float[0];
    Renderer deckRenderer;
    MaterialPropertyBlock deckBlock;
    MaterialPropertyBlock glassBlock;
    Renderer glassRenderer;
    Renderer glowBeams;
    MaterialPropertyBlock beamsBlock;
    LineRenderer beam;
    ParticleSystem embers;
    Light underLight;
    Light lanternLight;
    Transform flameRoot;
    Collider[] colliders = new Collider[0];
    Vector3[] beamPoints = new Vector3[24];
    bool igniteCuePlayed;

    public Vector3[] Points => points;
    public Vector3 StartPoint => points[0];
    public Vector3 EndPoint => points[points.Length - 1];
    public float Length => length;
    // The point halfway along the walkway line.
    public Vector3 MidPoint => PointAt(0.5f);
    public float Width => width;
    public Transform LanternRoot => lanternRoot;
    public BridgePhase Phase => phase;
    public bool Triggered => phase != BridgePhase.Waiting;
    public float Elapsed => since;
    public Collider[] Colliders => colliders;
    public float Kindle => kindle;

    // 0 until the beam has arrived, then 0..1 over GrowSeconds.
    public float Progress01 => phase == BridgePhase.Waiting ? 0f : BridgeTimings.Progress(since);
    public bool Walkable => phase == BridgePhase.Done;

    // Bridges whose deck is built in the loaded scene (the list empties with the scene).
    public static int BuiltCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] != null && All[i].Walkable)
                {
                    count++;
                }
            }

            return count;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        All.Clear();
    }

    public void Configure(Vector3[] line, float deckWidth, Transform lantern, Material deck, Material ember, Material beamMat)
    {
        points = (Vector3[])line.Clone();
        width = deckWidth;
        lanternRoot = lantern;
        deckMaterial = deck;
        emberMaterial = ember;
        beamMaterial = beamMat;
    }

    void Awake()
    {
        Build();
    }

    void OnEnable()
    {
        if (!All.Contains(this))
        {
            All.Add(this);
        }

        Beacon.Lit += OnBeaconLit;
    }

    void OnDisable()
    {
        Beacon.Lit -= OnBeaconLit;
        All.Remove(this);
    }

    void OnBeaconLit(Beacon beacon)
    {
        if (!SummitSpan && beacon != null && beacon == NearBeacon)
        {
            Begin(false);
        }
    }

    // Starts the build. skipBeam starts at the grow (the summit span, after the columns).
    public void Begin(bool skipBeam)
    {
        if (phase != BridgePhase.Waiting)
        {
            return;
        }

        Build();
        since = skipBeam ? BeamSeconds : 0f;
        phase = skipBeam ? BridgePhase.Grow : BridgePhase.Beam;
        if (!skipBeam && AudioManager.Instance != null && NearBeacon != null)
        {
            AudioManager.Instance.PlayCue(SoundCues.BeaconWhoosh, NearBeacon.transform.position);
        }

        if (skipBeam)
        {
            StartGrow();
        }
    }

    // Used by SummitGate once it has opened.
    public static void BeginSummit()
    {
        for (int i = 0; i < All.Count; i++)
        {
            if (All[i] != null && All[i].SummitSpan)
            {
                All[i].Begin(true);
            }
        }
    }

    // Safe light: true inside the zone of any built bridge.
    public static bool AnySafe(Vector3 position)
    {
        for (int i = 0; i < All.Count; i++)
        {
            LightBridge bridge = All[i];
            if (bridge != null && bridge.Walkable && bridge.ZoneContains(position))
            {
                return true;
            }
        }

        return false;
    }

    public static Vector3 PushOut(Vector3 position)
    {
        for (int i = 0; i < All.Count; i++)
        {
            LightBridge bridge = All[i];
            if (bridge != null && bridge.Walkable)
            {
                position = bridge.ZonePushOut(position);
            }
        }

        return position;
    }

    public static Vector3 Repel(Vector3 position)
    {
        Vector3 push = Vector3.zero;
        for (int i = 0; i < All.Count; i++)
        {
            LightBridge bridge = All[i];
            if (bridge != null && bridge.Walkable)
            {
                push += bridge.ZoneRepel(position);
            }
        }

        return push;
    }

    void Update()
    {
        if (phase == BridgePhase.Waiting)
        {
            if (!SummitSpan && NearBeacon != null && NearBeacon.IsLit)
            {
                Begin(false);
            }

            return;
        }

        float dt = Time.deltaTime;
        if (phase != BridgePhase.Done)
        {
            since += dt;
        }
        else
        {
            doneFor += dt;
        }

        if (phase == BridgePhase.Beam)
        {
            UpdateBeam();
            if (since >= BeamSeconds)
            {
                phase = BridgePhase.Grow;
                StartGrow();
            }
        }

        if (phase == BridgePhase.Grow && since >= TotalSeconds)
        {
            Finish();
        }

        UpdateLantern(dt);
        UpdateDeck();
    }

    void StartGrow()
    {
        HideBeam();
        if (embers != null && !embers.isPlaying)
        {
            embers.Play();
        }

        if (lanternRoot != null && !igniteCuePlayed)
        {
            igniteCuePlayed = true;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayCue(SoundCues.BeaconIgnite, lanternRoot.position);
            }
        }
    }

    void Finish()
    {
        since = TotalSeconds;
        phase = BridgePhase.Done;
        doneFor = 0f;
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = true;
        }

        if (underLight != null && GraphicsQuality.Current != GraphicsLevel.Low)
        {
            underLight.enabled = true;
        }
    }

    void UpdateDeck()
    {
        if (deckRenderer == null)
        {
            return;
        }

        float grow = Progress01;
        float settle = phase == BridgePhase.Done ? 1f + 0.6f * Mathf.Clamp01(1f - doneFor / SettleSeconds) : 1f;
        if (deckBlock == null)
        {
            deckBlock = new MaterialPropertyBlock();
        }

        deckRenderer.GetPropertyBlock(deckBlock);
        deckBlock.SetFloat(GrowId, grow);
        deckBlock.SetFloat(KindleId, grow > 0f ? settle : 0f);
        deckBlock.SetFloat(PlankCountId, Mathf.Max(2f, Mathf.Round(length / 0.9f)));
        deckRenderer.SetPropertyBlock(deckBlock);
        deckRenderer.enabled = grow > 0f;

        if (underLight != null && phase == BridgePhase.Grow && GraphicsQuality.Current != GraphicsLevel.Low)
        {
            underLight.enabled = grow > 0.02f;
            underLight.intensity = 2.4f * grow;
        }
        else if (underLight != null && phase == BridgePhase.Done)
        {
            float noise = Mathf.PerlinNoise(Time.time * 3.1f + MidPoint.x, 0.4f);
            underLight.intensity = 2.4f * Mathf.Lerp(0.85f, 1.1f, noise) * settle;
        }
    }

    void UpdateBeam()
    {
        if (beam == null)
        {
            return;
        }

        float head = BridgeTimings.BeamHead(since);
        float tail = Mathf.Max(0f, head - 0.45f);
        Vector3 origin = BeamOrigin();
        Vector3 target = BeamTarget();
        float lift = 2.5f + Vector3.Distance(origin, target) * 0.12f;
        int count = beamPoints.Length;
        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Lerp(tail, head, i / (float)(count - 1));
            beamPoints[i] = Vector3.Lerp(origin, target, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * lift);
        }

        beam.positionCount = count;
        beam.SetPositions(beamPoints);
        beam.enabled = true;
    }

    void HideBeam()
    {
        if (beam != null)
        {
            beam.positionCount = 0;
            beam.enabled = false;
        }
    }

    Vector3 BeamOrigin()
    {
        return NearBeacon != null ? NearBeacon.transform.position + Vector3.up * 2.4f : StartPoint + Vector3.up * 2.4f;
    }

    Vector3 BeamTarget()
    {
        if (FarBeacon != null)
        {
            return FarBeacon.transform.position + Vector3.up * 2.4f;
        }

        if (flameRoot != null)
        {
            return flameRoot.position;
        }

        return EndPoint + Vector3.up * 1.4f;
    }

    // The far lantern kindles when the beam arrives.
    void UpdateLantern(float dt)
    {
        if (lanternRoot == null)
        {
            return;
        }

        bool arrived = phase == BridgePhase.Grow || phase == BridgePhase.Done;
        if (arrived)
        {
            kindle = Mathf.Min(1f, kindle + dt / KindleSeconds);
        }

        float eased = Mathf.SmoothStep(0f, 1f, kindle);
        if (flameRoot != null)
        {
            flameRoot.localScale = Vector3.one * eased;
            if (eased > 0.01f)
            {
                float sway = Mathf.Sin(Time.time * 1.7f + StartPoint.x) * 8f;
                flameRoot.localRotation = Quaternion.Euler(0f, 0f, sway);
            }
        }

        if (lanternLight != null)
        {
            lanternLight.enabled = eased > 0.01f;
            float noise = Mathf.PerlinNoise(Time.time * 7.5f, StartPoint.z * 0.1f);
            lanternLight.intensity = 3.0f * eased * Mathf.Lerp(0.8f, 1.15f, noise);
        }

        if (glassRenderer != null)
        {
            if (glassBlock == null)
            {
                glassBlock = new MaterialPropertyBlock();
            }

            glassRenderer.GetPropertyBlock(glassBlock);
            glassBlock.SetFloat(GlowId, eased);
            glassRenderer.SetPropertyBlock(glassBlock);
        }

        if (glowBeams != null)
        {
            glowBeams.enabled = eased > 0.01f;
            if (beamsBlock == null)
            {
                beamsBlock = new MaterialPropertyBlock();
            }

            glowBeams.GetPropertyBlock(beamsBlock);
            beamsBlock.SetFloat(BeamIntensityId, eased * Mathf.Lerp(0.8f, 1f, Mathf.PerlinNoise(Time.time * 5f, StartPoint.x)) * (GraphicsQuality.Current == GraphicsLevel.Low ? BeaconVisual.BeamIntensityScaleLow : 1f));
            beamsBlock.SetFloat(BeamLengthId, GraphicsQuality.Current == GraphicsLevel.Low ? BeaconVisual.BeamLengthScaleLow : 1f);
            glowBeams.SetPropertyBlock(beamsBlock);
        }
    }

    // Everything the bridge shows or collides with, from the walkway line. Safe to call twice.
    void Build()
    {
        if (built)
        {
            return;
        }

        built = true;
        if (points == null || points.Length < 2)
        {
            points = new[] { transform.position, transform.position + Vector3.forward };
        }

        ComputeLengths();
        zones = new BridgeZone[points.Length - 1];
        for (int i = 0; i < zones.Length; i++)
        {
            zones[i] = new BridgeZone { start = points[i], end = points[i + 1], halfWidth = width * 0.5f };
        }

        BuildDeck();
        BuildColliders();
        BuildEmbers();
        BuildBeam();
        BuildLights();
        FindLantern();
        deckRenderer.enabled = false;
    }

    void ComputeLengths()
    {
        if (points == null || points.Length == 0)
        {
            cumulative = new float[0];
            length = 0f;
            return;
        }

        cumulative = new float[points.Length];
        for (int i = 1; i < points.Length; i++)
        {
            cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }

        length = cumulative[cumulative.Length - 1];
    }

    // The point at this fraction (0..1) of the walkway line's length.
    public Vector3 PointAt(float fraction)
    {
        if (points == null || points.Length == 0)
        {
            return transform.position;
        }

        if (cumulative.Length != points.Length)
        {
            ComputeLengths();
        }

        float target = Mathf.Clamp01(fraction) * length;
        for (int i = 1; i < points.Length; i++)
        {
            if (target <= cumulative[i] || i == points.Length - 1)
            {
                float span = cumulative[i] - cumulative[i - 1];
                float t = span > 1e-5f ? Mathf.Clamp01((target - cumulative[i - 1]) / span) : 0f;
                return Vector3.Lerp(points[i - 1], points[i], t);
            }
        }

        return points[0];
    }

    public bool ZoneContains(Vector3 position)
    {
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].Contains(position))
            {
                return true;
            }
        }

        return false;
    }

    Vector3 ZonePushOut(Vector3 position)
    {
        // Leaving one capsule can land inside the next one, so go round twice.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                position = zones[i].PushOut(position);
            }
        }

        return position;
    }

    Vector3 ZoneRepel(Vector3 position)
    {
        Vector3 push = Vector3.zero;
        for (int i = 0; i < zones.Length; i++)
        {
            push += zones[i].Repel(position);
        }

        return Vector3.ClampMagnitude(push, 3.2f);
    }

    void BuildDeck()
    {
        GameObject deck = new GameObject("Deck");
        deck.transform.SetParent(transform, false);
        deck.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        MeshFilter filter = deck.AddComponent<MeshFilter>();
        filter.sharedMesh = BuildRibbon(1.8f, 0.04f, "LightBridgeDeck", true);
        deckRenderer = deck.AddComponent<MeshRenderer>();
        Material material = deckMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("LanternKeeper/LightBridge");
            if (shader != null)
            {
                material = new Material(shader);
                material.name = "LightBridge (runtime)";
            }
        }

        deckRenderer.sharedMaterial = material;
        NoShadows(deckRenderer);
    }

    // A ribbon along the walkway line in world space, widthFactor x the deck width, lifted above the deck. UV x is 0..1 along the
    // length, y is 0..1 across the deck (-0.4..1.4 for the 1.8 wide ribbon with its under-glow).
    Mesh BuildRibbon(float widthFactor, float lift, string meshName, bool withUv)
    {
        int count = points.Length;
        Vector3[] vertices = new Vector3[count * 2];
        Vector2[] uv = new Vector2[count * 2];
        int[] triangles = new int[(count - 1) * 6];
        float half = width * 0.5f * widthFactor;
        Bounds bounds = new Bounds(points[0], Vector3.zero);
        for (int i = 0; i < count; i++)
        {
            Vector3 tangent = Vector3.zero;
            if (i > 0)
            {
                tangent += (points[i] - points[i - 1]).normalized;
            }

            if (i < count - 1)
            {
                tangent += (points[i + 1] - points[i]).normalized;
            }

            Vector3 side = Vector3.Cross(Vector3.up, tangent);
            side.y = 0f;
            side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
            Vector3 centre = points[i] + Vector3.up * lift;
            vertices[i * 2] = centre - side * half;
            vertices[i * 2 + 1] = centre + side * half;
            float along = length > 1e-5f ? cumulative[i] / length : 0f;
            float edge = 0.5f * widthFactor;
            uv[i * 2] = new Vector2(along, 0.5f - edge);
            uv[i * 2 + 1] = new Vector2(along, 0.5f + edge);
            bounds.Encapsulate(vertices[i * 2]);
            bounds.Encapsulate(vertices[i * 2 + 1]);
        }

        for (int i = 0; i < count - 1; i++)
        {
            int a = i * 2;
            int t = i * 6;
            triangles[t] = a;
            triangles[t + 1] = a + 2;
            triangles[t + 2] = a + 1;
            triangles[t + 3] = a + 1;
            triangles[t + 4] = a + 2;
            triangles[t + 5] = a + 3;
        }

        Mesh mesh = new Mesh();
        mesh.name = meshName;
        mesh.vertices = vertices;
        if (withUv)
        {
            mesh.uv = uv;
        }

        mesh.triangles = triangles;
        bounds.Expand(new Vector3(0f, 3f, 0f));
        mesh.bounds = bounds;
        return mesh;
    }

    void BuildColliders()
    {
        GameObject root = new GameObject("Collision");
        root.transform.SetParent(transform, false);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        List<Collider> list = new List<Collider>();
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
            float segment = Vector3.Distance(a, b);
            // Neighbouring boxes overlap at the joints so a bend leaves no gap; the two ends only reach just past the lips.
            float before = i == 0 ? EndOverlap : JointOverlap;
            float after = i == points.Length - 2 ? EndOverlap : JointOverlap;
            float boxLength = segment + before + after;
            float shift = (after - before) * 0.5f;
            GameObject seg = new GameObject("Seg" + i);
            seg.transform.SetParent(root.transform, false);
            seg.transform.SetPositionAndRotation(0.5f * (a + b), Quaternion.LookRotation((b - a).normalized, Vector3.up));
            BoxCollider deck = seg.AddComponent<BoxCollider>();
            deck.size = new Vector3(width, DeckThickness, boxLength);
            deck.center = new Vector3(0f, -DeckThickness * 0.5f, shift);
            list.Add(deck);
            list.Add(AddRail(seg.transform, "RailL" + i, -(width * 0.5f + 0.1f), boxLength, shift));
            list.Add(AddRail(seg.transform, "RailR" + i, width * 0.5f + 0.1f, boxLength, shift));
        }

        colliders = list.ToArray();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    static BoxCollider AddRail(Transform segment, string railName, float x, float boxLength, float shift)
    {
        GameObject rail = new GameObject(railName);
        rail.transform.SetParent(segment, false);
        BoxCollider box = rail.AddComponent<BoxCollider>();
        box.size = new Vector3(0.2f, RailHeight, boxLength);
        box.center = new Vector3(x, RailHeight * 0.5f - 0.3f, shift);
        return box;
    }


    void BuildEmbers()
    {
        GameObject host = new GameObject("Embers");
        host.transform.SetParent(transform, false);
        host.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        embers = host.AddComponent<ParticleSystem>();
        embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = embers.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.694f, 0.361f, 1f), new Color(1f, 0.827f, 0.541f, 1f));
        main.maxParticles = 80;
        ParticleSystem.EmissionModule emission = embers.emission;
        emission.rateOverTime = 16f;
        ParticleSystem.ShapeModule shape = embers.shape;
        // Emits from a world-space ribbon over the deck (the same line as the deck mesh, a little narrower and higher).
        shape.shapeType = ParticleSystemShapeType.Mesh;
        shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
        shape.mesh = BuildRibbon(0.9f, 0.15f, "LightBridgeEmberShape", false);
        ParticleSystem.VelocityOverLifetimeModule velocity = embers.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        ParticleSystem.ColorOverLifetimeModule colour = embers.colorOverLifetime;
        colour.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        ParticleSystem.SizeOverLifetimeModule size = embers.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
        ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = emberMaterial != null ? emberMaterial : FallbackAdditive("BridgeEmber");
        NoShadows(renderer);
        ParticleQuality.ApplyTo(embers);
    }

    void BuildBeam()
    {
        if (SummitSpan)
        {
            return;
        }

        GameObject host = new GameObject("Beam");
        host.transform.SetParent(transform, false);
        beam = host.AddComponent<LineRenderer>();
        beam.useWorldSpace = true;
        beam.positionCount = 0;
        beam.numCapVertices = 4;
        beam.alignment = LineAlignment.View;
        beam.textureMode = LineTextureMode.Stretch;
        beam.widthMultiplier = 0.55f;
        beam.widthCurve = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.7f, 0.7f), new Keyframe(1f, 1f));
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.694f, 0.361f), 0f), new GradientColorKey(new Color(1f, 0.827f, 0.541f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(1f, 1f) });
        beam.colorGradient = gradient;
        beam.sharedMaterial = beamMaterial != null ? beamMaterial : FallbackAdditive("BridgeBeam");
        NoShadows(beam);
        beam.enabled = false;
    }

    void BuildLights()
    {
        GameObject host = new GameObject("UnderGlow");
        host.transform.SetParent(transform, false);
        host.transform.position = MidPoint + Vector3.down * 1.6f;
        underLight = host.AddComponent<Light>();
        underLight.type = LightType.Point;
        underLight.color = new Color(1f, 0.58f, 0.22f);
        underLight.range = 9f;
        underLight.intensity = 0f;
        underLight.shadows = LightShadows.None;
        underLight.enabled = false;
    }

    void FindLantern()
    {
        if (lanternRoot == null)
        {
            return;
        }

        flameRoot = lanternRoot.Find("FlameRoot");
        if (flameRoot != null)
        {
            flameRoot.localScale = Vector3.zero;
        }

        lanternLight = lanternRoot.GetComponentInChildren<Light>(true);
        if (lanternLight != null)
        {
            lanternLight.enabled = false;
        }

        Transform glass = lanternRoot.Find("BeaconGlass");
        glassRenderer = glass != null ? glass.GetComponent<Renderer>() : null;
        Transform beams = lanternRoot.Find("BeaconBeams");
        glowBeams = beams != null ? beams.GetComponent<Renderer>() : null;
        if (glowBeams != null)
        {
            glowBeams.enabled = false;
        }
    }

    static void NoShadows(Renderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    // Used only when the scene did not wire a material (a bare test scene).
    static Material FallbackAdditive(string materialName)
    {
        Shader shader = Shader.Find("LanternKeeper/AdditiveUnlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material material = new Material(shader);
        material.name = materialName + " (runtime)";
        material.SetColor(BaseColorId, new Color(1f, 0.75f, 0.4f, 0.8f));
        return material;
    }
}
}
