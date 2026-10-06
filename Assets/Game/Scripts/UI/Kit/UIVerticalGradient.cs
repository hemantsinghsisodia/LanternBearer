using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// Multiplies the graphic's vertex colours by a top-to-bottom gradient. Used for the primary button.
[AddComponentMenu("Lantern Keeper/UI Vertical Gradient")]
public class UIVerticalGradient : BaseMeshEffect
{
    [SerializeField] private Color top = Color.white;
    [SerializeField] private Color bottom = Color.white;

    public Color Top { get { return top; } }
    public Color Bottom { get { return bottom; } }

    public void SetColours(Color topColour, Color bottomColour)
    {
        top = topColour;
        bottom = bottomColour;
        if (graphic != null)
        {
            graphic.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0)
        {
            return;
        }
        List<UIVertex> verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);
        float min = float.MaxValue;
        float max = float.MinValue;
        for (int i = 0; i < verts.Count; i++)
        {
            min = Mathf.Min(min, verts[i].position.y);
            max = Mathf.Max(max, verts[i].position.y);
        }
        float range = Mathf.Max(0.0001f, max - min);
        for (int i = 0; i < verts.Count; i++)
        {
            UIVertex v = verts[i];
            float t = (v.position.y - min) / range;
            v.color = (Color32)((Color)v.color * Color.Lerp(bottom, top, t));
            verts[i] = v;
        }
        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }
}
}
