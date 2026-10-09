"""Keeper outfit, Phase C2 (LOD0 meshes): closed-top hood, two-layer draped cowl, fitted tunic, boots, satchel.

Run:  blender.exe -b ArtSource/Keeper/Keeper.blend -P ArtSource/Keeper/build_outfit.py
Then: blender.exe -b ArtSource/Keeper/Keeper.blend -P ArtSource/Keeper/build_lods.py   (LOD1 copies)

Works inside Keeper.blend (true scale, Z up, the keeper faces -Y, left = +X). Idempotent: the first run keeps
pristine copies of the four Medieval_* body meshes as "<name>.orig" (fake user, not exported) and every run
rebuilds from them. The Phase C hood/cloak/satchel objects, the four body objects and any stale *_LOD1 meshes are
removed (the LOD1 set must be rebuilt with build_lods.py afterwards).

Produces (all skinned to CharacterArmature with an Armature modifier, at most 4 influences per vertex):
  Body_LOD0     Body, Head and Legs welded, quad-joined, one subdivision level, folds on tunic and trousers, fitted
                (then looser) trousers, dark tunic and sleeves, leather-slot gloves darkened by vertex G, leather
                cuffs with seam grooves and the procedural boots (sole, toe cap, shaft with strap, rolled cuff).
                Slots KeeperLeather, KeeperSkin (face only), Black.
  Hood_LOD0     slot KeeperCloth. Fitted, closed on top and sides; the opening is a front arch under a slight brim.
                Double shell, subdivided once; the inner shell is the black void.
  Cape_LOD0     slot KeeperCloth. Two-layer draped cowl with torn edges, the waist sash (band and short tail) and the
                short tunic skirt below the sash, all subdivided once. Cape_base is the unsubdivided copy used only by
                check_clipping.py (render-hidden, never exported).
  Satchel_LOD0  slot KeeperLeather. Bag on the left hip, strap from the right shoulder across the back.

Vertex colours ("Color", point domain, linear floats; export with colors_type LINEAR):
  R = sway weight: cowl edge 1 -> collar 0 and the sash tail 0 -> 1; 0 on everything else
  G = AO: 1 normally, 0 on the hood's inner shell and on the face (a full black void), 0.42 on the gloves
  B = worn edge: 1 on the cowl's torn edges, the sash tail end and the hood rim, 0 elsewhere

The cowl, sash, hood and satchel are designed in the Idle pose (frame 1) and mapped back to the rest pose through
the inverse of their own skin weights, so they fit the idle body exactly and the armature modifier reproduces the
design at Idle.
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
# "Cloak" is legacy cleanup of the Phase C long cloak; the other names are the Phase C hood, satchel and body parts.
OLD_OBJECTS = ["Hood", "Cloak", "Satchel"] + BODY_PARTS
# Source material -> slot of the joined body. Metal (pauldrons) and White (hair) are culled before this.
MAT_MAP = {"LightBrown": "KeeperLeather", "DarkBrown": "KeeperLeather", "Brown": "KeeperLeather",
           "Gold": "KeeperLeather", "Skin": "KeeperSkin", "KeeperSkin": "KeeperSkin", "Black": "Black"}
# The fitted tunic (all of the old torso and sleeves) and the gloves use the dark slot; only the belt buckle stays leather.
BODY_MAT_MAP = dict(MAT_MAP, LightBrown="Black", DarkBrown="Black", Brown="Black", Skin="Black", KeeperSkin="Black")
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


LEG_FIT = [(0.30, 0.40), (0.40, 0.44), (0.46, 0.52), (0.52, 0.66), (0.60, 0.78), (0.70, 0.88), (0.80, 0.94),
           (0.90, 0.98), (0.95, 1.0), (1.00, 1.0)]


def fit_trousers(bm):
    """The Quaternius trousers are baggy capris: pull every vertex towards its leg axis so they fit and tuck into
    the boots (rest pose, world coordinates)."""
    for v in bm.verts:
        p = v.co
        side = 1.0 if p.x >= 0.0 else -1.0
        z = p.z
        if z >= LEG_FIT[-1][0]:
            continue
        f = LEG_FIT[0][1]
        for (z0, f0), (z1, f1) in zip(LEG_FIT, LEG_FIT[1:]):
            if z0 <= z <= z1:
                f = f0 + (f1 - f0) * (z - z0) / (z1 - z0)
                break
        t = smoothstep(0.4, 1.0, z)
        axis = Vector((side * (0.085 + 0.005 * t), -0.050 - 0.015 * t, z))
        v.co = Vector((axis.x + (p.x - axis.x) * f, axis.y + (p.y - axis.y) * f, z))


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
    hand_mark = [f for f in bm.faces if name == "Medieval_Body" and mats[f.material_index] in ("Skin", "KeeperSkin")]
    hand_idx = [f.index for f in hand_mark]
    for f in bm.faces:
        f.material_index = slot_index[(BODY_MAT_MAP if name == "Medieval_Body" else MAT_MAP)[mats[f.material_index]]]
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1.0e-4)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if name == "Medieval_Legs":
        fit_trousers(bm)
    bm.faces.ensure_lookup_table()
    hand_faces = []
    if name == "Medieval_Body":
        bm.faces.ensure_lookup_table()
        hand_faces = list(hand_idx)
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
                f.material_index = slot_index["KeeperLeather"]      # gloves: leather, darkened by vertex G
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


def smooth_hand(obj, iterations=2, factor=0.5, inflate=0.005):
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


def fold_shaping(obj, kind):
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
        if kind == "legs" and p.z < 1.0 and p.z > 0.40:
            side = 1.0 if p.x >= 0.0 else -1.0
            th = math.atan2(p.x - side * 0.085, p.y + 0.055)
            d += 0.010 * math.exp(-((p.z - 0.62) / 0.05) ** 2) * (0.6 + 0.4 * math.cos(th - math.pi))   # knee bulge
            d += 0.014 * math.exp(-((p.z - 0.50) / 0.04) ** 2)                                         # blouse over the cuff
            d += 0.007 * math.sin(3.0 * th + p.z * 22.0) * smoothstep(0.46, 0.6, p.z) * smoothstep(0.85, 0.65, p.z)
            d += 0.007 * math.sin(4.0 * th + p.z * 9.0) * smoothstep(0.5, 0.8, p.z) * smoothstep(1.0, 0.92, p.z)
        elif kind == "tunic" and 1.0 < p.z < 1.40 and mi == black:
            th = math.atan2(p.x, p.y + 0.07)
            d += 0.003 * math.sin(5.0 * th + p.z * 9.0) * smoothstep(1.02, 1.12, p.z) * smoothstep(1.4, 1.3, p.z)
        if d != 0.0:
            v.co += v.normal * d
    bm.to_mesh(me)
    bm.free()


SLEEVE_UPPER = 0.025     # sleeve thickening at the upper arm (m, along the normal)
SLEEVE_WRIST = 0.020     # ... tapering to this at the wrist
SLEEVE_FLARE = 0.004     # extra flare over the last SLEEVE_FLARE_LEN before the glove cuff
SLEEVE_FLARE_LEN = 0.035
COWL_FIT = 0.3   # share of the sleeve thickening the cowl is fitted against
RAMP0 = 0.55            # the upper arm stays at 0 over the top 55% (under the cowl), then ramps to the elbow
SLEEVE_PEAK = 0.030      # widest point, just below the elbow
SLEEVE_FOLD = 0.003     # gentle fold amplitude


def thicken_sleeves(obj, arm, upper=SLEEVE_UPPER, wrist_t=SLEEVE_WRIST):
    """Push the sleeve (arm part of the tunic) out along its normals: `upper` at the shoulder end tapering to
    `wrist_t` at the wrist, a soft flare over the last few cm, and gentle folds. The weight comes from the skin
    weights of the arm bones (upper arm, forearm, wrist), so it blends out smoothly into the torso at the shoulder
    seam. Skin weights are untouched."""
    me = obj.data
    mw = obj.matrix_world
    amw = arm.matrix_world
    names = {g.index: g.name for g in obj.vertex_groups}
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    dl = bm.verts.layers.deform.verify()
    black = BODY_SLOTS.index("Black")
    ends = {}
    for side in (".L", ".R"):
        ub = arm.data.bones["UpperArm" + side]
        lb = arm.data.bones["LowerArm" + side]
        a = amw @ ub.head_local
        w = amw @ lb.tail_local
        ends[side] = (a, w, (amw @ lb.head_local - a).dot((w - a).normalized()) / (w - a).length)
    for v in bm.verts:
        wts = {}
        for gi, wt in v[dl].items():
            n = names[gi]
            wts[n] = wt
        side = None
        for sd in (".L", ".R"):
            tot = sum(wts.get(k + sd, 0.0) for k in ("UpperArm", "LowerArm", "Wrist"))
            if tot > 0.0 and (side is None or tot > side[1]):
                side = (sd, tot)
        if side is None:
            continue
        sd, mask = side
        if not v.link_faces or v.link_faces[0].material_index != black:
            continue
        a, w, te = ends[sd]
        axis = w - a
        length = axis.length
        axis /= length
        p = mw @ v.co
        rel = p - a
        t = rel.dot(axis) / length                       # 0 at the shoulder joint, 1 at the wrist
        if t > 1.15:
            continue
        m = smoothstep(0.35, 0.95, mask)
        tt = min(max(t, 0.0), 1.0)
        if tt < te:                                       # upper arm: 0 over the top 35%, up to `upper` at the elbow
            d = upper * smoothstep(RAMP0 * te, te, tt) * m
            d *= 1.0 - 0.85 * smoothstep(0.15, 0.7, v.normal.z)       # the top of the arm is what pokes through the cowl
        else:                                             # forearm: peak just below the elbow, tapering to the wrist
            f = (tt - te) / max(1.0 - te, 1e-6)
            pk = upper + (SLEEVE_PEAK - upper) * smoothstep(0.0, 0.15, f)
            k = smoothstep(0.15, 1.0, f)
            d = (pk * (1.0 - k) + wrist_t * k) * m
        s = (w - p).dot(axis)                            # distance before the wrist (m)
        if s < SLEEVE_FLARE_LEN:
            d += SLEEVE_FLARE * smoothstep(SLEEVE_FLARE_LEN, 0.0, max(s, 0.0)) * m
        radial = rel - axis * rel.dot(axis)
        th = math.atan2(radial.dot(Vector((0.0, 0.0, 1.0))), radial.dot(axis.cross(Vector((0.0, 0.0, 1.0)))))
        d += SLEEVE_FOLD * math.sin(4.0 * th + s * 55.0) * smoothstep(0.3, 0.55, t) * smoothstep(1.0, 0.8, t) * m
        d += SLEEVE_FOLD * 0.8 * math.sin(2.0 * th - s * 30.0) * smoothstep(0.3, 0.6, t) * m
        v.co += v.normal * d
    bm.to_mesh(me)
    bm.free()


def face_colour(z):
    return 1.0 - smoothstep(1.36, 1.44, z)       # the face is a full black void; the neck ramps back up


def build_body(arm, slot_index, group_names):
    pieces = []
    for name in BODY_PARTS[:3]:
        cloth, hand = build_part(name, arm, group_names, slot_index, subdivide=True, crease_deg=48.0)
        if name == "Medieval_Legs":
            fold_shaping(cloth, "legs")
        elif name == "Medieval_Body":
            fold_shaping(cloth, "tunic")
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
    gnames = {g.index: g.name for g in body.vertex_groups}
    for v in me.vertices:
        z = (mw @ v.co).z
        g = face_colour(z)
        if v.groups:
            top = max(v.groups, key=lambda x: x.weight)
            if gnames[top.group].startswith(HAND_GROUPS):
                g = 0.42                  # gloves: deep brown-black
        cols.append((0.0, g, 0.0))
    add_color_attr(me, cols)
    me.attributes.remove(me.attributes["crease_edge"]) if "crease_edge" in me.attributes else None
    shade_smooth_with_sharp(me, 42.0)
    return body


# ---------------------------------------------------------------------------------------------- weights

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


def build_shell(b, pts_grid, thickness, centre, colour_fn, weight_fn, mat):
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
            closed = (Vector(row[0]) - Vector(row[-1])).length < 1e-6
            edge = i in (0, len(row) - 1) and not closed
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
                if outer[k][0] == outer[k][ncols] and outer[k + 1][0] == outer[k + 1][ncols]:
                    continue                  # both rows closed: no open edge here
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
    (1.790, 0.020, 0.030, 0.030, -0.030, 0.0),     # phi0 = 0: closed ring (solid crown); the opening is a front arch only
    (1.765, 0.075, 0.095, 0.100, -0.032, 0.0),
    (1.735, 0.122, 0.170, 0.150, -0.034, 0.0),
    (1.715, 0.134, 0.195, 0.158, -0.034, 14.0),
    (1.690, 0.143, 0.215, 0.164, -0.035, 26.0),
    (1.650, 0.151, 0.215, 0.170, -0.035, 33.0),
    (1.600, 0.156, 0.218, 0.172, -0.035, 34.0),
    (1.540, 0.150, 0.208, 0.170, -0.034, 40.0),
    (1.480, 0.150, 0.188, 0.165, -0.033, 50.0),
    (1.425, 0.170, 0.188, 0.176, -0.032, 64.0),
    (1.375, 0.205, 0.205, 0.205, -0.031, 80.0),
]
HOOD_COLS = 24


def hood_points(per_segment=2):
    keys = refine_rings([tuple(k) for k in HOOD_KEYS], per_segment)
    grid = []
    for k, (z, rx, ryf, ryb, cy, phi0) in enumerate(keys):
        row = []
        p0 = math.radians(max(0.0, phi0))
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

    centre = lambda p: (AXIS_X + HOOD_OFFSET.x + 0.013, -0.034 + HOOD_OFFSET.y, 1.55 + HOOD_OFFSET.z)
    outer, inner = build_shell(b, grid, 0.011, centre, colour, lambda p: hood_weights(p.z), cloth)
    rim_strips(b, outer, inner, cloth)
    return b.make_object("Hood_LOD0", [cloth], arm, mats=mats), b


# ---------------------------------------------------------------------------------------------- cowl and sash

# Layered draped cowl: three overlapping wrapped layers, each a double shell designed in the Idle pose.
# keys: (z, rx, ry_front, ry_back, phi0 degrees) collar to hem, centred on (AXIS_X, AXIS_Y).
COWL_LAYERS = [
    dict(name="mantle", seed=23, teeth=1.0, clear=0.0, thick=0.0075, skew=6.0, cols=36, front_rise=0.10, side_rise=0.09,
         tilt=0.05, tilt_phase=2.2, fold=0.020, fold_k=5.0, fold_z=30.0,
         keys=[(1.462, 0.075, 0.070, 0.078, 8.0), (1.435, 0.135, 0.100, 0.108, 10.0),
               (1.395, 0.205, 0.120, 0.118, 16.0), (1.350, 0.245, 0.135, 0.124, 22.0),
               (1.295, 0.268, 0.145, 0.128, 28.0), (1.225, 0.280, 0.150, 0.134, 32.0),
               (1.150, 0.290, 0.157, 0.146, 34.0), (1.100, 0.300, 0.162, 0.156, 36.0)]),
    dict(name="drape", seed=41, teeth=0.9, clear=0.026, thick=0.0075, skew=52.0, cols=36, front_rise=0.05, side_rise=0.07,
         tilt=0.10, tilt_phase=0.7, fold=0.030, fold_k=4.0, fold_z=-24.0,
         keys=[(1.452, 0.150, 0.105, 0.115, 22.0), (1.415, 0.225, 0.132, 0.125, 26.0),
               (1.365, 0.275, 0.152, 0.134, 30.0), (1.300, 0.305, 0.165, 0.144, 34.0),
               (1.230, 0.318, 0.170, 0.150, 38.0)]),
]
def hem_pattern(ncols, seed=23):
    """Drop (metres) of the torn lower edge per column: irregular, lopsided teeth of varied width and length."""
    rng = random.Random(seed)
    drops = []
    while len(drops) <= ncols:
        width = rng.choice((2, 3, 3, 4, 5, 7, 8))
        depth = 0.015 + rng.random() * rng.random() * 0.13
        if rng.random() < 0.15:
            depth += 0.07
            width = max(width, 4)
        peak = 0.2 + 0.6 * rng.random()
        base = 0.008 + rng.random() * 0.02
        for k in range(width):
            t = k / width
            tri = t / peak if t < peak else (1.0 - t) / (1.0 - peak)
            drops.append(base + depth * tri ** 1.3)
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
            # diagonal wrap: the whole layer tilts around the neck, and soft folds run diagonally across it
            zz += layer["tilt"] * math.cos(phi - layer["tilt_phase"]) * low
            fold = layer["fold"] * smoothstep(1.45, 1.38, z) * (
                math.sin(layer["fold_k"] * phi + layer["fold_z"] * z) + 0.3 * math.sin(1.7 * layer["fold_k"] * phi - 0.5 * layer["fold_z"] * z + 1.3))
            row.append((AXIS_X + (rx + fold) * math.sin(phi), AXIS_Y - (ry + fold) * c, zz))
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
        # smooth the radius down each column too (soft flowing folds, no spikes)
        radg = [[Vector((p[0] - c0.x, p[1] - c0.y, 0.0)).length for p in row] for row in grid]
        for k in range(1, len(grid) - 1):
            for i in range(len(grid[k])):
                rk = 0.25 * radg[k - 1][i] + 0.5 * radg[k][i] + 0.25 * radg[k + 1][i]
                d = Vector((grid[k][i][0] - c0.x, grid[k][i][1] - c0.y, 0.0))
                if d.length > 1e-6 and rk > radg[k][i] * 0.999:
                    dn = d.normalized()
                    grid[k][i] = (c0.x + dn.x * rk, c0.y + dn.y * rk, grid[k][i][2])
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
        grid = relax_from_body(grid, body_bvh, 0.024 + layer["clear"] + layer["thick"], below, 0.010)
        outer, inner = build_shell(b, grid, layer["thick"], centre, colour, cape_weights, cloth)
        rim_strips(b, outer, inner, cloth)
        below.append(layer_bvh(b, set(v for row in outer for v in row)))
    # sash: wrapped band at the waist plus a short hanging tail at the right back hip
    sash_first = len(b.verts)
    grid = relax_from_body(sash_points(), body_bvh, 0.012, (), 0.0)
    sash_w = lambda p: {"Hips": 0.55, "Abdomen": 0.45}
    outer, inner = build_shell(b, grid, 0.008, centre, lambda k, n, i, e: (0.0, 1.0, 0.0), sash_w, cloth)
    rim_strips(b, outer, inner, cloth, bottom=True, sides=False, top=True)
    legs_first = len(b.verts)
    skirt_keys = [(1.040, 0.130, 0.094, 0.090), (0.990, 0.142, 0.104, 0.098), (0.950, 0.156, 0.116, 0.106),
                  (0.918, 0.168, 0.126, 0.114)]
    hemd = hem_pattern(28, 77)
    sgrid = []
    for kk, (z, rx, ryf, ryb) in enumerate(refine_rings(skirt_keys, 1)):
        row = []
        for i in range(29):
            phi = 2.0 * math.pi * i / 28
            c = math.cos(phi)
            ry = ryf if c > 0.0 else ryb
            drop = hemd[i] * 0.35 if kk == len(skirt_keys) - 1 else 0.0
            row.append((AXIS_X + rx * math.sin(phi), AXIS_Y - ry * c, z - drop))
        sgrid.append(row)
    sgrid = relax_from_body(sgrid, body_bvh, 0.010 + 0.006, (), 0.0)
    so, si = build_shell(b, sgrid, 0.006, centre, lambda k, nn, i, e: (0.0, 1.0, 0.0),
                         lambda p: finalize({"Hips": 0.75, "Abdomen": 0.25 * smoothstep(0.95, 1.04, p.z) + 0.001}), cloth)
    rim_strips(b, so, si, cloth, bottom=True, sides=False, top=False)
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
    obj.data["legs_first_vertex"] = legs_first
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




# ---------------------------------------------------------------------------------------------- boots

BOOT_X = 0.083
BOOT_Y = -0.045


def bridge(b, r0, r1, mat, exp_fn):
    n = len(r0)
    for i in range(n):
        j = (i + 1) % n
        idx = (r0[i], r0[j], r1[j], r1[i])
        mid = sum((Vector(b.verts[q]) for q in idx), Vector()) / 4.0
        b.face(idx, mat, tuple(exp_fn(mid)))


def superellipse_ring(b, cx, y, w, zb, zt, n, e, rgb, wfn):
    ids = []
    for i in range(n):
        a = 2.0 * math.pi * i / n
        c, sn = math.cos(a), math.sin(a)
        x = cx + w * math.copysign(abs(c) ** (2.0 / e), c)
        z = zb + (zt - zb) * 0.5 * (1.0 + math.copysign(abs(sn) ** (2.0 / e), sn))
        p = (x, y, z)
        ids.append(b.vert(p, rgb, wfn(Vector(p))))
    return ids


def cap_fan(b, ring, mat, direction, rgb, wfn):
    pts = [Vector(b.verts[i]) for i in ring]
    c = sum(pts, Vector()) / len(pts)
    ci = b.vert(c, rgb, wfn(c))
    for i in range(len(ring)):
        b.face((ring[i], ring[(i + 1) % len(ring)], ci), mat, tuple(direction))


def build_boots(arm, slots, side):
    """One boot (rest pose): sole slab, leather upper with a toe cap, calf-height shaft and a folded cuff.
    slots = [KeeperLeather, KeeperSkin, Black]."""
    b = Builder()
    leather, dark = slots[0], slots[2]
    cx = side * BOOT_X
    rgb = (0.0, 1.0, 0.0)
    L = ".L" if side > 0 else ".R"

    def foot_w(p):
        t = smoothstep(0.02, -0.04, p.y)
        return finalize({"LowerLeg" + L: 0.0 + (1.0 - t) * 0.55, "Foot" + L: 0.45 + t * 0.55})

    def shaft_w(p):
        t = smoothstep(0.17, 0.12, p.z)
        return finalize({"LowerLeg" + L: 1.0 - t * 0.5, "Foot" + L: t * 0.5})

    # upper: rows heel -> toe
    rows = [(0.022, 0.044, 0.150), (0.004, 0.055, 0.130), (-0.040, 0.058, 0.098), (-0.090, 0.060, 0.074),
            (-0.140, 0.059, 0.062), (-0.190, 0.052, 0.054), (-0.226, 0.036, 0.042), (-0.240, 0.016, 0.034)]
    n = 16
    rings = [superellipse_ring(b, cx, y, w, 0.020, zt, n, 2.6, rgb, foot_w) for (y, w, zt) in rows]
    for r0, r1 in zip(rings, rings[1:]):
        bridge(b, r0, r1, leather, lambda m: (m.x - cx, 0.0, m.z - 0.07))
    cap_fan(b, rings[0], leather, (0.0, 1.0, 0.0), rgb, foot_w)
    cap_fan(b, rings[-1], leather, (0.0, -1.0, 0.0), rgb, foot_w)
    # toe cap: a slightly raised leather patch over the front of the foot, with a visible step
    cap_rows = [(-0.112, 0.062, 0.071), (-0.140, 0.062, 0.066), (-0.190, 0.056, 0.058), (-0.226, 0.040, 0.046),
                (-0.243, 0.018, 0.038)]
    crings = [superellipse_ring(b, cx, y, w, 0.019, zt, n, 2.6, rgb, foot_w) for (y, w, zt) in cap_rows]
    for r0, r1 in zip(crings, crings[1:]):
        bridge(b, r0, r1, leather, lambda m: (m.x - cx, 0.0, m.z - 0.07))
    cap_fan(b, crings[-1], leather, (0.0, -1.0, 0.0), rgb, foot_w)
    # step wall back to the upper at the cap's rear edge
    back = [superellipse_ring(b, cx, -0.112, 0.0575, 0.020, 0.0705, n, 2.6, rgb, foot_w)]
    bridge(b, crings[0], back[0], leather, lambda m: (0.0, 1.0, 0.0))
    # sole slab
    srows = [(0.030, 0.050), (0.006, 0.062), (-0.060, 0.067), (-0.140, 0.066), (-0.205, 0.057), (-0.238, 0.040),
             (-0.250, 0.016)]
    srings = [superellipse_ring(b, cx, y, w, 0.0, 0.026, n, 4.0, rgb, foot_w) for (y, w) in srows]
    for r0, r1 in zip(srings, srings[1:]):
        bridge(b, r0, r1, dark, lambda m: (m.x - cx, 0.0, m.z - 0.013))
    cap_fan(b, srings[0], dark, (0.0, 1.0, 0.0), rgb, foot_w)
    cap_fan(b, srings[-1], dark, (0.0, -1.0, 0.0), rgb, foot_w)
    # shaft: ankle to calf, circular
    cy = BOOT_Y
    shaft = [(0.07, 0.049), (0.13, 0.050), (0.20, 0.050), (0.30, 0.056), (0.38, 0.060), (0.405, 0.061)]
    sr = []
    for z, r in shaft:
        ids = []
        for i in range(n):
            a = 2.0 * math.pi * i / n
            p = (cx + r * math.cos(a) * 1.0, cy + r * math.sin(a) * 1.05, z)
            ids.append(b.vert(p, rgb, shaft_w(Vector(p))))
        sr.append(ids)
    for r0, r1 in zip(sr, sr[1:]):
        bridge(b, r0, r1, leather, lambda m: (m.x - cx, m.y - cy, 0.0))
    # strap ring with a buckle around the shaft
    strap_prof = [(0.285, 0.0585), (0.291, 0.0625), (0.313, 0.0625), (0.319, 0.0585)]
    sp = []
    for z, r in strap_prof:
        ids = []
        for i in range(n):
            a = 2.0 * math.pi * i / n
            ids.append(b.vert((cx + r * math.cos(a), cy + r * math.sin(a) * 1.05, z), rgb, {"LowerLeg" + L: 1.0}))
        sp.append(ids)
    for k in range(len(sp)):
        bridge(b, sp[k], sp[(k + 1) % len(sp)], leather,
               lambda m: (Vector((m.x - cx, m.y - cy, 0.0)).normalized() * (Vector((m.x - cx, m.y - cy, 0.0)).length - 0.056)
                          + Vector((0.0, 0.0, m.z - 0.302))))
    box(b, (cx, cy - 0.0665, 0.302), ((1, 0, 0), (0, 1, 0), (0, 0, 1)), (0.011, 0.0035, 0.015), dark, rgb, {"LowerLeg" + L: 1.0})
    # stacked heel
    hrows = [(0.038, 0.040), (0.016, 0.054), (-0.030, 0.054), (-0.050, 0.040)]
    hrings = [superellipse_ring(b, cx, y, w, 0.024, 0.052, n, 3.0, rgb, foot_w) for (y, w) in hrows]
    for r0, r1 in zip(hrings, hrings[1:]):
        bridge(b, r0, r1, dark, lambda m: (m.x - cx, 0.0, m.z - 0.038))
    cap_fan(b, hrings[0], dark, (0.0, 1.0, 0.0), rgb, foot_w)
    cap_fan(b, hrings[-1], dark, (0.0, -1.0, 0.0), rgb, foot_w)
    # folded cuff: a closed rolled profile (z, r) revolved around the calf
    prof = [(0.392, 0.062), (0.405, 0.074), (0.435, 0.078), (0.462, 0.071), (0.466, 0.062), (0.450, 0.058),
            (0.420, 0.060)]
    pr = []
    for z, r in prof:
        ids = []
        for i in range(n):
            a = 2.0 * math.pi * i / n
            p = (cx + r * math.cos(a), cy + r * math.sin(a) * 1.05, z)
            ids.append(b.vert(p, rgb, {"LowerLeg" + L: 1.0}))
        pr.append(ids)
    zc, rc = 0.43, 0.068
    for k in range(len(pr)):
        bridge(b, pr[k], pr[(k + 1) % len(pr)], leather,
               lambda m: (Vector((m.x - cx, m.y - cy, 0.0)).normalized() * (Vector((m.x - cx, m.y - cy, 0.0)).length - rc)
                          + Vector((0.0, 0.0, m.z - zc))))
    return b


def build_boots_both(arm, slots):
    objs = []
    for side in (1, -1):
        b = build_boots(arm, slots, side)
        objs.append(b.make_object("Boot_tmp_%d" % side, list(slots), arm, mats=None, sharp_deg=50.0))
    return objs

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
        samples = [(-0.064, 0.0425), (-0.060, 0.0445), (-0.057, 0.0405), (-0.054, 0.0445), (-0.040, 0.0450), (-0.022, 0.0485),
                   (-0.008, 0.0520), (0.002, 0.0535), (0.006, 0.0510), (0.009, 0.0545), (0.013, 0.0545)]
        for t, r in samples:
            row = []
            for i in range(17):
                a = 2.0 * math.pi * i / 16
                row.append(tuple(w + d * t + u * (math.cos(a) * r) + v * (math.sin(a) * r)))
            rows.append(row)

        def colour(k, nrr, inner, edge):
            return (0.0, 0.25 if k in (2, 8) else 1.0, 0.0)        # dark seam grooves

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
    return body

# ---------------------------------------------------------------------------------------------- main

def tri_total():
    total = {}
    for o in bpy.data.objects:
        if o.type == 'MESH' and o.name.endswith("_LOD0"):
            total[o.name] = tri_count(o.data)
    return total


def subdivide_soft(obj, crease=0.7, sharp_deg=55.0):
    """One Catmull-Clark level with rim creases (thin cloth edge stays defined), then smooth shading."""
    me = obj.data
    mark_creases(me, 60.0, material_edges=False)
    attr = me.attributes["crease_edge"]
    vals = [0.0] * len(me.edges)
    attr.data.foreach_get("value", vals)
    attr.data.foreach_set("value", [v * crease for v in vals])
    apply_subsurf(obj, 1)
    if "crease_edge" in obj.data.attributes:
        obj.data.attributes.remove(obj.data.attributes["crease_edge"])
    shade_smooth_with_sharp(obj.data, sharp_deg, material_edges=False)


def limit_influences(objs, limit=4):
    """Cap every vertex at `limit` bone influences and renormalise (subdivision, welding and decimation blend
    weights and can exceed 4). Prints the maximum influences per mesh."""
    for o in objs:
        for v in bpy.context.view_layer.objects:
            v.select_set(False)
        o.select_set(True)
        with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o], selected_editable_objects=[o]):
            bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=limit)
            bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
        top = max((sum(1 for g in v.groups if g.weight > 1e-5) for v in o.data.vertices), default=0)
        print("INFLUENCES %-14s max %d" % (o.name, top))


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

    remove_objects(LOD0_NAMES + [n[:-1] + "1" for n in LOD0_NAMES])      # stale LOD1 meshes too
    body = build_body(arm, slot_index, group_names)
    # old objects go after the body is built (build_part reads their vertex groups)
    remove_objects(OLD_OBJECTS)

    set_pose(arm, "Idle", 1)
    mats = skin_matrices(arm)
    rest = [v.co.copy() for v in body.data.vertices]
    thicken_sleeves(body, arm)
    full = [v.co.copy() for v in body.data.vertices]
    for v, r, f in zip(body.data.vertices, rest, full):
        v.co = r.lerp(f, COWL_FIT)       # the cowl is fitted over part of the thickening, so it clears the sleeves
    body.data.update()
    bpy.context.view_layer.update()
    body_bvh = evaluated_bvh(body)
    for v, f in zip(body.data.vertices, full):
        v.co = f
    body.data.update()
    cuffs = build_cuffs(arm, [leather, skin, black], mats)
    boots = build_boots_both(arm, [leather, skin, black])
    join_into(body, [cuffs] + boots)
    hood, hb = build_hood(arm, cloth, mats)
    cape, cb = build_cape(arm, cloth, body_bvh, mats)
    satchel, sb = build_satchel(arm, leather, mats)
    bpy.context.view_layer.update()
    for o, bb in ((hood, hb), (cape, cb), (satchel, sb)):
        verify_fit(o, bb)
    # the unsubdivided cape stays as a hidden-from-render collision proxy for check_clipping.py (not exported)
    remove_objects(["Cape_base"])
    base = cape.copy()
    base.data = cape.data.copy()
    base.name = "Cape_base"
    base.data.name = "Cape_base"
    bpy.context.scene.collection.objects.link(base)
    base.hide_render = True
    subdivide_soft(cape)
    subdivide_soft(hood)
    limit_influences([body, cape, hood, satchel])
    reset_pose(arm)
    tris = tri_total()
    print("TRIS", tris)
    print("TRIS LOD0 total", sum(tris.values()))
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


if __name__ == "__main__":
    main()
