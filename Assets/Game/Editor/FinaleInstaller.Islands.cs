using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// The detailed distant islands of the finale (Islands 1-4), baked from each island's TerrainData and, read only, from its scene:
//  - Terrain: a 128x128 mesh (and a 64x64 one for Low) with the sea skirt cut away, vertex coloured from the alphamap layers (average colour of
//    each layer's diffuse texture) with steep slopes blended to the rock layer.
//  - Props: the largest trees and rocks of the scene (by bounds), as low-poly stand-ins (the kit has no LODs; its trees are 3k to 10k triangles of
//    leaf cards) in the island's own leaf, bark and rock colours, merged into one mesh.
//  - Beacons: the real beacon model's LOD1 cairn and iron (one mesh), the lantern glass, the light beams, and a flame card per beacon.
// The island scenes are opened additively and closed without saving, so they and their terrain data are never modified.
public static partial class FinaleInstaller
{
    public const int MinProps = 40;
    public const int MaxProps = 80;
    const int MaxTrees = 45;
    // Triangles an island may have in all (terrain + props + beacons).
    public const int TriangleBudget = 25000;
    const int TriangleTarget = 24000;
    const float FlameWidth = 1.1f;
    const float FlameHeight = 1.7f;
    const float FlameFootY = 1.7f;
    const float CairnCell = 0.45f;
    const float IronCell = 0.12f;

    public static string IslandMeshPath(int island, string part)
    {
        return Folder + "/Finale_Island" + island + "_" + part + ".asset";
    }

    public static string SilhouettePath(int island)
    {
        return Folder + "/Silhouette_Island" + island + ".asset";
    }

    public class IslandBake
    {
        public Mesh terrain, terrainLow, props, beaconBase, glass, beams, flame;
        public Vector3 flareLocal;
        public int propCount, beaconCount, treeCount, rockCount;
        public int[] triangles;
    }

    struct PropSpot
    {
        public bool tree;
        public bool pine;
        public bool bush;
        public Vector3 foot;
        public float height, radius, yaw, size;
        public Color32 leaf, bark;
    }

    static readonly Dictionary<string, Color> averageColours = new Dictionary<string, Color>();

    // Deletes assets the detailed islands replace (the 48x48 silhouettes and their material) so no orphans are left behind.
    static void RemoveOldSilhouettes()
    {
        for (int i = 1; i <= 4; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(SilhouettePath(i)) != null)
            {
                AssetDatabase.DeleteAsset(SilhouettePath(i));
            }
        }

        if (AssetDatabase.LoadAssetAtPath<Material>(SilhouetteMaterialPath) != null)
        {
            AssetDatabase.DeleteAsset(SilhouetteMaterialPath);
        }
    }

    // The island scenes' LookApplier writes its fog, ambient and skybox into the global RenderSettings when a scene is opened, and closing the scene does
    // not undo that. The Island 5 scene is saved afterwards, so the settings are put back exactly as they were.
    class RenderSettingsSnapshot
    {
        readonly bool fog;
        readonly FogMode fogMode;
        readonly Color fogColor;
        readonly float fogDensity, fogStart, fogEnd;
        readonly UnityEngine.Rendering.AmbientMode ambientMode;
        readonly Color ambientSky, ambientEquator, ambientGround, ambientLight, subtractiveShadow;
        readonly float ambientIntensity, reflectionIntensity, haloStrength, flareStrength, flareFadeSpeed;
        readonly Material skybox;
        readonly Light sun;
        readonly UnityEngine.Rendering.DefaultReflectionMode reflectionMode;
        readonly Cubemap customReflection;
        readonly int reflectionBounces;
        readonly UnityEngine.Rendering.SphericalHarmonicsL2 ambientProbe;

        public RenderSettingsSnapshot()
        {
            fog = RenderSettings.fog;
            fogMode = RenderSettings.fogMode;
            fogColor = RenderSettings.fogColor;
            fogDensity = RenderSettings.fogDensity;
            fogStart = RenderSettings.fogStartDistance;
            fogEnd = RenderSettings.fogEndDistance;
            ambientMode = RenderSettings.ambientMode;
            ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor;
            ambientGround = RenderSettings.ambientGroundColor;
            ambientLight = RenderSettings.ambientLight;
            ambientIntensity = RenderSettings.ambientIntensity;
            subtractiveShadow = RenderSettings.subtractiveShadowColor;
            reflectionIntensity = RenderSettings.reflectionIntensity;
            reflectionBounces = RenderSettings.reflectionBounces;
            reflectionMode = RenderSettings.defaultReflectionMode;
            customReflection = RenderSettings.customReflectionTexture as Cubemap;
            haloStrength = RenderSettings.haloStrength;
            flareStrength = RenderSettings.flareStrength;
            flareFadeSpeed = RenderSettings.flareFadeSpeed;
            skybox = RenderSettings.skybox;
            sun = RenderSettings.sun;
            ambientProbe = RenderSettings.ambientProbe;
        }

        public void Restore()
        {
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientLight = ambientLight;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.subtractiveShadowColor = subtractiveShadow;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.reflectionBounces = reflectionBounces;
            RenderSettings.defaultReflectionMode = reflectionMode;
            RenderSettings.customReflectionTexture = customReflection;
            RenderSettings.haloStrength = haloStrength;
            RenderSettings.flareStrength = flareStrength;
            RenderSettings.flareFadeSpeed = flareFadeSpeed;
            RenderSettings.skybox = skybox;
            RenderSettings.sun = sun;
            RenderSettings.ambientProbe = ambientProbe;
        }
    }

    // Bakes island `island` (1..4). False (and an error) if its data is missing.
    static bool BakeIsland(int island, out IslandBake bake)
    {
        bake = null;
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath(island));
        if (data == null)
        {
            Debug.LogError("FinaleInstaller: missing " + TerrainPath(island));
            return false;
        }

        RenderSettingsSnapshot renderSettings = new RenderSettingsSnapshot();
        Scene scene = OpenForRead("Assets/Game/Scenes/Island" + island + ".unity", out bool opened);
        try
        {
            Terrain sceneTerrain = FindInScene<Terrain>(scene);
            if (!scene.IsValid() || sceneTerrain == null)
            {
                Debug.LogError("FinaleInstaller: Island" + island + " has no terrain to read.");
                return false;
            }

            Vector3 size = data.size;
            Vector3 basePosition = sceneTerrain.transform.position;
            float seaY = WaterFraction * size.y;
            Vector3 origin = new Vector3(basePosition.x + size.x * 0.5f, basePosition.y + seaY, basePosition.z + size.z * 0.5f);

            // terrain
            TerrainLayer[] layers = data.terrainLayers;
            Color[] layerColours = new Color[layers.Length];
            int cliffLayer = -1;
            for (int k = 0; k < layers.Length; k++)
            {
                layerColours[k] = LayerColour(layers[k]);
                if (layers[k] != null && layers[k].name == "rock") cliffLayer = k;
            }

            int res = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, res, res);
            float[,,] alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            bake = new IslandBake();
            bake.terrain = FinaleIslandMesher.BuildTerrain("Finale_Island" + island + "_Terrain", heights, alpha, layerColours, size, seaY,
                FinaleIslandMesher.LandCells, cliffLayer, SkirtFloor);
            bake.terrainLow = FinaleIslandMesher.BuildTerrain("Finale_Island" + island + "_TerrainLow", heights, alpha, layerColours, size, seaY,
                FinaleIslandMesher.LowCells, cliffLayer, SkirtFloor);
            int terrainTriangles = bake.terrain.triangles.Length / 3;
            if (terrainTriangles == 0)
            {
                Debug.LogError("FinaleInstaller: island " + island + " has no land above the sea.");
                return false;
            }

            Matrix4x4 toIsland = Matrix4x4.Translate(-origin);

            // beacons
            List<Beacon> beacons = new List<Beacon>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                beacons.AddRange(root.GetComponentsInChildren<Beacon>(true));
            }

            beacons.Sort((a, b) =>
            {
                Vector3 pa = a.transform.position, pb = b.transform.position;
                int c = pb.y.CompareTo(pa.y);
                if (c != 0) return c;
                c = pa.x.CompareTo(pb.x);
                return c != 0 ? c : pa.z.CompareTo(pb.z);
            });
            if (beacons.Count == 0)
            {
                Debug.LogError("FinaleInstaller: Island" + island + " has no beacons.");
                return false;
            }

            if (!BakeBeacons(island, beacons, toIsland, bake))
            {
                return false;
            }

            // props
            int beaconTriangles = bake.beaconBase.triangles.Length / 3 + bake.glass.triangles.Length / 3 + bake.beams.triangles.Length / 3 + bake.flame.triangles.Length / 3;
            int propBudget = TriangleTarget - terrainTriangles - beaconTriangles;
            bake.props = BakeProps(island, scene, toIsland, propBudget, bake);
            bake.triangles = new[]
            {
                terrainTriangles, bake.props.triangles.Length / 3, beaconTriangles, bake.terrainLow.triangles.Length / 3
            };
            return true;
        }
        finally
        {
            if (opened)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            renderSettings.Restore();
        }
    }

    static bool BakeBeacons(int island, List<Beacon> beacons, Matrix4x4 toIsland, IslandBake bake)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureInstaller.BeaconPrefabPath);
        Material iron = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconIronMaterialPath);
        Material cairn = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconCairnMaterialPath);
        if (prefab == null || iron == null || cairn == null)
        {
            Debug.LogError("FinaleInstaller: the Beacon prefab or its materials are missing.");
            return false;
        }

        Mesh cairnMesh = null, ironMesh = null, glassMesh = null, beamMesh = null;
        Matrix4x4 cairnPose = Matrix4x4.identity, ironPose = Matrix4x4.identity, glassPose = Matrix4x4.identity, beamPose = Matrix4x4.identity;
        foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            Matrix4x4 pose = prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            switch (renderer.name)
            {
                case "BeaconCairn_L1": cairnMesh = filter.sharedMesh; cairnPose = pose; break;
                case "BeaconIron_L1": ironMesh = filter.sharedMesh; ironPose = pose; break;
                case "BeaconGlass": glassMesh = filter.sharedMesh; glassPose = pose; break;
                case "BeaconBeams": beamMesh = filter.sharedMesh; beamPose = pose; break;
            }
        }

        if (cairnMesh == null || ironMesh == null || glassMesh == null || beamMesh == null)
        {
            Debug.LogError("FinaleInstaller: the Beacon prefab lacks BeaconCairn_L1, BeaconIron_L1, BeaconGlass or BeaconBeams.");
            return false;
        }

        LookProfile look = AssetDatabase.LoadAssetAtPath<LookProfile>("Assets/Game/Art/Look/LookProfile_island" + island + ".asset");
        Color rockTint = look != null ? look.rockTint : Color.white;
        Color cairnColour = MultiplyLinear(AverageColour(cairn.GetTexture("_BaseMap")), rockTint);
        Color ironColour = iron.GetColor("_BaseColor");

        // The beacon model is far too detailed at 300 m: cluster its vertices to a coarse grid (one simplified copy per island, shared by every beacon).
        FinaleIslandMesher.Simplify(cairnMesh.vertices, cairnMesh.triangles, CairnCell, out Vector3[] cairnVertices, out int[] cairnTriangles);
        FinaleIslandMesher.Simplify(ironMesh.vertices, ironMesh.triangles, IronCell, out Vector3[] ironVertices, out int[] ironTriangles);

        FinaleMeshBuilder body = new FinaleMeshBuilder();
        FinaleMeshBuilder glass = new FinaleMeshBuilder();
        FinaleMeshBuilder beams = new FinaleMeshBuilder();
        List<Vector3> flameVertices = new List<Vector3>();
        List<Vector2> flameUvs = new List<Vector2>();
        List<int> flameTriangles = new List<int>();
        List<Vector2> glassUvs = new List<Vector2>();
        List<Vector2> beamUvs = new List<Vector2>();
        for (int b = 0; b < beacons.Count; b++)
        {
            Matrix4x4 world = toIsland * Matrix4x4.TRS(beacons[b].transform.position, beacons[b].transform.rotation, Vector3.one);
            body.AddMesh(cairnVertices, cairnTriangles, world * cairnPose, FinaleIslandMesher.ToVertexColor(cairnColour));
            body.AddMesh(ironVertices, ironTriangles, world * ironPose, FinaleIslandMesher.ToVertexColor(ironColour));
            AddWithUvs(glass, glassUvs, glassMesh, world * glassPose);
            AddWithUvs(beams, beamUvs, beamMesh, world * beamPose);
            Vector3 foot = world.MultiplyPoint3x4(new Vector3(0f, FlameFootY, 0f));
            FinaleIslandMesher.AddCrossedCards(flameVertices, flameUvs, flameTriangles, foot, FlameWidth, FlameHeight);
        }

        bake.beaconBase = body.ToMesh("Finale_Island" + island + "_Base", true);
        bake.glass = ToUvMesh(glass, glassUvs, "Finale_Island" + island + "_Glass");
        bake.beams = ToUvMesh(beams, beamUvs, "Finale_Island" + island + "_Beams");
        Mesh flame = new Mesh();
        flame.name = "Finale_Island" + island + "_Flame";
        flame.SetVertices(flameVertices);
        flame.SetUVs(0, flameUvs);
        flame.SetTriangles(flameTriangles, 0);
        flame.RecalculateBounds();
        bake.flame = flame;
        bake.beaconCount = beacons.Count;
        bake.flareLocal = toIsland.MultiplyPoint3x4(beacons[0].transform.position);
        return true;
    }

    // The glass and beam meshes keep their object space positions and uvs (the shaders read both), moved to the beacon's pose.
    static void AddWithUvs(FinaleMeshBuilder builder, List<Vector2> uvs, Mesh source, Matrix4x4 matrix)
    {
        builder.AddMesh(source.vertices, source.triangles, matrix, new Color32(255, 255, 255, 255));
        List<Vector2> sourceUvs = new List<Vector2>();
        source.GetUVs(0, sourceUvs);
        uvs.AddRange(sourceUvs);
    }

    static Mesh ToUvMesh(FinaleMeshBuilder builder, List<Vector2> uvs, string name)
    {
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.SetVertices(builder.Vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(builder.Triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // The biggest trees and rocks of the island scene as one vertex coloured mesh, at most MaxProps and within `triangleBudget`.
    static Mesh BakeProps(int island, Scene scene, Matrix4x4 toIsland, int triangleBudget, IslandBake bake)
    {
        List<PropSpot> candidates = new List<PropSpot>();
        List<string> ignored = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Props")
            {
                continue;
            }

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Transform child = filter.transform;
                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null)
                {
                    continue;
                }

                string n = child.name;
                bool pine = n.Contains("Pine");
                bool tree = pine || n.Contains("Tree");
                bool bush = !tree && (n.Contains("Bush") || n.Contains("Plant") || n.Contains("Fern"));
                bool rock = !tree && !bush && (n.Contains("Rock") || n.Contains("Pebble") || n.Contains("Stump") || n.Contains("Log"));
                if (!tree && !bush && !rock)
                {
                    if (!ignored.Contains(n)) ignored.Add(n);
                    continue;
                }

                Bounds bounds = renderer.bounds;
                PropSpot spot = new PropSpot();
                spot.tree = tree;
                spot.pine = pine;
                spot.bush = bush;
                Vector3 world = new Vector3(child.position.x, bounds.min.y, child.position.z);
                spot.foot = toIsland.MultiplyPoint3x4(world);
                spot.height = bounds.size.y;
                spot.radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
                spot.yaw = child.eulerAngles.y;
                spot.size = bounds.size.magnitude;
                Color leaf = new Color(0.2f, 0.4f, 0.2f), bark = new Color(0.33f, 0.24f, 0.18f);
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || !material.HasProperty("_BaseColor"))
                    {
                        continue;
                    }

                    if (material.shader.name.Contains("Foliage")) leaf = material.GetColor("_BaseColor") * 0.85f;
                    else if (tree) bark = material.GetColor("_BaseColor");
                    else bark = MultiplyLinear(AverageColour(material.GetTexture("_BaseMap")), material.GetColor("_BaseColor"));
                }

                leaf.a = 1f;
                bark.a = 1f;
                spot.leaf = FinaleIslandMesher.ToVertexColor(leaf);
                spot.bark = FinaleIslandMesher.ToVertexColor(bark);
                candidates.Add(spot);
            }
        }

        // Trees first (the biggest MaxTrees), then rocks, then bushes; the biggest first within each (a stable order for equal sizes: scene order).
        List<int> order = new List<int>();
        for (int i = 0; i < candidates.Count; i++) order.Add(i);
        order.Sort((a, b) =>
        {
            int ga = candidates[a].tree ? 0 : (candidates[a].bush ? 2 : 1), gb = candidates[b].tree ? 0 : (candidates[b].bush ? 2 : 1);
            if (ga != gb) return ga.CompareTo(gb);
            int c = candidates[b].size.CompareTo(candidates[a].size);
            return c != 0 ? c : a.CompareTo(b);
        });

        FinaleMeshBuilder builder = new FinaleMeshBuilder();
        for (int k = 0; k < order.Count && bake.propCount < MaxProps; k++)
        {
            PropSpot spot = candidates[order[k]];
            if (spot.tree && bake.treeCount >= MaxTrees)
            {
                continue;
            }

            int vertexMark = builder.Vertices.Count, triangleMark = builder.Triangles.Count;
            if (spot.tree)
            {
                if (spot.pine) FinaleIslandMesher.AddPine(builder, spot.foot, spot.height, spot.radius, spot.leaf, spot.bark);
                else FinaleIslandMesher.AddBroadleaf(builder, spot.foot, spot.height, spot.radius, spot.leaf, spot.bark);
            }
            else
            {
                // A rock, stump, log or bush: a squat faceted ellipsoid sunk a little into the ground.
                FinaleIslandMesher.AddEllipsoid(builder, spot.foot + Vector3.up * spot.height * 0.35f,
                    new Vector3(spot.radius, spot.height * 0.65f, spot.radius), 7, 3, spot.bush ? spot.leaf : spot.bark);
            }

            if (builder.TriangleCount > triangleBudget)
            {
                // Over the island's triangle budget: take it back.
                builder.Vertices.RemoveRange(vertexMark, builder.Vertices.Count - vertexMark);
                builder.Colors.RemoveRange(vertexMark, builder.Colors.Count - vertexMark);
                builder.Triangles.RemoveRange(triangleMark, builder.Triangles.Count - triangleMark);
                continue;
            }

            bake.propCount++;
            if (spot.tree) bake.treeCount++; else bake.rockCount++; // rocks count the small stand-ins too (stumps, logs, bushes)
        }

        Debug.Log("FinaleInstaller: island " + island + " props: " + candidates.Count + " trees/rocks found, " + bake.propCount + " baked (" + bake.treeCount + " trees, " + bake.rockCount + " rocks, " + builder.TriangleCount + " triangles); ignored kinds: " + string.Join(", ", ignored));

        return builder.ToMesh("Finale_Island" + island + "_Props", true);
    }

    // The average colour (sRGB) of a terrain layer's diffuse texture, times its remap tint.
    static Color LayerColour(TerrainLayer layer)
    {
        if (layer == null)
        {
            return new Color(0.3f, 0.3f, 0.3f, 1f);
        }

        Color average = AverageColour(layer.diffuseTexture);
        return MultiplyLinear(average, layer.diffuseRemapMax);
    }

    static Color MultiplyLinear(Color a, Color b)
    {
        Color l = a.linear;
        Color m = b.linear;
        return new Color(l.r * m.r, l.g * m.g, l.b * m.b, 1f).gamma;
    }

    // The average colour (sRGB) of a texture, decoded from its source file in a fixed stride so the result is deterministic.
    static Color AverageColour(Texture texture)
    {
        string path = texture != null ? AssetDatabase.GetAssetPath(texture) : null;
        if (string.IsNullOrEmpty(path))
        {
            return new Color(0.4f, 0.4f, 0.4f, 1f);
        }

        if (averageColours.TryGetValue(path, out Color cached))
        {
            return cached;
        }

        Color result = new Color(0.4f, 0.4f, 0.4f, 1f);
        string full = System.IO.Path.GetFullPath(path);
        Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        if (System.IO.File.Exists(full) && ImageConversion.LoadImage(decoded, System.IO.File.ReadAllBytes(full), false))
        {
            int w = decoded.width, h = decoded.height;
            int stepX = Mathf.Max(1, w / 128), stepY = Mathf.Max(1, h / 128);
            Color32[] pixels = decoded.GetPixels32();
            double r = 0, g = 0, b = 0;
            int count = 0;
            for (int y = 0; y < h; y += stepY)
            {
                for (int x = 0; x < w; x += stepX)
                {
                    Color c = ((Color)pixels[y * w + x]).linear;
                    r += c.r;
                    g += c.g;
                    b += c.b;
                    count++;
                }
            }

            if (count > 0)
            {
                result = new Color((float)(r / count), (float)(g / count), (float)(b / count), 1f).gamma;
            }
        }
        else
        {
            Debug.LogWarning("FinaleInstaller: cannot decode " + path + "; using a mid grey for its average colour.");
        }

        Object.DestroyImmediate(decoded);
        averageColours[path] = result;
        return result;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    // An island scene is opened additively for a read, unless it is already open. The caller closes it (without saving) when `opened`.
    static Scene OpenForRead(string path, out bool opened)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.isLoaded && loaded.path == path)
            {
                opened = false;
                return loaded;
            }
        }

        opened = true;
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }
}
}
