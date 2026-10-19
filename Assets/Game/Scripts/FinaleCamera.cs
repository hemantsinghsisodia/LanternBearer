using UnityEngine;

namespace LanternKeeper
{
// The finale camera. Begin() takes the main camera: it disables CameraFollow (the scene ends in the credits and then the menu, so it is
// never given back). From then on the camera flies the authored FinaleFlightPath (a Catmull-Rom spline through waypoints around the lamp):
// the lantern room, a rise and swing round the lighthouse as the storm breaks, a glide along the island chain with each island centred as its
// beacons flare, then back to the lighthouse against the rising sun and a slow push-in hold. The pitch never goes steeper than MaxDownPitch
// and the lens is the gameplay camera's. Pure placement from the finale time: no state, no allocation.
public class FinaleCamera : MonoBehaviour
{
    public const float FlameHeight = FinaleFlightPath.FlameHeight;
    public const float MaxDownPitch = FinaleFlightPath.MaxDownPitch;
    // Island 5's dawn sun (_MoonDir) is at bearing 35: the sunrise looks that way.
    public const float SunBearing = FinaleFlightPath.SunBearing;

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

    // The bearing of the sky the camera looks through (the cloud window is centred on it).
    public static float LookBearing(float t)
    {
        return FinaleFlightPath.LookBearing(t);
    }

    public void Place(float t)
    {
        if (cam == null)
        {
            return;
        }

        Vector3 axis = lamp != null ? lamp.position : Vector3.zero;
        Vector3 position = axis + FinaleFlightPath.Position(t);
        Vector3 forward = axis + FinaleFlightPath.LookTarget(t) - position;
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
