using UnityEngine;

namespace LanternKeeper
{
public class LanternSway : MonoBehaviour
{
    [SerializeField] Transform trackedBody;
    [SerializeField] float minDegrees = 4f;
    [SerializeField] float maxDegrees = 16f;

    // Sine swing plus a kick impulse never exceeds this many degrees.
    const float TotalLimitDegrees = 24f;
    const float KickHalfLife = 0.35f;

    Vector3 lastPosition;
    float swing;
    float kick;

    // Adds a decaying impulse (a stagger, a stolen step) on top of the sine swing.
    public void Kick(float degrees)
    {
        kick = Mathf.Clamp(kick + degrees, -TotalLimitDegrees, TotalLimitDegrees);
    }

    void Awake()
    {
        if (trackedBody == null)
        {
            trackedBody = transform.root;
        }

        if (trackedBody != null)
        {
            lastPosition = trackedBody.position;
        }
    }

    void LateUpdate()
    {
        float speed = 0f;
        if (trackedBody != null)
        {
            Vector3 delta = trackedBody.position - lastPosition;
            delta.y = 0f;
            speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            lastPosition = trackedBody.position;
        }

        float amount = Mathf.Lerp(minDegrees, maxDegrees, Mathf.Clamp01(speed / 8f));
        float rate = 2.2f + Mathf.Clamp(speed, 0f, 8f) * 0.35f;
        kick *= Mathf.Pow(0.5f, Time.deltaTime / KickHalfLife);
        swing = Mathf.Clamp(Mathf.Sin(Time.time * rate) * amount + kick, -TotalLimitDegrees, TotalLimitDegrees);
        // A lantern hangs from its ring toward gravity in every pose: rest is world down, yaw follows the keeper's facing.
        Vector3 facing = trackedBody != null ? trackedBody.forward : transform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
        {
            facing = Vector3.forward;
        }

        transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up) * Quaternion.Euler(swing, 0f, swing * 0.55f);
    }
}
}
