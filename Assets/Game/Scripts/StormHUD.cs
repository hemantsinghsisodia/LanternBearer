using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// Storm widgets for Island 4, created by HUD only when Wind or Lightning exists:
// a wind arrow with a strength arc in the tide gauge slot, a thunder glyph, and a dark edge pulse on a Shade steal.
public class StormHUD : MonoBehaviour
{
    static readonly Color WindColor = new Color(0.78f, 0.9f, 1f, 1f);
    static readonly Color ThunderColor = new Color(0.82f, 0.88f, 1f, 1f);

    RectTransform windRoot;
    RectTransform arrow;
    CanvasGroup windGroup;
    Image arc;
    TMP_Text windText;
    RectTransform thunderRoot;
    CanvasGroup thunderGroup;
    LowFuelFX edgeFx;
    bool edgeSearched;
    bool subscribed;
    Transform cameraTransform;

    void OnEnable()
    {
        if (!subscribed)
        {
            subscribed = true;
            Shade.Stole += OnStole;
        }
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    void Unsubscribe()
    {
        if (subscribed)
        {
            subscribed = false;
            Shade.Stole -= OnStole;
        }
    }

    void Start()
    {
        if (Wind.Instance != null)
        {
            BuildWind();
        }

        if (Lightning.Instance != null)
        {
            BuildThunder();
        }
    }

    void Update()
    {
        UpdateWind();
        UpdateThunder();
    }

    void OnStole(float amount)
    {
        if (!edgeSearched)
        {
            edgeSearched = true;
            edgeFx = FindAnyObjectByType<LowFuelFX>();
        }

        if (edgeFx != null)
        {
            edgeFx.PulseEdge(1f);
        }
    }

    // The arrow points where the wind pushes, as seen from the camera. The arc fills with gust strength.
    void UpdateWind()
    {
        Wind wind = Wind.Instance;
        if (windRoot == null || wind == null)
        {
            return;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        forward.y = 0f;
        Vector3 dir = wind.Direction;
        dir.y = 0f;
        if (forward.sqrMagnitude > 0.0001f && dir.sqrMagnitude > 0.0001f)
        {
            arrow.localEulerAngles = new Vector3(0f, 0f, -Vector3.SignedAngle(forward, dir, Vector3.up));
        }

        WindPhase phase = wind.Phase;
        float alpha = 0.3f;
        string label = "Wind";
        if (phase == WindPhase.Warning)
        {
            alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
            label = "Gust!";
        }
        else if (phase == WindPhase.Gust)
        {
            alpha = 1f;
            label = "Gust";
        }

        arc.fillAmount = phase == WindPhase.Gust ? wind.Strength01 : 0f;
        windGroup.alpha = wind.Sheltered ? alpha * 0.4f : alpha;
        if (windText.text != label)
        {
            windText.text = label;
        }
    }

    void UpdateThunder()
    {
        Lightning lightning = Lightning.Instance;
        if (thunderRoot == null || lightning == null)
        {
            return;
        }

        bool show = lightning.Phase == LightningPhase.Thunder;
        thunderGroup.alpha = show ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 14f)) : 0f;
    }

    void BuildWind()
    {
        GameObject root = new GameObject("WindGauge", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        windRoot = root.GetComponent<RectTransform>();
        windGroup = root.GetComponent<CanvasGroup>();
        windGroup.blocksRaycasts = false;
        windGroup.interactable = false;
        windRoot.anchorMin = new Vector2(0f, 1f);
        windRoot.anchorMax = new Vector2(0f, 1f);
        windRoot.pivot = new Vector2(0f, 1f);
        windRoot.anchoredPosition = new Vector2(36f, -228f);
        windRoot.sizeDelta = new Vector2(260f, 44f);

        MakeImage(windRoot, "WindBack", HUD.Circle(), new Vector2(22f, -22f), new Vector2(40f, 40f), new Color(0.05f, 0.1f, 0.14f, 0.8f));
        arc = MakeImage(windRoot, "WindArc", HUD.Circle(), new Vector2(22f, -22f), new Vector2(40f, 40f), new Color(0.55f, 0.8f, 1f, 0.65f));
        arc.type = Image.Type.Filled;
        arc.fillMethod = Image.FillMethod.Radial360;
        arc.fillOrigin = (int)Image.Origin360.Top;
        arc.fillAmount = 0f;

        GameObject arrowObject = new GameObject("WindArrow", typeof(RectTransform));
        arrowObject.transform.SetParent(windRoot, false);
        arrow = arrowObject.GetComponent<RectTransform>();
        arrow.anchorMin = new Vector2(0f, 1f);
        arrow.anchorMax = new Vector2(0f, 1f);
        arrow.anchoredPosition = new Vector2(22f, -22f);
        arrow.sizeDelta = new Vector2(40f, 40f);
        Image shaft = MakeImage(arrow, "Shaft", HUD.White(), new Vector2(0f, -2f), new Vector2(4f, 20f), WindColor);
        shaft.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        shaft.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        TMP_Text head = HUD.MakeRuntimeText(arrow, "Head", "^", 28, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 9f), new Vector2(30f, 30f), WindColor, TextAlignmentOptions.Center);
        head.fontStyle = FontStyles.Bold;

        windText = HUD.MakeRuntimeText(windRoot, "WindText", "Wind", 20, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(140f, 28f), WindColor, TextAlignmentOptions.MidlineLeft);
        windText.rectTransform.pivot = new Vector2(0f, 0.5f);
    }

    // A small bolt (three bars) with a label, shown while the thunder lead runs.
    void BuildThunder()
    {
        GameObject root = new GameObject("ThunderGlyph", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        thunderRoot = root.GetComponent<RectTransform>();
        thunderGroup = root.GetComponent<CanvasGroup>();
        thunderGroup.blocksRaycasts = false;
        thunderGroup.interactable = false;
        thunderGroup.alpha = 0f;
        thunderRoot.anchorMin = new Vector2(0f, 1f);
        thunderRoot.anchorMax = new Vector2(0f, 1f);
        thunderRoot.pivot = new Vector2(0f, 1f);
        thunderRoot.anchoredPosition = new Vector2(36f, -278f);
        thunderRoot.sizeDelta = new Vector2(200f, 36f);

        MakeBar(thunderRoot, "BoltTop", new Vector2(16f, -6f), new Vector2(16f, 4f), 0f);
        MakeBar(thunderRoot, "BoltMid", new Vector2(16f, -18f), new Vector2(24f, 4f), 55f);
        MakeBar(thunderRoot, "BoltBottom", new Vector2(16f, -30f), new Vector2(16f, 4f), 0f);
        TMP_Text label = HUD.MakeRuntimeText(thunderRoot, "ThunderText", "Thunder", 20, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(140f, 28f), ThunderColor, TextAlignmentOptions.MidlineLeft);
        label.rectTransform.pivot = new Vector2(0f, 0.5f);
    }

    static void MakeBar(RectTransform parent, string barName, Vector2 position, Vector2 size, float degrees)
    {
        Image bar = MakeImage(parent, barName, HUD.White(), position, size, ThunderColor);
        bar.rectTransform.localEulerAngles = new Vector3(0f, 0f, degrees);
    }

    // Anchored top-left so positions are measured down from the parent's top edge.
    static Image MakeImage(RectTransform parent, string imageName, Sprite sprite, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(imageName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
}
