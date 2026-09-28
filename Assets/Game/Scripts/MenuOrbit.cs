using UnityEngine;

namespace LanternKeeper
{
public class MenuOrbit : MonoBehaviour
{
    [SerializeField] Transform focus;
    [SerializeField] float radius = 22f;
    [SerializeField] float height = 9f;
    [SerializeField] float degreesPerSecond = 7f;
    [SerializeField] float lookHeight = 2.2f;

    float angle;

    public void Configure(Transform newFocus, float orbitRadius, float orbitHeight)
    {
        focus = newFocus;
        radius = orbitRadius;
        height = orbitHeight;
    }

    void LateUpdate()
    {
        if (focus == null)
        {
            return;
        }

        angle += degreesPerSecond * Time.deltaTime;
        float radians = angle * Mathf.Deg2Rad;
        Vector3 position = focus.position;
        position.x += Mathf.Sin(radians) * radius;
        position.y += height;
        position.z += Mathf.Cos(radians) * radius;
        transform.position = position;
        transform.LookAt(focus.position + Vector3.up * lookHeight);
    }
}
}
