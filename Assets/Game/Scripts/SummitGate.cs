using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// Island 5: opens when every beacon is lit. A light column then rises from each beacon (BridgeTimings.ColumnSeconds) and, once they
// have risen, the summit span grows. The great lamp can be lit as soon as the gate is Open.
public class SummitGate : MonoBehaviour
{
    const float ColumnHeight = 30f;
    const float ColumnWidth = 2.0f;

    [SerializeField] int required = 8;
    [SerializeField] Material columnMaterial;

    static Mesh columnMesh;

    readonly List<Transform> columns = new List<Transform>();
    float columnTime;
    bool spanStarted;

    public bool Open { get; private set; }
    public int LitCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Beacon.All.Count; i++)
            {
                if (Beacon.All[i] != null && Beacon.All[i].IsLit)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int Required => required;
    public bool SpanStarted => spanStarted;
    public float ColumnProgress => Open ? Mathf.Clamp01(columnTime / BridgeTimings.ColumnSeconds) : 0f;

    void OnEnable()
    {
        Beacon.Lit += OnLit;
    }

    void OnDisable()
    {
        Beacon.Lit -= OnLit;
    }

    void Start()
    {
        Check();
    }

    void OnLit(Beacon beacon)
    {
        Check();
    }

    void Check()
    {
        if (Open || LitCount < required)
        {
            return;
        }

        Open = true;
        columnTime = 0f;
        BuildColumns();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCue2D(SoundCues.BeaconIgnite);
        }
    }

    void Update()
    {
        if (!Open)
        {
            return;
        }

        if (columnTime < BridgeTimings.ColumnSeconds)
        {
            columnTime += Time.deltaTime;
            float rise = Mathf.SmoothStep(0f, 1f, ColumnProgress);
            for (int i = 0; i < columns.Count; i++)
            {
                if (columns[i] != null)
                {
                    columns[i].localScale = new Vector3(ColumnWidth, Mathf.Max(0.01f, ColumnHeight * rise), ColumnWidth);
                }
            }
        }

        if (!spanStarted && columnTime >= BridgeTimings.ColumnSeconds)
        {
            spanStarted = true;
            LightBridge.BeginSummit();
        }
    }

    void BuildColumns()
    {
        Material material = columnMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("LanternKeeper/AdditiveUnlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            material = new Material(shader);
            material.name = "SummitColumn (runtime)";
            material.SetColor("_BaseColor", new Color(1f, 0.78f, 0.45f, 0.5f));
        }

        Mesh mesh = ColumnMesh();
        for (int i = 0; i < Beacon.All.Count; i++)
        {
            Beacon beacon = Beacon.All[i];
            if (beacon == null)
            {
                continue;
            }

            GameObject column = new GameObject("SummitColumn_" + beacon.name);
            column.transform.SetParent(transform, false);
            column.transform.position = beacon.transform.position + Vector3.up * 1.2f;
            column.transform.localScale = new Vector3(ColumnWidth, 0.01f, ColumnWidth);
            column.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = column.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            columns.Add(column.transform);
        }
    }

    // Two crossed unit quads (x/z from -0.5 to 0.5, y from 0 to 1). The texture's soft blob has its centre at v = 0.5, so the
    // quad samples only the upper half: bright at the foot, fading to nothing at the top and at the sides.
    static Mesh ColumnMesh()
    {
        if (columnMesh != null)
        {
            return columnMesh;
        }

        columnMesh = new Mesh();
        columnMesh.name = "SummitColumn";
        columnMesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f),
            new Vector3(0f, 0f, -0.5f), new Vector3(0f, 0f, 0.5f), new Vector3(0f, 1f, -0.5f), new Vector3(0f, 1f, 0.5f)
        };
        Vector2[] uv =
        {
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(1f, 1f)
        };
        columnMesh.uv = uv;
        columnMesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 };
        columnMesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
        return columnMesh;
    }
}
}
