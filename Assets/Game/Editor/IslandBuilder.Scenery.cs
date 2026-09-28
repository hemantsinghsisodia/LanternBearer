using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
public static partial class IslandBuilder
{
    static Mesh beamMesh;

    static void CreateWater(LevelConfig config, ArtKit art, Stage stage)
    {
        Debug.Log("Water mesh for " + config.sceneName);
        EnsureFolder("Assets/Game/Models/Generated");
        Mesh mesh = BuildWaterMesh(640f);
        Mesh asset = SaveMeshAsset(mesh, "Assets/Game/Models/Generated/WaterMesh.asset");

        GameObject water = new GameObject("Water");
        water.transform.position = new Vector3(0f, stage.waterY, 0f);
        MeshFilter filter = water.AddComponent<MeshFilter>();
        filter.sharedMesh = asset;
        MeshRenderer renderer = water.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = art.water;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

        GameObject volume = new GameObject("WaterVolume");
        volume.transform.position = new Vector3(0f, stage.waterY - 3f, 0f);
        BoxCollider box = volume.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(620f, 6f, 620f);
        WaterHazard hazard = volume.AddComponent<WaterHazard>();
        SerializedObject so = new SerializedObject(hazard);
        so.FindProperty("surfaceY").floatValue = stage.waterY;
        so.FindProperty("penalty").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void UpdateWaterSurface()
    {
        EnsureFolder("Assets/Game/Models/Generated");
        Mesh mesh = BuildWaterMesh(640f);
        SaveMeshAsset(mesh, "Assets/Game/Models/Generated/WaterMesh.asset");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Generated/Water.mat");
        if (mat != null)
        {
            mat.SetFloat("_FadeStart", 520f);
            mat.SetFloat("_FadeEnd", 630f);
            mat.SetFloat("_WaveAmp", 8f);
            mat.SetFloat("_FoamDepth", 1.6f);
            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();
    }

    static Mesh BuildWaterMesh(float radius)
    {
        const int segments = 160;
        const int rings = 48;
        int vertCount = 1 + rings * segments;
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uv = new Vector2[vertCount];
        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0.5f);
        int cursor = 1;
        for (int ring = 1; ring <= rings; ring++)
        {
            float t = ring / (float)rings;
            float dist = radius * Mathf.Pow(t, 1.55f);
            for (int s = 0; s < segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * dist;
                float z = Mathf.Sin(angle) * dist;
                vertices[cursor] = new Vector3(x, 0f, z);
                uv[cursor] = new Vector2(x / radius * 0.5f + 0.5f, z / radius * 0.5f + 0.5f);
                cursor++;
            }
        }

        int triCount = segments + (rings - 1) * segments * 2;
        int[] triangles = new int[triCount * 3];
        int ti = 0;
        for (int s = 0; s < segments; s++)
        {
            int next = (s + 1) % segments;
            triangles[ti++] = 0;
            triangles[ti++] = 1 + next;
            triangles[ti++] = 1 + s;
        }

        for (int ring = 0; ring < rings - 1; ring++)
        {
            int inner = 1 + ring * segments;
            int outer = 1 + (ring + 1) * segments;
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                int a = inner + s;
                int b = inner + next;
                int c = outer + s;
                int d = outer + next;
                triangles[ti++] = a;
                triangles[ti++] = d;
                triangles[ti++] = b;
                triangles[ti++] = a;
                triangles[ti++] = c;
                triangles[ti++] = d;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "WaterMesh";
        mesh.indexFormat = IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        if (mesh.normals != null && mesh.normals.Length > 0 && mesh.normals[0].y < 0f)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int swap = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = swap;
            }

            mesh.triangles = triangles;
            mesh.RecalculateNormals();
        }

        mesh.bounds = new Bounds(Vector3.zero, new Vector3(radius * 2f, 6f, radius * 2f));
        return mesh;
    }

    static Mesh SaveMeshAsset(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        existing.Clear();
        existing.indexFormat = mesh.indexFormat;
        existing.vertices = mesh.vertices;
        existing.uv = mesh.uv;
        existing.triangles = mesh.triangles;
        existing.normals = mesh.normals;
        existing.bounds = mesh.bounds;
        existing.name = mesh.name;
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    static void CreateHorizon(LevelConfig config, ArtKit art, Stage stage)
    {
        bool mesa = config.sceneName == "Island2";
        Shader rangeShader = Shader.Find("LanternKeeper/DistantRange");
        Shader hazeShader = Shader.Find("LanternKeeper/HorizonHaze");
        GameObject root = new GameObject("Horizon");
        root.transform.position = new Vector3(0f, stage.waterY, 0f);

        Color tint = mesa ? new Color(1.32f, 0.74f, 0.48f, 1f) : new Color(0.78f, 0.84f, 0.8f, 1f);
        float[] radii = { 220f, 320f, 450f };
        float[] aerialStart = { 480f, 210f, 140f };
        float[] aerialEnd = { 1200f, 520f, 360f };
        Material isletMat = null;
        for (int band = 0; band < radii.Length; band++)
        {
            Mesh range = BuildRangeMesh(radii[band], config.seed, band, mesa);
            Material mat = DistantMat(rangeShader, art, tint, aerialStart[band], aerialEnd[band]);
            if (band == 0)
            {
                isletMat = mat;
            }

            GameObject go = new GameObject("Range" + band);
            go.transform.SetParent(root.transform, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = range;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        if (isletMat == null)
        {
            isletMat = DistantMat(rangeShader, art, tint, 300f, 700f);
        }

        System.Random random = new System.Random(config.seed + 41);
        int isletCount = 3 + (config.seed % 2);
        float start = (config.seed % 360) * Mathf.Deg2Rad;
        int lighthouseIndex = isletCount - 1;
        float lighthouseTop = 2f;
        Transform lighthouseIslet = null;
        for (int i = 0; i < isletCount; i++)
        {
            float angle = start + i * (Mathf.PI * 2f / isletCount) + ((float)random.NextDouble() - 0.5f) * 0.18f;
            float radius = 128f + (float)random.NextDouble() * 48f;
            if (i == lighthouseIndex)
            {
                radius = 168f + (float)random.NextDouble() * 12f;
            }

            float top;
            Mesh mesh = BuildIsletMesh(config.seed, i, mesa, out top);
            GameObject islet = new GameObject("Islet" + i);
            islet.transform.SetParent(root.transform, false);
            islet.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            MeshFilter filter = islet.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = islet.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = isletMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            int pines = i == lighthouseIndex ? 1 : 2 + random.Next(2);
            for (int p = 0; p < pines; p++)
            {
                float pAngle = (float)random.NextDouble() * Mathf.PI * 2f;
                float pRadius = 1.5f + (float)random.NextDouble() * 4.5f;
                Vector3 local = new Vector3(Mathf.Cos(pAngle) * pRadius, top * 0.55f, Mathf.Sin(pAngle) * pRadius);
                PlacePineSilhouette(islet.transform, local, isletMat, mesa ? 3.2f : 4.4f);
            }

            if (i == lighthouseIndex)
            {
                lighthouseIslet = islet.transform;
                lighthouseTop = top;
            }
        }

        if (lighthouseIslet != null)
        {
            PlaceLighthouse(lighthouseIslet, lighthouseTop, art);
        }

        if (hazeShader != null)
        {
            GameObject haze = new GameObject("Haze");
            haze.transform.SetParent(root.transform, false);
            MeshFilter filter = haze.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildHazeMesh(520f);
            MeshRenderer renderer = haze.AddComponent<MeshRenderer>();
            Material mat = new Material(hazeShader);
            Color hazeColor = mesa ? new Color(0.72f, 0.48f, 0.32f, 0.03f) : new Color(0.62f, 0.74f, 0.82f, 0.035f);
            mat.SetColor("_BaseColor", hazeColor);
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        StripColliders(root);
    }

    static Material DistantMat(Shader shader, ArtKit art, Color tint, float aerialStart, float aerialEnd)
    {
        if (shader == null)
        {
            throw new System.InvalidOperationException("LanternKeeper/DistantRange is missing.");
        }

        Material mat = new Material(shader);
        mat.SetTexture("_RockMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Textures/PolyHaven/rock_face_03_Diffuse.jpg"));
        mat.SetTexture("_RockNormal", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Textures/PolyHaven/rock_face_03_nor_gl.jpg"));
        mat.SetTexture("_GroundMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Textures/PolyHaven/forrest_ground_01_Diffuse.jpg"));
        mat.SetTexture("_GroundNormal", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Textures/PolyHaven/forrest_ground_01_nor_gl.jpg"));
        Cubemap night = art.sky != null ? art.sky.GetTexture("_NightCube") as Cubemap : null;
        Cubemap dawn = art.sky != null ? art.sky.GetTexture("_DawnCube") as Cubemap : null;
        if (night == null)
        {
            night = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Game/Textures/Sky/qwantani_moonrise_puresky_2k.hdr");
        }

        if (dawn == null)
        {
            dawn = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Game/Textures/Sky/qwantani_dawn_puresky_2k.hdr");
        }

        float exposure = art.sky != null && art.sky.HasProperty("_Exposure") ? art.sky.GetFloat("_Exposure") : 1.5f;
        float dawnExposure = art.sky != null && art.sky.HasProperty("_DawnExposure") ? art.sky.GetFloat("_DawnExposure") : exposure;
        mat.SetTexture("_SkyCube", night);
        mat.SetTexture("_DawnCube", dawn);
        mat.SetColor("_Tint", tint);
        mat.SetFloat("_SkyExposure", exposure);
        mat.SetFloat("_DawnExposure", dawnExposure);
        mat.SetFloat("_AerialStart", aerialStart);
        mat.SetFloat("_AerialEnd", aerialEnd);
        mat.SetFloat("_MapScale", 0.016f);
        return mat;
    }

    static float LayeredRidge(float angle, int seed, int band, bool mesa)
    {
        float sum = 0f;
        float weight = 0f;
        float amp = 1f;
        float freq = (mesa ? 2.6f : 1.35f) + band * 0.22f;
        float ox = (seed % 97) * 0.017f + band * 2.7f;
        for (int octave = 0; octave < 4; octave++)
        {
            float a = angle * freq;
            float n = Mathf.PerlinNoise(Mathf.Cos(a) * (1.15f + octave * 0.2f) + ox, Mathf.Sin(a) * (1.15f + octave * 0.2f) + ox * 0.37f + octave * 4.1f);
            float ridge = 1f - Mathf.Abs(n * 2f - 1f);
            ridge = Mathf.Pow(Mathf.Clamp01(ridge), mesa ? 1.65f : 1.22f);
            sum += ridge * amp;
            weight += amp;
            amp *= 0.5f;
            freq *= 2.07f;
            ox += 9.2f;
        }

        float h = sum / Mathf.Max(weight, 0.001f);
        if (mesa)
        {
            h = Mathf.Pow(h, 1.35f);
            h = Mathf.Lerp(h * 0.5f, h, Mathf.SmoothStep(0.6f, 0.88f, h));
        }

        return h;
    }

    static float MesaRow(float t)
    {
        t = Mathf.Clamp01(t);
        float shelf = Mathf.Floor(t * 3.0001f);
        float local = Mathf.Clamp01(t * 3f - shelf);
        float rise = Mathf.SmoothStep(0.62f, 1f, local);
        return Mathf.Clamp01((shelf + rise) / 3f);
    }

    static void AlignNormals(Mesh mesh, bool inward)
    {
        mesh.RecalculateNormals();
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        float facing = 0f;
        int step = Mathf.Max(1, vertices.Length / 48);
        int count = 0;
        for (int i = 0; i < vertices.Length; i += step)
        {
            Vector3 radial = vertices[i];
            radial.y = 0f;
            if (radial.sqrMagnitude < 0.01f)
            {
                continue;
            }

            facing += Vector3.Dot(normals[i], radial.normalized);
            count++;
        }

        bool pointsOut = count > 0 && facing > 0f;
        bool shouldFlip = inward ? pointsOut : !pointsOut;
        if (count == 0 || !shouldFlip)
        {
            mesh.RecalculateBounds();
            return;
        }

        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int swap = triangles[i + 1];
            triangles[i + 1] = triangles[i + 2];
            triangles[i + 2] = swap;
        }

        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    static Mesh BuildRangeMesh(float radius, int seed, int band, bool mesa)
    {
        const int segments = 720;
        const int rows = 7;
        float[] forestAmp = { 40f, 26f, 14f };
        float[] mesaAmp = { 13f, 8f, 5f };
        float amp = (mesa ? mesaAmp : forestAmp)[Mathf.Clamp(band, 0, 2)];
        float lift = mesa ? 2.2f : 5f;
        Vector3[] vertices = new Vector3[segments * rows];
        Vector2[] uv = new Vector2[segments * rows];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float ridge = LayeredRidge(angle, seed, band, mesa);
            float crest = lift + ridge * amp;
            if (band == 0)
            {
                float saddle = mesa ? 0.6f : 1.4f;
                float peak = mesa ? 7f : 14f;
                crest = Mathf.Lerp(saddle, peak, ridge);
            }

            float jut = (ridge - 0.4f) * (mesa ? 3f : 8f);
            for (int r = 0; r < rows; r++)
            {
                float t = r / (float)(rows - 1);
                float shaped = mesa ? MesaRow(t) : Mathf.Pow(t, 0.85f);
                float y = Mathf.Lerp(-8f, crest, shaped);
                float rad = Mathf.Lerp(radius - 1.5f, radius + 12f + jut, t);
                int v = i * rows + r;
                vertices[v] = new Vector3(Mathf.Cos(angle) * rad, y, Mathf.Sin(angle) * rad);
                uv[v] = new Vector2(i / (float)segments, t);
            }
        }

        int[] triangles = new int[segments * (rows - 1) * 6];
        int ti = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            for (int r = 0; r < rows - 1; r++)
            {
                int a = i * rows + r;
                int b = i * rows + r + 1;
                int c = next * rows + r;
                int d = next * rows + r + 1;
                triangles[ti++] = a;
                triangles[ti++] = b;
                triangles[ti++] = c;
                triangles[ti++] = c;
                triangles[ti++] = b;
                triangles[ti++] = d;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "HorizonRange";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        AlignNormals(mesh, true);
        return mesh;
    }

    static Mesh BuildIsletMesh(int seed, int index, bool mesa, out float topHeight)
    {
        const int segments = 20;
        const int rows = 6;
        System.Random random = new System.Random(seed + 90 + index * 19);
        topHeight = mesa ? 3.2f + (float)random.NextDouble() * 1.5f : 5.4f + (float)random.NextDouble() * 2.8f;
        float radius = 14f + (float)random.NextDouble() * 8f;
        float foot = -7.4f;
        float[] yT = { 0f, 0.16f, 0.34f, 0.56f, 0.8f, 1f };
        float[] rT = { 0.55f, 1.14f, 1f, 0.72f, 0.4f, 0.1f };
        float[] jitter = new float[segments];
        for (int i = 0; i < segments; i++)
        {
            jitter[i] = 0.8f + (float)random.NextDouble() * 0.38f;
        }

        Vector3[] vertices = new Vector3[segments * rows];
        Vector2[] uv = new Vector2[segments * rows];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            for (int r = 0; r < rows; r++)
            {
                float t = yT[r];
                float shaped = mesa ? MesaRow(t) : t;
                float y = Mathf.Lerp(foot, topHeight, shaped);
                if (mesa && r >= rows - 2)
                {
                    y = Mathf.Lerp(topHeight * 0.76f, topHeight, r - (rows - 2));
                }

                float rad = radius * rT[r] * Mathf.Lerp(1f, jitter[i], 0.3f + 0.7f * (1f - t));
                int v = i * rows + r;
                vertices[v] = new Vector3(Mathf.Cos(angle) * rad, y, Mathf.Sin(angle) * rad);
                uv[v] = new Vector2(i / (float)segments, Mathf.InverseLerp(foot, topHeight, y));
            }
        }

        int[] triangles = new int[segments * (rows - 1) * 6];
        int ti = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            for (int r = 0; r < rows - 1; r++)
            {
                int a = i * rows + r;
                int b = i * rows + r + 1;
                int c = next * rows + r;
                int d = next * rows + r + 1;
                triangles[ti++] = a;
                triangles[ti++] = b;
                triangles[ti++] = c;
                triangles[ti++] = c;
                triangles[ti++] = b;
                triangles[ti++] = d;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "Islet";
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        AlignNormals(mesh, false);
        return mesh;
    }

    static Mesh BuildHazeMesh(float radius)
    {
        const int segments = 64;
        Vector3[] vertices = new Vector3[segments * 2];
        Vector2[] uv = new Vector2[segments * 2];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float c = Mathf.Cos(angle);
            float s = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(c * radius, -1.2f, s * radius);
            vertices[i * 2 + 1] = new Vector3(c * radius, 6.5f, s * radius);
            uv[i * 2] = new Vector2(i / (float)segments, 0f);
            uv[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
        }

        int[] triangles = new int[segments * 6];
        int ti = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = ((i + 1) % segments) * 2;
            int a = i * 2;
            triangles[ti++] = a;
            triangles[ti++] = a + 1;
            triangles[ti++] = next;
            triangles[ti++] = next;
            triangles[ti++] = a + 1;
            triangles[ti++] = next + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name = "HorizonHaze";
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    static void PlacePineSilhouette(Transform parent, Vector3 localPos, Material mat, float height)
    {
        MakeCone("Pine", parent, localPos, new Vector3(height * 0.34f, height * 0.42f, height * 0.34f), mat, Quaternion.identity);
        MakeCone("PineTip", parent, localPos + new Vector3(0f, height * 0.55f, 0f), new Vector3(height * 0.2f, height * 0.34f, height * 0.2f), mat, Quaternion.identity);
    }

    static void PlaceLighthouse(Transform islet, float top, ArtKit art)
    {
        GameObject house = new GameObject("Lighthouse");
        house.transform.SetParent(islet, false);
        float towerHeight = 13f;
        GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tower.name = "Tower";
        tower.transform.SetParent(house.transform, false);
        tower.transform.localPosition = new Vector3(0f, top + towerHeight * 0.5f, 0f);
        tower.transform.localScale = new Vector3(1.7f, towerHeight * 0.5f, 1.7f);
        tower.GetComponent<Renderer>().sharedMaterial = art.rock;
        StripColliders(tower);

        MakeCone("Roof", house.transform, new Vector3(0f, top + towerHeight + 1.1f, 0f), new Vector3(2.4f, 1.3f, 2.4f), art.iron, Quaternion.identity);

        GameObject pivot = new GameObject("LampPivot");
        pivot.transform.SetParent(house.transform, false);
        pivot.transform.localPosition = new Vector3(0f, top + towerHeight + 0.35f, 0f);
        Vector3 outward = islet.position;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f)
        {
            outward = Vector3.forward;
        }

        pivot.transform.rotation = Quaternion.LookRotation(-outward.normalized, Vector3.up);

        GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lamp.name = "Lamp";
        lamp.transform.SetParent(pivot.transform, false);
        lamp.transform.localPosition = Vector3.zero;
        lamp.transform.localScale = Vector3.one * 0.85f;
        lamp.GetComponent<Renderer>().sharedMaterial = art.lamp;
        StripColliders(lamp);

        GameObject lightObject = new GameObject("LampLight");
        lightObject.transform.SetParent(pivot.transform, false);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.86f, 0.62f);
        light.intensity = 6.5f;
        light.range = 46f;
        light.shadows = LightShadows.None;

        Material coreMat = new Material(art.beam);
        coreMat.SetColor("_BaseColor", new Color(1f, 0.94f, 0.78f, 0.1f));
        Material softMat = new Material(art.beam);
        softMat.SetColor("_BaseColor", new Color(0.75f, 0.84f, 1f, 0.035f));
        CreateBeam("BeamCore", pivot.transform, coreMat, new Vector3(7f, 7f, 78f));
        CreateBeam("BeamSoft", pivot.transform, softMat, new Vector3(18f, 18f, 70f));

        LighthouseBeam script = pivot.AddComponent<LighthouseBeam>();
        SerializedObject beamObject = new SerializedObject(script);
        beamObject.FindProperty("beam").objectReferenceValue = pivot.transform;
        beamObject.FindProperty("lamp").objectReferenceValue = light;
        beamObject.FindProperty("lampGlow").objectReferenceValue = lamp.GetComponent<Renderer>();
        beamObject.FindProperty("degreesPerSecond").floatValue = 13f;
        beamObject.FindProperty("blinkPeriod").floatValue = 2.6f;
        beamObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateBeam(string name, Transform parent, Material mat, Vector3 scale)
    {
        GameObject beam = new GameObject(name);
        beam.transform.SetParent(parent, false);
        beam.transform.localPosition = new Vector3(0f, 0f, scale.z * 0.5f);
        beam.transform.localScale = scale;
        MeshFilter filter = beam.AddComponent<MeshFilter>();
        filter.sharedMesh = BeamMesh();
        MeshRenderer renderer = beam.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static Mesh BeamMesh()
    {
        if (beamMesh != null)
        {
            return beamMesh;
        }

        const int segments = 10;
        Vector3[] vertices = new Vector3[segments + 1];
        Color[] colors = new Color[segments + 1];
        Vector2[] uv = new Vector2[segments + 1];
        vertices[0] = new Vector3(0f, 0f, 0.5f);
        colors[0] = Color.white;
        uv[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, -0.5f);
            colors[i + 1] = Color.white;
            uv[i + 1] = new Vector2(0.5f, 0.5f);
        }

        int[] triangles = new int[segments * 3];
        int ti = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = i + 1 == segments ? 1 : i + 2;
            triangles[ti++] = 0;
            triangles[ti++] = i + 1;
            triangles[ti++] = next;
        }

        Mesh mesh = new Mesh();
        mesh.name = "LighthouseBeam";
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        beamMesh = mesh;
        return mesh;
    }

    static void CreateReflectionProbe(Stage stage)
    {
        GameObject probeObject = new GameObject("SkyProbe");
        probeObject.transform.position = new Vector3(0f, 28f, 0f);
        ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.resolution = 128;
        probe.hdr = true;
        probe.shadowDistance = 30f;
        probe.intensity = 1f;
        probe.boxProjection = false;
        probe.size = new Vector3(980f, 320f, 980f);
        probe.center = new Vector3(0f, -8f, 0f);
        probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
        probe.cullingMask = ~0;
        probe.renderDynamicObjects = false;
        probe.RenderProbe();
    }

    [MenuItem("Lantern Keeper/Refresh Sea Sky Horizon")]
    public static void RefreshSeaSkyAndHorizon()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play mode before refreshing the horizon.");
            return;
        }

        Material water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Generated/Water.mat");
        Material sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Generated/NightSky.mat");
        ApplySkyExposure(sky, water);
        AssetDatabase.SaveAssets();

        ArtKit art = new ArtKit();
        art.rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Generated/Rock.mat");
        art.iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Generated/TorchIron.mat");
        art.lamp = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Generated/LighthouseLamp.mat");
        art.beam = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Generated/LighthouseBeam.mat");
        art.sky = sky;
        art.water = water;

        EditorSceneManager.SaveOpenScenes();
        RebuildHorizonScene("Assets/Game/Scenes/Island1.unity", AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/Game/Levels/Island1.asset"), art);
        RebuildHorizonScene("Assets/Game/Scenes/Island2.unity", AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/Game/Levels/Island2.asset"), art);
        LevelConfig menu = ScriptableObject.CreateInstance<LevelConfig>();
        menu.sceneName = "MainMenu";
        menu.seed = 1101;
        menu.hillHeight = 7.5f;
        RebuildHorizonScene("Assets/Game/Scenes/MainMenu.unity", menu, art);
        Object.DestroyImmediate(menu);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Game/Scenes/Island1.unity");
        Debug.Log("Refreshed sea, sky, and horizon.");
    }

    static void RebuildHorizonScene(string scenePath, LevelConfig config, ArtKit art)
    {
        if (config == null)
        {
            Debug.LogError("Missing level config for " + scenePath);
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        float waterY = WaterFraction * config.hillHeight;
        GameObject waterObject = GameObject.Find("Water");
        if (waterObject != null)
        {
            waterY = waterObject.transform.position.y;
        }

        GameObject horizon = GameObject.Find("Horizon");
        if (horizon != null)
        {
            DestroyGeneratedHorizon(horizon);
        }

        if (art.sky != null)
        {
            Material current = RenderSettings.skybox;
            if (current != null && current.shader == art.sky.shader && current != art.sky)
            {
                current.CopyPropertiesFromMaterial(art.sky);
                EditorUtility.SetDirty(current);
            }
            else
            {
                RenderSettings.skybox = art.sky;
            }
        }

        Stage stage = new Stage();
        stage.waterY = waterY;
        CreateHorizon(config, art, stage);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Rebuilt horizon in " + scenePath + " waterY=" + waterY.ToString("0.00"));
    }

    static void DestroyGeneratedHorizon(GameObject horizon)
    {
        var meshes = new List<Mesh>();
        MeshFilter[] filters = horizon.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            if (mesh == null || AssetDatabase.Contains(mesh))
            {
                continue;
            }

            if (mesh.name == "HorizonRange" || mesh.name == "Islet" || mesh.name == "HorizonHaze" || mesh.name == "LighthouseBeam")
            {
                meshes.Add(mesh);
            }
        }

        var materials = new List<Material>();
        Renderer[] renderers = horizon.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material mat = renderers[i].sharedMaterial;
            if (mat == null || AssetDatabase.Contains(mat) || mat.shader == null)
            {
                continue;
            }

            string shaderName = mat.shader.name;
            if (shaderName == "LanternKeeper/DistantRange" || shaderName == "LanternKeeper/Silhouette" || shaderName == "LanternKeeper/HorizonHaze" || shaderName == "LanternKeeper/AdditiveUnlit")
            {
                materials.Add(mat);
            }
        }

        Object.DestroyImmediate(horizon);
        for (int i = 0; i < meshes.Count; i++)
        {
            Object.DestroyImmediate(meshes[i]);
        }

        for (int i = 0; i < materials.Count; i++)
        {
            Object.DestroyImmediate(materials[i]);
        }

        beamMesh = null;
    }
}
}
