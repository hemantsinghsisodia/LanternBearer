using UnityEngine;

namespace LanternKeeper
{
public class LanternSway : MonoBehaviour
{
    [SerializeField] Transform trackedBody;
    [SerializeField] float minDegrees = 4f;
    [SerializeField] float maxDegrees = 16f;

    Vector3 lastPosition;
    float swing;

    void Awake()
    {
        if (trackedBody == null && transform.parent != null)
        {
            trackedBody = transform.parent;
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
        swing = Mathf.Sin(Time.time * rate) * amount;
        transform.localRotation = Quaternion.Euler(swing, 0f, swing * 0.55f);
    }
}
}
