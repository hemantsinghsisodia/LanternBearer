"""Cloak clipping check over the gameplay clips.

Run:  blender.exe -b ArtSource/Keeper/Keeper.blend -P ArtSource/Keeper/check_clipping.py -- [spot OUTDIR]

For each clip and sampled frame it evaluates the deformed meshes and reports
  A  body vertices that were covered by the cloak in Idle frame 1 and now poke through its side or back wall
     (excluding the open front and a 12 degree margin around it), with the largest protrusion in cm
  B  cloak outer-shell vertices that end up inside the body, with the largest depth in cm
With `spot OUTDIR` it also renders side and back views of each worst frame to OUTDIR.
"""
import math
import os
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

CLIPS = {
    "Idle": [1, 13, 26, 39, 51],
    "Walk": [1, 6, 11, 16, 21, 26, 31, 36, 41],
    "Run": [1, 5, 9, 13, 17, 21, 25],
    "Roll": [1, 6, 11, 16, 21, 26, 31, 36, 41],
    "Interact": [1, 8, 16, 24, 32, 39],
    "HitRecieve": [1, 5, 9, 13, 18],
    "Death": [1, 6, 12, 18, 24, 33],
    "LanternHold": [1],
}
BODY = ["Medieval_Body", "Medieval_Head", "Medieval_Legs", "Medieval_Feet"]
NCOLS = 24            # keep in step with build_outfit.build_cloak
HAND_GROUPS = ("Index", "Middle", "Ring", "Pinky", "Thumb")


def set_pose(arm, clip, frame):
    act = bpy.data.actions["CharacterArmature|" + clip]
    arm.animation_data.action = act
    if hasattr(arm.animation_data, "action_slot") and act.slots:
        arm.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()


def evaluated(obj, dg):
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    mw = ev.matrix_world
    verts = [mw @ v.co for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]
    return ev, me, verts, polys


def body_data(dg):
    allv, allp, tags = [], [], []
    for name in BODY:
        obj = bpy.data.objects[name]
        ev, me, verts, polys = evaluated(obj, dg)
        names = {g.index: g.name for g in obj.vertex_groups}
        base = len(allv)
        for i, v in enumerate(verts):
            grp = max(me.vertices[i].groups, key=lambda g: g.weight, default=None)
            gname = names[grp.group] if grp else ""
            tags.append((name, gname))
        allv += verts
        allp += [tuple(base + i for i in p) for p in polys]
        ev.to_mesh_clear()
    return allv, allp, tags


def cloak_data(dg):
    obj = bpy.data.objects["Cloak"]
    ev, me, verts, polys = evaluated(obj, dg)
    ev.to_mesh_clear()
    outer = [p for p in polys if all(i % 2 == 0 for i in p)]
    return verts, outer


AXIS = Vector((0.0, -0.04, 0.0))
# Half-opening angle of the cloak's open front (degrees) by height; keep in step with CLOAK_RINGS.
OPENING = [(1.465, 12.0), (1.415, 18.0), (1.34, 34.0), (1.20, 42.0), (1.02, 46.0), (0.84, 48.0), (0.66, 50.0),
           (0.52, 52.0)]
OPENING_MARGIN = 12.0   # body coming out within this many degrees of the opening is not a wall poke-through


def in_front_opening(v):
    z = v.z
    if z >= OPENING[0][0]:
        half = OPENING[0][1]
    elif z <= OPENING[-1][0]:
        half = OPENING[-1][1]
    else:
        half = OPENING[-1][1]
        for (z0, a0), (z1, a1) in zip(OPENING, OPENING[1:]):
            if z1 <= z <= z0:
                half = a0 + (a1 - a0) * (z0 - z) / (z0 - z1)
                break
    phi = math.degrees(math.atan2(abs(v.x - AXIS.x), -(v.y - AXIS.y)))
    return phi < half + OPENING_MARGIN


def radial(v):
    d = Vector((v.x - AXIS.x, v.y - AXIS.y, 0.0))
    return d.normalized() if d.length > 1e-6 else Vector((0.0, -1.0, 0.0))


def covered(bvh, v):
    """True when the cloak has a surface further out along the horizontal radial direction."""
    hit = bvh.ray_cast(v + radial(v) * 0.002, radial(v), 1.5)
    return hit[0] is not None


def protrusion(bvh, v):
    """Distance back towards the axis to the nearest cloak surface, or None when none within 12 cm."""
    d = -radial(v)
    hit = bvh.ray_cast(v + d * 0.002, d, 0.12)
    return None if hit[0] is None else hit[3]


def measure(base_covered):
    dg = bpy.context.evaluated_depsgraph_get()
    bv, bp, tags = body_data(dg)
    cv, cp = cloak_data(dg)
    cloak_bvh = BVHTree.FromPolygons(cv, cp)
    body_bvh = BVHTree.FromPolygons(bv, bp)
    stats = {"A_count": 0, "A_max": 0.0, "A_where": "", "B_count": 0, "B_max": 0.0, "covered": set()}
    for i, v in enumerate(bv):
        name, grp = tags[i]
        if v.z < 0.45 or grp.startswith(HAND_GROUPS) or grp.startswith("Wrist"):
            continue
        cov = covered(cloak_bvh, v)
        if cov:
            stats["covered"].add(i)
        if base_covered is not None and i in base_covered and not cov:
            d = None if in_front_opening(v) else protrusion(cloak_bvh, v)
            if d is not None:
                stats["A_count"] += 1
                if d > stats["A_max"]:
                    stats["A_max"] = d
                    stats["A_where"] = "%s/%s" % (name.replace("Medieval_", ""), grp)
                    stats["A_pos"] = (round(v.x, 2), round(v.y, 2), round(v.z, 2))
    for vi in set(i for p in cp for i in p):
        v = cv[vi]
        loc, n, fi, dist = body_bvh.find_nearest(v, 0.06)
        if loc is None:
            continue
        if (v - loc).dot(n) < 0.0 and dist > 0.005:
            stats["B_count"] += 1
            stats["B_max"] = max(stats["B_max"], dist)
    return stats


def main():
    arm = bpy.data.objects["CharacterArmature"]
    set_pose(arm, "Idle", 1)
    base = measure(None)["covered"]
    print("BASELINE covered body verts:", len(base))
    worst = []
    for clip, frames in CLIPS.items():
        for f in frames:
            set_pose(arm, clip, f)
            s = measure(base)
            if s["A_max"] > 0.03:
                print("   worst vertex", s.get("A_pos"))
            print("CLIP %-11s f%02d  A:%4d max %5.1f cm %-22s  B:%4d max %5.1f cm" % (
                clip, f, s["A_count"], s["A_max"] * 100.0, s["A_where"], s["B_count"], s["B_max"] * 100.0))
            worst.append((max(s["A_max"], s["B_max"]), clip, f))
    worst.sort(reverse=True)
    print("WORST", [(round(w * 100, 1), c, f) for w, c, f in worst[:8]])


main()
