"""Keeper outfit renders (Eevee), for the approval gate. Does not save the blend.

Run:  blender.exe -b ArtSource/Keeper/Keeper.blend -P ArtSource/Keeper/render_keeper.py -- final
      blender.exe -b ArtSource/Keeper/Keeper.blend -P ArtSource/Keeper/render_keeper.py -- quick OUTDIR
      blender.exe -b ArtSource/Keeper/Keeper.blend -P ArtSource/Keeper/render_keeper.py -- spot OUTDIR Clip:frame ...

final: writes docs/look/keeper/renders/c2/lod{0,1}_{front,side,threequarter,lantern}.png and lod0_face.png
quick: small previews of the same shots into OUTDIR (default: next to this script)

The preview palette follows the art direction: cloth #2E3A4F, leather #2B2119, skin #A5806A.
Front/side/three-quarter use the Idle pose; the lantern shot uses LanternHold with Lantern.blend's Iron, Glass
and FlameAnchor parented to HandSocket and a warm point light at the flame.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
LANTERN_BLEND = os.path.join(PROJECT, "ArtSource", "Lantern", "Lantern.blend")
RENDER_DIR = os.path.join(PROJECT, "docs", "look", "keeper", "renders", "c2")

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODE = argv[0] if argv else "quick"
OUT_DIR = RENDER_DIR if MODE == "final" else (argv[1] if len(argv) > 1 else HERE)
SCALE = 1.0 if MODE == "final" else (0.6 if MODE == "spot" else 0.5)


def lin(h):
    f = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(f(int(h[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (1.0,)


def set_color(mat, rgba, rough=0.85):
    mat.diffuse_color = rgba
    if mat.node_tree:
        b = mat.node_tree.nodes.get("Principled BSDF")
        if b:
            b.inputs["Base Color"].default_value = rgba
            b.inputs["Roughness"].default_value = rough
            if "Specular IOR Level" in b.inputs:
                b.inputs["Specular IOR Level"].default_value = 0.2


def preview_palette():
    for name, col in (("DarkBrown", lin("2B2119")), ("LightBrown", lin("3A2C20")),
                      ("Black", lin("232C3B")), ("Brown", lin("2B2119")), ("Metal", lin("3A4048")),
                      ("Gold", lin("6A5030"))):
        m = bpy.data.materials.get(name)
        if m:
            set_color(m, col)


def set_pose(arm, clip, frame):
    act = bpy.data.actions["CharacterArmature|" + clip]
    arm.animation_data.action = act
    if hasattr(arm.animation_data, "action_slot") and act.slots:
        arm.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()


def add_lantern(arm):
    with bpy.data.libraries.load(LANTERN_BLEND, link=False) as (src, dst):
        dst.objects = list(src.objects)
    socket = bpy.data.objects["HandSocket"]
    objs = []
    for o in dst.objects:
        bpy.context.scene.collection.objects.link(o)
        o.parent = socket
        o.matrix_parent_inverse.identity()
        o.location = (0, 0, 0)
        o.rotation_euler = (math.radians(-90.0), 0, 0)   # hang along the socket's -Y (world down in the pose)
        objs.append(o)
        if o.type == 'MESH':
            for m in o.data.materials:
                if m and m.node_tree and m.node_tree.nodes.get("Principled BSDF") and "Glass" in m.name:
                    b = m.node_tree.nodes["Principled BSDF"]
                    b.inputs["Emission Color"].default_value = (1.0, 0.7, 0.35, 1.0)
                    b.inputs["Emission Strength"].default_value = 3.0
    bpy.context.view_layer.update()
    return [o for o in objs if o.name.startswith("FlameAnchor")][0]


def setup_scene():
    scene = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
    scene.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in engines else 'BLENDER_EEVEE_NEXT'
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = False
    try:
        scene.view_settings.view_transform = 'Standard'
    except TypeError:
        pass
    scene.view_settings.exposure = 0.0
    world = bpy.data.worlds.new("Night")
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes["Background"].inputs["Color"].default_value = (0.006, 0.012, 0.032, 1.0)
    nt.nodes["Background"].inputs["Strength"].default_value = 1.0
    scene.world = world
    # ground
    bpy.ops.mesh.primitive_plane_add(size=12.0, location=(0, 0, 0))
    ground = bpy.context.active_object
    gm = bpy.data.materials.new("Ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.012, 0.018, 0.03, 1.0)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    ground.data.materials.append(gm)
    # cool moon key from the keeper's right/back, a faint fill from the opposite side
    key = bpy.data.objects.new("MoonKey", bpy.data.lights.new("MoonKey", 'SUN'))
    key.data.color = (0.78, 0.86, 1.0)
    key.data.energy = 2.6
    key.data.angle = math.radians(2.0)
    key.rotation_euler = (math.radians(58), 0, math.radians(-55))
    scene.collection.objects.link(key)
    rim = bpy.data.objects.new("MoonRim", bpy.data.lights.new("MoonRim", 'SUN'))
    rim.data.color = (0.45, 0.6, 1.0)
    rim.data.energy = 0.8
    rim.rotation_euler = (math.radians(70), 0, math.radians(150))
    scene.collection.objects.link(rim)
    fill = bpy.data.objects.new("MoonFill", bpy.data.lights.new("MoonFill", 'SUN'))
    fill.data.color = (0.6, 0.7, 0.95)
    fill.data.energy = 0.3
    fill.data.angle = math.radians(8.0)
    scene.collection.objects.link(fill)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def light_for(cam_loc):
    """Moon key from ~55 degrees to the side of the camera, on the keeper's front side, plus a cool rim."""
    az = math.atan2(cam_loc[1], cam_loc[0])
    options = [az + math.radians(55.0), az - math.radians(55.0)]
    az_key = min(options, key=lambda a: math.sin(a))
    el = math.radians(48.0)
    key = bpy.data.objects["MoonKey"]
    rim = bpy.data.objects["MoonRim"]
    fill = bpy.data.objects["MoonFill"]
    for light, a, e in ((key, az_key, el), (rim, az + math.radians(180.0 + 25.0), math.radians(30.0)),
                        (fill, az, math.radians(20.0))):
        pos = Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))) * 6.0 + Vector((0, 0, 1))
        light.location = pos
        light.rotation_euler = (Vector((0, 0, 1.0)) - pos).to_track_quat('-Z', 'Y').to_euler()


def aim(cam, loc, target, lens):
    cam.data.lens = lens
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()


def render(cam, name, loc, target, lens, w, h):
    scene = bpy.context.scene
    scene.render.resolution_x = int(w * SCALE)
    scene.render.resolution_y = int(h * SCALE)
    aim(cam, loc, target, lens)
    light_for(loc)
    os.makedirs(OUT_DIR, exist_ok=True)
    scene.render.filepath = os.path.join(OUT_DIR, name)
    bpy.ops.render.render(write_still=True)
    print("Rendered", scene.render.filepath)


def spot(arm, cam):
    for item in argv[2:]:
        clip, frame = item.split(":")
        set_pose(arm, clip, int(frame))
        tag = "%s_%s" % (clip, frame)
        render(cam, "spot_%s_a.png" % tag, (3.0, -3.2, 1.2), (0.0, 0.0, 0.85), 70, 1024, 1024)
        render(cam, "spot_%s_b.png" % tag, (-3.2, 3.0, 1.2), (0.0, 0.0, 0.85), 70, 1024, 1024)
        render(cam, "spot_%s_c.png" % tag, (0.0, -3.4, 1.0), (0.0, 0.0, 0.85), 70, 1024, 1024)


def set_lod(lod):
    """Show only the LOD0 (lod=0) or LOD1 (lod=1) meshes."""
    for o in bpy.data.objects:
        if o.name == "Cape_base":
            o.hide_render = True
        if o.type == 'MESH' and o.name.endswith(("_LOD0", "_LOD1")):
            o.hide_render = not o.name.endswith("_LOD%d" % lod)
            o.hide_viewport = o.hide_render


def main():
    arm = bpy.data.objects["CharacterArmature"]
    preview_palette()
    cam = setup_scene()
    if MODE == "spot":
        set_lod(0)
        spot(arm, cam)
        return
    W, H = 1024, 1536
    full_t = (0.0, 0.0, 0.92)
    lods = (0, 1)
    flame = None
    warm = None
    for lod in lods:
        set_lod(lod)
        prefix = "lod%d_" % lod
        set_pose(arm, "Idle", 1)
        if warm is not None:
            warm.hide_render = True
            for o in bpy.data.objects:
                if o.parent and o.parent.name == "HandSocket":
                    o.hide_render = True
        render(cam, prefix + "front.png", (0.0, -5.2, 0.95), full_t, 85, W, H)
        render(cam, prefix + "side.png", (5.2, 0.0, 0.95), full_t, 85, W, H)
        render(cam, prefix + "threequarter.png", (3.6, -3.9, 1.0), full_t, 85, W, H)
        if MODE == "quick" and lod == 0:
            render(cam, "back.png", (0.0, 5.2, 0.95), full_t, 85, W, H)
            render(cam, "back34.png", (-3.6, 3.9, 1.0), full_t, 85, W, H)
        # lantern shots: LanternHold with the lantern parented to HandSocket and a warm light at the flame
        set_pose(arm, "LanternHold", 1)
        if flame is None:
            flame = add_lantern(arm)
            warm = bpy.data.objects.new("Flame", bpy.data.lights.new("Flame", 'POINT'))
            warm.data.color = (1.0, 0.69, 0.36)
            warm.data.energy = 14.0
            warm.data.use_shadow = False   # the iron cage would otherwise swallow the light; in game it is not blocked
            warm.data.shadow_soft_size = 0.02
            bpy.context.scene.collection.objects.link(warm)
        warm.hide_render = False
        for o in bpy.data.objects:
            if o.parent and o.parent.name == "HandSocket":
                o.hide_render = False
        warm.location = flame.matrix_world.translation
        render(cam, prefix + "lantern.png", (-3.6, -1.0, 1.15), (-0.15, -0.22, 1.05), 55, 1280, 1280)
        if lod == 0:
            render(cam, "lod0_face.png", (0.10, -1.35, 1.50), (-0.03, -0.20, 1.56), 55, 1280, 1280)
        if MODE == "quick" and lod == 0:
            render(cam, "hand.png", (-0.9, -1.0, 1.15), (-0.25, -0.3, 1.04), 60, 1024, 1024)


main()
