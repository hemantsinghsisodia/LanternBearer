using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
public class BeaconCompass : MonoBehaviour
{
    [SerializeField] RectTransform arrow;
    [SerializeField] Text distanceText;

    Transform player;

    void Awake()
    {
        if (arrow == null)
        {
            Transform child = transform.Find("Arrow");
            if (child != null)
            {
                arrow = child as RectTransform;
            }
        }

        if (distanceText == null)
        {
            Transform child = transform.Find("Distance");
            if (child != null)
            {
                distanceText = child.GetComponent<Text>();
            }
        }
    }

    void Update()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (player == null || arrow == null)
        {
            return;
        }

        Beacon nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < Beacon.All.Count; i++)
        {
            Beacon beacon = Beacon.All[i];
            if (beacon == null || beacon.IsLit)
            {
                continue;
            }

            float distance = Vector3.Distance(player.position, beacon.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = beacon;
            }
        }

        bool show = nearest != null && (GameManager.Instance == null || !GameManager.Instance.IsRoundOver);
        arrow.gameObject.SetActive(show);
        if (distanceText != null)
        {
            distanceText.gameObject.SetActive(show);
        }

        if (!show)
        {
            return;
        }

        Vector3 to = nearest.transform.position - player.position;
        to.y = 0f;
        Vector3 forward = player.forward;
        Camera view = Camera.main;
        if (view != null)
        {
            forward = view.transform.forward;
            forward.y = 0f;
        }

        if (to.sqrMagnitude < 0.001f || forward.sqrMagnitude < 0.001f)
        {
            return;
        }

        float angle = Vector3.SignedAngle(forward, to, Vector3.up);
        arrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
        if (distanceText != null)
        {
            distanceText.text = Mathf.RoundToInt(nearestDistance) + "m";
        }
    }
}
}
