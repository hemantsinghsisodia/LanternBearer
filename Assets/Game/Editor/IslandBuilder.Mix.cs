using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

namespace LanternKeeper
{
public static partial class IslandBuilder
{
    const string MixerPath = "Assets/Game/Audio/LanternMixer.mixer";

    public static void RefreshGameplayHud()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play mode before refreshing the HUD.");
            return;
        }

        EnsureHudSprites();
        EnsureMixer();
        string previous = EditorSceneManager.GetActiveScene().path;
        string[] scenes = LevelScenePaths();
        for (int i = 0; i < scenes.Length; i++)
        {
            EditorSceneManager.OpenScene(scenes[i]);
            RebuildHudObject();
            AssignMixer(UnityEngine.Object.FindAnyObjectByType<AudioManager>());
            SceneWiring.ApplyActiveScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Refreshed HUD in " + scenes[i]);
        }

        EditorSceneManager.OpenScene("Assets/Game/Scenes/MainMenu.unity");
        AssignMixer(UnityEngine.Object.FindAnyObjectByType<AudioManager>());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        if (!string.IsNullOrEmpty(previous) && previous != "Assets/Game/Scenes/MainMenu.unity")
        {
            EditorSceneManager.OpenScene(previous);
        }
    }

    static void RebuildHudObject()
    {
        ArtKit art = new ArtKit();
        art.uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        GameObject old = GameObject.Find("HUD");
        if (old != null)
        {
            UnityEngine.Object.DestroyImmediate(old);
        }

        CreateHud(art, new Stage());
    }

    static void AssignMixer(AudioManager audio)
    {
        if (audio == null)
        {
            return;
        }

        AudioMixer mixer = EnsureMixer();
        SerializedObject so = new SerializedObject(audio);
        SerializedProperty prop = so.FindProperty("mixer");
        if (prop == null)
        {
            return;
        }

        prop.objectReferenceValue = mixer;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureHudSprites()
    {
        EnsureFolder("Assets/Game");
        EnsureFolder("Assets/Game/UI");
        WriteDisc("Assets/Game/UI/HudCircle.png", 64, 0.46f, 0.04f);
        WriteDisc("Assets/Game/UI/HudGlow.png", 64, 0.02f, 0.55f);
    }

    static void WriteDisc(string path, int size, float solid, float feather)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - radius) / radius;
                    float dy = (y + 0.5f - radius) / radius;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 1f - Mathf.InverseLerp(solid, solid + feather, dist);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        ConfigureSprite(path);
    }

    static void ConfigureSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool ready = importer.textureType == TextureImporterType.Sprite
            && importer.spriteImportMode == SpriteImportMode.Single
            && !importer.mipmapEnabled
            && settings.spriteMeshType == SpriteMeshType.FullRect;
        if (ready)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spritePixelsPerUnit = 100f;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    static AudioMixer EnsureMixer()
    {
        EnsureFolder("Assets/Game");
        EnsureFolder("Assets/Game/Audio");
        AudioMixer existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (existing != null)
        {
            float volume;
            bool named = existing.GetFloat("MusicVol", out volume) && existing.FindSnapshot("Ducked") != null && existing.FindSnapshot("Normal") != null;
            if (named)
            {
                existing.updateMode = AudioMixerUpdateMode.UnscaledTime;
                return existing;
            }

            AssetDatabase.DeleteAsset(MixerPath);
        }

        Type controllerType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
        MethodInfo create = controllerType.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        object mixer = create.Invoke(null, new object[] { MixerPath });
        object master = controllerType.GetProperty("masterGroup").GetValue(mixer);
        MethodInfo createGroup = controllerType.GetMethod("CreateNewGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo addChild = controllerType.GetMethod("AddChildToParent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        object music = createGroup.Invoke(mixer, new object[] { "Music", false });
        object sfx = createGroup.Invoke(mixer, new object[] { "SFX", false });
        object ambience = createGroup.Invoke(mixer, new object[] { "Ambience", false });
        addChild.Invoke(mixer, new object[] { music, master });
        addChild.Invoke(mixer, new object[] { sfx, master });
        addChild.Invoke(mixer, new object[] { ambience, master });
        object lowpass = AddLowpass(music);

        PropertyInfo snapshotsProp = controllerType.GetProperty("snapshots");
        Array snapshots = (Array)snapshotsProp.GetValue(mixer);
        object normal = snapshots.GetValue(0);
        ((UnityEngine.Object)normal).name = "Normal";
        controllerType.GetProperty("TargetSnapshot").SetValue(mixer, normal);
        controllerType.GetMethod("CloneNewSnapshotFromTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(mixer, new object[] { false });
        snapshots = (Array)snapshotsProp.GetValue(mixer);
        object ducked = null;
        for (int i = 0; i < snapshots.Length; i++)
        {
            object snapshot = snapshots.GetValue(i);
            if (snapshot != normal)
            {
                ducked = snapshot;
                ((UnityEngine.Object)ducked).name = "Ducked";
            }
        }

        SetVolume(music, mixer, normal, 0f);
        SetVolume(music, mixer, ducked, -8f);
        SetVolume(sfx, mixer, normal, 0f);
        SetVolume(sfx, mixer, ducked, 0f);
        SetVolume(ambience, mixer, normal, 0f);
        SetVolume(ambience, mixer, ducked, 0f);
        SetEffect(lowpass, mixer, normal, "Cutoff freq", 22000f);
        SetEffect(lowpass, mixer, ducked, "Cutoff freq", 1400f);
        SetEffect(lowpass, mixer, normal, "Resonance", 1f);
        SetEffect(lowpass, mixer, ducked, "Resonance", 1f);
        ExposeVolume(mixer, music, "MusicVol");
        ExposeVolume(mixer, sfx, "SFXVol");
        ExposeVolume(mixer, ambience, "AmbienceVol");
        controllerType.GetProperty("startSnapshot").SetValue(mixer, normal);

        AudioMixer asset = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        asset.updateMode = AudioMixerUpdateMode.UnscaledTime;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log("Created Lantern mixer at " + MixerPath);
        return asset;
    }

    static object AddLowpass(object group)
    {
        Type effectType = Type.GetType("UnityEditor.Audio.AudioMixerEffectController, UnityEditor");
        object effect = Activator.CreateInstance(effectType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { "Lowpass" }, null);
        MethodInfo insert = group.GetType().GetMethod("InsertEffect", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        insert.Invoke(group, new object[] { effect, 0 });
        return effect;
    }

    static void SetVolume(object group, object mixer, object snapshot, float decibels)
    {
        MethodInfo set = group.GetType().GetMethod("SetValueForVolume", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        set.Invoke(group, new object[] { mixer, snapshot, decibels });
    }

    static void SetEffect(object effect, object mixer, object snapshot, string parameter, float value)
    {
        MethodInfo set = effect.GetType().GetMethod("SetValueForParameter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        set.Invoke(effect, new object[] { mixer, snapshot, parameter, value });
    }

    static void ExposeVolume(object mixer, object group, string exposedName)
    {
        object guid = group.GetType().GetMethod("GetGUIDForVolume").Invoke(group, null);
        Type exposedType = Type.GetType("UnityEditor.Audio.ExposedAudioParameter, UnityEditor");
        object entry = Activator.CreateInstance(exposedType);
        exposedType.GetField("guid", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(entry, guid);
        exposedType.GetField("name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(entry, exposedName);
        PropertyInfo exposed = mixer.GetType().GetProperty("exposedParameters");
        Array current = (Array)exposed.GetValue(mixer);
        int length = current == null ? 0 : current.Length;
        Array next = Array.CreateInstance(exposedType, length + 1);
        if (current != null && length > 0)
        {
            Array.Copy(current, next, length);
        }

        next.SetValue(entry, length);
        exposed.SetValue(mixer, next);
    }
}
}
