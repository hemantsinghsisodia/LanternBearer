using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
public class BeaconCompass : MonoBehaviour
{
    [SerializeField] RectTransform arrow;
    [SerializeField] Text distanceText;
    [SerializeField] Transform player;

    Camera view;
    int shownMeters = int.MinValue;
    bool playerResolved;
    static bool loggedFallback;

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

    void Start()
    {
        ResolvePlayer();
        view = Camera.main;
    }

    void ResolvePlayer()
    {
        if (player != null || playerResolved)
        {
            return;
        }

        playerResolved = true;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }

        if (!loggedFallback)
        {
            loggedFallback = true;
            Debug.LogWarning("BeaconCompass player was not wired. Resolved once.", this);
        }
    }

    void Update()
    {
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
        if (arrow.gameObject.activeSelf != show)
        {
            arrow.gameObject.SetActive(show);
        }

        if (distanceText != null && distanceText.gameObject.activeSelf != show)
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
        if (distanceText == null)
        {
            return;
        }

        int meters = Mathf.RoundToInt(nearestDistance);
        if (meters == shownMeters)
        {
            return;
        }

        shownMeters = meters;
        string label = meters.ToString() + "m";
        if (distanceText.text != label)
        {
            distanceText.text = label;
        }
    }
}
}
