using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// WebGL only. A browser compiles each shader variant the first time something using it is drawn, on the one browser thread: a moth
// spawning, the first lightning flash, the grass coming into range or a beacon's light each cost 40 to 500 ms. This draws every material
// that is loaded when a scene starts (scene renderers, inactive ones and the prefabs the spawners hold) once, as a tiny mesh in front of
// the camera, in the first frames after the scene loads, while the intro is up. The variants then exist before they are needed.
// Windows and the Editor are unchanged: nothing here is compiled for them.
public class WebShaderPrewarm : MonoBehaviour
{
    // Test hook: the measurement probe turns the pre-warm off (?prewarm=0) to compare.
    public static bool Disabled;

#if UNITY_WEBGL && !UNITY_EDITOR
    const int WarmFrames = 3;
    const int StartDelayFrames = 2;

    static WebShaderPrewarm instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        GameObject host = new GameObject("WebShaderPrewarm");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<WebShaderPrewarm>();
        SceneManager.sceneLoaded += OnSceneLoaded;
        instance.StartCoroutine(instance.Warm());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance != null)
        {
            instance.StopAllCoroutines();
            instance.StartCoroutine(instance.Warm());
        }
    }

    static Mesh BuildQuad()
    {
        Mesh mesh = new Mesh();
        mesh.name = "WebPrewarmQuad";
        mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        mesh.tangents = new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
        return mesh;
    }

    IEnumerator Warm()
    {
        for (int i = 0; i < StartDelayFrames; i++)
        {
            yield return null;
        }

        Camera cam = Camera.main;
        if (cam == null || Disabled)
        {
            yield break;
        }

        List<Material> materials = new List<Material>(256);
        HashSet<Material> seen = new HashSet<Material>();
        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
        List<Material> sharedBuffer = new List<Material>(4);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.GetSharedMaterials(sharedBuffer);
            for (int m = 0; m < sharedBuffer.Count; m++)
            {
                Material material = sharedBuffer[m];
                if (material != null && material.shader != null && seen.Add(material) && WebPrewarmRules.ShouldWarm(material.shader.name))
                {
                    materials.Add(material);
                }
            }
        }

        Mesh quad = BuildQuad();

        for (int frame = 0; frame < WarmFrames; frame++)
        {
            cam = Camera.main;
            if (cam == null)
            {
                yield break;
            }

            Vector3 position = cam.transform.position + cam.transform.forward * 3f;
            Matrix4x4 matrix = Matrix4x4.TRS(position, cam.transform.rotation, Vector3.one * 0.002f);
            Matrix4x4[] pair = { matrix, matrix };
            for (int i = 0; i < materials.Count; i++)
            {
                Graphics.DrawMesh(quad, matrix, materials[i], 0, null, 0, null, ShadowCastingMode.On, true);
                if (materials[i].enableInstancing)
                {
                    // GPU-instanced materials (grass, foliage) compile a separate INSTANCING_ON variant.
                    Graphics.DrawMeshInstanced(quad, 0, materials[i], pair, 2, null, ShadowCastingMode.On, true);
                }
            }

            yield return null;
        }
    }
#endif
}
}
