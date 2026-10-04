"""Clipping check over the gameplay clips: the two-layer cowl against Body_LOD0, plus the sash, skirt and tail.

Run:  blender.exe -b ArtSource/Keeper/Keeper.blend -P ArtSource/Keeper/check_clipping.py [-- Cape_base Body_LOD0]

The cowl is measured on Cape_base, the unsubdivided render-hidden copy of Cape_LOD0 (the subdivided mesh renumbers
vertices). For each clip and sampled frame it evaluates the deformed meshes and reports
  A  body vertices (hands, head and neck excluded) that were covered by the cowl in Idle frame 1 and now poke
     through its side or back wall (excluding the open front and a 12 degree margin around it), largest in cm
  B  cowl inner-shell vertices that end up inside the body (collar under the hood excluded), largest depth in cm
  tail  skirt and sash-tail inner vertices inside the legs, largest depth in cm
and a per-clip worst value. Target: <= 3 cm for Walk, Run, Interact and LanternHold (LanternHold is evaluated on
top of Idle because it keys only the arm bones).
Render spot checks of any clip/frame with render_keeper.py (`-- spot OUTDIR Clip:frame ...`).
"""
import math
import os
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import build_outfit

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
CAPE_NAME = argv[0] if len(argv) > 0 else "Cape_base"   # unsubdivided proxy of Cape_LOD0
BODY_NAME = argv[1] if len(argv) > 1 else "Body_LOD0"

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
HAND_GROUPS = ("Index", "Middle", "Ring", "Pinky", "Thumb", "Wrist")
AXIS = Vector((build_outfit.AXIS_X, build_outfit.AXIS_Y, 0.0))
# Half-opening angle of the cape's open front (degrees) by height, straight from the cape profile.
OPENING = [(k[0], k[4]) for k in build_outfit.COWL_LAYERS[0]["keys"]]
OPENING_MARGIN = 12.0   # body coming out within this many degrees of the opening is not a wall poke-through
MIN_Z = 0.95            # the cape does not reach below this height


_cape = bpy.data.objects[CAPE_NAME]
SASH_FIRST = _cape.data.get("sash_first_vertex", 10 ** 9)
_col = _cape.data.color_attributes["Color"].data
LEGS_FIRST = _cape.data.get("legs_first_vertex", 10 ** 9)
TAIL = [i for i in range(len(_cape.data.vertices)) if i >= LEGS_FIRST and i % 2 == 1]


def set_pose(arm, clip, frame):
    build_outfit.reset_pose(arm)          # some clips (LanternHold) key only a few bones
    if clip == "LanternHold":
        build_outfit.set_pose(arm, "Idle", 1)   # in game it is a layer on top of the base locomotion
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
    obj = bpy.data.objects[BODY_NAME]
    ev, me, verts, polys = evaluated(obj, dg)
    names = {g.index: g.name for g in obj.vertex_groups}
    tags = []
    for i in range(len(verts)):
        grp = max(me.vertices[i].groups, key=lambda g: g.weight, default=None)
        tags.append(names[grp.group] if grp else "")
    ev.to_mesh_clear()
    return verts, polys, tags


def cape_data(dg):
    obj = bpy.data.objects[CAPE_NAME]
    ev, me, verts, polys = evaluated(obj, dg)
    ev.to_mesh_clear()
    outer = [p for p in polys if all(i % 2 == 0 and i < SASH_FIRST for i in p)]   # cowl layers only
    return verts, polys, outer


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
    """True when the cape has a surface further out along the horizontal radial direction."""
    hit = bvh.ray_cast(v + radial(v) * 0.002, radial(v), 1.5)
    return hit[0] is not None


def protrusion(bvh, v):
    """Distance back towards the axis to the nearest cape surface, or None when none within 12 cm."""
    d = -radial(v)
    hit = bvh.ray_cast(v + d * 0.002, d, 0.12)
    return None if hit[0] is None else hit[3]


DIRS = [Vector(d) for d in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))]


def really_inside(bvh, v, dist):
    """Normal-side test, confirmed by a six-ray vote when deep (the body mesh is open at the back of the head)."""
    if dist <= 0.02:
        return True
    hits = sum(1 for d in DIRS if bvh.ray_cast(v + d * 0.001, d, 2.0)[0] is not None)
    return hits >= 5


def measure(base_covered):
    dg = bpy.context.evaluated_depsgraph_get()
    bv, bp, tags = body_data(dg)
    cv, cpolys, couter = cape_data(dg)
    outer_bvh = BVHTree.FromPolygons(cv, couter)
    body_bvh = BVHTree.FromPolygons(bv, bp)
    stats = {"A_count": 0, "A_max": 0.0, "A_where": "", "B_count": 0, "B_max": 0.0, "B_where": "", "covered": set()}
    for i, v in enumerate(bv):
        grp = tags[i]
        if v.z < MIN_Z or v.z > 1.40 or grp.startswith(HAND_GROUPS + ("Neck", "Head")):
            continue
        cov = covered(outer_bvh, v)
        if cov:
            stats["covered"].add(i)
        if base_covered is not None and i in base_covered and not cov:
            d = None if in_front_opening(v) else protrusion(outer_bvh, v)
            if d is not None:
                stats["A_count"] += 1
                if d > stats["A_max"]:
                    stats["A_max"] = d
                    stats["A_where"] = grp
                    stats["A_pos"] = (round(v.x, 2), round(v.y, 2), round(v.z, 2))
    for vi in set(i for p in cpolys for i in p if i % 2 == 1):
        v = cv[vi]
        if vi >= SASH_FIRST:
            continue
        if v.z > 1.42:
            continue                      # the collar is under the hood
        loc, n, fi, dist = body_bvh.find_nearest(v, 0.08)
        if loc is None:
            continue
        if (v - loc).dot(n) < 0.0 and dist > 0.005 and really_inside(body_bvh, v, dist):
            stats["B_count"] += 1
            if dist > stats["B_max"]:
                stats["B_max"] = dist
                stats["B_where"] = "%s@z%.2f" % (tags[bp[fi][0]], v.z)
    # C: sash tail inner-shell vertices inside the body (legs)
    stats["C_max"] = 0.0
    for vi in TAIL:
        v = cv[vi]
        loc, n, fi, dist = body_bvh.find_nearest(v, 0.08)
        if loc is not None and (v - loc).dot(n) < 0.0 and dist > 0.003 and really_inside(body_bvh, v, dist):
            stats["C_max"] = max(stats["C_max"], dist)
    return stats


def main():
    arm = bpy.data.objects["CharacterArmature"]
    set_pose(arm, "Idle", 1)
    base = measure(None)["covered"]
    print("BASELINE covered body verts:", len(base), "tail verts:", len(TAIL))
    per_clip = {}
    for clip, frames in CLIPS.items():
        for f in frames:
            set_pose(arm, clip, f)
            s = measure(base)
            print("CLIP %-11s f%02d  A:%4d max %5.1f cm %-12s  B:%4d max %5.1f cm %s  tail %4.1f cm" % (
                clip, f, s["A_count"], s["A_max"] * 100.0, s["A_where"], s["B_count"], s["B_max"] * 100.0,
                s["B_where"], s["C_max"] * 100.0))
            if s["A_max"] > 0.03:
                print("   A worst vertex", s.get("A_pos"))
            w = max(s["A_max"], s["B_max"])
            cur = per_clip.get(clip, (0.0, f, 0.0))
            per_clip[clip] = (max(w, cur[0]), f if w > cur[0] else cur[1], max(s["C_max"], cur[2]))
    print("WORST_PER_CLIP cowl (cm, frame, tail cm)", {c: (round(w * 100.0, 1), f, round(t * 100.0, 1)) for c, (w, f, t) in per_clip.items()})
    set_pose(arm, "Idle", 1)


if __name__ == "__main__":
    main()
