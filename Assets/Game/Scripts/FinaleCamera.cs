using UnityEngine;

namespace LanternKeeper
{
// The finale camera. Begin() takes the main camera: it disables CameraFollow (the scene ends in the credits and then the menu, so it is
// never given back) and holds a 3/4 view of the lantern room. From FinaleTimeline.BeamStart the camera pulls back and rises along
// FinaleTimeline.CameraHeight, orbiting at 10 degrees a second, to frame the island and the arc of distant islands; after the Islands
// phase it drifts slowly and holds its last frame when the finale ends. Pure placement from the finale time: no state, no allocation.
public class FinaleCamera : MonoBehaviour
{
    public const float OrbitDegreesPerSecond = 10f;
    public const float HoldDegreesPerSecond = 2f;
    public const float StartRadius = 13f;
    public const float EndRadius = 150f;
    public const float FlameHeight = 11.9f;
    // The arc of silhouettes is centred on this bearing (FinaleLayout: 200..335 degrees clockwise from +Z).
    public const float ArcBearing = 267.5f;
    // How far the view swings from the lantern room towards the silhouette arc (1 = fully at the arc): the island stays in the lower frame.
    public const float FarLookShare = 0.5f;
    // The view stays on the lantern room while the camera pulls back, then tilts up towards the arc as the silhouettes appear.
    public const float LookShiftStart = 12f, LookShiftEnd = 24f;
    // The view widens while the camera pulls back so the arc of islands fits, then pans across it: island 1 (200 degrees) at its flare, island 4 (335) last.
    public const float WideFieldOfView = 78f;
    public const float PanFromBearing = 235f, PanToBearing = 290f, PanStart = 16f, PanEnd = 36f;

    [SerializeField] Transform lamp;
    [SerializeField] Transform flame;
    [SerializeField] float seaY = 10.4f;
    [SerializeField] float farLookDistance = 330f;
    [SerializeField] float farLookHeight = 100f;

    Camera cam;
    float startFieldOfView = 58f;
    FinaleSequence sequence;
    bool active;

    public bool Active => active;

    public void Configure(Transform lampRoot, Transform flameRoot, float sea)
    {
        lamp = lampRoot;
        flame = flameRoot;
        seaY = sea;
    }

    // Takes the main camera. Returns false if there is none.
    public bool Begin(FinaleSequence owner)
    {
        cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow != null)
        {
            follow.enabled = false;
        }

        startFieldOfView = cam.fieldOfView;
        sequence = owner;
        active = true;
        Place(0f);
        return true;
    }

    public void End()
    {
        active = false;
    }

    void LateUpdate()
    {
        if (!active || cam == null || sequence == null)
        {
            return;
        }

        Place(sequence.Elapsed);
    }

    // Bearing (degrees clockwise from +Z) of the camera as seen from the lamp.
    public static float OrbitBearing(float t)
    {
        // The pull-back orbits at 10 degrees a second; it starts on the side of the silhouette arc and ends opposite it, looking across the
        // island at the arc. After the Islands phase it drifts at 2 degrees a second.
        float start = ArcBearing + 180f - OrbitDegreesPerSecond * FinaleTimeline.IslandsStart;
        float orbit = OrbitDegreesPerSecond * Mathf.Min(t, FinaleTimeline.IslandsStart);
        float hold = HoldDegreesPerSecond * Mathf.Max(0f, t - FinaleTimeline.IslandsStart);
        return start + orbit + hold;
    }

    // The bearing (from the lamp) the far look point lies on: the middle of the arc, panning across it during the Islands phase.
    public static float LookBearing(float t)
    {
        return Mathf.Lerp(PanFromBearing, PanToBearing, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - PanStart) / (PanEnd - PanStart))));
    }

    // Horizontal distance from the lamp axis: close at the lantern room, out to EndRadius by the end of the Beam phase.
    public static float OrbitRadius(float t)
    {
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - FinaleTimeline.BeamStart * 0.5f) / (FinaleTimeline.IslandsStart - FinaleTimeline.BeamStart * 0.5f)));
        return Mathf.Lerp(StartRadius, EndRadius, k);
    }

    public void Place(float t)
    {
        Vector3 axis = lamp != null ? lamp.position : Vector3.zero;
        Vector3 flamePosition = flame != null ? flame.position : axis + Vector3.up * FlameHeight;
        float bearing = OrbitBearing(t) * Mathf.Deg2Rad;
        float radius = OrbitRadius(t);
        Vector3 offset = new Vector3(Mathf.Sin(bearing), 0f, Mathf.Cos(bearing)) * radius;
        float height = axis.y + FinaleTimeline.CameraHeight(t);
        Vector3 position = new Vector3(axis.x + offset.x, height, axis.z + offset.z);

        // Look at the lantern room, easing towards the middle of the silhouette arc as the camera rises.
        float look = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - LookShiftStart) / (LookShiftEnd - LookShiftStart)));
        float arc = LookBearing(t) * Mathf.Deg2Rad;
        Vector3 far = new Vector3(axis.x + Mathf.Sin(arc) * farLookDistance, seaY + farLookHeight, axis.z + Mathf.Cos(arc) * farLookDistance);
        Vector3 target = Vector3.Lerp(flamePosition, far, look * FarLookShare);
        Vector3 forward = target - position;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float widen = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - FinaleTimeline.BeamStart) / (FinaleTimeline.IslandsStart - FinaleTimeline.BeamStart)));
        cam.fieldOfView = Mathf.Lerp(startFieldOfView, WideFieldOfView, widen);
        cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward.normalized, Vector3.up));
    }
}
}
