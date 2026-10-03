"""Iron storm lantern build, export and thumbnails.

Run:  blender.exe -b -P ArtSource/Lantern/build_lantern.py

Builds the lantern from primitives (always from scratch, so the script is the source of truth),
saves ArtSource/Lantern/Lantern.blend, exports Assets/Game/Art/Lantern/Lantern.fbx and renders
docs/look/keeper/renders/lantern_front.png and lantern_threequarter.png.

Blender space: Z up, origin at the inside top of the ring handle, body hangs along -Z.
FBX export (Y up, -Z forward) turns that into Unity -Y, so the lantern hangs down from the pivot.
Objects: Iron (frame, cap, ring), Glass (four panes), FlameAnchor (empty at the wick).
Materials: LanternIron, LanternGlass (Unity slot names).
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
BLEND_PATH = os.path.join(HERE, "Lantern.blend")
FBX_PATH = os.path.join(PROJECT, "Assets", "Game", "Art", "Lantern", "Lantern.fbx")
RENDER_DIR = os.path.join(PROJECT, "docs", "look", "keeper", "renders")

# Dimensions in metres. Z values are negative: the body hangs below the ring's inside top (z = 0).
RING_R = 0.030        # ring centre line radius
RING_T = 0.004        # ring tube radius
RING_ZC = -(RING_R - RING_T)  # inside top of the ring sits at z = 0
CAP_APEX = -0.046
CAP_BASE = -0.085
CAP_HALF = 0.046
FRAME_TOP = CAP_BASE
FRAME_BOT = -0.235
FRAME_HALF = 0.034
POST = 0.0065         # corner post half-thickness
BASE_BOT = -0.258
FOOT_BOT = -0.276
FOOT_HALF = 0.022
FLAME_Z = RING_ZC - RING_R - RING_T - 0.11  # 0.11 below the ring's bottom
GLASS_HALF = FRAME_HALF - 0.002


def box(bm, cx, cy, cz, sx, sy, sz):
    m = Matrix.Translation((cx, cy, cz)) @ Matrix.Diagonal((sx, sy, sz, 1.0))
    bmesh.ops.create_cube(bm, size=1.0, matrix=m)


def frustum(bm, z0, z1, half0, half1):
    """Closed four sided frustum whose faces line up with the box sides."""
    rows = []
    for z, h in ((z0, half0), (z1, half1)):
        rows.append([bm.verts.new((sx * h, sy * h, z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    lo, hi = rows
    bm.faces.new(lo[::-1])
    bm.faces.new(hi)
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((lo[i], lo[j], hi[j], hi[i]))


def add_ring(bm, segments=12, tube=4):
    loops = []
    for i in range(segments):
        a = 2 * math.pi * i / segments
        center = Vector((math.cos(a) * RING_R, 0.0, RING_ZC + math.sin(a) * RING_R))
        radial = Vector((math.cos(a), 0.0, math.sin(a)))
        side = Vector((0.0, 1.0, 0.0))
        loop = []
        for k in range(tube):
            b = 2 * math.pi * k / tube + math.pi / 4
            loop.append(bm.verts.new(center + radial * (math.cos(b) * RING_T) + side * (math.sin(b) * RING_T)))
        loops.append(loop)
    for i in range(segments):
        j = (i + 1) % segments
        for k in range(tube):
            n = (k + 1) % tube
            bm.faces.new((loops[i][k], loops[i][n], loops[j][n], loops[j][k]))


def finish(bm, name):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def build_iron():
    bm = bmesh.new()
    zc = (FRAME_TOP + FRAME_BOT) * 0.5
    h = FRAME_TOP - FRAME_BOT
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(bm, sx * (FRAME_HALF - POST), sy * (FRAME_HALF - POST), zc, POST * 2, POST * 2, h)
    # Top rail, bottom rail and a mid band around the glass.
    for z, t in ((FRAME_TOP - 0.004, 0.008), (FRAME_BOT + 0.004, 0.008), (zc, 0.005)):
        box(bm, 0, 0, z, FRAME_HALF * 2, FRAME_HALF * 2, t)
    # Peaked roof cap with a small chimney block on top.
    frustum(bm, CAP_BASE, CAP_APEX + 0.012, CAP_HALF, 0.012)
    box(bm, 0, 0, CAP_APEX + 0.016, 0.030, 0.030, 0.008)
    # Base tank block and a tapered foot.
    box(bm, 0, 0, (FRAME_BOT + BASE_BOT) * 0.5, FRAME_HALF * 2 + 0.008, FRAME_HALF * 2 + 0.008, FRAME_BOT - BASE_BOT)
    frustum(bm, BASE_BOT, FOOT_BOT, FRAME_HALF + 0.002, FOOT_HALF)
    add_ring(bm)
    return finish(bm, "Iron")


def build_glass():
    bm = bmesh.new()
    t = 0.0015
    z0 = FRAME_BOT + 0.008
    z1 = FRAME_TOP - 0.008
    zc = (z0 + z1) * 0.5
    h = z1 - z0
    w = (GLASS_HALF - POST) * 2
    box(bm, 0, GLASS_HALF - t, zc, w, t * 2, h)
    box(bm, 0, -(GLASS_HALF - t), zc, w, t * 2, h)
    box(bm, GLASS_HALF - t, 0, zc, t * 2, w, h)
    box(bm, -(GLASS_HALF - t), 0, zc, t * 2, w, h)
    return finish(bm, "Glass")


def make_material(name, color, metallic, rough):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    mat.diffuse_color = color
    return mat


def build_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    iron = bpy.data.objects.new("Iron", build_iron())
    glass = bpy.data.objects.new("Glass", build_glass())
    scene.collection.objects.link(iron)
    scene.collection.objects.link(glass)
    iron.data.materials.append(make_material("LanternIron", (0.07, 0.07, 0.09, 1.0), 0.6, 0.55))
    glass.data.materials.append(make_material("LanternGlass", (1.0, 0.82, 0.5, 1.0), 0.0, 0.1))
    for o in (iron, glass):
        for p in o.data.polygons:
            p.use_smooth = False
    anchor = bpy.data.objects.new("FlameAnchor", None)
    anchor.empty_display_type = 'SPHERE'
    anchor.empty_display_size = 0.01
    anchor.location = (0.0, 0.0, FLAME_Z)
    scene.collection.objects.link(anchor)
    return iron, glass, anchor


def tri_count(*objs):
    return sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)


def export():
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=False,
        object_types={'MESH', 'EMPTY'},
        apply_scale_options='FBX_SCALE_UNITS',
        global_scale=1.0,
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=False,
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode='AUTO',
        embed_textures=False,
    )
    print("Exported", FBX_PATH)


def render(name, cam_loc, target, lens=70):
    scene = bpy.context.scene
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.filepath = os.path.join(RENDER_DIR, name)
    scene.render.image_settings.file_format = 'PNG'
    cam = scene.objects.get("Cam")
    if cam is None:
        cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
        scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.lens = lens
    cam.location = cam_loc
    cam.rotation_euler = (Vector(target) - Vector(cam_loc)).to_track_quat('-Z', 'Y').to_euler()
    bpy.ops.render.render(write_still=True)


def setup_render(glass):
    scene = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
    scene.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in engines else 'BLENDER_EEVEE_NEXT'
    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.03, 0.05, 0.12, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
    scene.world = world
    warm = bpy.data.objects.new("WarmInside", bpy.data.lights.new("WarmInside", 'POINT'))
    warm.data.color = (1.0, 0.69, 0.36)
    warm.data.energy = 0.5
    warm.data.shadow_soft_size = 0.01
    warm.location = (0.0, 0.0, FLAME_Z)
    scene.collection.objects.link(warm)
    key = bpy.data.objects.new("Key", bpy.data.lights.new("Key", 'SUN'))
    key.data.energy = 2.0
    key.data.color = (0.7, 0.8, 1.0)
    key.rotation_euler = (math.radians(50), 0, math.radians(30))
    scene.collection.objects.link(key)
    # Warm glow on the panes so the glass reads as lit. Render-only: the blend and FBX are already written.
    bsdf = glass.data.materials[0].node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Emission Color"].default_value = (1.0, 0.7, 0.35, 1.0)
    bsdf.inputs["Emission Strength"].default_value = 1.5


def main():
    iron, glass, anchor = build_scene()
    print("Triangles:", tri_count(iron, glass))
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    export()
    os.makedirs(RENDER_DIR, exist_ok=True)
    setup_render(glass)
    target = (0.0, 0.0, -0.14)
    render("lantern_front.png", (0.0, -0.9, -0.14), target)
    render("lantern_threequarter.png", (0.55, -0.7, -0.07), target)


main()
