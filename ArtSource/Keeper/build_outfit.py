"""Keeper outfit, Phase C2: deep hood, short torn shoulder cape, upgraded body, satchel. LOD0 meshes.

Run:  blender.exe -b ArtSource/Keeper/Keeper.blend -P ArtSource/Keeper/build_outfit.py
Then: blender.exe -b ArtSource/Keeper/Keeper.blend -P ArtSource/Keeper/build_lods.py   (LOD1 copies)

Works inside Keeper.blend (true scale, Z up, the keeper faces -Y, left = +X). Idempotent: the first run keeps
pristine copies of the four Medieval_* body meshes as "<name>.orig" (fake user, not exported) and every run
rebuilds from them. The Phase C hood/cloak/satchel objects and the four body objects are removed.

Produces (all skinned to CharacterArmature with an Armature modifier):
  Body_LOD0     Body, Head, Legs and Feet welded, quad-joined, one subdivision level on the clothing (creases kept at
                cuffs, belt, collar and boot soles), folds on the trousers and tunic, smoothed hands, smooth shading.
                Slots KeeperLeather, KeeperSkin, Black.
  Hood_LOD0     slot KeeperCloth. Deep, round, open front, double shell; the inner shell is the dark hollow.
  Cape_LOD0     slot KeeperCloth. Short shoulder cape, torn lower edge, double shell.
  Satchel_LOD0  slot KeeperLeather. Bag on the left hip, strap from the right shoulder across the back.

Vertex colours ("Color", point domain, linear floats; export with colors_type LINEAR):
  R = sway weight: cape edge 1 -> collar 0; 0 on everything else
  G = AO: 1 normally, 0 on the hood's inner shell, ~0.14 on the face
  B = worn edge: 1 on the cape's torn edge and the hood rim, 0 elsewhere

The cape and satchel are designed in the Idle pose (frame 1) and mapped back to the rest pose through the inverse of
their own skin weights, so they fit the idle body exactly and the armature modifier reproduces the design at Idle.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(HERE, "Keeper.blend")

CLOTH_HEX = "2E3A4F"
LEATHER_HEX = "2B2119"
SKIN_HEX = "A5806A"
DARK_HEX = "232C3B"
BODY_PARTS = ["Medieval_Body", "Medieval_Head", "Medieval_Legs", "Medieval_Feet"]
LOD0_NAMES = ["Body_LOD0", "Hood_LOD0", "Cape_LOD0", "Satchel_LOD0"]
OLD_OBJECTS = ["Hood", "Cloak", "Satchel"] + BODY_PARTS
# Source material -> slot of the joined body. Metal (pauldrons) and White (hair) are culled before this.
MAT_MAP = {"LightBrown": "KeeperLeather", "DarkBrown": "KeeperLeather", "Brown": "KeeperLeather",
           "Gold": "KeeperLeather", "Skin": "KeeperSkin", "KeeperSkin": "KeeperSkin", "Black": "Black"}
BODY_SLOTS = ["KeeperLeather", "KeeperSkin", "Black"]
HAND_GROUPS = ("Index", "Middle", "Ring", "Pinky", "Thumb", "Wrist")

AXIS_X = -0.013      # Idle-pose centre line of the torso
AXIS_Y = -0.070


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_to_linear(h):
    return tuple(srgb_to_linear(int(h[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (1.0,)


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def lerp(a, b, t):
    return a + (b - a) * t


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
    bsdf.inputs["Specular IOR Level"].default_value = 0.0      # flat painterly preview: no sheen on the black hollow
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
    # Mix factor 1 picks the second colour: G = 1 keeps the colour, G = 0 goes to black (the hood hollow).
    nt.links.new(sep.outputs["Green"], ao.inputs[0])
    nt.links.new(ao.outputs["Result"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def skin_material():
    """KeeperSkin: the old Skin material (renamed), base colour darkened by vertex colour G."""
    mat = bpy.data.materials.get("KeeperSkin") or bpy.data.materials.get("Skin")
    if mat is None:
        mat = bpy.data.materials.new("KeeperSkin")
    mat.name = "KeeperSkin"
    mat.use_nodes = True
    base = hex_to_linear(SKIN_HEX)
    mat.diffuse_color = base
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = base
    bsdf.inputs["Specular IOR Level"].default_value = 0.0
    if nt.nodes.get("SkinAO") is not None:
        return mat
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
    return mat


# ---------------------------------------------------------------------------------------------- armature helpers

def reset_pose(arm):
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


def set_pose(arm, clip, frame):
    act = bpy.data.actions["CharacterArmature|" + clip]
    arm.animation_data.action = act
    if hasattr(arm.animation_data, "action_slot") and act.slots:
        arm.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()


def skin_matrices(arm):
    """World-space skinning matrix per bone for the current pose (posed = M @ rest)."""
    inv_w = arm.matrix_world.inverted()
    out = {}
    for pb in arm.pose.bones:
        b = arm.data.bones[pb.name]
        out[pb.name] = arm.matrix_world @ (pb.matrix @ b.matrix_local.inverted()) @ inv_w
    return out


def to_rest(mats, p, weights):
    """Rest-pose position that skins to posed position p with these weights."""
    m = Matrix(((0.0,) * 4,) * 4)
    for k, w in weights.items():
        m = m + mats[k] * w
    return m.inverted() @ Vector(p)


# ---------------------------------------------------------------------------------------------- mesh utilities

def remove_objects(names):
    for n in names:
        for o in [o for o in bpy.data.objects if o.name == n]:
            m = o.data
            bpy.data.objects.remove(o)
            if m is not None and m.users == 0 and not m.name.endswith(".orig"):
                bpy.data.meshes.remove(m)


def tri_count(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def add_color_attr(me, rgb_list):
    attr = me.color_attributes.new("Color", 'FLOAT_COLOR', 'POINT')
    flat = []
    for r, g, b in rgb_list:
        flat += [r, g, b, 1.0]
    attr.data.foreach_set("color", flat)
    me.color_attributes.active_color = attr
    me.color_attributes.render_color_index = me.color_attributes.find("Color")


def shade_smooth_with_sharp(me, angle_deg, material_edges=True):
    """Smooth shading everywhere, split only at real creases: dihedral above angle_deg or a material change."""
    bm = bmesh.new()
    bm.from_mesh(me)
    thr = math.radians(angle_deg)
    sharp = {}
    for e in bm.edges:
        flag = False
        if len(e.link_faces) == 2:
            f0, f1 = e.link_faces
            if f0.normal.angle(f1.normal, 0.0) > thr:
                flag = True
            elif material_edges and f0.material_index != f1.material_index:
                flag = True
        sharp[(e.verts[0].index, e.verts[1].index)] = flag
    bm.free()
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    me.update()
    flags = []
    for e in me.edges:
        a, b = e.vertices
        flags.append(sharp.get((a, b), sharp.get((b, a), False)))
    me.edges.foreach_set("use_edge_sharp", flags)
    me.update()


def new_skinned_object(name, me, arm, group_names):
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    for g in group_names:
        obj.vertex_groups.new(name=g)
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()
    mod = obj.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    return obj


def apply_subsurf(obj, levels=1):
    mod = obj.modifiers.new("Subsurf", 'SUBSURF')
    mod.levels = levels
    mod.render_levels = levels
    mod.use_creases = True
    mod.boundary_smooth = 'PRESERVE_CORNERS'
    mod.use_limit_surface = False
    with bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj]):
        bpy.ops.object.modifier_apply(modifier=mod.name)


# ---------------------------------------------------------------------------------------------- body

def dominant(bm_vert_layer, f, names):
    acc = {}
    for v in f.verts:
        for gi, w in v[bm_vert_layer].items():
            acc[names[gi]] = acc.get(names[gi], 0.0) + w
    return max(acc, key=acc.get) if acc else ""


def cull_faces(bm, part, mats, names):
    dl = bm.verts.layers.deform.active
    kill = []
    for f in bm.faces:
        mat = mats[f.material_index]
        grp = dominant(dl, f, names)
        c = f.calc_center_median()
        if part == "Medieval_Body":
            if mat == "Metal":
                kill.append(f)                  # pauldrons: the cape covers the shoulders
            elif mat == "LightBrown" and grp.startswith("Shoulder"):
                kill.append(f)                  # the old collar: the cape collar replaces it
        elif part == "Medieval_Head":
            if mat in ("White", "DarkBrown"):
                kill.append(f)                  # old hair and old hood: replaced by the new Hood
            elif c.y > -0.05:
                kill.append(f)                  # back of the head, inside the hood
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')


def mark_creases(me, crease_deg, material_edges=True):
    """Crease attribute: 1 on edges that must stay crisp through subdivision."""
    bm = bmesh.new()
    bm.from_mesh(me)
    thr = math.radians(crease_deg)
    vals = {}
    for e in bm.edges:
        v = 0.0
        if len(e.link_faces) == 2:
            f0, f1 = e.link_faces
            if f0.normal.angle(f1.normal, 0.0) > thr:
                v = 1.0
            elif material_edges and f0.material_index != f1.material_index:
                v = 1.0
        vals[(e.verts[0].index, e.verts[1].index)] = v
    bm.free()
    attr = me.attributes.get("crease_edge") or me.attributes.new("crease_edge", 'FLOAT', 'EDGE')
    data = []
    for e in me.edges:
        a, b = e.vertices
        data.append(vals.get((a, b), vals.get((b, a), 0.0)))
    attr.data.foreach_set("value", data)


def source_matrix(name):
    flat = bpy.context.scene["keeper_mw_" + name]
    return Matrix([list(flat[i * 4:i * 4 + 4]) for i in range(4)])


def build_part(name, arm, group_names, slot_index, subdivide, crease_deg):
    """Load <name>.orig, cull, weld, remap materials, join triangles to quads and (optionally) subdivide.
    Returns (cloth_object, hand_object_or_None), both temporary."""
    orig = bpy.data.meshes[name + ".orig"]
    me = orig.copy()
    if me.has_custom_normals:
        me.attributes.remove(me.attributes["custom_normal"])
    me.transform(source_matrix(name))      # the source objects carry the FBX rotation; work in world coordinates
    mats = [m.name for m in orig.materials]
    part_groups = group_names[name]
    names = {i: n for i, n in enumerate(part_groups)}
    bm = bmesh.new()
    bm.from_mesh(me)
    cull_faces(bm, name, mats, names)
    for f in bm.faces:
        f.material_index = slot_index[MAT_MAP[mats[f.material_index]]]
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1.0e-4)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.faces.ensure_lookup_table()
    hand_faces = []
    if name == "Medieval_Body":
        dl = bm.verts.layers.deform.active
        for f in bm.faces:
            if f.material_index == slot_index["KeeperSkin"]:
                hand_faces.append(f.index)
    bm_hand = None
    if hand_faces:
        bm_hand = bm.copy()
        bm_hand.faces.ensure_lookup_table()
        keep = set(hand_faces)
        bmesh.ops.delete(bm_hand, geom=[f for f in bm_hand.faces if f.index not in keep], context='FACES')
        bmesh.ops.delete(bm_hand, geom=[v for v in bm_hand.verts if not v.link_faces], context='VERTS')
        bmesh.ops.delete(bm, geom=[bm.faces[i] for i in hand_faces], context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    out = []
    for b, is_hand in ((bm, False), (bm_hand, True)):
        if b is None:
            out.append(None)
            continue
        if is_hand:
            for f in b.faces:
                f.material_index = slot_index["KeeperLeather"]      # dark leather gloves
        if not is_hand:
            bmesh.ops.join_triangles(b, faces=list(b.faces), cmp_seam=False, cmp_sharp=False, cmp_uvs=False,
                                     cmp_vcols=False, cmp_materials=True,
                                     angle_face_threshold=math.radians(35.0), angle_shape_threshold=math.radians(45.0))
        m = bpy.data.meshes.new(name + ("_hand" if is_hand else "_cloth"))
        b.to_mesh(m)
        b.free()
        for slot in BODY_SLOTS:
            m.materials.append(bpy.data.materials[slot])
        o = new_skinned_object(m.name, m, arm, part_groups)
        if not is_hand and subdivide:
            mark_creases(m, crease_deg)
            apply_subsurf(o, 1)
        out.append(o)
    return out[0], out[1]


def smooth_hand(obj, iterations=2, factor=0.5, inflate=0.0028):
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    for _ in range(iterations):
        bmesh.ops.smooth_vert(bm, verts=bm.verts, factor=factor, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * inflate
    bm.to_mesh(me)
    bm.free()


def fold_shaping(obj):
    """Cloth folds on the trousers and tunic: displacement along the vertex normal in the rest pose."""
    me = obj.data
    mw = obj.matrix_world
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    black = BODY_SLOTS.index("Black")
    leather = BODY_SLOTS.index("KeeperLeather")
    creased = set()
    for e in bm.edges:
        if len(e.link_faces) == 2:
            f0, f1 = e.link_faces
            if f0.material_index != f1.material_index or f0.normal.angle(f1.normal, 0.0) > math.radians(40.0):
                creased.add(e.verts[0].index)
                creased.add(e.verts[1].index)
        else:
            creased.add(e.verts[0].index)
            creased.add(e.verts[1].index)
    rng = random.Random(11)
    for v in bm.verts:
        if v.index in creased:
            continue
        faces = v.link_faces
        if not faces:
            continue
        mi = faces[0].material_index
        p = mw @ v.co
        d = 0.0
        if mi == black and p.z < 1.05 and p.z > 0.36:
            side = 1.0 if p.x >= 0.0 else -1.0
            cx = side * 0.085
            th = math.atan2(p.x - cx, p.y + 0.07)
            # bunching above the boots, a crease at the back of the knee and vertical drape folds on the thighs
            d += 0.0065 * math.sin(p.z * 150.0 + 2.0 * th) * smoothstep(0.52, 0.42, p.z)
            d += 0.010 * math.exp(-((p.z - 0.64) / 0.035) ** 2) * math.cos(th - math.pi) * 0.5
            d += 0.0045 * math.sin(5.0 * th + p.z * 9.0) * smoothstep(0.55, 0.75, p.z) * smoothstep(1.02, 0.9, p.z)
        elif mi == leather and 1.0 < p.z < 1.40:
            th = math.atan2(p.x, p.y + 0.07)
            d += 0.0035 * math.sin(9.0 * th + p.z * 14.0) * smoothstep(1.06, 1.14, p.z) * smoothstep(1.4, 1.3, p.z)
        if d != 0.0:
            v.co += v.normal * d
    bm.to_mesh(me)
    bm.free()


def face_colour(z):
    return 1.0 - smoothstep(1.36, 1.44, z)       # the face is a full black void; the neck ramps back up


def build_body(arm, slot_index, group_names):
    pieces = []
    for name in BODY_PARTS:
        cloth, hand = build_part(name, arm, group_names, slot_index, subdivide=True, crease_deg=48.0)
        if name == "Medieval_Feet":
            pass
        if name == "Medieval_Legs" or name == "Medieval_Body":
            fold_shaping(cloth)
        pieces.append(cloth)
        if hand is not None:
            smooth_hand(hand)
            pieces.append(hand)
    bpy.ops.object.select_all(action='DESELECT')
    for o in pieces:
        o.select_set(True)
    with bpy.context.temp_override(object=pieces[0], active_object=pieces[0], selected_objects=pieces,
                                   selected_editable_objects=pieces):
        bpy.ops.object.join()
    body = pieces[0]
    body.name = "Body_LOD0"
    body.data.name = "Body_LOD0"
    me = body.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=2.0e-4)
    bm.to_mesh(me)
    bm.free()
    # vertex colours: G = AO, the face is a dark hollow and the neck ramps back up to 1
    mw = body.matrix_world
    cols = []
    for v in me.vertices:
        z = (mw @ v.co).z
        cols.append((0.0, face_colour(z), 0.0))
    add_color_attr(me, cols)
    me.attributes.remove(me.attributes["crease_edge"]) if "crease_edge" in me.attributes else None
    shade_smooth_with_sharp(me, 42.0)
    return body


# ---------------------------------------------------------------------------------------------- weights

SIDE_TOKENS = ("Shoulder", "UpperArm", "UpperLeg", "LowerLeg")
MAX_INFLUENCES = 4


def finalize(weights):
    items = sorted(weights.items(), key=lambda kv: -kv[1])[:MAX_INFLUENCES]
    total = sum(v for _, v in items)
    return {k: v / total for k, v in items if v > 1e-4}


def blend_keys(keys, z):
    if z >= keys[0][0]:
        return dict(keys[0][1])
    if z <= keys[-1][0]:
        return dict(keys[-1][1])
    for (z0, w0), (z1, w1) in zip(keys, keys[1:]):
        if z1 <= z <= z0:
            t = (z0 - z) / (z0 - z1)
            return {k: w0.get(k, 0.0) * (1.0 - t) + w1.get(k, 0.0) * t for k in set(w0) | set(w1)}
    return dict(keys[-1][1])


# (z, {bone token: weight}) down the torso, Idle-pose heights. Side tokens expand to .L / .R by the vertex side.
CAPE_TORSO_KEYS = [
    (1.47, {"Neck": 0.5, "Chest": 0.5}),
    (1.40, {"Chest": 1.0}),
    (1.28, {"Chest": 1.0}),
    (1.18, {"Torso": 0.7, "Chest": 0.3}),
    (1.08, {"Torso": 0.5, "Abdomen": 0.5}),
]


ARM_SEGMENTS = {".L": (Vector((0.101, -0.095, 1.310)), Vector((0.178, -0.020, 1.095))),
                ".R": (Vector((-0.127, -0.094, 1.311)), Vector((-0.215, -0.040, 1.094)))}


def seg_distance(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (p - (a + ab * t)).length


def cape_weights(p):
    """Weights for a point of the cowl or sash band (Idle-pose position)."""
    torso = blend_keys(CAPE_TORSO_KEYS, p.z)
    dx = abs(p.x - AXIS_X)
    side = ".L" if p.x >= AXIS_X else ".R"
    # points close to the upper arm follow it completely; the rest follows the torso
    arm = smoothstep(0.21, 0.11, seg_distance(p, *ARM_SEGMENTS[side])) * smoothstep(1.50, 1.44, p.z + 0.0 * dx)
    arm = arm if dx > 0.07 else arm * smoothstep(0.03, 0.07, dx)
    shoulder = smoothstep(0.06, 0.16, dx) * smoothstep(1.46, 1.38, p.z) * (1.0 - arm)
    out = {}
    for k, v in torso.items():
        out[k] = out.get(k, 0.0) + v * (1.0 - arm - shoulder * 0.35)
    out["UpperArm" + side] = out.get("UpperArm" + side, 0.0) + arm
    out["Shoulder" + side] = out.get("Shoulder" + side, 0.0) + shoulder * 0.35
    return finalize(out)


def hood_weights(z):
    head = smoothstep(1.35, 1.49, z)          # Idle-pose heights
    rest = 1.0 - head
    return finalize({"Head": head, "Neck": rest * 0.6, "Chest": rest * 0.4})


def strap_weights(p):
    t = smoothstep(1.30, 1.00, p.z)
    out = {"Chest": 1.0 - t}
    hip = {"Hips": 0.55, "Abdomen": 0.45}
    for k, v in hip.items():
        out[k] = out.get(k, 0.0) + v * t
    return finalize(out)


def bag_weights(p):
    return finalize({"Hips": 0.75, "UpperLeg.L": 0.25})


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

    def make_object(self, name, materials, arm, mats=None, sharp_deg=55.0):
        """mats: skin matrices of the pose the positions were designed in (None = rest pose)."""
        remove_objects([name])
        verts = list(self.verts)
        if mats is not None:
            verts = [tuple(to_rest(mats, p, w)) for p, w in zip(self.verts, self.weights)]
        me = bpy.data.meshes.new(name)
        me.from_pydata(verts, [], self.faces)
        for m in materials:
            me.materials.append(m)
        me.polygons.foreach_set("material_index", [materials.index(m) for m in self.fmat])
        me.update()
        add_color_attr(me, self.rgb)
        shade_smooth_with_sharp(me, sharp_deg, material_edges=False)
        names = set()
        for w in self.weights:
            names.update(w.keys())
        obj = new_skinned_object(name, me, arm, sorted(names))
        groups = {g.name: g for g in obj.vertex_groups}
        for vi, w in enumerate(self.weights):
            for n, val in w.items():
                if val > 1e-4:
                    groups[n].add([vi], val, 'REPLACE')
        return obj


# ---------------------------------------------------------------------------------------------- shells

def catmull(p0, p1, p2, p3, t):
    t2, t3 = t * t, t * t * t
    return tuple(0.5 * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3)
                 for a, b, c, d in zip(p0, p1, p2, p3))


def refine_rings(rings, per_segment):
    """Cubic interpolation of rings (tuples of floats) with `per_segment` rings per original segment."""
    out = []
    n = len(rings)
    for i in range(n - 1):
        p0 = rings[max(i - 1, 0)]
        p1, p2 = rings[i], rings[i + 1]
        p3 = rings[min(i + 2, n - 1)]
        for k in range(per_segment):
            out.append(catmull(p0, p1, p2, p3, k / per_segment))
    out.append(rings[-1])
    return out


def build_shell(b, pts_grid, thickness, centre, colour_fn, weight_fn, mat, design_weights=True):
    """Double shell over a grid of points pts_grid[k][i] (rings top to bottom, columns around). Ring 0 may be a
    single repeated point (apex). The inner shell is offset towards `centre(point)`.
    Returns (outer index grid, inner index grid)."""
    outer, inner = [], []
    nr = len(pts_grid)
    for k, row in enumerate(pts_grid):
        orow, irow = [], []
        cache = {}
        for i, p in enumerate(row):
            key = (round(p[0], 6), round(p[1], 6), round(p[2], 6))
            if key in cache:
                orow.append(cache[key][0])
                irow.append(cache[key][1])
                continue
            p = Vector(p)
            c = Vector(centre(p))
            radial = Vector((p.x - c.x, p.y - c.y, 0.0))
            if radial.length < 1e-6:
                radial = Vector((0.0, 0.0, 1.0))
                ip = p - radial * thickness
            else:
                radial.normalize()
                ip = p - radial * thickness
            edge = i in (0, len(row) - 1)
            w = weight_fn(p)
            o = b.vert(p, colour_fn(k, nr, False, edge), w)
            n = b.vert(ip, colour_fn(k, nr, True, edge), w)
            cache[key] = (o, n)
            orow.append(o)
            irow.append(n)
        outer.append(orow)
        inner.append(irow)
    for k in range(nr - 1):
        for i in range(len(pts_grid[k]) - 1):
            a, bb = outer[k][i], outer[k][i + 1]
            c, d = outer[k + 1][i + 1], outer[k + 1][i]
            quad(b, (a, d, c, bb), mat, cen=lambda q: centre(q), sign=1.0)
            a, bb = inner[k][i], inner[k][i + 1]
            c, d = inner[k + 1][i + 1], inner[k + 1][i]
            quad(b, (a, d, c, bb), mat, cen=lambda q: centre(q), sign=-1.0)
    return outer, inner


def quad(b, idx, mat, cen, sign):
    """Add a face (triangle when indices repeat), wound so its normal points away from (sign 1) or towards
    (sign -1) the centre."""
    seen = []
    for i in idx:
        if i not in seen:
            seen.append(i)
    if len(seen) < 3:
        return
    pts = [Vector(b.verts[i]) for i in seen]
    c = sum(pts, Vector()) / len(pts)
    cc = Vector(cen(c))
    away = Vector((c.x - cc.x, c.y - cc.y, 0.25 * (c.z - cc.z)))
    b.face(seen, mat, tuple(away * sign))


def rim_strips(b, outer, inner, mat, bottom=True, sides=True, top=False):
    nr = len(outer)
    ncols = len(outer[0]) - 1
    if bottom:
        for i in range(ncols):
            o0, o1 = outer[-1][i], outer[-1][i + 1]
            i0, i1 = inner[-1][i], inner[-1][i + 1]
            if o0 == o1:
                continue
            b.face((o0, i0, i1, o1), mat, (0.0, 0.0, -1.0))
    if top:
        for i in range(ncols):
            b.face((outer[0][i], inner[0][i], inner[0][i + 1], outer[0][i + 1]), mat, (0.0, 0.0, 1.0))
    if sides:
        for k in range(nr - 1):
            for col in (0, ncols):
                o0, o1 = outer[k][col], outer[k + 1][col]
                i0, i1 = inner[k][col], inner[k + 1][col]
                if o0 == o1 or o0 == i0:
                    continue
                nb = Vector(b.verts[outer[k][1 if col == 0 else ncols - 1]])
                away = Vector(b.verts[o0]) - nb
                away.z = 0.0
                if o0 == outer[k][1 if col == 0 else ncols - 1]:
                    continue
                if away.length < 1e-9:
                    continue
                b.face((o0, i0, i1, o1), mat, tuple(away))


# ---------------------------------------------------------------------------------------------- hood

# (z, rx, ry_front, ry_back, cy, phi0 degrees): the Phase C hood, a little deeper and with a narrower opening.
HOOD_KEYS = [
    (1.855, 0.020, 0.030, 0.030, -0.060, 70.0),
    (1.820, 0.090, 0.120, 0.120, -0.058, 58.0),
    (1.770, 0.150, 0.265, 0.178, -0.050, 33.0),
    (1.700, 0.186, 0.375, 0.188, -0.044, 20.0),
    (1.620, 0.198, 0.410, 0.190, -0.040, 16.0),
    (1.545, 0.187, 0.360, 0.176, -0.036, 17.0),
    (1.480, 0.182, 0.285, 0.170, -0.033, 29.0),
    (1.420, 0.205, 0.250, 0.195, -0.032, 52.0),
    (1.370, 0.238, 0.240, 0.230, -0.031, 76.0),
]
HOOD_COLS = 34


def hood_points(per_segment=2):
    keys = refine_rings([tuple(k) for k in HOOD_KEYS], per_segment)
    grid = []
    for k, (z, rx, ryf, ryb, cy, phi0) in enumerate(keys):
        row = []
        p0 = math.radians(phi0)
        for i in range(HOOD_COLS + 1):
            phi = p0 + (2.0 * math.pi - 2.0 * p0) * i / HOOD_COLS
            c = math.cos(phi)
            ry = ryf if c > 0.0 else ryb
            row.append((AXIS_X + rx * math.sin(phi), cy - ry * c, z))
        grid.append(row)
    return grid


HOOD_OFFSET = Vector((-0.013, -0.052, -0.040))   # the Idle pose leans the head forward and down from the rest pose


def build_hood(arm, cloth, mats):
    b = Builder()
    grid = [[tuple(Vector(p) + HOOD_OFFSET) for p in row] for row in hood_points(2)]
    nr = len(grid)
    # the apex ring collapses to a point at the top
    top = grid[0]
    apex = tuple(sum(p[i] for p in top) / len(top) for i in range(3))
    grid[0] = [apex] * len(top)

    def colour(k, nrr, inner, edge):
        return (0.0, 0.0 if inner else 1.0, 1.0 if (edge or k == nrr - 1) else 0.0)

    centre = lambda p: (AXIS_X + HOOD_OFFSET.x + 0.013, -0.040 + HOOD_OFFSET.y, 1.55 + HOOD_OFFSET.z)
    outer, inner = build_shell(b, grid, 0.016, centre, colour, lambda p: hood_weights(p.z), cloth)
    rim_strips(b, outer, inner, cloth)
    return b.make_object("Hood_LOD0", [cloth], arm, mats=mats), b


# ---------------------------------------------------------------------------------------------- cowl and sash

# Layered draped cowl: three overlapping wrapped layers, each a double shell designed in the Idle pose.
# keys: (z, rx, ry_front, ry_back, phi0 degrees) collar to hem, centred on (AXIS_X, AXIS_Y).
COWL_LAYERS = [
    dict(name="mantle", seed=23, teeth=1.0, clear=0.0, thick=0.011, skew=0.0, cols=44, front_rise=0.11, side_rise=0.095,
         keys=[(1.462, 0.075, 0.070, 0.078, 8.0), (1.435, 0.135, 0.100, 0.108, 10.0),
               (1.395, 0.205, 0.120, 0.118, 16.0), (1.350, 0.245, 0.135, 0.124, 22.0),
               (1.295, 0.268, 0.145, 0.128, 28.0), (1.225, 0.280, 0.150, 0.134, 32.0),
               (1.150, 0.290, 0.157, 0.146, 34.0), (1.100, 0.300, 0.162, 0.156, 36.0)]),
    dict(name="wrap", seed=5, teeth=0.4, clear=0.020, thick=0.010, skew=0.0, cols=40, front_rise=0.0, side_rise=0.0,
         keys=[(1.430, 0.232, 0.150, 0.150, 5.0), (1.400, 0.250, 0.158, 0.158, 7.0),
               (1.355, 0.262, 0.164, 0.160, 9.0), (1.310, 0.268, 0.168, 0.164, 12.0)]),
    dict(name="drape", seed=41, teeth=0.8, clear=0.034, thick=0.010, skew=42.0, cols=40, front_rise=0.04, side_rise=0.09,
         keys=[(1.452, 0.150, 0.105, 0.115, 22.0), (1.415, 0.225, 0.132, 0.125, 26.0),
               (1.365, 0.275, 0.152, 0.134, 30.0), (1.300, 0.305, 0.165, 0.144, 34.0),
               (1.230, 0.318, 0.170, 0.150, 38.0)]),
]
CAPE_THICK = 0.011


def hem_pattern(ncols, seed=23):
    """Drop (metres) of the torn lower edge per column: irregular, lopsided teeth with a few deep tears."""
    rng = random.Random(seed)
    drops = []
    while len(drops) <= ncols:
        width = rng.choice((3, 4, 4, 5, 6))
        depth = 0.040 + rng.random() * 0.075
        if rng.random() < 0.2:
            depth += 0.06
            width = max(width, 4)
        peak = 0.3 + 0.4 * rng.random()
        for k in range(width):
            t = k / width
            tri = t / peak if t < peak else (1.0 - t) / (1.0 - peak)
            drops.append(0.012 + depth * tri ** 1.1)
    return drops[:ncols + 1]


def layer_points(layer, per_segment=2):
    keys = refine_rings([tuple(k) for k in layer["keys"]], per_segment)
    ncols = layer["cols"]
    hem = hem_pattern(ncols, layer["seed"])
    grid = []
    nr = len(keys)
    for k, (z, rx, ryf, ryb, phi0) in enumerate(keys):
        row = []
        pa = math.radians(max(3.0, phi0 + layer["skew"]))
        pb = math.radians(max(3.0, phi0 - layer["skew"]))
        for i in range(ncols + 1):
            phi = pa + (2.0 * math.pi - pa - pb) * i / ncols
            c = math.cos(phi)
            ry = ryf if c > 0.0 else ryb
            zz = z - 0.06 * smoothstep(1.39, 1.462, z) * max(0.0, c) ** 2     # V neckline at the front
            low = smoothstep(1.36, 1.12, z)
            # the hem rises over the arms (forearms stay free) and at the front, and is deepest at the back
            zz += layer["side_rise"] * math.sin(phi) ** 2 * low + layer["front_rise"] * max(0.0, c) ** 2 * low
            tooth = hem[i] * layer["teeth"] * (0.25 + 0.3 * (1.0 - c))
            if k == nr - 1:
                zz -= tooth
            elif k == nr - 2:
                zz -= tooth * 0.22
            row.append((AXIS_X + rx * math.sin(phi), AXIS_Y - ry * c, zz))
        grid.append(row)
    return grid


def relax_from_body(grid, body_bvh, clear, layer_bvhs=(), layer_clear=0.0):
    """Push cape points radially outwards until they stand at least `clear` away from the body (Idle pose) and
    `layer_clear` outside the layers below them."""
    c0 = Vector((AXIS_X, AXIS_Y, 0.0))
    limit = [[Vector((p[0] - c0.x, p[1] - c0.y, 0.0)).length + 0.06 for p in row] for row in grid]
    for _ in range(6):
        moved = 0
        for k, row in enumerate(grid):
            for i, p in enumerate(row):
                v = Vector(p)
                radial = Vector((v.x - c0.x, v.y - c0.y, 0.0))
                if radial.length < 1e-6:
                    continue
                r = radial.length
                radial.normalize()
                need = 0.0
                loc, n, fi, dist = body_bvh.find_nearest(v)
                if loc is not None:
                    inside = (v - loc).dot(n) < 0.0
                    if inside or dist < clear:
                        need = max(need, (clear + dist) if inside else (clear - dist))
                for lb in layer_bvhs:
                    loc2, n2, fi2, d2 = lb.find_nearest(v)
                    if loc2 is None or d2 > layer_clear + 0.05:
                        continue
                    rn = Vector((loc2.x - c0.x, loc2.y - c0.y, 0.0)).length
                    if r - rn < layer_clear:
                        need = max(need, layer_clear - (r - rn))
                if need > 0.0:
                    nr_ = min(r + need * 0.8 + 0.001, limit[k][i])
                    row[i] = (c0.x + radial.x * nr_, c0.y + radial.y * nr_, v.z)
                    moved += 1
        for k, row in enumerate(grid):
            if k == 0:
                continue
            rad = [Vector((p[0] - c0.x, p[1] - c0.y, 0.0)).length for p in row]
            sm = [rad[0]] + [0.25 * rad[i - 1] + 0.5 * rad[i] + 0.25 * rad[i + 1] for i in range(1, len(rad) - 1)] + [rad[-1]]
            for i, p in enumerate(row):
                d = Vector((p[0] - c0.x, p[1] - c0.y, 0.0))
                if d.length < 1e-6:
                    continue
                s = max(sm[i], rad[i] * 0.999) if i not in (0, len(row) - 1) else rad[i]
                dn = d.normalized()
                row[i] = (c0.x + dn.x * s, c0.y + dn.y * s, p[2])
        if moved == 0:
            break
    return grid


def layer_bvh(b, outer):
    verts = [Vector(v) for v in b.verts]
    polys = [tuple(f) for f in b.faces if all(i in outer for i in f)]
    return BVHTree.FromPolygons(verts, polys)


def sash_points():
    keys = [(1.075, 0.128, 0.092, 0.088, 0.0), (1.035, 0.136, 0.098, 0.092, 0.0), (0.995, 0.130, 0.104, 0.094, 0.0)]
    grid = []
    ncols = 28
    for (z, rx, ryf, ryb, phi0) in refine_rings(keys, 1):
        row = []
        for i in range(ncols + 1):
            phi = 2.0 * math.pi * i / ncols
            c = math.cos(phi)
            ry = ryf if c > 0.0 else ryb
            row.append((AXIS_X + rx * math.sin(phi), AXIS_Y - ry * c, z + 0.012 * math.sin(phi * 2.0)))
        grid.append(row)
    return grid


def build_cape(arm, cloth, body_bvh, mats):
    """The cowl layers plus the sash (band and short tail) in one cloth mesh."""
    b = Builder()
    centre = lambda p: (AXIS_X, AXIS_Y, 1.2)
    below = []

    def colour(k, nrr, inner, edge):
        r = round((k / (nrr - 1)) ** 1.4, 4)
        return (r, 1.0, 1.0 if k == nrr - 1 else 0.0)

    for layer in COWL_LAYERS:
        grid = layer_points(layer, 2)
        grid = relax_from_body(grid, body_bvh, 0.026 + layer["clear"] + layer["thick"], below, 0.014)
        outer, inner = build_shell(b, grid, layer["thick"], centre, colour, cape_weights, cloth)
        rim_strips(b, outer, inner, cloth)
        below.append(layer_bvh(b, set(v for row in outer for v in row)))
    # sash: wrapped band at the waist plus a short hanging tail at the right back hip
    sash_first = len(b.verts)
    grid = relax_from_body(sash_points(), body_bvh, 0.012, (), 0.0)
    sash_w = lambda p: {"Hips": 0.55, "Abdomen": 0.45}
    outer, inner = build_shell(b, grid, 0.008, centre, lambda k, n, i, e: (0.0, 1.0, 0.0), sash_w, cloth)
    rim_strips(b, outer, inner, cloth, bottom=True, sides=False, top=True)
    tail = []
    n = 8
    for k in range(n + 1):
        t = k / n
        cx = -0.105 - 0.02 * t
        cy = 0.072 + 0.045 * t * t
        z = 1.03 - 0.235 * t
        w = 0.034 + 0.016 * t
        tail.append([(cx + dx * w, cy, z - (0.035 * (1.0 - abs(dx)) if k == n else 0.0)) for dx in (-1.0, -0.5, 0.0, 0.5, 1.0)])

    def tail_colour(k, nrr, inner, edge):
        t = k / (nrr - 1)
        return (round(t ** 1.3, 4), 1.0, 1.0 if k == nrr - 1 else 0.0)

    to, ti = build_shell(b, tail, 0.007, centre, tail_colour, lambda p: {"Hips": 1.0}, cloth)
    rim_strips(b, to, ti, cloth)
    obj = b.make_object("Cape_LOD0", [cloth], arm, mats=mats)
    obj.data["sash_first_vertex"] = sash_first
    return obj, b


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


def strap(b, path, width, thick, mat, rgb, weights_fn, subdiv=5):
    """Flat band along path = [(pos, normal)] (smoothly resampled), rectangular section."""
    pts = [Vector(p) for p, _ in path]
    nrm = [Vector(n).normalized() for _, n in path]
    pos2, nrm2 = [], []
    for i in range(len(pts) - 1):
        p0 = pts[max(i - 1, 0)]
        p3 = pts[min(i + 2, len(pts) - 1)]
        for k in range(subdiv):
            t = k / subdiv
            pos2.append(Vector(catmull(tuple(p0), tuple(pts[i]), tuple(pts[i + 1]), tuple(p3), t)))
            nrm2.append((nrm[i] * (1 - t) + nrm[i + 1] * t).normalized())
    pos2.append(pts[-1])
    nrm2.append(nrm[-1])
    rings = []
    for i, (p, n) in enumerate(zip(pos2, nrm2)):
        t = pos2[min(i + 1, len(pos2) - 1)] - pos2[max(i - 1, 0)]
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


# Bag profile, top to bottom: (w, half width, half depth). Widest below the middle so it sags.
BAG_RINGS = [(0.100, 0.090, 0.032), (0.082, 0.104, 0.042), (0.055, 0.115, 0.050), (0.020, 0.122, 0.054),
             (-0.020, 0.123, 0.055), (-0.058, 0.113, 0.050), (-0.088, 0.092, 0.040), (-0.108, 0.060, 0.028)]
BAG_SIDES = 16


def bag_depth(w):
    for (w0, _, d0), (w1, _, d1) in zip(BAG_RINGS, BAG_RINGS[1:]):
        if w1 <= w <= w0:
            t = (w0 - w) / (w0 - w1)
            return d0 + (d1 - d0) * t
    return BAG_RINGS[0][2] if w > BAG_RINGS[0][0] else BAG_RINGS[-1][2]


def rounded_rect(hw, hd, n):
    """n points around a superellipse section (u across, v in depth)."""
    pts = []
    e = 2.6
    for i in range(n):
        a = 2.0 * math.pi * i / n
        c, s = math.cos(a), math.sin(a)
        pts.append((hw * math.copysign(abs(c) ** (2.0 / e), c), hd * math.copysign(abs(s) ** (2.0 / e), s)))
    return pts


BAG_CENTRE = Vector((0.205, 0.055, 0.745))
BAG_YAW = math.radians(-40.0)       # outward direction rotated towards the back
STRAP_PATH = [
    ((-0.150, -0.085, 1.395), (-0.3, 0.2, 0.9)),
    ((-0.150, 0.012, 1.385), (-0.4, 0.8, 0.5)),
    ((-0.100, 0.075, 1.330), (-0.2, 1.0, 0.1)),
    ((-0.020, 0.085, 1.215), (0.0, 1.0, 0.0)),
    ((0.060, 0.090, 1.090), (0.1, 1.0, 0.0)),
    ((0.130, 0.100, 0.980), (0.3, 0.95, 0.0)),
    ((0.185, 0.095, 0.880), (0.7, 0.7, 0.0)),
]


def build_satchel(arm, leather, mats):
    b = Builder()
    outward = Vector((math.cos(0.0) * 1.0, 0.0, 0.0))
    ct, st = math.cos(BAG_YAW), math.sin(BAG_YAW)
    # outward = +X turned by BAG_YAW about Z; tangent along the hip surface (towards the front is -Y)
    outward = Vector((ct, -st, 0.0))
    tangent = Vector((st, ct, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    c0 = BAG_CENTRE
    rgb = (0.0, 1.0, 0.0)
    wts = bag_weights(c0)

    def P(u, v, w):
        return c0 + tangent * u + outward * v + up * w

    def radial_dir(idx, squash=1.0):
        cen = sum((Vector(b.verts[q]) for q in idx), Vector()) / len(idx)
        d = cen - c0
        return (d.x, d.y, d.z * squash)

    n = BAG_SIDES
    rings = []
    for w, hw, hd in BAG_RINGS:
        rings.append([b.vert(P(u, v, w), rgb, wts) for u, v in rounded_rect(hw, hd, n)])
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(n):
            j = (i + 1) % n
            idx = (r0[i], r0[j], r1[j], r1[i])
            b.face(idx, leather, radial_dir(idx, 0.2))
    bottom = b.vert(P(0.0, 0.0, -0.116), rgb, wts)
    for i in range(n):
        j = (i + 1) % n
        b.face((rings[-1][i], rings[-1][j], bottom), leather, (0.0, 0.0, -1.0))
    # flap: rounded front, folded over the top. Double shell with a rim.
    cols = 14
    outer, inner = [], []
    for k in range(cols + 1):
        t = -1.0 + 2.0 * k / cols
        u = t * 0.106
        drop = 0.050 + 0.082 * math.sqrt(max(0.0, 1.0 - t * t))
        w_top = 0.099
        w_mid = w_top - drop * 0.5
        w_bot = w_top - drop
        col_o = [P(u, -bag_depth(w_top) * 0.9, w_top + 0.004), P(u, bag_depth(w_top) + 0.010, w_top + 0.010),
                 P(u, bag_depth(w_top - drop * 0.25) + 0.014, w_top - drop * 0.25),
                 P(u, bag_depth(w_mid) + 0.014, w_mid),
                 P(u, bag_depth(w_bot) + 0.014, w_bot)]
        col_i = []
        for q in col_o:
            local = q - c0
            col_i.append(c0 + tangent * (local.dot(tangent) * 0.985) + outward * (local.dot(outward) - 0.008)
                         + up * (local.dot(up) - 0.007))
        outer.append([b.vert(q, rgb, wts) for q in col_o])
        inner.append([b.vert(q, rgb, wts) for q in col_i])
    rows = len(outer[0])
    for k in range(cols):
        for r in range(rows - 1):
            for grid, sign in ((outer, 1.0), (inner, -1.0)):
                idx = (grid[k][r], grid[k + 1][r], grid[k + 1][r + 1], grid[k][r + 1])
                d = radial_dir(idx)
                b.face(idx, leather, (d[0] * sign, d[1] * sign, d[2] * sign))
    flap_centre = c0 + up * 0.05
    last = rows - 1
    for k in range(cols):
        o0, o1, i0, i1 = outer[k][last], outer[k + 1][last], inner[k][last], inner[k + 1][last]
        mid = (Vector(b.verts[o0]) + Vector(b.verts[o1])) / 2.0
        b.face((o0, o1, i1, i0), leather, tuple(mid - flap_centre))
    for col in (0, cols):
        for r in range(rows - 1):
            idx = (outer[col][r], outer[col][r + 1], inner[col][r + 1], inner[col][r])
            away = tangent * (1.0 if col == cols else -1.0)
            b.face(idx, leather, tuple(away))
    # buckle and strap tab on the flap
    wb = 0.099 - 0.132
    box(b, P(0.0, bag_depth(wb) + 0.030, wb + 0.012), (tangent, outward, up), (0.017, 0.008, 0.02), leather, rgb, wts)
    box(b, P(0.0, bag_depth(wb - 0.04) + 0.010, wb - 0.045), (tangent, outward, up), (0.022, 0.006, 0.03),
        leather, rgb, wts)
    # strap: right shoulder across the back to the bag top
    path = STRAP_PATH + [(tuple(P(0.0, 0.0, 0.112)), tuple(up))]
    strap(b, path, 0.040, 0.011, leather, rgb, strap_weights)
    return b.make_object("Satchel_LOD0", [leather], arm, mats=mats, sharp_deg=50.0), b



# ---------------------------------------------------------------------------------------------- cuffs

def build_cuffs(arm, slots, mats):
    """Short flared leather cuffs at both wrists (Idle-pose design), returned as one object that is joined into the
    body. slots = [KeeperLeather, KeeperSkin, Black] materials so the slot order matches Body_LOD0."""
    b = Builder()
    leather = slots[0]
    inv = arm.matrix_world
    for side in (".L", ".R"):
        w = (inv @ arm.pose.bones["Wrist" + side].head)
        e = (inv @ arm.pose.bones["LowerArm" + side].head)
        d = (w - e).normalized()
        ref = Vector((0.0, -1.0, 0.0)) if abs(d.y) < 0.9 else Vector((1.0, 0.0, 0.0))
        u = d.cross(ref).normalized()
        v = d.cross(u).normalized()
        rows = []
        samples = [(-0.062, 0.043), (-0.045, 0.044), (-0.020, 0.048), (0.000, 0.053), (0.012, 0.055)]
        for t, r in samples:
            row = []
            for i in range(17):
                a = 2.0 * math.pi * i / 16
                row.append(tuple(w + d * t + u * (math.cos(a) * r) + v * (math.sin(a) * r)))
            rows.append(row)

        def colour(k, nrr, inner, edge):
            return (0.0, 1.0, 0.0)

        def wfn(p, side=side, w=w, d=d):
            t = (Vector(p) - w).dot(d)
            f = smoothstep(-0.05, 0.01, t)
            return finalize({"LowerArm" + side: 1.0 - f * 0.7, "Wrist" + side: f * 0.7})

        def centre(p, w=w, d=d):
            q = w + d * (Vector(p) - w).dot(d)
            return (q.x, q.y, q.z)

        outer, inner = build_shell(b, rows, 0.006, centre, colour, wfn, leather)
        rim_strips(b, outer, inner, leather, bottom=True, sides=False, top=True)
    me_obj = b.make_object("Cuffs_tmp", list(slots), arm, mats=mats, sharp_deg=50.0)
    return me_obj


def join_into(body, extra):
    objs = [body] + extra
    for o in objs:
        o.select_set(True)
    with bpy.context.temp_override(object=body, active_object=body, selected_objects=objs,
                                   selected_editable_objects=objs):
        bpy.ops.object.join()
    shade = body.data
    return body

# ---------------------------------------------------------------------------------------------- main

def tri_total():
    total = {}
    for o in bpy.data.objects:
        if o.type == 'MESH' and o.name.endswith("_LOD0"):
            total[o.name] = tri_count(o.data)
    return total


def evaluated_bvh(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    mw = ev.matrix_world
    verts = [mw @ v.co for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]
    bvh = BVHTree.FromPolygons(verts, polys)
    ev.to_mesh_clear()
    return bvh


def verify_fit(obj, builder):
    """At the design pose the skinned mesh must reproduce the designed positions; print the worst error."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    mw = ev.matrix_world
    worst, at = 0.0, -1
    for i, v in enumerate(me.vertices):
        e = (mw @ v.co - Vector(builder.verts[i])).length
        if e > worst:
            worst, at = e, i
    ev.to_mesh_clear()
    print("FIT", obj.name, "worst error %.4f m at vertex %d" % (worst, at), builder.weights[at] if at >= 0 else "")


def main():
    arm = bpy.data.objects["CharacterArmature"]
    reset_pose(arm)

    # keep pristine copies of the source body meshes
    for name in BODY_PARTS:
        obj = bpy.data.objects.get(name)
        if obj is not None and bpy.data.meshes.get(name + ".orig") is None:
            orig = obj.data.copy()
            orig.name = name + ".orig"
            orig.use_fake_user = True
    # vertex group names of each source part (the weights in the .orig meshes index into them), kept on the scene
    scene = bpy.context.scene
    for name in BODY_PARTS:
        if bpy.data.objects.get(name) is not None:
            scene["keeper_groups_" + name] = [g.name for g in bpy.data.objects[name].vertex_groups]
            scene["keeper_mw_" + name] = [x for row in bpy.data.objects[name].matrix_world for x in row]
    group_names = {name: list(scene["keeper_groups_" + name]) for name in BODY_PARTS}

    cloth = make_material("KeeperCloth", CLOTH_HEX, "5A6678")
    leather = make_material("KeeperLeather", LEATHER_HEX, "4A3A2C")
    skin = skin_material()
    black = bpy.data.materials.get("Black")
    black.diffuse_color = hex_to_linear(DARK_HEX)
    if black.node_tree and black.node_tree.nodes.get("Principled BSDF"):
        black.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = hex_to_linear(DARK_HEX)
    slot_index = {n: i for i, n in enumerate(BODY_SLOTS)}

    remove_objects(LOD0_NAMES)
    body = build_body(arm, slot_index, group_names)
    # old objects go after the body is built (build_part reads their vertex groups)
    remove_objects(OLD_OBJECTS)
    for m in [m for m in bpy.data.materials if m.users == 0 and m.name in ("Metal", "White", "Gold", "Brown",
                                                                          "LightBrown", "DarkBrown")]:
        pass

    set_pose(arm, "Idle", 1)
    mats = skin_matrices(arm)
    body_bvh = evaluated_bvh(body)
    cuffs = build_cuffs(arm, [leather, skin, black], mats)
    join_into(body, [cuffs])
    hood, hb = build_hood(arm, cloth, mats)
    cape, cb = build_cape(arm, cloth, body_bvh, mats)
    satchel, sb = build_satchel(arm, leather, mats)
    bpy.context.view_layer.update()
    for o, bb in ((hood, hb), (cape, cb), (satchel, sb)):
        verify_fit(o, bb)
    reset_pose(arm)
    tris = tri_total()
    print("TRIS", tris)
    print("TRIS LOD0 total", sum(tris.values()))
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


if __name__ == "__main__":
    main()
