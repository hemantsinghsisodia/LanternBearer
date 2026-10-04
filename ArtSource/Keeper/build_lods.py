"""LOD1 copies of the keeper meshes: Decimate (collapse) plus smoothing cleanup.

Run:  blender.exe -b ArtSource/Keeper/Keeper.blend -P ArtSource/Keeper/build_lods.py
(after build_outfit.py; saves Keeper.blend)

Every *_LOD0 mesh gets a *_LOD1 copy with the same armature modifier, vertex groups (skin weights), vertex colours
and material slots. Collapse decimation keeps the silhouette; the vertex colours (R sway, G AO, B worn edge) and the
weights are interpolated by the decimator. Prints the triangle totals (targets: LOD0 18,000-25,000, LOD1 6,000-8,000).
"""
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import build_outfit

# triangle targets per mesh (sum about 7,000)
TARGETS = {"Body": 3500, "Hood": 800, "Cape": 2300, "Satchel": 400}


def decimate_copy(src, target):
    name = src.name[:-1] + "1"
    build_outfit.remove_objects([name])
    obj = src.copy()
    obj.data = src.data.copy()
    obj.name = name
    obj.data.name = name
    bpy.context.scene.collection.objects.link(obj)
    ratio = min(1.0, target / float(build_outfit.tri_count(src.data)))
    for attempt in range(3):
        if attempt > 0:
            obj.data = src.data.copy()
            obj.data.name = name
            have = build_outfit.tri_count(obj.data)
        mod = obj.modifiers.new("Decimate", 'DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = ratio
        mod.use_collapse_triangulate = True
        with bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj]):
            bpy.ops.object.modifier_apply(modifier=mod.name)
        got = build_outfit.tri_count(obj.data)
        if abs(got - target) < target * 0.06:
            break
        ratio = max(0.01, min(1.0, ratio * target / float(max(got, 1))))
    # decimation leaves the smoothing flags alone but the face/edge ordering changed: redo creases
    build_outfit.limit_influences([obj])
    is_body = name.startswith("Body")
    build_outfit.shade_smooth_with_sharp(obj.data, 42.0 if is_body else 50.0, material_edges=is_body)
    return obj


def check_colours(lod1):
    """The vertex colours must survive decimation: the face stays a black void, the cowl sway still spans 0..1."""
    cols = lod1.data.color_attributes["Color"].data
    mw = lod1.matrix_world
    if lod1.name.startswith("Body"):
        face = [cols[i].color[1] for i, v in enumerate(lod1.data.vertices) if (mw @ v.co).z > 1.47]
        top = max(face)
        print("COLOUR Body_LOD1 face G max %.3f (expect ~0)" % top)
        assert top < 0.05, "face G not preserved"
    elif lod1.name.startswith("Cape"):
        r = [c.color[0] for c in cols]
        print("COLOUR Cape_LOD1 R range %.3f .. %.3f (expect 0..1)" % (min(r), max(r)))
        assert min(r) < 0.05 and max(r) > 0.9, "cape R not preserved"
    elif lod1.name.startswith("Hood"):
        g = [c.color[1] for c in cols]
        b = [c.color[2] for c in cols]
        print("COLOUR Hood_LOD1 G min %.3f B max %.3f (expect 0 / 1)" % (min(g), max(b)))
        assert min(g) < 0.05 and max(b) > 0.9, "hood colours not preserved"


def main():
    totals = {"LOD0": 0, "LOD1": 0}
    per = {}
    for name in build_outfit.LOD0_NAMES:
        src = bpy.data.objects[name]
        key = name.split("_")[0]
        lod1 = decimate_copy(src, TARGETS[key])
        check_colours(lod1)
        t0 = build_outfit.tri_count(src.data)
        t1 = build_outfit.tri_count(lod1.data)
        per[key] = (t0, t1)
        totals["LOD0"] += t0
        totals["LOD1"] += t1
    for k, (a, b) in per.items():
        print("LOD_TRIS %-8s LOD0 %6d  LOD1 %6d" % (k, a, b))
    print("LOD_TOTALS", totals)
    build_outfit.limit_influences([bpy.data.objects[n] for n in build_outfit.LOD0_NAMES])
    bpy.ops.wm.save_as_mainfile(filepath=build_outfit.BLEND_PATH)


if __name__ == "__main__":
    main()
