using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Keeper close-ups on a temporary studio scene (ground, Keeper.prefab, island 1 look, PC renderer with the moon rim).
// Runs synchronously in edit mode: three shots x three states x Low and Ultra, written as <preset>/<shot>_<state>.jpg.
// The open scene and the graphics preset are put back afterwards, and nothing is saved to the project.
public static class KeeperCloseupCapture
{
    const string KeeperPath = "Assets/Game/Prefabs/Characters/Keeper.prefab";
    const string LookPath = "Assets/Game/Art/Look/LookProfile_island1.asset";
    const string VolumePath = "Assets/Game/Art/Look/Volumes/LookVolume_island1.asset";
    const string SkyPath = "Assets/Game/Art/Look/Sky/NightSky_island1.mat";
    const string DefaultOutput = "docs/look/keeper/2026-10-03-before";
    const int Width = 1920;
    const int Height = 1080;
    const int JpegQuality = 92;
    const float Distance = 2.2f;
    const float Fov = 50f;
    const float ChestFraction = 0.68f;
    const float PoseTime = 0.6f;
    const float LowFuel = 0.15f;
    const int WarmRenders = 2;

    static readonly GraphicsLevel[] Presets = { GraphicsLevel.Low, GraphicsLevel.Ultra };
    static readonly string[] States = { "idle", "gust", "lowfuel" };
    static readonly string[] Shots = { "front", "threequarter", "back" };
    static readonly float[] ShotYaw = { 0f, 45f, 180f };

    [MenuItem("Lantern Keeper/Look/Capture Keeper Close-ups")]
    public static void CaptureFromMenu()
    {
        Run(DefaultOutput);
    }

    public static void Run(string outputDir)
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Keeper close-ups: leave play mode first.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        string root = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.IsPathRooted(outputDir) ? outputDir : Path.Combine(root, outputDir);
        string scenePath = EditorSceneManager.GetActiveScene().path;
        GraphicsLevel level = GraphicsQuality.Current;
        bool hadPref = PlayerPrefs.HasKey(GraphicsQuality.PrefsKey);
        int pref = PlayerPrefs.GetInt(GraphicsQuality.PrefsKey, 0);
        int count = 0;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            count = Capture(output);
        }
        catch (Exception exception)
        {
            Debug.LogError("Keeper close-ups failed: " + exception);
        }
        finally
        {
            Restore(scenePath, level, hadPref, pref);
        }

        Debug.Log("Keeper close-ups: wrote " + count + " images to " + output);
    }

    static void Restore(string scenePath, GraphicsLevel level, bool hadPref, int pref)
    {
        try
        {
            GraphicsQuality.Set(level);
            if (hadPref)
            {
                PlayerPrefs.SetInt(GraphicsQuality.PrefsKey, pref);
            }
            else
            {
                PlayerPrefs.DeleteKey(GraphicsQuality.PrefsKey);
            }

            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Debug.LogError("Keeper close-ups: restoring the graphics preset failed: " + exception);
        }

        try
        {
            if (!string.IsNullOrEmpty(scenePath))
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("Keeper close-ups: reopening the scene failed: " + exception);
        }
    }

    static int Capture(string output)
    {
        GameObject keeperAsset = AssetDatabase.LoadAssetAtPath<GameObject>(KeeperPath);
        LookProfile look = AssetDatabase.LoadAssetAtPath<LookProfile>(LookPath);
        if (keeperAsset == null || look == null)
        {
            throw new InvalidOperationException("Missing the keeper prefab or the island 1 look profile");
        }

        GameObject keeper = (GameObject)PrefabUtility.InstantiatePrefab(keeperAsset);
        keeper.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Bounds bounds = KeeperBounds(keeper);
        keeper.transform.position = new Vector3(0f, -bounds.min.y, 0f);
        bounds = KeeperBounds(keeper);
        Vector3 chest = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * ChestFraction, bounds.center.z);

        BuildGround();
        Light moon = BuildMoon();
        BuildLook(look, moon);
        Camera camera = BuildCamera();

        Animator animator = keeper.GetComponentInChildren<Animator>();
        Light lantern = FindLantern(keeper);
        Lantern lanternComponent = lantern != null ? lantern.GetComponentInParent<Lantern>() : null;
        float lowIntensity;
        float lowRange;
        LowFuelLight(lanternComponent, out lowIntensity, out lowRange);
        float fullIntensity = lantern != null ? lantern.intensity : 0f;
        float fullRange = lantern != null ? lantern.range : 0f;
        bool hasLean = animator != null && animator.GetLayerIndex("Lean") >= 0;
        Component flame = FindByTypeName(keeper, "LanternFlame");
        LanternHalo halo = keeper.GetComponentInChildren<LanternHalo>(true);
        Debug.Log("Keeper close-ups: height " + bounds.size.y.ToString("F2") + " m, lean layer " + hasLean + ", LanternFlame " + (flame != null)
            + ", lantern light " + (lantern != null) + ", animator " + (animator != null));

        int written = 0;
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }

        for (int p = 0; p < Presets.Length; p++)
        {
            GraphicsQuality.Set(Presets[p]);
            // KeeperQuality does not run in edit mode, so force the preset's LOD set here.
            ForcePresetLod(keeper, Presets[p]);
            camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            for (int s = 0; s < States.Length; s++)
            {
                string state = States[s];
                if (lantern != null)
                {
                    lantern.intensity = state == "lowfuel" ? lowIntensity : fullIntensity;
                    lantern.range = state == "lowfuel" ? lowRange : fullRange;
                }

                float fuel = state == "lowfuel" ? LowFuel : 1f;
                if (flame != null)
                {
                    CallApplyFuel(flame, fuel);
                }

                Pose(animator, state == "gust" && hasLean);
                RefreshSkin(keeper);
                for (int i = 0; i < Shots.Length; i++)
                {
                    Quaternion yaw = Quaternion.Euler(0f, ShotYaw[i], 0f);
                    Vector3 offset = keeper.transform.rotation * yaw * Vector3.forward * Distance;
                    camera.transform.position = chest + offset;
                    camera.transform.LookAt(chest);
                    if (halo != null)
                    {
                        // The halo quad faces the camera, so it follows each shot's camera.
                        halo.ApplyFuel(fuel);
                    }

                    string path = Path.Combine(output, Presets[p].ToString().ToLowerInvariant(), Shots[i] + "_" + state + ".jpg");
                    Render(camera, path);
                    written++;
                }
            }
        }

        return written;
    }

    static void ForcePresetLod(GameObject keeper, GraphicsLevel preset)
    {
        LODGroup group = keeper.GetComponentInChildren<LODGroup>(true);
        GraphicsProfile profile = AssetDatabase.LoadAssetAtPath<GraphicsProfile>("Assets/Game/Settings/Graphics/GraphicsProfile_" + preset + ".asset");
        if (group == null || profile == null)
        {
            Debug.LogWarning("Keeper close-ups: no LODGroup or graphics profile for " + preset + "; the LOD is not forced.");
            return;
        }

        group.ForceLOD(profile.keeperLod);
    }

    // Samples the pose the way the runtime would at rest: fresh bind, the layer weights KeeperAnimator writes
    // (LanternArm 1, Lean 0 or 1 for a gust, UpperActions 1, Actions at its default of 1, both in Empty), then a short update.
    static void Pose(Animator animator, bool lean)
    {
        if (animator == null)
        {
            return;
        }

        animator.Rebind();
        SetWeight(animator, "LanternArm", 1f);
        SetWeight(animator, "Lean", lean ? 1f : 0f);
        SetWeight(animator, "Actions", 1f);
        SetWeight(animator, "UpperActions", 1f);
        animator.Update(0f);
        animator.Update(PoseTime);
    }

    // An edit-mode Animator.Update moves the bones, but a skinned mesh that was already rendered keeps its previous skinning
    // until the renderer is toggled: without this, re-posing left the hood, cloak and body in the first pose.
    static void RefreshSkin(GameObject keeper)
    {
        SkinnedMeshRenderer[] skins = keeper.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skins.Length; i++)
        {
            skins[i].enabled = false;
            skins[i].enabled = true;
        }
    }

    static void SetWeight(Animator animator, string layer, float weight)
    {
        int index = animator.GetLayerIndex(layer);
        if (index >= 0)
        {
            animator.SetLayerWeight(index, weight);
        }
    }

    static Bounds KeeperBounds(GameObject keeper)
    {
        Renderer[] renderers = keeper.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(keeper.transform.position, Vector3.zero);
        bool first = true;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is ParticleSystemRenderer || renderers[i] is TrailRenderer || renderers[i] is LineRenderer)
            {
                continue;
            }

            if (first)
            {
                bounds = renderers[i].bounds;
                first = false;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return bounds;
    }

    static Light FindLantern(GameObject keeper)
    {
        Light[] lights = keeper.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].name == "LanternLight")
            {
                return lights[i];
            }
        }

        return null;
    }

    // The same mapping as Lantern.ApplyLight at fuel 0.15, read from the component's serialized fields so there is one source of truth.
    static void LowFuelLight(Lantern lantern, out float intensity, out float range)
    {
        float minIntensity = 0.35f;
        float maxIntensity = 5.5f;
        float minRange = 3.5f;
        float maxRange = 14f;
        if (lantern != null)
        {
            SerializedObject serialized = new SerializedObject(lantern);
            minIntensity = serialized.FindProperty("minIntensity").floatValue;
            maxIntensity = serialized.FindProperty("maxIntensity").floatValue;
            minRange = serialized.FindProperty("minRange").floatValue;
            maxRange = serialized.FindProperty("maxRange").floatValue;
        }

        intensity = Mathf.Lerp(minIntensity, maxIntensity, LowFuel);
        range = Mathf.Lerp(minRange, maxRange, LowFuel);
    }

    static Component FindByTypeName(GameObject root, string typeName)
    {
        Component[] all = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].GetType().Name == typeName)
            {
                return all[i];
            }
        }

        return null;
    }

    static void CallApplyFuel(Component flame, float fuel)
    {
        MethodInfo method = flame.GetType().GetMethod("ApplyFuel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            Debug.LogWarning("Keeper close-ups: LanternFlame has no ApplyFuel method.");
            return;
        }

        method.Invoke(flame, new object[] { fuel });
    }

    static void BuildGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "StudioGround";
        // Large enough that its far edge is past the camera far plane (the moon rim pass draws a line on a nearer edge).
        ground.transform.localScale = new Vector3(40f, 1f, 40f);
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = new Color(0.16f, 0.2f, 0.17f);
        material.SetFloat("_Smoothness", 0.1f);
        ground.GetComponent<Renderer>().sharedMaterial = material;
    }

    // The island builder's moonlight, with its fallback direction.
    static Light BuildMoon()
    {
        GameObject moonObject = new GameObject("Moonlight");
        Light moon = moonObject.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.65f;
        moonObject.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
        return moon;
    }

    static void BuildLook(LookProfile look, Light moon)
    {
        GameObject lookObject = new GameObject("LookApplier");
        LookApplier applier = lookObject.AddComponent<LookApplier>();
        SerializedObject serialized = new SerializedObject(applier);
        serialized.FindProperty("profile").objectReferenceValue = look;
        serialized.FindProperty("moon").objectReferenceValue = moon;
        serialized.FindProperty("skyMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        lookObject.AddComponent<MoonRimSettings>();
        applier.Apply();

        GameObject volumeObject = new GameObject("LookVolume");
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.weight = 1f;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
        DynamicGI.UpdateEnvironment();
    }

    static Camera BuildCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = Fov;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 200f;
        UniversalAdditionalCameraData data = cameraObject.GetComponent<UniversalAdditionalCameraData>();
        if (data == null)
        {
            data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        }

        data.renderPostProcessing = true;
        return camera;
    }

    static void Render(Camera camera, string path)
    {
        RenderTexture target = null;
        Texture2D texture = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            // The first renders after a light or pose change only prime shadow maps and the post stack.
            for (int i = 0; i < WarmRenders; i++)
            {
                camera.Render();
            }

            camera.Render();
            camera.targetTexture = null;
            RenderTexture.active = target;
            texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, ImageConversion.EncodeToJPG(texture, JpegQuality));
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }

            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
}
