"""Bake seamless OpenGL normals and a foam mask from the ChuckCG water shader values.

Recorded from WaterMaterial.003 / NodeGroup.004 in WaterShader4_3.blend:
  Wave noise: 4D FBM, normalize, scale 30, detail 3, roughness 0.5, lacunarity 2
  Mapping scale before the wave noise: (1, 2, 1)
  Bump strength 0.05, distance 1, filter width 1
  Foam noise: 4D FBM, scale 1.25, detail 6.69, roughness 0.615, lacunarity 1.95
  Foam mapping scale: 3.4
  Foam ramp: 0.213636 -> black, 0.718182 -> (0.532489, 0.532489, 0.532489)
  Foam gamma: 2.79
  Absorption color: (0.210425, 0.687577, 0.804873), density 0.24
"""
import array
import math
import os
import struct
import traceback
import zlib

import bpy

OUT_DIR = r"C:\Hemant\Prj\Learning\Unity3DPrj\Assets\Game\Textures\Water"
RES = 1024
# Torus path length over one UV tile is 2*pi. Divide by that so noise scale
# means "features per tile", matching the pack's generated-coordinate scale.
TORUS_FIT = 1.0 / (2.0 * math.pi)
WAVE_MAP = (1.0, 2.0, 1.0)  # (U radius, V radius, unused)
WAVE_SCALES = (30.0, 51.8)  # pack WaveScale, then the shader's 0.19/0.11 ratio
FOAM_MAP = 3.4
FOAM_NOISE_SCALE = 1.25
FOAM_DETAIL = 6.690001
FOAM_ROUGH = 0.615
FOAM_LACUNARITY = 1.95
RAMP_POS = (0.213636, 0.718182)
RAMP_PEAK = 0.532489
FOAM_GAMMA = 2.79


def enum_ids(rna_type, prop):
    return [item.identifier for item in rna_type.bl_rna.properties[prop].enum_items]


def pick(options, *candidates):
    for name in candidates:
        if name in options:
            return name
    raise RuntimeError("None of %s in %s" % (candidates, options))


MATH_OPS = enum_ids(bpy.types.ShaderNodeMath, "operation")
VEC_OPS = enum_ids(bpy.types.ShaderNodeVectorMath, "operation")
NOISE_DIMS = enum_ids(bpy.types.ShaderNodeTexNoise, "noise_dimensions")
NOISE_TYPES = enum_ids(bpy.types.ShaderNodeTexNoise, "noise_type")
OP_MUL = pick(MATH_OPS, "MULTIPLY")
OP_ADD = pick(MATH_OPS, "ADD")
OP_SUB = pick(MATH_OPS, "SUBTRACT")
OP_DIV = pick(MATH_OPS, "DIVIDE")
OP_COS = pick(MATH_OPS, "COSINE")
OP_SIN = pick(MATH_OPS, "SINE")
OP_FRACT = pick(MATH_OPS, "FRACT", "FRACTION")
OP_POW = pick(MATH_OPS, "POWER")
VEC_ADD = pick(VEC_OPS, "ADD")
VEC_FRACT = pick(VEC_OPS, "FRACT", "FRACTION")
VEC_NORM = pick(VEC_OPS, "NORMALIZE")
DIM_4D = pick(NOISE_DIMS, "4D")
TYPE_FBM = pick(NOISE_TYPES, "FBM")
print("ENUMS", OP_FRACT, VEC_FRACT, DIM_4D, TYPE_FBM)


def math_node(tree, op, a=None, b=None, b_value=None):
    node = tree.nodes.new("ShaderNodeMath")
    node.operation = op
    if a is not None:
        tree.links.new(a, node.inputs[0])
    if b is not None:
        tree.links.new(b, node.inputs[1])
    if b_value is not None:
        node.inputs[1].default_value = b_value
    return node


def build_height_group(name, noise_scale, radius_u, radius_v, detail, roughness, lacunarity):
    existing = bpy.data.node_groups.get(name)
    if existing is not None:
        bpy.data.node_groups.remove(existing)
    group = bpy.data.node_groups.new(name, "ShaderNodeTree")
    group.interface.new_socket(name="UV", in_out="INPUT", socket_type="NodeSocketVector")
    group.interface.new_socket(name="Height", in_out="OUTPUT", socket_type="NodeSocketFloat")
    nodes = group.nodes
    links = group.links
    gi = nodes.new("NodeGroupInput")
    go = nodes.new("NodeGroupOutput")
    sep = nodes.new("ShaderNodeSeparateXYZ")
    links.new(gi.outputs["UV"], sep.inputs["Vector"])
    ang_u = math_node(group, OP_MUL, sep.outputs["X"], b_value=2.0 * math.pi)
    ang_v = math_node(group, OP_MUL, sep.outputs["Y"], b_value=2.0 * math.pi)
    cos_u = math_node(group, OP_COS, ang_u.outputs[0])
    sin_u = math_node(group, OP_SIN, ang_u.outputs[0])
    cos_v = math_node(group, OP_COS, ang_v.outputs[0])
    sin_v = math_node(group, OP_SIN, ang_v.outputs[0])
    # Unit-circle derivative is 2*pi. Fit that back to 1, then apply the pack's mapping scale.
    x = math_node(group, OP_MUL, cos_u.outputs[0], b_value=TORUS_FIT * radius_u)
    y = math_node(group, OP_MUL, sin_u.outputs[0], b_value=TORUS_FIT * radius_u)
    z = math_node(group, OP_MUL, cos_v.outputs[0], b_value=TORUS_FIT * radius_v)
    w = math_node(group, OP_MUL, sin_v.outputs[0], b_value=TORUS_FIT * radius_v)
    comb = nodes.new("ShaderNodeCombineXYZ")
    links.new(x.outputs[0], comb.inputs["X"])
    links.new(y.outputs[0], comb.inputs["Y"])
    links.new(z.outputs[0], comb.inputs["Z"])
    noise = nodes.new("ShaderNodeTexNoise")
    noise.noise_dimensions = DIM_4D
    noise.noise_type = TYPE_FBM
    noise.normalize = True
    noise.inputs["Scale"].default_value = noise_scale
    noise.inputs["Detail"].default_value = detail
    noise.inputs["Roughness"].default_value = roughness
    noise.inputs["Lacunarity"].default_value = lacunarity
    noise.inputs["Distortion"].default_value = 0.0
    links.new(comb.outputs["Vector"], noise.inputs["Vector"])
    links.new(w.outputs[0], noise.inputs["W"])
    links.new(noise.outputs["Factor"], go.inputs["Height"])
    return group


def offset_uv(tree, uv_socket, du, dv):
    add = tree.nodes.new("ShaderNodeVectorMath")
    add.operation = VEC_ADD
    add.inputs[1].default_value = (du, dv, 0.0)
    tree.links.new(uv_socket, add.inputs[0])
    fract = tree.nodes.new("ShaderNodeVectorMath")
    fract.operation = VEC_FRACT
    tree.links.new(add.outputs[0], fract.inputs[0])
    return fract.outputs[0]


def prepare_plane():
    for name in ("Plane", "WaterBakePlane"):
        old = bpy.data.objects.get(name)
        if old is not None:
            mesh = old.data
            bpy.data.objects.remove(old, do_unlink=True)
            if mesh is not None and mesh.users == 0:
                bpy.data.meshes.remove(mesh)
    for obj in list(bpy.data.objects):
        obj.hide_set(True)
        obj.hide_render = True
    before = set(bpy.data.objects)
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0.0, 0.0, 0.0))
    created = [obj for obj in bpy.data.objects if obj not in before]
    plane = created[0]
    plane.name = "WaterBakePlane"
    plane.hide_set(False)
    plane.hide_render = False
    bpy.context.view_layer.objects.active = plane
    return plane


def assign_material(plane):
    mat = bpy.data.materials.new("WaterBake")
    mat.use_nodes = True
    plane.data.materials.clear()
    plane.data.materials.append(mat)
    return mat


def bake_emit(plane, mat, image):
    tex = None
    for node in mat.node_tree.nodes:
        if node.type == "TEX_IMAGE":
            tex = node
            break
    if tex is None:
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.select = True
    mat.node_tree.nodes.active = tex
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 1
    scene.cycles.use_denoising = False
    scene.cycles.device = "CPU"
    scene.render.bake.use_selected_to_active = False
    scene.render.bake.margin = 0
    scene.render.bake.use_clear = True
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    plane.select_set(True)
    bpy.context.view_layer.objects.active = plane
    bpy.ops.object.bake(type="EMIT")


def write_png(path, image):
    # Blender's Image.save() reloads a generated image when filepath is assigned and
    # writes a blank file. Dump the float buffer straight to an 8-bit PNG instead.
    width, height = image.size
    buf = array.array("f", [0.0]) * (width * height * 4)
    image.pixels.foreach_get(buf)

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    raw = bytearray()
    for y in range(height - 1, -1, -1):
        raw.append(0)
        row = y * width * 4
        for x in range(width):
            i = row + x * 4
            raw.append(max(0, min(255, int(round(buf[i] * 255.0)))))
            raw.append(max(0, min(255, int(round(buf[i + 1] * 255.0)))))
            raw.append(max(0, min(255, int(round(buf[i + 2] * 255.0)))))
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(bytes(raw), 6)) + chunk(b"IEND", b"")
    with open(path, "wb") as handle:
        handle.write(png)
    mid = (height // 2 * width + width // 2) * 4
    print("SAVED", path, width, height, len(png), "mid", [round(buf[mid + c], 3) for c in range(3)])


def save_image(image, path):
    # Do not touch colorspace here. Assigning it after a bake regenerates the
    # generated image and wipes the Cycles result.
    write_png(path, image)


def image_stats(image):
    w, h = image.size
    buf = [0.0] * (w * h * 4)
    image.pixels.foreach_get(buf)
    # Sample every 4th pixel for speed, plus a full edge seam test.
    step = 4
    count = 0
    sum_r = 0.0
    sum_g = 0.0
    sum_sq_r = 0.0
    min_r = 1.0
    max_r = 0.0
    min_g = 1.0
    max_g = 0.0
    for y in range(0, h, step):
        row = y * w * 4
        for x in range(0, w, step):
            i = row + x * 4
            r = buf[i]
            g = buf[i + 1]
            sum_r += r
            sum_g += g
            sum_sq_r += r * r
            min_r = min(min_r, r)
            max_r = max(max_r, r)
            min_g = min(min_g, g)
            max_g = max(max_g, g)
            count += 1
    mean_r = sum_r / count
    std_r = math.sqrt(max(sum_sq_r / count - mean_r * mean_r, 0.0))

    def pix(x, y, c):
        return buf[(y * w + x) * 4 + c]

    seam = 0.0
    interior = 0.0
    samples = 0
    for y in range(h):
        for c in range(3):
            jump = abs(pix(0, y, c) - pix(w - 1, y, c))
            inside = abs(pix(1, y, c) - pix(0, y, c))
            seam = max(seam, jump)
            interior = max(interior, inside)
        samples += 1
    for x in range(w):
        for c in range(3):
            jump = abs(pix(x, 0, c) - pix(x, h - 1, c))
            inside = abs(pix(x, 1, c) - pix(x, 0, c))
            seam = max(seam, jump)
            interior = max(interior, inside)
    return {
        "mean_r": mean_r,
        "std_r": std_r,
        "min_r": min_r,
        "max_r": max_r,
        "min_g": min_g,
        "max_g": max_g,
        "edge_jump": seam,
        "neighbor_jump": interior,
    }


def new_image(name):
    old = bpy.data.images.get(name)
    if old is not None:
        bpy.data.images.remove(old)
    image = bpy.data.images.new(name, RES, RES, alpha=False, float_buffer=True)
    image.colorspace_settings.name = "Non-Color"
    return image


def connect_normal_shader(mat, height_group, strength):
    tree = mat.node_tree
    nodes = tree.nodes
    links = tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emit = nodes.new("ShaderNodeEmission")
    links.new(emit.outputs["Emission"], out.inputs["Surface"])
    uv = nodes.new("ShaderNodeTexCoord")
    step = 1.0 / float(RES)
    sockets = {
        "C": uv.outputs["UV"],
        "L": offset_uv(tree, uv.outputs["UV"], -step, 0.0),
        "R": offset_uv(tree, uv.outputs["UV"], step, 0.0),
        "D": offset_uv(tree, uv.outputs["UV"], 0.0, -step),
        "U": offset_uv(tree, uv.outputs["UV"], 0.0, step),
    }
    heights = {}
    for key, socket in sockets.items():
        inst = nodes.new("ShaderNodeGroup")
        inst.node_tree = height_group
        links.new(socket, inst.inputs["UV"])
        heights[key] = inst.outputs["Height"]
    dx = math_node(tree, OP_SUB, heights["L"], heights["R"])
    dy = math_node(tree, OP_SUB, heights["D"], heights["U"])
    sx = math_node(tree, OP_MUL, dx.outputs[0], b_value=strength)
    sy = math_node(tree, OP_MUL, dy.outputs[0], b_value=strength)
    comb = nodes.new("ShaderNodeCombineXYZ")
    links.new(sx.outputs[0], comb.inputs["X"])
    links.new(sy.outputs[0], comb.inputs["Y"])
    comb.inputs["Z"].default_value = 1.0
    norm = nodes.new("ShaderNodeVectorMath")
    norm.operation = VEC_NORM
    links.new(comb.outputs["Vector"], norm.inputs[0])
    scale = nodes.new("ShaderNodeVectorMath")
    scale.operation = pick(VEC_OPS, "SCALE", "MULTIPLY")
    if scale.operation == "SCALE":
        links.new(norm.outputs[0], scale.inputs[0])
        scale.inputs["Scale"].default_value = 0.5
    else:
        links.new(norm.outputs[0], scale.inputs[0])
        scale.inputs[1].default_value = (0.5, 0.5, 0.5)
    bias = nodes.new("ShaderNodeVectorMath")
    bias.operation = VEC_ADD
    links.new(scale.outputs[0], bias.inputs[0])
    bias.inputs[1].default_value = (0.5, 0.5, 0.5)
    links.new(bias.outputs[0], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = 1.0
    nodes.new("ShaderNodeTexImage")


def connect_foam_shader(mat, height_group):
    tree = mat.node_tree
    nodes = tree.nodes
    links = tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emit = nodes.new("ShaderNodeEmission")
    links.new(emit.outputs["Emission"], out.inputs["Surface"])
    uv = nodes.new("ShaderNodeTexCoord")
    inst = nodes.new("ShaderNodeGroup")
    inst.node_tree = height_group
    links.new(uv.outputs["UV"], inst.inputs["UV"])
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "LINEAR"
    ramp.color_ramp.elements[0].position = RAMP_POS[0]
    ramp.color_ramp.elements[0].color = (0.0, 0.0, 0.0, 1.0)
    ramp.color_ramp.elements[1].position = RAMP_POS[1]
    ramp.color_ramp.elements[1].color = (RAMP_PEAK, RAMP_PEAK, RAMP_PEAK, 1.0)
    links.new(inst.outputs["Height"], ramp.inputs["Factor"])
    gamma = nodes.new("ShaderNodeGamma")
    gamma.inputs["Gamma"].default_value = FOAM_GAMMA
    links.new(ramp.outputs["Color"], gamma.inputs["Color"])
    # Lift the ramp peak back to white so the existing shore-foam threshold can see caps.
    peak = RAMP_PEAK ** FOAM_GAMMA
    lifted = nodes.new("ShaderNodeMixRGB")
    # Divide via a vector math on the color.
    div = nodes.new("ShaderNodeVectorMath")
    div.operation = pick(VEC_OPS, "DIVIDE")
    links.new(gamma.outputs["Color"], div.inputs[0])
    div.inputs[1].default_value = (peak, peak, peak)
    # R gets the raw noise (streak breakup). G/B get the whitecap mask (shore foam reads G).
    sep_h = nodes.new("ShaderNodeSeparateXYZ")
    # raw height is a float; pack it into R through a combine
    comb = nodes.new("ShaderNodeCombineXYZ")
    links.new(inst.outputs["Height"], comb.inputs["X"])
    sep_m = nodes.new("ShaderNodeSeparateXYZ")
    links.new(div.outputs[0], sep_m.inputs["Vector"])
    links.new(sep_m.outputs["Y"], comb.inputs["Y"])
    links.new(sep_m.outputs["Y"], comb.inputs["Z"])
    links.new(comb.outputs["Vector"], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = 1.0
    nodes.new("ShaderNodeTexImage")
    # silence unused
    _ = (lifted, sep_h)


def show_tiled(mat, image):
    import mathutils

    tree = mat.node_tree
    nodes = tree.nodes
    links = tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emit = nodes.new("ShaderNodeEmission")
    links.new(emit.outputs["Emission"], out.inputs["Surface"])
    uv = nodes.new("ShaderNodeTexCoord")
    mapping = nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (2.0, 2.0, 1.0)
    links.new(uv.outputs["UV"], mapping.inputs["Vector"])
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"
    tex.extension = "REPEAT"
    links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
    links.new(tex.outputs["Color"], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = 1.0
    plane = bpy.data.objects["WaterBakePlane"]
    plane.scale = (2.0, 2.0, 1.0)
    for obj in bpy.data.objects:
        hide = obj is not plane
        obj.hide_set(hide)
        obj.hide_render = hide
    bpy.context.view_layer.update()
    areas = 0
    for window in bpy.context.window_manager.windows:
        screen = window.screen
        for area in screen.areas:
            if area.type != "VIEW_3D":
                continue
            space = area.spaces.active
            space.shading.type = "MATERIAL"
            space.shading.use_scene_lights = False
            space.shading.use_scene_world = False
            space.overlay.show_overlays = False
            space.lens = 50
            region = next(reg for reg in area.regions if reg.type == "WINDOW")
            with bpy.context.temp_override(window=window, area=area, region=region):
                bpy.ops.view3d.view_axis(type="TOP")
                plane.select_set(True)
                bpy.context.view_layer.objects.active = plane
                bpy.ops.view3d.view_selected()
            r3d = space.region_3d
            areas += 1
            print(
                "VIEW",
                areas,
                tuple(round(v, 3) for v in r3d.view_rotation),
                tuple(round(v, 3) for v in r3d.view_location),
                round(r3d.view_distance, 3),
                r3d.view_perspective,
            )
    print("VIEW_AREAS", areas)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    plane = prepare_plane()
    mat = assign_material(plane)
    # Pack bump is strength 0.05, distance 1. On a 1024 tile that slope is almost flat,
    # so the tangent maps are gained until the broad ripples read in the lake shader.
    # The finer scale uses a lower gain so its steeper per-texel slopes stay water-like.
    strengths = (7.0, 4.05)
    print("STRENGTH", strengths)

    paths = []
    for index, scale in enumerate(WAVE_SCALES):
        name = "WaterRippleNormalA" if index == 0 else "WaterRippleNormalB"
        group = build_height_group(name + "Height", scale, WAVE_MAP[0], WAVE_MAP[1], 3.0, 0.5, 2.0)
        connect_normal_shader(mat, group, strengths[index])
        image = new_image(name)
        bake_emit(plane, mat, image)
        path = os.path.join(OUT_DIR, name + ".png")
        stats = image_stats(image)
        print(name, stats)
        save_image(image, path)
        paths.append(path)

    foam_group = build_height_group(
        "FoamHeight", FOAM_NOISE_SCALE, FOAM_MAP, FOAM_MAP, FOAM_DETAIL, FOAM_ROUGH, FOAM_LACUNARITY
    )
    connect_foam_shader(mat, foam_group)
    foam = new_image("WaterFoam")
    bake_emit(plane, mat, foam)
    foam_path = os.path.join(OUT_DIR, "WaterFoam.png")
    print("WaterFoam", image_stats(foam))
    save_image(foam, foam_path)
    paths.append(foam_path)

    # Leave the viewport on a 2x2 repeat of the first normal so the seam can be seen.
    show_tiled(mat, bpy.data.images["WaterRippleNormalA"])
    print("DONE", paths)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        raise
