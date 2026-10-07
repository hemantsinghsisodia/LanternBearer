using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
public class BeaconCompass : MonoBehaviour
{
    const int MaxMarkers = 3;
    const float PadPixels = 72f;

    [SerializeField] RectTransform markerRoot;
    [SerializeField] Transform player;
    [SerializeField] UITheme theme;
    [SerializeField] Sprite roofIcon;

    readonly Marker[] markers = new Marker[MaxMarkers];
    readonly Beacon[] nearest = new Beacon[MaxMarkers];
    readonly float[] nearestDistance = new float[MaxMarkers];
    Camera view;
    bool playerResolved;
    bool built;
    static bool loggedFallback;
    static string[] meterLabels;
    static Sprite arrowSprite;
    static Sprite backingSprite;
    static TMP_FontAsset font;
    static Material fontFace;

    struct Marker
    {
        public RectTransform Root;
        public RectTransform Arrow;
        public TMP_Text Distance;
        public CanvasGroup Group;
        public int ShownMeters;
    }

    void Awake()
    {
        EnsureLabels();
        EnsureStyle();
        if (markerRoot == null)
        {
            Transform existing = transform.Find("EdgeMarkers");
            if (existing != null)
            {
                markerRoot = existing as RectTransform;
            }
        }

        BuildMarkers();
        HideLegacy();
    }

    void Start()
    {
        ResolvePlayer();
        if (view == null)
        {
            view = Camera.main;
        }
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

    void HideLegacy()
    {
        Transform arrow = transform.Find("Compass");
        if (arrow != null && arrow.gameObject.activeSelf)
        {
            arrow.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (player == null || !built)
        {
            return;
        }

        if (view == null)
        {
            view = Camera.main;
            if (view == null)
            {
                return;
            }
        }

        GameManager manager = GameManager.Instance;
        bool hide = manager != null && (manager.IsRoundOver || manager.IsPaused || manager.DawnPlaying);
        int found = hide ? 0 : CollectNearest();
        for (int i = 0; i < MaxMarkers; i++)
        {
            bool show = i < found;
            if (markers[i].Root.gameObject.activeSelf != show)
            {
                markers[i].Root.gameObject.SetActive(show);
            }

            if (show)
            {
                Place(i, nearest[i], nearestDistance[i]);
            }
        }
    }

    int CollectNearest()
    {
        int found = 0;
        int count = Beacon.All.Count;
        Vector3 origin = player.position;
        for (int i = 0; i < count; i++)
        {
            Beacon beacon = Beacon.All[i];
            if (beacon == null || beacon.IsLit)
            {
                continue;
            }

            float distance = Vector3.Distance(origin, beacon.transform.position);
            int slot = found < MaxMarkers ? found : MaxMarkers - 1;
            if (found >= MaxMarkers && distance >= nearestDistance[slot])
            {
                continue;
            }

            if (found < MaxMarkers)
            {
                found++;
            }

            int insert = found - 1;
            while (insert > 0 && distance < nearestDistance[insert - 1])
            {
                nearest[insert] = nearest[insert - 1];
                nearestDistance[insert] = nearestDistance[insert - 1];
                insert--;
            }

            nearest[insert] = beacon;
            nearestDistance[insert] = distance;
        }

        return found;
    }

    void Place(int index, Beacon beacon, float distance)
    {
        Vector3 world = beacon.transform.position + Vector3.up * 1.6f;
        Vector3 screen = view.WorldToScreenPoint(world);
        bool behind = screen.z < 0f;
        if (behind)
        {
            screen.x = Screen.width - screen.x;
            screen.y = Screen.height - screen.y;
        }

        float minX = PadPixels;
        float maxX = Screen.width - PadPixels;
        float minY = PadPixels;
        float maxY = Screen.height - PadPixels;
        float rawX = screen.x;
        float rawY = screen.y;
        float clampedX = Mathf.Clamp(rawX, minX, maxX);
        float clampedY = Mathf.Clamp(rawY, minY, maxY);
        bool inside = !behind && Mathf.Abs(clampedX - rawX) < 0.5f && Mathf.Abs(clampedY - rawY) < 0.5f;

        Vector2 screenPoint = new Vector2(clampedX, clampedY);
        Vector2 local;
        RectTransform space = markerRoot != null ? markerRoot : (RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screenPoint, null, out local);
        if (inside)
        {
            local.y += 64f;
        }

        Vector2 delta = local - markers[index].Root.anchoredPosition;
        if (delta.sqrMagnitude > 0.25f)
        {
            markers[index].Root.anchoredPosition = local;
        }

        float scale = index == 0 ? 1.2f : 0.9f;
        if (Mathf.Abs(markers[index].Root.localScale.x - scale) > 0.01f)
        {
            markers[index].Root.localScale = new Vector3(scale, scale, 1f);
        }

        Vector2 toward = new Vector2(rawX - clampedX, rawY - clampedY);
        if (toward.sqrMagnitude < 4f)
        {
            toward = Vector2.down;
        }

        float angle = Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg - 90f;
        float angleDelta = Mathf.DeltaAngle(markers[index].Arrow.localEulerAngles.z, angle);
        if (Mathf.Abs(angleDelta) > 0.5f)
        {
            markers[index].Arrow.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float radius = Mathf.Min(Screen.width, Screen.height) * 0.5f;
        float alpha = 1f;
        if (inside)
        {
            float unit = Vector2.Distance(new Vector2(rawX, rawY), center) / Mathf.Max(1f, radius);
            alpha = Mathf.SmoothStep(0.18f, 0.42f, unit);
        }

        if (Mathf.Abs(markers[index].Group.alpha - alpha) > 0.02f)
        {
            markers[index].Group.alpha = alpha;
        }

        int meters = Mathf.RoundToInt(distance);
        if (meters == markers[index].ShownMeters)
        {
            return;
        }

        markers[index].ShownMeters = meters;
        markers[index].Distance.text = Label(meters);
    }

    void BuildMarkers()
    {
        if (built)
        {
            return;
        }

        if (markerRoot == null)
        {
            GameObject root = new GameObject("EdgeMarkers", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            markerRoot = root.GetComponent<RectTransform>();
            markerRoot.anchorMin = Vector2.zero;
            markerRoot.anchorMax = Vector2.one;
            markerRoot.offsetMin = Vector2.zero;
            markerRoot.offsetMax = Vector2.zero;
        }

        EnsureStyle();
        for (int i = 0; i < MaxMarkers; i++)
        {
            markers[i] = CreateMarker(i);
            markers[i].ShownMeters = int.MinValue;
            markers[i].Root.gameObject.SetActive(false);
        }

        built = true;
    }

    // An amber edge arrow above a soft ink chip holding a roof icon and the distance (Inter, 20 px base; the smaller markers scale to 18).
    Marker CreateMarker(int index)
    {
        GameObject root = new GameObject("Marker" + index, typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(markerRoot, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(110f, 76f);
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        arrowObject.transform.SetParent(root.transform, false);
        RectTransform arrow = arrowObject.GetComponent<RectTransform>();
        arrow.anchoredPosition = new Vector2(0f, 14f);
        arrow.sizeDelta = new Vector2(22f, 28f);
        Image image = arrowObject.GetComponent<Image>();
        image.sprite = Arrow();
        image.color = theme != null ? theme.amber : new Color(1f, 0.86f, 0.48f, 1f);
        image.raycastTarget = false;

        GameObject chip = new GameObject("Chip", typeof(RectTransform), typeof(Image));
        chip.transform.SetParent(root.transform, false);
        RectTransform chipRect = chip.GetComponent<RectTransform>();
        chipRect.anchoredPosition = new Vector2(0f, -20f);
        chipRect.sizeDelta = new Vector2(104f, 34f);
        Image back = chip.GetComponent<Image>();
        back.sprite = Backing();
        back.type = Image.Type.Sliced;
        Color ink = theme != null ? theme.inkPanel : new Color(0.05f, 0.06f, 0.12f, 0.88f);
        ink.a = 0.7f;
        back.color = ink;
        back.raycastTarget = false;

        GameObject iconObject = new GameObject("Roof", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(chip.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchoredPosition = new Vector2(-34f, 0f);
        iconRect.sizeDelta = new Vector2(22f, 20f);
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = roofIcon;
        icon.enabled = roofIcon != null;
        icon.preserveAspect = true;
        icon.color = theme != null ? theme.amber : new Color(1f, 0.86f, 0.48f, 1f);
        icon.raycastTarget = false;

        GameObject labelObject = new GameObject("Distance", typeof(RectTransform));
        labelObject.transform.SetParent(chip.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchoredPosition = new Vector2(10f, 0f);
        labelRect.sizeDelta = new Vector2(70f, 30f);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.richText = false;
        label.text = Label(0);
        if (theme != null)
        {
            ThemedLabel themed = labelObject.AddComponent<ThemedLabel>();
            themed.Configure(theme, label, ThemedLabel.Role.Number, 20f);
        }
        else
        {
            label.font = font;
            label.fontSize = 18;
            label.color = new Color(0.96f, 0.93f, 0.86f, 1f);
        }

        TextScaler.Notify(label);

        Marker marker = new Marker();
        marker.Root = rect;
        marker.Arrow = arrow;
        marker.Distance = label;
        marker.Group = group;
        return marker;
    }

    static void EnsureLabels()
    {
        if (meterLabels != null)
        {
            return;
        }

        meterLabels = new string[480];
        for (int i = 0; i < meterLabels.Length; i++)
        {
            meterLabels[i] = i.ToString() + "m";
        }
    }

    static string Label(int meters)
    {
        if (meters < 0)
        {
            meters = 0;
        }

        if (meters < meterLabels.Length)
        {
            return meterLabels[meters];
        }

        return meters.ToString() + "m";
    }

    static void EnsureStyle()
    {
        if (font != null)
        {
            return;
        }

        if (TMP_Settings.defaultFontAsset != null)
        {
            font = TMP_Settings.defaultFontAsset;
        }
        else
        {
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        fontFace = Resources.Load<Material>("Fonts & Materials/LiberationSans SDF - Outline");
    }

    // Soft rounded rectangle, nine-sliced, with feathered edges.
    static Sprite Backing()
    {
        if (backingSprite != null)
        {
            return backingSprite;
        }

        const int size = 32;
        const float radius = 12f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Max(radius - (x + 0.5f), 0f, (x + 0.5f) - (size - radius));
                float cy = Mathf.Max(radius - (y + 0.5f), 0f, (y + 0.5f) - (size - radius));
                float dist = Mathf.Sqrt(cx * cx + cy * cy);
                float alpha = Mathf.Clamp01((radius - dist) / 3f + 0.25f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false);
        backingSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12f, 12f, 12f, 12f));
        backingSprite.name = "CompassBacking";
        return backingSprite;
    }

    static Sprite Arrow()
    {
        if (arrowSprite != null)
        {
            return arrowSprite;
        }

        const int w = 32;
        const int h = 40;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float tip = y / (float)(h - 1);
            float half = (1f - tip) * (w * 0.5f);
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - w * 0.5f);
                float alpha = dx <= half ? 1f : 0f;
                pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false);
        arrowSprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.15f), 100f);
        arrowSprite.name = "EdgeArrow";
        return arrowSprite;
    }
}
}
