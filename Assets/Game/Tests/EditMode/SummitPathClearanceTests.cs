using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper.Tests
{
// The keeper got wedged between boulders near the Island 5 summit: the props were placed with no clearance on the spiral path above
// the last trail point. In the saved scene, a keeper-sized capsule must stand on every path sample from u = 0.6 to the lamp plateau
// (centre and 1.2 m either side) without touching a collider, and the lamp plateau around the lighthouse must be open as well.
public class SummitPathClearanceTests
{
    const string ScenePath = "Assets/Game/Scenes/Island5.unity";
    // CharacterController of the keeper: radius 0.28, height 1.7, centre 0.88.
    const float Radius = 0.28f, Height = 1.7f, CentreY = 0.88f;

    static Scene Open(out bool opened)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.isLoaded && loaded.path == ScenePath)
            {
                opened = false;
                return loaded;
            }
        }

        opened = true;
        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }

    // Roots whose colliders belong on the walkway: the beacons the keeper lights, the lamp and lighthouse, the gate.
    static bool Intended(Collider collider)
    {
        string root = collider.transform.root.name;
        return root == "Beacons" || root == "GreatLamp" || root == "SummitGate" || root == "Keeper" || collider is TerrainCollider;
    }

    static void Scan(Terrain terrain, Vector2 xz, float groundReference, List<string> report, string label)
    {
        float ground = terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y;
        if (Mathf.Abs(ground - groundReference) > 0.6f)
        {
            return;   // off the walkway (a chasm or a cliff beside it)
        }

        Vector3 foot = new Vector3(xz.x, ground + 0.05f, xz.y);
        Vector3 bottom = foot + new Vector3(0f, CentreY - Height * 0.5f + Radius, 0f);
        Vector3 top = foot + new Vector3(0f, CentreY + Height * 0.5f - Radius, 0f);
        Collider[] hits = Physics.OverlapCapsule(bottom, top, Radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!Intended(hits[i]))
            {
                report.Add(label + ": " + hits[i].name + " (" + (hits[i].transform.parent != null ? hits[i].transform.parent.name : "-") + ") at " + hits[i].transform.position.ToString("F1"));
            }
        }
    }

    [Test]
    public void SummitPathIsClear()
    {
        bool opened;
        Scene scene = Open(out opened);
        try
        {
            Terrain terrain = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Island")
                {
                    terrain = root.GetComponent<Terrain>();
                }
            }

            Assert.IsNotNull(terrain, "Island 5 terrain");
            Physics.SyncTransforms();
            List<string> report = new List<string>();
            Vector2 previous = SpireLayout.PathPoint(0.6f);
            float travelled = 0.5f;
            int samples = 0;
            for (int i = 0; i <= 4000; i++)
            {
                float u = 0.6f + 0.4f * i / 4000f;
                Vector2 point = SpireLayout.PathPoint(u);
                travelled += Vector2.Distance(previous, point);
                previous = point;
                if (travelled < 0.5f)
                {
                    continue;
                }

                travelled = 0f;
                samples++;
                Vector2 tangent = SpireLayout.PathTangent(u);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                float centreGround = terrain.SampleHeight(new Vector3(point.x, 0f, point.y)) + terrain.transform.position.y;
                float[] offsets = { 0f, 1.2f, -1.2f };
                for (int o = 0; o < offsets.Length; o++)
                {
                    Scan(terrain, point + normal * offsets[o], centreGround, report, "u=" + u.ToString("F3") + " off=" + offsets[o]);
                }
            }

            // The lamp plateau: the ring the keeper walks around the lighthouse.
            for (int k = 0; k < 24; k++)
            {
                float angle = k * Mathf.PI * 2f / 24f;
                Vector2 ring = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (SpireLayout.SummitPlateauRadius - 1f);
                float ground = terrain.SampleHeight(new Vector3(ring.x, 0f, ring.y)) + terrain.transform.position.y;
                Scan(terrain, ring, ground, report, "plateau " + k);
            }

            Assert.Greater(samples, 50, "path samples");
            StringBuilder text = new StringBuilder();
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < report.Count; i++)
            {
                string key = report[i].Substring(report[i].IndexOf(':') + 1);
                if (seen.Add(key))
                {
                    text.AppendLine(report[i]);
                }
            }

            Assert.AreEqual(0, seen.Count, "colliders intrude into the keeper's path:\n" + text);
        }
        finally
        {
            if (opened)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
}
