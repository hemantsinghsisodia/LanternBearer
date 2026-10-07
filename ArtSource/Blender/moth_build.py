"""Moth model, wing texture and FBX export.

Run:  blender.exe -b -P ArtSource/Blender/moth_build.py

Builds the moth from scratch (the script is the source of truth), saves ArtSource/Blender/Moth.blend,
writes the wing texture to Assets/Game/Art/Creatures/Moth/MothWing.png and exports Moth.fbx beside it.

Blender space: Z up, the body axis runs along Y (head at -Y, which the FBX export (baked axis conversion) turns into Unity +Z, forward), wings spread along +-X.
The LanternKeeper/MothWing shader rotates the wings about the body axis, so the wing pivot is the origin.
Vertex colour R on the wing mesh is the span fraction: 0 at the root, 1 at the tip.
Objects: MothBody (material MothBody), MothWings (material MothWing). 1 unit = 1 m.
"""
import math
import os

import bmesh
import bpy
import numpy as np
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
BLEND_PATH = os.path.join(HERE, "Moth.blend")
OUT_DIR = os.path.join(PROJECT, "Assets", "Game", "Art", "Creatures", "Moth")
FBX_PATH = os.path.join(OUT_DIR, "Moth.fbx")
TEX_PATH = os.path.join(OUT_DIR, "MothWing.png")

# Controller ruling: wingspan about 0.40 m so the moth reads at gameplay distance. Uniform, proportions unchanged.
SIZE_SCALE = 1.59

TEX_W = 512
TEX_H = 256
TILE = 256

# Wing rectangles in metres: root x, tip x, rear y, front y. Forewing UVs use the left tile, hindwing the right.
FORE = dict(x0=0.004, x1=0.126, y0=-0.055, y1=0.055, u0=0.0)
HIND = dict(x0=0.004, x1=0.102, y0=-0.088, y1=0.002, u0=0.5)
HIND_Z = -0.0012
SPAN_SEGS = 6
CHORD_SEGS = 4

# Body profile along Y: (y, radius). Built with the head at +Y, then turned 180 degrees about Z and scaled by SIZE_SCALE in build_scene.
BODY_PROFILE = [(-0.048, 0.0015), (-0.040, 0.0060), (-0.025, 0.0090), (-0.008, 0.0100),
                (0.006, 0.0125), (0.020, 0.0110), (0.031, 0.0080), (0.038, 0.0035), (0.041, 0.0)]
BODY_SIDES = 8


def finish_mesh(bm, name):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def add_lathe(bm, profile, sides, scale=1.0, y_from=None, y_to=None, flatten=0.85):
    """Lathe around Y. Profile entries with radius 0 collapse to a pole vertex."""
    rows = []
    for y, r in profile:
        if y_from is not None and y < y_from:
            continue
        if y_to is not None and y > y_to:
            continue
        r *= scale
        if r <= 1e-6:
            rows.append(bm.verts.new((0.0, y, 0.0)))
            continue
        ring = []
        for i in range(sides):
            a = 2 * math.pi * i / sides
            ring.append(bm.verts.new((math.cos(a) * r, y, math.sin(a) * r * flatten)))
        rows.append(ring)
    for a, b in zip(rows[:-1], rows[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            if isinstance(a, list) and isinstance(b, list):
                bm.faces.new((a[i], a[j], b[j], b[i]))
            elif isinstance(a, list):
                bm.faces.new((a[i], a[j], b))
            elif isinstance(b, list):
                bm.faces.new((a, b[j], b[i]))
    # Cap an open start ring.
    if isinstance(rows[0], list):
        bm.faces.new(rows[0][::-1])


def add_antenna(bm, sign):
    pts = [(0.003 * sign, 0.036, 0.003), (0.010 * sign, 0.052, 0.012), (0.020 * sign, 0.062, 0.022), (0.030 * sign, 0.066, 0.030)]
    widths = [0.0016, 0.0012, 0.0009, 0.0004]
    verts = []
    for p, w in zip(pts, widths):
        verts.append((bm.verts.new((p[0] - w, p[1], p[2])), bm.verts.new((p[0] + w, p[1], p[2]))))
    for (a0, a1), (b0, b1) in zip(verts[:-1], verts[1:]):
        bm.faces.new((a0, a1, b1, b0))


def build_body():
    bm = bmesh.new()
    add_lathe(bm, BODY_PROFILE, BODY_SIDES)
    # Fuzz shell around the thorax and the abdomen's front: a slightly larger lathe.
    add_lathe(bm, BODY_PROFILE, BODY_SIDES, scale=1.28, y_from=-0.012, y_to=0.032)
    add_antenna(bm, 1)
    add_antenna(bm, -1)
    return finish_mesh(bm, "MothBody")


def add_wing(bm, rect, side, z, uv_layer, color_layer):
    cols = SPAN_SEGS + 1
    rows = CHORD_SEGS + 1
    grid = []
    for r in range(rows):
        t = r / CHORD_SEGS
        row = []
        for c in range(cols):
            s = c / SPAN_SEGS
            x = side * (rect["x0"] + s * (rect["x1"] - rect["x0"]))
            y = rect["y0"] + t * (rect["y1"] - rect["y0"])
            row.append((bm.verts.new((x, y, z)), s, t))
        grid.append(row)
    for r in range(rows - 1):
        for c in range(cols - 1):
            quad = [grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]]
            if side < 0:
                quad = quad[::-1]
            face = bm.faces.new([q[0] for q in quad])
            for loop, (v, s, t) in zip(face.loops, quad):
                loop[uv_layer].uv = (rect["u0"] + s * 0.5, t)
                loop[color_layer] = (s, 0.0, 0.0, 1.0)


def build_wings():
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    color_layer = bm.loops.layers.float_color.new("Color")
    for side in (1, -1):
        add_wing(bm, FORE, side, 0.0, uv_layer, color_layer)
        add_wing(bm, HIND, side, HIND_Z, uv_layer, color_layer)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        # Wings are flat and double sided in the shader: force every normal up.
        if f.normal.z < 0:
            f.normal_flip()
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("MothWings")
    bm.to_mesh(mesh)
    bm.free()
    return mesh


# ---- texture -------------------------------------------------------------------------------------

def catmull_closed(points, per_seg=12):
    pts = np.array(points, dtype=np.float64)
    n = len(pts)
    out = []
    for i in range(n):
        p0, p1, p2, p3 = pts[(i - 1) % n], pts[i], pts[(i + 1) % n], pts[(i + 2) % n]
        for k in range(per_seg):
            t = k / per_seg
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    return np.array(out)


def inside_polygon(px, py, poly):
    inside = np.zeros(px.shape, dtype=bool)
    n = len(poly)
    j = n - 1
    for i in range(n):
        xi, yi = poly[i]
        xj, yj = poly[j]
        cond = ((yi > py) != (yj > py)) & (px < (xj - xi) * (py - yi) / (yj - yi + 1e-12) + xi)
        inside ^= cond
        j = i
    return inside


# Outlines in (span fraction, chord fraction); chord 1 is the front edge.
FORE_OUTLINE = [(0.0, 0.70), (0.35, 0.82), (0.75, 0.92), (1.0, 0.80), (0.98, 0.52), (0.88, 0.24),
                (0.62, 0.10), (0.30, 0.20), (0.0, 0.34)]
HIND_OUTLINE = [(0.0, 0.84), (0.45, 0.90), (0.88, 0.80), (1.0, 0.52), (0.90, 0.22), (0.62, 0.08),
                (0.28, 0.22), (0.0, 0.40)]


def box_blur(a, k=1):
    out = a.copy()
    for _ in range(k):
        out = (np.roll(out, 1, 0) + np.roll(out, -1, 0) + np.roll(out, 1, 1) + np.roll(out, -1, 1) + out * 4) / 8.0
    return out


def paint_tile(outline, eye, seed):
    ss = 2
    n = TILE * ss
    ys, xs = np.mgrid[0:n, 0:n]
    s = (xs + 0.5) / n
    t = (ys + 0.5) / n
    poly = catmull_closed(outline)
    mask = inside_polygon(s, t, poly).astype(np.float64)
    mask = mask.reshape(TILE, ss, TILE, ss).mean(axis=(1, 3))
    mask = box_blur(mask, 1)

    ys, xs = np.mgrid[0:TILE, 0:TILE]
    s = (xs + 0.5) / TILE
    t = (ys + 0.5) / TILE
    rng = np.random.default_rng(seed)

    pale = np.array([0.90, 0.84, 0.72])
    dusty = np.array([0.66, 0.60, 0.54])
    dark = np.array([0.40, 0.33, 0.30])
    dist = np.sqrt((s - 0.05) ** 2 + ((t - 0.55) * 0.9) ** 2)
    k = np.clip((dist - 0.45) / 0.65, 0.0, 1.0)[..., None]
    col = pale * (1 - k) + dusty * k
    # Faint wavy bands.
    band = 0.5 + 0.5 * np.sin((s * 9.0 + np.sin(t * 6.0) * 0.8) * math.pi)
    col = col - (band ** 6)[..., None] * 0.07
    # Darker rim, built from the blurred mask.
    rim = np.clip(1.0 - box_blur(mask, 5) * 1.25, 0.0, 1.0)
    col = col * (1 - rim[..., None] * 0.6) + dark * (rim[..., None] * 0.6)
    # Eye spot: a faint ring with a pale core.
    ex, ey, er = eye
    d = np.sqrt((s - ex) ** 2 + (t - ey) ** 2) / er
    ring = np.exp(-((d - 0.75) / 0.18) ** 2)
    core = np.exp(-(d / 0.45) ** 2)
    col = col * (1 - ring[..., None] * 0.42) + dark * (ring[..., None] * 0.42)
    col = col * (1 - core[..., None] * 0.35) + np.array([0.97, 0.93, 0.82]) * (core[..., None] * 0.35)
    # Dust speckle.
    speck = box_blur(rng.normal(0.0, 0.04, (TILE, TILE)), 1)
    col = np.clip(col + speck[..., None], 0.0, 1.0)
    out = np.zeros((TILE, TILE, 4))
    out[..., :3] = col
    out[..., 3] = mask
    return out


def build_texture():
    img = np.zeros((TEX_H, TEX_W, 4))
    img[..., :3] = np.array([0.84, 0.78, 0.68])
    img[:, :TILE] = paint_tile(FORE_OUTLINE, (0.72, 0.40, 0.11), 3)
    img[:, TILE:] = paint_tile(HIND_OUTLINE, (0.66, 0.40, 0.12), 7)
    image = bpy.data.images.new("MothWing", TEX_W, TEX_H, alpha=True)
    image.alpha_mode = 'STRAIGHT'
    image.pixels.foreach_set(img.astype(np.float32).ravel())
    image.filepath_raw = TEX_PATH
    image.file_format = 'PNG'
    os.makedirs(OUT_DIR, exist_ok=True)
    image.save()
    print("Wrote", TEX_PATH)
    return image


# ---- scene ---------------------------------------------------------------------------------------

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


def build_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    build_texture()
    body_mesh = build_body()
    wing_mesh = build_wings()
    # The FBX export with baked axis conversion sends Blender -Y to Unity +Z: turn the head to -Y so it faces forward.
    turn = Matrix.Rotation(math.pi, 4, 'Z') @ Matrix.Scale(SIZE_SCALE, 4)
    body_mesh.transform(turn)
    wing_mesh.transform(turn)
    body = bpy.data.objects.new("MothBody", body_mesh)
    wings = bpy.data.objects.new("MothWings", wing_mesh)
    scene.collection.objects.link(body)
    scene.collection.objects.link(wings)
    body.data.materials.append(make_material("MothBody", (0.16, 0.14, 0.15, 1.0), 0.9))
    wings.data.materials.append(make_material("MothWing", (0.85, 0.80, 0.70, 1.0), 0.8))
    for p in body.data.polygons:
        p.use_smooth = True
    for p in wings.data.polygons:
        p.use_smooth = False
    return body, wings


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
    body, wings = build_scene()
    lo, hi = bounds(body, wings)
    print("Triangles:", tri_count(body, wings), "(body %d, wings %d)" % (tri_count(body), tri_count(wings)))
    print("Bounds min:", [round(v, 4) for v in lo], "max:", [round(v, 4) for v in hi])
    print("Wingspan (m):", round(hi[0] - lo[0], 4), "Length (m):", round(hi[1] - lo[1], 4))
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    export()


main()
