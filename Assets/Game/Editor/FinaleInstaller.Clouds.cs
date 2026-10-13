using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
// The finale's dramatic clouds (H4 spec): three layers of cloud banks (near, mid, far) and the god rays, baked as meshes of puffs (one quad per
// puff, all four vertices at the puff centre: FinaleCloud.shader billboards them) and built into the Finale object as "FinaleClouds".
// One material per layer plus one for the rays. Deterministic (seeded), so a second run is byte-identical.
public static partial class FinaleInstaller
{
    public const string CloudsName = "FinaleClouds";
    const string CloudShaderName = "LanternKeeper/FinaleCloud";
    const string RayShaderName = "LanternKeeper/FinaleGodRays";
    const string RayMaterialPath = Folder + "/FinaleGodRays.mat";
    const string RayMeshPath = Folder + "/FinaleGodRays.asset";
    const int RayShafts = 14;

    struct CloudLayerSpec
    {
        public string name;
        public float radius, baseY, bankHeight, puffMin, puffMax;
        public int banks, perBank;
        public float bankDegMin, bankDegMax;
        public float fadeHeight;
        public float gapHalf, drift, haze, horizonGlow, noiseScale;
        public int queue;
    }

    // Near about 250 m, mid 450 m, far 700 m. The far banks are the biggest and the lowest, hazed and glowing at the horizon; the near ones sit
    // high so they leave the horizon band (where the islands stand) clear.
    static readonly CloudLayerSpec[] CloudLayers =
    {
        new CloudLayerSpec { name = "Near", radius = 250f, baseY = 55f, bankHeight = 100f, puffMin = 62f, puffMax = 120f, banks = 15, perBank = 9, bankDegMin = 14f, bankDegMax = 24f, fadeHeight = 25f, gapHalf = 0.06f, drift = 0.0007f, haze = 0f, horizonGlow = 0f, noiseScale = 1.5f, queue = 3012 },
        new CloudLayerSpec { name = "Mid", radius = 450f, baseY = 30f, bankHeight = 160f, puffMin = 120f, puffMax = 230f, banks = 15, perBank = 9, bankDegMin = 13f, bankDegMax = 22f, fadeHeight = 45f, gapHalf = 0.065f, drift = 0.00033f, haze = 0.12f, horizonGlow = 0.3f, noiseScale = 1.4f, queue = 3011 },
        new CloudLayerSpec { name = "Far", radius = 700f, baseY = 10f, bankHeight = 230f, puffMin = 200f, puffMax = 380f, banks = 15, perBank = 10, bankDegMin = 12f, bankDegMax = 21f, fadeHeight = 90f, gapHalf = 0.06f, drift = 0.00014f, haze = 0.3f, horizonGlow = 0.85f, noiseScale = 1.3f, queue = 3010 }
    };

    static string CloudMeshPath(string layer)
    {
        return Folder + "/FinaleCloud_" + layer + ".asset";
    }

    static string CloudMaterialPath(string layer)
    {
        return Folder + "/FinaleCloud_" + layer + ".mat";
    }

    // Builds the "FinaleClouds" child of `parent`, returns it. Null (an error logged) if a shader is missing.
    static FinaleClouds BuildClouds(Transform parent, float seaY, Texture2D noise)
    {
        Shader cloudShader = Shader.Find(CloudShaderName);
        Shader rayShader = Shader.Find(RayShaderName);
        if (cloudShader == null || rayShader == null)
        {
            Debug.LogError("FinaleInstaller: the FinaleCloud or FinaleGodRays shader is missing.");
            return null;
        }

        GameObject root = new GameObject(CloudsName);
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(0f, seaY, 0f);
        FinaleClouds clouds = root.AddComponent<FinaleClouds>();

        MeshRenderer[] layerRenderers = new MeshRenderer[CloudLayers.Length];
        for (int i = 0; i < CloudLayers.Length; i++)
        {
            CloudLayerSpec spec = CloudLayers[i];
            Material material = EnsureMaterial(CloudMaterialPath(spec.name), cloudShader, "FinaleCloud_" + spec.name);
            material.SetTexture("_CloudTex", noise);
            material.SetFloat("_GapHalf", spec.gapHalf);
            material.SetFloat("_Drift", spec.drift);
            material.SetFloat("_Haze", spec.haze);
            material.SetFloat("_HorizonGlow", spec.horizonGlow);
            material.SetFloat("_NoiseScale", spec.noiseScale);
            material.SetFloat("_FadeHeight", spec.fadeHeight);
            material.SetFloat("_Fade", 0f);
            material.renderQueue = spec.queue;
            EditorUtility.SetDirty(material);

            Mesh mesh = EnsureMesh(CloudMeshPath(spec.name), "FinaleCloud_" + spec.name, BuildCloudMesh(spec, 7700 + i * 13));
            GameObject layer = new GameObject(spec.name);
            layer.transform.SetParent(root.transform, false);
            layer.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = layer.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material);
            renderer.enabled = false;
            layerRenderers[i] = renderer;
        }

        Material rayMaterial = EnsureMaterial(RayMaterialPath, rayShader, "FinaleGodRays");
        rayMaterial.SetTexture("_CloudTex", noise);
        rayMaterial.SetColor("_Color", new Color(1f, 0.78f, 0.5f, 1f));
        rayMaterial.SetFloat("_Strength", 0f);
        EditorUtility.SetDirty(rayMaterial);
        Mesh rayMesh = EnsureMesh(RayMeshPath, "FinaleGodRays", BuildRayMesh(7790));
        GameObject rayObject = new GameObject("GodRays");
        rayObject.transform.SetParent(root.transform, false);
        rayObject.AddComponent<MeshFilter>().sharedMesh = rayMesh;
        MeshRenderer rayRenderer = rayObject.AddComponent<MeshRenderer>();
        ConfigureRenderer(rayRenderer, rayMaterial);
        rayRenderer.enabled = false;

        SerializedObject data = new SerializedObject(clouds);
        SerializedProperty list = data.FindProperty("layers");
        list.arraySize = layerRenderers.Length;
        for (int i = 0; i < layerRenderers.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = layerRenderers[i];
        }

        data.FindProperty("rays").objectReferenceValue = rayRenderer;
        data.ApplyModifiedPropertiesWithoutUndo();
        return clouds;
    }

    // `banks` banks round the circle, each `perBank` puffs in a mound (flat base, domed top): the puffs lower in a bank are bigger and darker.
    // Vertices: the puff centre (x4); uv0 the corner; uv1 (size in metres, random); colour R the height in the bank (0 base .. 1 top),
    // G the mound factor, B a random.
    static Mesh BuildCloudMesh(CloudLayerSpec spec, int seed)
    {
        System.Random rng = new System.Random(seed);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv1 = new List<Vector2>();
        List<Color32> colours = new List<Color32>();
        List<int> triangles = new List<int>();
        for (int bank = 0; bank < spec.banks; bank++)
        {
            float centre = (bank + (float)(rng.NextDouble() - 0.5) * 0.6f) / spec.banks;
            float width = Mathf.Lerp(spec.bankDegMin, spec.bankDegMax, (float)rng.NextDouble()) / 360f;
            float tall = Mathf.Lerp(0.5f, 1.2f, (float)rng.NextDouble());
            for (int j = 0; j < spec.perBank; j++)
            {
                float across = ((j + (float)rng.NextDouble() * 0.8f) / spec.perBank) - 0.5f;
                float mound = 1f - 4f * across * across;
                float h01 = Mathf.Pow((float)rng.NextDouble(), 1.6f);
                float turn = centre + across * width;
                float radius = spec.radius * (1f + ((float)rng.NextDouble() - 0.5f) * 0.12f);
                float size = Mathf.Lerp(spec.puffMax, spec.puffMin, h01 * 0.7f) * (0.55f + 0.45f * mound) * (0.85f + 0.3f * (float)rng.NextDouble());
                float y = Mathf.Max(spec.baseY + h01 * spec.bankHeight * tall * (0.3f + 0.7f * mound) + size * 0.1f, size * 0.15f);   // the shader fades the bottoms out near the waterline
                float angle = turn * 2f * Mathf.PI;
                Vector3 p = new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius);
                float random = (float)rng.NextDouble();
                int first = vertices.Count;
                for (int k = 0; k < 4; k++)
                {
                    vertices.Add(p);
                    uv0.Add(new Vector2(k & 1, k >> 1));
                    uv1.Add(new Vector2(size, random));
                    colours.Add(new Color32((byte)Mathf.RoundToInt(h01 * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(mound) * 255f), (byte)(random * 255f), 255));
                }

                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
                triangles.Add(first + 2); triangles.Add(first + 3); triangles.Add(first + 1);
            }
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetColors(colours);
        mesh.SetTriangles(triangles, 0);
        // The vertex shader moves the puffs round the circle and out by their size: the bounds are the whole ring.
        float extent = spec.radius * 1.2f + spec.puffMax;
        mesh.bounds = new Bounds(new Vector3(0f, spec.baseY + spec.bankHeight * 0.5f, 0f), new Vector3(extent * 2f, spec.bankHeight + spec.puffMax * 2f, extent * 2f));
        return mesh;
    }

    // `RayShafts` shafts on a line across the sun's side of the sky (the object is turned to the sun's bearing, so +Z is towards the sun), about
    // 950 m out and 150 to 260 m up: FinaleGodRays.shader slants each one down along the light to the sea. uv0 the side and the position along the
    // shaft, uv1 (start height, half width), colour R a random.
    static Mesh BuildRayMesh(int seed)
    {
        System.Random rng = new System.Random(seed);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv1 = new List<Vector2>();
        List<Color32> colours = new List<Color32>();
        List<int> triangles = new List<int>();
        for (int i = 0; i < RayShafts; i++)
        {
            float lateral = Mathf.Lerp(-1f, 1f, (i + 0.5f) / RayShafts) * 900f + ((float)rng.NextDouble() - 0.5f) * 120f;
            float height = Mathf.Lerp(100f, 210f, (float)rng.NextDouble());
            float distance = 1300f + ((float)rng.NextDouble() - 0.5f) * 160f;
            float halfWidth = Mathf.Lerp(16f, 40f, (float)rng.NextDouble());
            float random = (float)rng.NextDouble();
            Vector3 start = new Vector3(lateral, height, distance);
            int first = vertices.Count;
            for (int k = 0; k < 4; k++)
            {
                vertices.Add(start);
                uv0.Add(new Vector2(k & 1, k >> 1));
                uv1.Add(new Vector2(height, halfWidth));
                colours.Add(new Color32((byte)(random * 255f), 0, 0, 255));
            }

            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
            triangles.Add(first + 2); triangles.Add(first + 3); triangles.Add(first + 1);
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetColors(colours);
        mesh.SetTriangles(triangles, 0);
        mesh.bounds = new Bounds(new Vector3(0f, 120f, 0f), new Vector3(4400f, 900f, 4400f));
        return mesh;
    }
}
}
