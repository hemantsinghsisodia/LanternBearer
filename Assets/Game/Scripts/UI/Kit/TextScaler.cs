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

    // Sizes seen for one plain text: the author's base, and what this scaler last wrote (to spot code changing fontSize later).
    private class PlainEntry
    {
        public float[] baseSizes;
        public float[] applied;
    }

    private readonly Dictionary<TMP_Text, PlainEntry> plainBase = new Dictionary<TMP_Text, PlainEntry>();
    private float nextScan;
    private float lastScale = -1f;

    // Scales a text the moment its creator makes it (HUD.MakeRuntimeText, the compass labels), so it never
    // renders a frame at 100%. The 0.5 s rescan stays as the safety net for anything created elsewhere.
    public static void Notify(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }
        TextScaler scaler = text.GetComponentInParent<TextScaler>(true);
        if (scaler != null && scaler.scalePlainText && scaler.isActiveAndEnabled)
        {
            scaler.ScaleOne(text, Current, false);
        }
    }

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
            ScaleOne(texts[i], scale, scaleChanged);
        }
    }

    private void ScaleOne(TMP_Text text, float scale, bool scaleChanged)
    {
        if (text.GetComponent<ThemedLabel>() != null || text.GetComponentInParent<TextScaler>(true) != this)
        {
            return;
        }
        PlainEntry entry;
        bool fresh = !plainBase.TryGetValue(text, out entry);
        if (fresh)
        {
            entry = new PlainEntry { baseSizes = new[] { text.fontSize, text.fontSizeMin, text.fontSizeMax }, applied = null };
            plainBase[text] = entry;
        }
        else if (entry.applied != null && !SameSizes(text, entry.applied))
        {
            // Code changed the size since we last wrote it: that is the new base.
            entry.baseSizes = new[] { text.fontSize, text.fontSizeMin, text.fontSizeMax };
            fresh = true;
        }
        if (!fresh && !scaleChanged)
        {
            return;
        }
        if (text.enableAutoSizing)
        {
            text.fontSizeMin = entry.baseSizes[1] * scale;
            text.fontSizeMax = entry.baseSizes[2] * scale;
        }
        else
        {
            text.fontSize = entry.baseSizes[0] * scale;
        }
        entry.applied = new[] { text.fontSize, text.fontSizeMin, text.fontSizeMax };
    }

    private static bool SameSizes(TMP_Text text, float[] sizes)
    {
        // Auto-sizing rewrites fontSize itself, so only the min/max range tells us about outside changes.
        if (text.enableAutoSizing)
        {
            return Mathf.Approximately(text.fontSizeMin, sizes[1]) && Mathf.Approximately(text.fontSizeMax, sizes[2]);
        }
        return Mathf.Approximately(text.fontSize, sizes[0]);
    }
}
}
