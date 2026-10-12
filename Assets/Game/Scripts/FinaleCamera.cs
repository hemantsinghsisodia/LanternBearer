using UnityEngine;

namespace LanternKeeper
{
// The finale camera. Begin() takes the main camera: it disables CameraFollow (the scene ends in the credits and then the menu, so it is
// never given back) and holds a 3/4 view of the lantern room. From FinaleTimeline.BeamStart it pulls back and up past the lighthouse
// (FinaleTimeline.CameraHeight: 6 m to 50 m above the lamp base) and then holds low, with the lighthouse and its sweeping beam in the
// foreground and the sea, the opening storm and the arc of distant islands behind it. The camera always sits on the far side of the
// lighthouse from where it looks, so it orbits as the view pans: across the arc during the Islands phase (each flare lands in frame in
// turn) and back to the dawn side for the sunrise. The pitch never goes steeper than MaxDownPitch and the lens is the gameplay camera's.
// Pure placement from the finale time: no state, no allocation.
public class FinaleCamera : MonoBehaviour
{
    public const float OrbitDegreesPerSecond = 10f;
    public const float StartRadius = 13f;
    public const float EndRadius = 80f;
    public const float FlameHeight = 11.9f;
    public const float MaxDownPitch = 20f;
    // Pitch while looking out at the horizon (degrees below level).
    public const float HoldPitch = 11f;
    public const float SunrisePitch = 1f;
    // The view stays on the lantern room while the camera pulls back, then swings out to the horizon.
    public const float LookShiftStart = 6f, LookShiftEnd = 16f;
    // The bearing (degrees clockwise from +Z, seen from the lamp) the camera looks towards: the middle of the arc, then across it, then the dawn.
    public const float PanFromBearing = 225f, PanToBearing = 310f, PanStart = 16f, PanEnd = 34f;
    // DawnSequence's sun sits on the bearing opposite its light direction (yaw 28): the sunrise looks that way.
    public const float SunBearing = 208f, SunPanEnd = 50f;
    const float FarLook = 300f;

    [SerializeField] Transform lamp;
    [SerializeField] Transform flame;
    [SerializeField] float seaY = 10.4f;

    Camera cam;
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

    // The bearing the camera looks towards.
    public static float LookBearing(float t)
    {
        // Before the pan the camera orbits at 10 degrees a second, arriving opposite PanFromBearing at PanStart, so the view turns with it.
        float across = t < PanStart
            ? PanFromBearing - OrbitDegreesPerSecond * (PanStart - t)
            : Mathf.Lerp(PanFromBearing, PanToBearing, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - PanStart) / (PanEnd - PanStart))));
        float dawn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - FinaleTimeline.SunriseStart + 2f) / (SunPanEnd - FinaleTimeline.SunriseStart + 2f)));
        return Mathf.Lerp(across, SunBearing, dawn);
    }

    // Bearing of the camera as seen from the lamp: always opposite the look direction (so the lighthouse stays centred horizontally).
    public static float OrbitBearing(float t)
    {
        return LookBearing(t) + 180f;
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
        Vector3 position = new Vector3(axis.x + Mathf.Sin(bearing) * radius, axis.y + FinaleTimeline.CameraHeight(t), axis.z + Mathf.Cos(bearing) * radius);

        // Look at the lantern room, then out along the look bearing at HoldPitch.
        float look = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - LookShiftStart) / (LookShiftEnd - LookShiftStart)));
        float lookBearing = LookBearing(t) * Mathf.Deg2Rad;
        // The sunrise looks level so the sky gradient fills the frame.
        float pitch = Mathf.Lerp(HoldPitch, SunrisePitch, Mathf.SmoothStep(0f, 1f, FinaleTimeline.Phase01(t, FinalePhase.Sunrise)));
        Vector3 far = position + new Vector3(Mathf.Sin(lookBearing), -Mathf.Tan(pitch * Mathf.Deg2Rad), Mathf.Cos(lookBearing)) * FarLook;
        Vector3 forward = Vector3.Lerp(flamePosition, far, look) - position;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        forward.Normalize();
        // Never steeper than MaxDownPitch.
        float minY = -Mathf.Sin(MaxDownPitch * Mathf.Deg2Rad);
        if (forward.y < minY)
        {
            float horizontal = Mathf.Sqrt(1f - minY * minY) / Mathf.Max(0.0001f, Mathf.Sqrt(forward.x * forward.x + forward.z * forward.z));
            forward = new Vector3(forward.x * horizontal, minY, forward.z * horizontal);
        }

        cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
    }
}
}
