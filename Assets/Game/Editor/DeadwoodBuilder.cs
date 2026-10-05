using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
// Own-authored deadwood for the nature-kit biomes: fallen logs and stumps as low-poly tapered cylinders with a few broken-branch nubs.
// The kit's DeadTree models read as spidery branch heaps when laid down (even at 3.5 m), so deadwood uses these instead.
// Prefabs use each island's existing bark material (Nature_BarkDead_<id>) and the same collider rule as every Deadwood prefab
// (PolyHavenImporter.AddCollider: a non-convex MeshCollider).
public static class DeadwoodBuilder
{
    const string Root = "Assets/Game/Art/Environment/Nature";
    const string MeshRoot = Root + "/Meshes";
    const string MaterialRoot = Root + "/Materials";
    const string PrefabFolder = Root + "/Prefabs/Deadwood";
    const int Sides = 9;

    // Model names as the biome table uses them (prefab: Nature_<name>_<levelId>).
    public static readonly string[] Models = { "Wood_Log_1", "Wood_Stump_1", "Wood_Log_2", "Wood_Stump_2" };

    class MeshBuilder
    {
        public List<Vector3> vertices = new List<Vector3>();
        public List<Vector3> normals = new List<Vector3>();
        public List<Vector2> uvs = new List<Vector2>();
        public List<int> triangles = new List<int>();

        // A tube from a start centre to an end centre, with a per-ring radius profile and jitter. The cap faces are flat.
        public void Tube(Vector3 start, Vector3 end, float radiusStart, float radiusEnd, int segments, float wobble, int seed, bool capStart, bool capEnd, float uvScale)
        {
            Vector3 axis = (end - start);
            float length = axis.magnitude;
            axis /= Mathf.Max(0.0001f, length);
            Vector3 side = Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 u = Vector3.Cross(axis, side).normalized;
            Vector3 v = Vector3.Cross(axis, u).normalized;
            System.Random random = new System.Random(seed);
            int baseIndex = vertices.Count;
            for (int s = 0; s <= segments; s++)
            {
                float t = s / (float)segments;
                float radius = Mathf.Lerp(radiusStart, radiusEnd, t) * (1f + ((float)random.NextDouble() - 0.5f) * wobble);
                Vector3 centre = Vector3.Lerp(start, end, t) + (u * Mathf.Sin(t * 5f + seed) + v * Mathf.Cos(t * 4f + seed)) * wobble * 0.25f * radiusStart;
                for (int i = 0; i <= Sides; i++)
                {
                    float angle = i / (float)Sides * Mathf.PI * 2f;
                    Vector3 radial = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                    vertices.Add(centre + radial * radius);
                    normals.Add(radial);
                    uvs.Add(new Vector2(i / (float)Sides, t * length * uvScale));
                }
            }

            int row = Sides + 1;
            for (int s = 0; s < segments; s++)
            {
                for (int i = 0; i < Sides; i++)
                {
                    int a = baseIndex + s * row + i;
                    int b = a + 1;
                    int c = a + row;
                    int d = c + 1;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            if (capStart)
            {
                Cap(baseIndex, -axis, false);
            }

            if (capEnd)
            {
                Cap(baseIndex + segments * row, axis, true);
            }
        }

        // A flat fan over one ring, duplicating the ring so the cap keeps its own normal.
        void Cap(int ringStart, Vector3 normal, bool flip)
        {
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < Sides; i++)
            {
                centre += vertices[ringStart + i];
            }

            centre /= Sides;
            int fan = vertices.Count;
            vertices.Add(centre);
            normals.Add(normal);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= Sides; i++)
            {
                vertices.Add(vertices[ringStart + i % Sides]);
                normals.Add(normal);
                float angle = i / (float)Sides * Mathf.PI * 2f;
                uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
            }

            for (int i = 0; i < Sides; i++)
            {
                triangles.Add(fan);
                triangles.Add(flip ? fan + 2 + i : fan + 1 + i);
                triangles.Add(flip ? fan + 1 + i : fan + 2 + i);
            }
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    // A fallen log lying along X, about half sunk into the ground, with broken-branch nubs on its upper side.
    static Mesh Log(string name, float length, float radius, int seed, int nubs)
    {
        MeshBuilder b = new MeshBuilder();
        float y = radius * 0.8f;
        b.Tube(new Vector3(-length * 0.5f, y, 0f), new Vector3(length * 0.5f, y + radius * 0.15f, 0f), radius * 1.15f, radius * 0.8f, 6, 0.12f, seed, true, true, 0.8f);
        System.Random random = new System.Random(seed + 7);
        for (int i = 0; i < nubs; i++)
        {
            float t = 0.15f + 0.7f * (i + (float)random.NextDouble() * 0.5f) / nubs;
            float x = Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
            float yaw = (-40f + 80f * (float)random.NextDouble()) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(0.35f * (i % 2 == 0 ? 1f : -1f), Mathf.Cos(yaw), Mathf.Sin(yaw)).normalized;
            float local = Mathf.Lerp(radius * 1.15f, radius * 0.8f, t);
            Vector3 root = new Vector3(x, y, 0f) + dir * local * 0.7f;
            float nubLength = 0.35f + 0.25f * (float)random.NextDouble();
            b.Tube(root, root + dir * nubLength, radius * 0.24f, radius * 0.12f, 2, 0.1f, seed + i * 3, false, true, 1f);
        }

        return b.ToMesh(name);
    }

    // A broken stump: a flared cylinder with a ragged top and two root nubs.
    static Mesh Stump(string name, float height, float radius, int seed)
    {
        MeshBuilder b = new MeshBuilder();
        b.Tube(new Vector3(0f, -0.1f, 0f), new Vector3(0f, height, 0f), radius * 1.3f, radius * 0.9f, 4, 0.14f, seed, true, true, 0.8f);
        System.Random random = new System.Random(seed + 11);
        for (int i = 0; i < 2; i++)
        {
            float yaw = (i * 140f + 30f * (float)random.NextDouble()) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(yaw), -0.15f, Mathf.Sin(yaw)).normalized;
            Vector3 root = dir * radius * 1.0f + Vector3.up * 0.15f;
            b.Tube(root, root + dir * (radius * 0.9f) + Vector3.up * 0.05f, radius * 0.28f, radius * 0.1f, 2, 0.1f, seed + i * 5, false, true, 1f);
        }

        // One broken spike rising from the top.
        b.Tube(new Vector3(radius * 0.25f, height - 0.05f, 0f), new Vector3(radius * 0.15f, height + radius * 0.8f, radius * 0.1f), radius * 0.38f, radius * 0.1f, 2, 0.12f, seed + 17, false, true, 1f);
        return b.ToMesh(name);
    }

    static Mesh Model(string model)
    {
        switch (model)
        {
            case "Wood_Log_1":
                return Log(model, 3.4f, 0.27f, 3, 3);
            case "Wood_Log_2":
                return Log(model, 2.4f, 0.22f, 9, 2);
            case "Wood_Stump_1":
                return Stump(model, 0.75f, 0.4f, 5);
            default:
                return Stump(model, 0.5f, 0.5f, 13);
        }
    }

    // Builds the meshes (shared) and one prefab per model and island. Needs the island bark materials (NatureKitImporter.ImportKit).
    public static int BuildAll(string[] levelIds)
    {
        EnsureFolder(MeshRoot);
        EnsureFolder(PrefabFolder);
        int count = 0;
        for (int m = 0; m < Models.Length; m++)
        {
            string meshPath = MeshRoot + "/" + Models[m] + ".asset";
            Mesh fresh = Model(Models[m]);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                AssetDatabase.CreateAsset(fresh, meshPath);
                mesh = fresh;
            }
            else
            {
                EditorUtility.CopySerialized(fresh, mesh);
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(mesh);
            }

            for (int i = 0; i < levelIds.Length; i++)
            {
                Material bark = AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + "/Nature_BarkDead_" + levelIds[i] + ".mat");
                if (bark == null)
                {
                    Debug.LogWarning("DeadwoodBuilder: missing bark material for " + levelIds[i]);
                    continue;
                }

                string name = "Nature_" + Models[m] + "_" + levelIds[i];
                GameObject root = new GameObject(name);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = bark;
                PolyHavenImporter.AddCollider(root, BiomeCategory.Deadwood);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + name + ".prefab");
                Object.DestroyImmediate(root);
                count++;
            }
        }

        return count;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
}
