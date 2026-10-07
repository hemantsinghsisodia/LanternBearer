"""Beacon model and FBX export: a keeper's lamp-post, a storm lantern on an iron post over a dry-stone cairn.

Run:  blender.exe -b -P ArtSource/Blender/beacon_build.py

Builds the beacon from scratch (the script is the source of truth), saves ArtSource/Blender/Beacon.blend and
exports Assets/Game/Art/Beacons/Beacon.fbx.

Blender space: Z up, the origin is the centre of the cairn base on the ground; the lantern axis is X = Y = 0.
The FBX export bakes the axis conversion (Y up in Unity), exactly like moth_build.py.
Objects (one renderer each; the installer assigns the materials by name):
  BeaconCairn / BeaconIron   LOD0 meshes (cairn blocks, iron post + bracket + lantern frame + cap + ring)
  BeaconCairn_L1 / BeaconIron_L1   LOD1 meshes (fewer, plainer pieces)
  BeaconGlass                four pane quads, UV 0..1 per pane (the glass slot)
  BeaconBeams                four additive light cards, one per pane. They radiate from the lantern axis:
                             the shader derives the outward direction from the object space XZ position, so the
                             cards need no extra data. UV.x = 0 at the pane, 1 at the tip (full length BEAM_LEN),
                             UV.y across the card.
Materials (Unity slot names): BeaconCairn, BeaconIron, BeaconGlass, BeaconBeam. 1 unit = 1 m.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
BLEND_PATH = os.path.join(HERE, "Beacon.blend")
OUT_DIR = os.path.join(PROJECT, "Assets", "Game", "Art", "Beacons")
FBX_PATH = os.path.join(OUT_DIR, "Beacon.fbx")

# Heights (m). A big storm lantern sitting low and heavy on a broad cairn: cairn about 1.5 m tall and 1.8 m wide, a short stout iron
# stand on top, the lantern about 1.2 m with its cap and ring, total about 3.1 m.
CAIRN_TOP = 1.5
POST_BOT = 1.3
POST_TOP = 1.72
PLATE_Z = 1.72
BASE_BOT = 1.745
FRAME_BOT = 1.82
FRAME_TOP = 2.70
CAP_BASE = 2.70
CAP_APEX = 2.95
FRAME_HALF = 0.17
CAP_HALF = 0.23
PANE_HALF = 0.152     # half width of a pane
PANE_PLANE = 0.148    # distance of the pane plane from the lantern axis
POST_T = 0.035        # corner post half thickness
RING_R = 0.065
RING_T = 0.014
BEAM_START = 0.15
BEAM_LEN = 3.0
CAIRN_TILE = 1.6      # metres per texture repeat


def box(bm, centre, size):
    m = Matrix.Translation(centre) @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
    bmesh.ops.create_cube(bm, size=1.0, matrix=m)


def bar(bm, p0, p1, half):
    """Square section bar between two points."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    length = d.length
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    m = Matrix.Translation((p0 + p1) / 2) @ rot @ Matrix.Diagonal((half * 2, half * 2, length, 1.0))
    bmesh.ops.create_cube(bm, size=1.0, matrix=m)


def frustum(bm, z0, z1, half0, half1):
    rows = []
    for z, h in ((z0, half0), (z1, half1)):
        rows.append([bm.verts.new((sx * h, sy * h, z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    lo, hi = rows
    bm.faces.new(lo[::-1])
    bm.faces.new(hi)
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((lo[i], lo[j], hi[j], hi[i]))


def cylinder(bm, z0, z1, r0, r1, sides):
    bmesh.ops.create_cone(bm, cap_ends=True, segments=sides, radius1=r0, radius2=r1, depth=z1 - z0,
                          matrix=Matrix.Translation((0, 0, (z0 + z1) / 2)))


def ring(bm, segments, tube):
    centre_z = CAP_APEX + RING_R - RING_T
    loops = []
    for i in range(segments):
        a = 2 * math.pi * i / segments
        c = Vector((math.cos(a) * RING_R, 0.0, centre_z + math.sin(a) * RING_R))
        radial = Vector((math.cos(a), 0.0, math.sin(a)))
        side = Vector((0.0, 1.0, 0.0))
        loop = []
        for k in range(tube):
            b = 2 * math.pi * k / tube + math.pi / 4
            loop.append(bm.verts.new(c + radial * (math.cos(b) * RING_T) + side * (math.sin(b) * RING_T)))
        loops.append(loop)
    for i in range(segments):
        j = (i + 1) % segments
        for k in range(tube):
            n = (k + 1) % tube
            bm.faces.new((loops[i][k], loops[i][n], loops[j][n], loops[j][k]))


def build_iron(detail):
    """detail 1 = LOD0, 0 = LOD1."""
    bm = bmesh.new()
    # Post, with a collar where it leaves the stones and a plate under the lantern.
    cylinder(bm, POST_BOT, POST_TOP, 0.10, 0.082, 8 if detail else 6)
    if detail:
        cylinder(bm, 1.46, 1.54, 0.13, 0.13, 8)
        cylinder(bm, 1.64, 1.69, 0.115, 0.115, 8)
    box(bm, (0, 0, PLATE_Z), (0.42, 0.42, 0.05))
    # Bracket: four diagonal braces from the post up to the plate corners.
    if detail:
        for sx, sy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
            bar(bm, (0, 0, 1.52), (sx * 0.18, sy * 0.18, PLATE_Z - 0.02), 0.016)
    # Lantern base and foot.
    box(bm, (0, 0, (BASE_BOT + FRAME_BOT) / 2 + 0.005), (0.38, 0.38, FRAME_BOT - BASE_BOT + 0.01))
    # Frame: four corner posts, top and bottom rails.
    for sx, sy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        box(bm, (sx * FRAME_HALF, sy * FRAME_HALF, (FRAME_BOT + FRAME_TOP) / 2), (POST_T * 2, POST_T * 2, FRAME_TOP - FRAME_BOT))
    if detail:
        for z in (FRAME_BOT + 0.02, FRAME_TOP - 0.02):
            for sign in (1, -1):
                box(bm, (sign * FRAME_HALF, 0, z), (0.05, 2 * FRAME_HALF, 0.05))
                box(bm, (0, sign * FRAME_HALF, z), (2 * FRAME_HALF, 0.05, 0.05))
        # A slim mullion in the middle of every pane, like the keeper's lantern.
        for sign in (1, -1):
            box(bm, (sign * (PANE_PLANE + 0.004), 0, (FRAME_BOT + FRAME_TOP) / 2), (0.02, 0.02, FRAME_TOP - FRAME_BOT))
            box(bm, (0, sign * (PANE_PLANE + 0.004), (FRAME_BOT + FRAME_TOP) / 2), (0.02, 0.02, FRAME_TOP - FRAME_BOT))
    # Peaked roof cap with a slight overhang, and a small knob under the ring.
    frustum(bm, CAP_BASE, CAP_BASE + 0.02, CAP_HALF, CAP_HALF)
    frustum(bm, CAP_BASE + 0.02, CAP_APEX, CAP_HALF, 0.045)
    ring(bm, 12 if detail else 6, 4 if detail else 3)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("BeaconIron" if detail else "BeaconIron_L1")
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def cairn_blocks():
    """(centre, half extents, yaw) for every block, bottom layer first. Fixed seed: always the same cairn."""
    rng = random.Random(7)
    blocks = []
    layers = [
        # radius of block centres, count, half width, base z, half height
        (0.55, 10, 0.27, 0.00, 0.19),
        (0.49, 9, 0.25, 0.32, 0.17),
        (0.43, 8, 0.23, 0.62, 0.16),
        (0.36, 7, 0.20, 0.90, 0.15),
        (0.29, 6, 0.18, 1.16, 0.13),
    ]
    for radius, count, half, z0, hz in layers:
        for i in range(count):
            a = 2 * math.pi * (i + rng.uniform(-0.15, 0.15)) / count + radius * 3.0
            r = radius * rng.uniform(0.92, 1.08)
            h = half * rng.uniform(0.85, 1.12)
            zc = z0 + hz * rng.uniform(0.92, 1.1)
            blocks.append((Vector((math.cos(a) * r, math.sin(a) * r, zc)), Vector((h * 1.12, h, hz)), rng.uniform(0, math.pi)))
        # Fill the middle so the stack has no hole.
        blocks.append((Vector((0, 0, z0 + hz)), Vector((half * 1.1, half * 1.1, hz)), rng.uniform(0, math.pi)))
    # Summit stone: the post stands in it.
    blocks.append((Vector((0.0, 0.0, 1.30)), Vector((0.34, 0.32, 0.20)), 0.4))
    # A few small stones at the foot.
    for i in range(8):
        a = 2 * math.pi * i / 8 + rng.uniform(-0.2, 0.2)
        r = rng.uniform(0.78, 0.86)
        h = rng.uniform(0.09, 0.14)
        blocks.append((Vector((math.cos(a) * r, math.sin(a) * r, h * 0.7)), Vector((h * 1.2, h, h * 0.8)), rng.uniform(0, math.pi)))
    return blocks


def build_cairn(detail):
    rng = random.Random(11)
    bm = bmesh.new()
    scratch = bpy.data.meshes.new("scratch")
    for centre, half, yaw in cairn_blocks():
        blk = bmesh.new()
        bmesh.ops.create_cube(blk, size=2.0)
        cuts = (2 if half.x > 0.24 else 1) if detail else 0
        if cuts:
            bmesh.ops.subdivide_edges(blk, edges=list(blk.edges), cuts=cuts, use_grid_fill=True)
        rot = Matrix.Rotation(yaw, 4, 'Z')
        for v in blk.verts:
            p = v.co.copy()
            inf = max(abs(p.x), abs(p.y), abs(p.z))
            if p.length > 1e-6:
                p = p.lerp(p.normalized() * inf, 0.4 if detail else 0.25)
            p = Vector((p.x * half.x, p.y * half.y, p.z * half.z))
            p += Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * 0.05
            v.co = (rot @ p) + centre
        blk.to_mesh(scratch)
        blk.free()
        bm.from_mesh(scratch)
    bpy.data.meshes.remove(scratch)
    uv_layer = bm.loops.layers.uv.new("UVMap")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # Box projected UVs in metres (one repeat per CAIRN_TILE).
    for f in bm.faces:
        n = f.normal
        axis = max(range(3), key=lambda k: abs(n[k]))
        for loop in f.loops:
            c = loop.vert.co
            if axis == 0:
                uv = (c.y, c.z)
            elif axis == 1:
                uv = (c.x, c.z)
            else:
                uv = (c.x, c.y)
            loop[uv_layer].uv = (uv[0] / CAIRN_TILE, uv[1] / CAIRN_TILE)
        f.smooth = True
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("BeaconCairn" if detail else "BeaconCairn_L1")
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = True
    return mesh


def build_glass():
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    z0, z1 = FRAME_BOT + 0.01, FRAME_TOP - 0.01
    for k in range(4):
        rot = Matrix.Rotation(k * math.pi / 2, 4, 'Z')
        corners = [(-PANE_HALF, PANE_PLANE, z0), (PANE_HALF, PANE_PLANE, z0), (PANE_HALF, PANE_PLANE, z1), (-PANE_HALF, PANE_PLANE, z1)]
        # Winding so the normal faces out (+Y before the rotation).
        verts = [bm.verts.new(rot @ Vector(c)) for c in corners]
        face = bm.faces.new((verts[0], verts[3], verts[2], verts[1]))
        uvs = [(0, 0), (1, 0), (1, 1), (0, 1)]
        order = [0, 3, 2, 1]
        for loop, idx in zip(face.loops, order):
            loop[uv_layer].uv = uvs[idx]
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("BeaconGlass")
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def build_beams():
    """Four vertical fans, one per pane, growing taller with distance. Double sided in the shader."""
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    zc = (FRAME_BOT + FRAME_TOP) / 2
    for k in range(4):
        rot = Matrix.Rotation(k * math.pi / 2, 4, 'Z')
        pts = [(0.0, BEAM_START, zc - 0.28), (0.0, BEAM_START, zc + 0.28),
               (0.0, BEAM_START + BEAM_LEN, zc + 0.85), (0.0, BEAM_START + BEAM_LEN, zc - 0.85)]
        uvs = [(0, 0), (0, 1), (1, 1), (1, 0)]
        verts = [bm.verts.new(rot @ Vector(p)) for p in pts]
        face = bm.faces.new(verts)
        for loop, uv in zip(face.loops, uvs):
            loop[uv_layer].uv = uv
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("BeaconBeams")
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
    cairn_mat = make_material("BeaconCairn", (0.35, 0.34, 0.33, 1.0), 0.9)
    iron_mat = make_material("BeaconIron", (0.08, 0.08, 0.09, 1.0), 0.5)
    glass_mat = make_material("BeaconGlass", (0.1, 0.12, 0.16, 1.0), 0.1)
    beam_mat = make_material("BeaconBeam", (1.0, 0.7, 0.35, 1.0), 1.0)
    objs = {
        "cairn": add_object(scene, "BeaconCairn", build_cairn(True), cairn_mat),
        "iron": add_object(scene, "BeaconIron", build_iron(1), iron_mat),
        "cairn1": add_object(scene, "BeaconCairn_L1", build_cairn(False), cairn_mat),
        "iron1": add_object(scene, "BeaconIron_L1", build_iron(0), iron_mat),
        "glass": add_object(scene, "BeaconGlass", build_glass(), glass_mat),
        "beams": add_object(scene, "BeaconBeams", build_beams(), beam_mat),
    }
    return objs


def export():
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=False,
        object_types={'MESH'},
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
    body = [objs["cairn"], objs["iron"], objs["glass"]]
    lo, hi = bounds(*body)
    print("Height (m):", round(hi[2] - lo[2], 4), "min z", round(lo[2], 4), "max z", round(hi[2], 4))
    print("Width (m): %.3f x %.3f" % (hi[0] - lo[0], hi[1] - lo[1]))
    clo, chi = bounds(objs["cairn"])
    print("Cairn height (m):", round(chi[2] - clo[2], 3), "width", round(chi[0] - clo[0], 3), round(chi[1] - clo[1], 3))
    print("LOD0 triangles:", tri_count(*body), "(cairn %d, iron %d, glass %d)" % (
        tri_count(objs["cairn"]), tri_count(objs["iron"]), tri_count(objs["glass"])))
    print("LOD1 triangles:", tri_count(objs["cairn1"], objs["iron1"], objs["glass"]))
    print("Beam triangles:", tri_count(objs["beams"]))
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    export()


main()
