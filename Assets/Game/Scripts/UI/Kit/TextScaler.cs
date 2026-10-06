using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LanternKeeper
{
// Put on a screen root. Follows UserSettings.TextScale and re-applies basePx x Current to every
// ThemedLabel underneath whenever settings change. Not ExecuteAlways: edit mode
// never rewrites label sizes (that could bake the developer's scale into prefabs); tests call Reapply().
public class TextScaler : MonoBehaviour
{
    private const float RescanInterval = 0.5f;

    // On for the island HUD canvas: its labels are plain TMP texts, so they are scaled from the size first seen.
    [SerializeField] private bool scalePlainText;

    private readonly Dictionary<TMP_Text, float[]> plainBase = new Dictionary<TMP_Text, float[]>();
    private float nextScan;
    private float lastScale = -1f;

    public bool ScalePlainText
    {
        get { return scalePlainText; }
        set { scalePlainText = value; }
    }

    public static float Current
    {
        get { return UserSettings.TextScale; }
    }

    private void OnEnable()
    {
        UserSettings.Changed += Reapply;
        Reapply();
    }

    private void OnDisable()
    {
        UserSettings.Changed -= Reapply;
    }

    private void OnDestroy()
    {
        UserSettings.Changed -= Reapply;
    }

    public void Reapply()
    {
        ThemedLabel[] labels = GetComponentsInChildren<ThemedLabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].Apply();
        }
        if (scalePlainText)
        {
            ScalePlain();
        }
    }

    private void Update()
    {
        if (scalePlainText && Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + RescanInterval;
            ScalePlain();
        }
    }

    private void ScalePlain()
    {
        float scale = Current;
        bool scaleChanged = !Mathf.Approximately(scale, lastScale);
        lastScale = scale;
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text.GetComponent<ThemedLabel>() != null || text.GetComponentInParent<TextScaler>(true) != this)
            {
                continue;
            }
            float[] baseSizes;
            bool fresh = !plainBase.TryGetValue(text, out baseSizes);
            if (fresh)
            {
                baseSizes = new[] { text.fontSize, text.fontSizeMin, text.fontSizeMax };
                plainBase[text] = baseSizes;
            }
            if (!fresh && !scaleChanged)
            {
                continue;
            }
            if (text.enableAutoSizing)
            {
                text.fontSizeMin = baseSizes[1] * scale;
                text.fontSizeMax = baseSizes[2] * scale;
            }
            else
            {
                text.fontSize = baseSizes[0] * scale;
            }
        }
    }
}
}
