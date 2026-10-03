"""Hooded-wanderer outfit for the keeper: hood, torn cloak, satchel with strap.

Run:  blender.exe -b ArtSource/Keeper/Keeper.blend -P ArtSource/Keeper/build_outfit.py

Works inside Keeper.blend (true scale, Z up, the keeper faces -Y, left = +X). Idempotent: the first run keeps
pristine copies of the four Medieval_* body meshes as "<name>.orig" (fake user, not exported), every run
restores from them, culls the faces hidden under the outfit, deletes and rebuilds Hood, Cloak and Satchel,
then saves Keeper.blend. It does not export the FBX (export_keeper.py does that).

New meshes (all skinned to the existing bones with an Armature modifier):
  Hood     slot KeeperCloth. Open front, double shell; the inner shell is the dark hollow.
  Cloak    slot KeeperCloth. Open-front A-line cloak with a ragged torn hem, double shell.
  Satchel  slot KeeperLeather. Pouch on the left hip plus the strap from the right shoulder.

Vertex colours ("Color", point domain, linear floats; export with colors_type LINEAR):
  R = sway weight: cloak hem 1 -> top ring 0, hood and satchel 0
  G = AO: 1 normally, 0 on the hood's inner (hollow) shell
  B = worn edge: 1 on the cloak hem and the hood rim, 0 elsewhere
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(HERE, "Keeper.blend")

CLOTH_HEX = "2E3A4F"
LEATHER_HEX = "2B2119"
SKIN_HEX = "A5806A"
BODY_MESHES = ["Medieval_Body", "Medieval_Head", "Medieval_Legs", "Medieval_Feet"]
OUTFIT_OBJECTS = ["Hood", "Cloak", "Satchel"]


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_to_linear(h):
    return tuple(srgb_to_linear(int(h[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (1.0,)


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


# ---------------------------------------------------------------------------------------------- materials

def make_material(name, hexcol, worn_hex):
    mat = bpy.data.materials.get(name)
    if mat is not None:
        bpy.data.materials.remove(mat)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    base = hex_to_linear(hexcol)
    mat.diffuse_color = base
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = base
    bsdf.inputs["Roughness"].default_value = 0.9
    attr = nt.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Color"
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(attr.outputs["Color"], sep.inputs["Color"])
    # Preview of what KeeperLit does: B pulls towards a frayed, lighter, greyer edge, then G darkens (AO tint).
    worn = nt.nodes.new("ShaderNodeMix")
    worn.data_type = 'RGBA'
    worn.inputs[6].default_value = base
    worn.inputs[7].default_value = hex_to_linear(worn_hex)
    nt.links.new(sep.outputs["Blue"], worn.inputs[0])
    ao = nt.nodes.new("ShaderNodeMix")
    ao.data_type = 'RGBA'
    ao.inputs[6].default_value = (0.0, 0.0, 0.0, 1.0)
    nt.links.new(worn.outputs["Result"], ao.inputs[7])
    # Mix factor 1 picks B: G = 1 keeps the colour, G = 0 goes to black (the hood hollow).
    nt.links.new(sep.outputs["Green"], ao.inputs[0])
    nt.links.new(ao.outputs["Result"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


# ---------------------------------------------------------------------------------------------- body culling

def restore_and_cull_body():
    stats = {}
    for name in BODY_MESHES:
        obj = bpy.data.objects[name]
        orig = bpy.data.meshes.get(name + ".orig")
        if orig is None:
            orig = obj.data.copy()
            orig.name = name + ".orig"
            orig.use_fake_user = True
        old = obj.data
        fresh = orig.copy()
        fresh.name = name + ".work"
        obj.data = fresh
        if old.users == 0 and old is not orig:
            bpy.data.meshes.remove(old)
        before = sum(len(p.vertices) - 2 for p in fresh.polygons)
        cull(obj)
        add_body_colours(obj)
        fresh.name = name
        after = sum(len(p.vertices) - 2 for p in fresh.polygons)
        stats[name] = (before, after)
    return stats


def add_body_colours(obj):
    """Body meshes get a Color attribute so the Skin material can read G as AO. The head's face is dark (hood
    hollow); the neck ramps back up to 1."""
    me = obj.data
    attr = me.color_attributes.new("Color", 'FLOAT_COLOR', 'POINT')
    mw = obj.matrix_world
    flat = []
    for v in me.vertices:
        g = 1.0
        if obj.name == "Medieval_Head":
            z = (mw @ v.co).z
            g = 0.14 + 0.86 * smoothstep(1.50, 1.44, z)
        flat += [0.0, g, 0.0, 1.0]
    attr.data.foreach_set("color", flat)
    me.color_attributes.active_color = attr


def skin_ao(mat):
    """Skin: base colour darkened by vertex colour G (same AO as KeeperLit)."""
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    if bsdf is None or nt.nodes.get("SkinAO") is not None:
        return
    base = hex_to_linear(SKIN_HEX)
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "Color"
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(vc.outputs["Color"], sep.inputs["Color"])
    ao = nt.nodes.new("ShaderNodeMix")
    ao.name = "SkinAO"
    ao.data_type = 'RGBA'
    ao.inputs[6].default_value = (0.0, 0.0, 0.0, 1.0)
    ao.inputs[7].default_value = base
    nt.links.new(sep.outputs["Green"], ao.inputs[0])
    for link in list(bsdf.inputs["Base Color"].links):
        nt.links.remove(link)
    nt.links.new(ao.outputs["Result"], bsdf.inputs["Base Color"])


def dominant_group(obj, me, poly, names):
    acc = {}
    for vi in poly.vertices:
        for g in me.vertices[vi].groups:
            n = names[g.group]
            acc[n] = acc.get(n, 0.0) + g.weight
    return max(acc, key=acc.get) if acc else ""


def cull(obj):
    me = obj.data
    names = {g.index: g.name for g in obj.vertex_groups}
    mw = obj.matrix_world
    kill = []
    torso = ("Chest", "Torso", "Abdomen", "Body")
    for p in me.polygons:
        mat = me.materials[p.material_index].name
        c = mw @ p.center
        grp = dominant_group(obj, me, p, names)
        if obj.name == "Medieval_Body":
            if mat == "Metal":
                kill.append(p.index)               # pauldrons: the cloak mantle covers the shoulders
            elif mat == "LightBrown" and grp.startswith("Shoulder"):
                kill.append(p.index)               # the old collar
            elif grp in torso and c.y > -0.06:
                kill.append(p.index)               # back of the torso, under the cloak
        elif obj.name == "Medieval_Head":
            if mat in ("White", "DarkBrown"):
                kill.append(p.index)               # old hair and old hood: replaced by the new Hood
            elif c.y > -0.05:
                kill.append(p.index)               # back of the head, inside the hood
        elif obj.name == "Medieval_Legs":
            if c.y > -0.05 and c.z > 0.55:
                kill.append(p.index)               # back of the thighs, under the cloak
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    faces = [bm.faces[i] for i in kill]
    bmesh.ops.delete(bm, geom=faces, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(me)
    bm.free()


# ---------------------------------------------------------------------------------------------- mesh builder

class Builder:
    def __init__(self):
        self.verts = []
        self.rgb = []
        self.weights = []
        self.faces = []
        self.fmat = []

    def vert(self, pos, rgb, weights):
        self.verts.append(tuple(pos))
        self.rgb.append(tuple(rgb))
        self.weights.append(weights)
        return len(self.verts) - 1

    def face(self, idx, mat, expected=None):
        """Add a polygon; when `expected` is given, flip it so its normal faces that direction."""
        if expected is not None:
            pts = [Vector(self.verts[i]) for i in idx]
            n = Vector()
            for a in range(len(pts)):
                n += pts[a].cross(pts[(a + 1) % len(pts)])
            if n.dot(Vector(expected)) < 0.0:
                idx = list(reversed(idx))
        self.faces.append(tuple(idx))
        self.fmat.append(mat)

    def tris(self):
        return sum(len(f) - 2 for f in self.faces)

    def make_object(self, name, materials, arm):
        for old in [o for o in bpy.data.objects if o.name == name]:
            m = old.data
            bpy.data.objects.remove(old)
            if m.users == 0:
                bpy.data.meshes.remove(m)
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.verts, [], self.faces)
        for m in materials:
            me.materials.append(m)
        me.polygons.foreach_set("material_index", [materials.index(m) for m in self.fmat])
        me.polygons.foreach_set("use_smooth", [False] * len(self.faces))
        me.update()
        attr = me.color_attributes.new("Color", 'FLOAT_COLOR', 'POINT')
        flat = []
        for r, g, b in self.rgb:
            flat += [r, g, b, 1.0]
        attr.data.foreach_set("color", flat)
        me.color_attributes.active_color = attr
        me.color_attributes.render_color_index = me.color_attributes.find("Color")
        obj = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(obj)
        obj.parent = arm
        obj.matrix_parent_inverse = arm.matrix_world.inverted()
        names = set()
        for w in self.weights:
            names.update(w.keys())
        groups = {n: obj.vertex_groups.new(name=n) for n in sorted(names)}
        for vi, w in enumerate(self.weights):
            for n, val in w.items():
                if val > 1e-4:
                    groups[n].add([vi], val, 'REPLACE')
        mod = obj.modifiers.new("Armature", 'ARMATURE')
        mod.object = arm
        return obj


# ---------------------------------------------------------------------------------------------- weights

# (z, {bone token: weight}). Tokens Shoulder, UpperLeg and LowerLeg expand to .L / .R by the side of the vertex.
CLOAK_WEIGHT_KEYS = [
    (1.50, {"Neck": 0.5, "Chest": 0.5}),
    (1.44, {"Chest": 1.0}),
    (1.34, {"Chest": 0.75, "Shoulder": 0.25}),
    (1.22, {"Torso": 1.0}),
    (1.10, {"Abdomen": 1.0}),
    (0.98, {"Abdomen": 0.4, "Hips": 0.6}),
    (0.90, {"Hips": 0.6, "UpperLeg": 0.4}),
    (0.74, {"Hips": 0.2, "UpperLeg": 0.8}),
    (0.58, {"UpperLeg": 0.5, "LowerLeg": 0.5}),
    (0.40, {"UpperLeg": 0.3, "LowerLeg": 0.7}),
]
SIDE_TOKENS = ("Shoulder", "UpperLeg", "LowerLeg", "UpperArm", "LowerArm")
MAX_INFLUENCES = 4


def blend_keys(keys, z):
    if z >= keys[0][0]:
        return dict(keys[0][1])
    if z <= keys[-1][0]:
        return dict(keys[-1][1])
    for (z0, w0), (z1, w1) in zip(keys, keys[1:]):
        if z1 <= z <= z0:
            t = (z0 - z) / (z0 - z1)
            out = {}
            for k in set(w0) | set(w1):
                out[k] = w0.get(k, 0.0) * (1.0 - t) + w1.get(k, 0.0) * t
            return out
    return dict(keys[-1][1])


def finalize(weights):
    items = sorted(weights.items(), key=lambda kv: -kv[1])[:MAX_INFLUENCES]
    total = sum(v for _, v in items)
    return {k: v / total for k, v in items if v > 1e-4}


ARM_FOLLOW = 0.0   # how strongly the lateral side panels follow the arm bones


def cloak_weights(x, z, y=0.0):
    tokens = blend_keys(CLOAK_WEIGHT_KEYS, z)
    # The side panels beside the arms follow the upper arm (and the forearm lower down), so a swinging or raised
    # arm carries the cloth with it instead of poking through.
    lateral = smoothstep(0.10, 0.28, abs(x)) * smoothstep(-0.06, 0.08, y)   # rear-lateral panels only
    band = smoothstep(0.90, 1.12, z) * smoothstep(1.46, 1.36, z)
    follow = ARM_FOLLOW * lateral * band
    if follow > 1e-4:
        tokens = {k: v * (1.0 - follow) for k, v in tokens.items()}
        lower = smoothstep(1.22, 1.02, z)
        tokens["UpperArm"] = follow * (1.0 - 0.45 * lower)
        tokens["LowerArm"] = follow * 0.45 * lower
    s = smoothstep(-0.14, 0.14, x)       # 1 on the left (+X)
    out = {}
    for k, v in tokens.items():
        if k in SIDE_TOKENS:
            out[k + ".L"] = out.get(k + ".L", 0.0) + v * s
            out[k + ".R"] = out.get(k + ".R", 0.0) + v * (1.0 - s)
        else:
            out[k] = out.get(k, 0.0) + v
    return finalize(out)


def hood_weights(z):
    head = smoothstep(1.39, 1.53, z)
    rest = 1.0 - head
    return finalize({"Head": head, "Neck": rest * 0.6, "Chest": rest * 0.4})


# ---------------------------------------------------------------------------------------------- shells

def lerp(a, b, t):
    return a + (b - a) * t


def ring_points(ring, ncols):
    """Positions around one profile ring. ring = (z, rx, ry_front, ry_back, cy, phi0_deg)."""
    z, rx, ryf, ryb, cy, phi0 = ring
    p0 = math.radians(phi0)
    pts = []
    for i in range(ncols + 1):
        phi = p0 + (2.0 * math.pi - 2.0 * p0) * i / ncols
        c = math.cos(phi)
        ry = ryf if c > 0.0 else ryb
        pts.append((rx * math.sin(phi), cy - ry * c, z))
    return pts


def build_shell(b, rings, ncols, mat, thickness, colour_fn, weight_fn, hem_drops=None, apex=False):
    """Double shell (outer + inner) over profile `rings`, listed top to bottom.

    colour_fn(k, nrings, inner, edge_col, last_ring) -> (r, g, b) for a vertex.
    With apex=True the first ring is a single point (top of the hood).
    Returns (outer index grid, inner index grid).
    """
    outer, inner = [], []
    nr = len(rings)
    for k, ring in enumerate(rings):
        z, rx, ryf, ryb, cy, phi0 = ring
        if apex and k == 0:
            o = b.vert((0.0, cy, z), colour_fn(k, nr, False, False, False), weight_fn(0.0, z, cy))
            n = b.vert((0.0, cy, z - thickness), colour_fn(k, nr, True, False, False), weight_fn(0.0, z, cy))
            outer.append([o] * (ncols + 1))
            inner.append([n] * (ncols + 1))
            continue
        pts = ring_points(ring, ncols)
        orow, irow = [], []
        for i, (x, y, zz) in enumerate(pts):
            edge = i in (0, ncols)
            last = k == nr - 1
            if last and hem_drops is not None:
                zz = zz - hem_drops[i]
            radial = Vector((x, y - cy, 0.0))
            radial.normalize()
            ip = (x - radial.x * thickness, y - radial.y * thickness, zz)
            orow.append(b.vert((x, y, zz), colour_fn(k, nr, False, edge, last), weight_fn(x, zz, y)))
            irow.append(b.vert(ip, colour_fn(k, nr, True, edge, last), weight_fn(x, zz, y)))
        outer.append(orow)
        inner.append(irow)
    for k in range(nr - 1):
        for i in range(ncols):
            a, bb = outer[k][i], outer[k][i + 1]
            c, d = outer[k + 1][i + 1], outer[k + 1][i]
            if a == bb:
                b.face((a, d, c), mat)
            else:
                b.face((a, d, c, bb), mat)
            a, bb = inner[k][i], inner[k][i + 1]
            c, d = inner[k + 1][i + 1], inner[k + 1][i]
            if a == bb:
                b.face((a, c, d), mat)
            else:
                b.face((a, bb, c, d), mat)
    return outer, inner


def rim_strips(b, outer, inner, mat, ncols, bottom=True, sides=True, top=False):
    nr = len(outer)
    if bottom:
        for i in range(ncols):
            b.face((outer[-1][i], inner[-1][i], inner[-1][i + 1], outer[-1][i + 1]), mat, (0.0, 0.0, -1.0))
    if top:
        for i in range(ncols):
            b.face((outer[0][i], inner[0][i], inner[0][i + 1], outer[0][i + 1]), mat, (0.0, 0.0, 1.0))
    if sides:
        for k in range(nr - 1):
            for col in (0, ncols):
                o0, o1 = outer[k][col], outer[k + 1][col]
                i0, i1 = inner[k][col], inner[k + 1][col]
                p0 = Vector(b.verts[outer[k][1 if col == 0 else ncols - 1]])
                pe = Vector(b.verts[o0])
                away = pe - p0
                away.z = 0.0
                if o0 == outer[k][1 if col == 0 else ncols - 1]:
                    continue
                if o0 == i0:
                    continue
                b.face((o0, i0, i1, o1), mat, tuple(away))


# ---------------------------------------------------------------------------------------------- hood

HOOD_RINGS = [
    (1.84, 0.0, 0.0, 0.0, -0.06, 0.0),     # apex
    (1.79, 0.09, 0.13, 0.12, -0.055, 62.0),
    (1.73, 0.15, 0.27, 0.17, -0.045, 34.0),
    (1.66, 0.18, 0.345, 0.18, -0.04, 23.0),
    (1.58, 0.185, 0.36, 0.18, -0.035, 20.0),
    (1.50, 0.172, 0.31, 0.165, -0.03, 25.0),
    (1.43, 0.19, 0.22, 0.17, -0.03, 48.0),
    (1.37, 0.225, 0.21, 0.205, -0.03, 72.0),
]


def build_hood(arm, cloth):
    b = Builder()
    ncols = 14
    thick = 0.016

    def colour(k, nr, inner, edge, last):
        return (0.0, 0.0 if inner else 1.0, 1.0 if (edge or last) else 0.0)

    outer, inner = build_shell(b, HOOD_RINGS, ncols, cloth, thick, colour,
                               lambda x, z, y=0.0: hood_weights(z), apex=True)
    centre = lambda c: (0.0, -0.035)
    # Outer faces were added interleaved with inner ones; fix winding per kind afterwards.
    rim_strips(b, outer, inner, cloth, ncols, bottom=True, sides=True)
    fix_winding_kinds(b, outer, inner, ncols, centre)
    return b.make_object("Hood", [cloth], arm), b


def fix_winding_kinds(b, outer, inner, ncols, centre):
    """Outer-shell faces must point away from the axis, inner-shell faces towards it."""
    outer_ids = set(v for row in outer for v in row)
    inner_ids = set(v for row in inner for v in row)
    for fi, f in enumerate(b.faces):
        fs = set(f)
        kind = 0
        if fs <= outer_ids:
            kind = 1
        elif fs <= inner_ids:
            kind = -1
        if kind == 0:
            continue
        pts = [Vector(b.verts[i]) for i in f]
        n = Vector()
        for a in range(len(pts)):
            n += pts[a].cross(pts[(a + 1) % len(pts)])
        c = sum(pts, Vector()) / len(pts)
        cc = centre(c)
        radial = Vector((c.x - cc[0], c.y - cc[1], 0.2 * (c.z - 1.55)))
        if n.dot(radial) * kind < 0.0:
            b.faces[fi] = tuple(reversed(f))


# ---------------------------------------------------------------------------------------------- cloak

CLOAK_RINGS = [
    (1.465, 0.125, 0.12, 0.125, -0.04, 12.0),
    (1.415, 0.245, 0.17, 0.21, -0.04, 18.0),
    (1.34, 0.355, 0.20, 0.30, -0.04, 34.0),
    (1.20, 0.365, 0.215, 0.33, -0.04, 42.0),
    (1.02, 0.385, 0.235, 0.355, -0.04, 46.0),
    (0.84, 0.415, 0.25, 0.365, -0.04, 48.0),
    (0.66, 0.445, 0.27, 0.365, -0.04, 50.0),
    (0.52, 0.47, 0.285, 0.365, -0.04, 52.0),
]


def hem_pattern(ncols):
    rng = random.Random(7)
    drops = []
    for i in range(ncols + 1):
        base = 0.03 if i % 2 == 0 else 0.10
        drops.append(base + rng.random() * 0.05)
    # a few deep tears
    for i in (3, 9, 15, 21):
        if i <= ncols:
            drops[i] += 0.09
    for i in (6, 18):
        if i <= ncols:
            drops[i] = max(0.0, drops[i] - 0.05)
    return drops


def build_cloak(arm, cloth):
    b = Builder()
    ncols = 24
    thick = 0.012
    nr = len(CLOAK_RINGS)

    def colour(k, nrr, inner, edge, last):
        return (round((k / (nrr - 1)) ** 1.4, 4), 1.0, 1.0 if last else 0.0)

    outer, inner = build_shell(b, CLOAK_RINGS, ncols, cloth, thick, colour,
                               lambda x, z, y=0.0: cloak_weights(x, z, y), hem_drops=hem_pattern(ncols))
    rim_strips(b, outer, inner, cloth, ncols, bottom=True, sides=True)
    fix_winding_kinds(b, outer, inner, ncols, lambda c: (0.0, -0.04))
    return b.make_object("Cloak", [cloth], arm), b


# ---------------------------------------------------------------------------------------------- satchel and strap

def box(b, centre, axes, half, mat, rgb, weights):
    """Oriented box. axes = (ax, ay, az) unit vectors for local x, y, z; half = half sizes."""
    cx = Vector(centre)
    ax, ay, az = (Vector(a) for a in axes)
    corners = {}
    for sx in (0, 1):
        for sy in (0, 1):
            for sz in (0, 1):
                p = cx + ax * ((sx * 2 - 1) * half[0]) + ay * ((sy * 2 - 1) * half[1]) + az * ((sz * 2 - 1) * half[2])
                corners[(sx, sy, sz)] = b.vert(p, rgb, weights)
    quads = [
        ((0, 0, 0), (0, 1, 0), (0, 1, 1), (0, 0, 1)), ((1, 0, 0), (1, 0, 1), (1, 1, 1), (1, 1, 0)),
        ((0, 0, 0), (0, 0, 1), (1, 0, 1), (1, 0, 0)), ((0, 1, 0), (1, 1, 0), (1, 1, 1), (0, 1, 1)),
        ((0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)), ((0, 0, 1), (0, 1, 1), (1, 1, 1), (1, 0, 1)),
    ]
    for q in quads:
        idx = [corners[c] for c in q]
        mid = (Vector(b.verts[idx[0]]) + Vector(b.verts[idx[2]])) / 2.0 - cx
        b.face(idx, mat, tuple(mid))


def strap(b, path, width, thick, mat, rgb, weights_fn):
    """Flat band along path = [(pos, normal)], with a rectangular section."""
    pts = [Vector(p) for p, _ in path]
    rings = []
    for i, (p, n) in enumerate(path):
        p = Vector(p)
        n = Vector(n).normalized()
        t = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
        t.normalize()
        wd = t.cross(n).normalized()
        wts = weights_fn(p)
        row = [b.vert(p + wd * (sw * width * 0.5) + n * (sn * thick * 0.5), rgb, wts)
               for sw, sn in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        rings.append((row, p))
    for (r0, p0), (r1, p1) in zip(rings, rings[1:]):
        for a in range(4):
            c = (a + 1) % 4
            mid = (Vector(b.verts[r0[a]]) + Vector(b.verts[r0[c]])) / 2.0 - p0
            b.face((r0[a], r0[c], r1[c], r1[a]), mat, tuple(mid))
    r0, p0 = rings[0]
    r1, p1 = rings[-1]
    b.face(tuple(r0), mat, tuple(p0 - p1))
    b.face(tuple(r1), mat, tuple(p1 - p0))


SATCHEL_CENTRE = Vector((0.40, -0.22, 0.80))
STRAP_PATH = [
    ((-0.245, -0.035, 1.385), (-0.6, 0.0, 0.8)),
    ((-0.225, -0.135, 1.365), (-0.55, -0.35, 0.75)),
    ((-0.16, -0.215, 1.30), (-0.35, -0.85, 0.3)),
    ((-0.06, -0.185, 1.19), (0.0, -1.0, 0.15)),
    ((0.07, -0.195, 1.07), (0.0, -1.0, 0.15)),
    ((0.21, -0.225, 0.995), (0.35, -0.9, 0.2)),
    ((0.31, -0.245, 0.96), (0.5, -0.85, 0.15)),
    ((0.39, -0.27, 0.925), (0.55, -0.83, 0.1)),
]
# Bag profile, top to bottom: (w, half width, half depth). Widest below the middle so it sags.
BAG_RINGS = [(0.095, 0.092, 0.034), (0.04, 0.114, 0.048), (-0.025, 0.122, 0.054), (-0.075, 0.108, 0.048),
             (-0.103, 0.06, 0.03)]


def bag_depth(w):
    for (w0, _, d0), (w1, _, d1) in zip(BAG_RINGS, BAG_RINGS[1:]):
        if w1 <= w <= w0:
            t = (w0 - w) / (w0 - w1)
            return d0 + (d1 - d0) * t
    return BAG_RINGS[0][2] if w > BAG_RINGS[0][0] else BAG_RINGS[-1][2]


def octagon(hw, hd):
    c = 0.62
    return [(-hw * c, -hd), (hw * c, -hd), (hw, -hd * 0.55), (hw, hd * 0.55),
            (hw * c, hd), (-hw * c, hd), (-hw, hd * 0.55), (-hw, -hd * 0.55)]


def build_satchel(arm, leather):
    b = Builder()
    yaw = math.radians(30.0)          # faces the same way as the cloak surface at its position
    outward = Vector((math.sin(yaw), -math.cos(yaw), 0.0))
    tangent = Vector((math.cos(yaw), math.sin(yaw), 0.0))
    up = Vector((0.0, 0.0, 1.0))
    c0 = SATCHEL_CENTRE
    wts = cloak_weights(c0.x, c0.z)
    rgb = (0.0, 1.0, 0.0)

    def P(u, v, w):
        return c0 + tangent * u + outward * v + up * w

    def radial_dir(idx, squash=1.0):
        cen = sum((Vector(b.verts[q]) for q in idx), Vector()) / len(idx)
        d = cen - c0
        return (d.x, d.y, d.z * squash)

    # bag body: rounded rings and a small bottom fan
    rings = []
    for w, hw, hd in BAG_RINGS:
        rings.append([b.vert(P(u, v, w), rgb, wts) for u, v in octagon(hw, hd)])
    n = 8
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(n):
            j = (i + 1) % n
            idx = (r0[i], r0[j], r1[j], r1[i])
            b.face(idx, leather, radial_dir(idx, 0.2))
    bottom = b.vert(P(0.0, 0.0, -0.112), rgb, wts)
    for i in range(n):
        j = (i + 1) % n
        b.face((rings[-1][i], rings[-1][j], bottom), leather, (0.0, 0.0, -1.0))
    # flap: rounded front, folded over the top. Double shell with a rim.
    cols = 6
    outer, inner = [], []
    for k in range(cols + 1):
        t = -1.0 + 2.0 * k / cols
        u = t * 0.108
        drop = 0.05 + 0.078 * math.sqrt(max(0.0, 1.0 - t * t))
        w_top = 0.097
        w_mid = w_top - drop * 0.5
        w_bot = w_top - drop
        col_o = [P(u, -bag_depth(w_top) * 0.9, w_top + 0.004), P(u, bag_depth(w_top) + 0.012, w_top + 0.008),
                 P(u, bag_depth(w_mid) + 0.012, w_mid), P(u, bag_depth(w_bot) + 0.012, w_bot)]
        col_i = []
        for q in col_o:
            local = q - c0
            col_i.append(c0 + tangent * (local.dot(tangent) * 0.985) + outward * (local.dot(outward) - 0.008)
                         + up * (local.dot(up) - 0.007))
        outer.append([b.vert(q, rgb, wts) for q in col_o])
        inner.append([b.vert(q, rgb, wts) for q in col_i])
    for k in range(cols):
        for r in range(3):
            for grid, sign in ((outer, 1.0), (inner, -1.0)):
                idx = (grid[k][r], grid[k + 1][r], grid[k + 1][r + 1], grid[k][r + 1])
                d = radial_dir(idx)
                b.face(idx, leather, (d[0] * sign, d[1] * sign, d[2] * sign))
    flap_centre = c0 + up * 0.05
    for k in range(cols):
        o0, o1, i0, i1 = outer[k][3], outer[k + 1][3], inner[k][3], inner[k + 1][3]
        mid = (Vector(b.verts[o0]) + Vector(b.verts[o1])) / 2.0
        b.face((o0, o1, i1, i0), leather, tuple(mid - flap_centre))
    for col in (0, cols):
        for r in range(3):
            idx = (outer[col][r], outer[col][r + 1], inner[col][r + 1], inner[col][r])
            away = tangent * (1.0 if col == cols else -1.0)
            b.face(idx, leather, tuple(away))
    # buckle and strap tab on the flap
    wb = 0.097 - 0.128
    box(b, P(0.0, bag_depth(wb) + 0.026, wb + 0.012), (tangent, outward, up), (0.017, 0.008, 0.02), leather, rgb, wts)
    box(b, P(0.0, bag_depth(wb - 0.04) + 0.008, wb - 0.045), (tangent, outward, up), (0.022, 0.006, 0.03),
        leather, rgb, wts)
    # strap: right shoulder across the chest to the satchel top
    strap(b, STRAP_PATH, 0.045, 0.012, leather, rgb, strap_weights)
    return b.make_object("Satchel", [leather], arm), b


def strap_weights(p):
    # Shoulder end follows the chest, the hip end follows the cloak at the satchel.
    t = smoothstep(1.30, 0.95, p.z)
    out = {"Chest": 1.0 - t}
    for k, v in cloak_weights(p.x, max(p.z, 0.9)).items():
        out[k] = out.get(k, 0.0) + v * t
    return finalize(out)


# ---------------------------------------------------------------------------------------------- main

def tri_total():
    total = {}
    for o in bpy.data.objects:
        if o.type == 'MESH':
            total[o.name] = sum(len(p.vertices) - 2 for p in o.data.polygons)
    return total


def main():
    arm = bpy.data.objects["CharacterArmature"]
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()

    cloth = make_material("KeeperCloth", CLOTH_HEX, "5A6678")
    leather = make_material("KeeperLeather", LEATHER_HEX, "4A3A2C")
    skin = bpy.data.materials.get("Skin")
    if skin is not None:
        skin.diffuse_color = hex_to_linear(SKIN_HEX)
        if skin.use_nodes and skin.node_tree.nodes.get("Principled BSDF"):
            skin.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = hex_to_linear(SKIN_HEX)
            skin_ao(skin)

    stats = restore_and_cull_body()
    for name, (before, after) in stats.items():
        print("BODY", name, before, "->", after)

    hood, hb = build_hood(arm, cloth)
    cloak, cb = build_cloak(arm, cloth)
    satchel, sb = build_satchel(arm, leather)
    tris = tri_total()
    print("TRIS", tris)
    print("TRIS total", sum(tris.values()))
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


main()
