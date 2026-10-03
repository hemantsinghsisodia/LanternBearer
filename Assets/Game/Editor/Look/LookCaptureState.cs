using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LanternKeeper
{
// Snapshot of every editor and player setting the baseline capture touches, so a run (or a failed run) can put it all back.
[Serializable]
public class LookCaptureState
{
    public const string DifficultyKey = "LanternKeeperDifficulty";

    [Serializable]
    public struct PrefEntry
    {
        public string key;
        public bool present;
        public int value;
    }

    public int graphicsLevel;
    public int qualityLevel;
    public List<PrefEntry> prefs = new List<PrefEntry>();
    public float timeScale;
    public bool runInBackground;
    public string scenePath;
    public bool wasPlaying;
    public int gameViewSizeIndex = -1;
    public bool gameViewSizeAdded;

    public static string IntroKey(string levelId)
    {
        return "LanternKeeperIntro_" + levelId;
    }

    public static LookCaptureState Snapshot()
    {
        LookCaptureState state = new LookCaptureState();
        state.graphicsLevel = (int)GraphicsQuality.Current;
        state.qualityLevel = QualitySettings.GetQualityLevel();
        state.AddPref(GraphicsQuality.PrefsKey);
        state.AddPref(DifficultyKey);
        for (int i = 0; i < LookShotGenerator.LevelIds.Length; i++)
        {
            state.AddPref(IntroKey(LookShotGenerator.LevelIds[i]));
        }

        state.timeScale = Time.timeScale;
        state.runInBackground = Application.runInBackground;
        state.scenePath = EditorSceneManager.GetActiveScene().path;
        state.wasPlaying = EditorApplication.isPlaying;
        state.gameViewSizeIndex = LookGameView.SelectedIndex();
        return state;
    }

    void AddPref(string key)
    {
        PrefEntry entry = new PrefEntry();
        entry.key = key;
        entry.present = PlayerPrefs.HasKey(key);
        entry.value = entry.present ? PlayerPrefs.GetInt(key) : 0;
        prefs.Add(entry);
    }

    // One line per tracked value, for the before and after comparison in the log.
    public string Describe()
    {
        string text = "graphics=" + graphicsLevel + " quality=" + qualityLevel + " timeScale=" + timeScale + " runInBackground=" + runInBackground + " scene=" + scenePath + " playing=" + wasPlaying + " gameViewSize=" + gameViewSizeIndex;
        for (int i = 0; i < prefs.Count; i++)
        {
            text += " " + prefs[i].key + "=" + (prefs[i].present ? prefs[i].value.ToString() : "absent");
        }

        return text;
    }

    // Call with play mode off.
    public void Restore()
    {
        for (int i = 0; i < prefs.Count; i++)
        {
            if (prefs[i].present)
            {
                PlayerPrefs.SetInt(prefs[i].key, prefs[i].value);
            }
            else
            {
                PlayerPrefs.DeleteKey(prefs[i].key);
            }
        }

        // Set re-applies the quality level and writes the pref, so put the original pref state back afterwards.
        GraphicsQuality.Set((GraphicsLevel)graphicsLevel);
        if (QualitySettings.GetQualityLevel() != qualityLevel)
        {
            QualitySettings.SetQualityLevel(qualityLevel, true);
        }

        for (int i = 0; i < prefs.Count; i++)
        {
            if (prefs[i].key == GraphicsQuality.PrefsKey && !prefs[i].present)
            {
                PlayerPrefs.DeleteKey(prefs[i].key);
            }
        }

        PlayerPrefs.Save();
        Time.timeScale = timeScale;
        Application.runInBackground = runInBackground;
        LookGameView.Select(gameViewSizeIndex);
        if (gameViewSizeAdded)
        {
            LookGameView.RemoveCustomSize();
        }

        if (!string.IsNullOrEmpty(scenePath) && EditorSceneManager.GetActiveScene().path != scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
    }
}

// Reflection over the internal Game view size list: a fixed 1920x1080 entry for the capture.
public static class LookGameView
{
    const string CustomSizeName = "LookCapture 1920x1080";
    static readonly Type GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
    static readonly Type SizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
    static readonly Type SizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
    static readonly Type SizeKind = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static object Group()
    {
        Type single = typeof(ScriptableSingleton<>).MakeGenericType(SizesType);
        object instance = single.GetProperty("instance", Any).GetValue(null, null);
        object groupType = SizesType.GetProperty("currentGroupType", Any).GetValue(instance, null);
        return SizesType.GetMethod("GetGroup", Any).Invoke(instance, new object[] { groupType });
    }

    static EditorWindow Window(bool create)
    {
        if (!create)
        {
            UnityEngine.Object[] open = Resources.FindObjectsOfTypeAll(GameViewType);
            return open.Length > 0 ? (EditorWindow)open[0] : null;
        }

        return EditorWindow.GetWindow(GameViewType, false, null, false);
    }

    public static int SelectedIndex()
    {
        EditorWindow window = Window(false);
        if (window == null)
        {
            return -1;
        }

        PropertyInfo property = GameViewType.GetProperty("selectedSizeIndex", Any);
        return property != null ? (int)property.GetValue(window, null) : -1;
    }

    public static void Select(int index)
    {
        if (index < 0)
        {
            return;
        }

        EditorWindow window = Window(false);
        if (window == null)
        {
            return;
        }

        PropertyInfo property = GameViewType.GetProperty("selectedSizeIndex", Any);
        if (property != null)
        {
            property.SetValue(window, index, null);
        }

        window.Repaint();
    }

    public static int FindCustom()
    {
        object group = Group();
        Type groupType = group.GetType();
        int count = (int)groupType.GetMethod("GetTotalCount", Any).Invoke(group, null);
        MethodInfo get = groupType.GetMethod("GetGameViewSize", Any);
        for (int i = 0; i < count; i++)
        {
            object size = get.Invoke(group, new object[] { i });
            string text = (string)SizeType.GetProperty("baseText", Any).GetValue(size, null);
            int width = (int)SizeType.GetProperty("width", Any).GetValue(size, null);
            int height = (int)SizeType.GetProperty("height", Any).GetValue(size, null);
            if (text == CustomSizeName && width == 1920 && height == 1080)
            {
                return i;
            }
        }

        return -1;
    }

    // Returns true when a new custom size was added (so the caller can remove it again).
    public static bool EnsureFixed1080(out int index)
    {
        index = FindCustom();
        if (index >= 0)
        {
            return false;
        }

        object group = Group();
        object kind = Enum.ToObject(SizeKind, 1);
        object size = Activator.CreateInstance(SizeType, new object[] { kind, 1920, 1080, CustomSizeName });
        group.GetType().GetMethod("AddCustomSize", Any).Invoke(group, new object[] { size });
        index = FindCustom();
        return true;
    }

    public static void RemoveCustomSize()
    {
        int index = FindCustom();
        if (index < 0)
        {
            return;
        }

        object group = Group();
        Type groupType = group.GetType();
        int builtin = (int)groupType.GetMethod("GetBuiltinCount", Any).Invoke(group, null);
        if (index >= builtin)
        {
            groupType.GetMethod("RemoveCustomSize", Any).Invoke(group, new object[] { index });
        }
    }

    public static void Show()
    {
        Window(true).Show();
    }

    public static Vector2 TargetSize()
    {
        EditorWindow window = Window(false);
        if (window == null)
        {
            return Vector2.zero;
        }

        MethodInfo method = GameViewType.GetMethod("GetSizeOfMainGameView", Any);
        if (method != null)
        {
            return (Vector2)method.Invoke(null, null);
        }

        return Vector2.zero;
    }
}
}
