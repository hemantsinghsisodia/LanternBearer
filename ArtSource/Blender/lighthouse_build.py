"""Lighthouse model and FBX export: the great light on the Island 5 summit, a tapered dry-stone tower with an iron
gallery and an eight-sided lantern room under a domed cap.

Run:  blender.exe -b -P ArtSource/Blender/lighthouse_build.py

Builds the lighthouse from scratch (the script is the source of truth), saves ArtSource/Blender/Lighthouse.blend and
exports Assets/Game/Art/Lighthouse/Lighthouse.fbx.

Blender space: Z up, the origin is the centre of the tower base on the ground; the tower axis is X = Y = 0. The door recess faces
-Y, which is Unity +Z after the export (the FBX export bakes the axis conversion, Y up in Unity, exactly like beacon_build.py).
Objects (one renderer each; LighthouseInstaller assigns the materials by name):
  LighthouseStone / LighthouseStone_L1   BeaconCairn slot: the tapered tower with its door recess and two slit windows
  LighthouseIron / LighthouseIron_L1     BeaconIron slot: door, slit frames, gallery deck + railing, mullions, cap, vent ball
  BeaconGlass                            BeaconGlass slot: the 8 panes, UV 0..1 per pane (the name GreatLamp looks up)
  FlameRoot                              an empty at the middle of the lantern room (GreatLamp grows it on ignition)
Heights (m): tower 0..10.5, gallery deck 10.5, railing to 11.6, lantern room 10.6..13.2, cap and vent ball to 14.0. 1 unit = 1 m.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
BLEND_PATH = os.path.join(HERE, "Lighthouse.blend")
OUT_DIR = os.path.join(PROJECT, "Assets", "Game", "Art", "Lighthouse")
FBX_PATH = os.path.join(OUT_DIR, "Lighthouse.fbx")

TOWER_TOP = 10.5
R_BASE = 2.2
R_TOP = 1.6
DECK_R = 2.3
RAIL_H = 1.0
LANTERN_BOT = 10.6
LANTERN_TOP = 13.2
LANTERN_R = 1.3        # circumradius of the octagon (corner posts)
CAP_TOP = 13.65
BALL_R = 0.2
BALL_Z = 13.8
FLAME_Z = 11.9
STONE_TILE = 2.0       # metres per texture repeat
DOOR_HALF = math.radians(15.0)
DOOR_H = 2.7
DOOR_DEPTH = 0.45
DOOR_ANGLE = -math.pi / 2          # -Y
WINDOWS = ((5.0, math.radians(0.0)), (8.0, math.radians(180.0)))   # (height, angle) of the two slits
WIN_HALF = math.radians(3.5)
WIN_H = 1.0
WIN_DEPTH = 0.3


def tower_radius(z):
    return R_BASE + (R_TOP - R_BASE) * (z / TOWER_TOP)


def box(bm, centre, size, rot=None):
    m = Matrix.Translation(centre)
    if rot is not None:
        m = m @ rot
    m = m @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
    bmesh.ops.create_cube(bm, size=1.0, matrix=m)


def bar(bm, p0, p1, half):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    m = Matrix.Translation((p0 + p1) / 2) @ rot @ Matrix.Diagonal((half * 2, half * 2, d.length, 1.0))
    bmesh.ops.create_cube(bm, size=1.0, matrix=m)


def cylinder(bm, z0, z1, r0, r1, sides):
    bmesh.ops.create_cone(bm, cap_ends=True, segments=sides, radius1=r0, radius2=r1, depth=z1 - z0,
                          matrix=Matrix.Translation((0, 0, (z0 + z1) / 2)))


def ang_diff(a, b):
    return (a - b + math.pi) % (2 * math.pi) - math.pi


def inset(angle, z):
    """How far the stone surface is pushed inward at (angle, z): the door recess and the two slit windows."""
    out = 0.0
    d = abs(ang_diff(angle, DOOR_ANGLE))
    if z <= DOOR_H and d <= DOOR_HALF:
        out = DOOR_DEPTH
    for wz, wa in WINDOWS:
        if abs(z - wz) <= WIN_H / 2 and abs(ang_diff(angle, wa)) <= WIN_HALF:
            out = max(out, WIN_DEPTH)
    return out


def build_stone(detail):
    """Tapered tower as a ring grid: courses of dry stone (a ledge every 0.5 m), a flared plinth, noise per vertex.
    Door and window rows are included in the grid so the recess has real side walls (the rows and columns line up with them)."""
    rng = random.Random(21)
    segs = 48 if detail else 16
    # Row heights: regular steps, plus rows exactly at the door top and window edges so the recess is crisp.
    step = 0.3 if detail else 1.3
    rows = set()
    z = 0.0
    while z < TOWER_TOP - 1e-6:
        rows.add(round(z, 4))
        z += step
    rows.add(TOWER_TOP)
    rows.add(DOOR_H)
    rows.add(DOOR_H + 0.02)
    for wz, wa in WINDOWS:
        rows.update((round(wz - WIN_H / 2, 4), round(wz - WIN_H / 2 + 0.02, 4), round(wz + WIN_H / 2, 4), round(wz + WIN_H / 2 + 0.02, 4)))
    rows = sorted(rows)
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    # Column angles: regular, plus the door and window edges.
    angles = set(round(2 * math.pi * i / segs, 5) for i in range(segs))
    angles = sorted(angles)
    # Snap the nearest regular column onto every recess edge instead of adding columns (keeps a clean quad grid).
    targets = [DOOR_ANGLE - DOOR_HALF, DOOR_ANGLE + DOOR_HALF]
    for wz, wa in WINDOWS:
        targets += [wa - WIN_HALF, wa + WIN_HALF]
    cols = [2 * math.pi * i / segs for i in range(segs)]
    for t in targets:
        t = t % (2 * math.pi)
        best = min(range(segs), key=lambda i: abs(ang_diff(cols[i], t)))
        cols[best] = t
    cols.sort()
    grid = []
    for z in rows:
        line = []
        for i in range(segs):
            a = cols[i]
            r = tower_radius(z)
            if detail:
                r += 0.05 * abs(math.sin(z / 0.5 * math.pi))
                if z < 0.8:
                    r += 0.25 * (0.8 - z) / 0.8      # plinth flare
            line.append([a, r, z])
        grid.append(line)
    # Noise per column/row that keeps the seam welded; skip it on recess rows so the edges stay straight.
    verts = []
    for j, z in enumerate(rows):
        line = []
        for i in range(segs):
            a, r, zz = grid[j][i]
            ins = inset(a, zz)
            # A row at the very top of a recess belongs to the recessed band only if it is at or below the edge: the +0.02 row is outside.
            r -= ins
            if detail and ins == 0.0:
                r += rng.uniform(-0.02, 0.02)
            line.append(bm.verts.new((math.cos(a) * r, math.sin(a) * r, zz)))
        verts.append(line)
    for j in range(len(rows) - 1):
        for i in range(segs):
            i2 = (i + 1) % segs
            f = bm.faces.new((verts[j][i], verts[j][i2], verts[j + 1][i2], verts[j + 1][i]))
            circ = 2 * math.pi * tower_radius(rows[j])
            u0 = cols[i] / (2 * math.pi) * circ / STONE_TILE
            u1 = (cols[i2] if i2 != 0 else cols[0] + 2 * math.pi) / (2 * math.pi) * circ / STONE_TILE
            uvs = ((u0, rows[j] / STONE_TILE), (u1, rows[j] / STONE_TILE), (u1, rows[j + 1] / STONE_TILE), (u0, rows[j + 1] / STONE_TILE))
            for loop, uv in zip(f.loops, uvs):
                loop[uv_layer].uv = uv
    # Roof of the tower under the gallery deck.
    bm.faces.new(verts[-1][::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("LighthouseStone" if detail else "LighthouseStone_L1")
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = True
    return mesh


def door_pos(z_centre):
    r = tower_radius(z_centre) - DOOR_DEPTH + 0.06
    return Vector((math.cos(DOOR_ANGLE) * r, math.sin(DOOR_ANGLE) * r, z_centre))


def build_iron(detail):
    bm = bmesh.new()
    sides = 24 if detail else 12
    # Door: a plank slab set in the recess, a lintel above it, cross bands.
    box(bm, door_pos(DOOR_H / 2), (1.5, 0.12, DOOR_H - 0.1))
    box(bm, door_pos(DOOR_H + 0.1) + Vector((0, 0.1, 0)), (1.9, 0.6, 0.2))
    if detail:
        box(bm, door_pos(DOOR_H / 2) + Vector((0.45, -0.1, 0.0)), (0.1, 0.08, 0.1))
        for zz in (0.6, 1.4, 2.2):
            box(bm, door_pos(zz) + Vector((0, -0.075, 0)), (1.5, 0.04, 0.08))
        # Slit windows: a dark pane at the back of each recess.
        for wz, wa in WINDOWS:
            r = tower_radius(wz) - WIN_DEPTH + 0.03
            c = Vector((math.cos(wa) * r, math.sin(wa) * r, wz))
            box(bm, c, (0.05, 0.28, WIN_H - 0.06), Matrix.Rotation(wa, 4, 'Z'))
    # Gallery: deck, a corbel cone under it, then the railing.
    cylinder(bm, TOWER_TOP - 0.05, TOWER_TOP + 0.08, DECK_R, DECK_R, sides)
    cylinder(bm, TOWER_TOP - 0.45, TOWER_TOP - 0.05, R_TOP + 0.1, DECK_R - 0.05, sides)
    n_posts = 24 if detail else 12
    for k in range(n_posts):
        a = 2 * math.pi * k / n_posts
        p = Vector((math.cos(a) * (DECK_R - 0.08), math.sin(a) * (DECK_R - 0.08), TOWER_TOP + 0.08 + RAIL_H / 2))
        box(bm, p, (0.05, 0.05, RAIL_H), Matrix.Rotation(a, 4, 'Z'))
    rail_zs = (TOWER_TOP + 0.08 + RAIL_H - 0.02, TOWER_TOP + 0.08 + RAIL_H * 0.5) if detail else (TOWER_TOP + 0.08 + RAIL_H - 0.02,)
    for zz in rail_zs:
        for k in range(n_posts):
            a0 = 2 * math.pi * k / n_posts
            a1 = 2 * math.pi * (k + 1) / n_posts
            p0 = Vector((math.cos(a0) * (DECK_R - 0.08), math.sin(a0) * (DECK_R - 0.08), zz))
            p1 = Vector((math.cos(a1) * (DECK_R - 0.08), math.sin(a1) * (DECK_R - 0.08), zz))
            bar(bm, p0, p1, 0.022)
    # Lantern room: base ring, eight corner mullions, top ring, then the domed cap and the vent ball.
    cylinder(bm, TOWER_TOP + 0.08, LANTERN_BOT, LANTERN_R + 0.08, LANTERN_R + 0.08, 8)
    corner = [Vector((math.cos(2 * math.pi * k / 8) * LANTERN_R, math.sin(2 * math.pi * k / 8) * LANTERN_R, 0)) for k in range(8)]
    for k in range(8):
        c = corner[k]
        box(bm, Vector((c.x, c.y, (LANTERN_BOT + LANTERN_TOP) / 2)), (0.1, 0.1, LANTERN_TOP - LANTERN_BOT),
            Matrix.Rotation(2 * math.pi * k / 8, 4, 'Z'))
        n = corner[(k + 1) % 8]
        zs = (LANTERN_BOT + 0.05, LANTERN_TOP - 0.05) if detail else (LANTERN_TOP - 0.05,)
        for zz in zs:
            bar(bm, (c.x, c.y, zz), (n.x, n.y, zz), 0.05)
    cylinder(bm, LANTERN_TOP, LANTERN_TOP + 0.08, LANTERN_R + 0.22, LANTERN_R + 0.22, 8)
    cylinder(bm, LANTERN_TOP + 0.08, CAP_TOP, LANTERN_R + 0.2, 0.2, 16 if detail else 8)
    cylinder(bm, CAP_TOP, CAP_TOP + 0.1, 0.12, 0.12, 8)
    bmesh.ops.create_uvsphere(bm, u_segments=12 if detail else 6, v_segments=8 if detail else 4, radius=BALL_R,
                              matrix=Matrix.Translation((0, 0, BALL_Z)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("LighthouseIron" if detail else "LighthouseIron_L1")
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def build_glass():
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    z0, z1 = LANTERN_BOT + 0.03, LANTERN_TOP - 0.03
    k_r = LANTERN_R * 0.97
    for k in range(8):
        a0 = 2 * math.pi * k / 8
        a1 = 2 * math.pi * (k + 1) / 8
        p0 = (math.cos(a0) * k_r, math.sin(a0) * k_r)
        p1 = (math.cos(a1) * k_r, math.sin(a1) * k_r)
        verts = [bm.verts.new((p0[0], p0[1], z0)), bm.verts.new((p1[0], p1[1], z0)),
                 bm.verts.new((p1[0], p1[1], z1)), bm.verts.new((p0[0], p0[1], z1))]
        face = bm.faces.new(verts)
        for loop, uv in zip(face.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
            loop[uv_layer].uv = uv
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("BeaconGlass")
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def make_material(name, color, rough):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = rough
    mat.diffuse_color = color
    return mat


def tri_count(*objs):
    return sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)


def bounds(*objs):
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    lo = [min(p[i] for p in pts) for i in range(3)]
    hi = [max(p[i] for p in pts) for i in range(3)]
    return lo, hi


def add_object(scene, name, mesh, material):
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def build_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    stone_mat = make_material("BeaconCairn", (0.35, 0.34, 0.33, 1.0), 0.9)
    iron_mat = make_material("BeaconIron", (0.08, 0.08, 0.09, 1.0), 0.5)
    glass_mat = make_material("BeaconGlass", (0.1, 0.12, 0.16, 1.0), 0.1)
    objs = {
        "stone": add_object(scene, "LighthouseStone", build_stone(True), stone_mat),
        "iron": add_object(scene, "LighthouseIron", build_iron(True), iron_mat),
        "stone1": add_object(scene, "LighthouseStone_L1", build_stone(False), stone_mat),
        "iron1": add_object(scene, "LighthouseIron_L1", build_iron(False), iron_mat),
        "glass": add_object(scene, "BeaconGlass", build_glass(), glass_mat),
    }
    flame = bpy.data.objects.new("FlameRoot", None)
    flame.empty_display_type = 'PLAIN_AXES'
    flame.location = (0.0, 0.0, FLAME_Z)
    scene.collection.objects.link(flame)
    objs["flame"] = flame
    return objs


def export():
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=False,
        object_types={'MESH', 'EMPTY'},
        apply_scale_options='FBX_SCALE_UNITS',
        global_scale=1.0,
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode='AUTO',
        embed_textures=False,
    )
    print("Exported", FBX_PATH)


def main():
    objs = build_scene()
    os.makedirs(OUT_DIR, exist_ok=True)
    body = [objs["stone"], objs["iron"], objs["glass"]]
    lo, hi = bounds(*body)
    print("Height (m):", round(hi[2] - lo[2], 4), "min z", round(lo[2], 4), "max z", round(hi[2], 4))
    print("Width (m): %.3f x %.3f" % (hi[0] - lo[0], hi[1] - lo[1]))
    print("LOD0 triangles:", tri_count(*body), "(stone %d, iron %d, glass %d)" % (
        tri_count(objs["stone"]), tri_count(objs["iron"]), tri_count(objs["glass"])))
    print("LOD1 triangles:", tri_count(objs["stone1"], objs["iron1"], objs["glass"]))
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    export()


main()
