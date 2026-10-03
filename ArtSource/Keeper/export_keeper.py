"""Keeper true-scale build and export.

Run:  blender.exe -b -P ArtSource/Keeper/export_keeper.py

Stage 1 (build) runs only when ArtSource/Keeper/Keeper.blend does not exist. It imports the original
Quaternius-derived FBX (set KEEPER_SOURCE_FBX to its path; defaults to the FBX already in Assets),
bakes the armature scale of ~93 into the bones, scales the pose bone location curves by the same factor,
adds the HandSocket empty and the LanternHold and Lean actions, and saves Keeper.blend.
Stage 2 (export) always runs and writes Assets/Game/Models/Keeper/Keeper.fbx from Keeper.blend.

The FBX axis and bone settings must stay in step with the importer defaults: Y up, -Z forward,
primary bone axis Y, secondary bone axis X, and Automatic Bone Orientation off. The keeper clip guard
test (KeeperClipTests) fails when the local bone rotations change.
"""
import math
import os

import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
BLEND_PATH = os.path.join(HERE, "Keeper.blend")
FBX_PATH = os.path.join(PROJECT, "Assets", "Game", "Models", "Keeper", "Keeper.fbx")
SOURCE_FBX = os.environ.get("KEEPER_SOURCE_FBX", FBX_PATH)

HAND_BONE = "Wrist.R"
SOCKET_NAME = "HandSocket"
CLIP_PREFIX = "CharacterArmature|"


def get_armature():
    return [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]


def action_fcurves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    yield fc


def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


def update():
    bpy.context.view_layer.update()


def bake_scale(arm):
    factor = arm.scale[0]
    arm.animation_data.action = None
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.data.objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    # Pose bone translations are in bone space, so they scale with the rest offsets.
    for action in bpy.data.actions:
        for fc in action_fcurves(action):
            if fc.data_path == "scale":
                # The source clips also animate the armature object scale at ~93. That is baked into the bones now.
                for kp in fc.keyframe_points:
                    kp.co[1] /= factor
                    kp.handle_left[1] /= factor
                    kp.handle_right[1] /= factor
                fc.update()
            elif fc.data_path.endswith(".location") and fc.data_path.startswith("pose.bones"):
                for kp in fc.keyframe_points:
                    kp.co[1] *= factor
                    kp.handle_left[1] *= factor
                    kp.handle_right[1] *= factor
                fc.update()
    return factor


def tidy_bone_lengths(arm):
    # The importer gives every bone the same long default length. Real lengths make the previews readable
    # and keep bone-parented objects close to the bone. Directions and heads stay unchanged.
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    for eb in arm.data.edit_bones:
        if eb.children:
            dist = (eb.children[0].head - eb.head).length
            eb.length = min(0.35, max(0.03, dist)) if dist > 0.001 else 0.05
        else:
            eb.length = 0.06
    bpy.ops.object.mode_set(mode='OBJECT')


def rename_actions():
    for action in bpy.data.actions:
        parts = action.name.split("|")
        action.name = CLIP_PREFIX + parts[-1]
        action.use_fake_user = True


def bone_world_head(arm, name):
    return arm.matrix_world @ arm.pose.bones[name].head


def rotate_about_head(arm, name, axis, angle):
    pb = arm.pose.bones[name]
    mw = arm.matrix_world
    m = mw @ pb.matrix
    head = m.translation.copy()
    rot = Quaternion(axis, angle).to_matrix().to_4x4()
    m2 = Matrix.Translation(head) @ rot @ Matrix.Translation(-head) @ m
    pb.matrix = mw.inverted() @ m2
    update()


def aim(arm, name, child, target_dir):
    pb = arm.pose.bones[name]
    cur = (bone_world_head(arm, child) - bone_world_head(arm, name)).normalized()
    diff = cur.rotation_difference(target_dir.normalized())
    axis, angle = diff.to_axis_angle()
    rotate_about_head(arm, name, axis, angle)


def two_bone_targets(shoulder, elbow, wrist, target, hint):
    l1 = (elbow - shoulder).length
    l2 = (wrist - elbow).length
    to_target = target - shoulder
    dist = min(to_target.length, (l1 + l2) * 0.995)
    axis = to_target.normalized()
    x = (dist * dist + l1 * l1 - l2 * l2) / (2.0 * dist)
    height = math.sqrt(max(0.0, l1 * l1 - x * x))
    pole = hint - axis * hint.dot(axis)
    pole.normalize()
    new_elbow = shoulder + axis * x + pole * height
    new_wrist = shoulder + axis * dist
    return new_elbow, new_wrist


def key_bones(arm, names, frame):
    for n in names:
        arm.pose.bones[n].keyframe_insert("rotation_quaternion", frame=frame)


def new_action(arm, name):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    arm.animation_data.action = action
    return action


FINGERS = ["Index", "Middle", "Ring", "Pinky"]


def curl_hand(arm):
    # Rest pose: right hand points along -X with the palm facing down. Curl towards the palm.
    down = Vector((0.0, 0.0, -1.0))
    mw3 = arm.matrix_world.to_3x3()
    plan = {1: math.radians(70.0), 2: math.radians(80.0), 3: math.radians(55.0)}
    for f in FINGERS:
        for seg, angle in plan.items():
            name = "%s%d.R" % (f, seg)
            bone = arm.data.bones[name]
            direction = mw3 @ bone.matrix_local.to_3x3() @ Vector((0.0, 1.0, 0.0))
            axis_world = direction.normalized().cross(down).normalized()
            local = (mw3 @ bone.matrix_local.to_3x3()).inverted() @ axis_world
            arm.pose.bones[name].rotation_quaternion = Quaternion(local.normalized(), angle)
    for seg, angle in {1: 25.0, 2: 35.0, 3: 30.0}.items():
        name = "Thumb%d.R" % seg
        bone = arm.data.bones[name]
        direction = mw3 @ bone.matrix_local.to_3x3() @ Vector((0.0, 1.0, 0.0))
        axis_world = direction.normalized().cross(down).normalized()
        local = (mw3 @ bone.matrix_local.to_3x3()).inverted() @ axis_world
        arm.pose.bones[name].rotation_quaternion = Quaternion(local.normalized(), math.radians(angle))
    update()


ARM_BONES = ["Shoulder.R", "UpperArm.R", "LowerArm.R", "Wrist.R"] + [
    "%s%d.R" % (f, i) for f in FINGERS for i in range(1, 5)] + ["Thumb1.R", "Thumb2.R", "Thumb3.R"]
SPINE_BONES = ["Abdomen", "Torso", "Chest", "Neck", "Head"]


def author_lantern_hold(arm):
    reset_pose(arm)
    action = new_action(arm, CLIP_PREFIX + "LanternHold")
    forward = Vector((0.0, -1.0, 0.0))
    hips = bone_world_head(arm, "Hips")
    shoulder = bone_world_head(arm, "UpperArm.R")
    elbow = bone_world_head(arm, "LowerArm.R")
    wrist = bone_world_head(arm, HAND_BONE)
    fist = Vector((hips.x - 0.20, hips.y + forward.y * 0.33, 1.02))
    hand_dir = Vector((0.0, -0.85, -0.45)).normalized()
    wrist_target = fist - hand_dir * 0.07
    new_elbow, new_wrist = two_bone_targets(shoulder, elbow, wrist, wrist_target, Vector((-0.5, 0.2, -1.0)))
    curl_hand(arm)
    aim(arm, "UpperArm.R", "LowerArm.R", new_elbow - shoulder)
    aim(arm, "LowerArm.R", HAND_BONE, new_wrist - bone_world_head(arm, "LowerArm.R"))
    aim(arm, HAND_BONE, "Middle1.R", hand_dir)
    # Roll the hand so the thumb is on top and the fist axis is vertical.
    forearm = (bone_world_head(arm, HAND_BONE) - bone_world_head(arm, "LowerArm.R")).normalized()
    rotate_about_head(arm, HAND_BONE, forearm, math.radians(90.0))
    for frame in (1, 2):
        key_bones(arm, ARM_BONES, frame)
    update()
    return action


def grip_center(arm):
    pts = [bone_world_head(arm, n) for n in ("Middle1.R", "Middle2.R", "Index2.R", "Ring2.R", "Pinky2.R")]
    center = sum(pts, Vector()) / len(pts)
    return center


def add_socket(arm):
    # Called with the arm in the LanternHold pose, so the socket sits inside the closed fist.
    center = grip_center(arm)
    empty = bpy.data.objects.new(SOCKET_NAME, None)
    empty.empty_display_type = 'ARROWS'
    empty.empty_display_size = 0.05
    bpy.context.scene.collection.objects.link(empty)
    empty.parent = arm
    empty.parent_type = 'BONE'
    empty.parent_bone = HAND_BONE
    update()
    # +Y up the fist (world up in this pose), X along the forearm's right side.
    up = Vector((0.0, 0.0, 1.0))
    wrist_pb = arm.pose.bones[HAND_BONE]
    fore = (arm.matrix_world @ wrist_pb.matrix).to_3x3() @ Vector((0.0, 1.0, 0.0))
    z_axis = fore.cross(up).normalized() if abs(fore.normalized().dot(up)) < 0.99 else Vector((1.0, 0.0, 0.0))
    x_axis = up.cross(z_axis).normalized()
    rot = Matrix((x_axis, up, z_axis)).transposed()
    desired = Matrix.Translation(center) @ rot.to_4x4()
    empty.matrix_world = desired
    update()
    return empty


def author_lean(arm):
    reset_pose(arm)
    action = new_action(arm, CLIP_PREFIX + "Lean")
    key_bones(arm, SPINE_BONES, 0)
    bend = {"Abdomen": 4.0, "Torso": 5.0, "Chest": 3.0, "Neck": 2.0, "Head": 4.0}
    # Forward is -Y, so +X rotation moves the top of the spine forward.
    for name, deg in bend.items():
        rotate_about_head(arm, name, Vector((1.0, 0.0, 0.0)), math.radians(deg))
    for frame in (1, 2):
        key_bones(arm, SPINE_BONES, frame)
    reset_pose(arm)
    return action


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SOURCE_FBX, use_anim=True, automatic_bone_orientation=False)
    arm = get_armature()
    bpy.context.scene.render.fps = 24
    arm.animation_data.action = None
    factor = bake_scale(arm)
    print("Baked armature scale", factor, "-> now", tuple(arm.scale))
    tidy_bone_lengths(arm)
    rename_actions()
    reset_pose(arm)
    author_lean(arm)
    author_lantern_hold(arm)
    add_socket(arm)
    reset_pose(arm)
    arm.animation_data.action = None
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


def export():
    arm = get_armature()
    arm.animation_data.action = None
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=False,
        object_types={'ARMATURE', 'MESH', 'EMPTY'},
        apply_scale_options='FBX_SCALE_UNITS',
        global_scale=1.0,
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=False,
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        primary_bone_axis='Y',
        secondary_bone_axis='X',
        armature_nodetype='NULL',
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode='AUTO',
        embed_textures=False,
    )
    print("Exported", FBX_PATH)


if os.path.exists(BLEND_PATH):
    bpy.ops.wm.open_mainfile(filepath=BLEND_PATH)
else:
    build()
export()
