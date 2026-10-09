"""Re-author the LanternHold action: right arm nearly straight, held out at arm's length.

Run:  blender.exe -b ArtSource/Keeper/Keeper.blend -P ArtSource/Keeper/author_hold.py

Replaces CharacterArmature|LanternHold (same name, same two identical keys on frames 1 and 2, same bones) and
re-seats HandSocket in the new fist (still parented to Wrist.R, +Y up the fist, so the lantern hangs down).

This module is the single home of the rig helpers (pose/aim/curl, keying) and of the hold authoring:
export_keeper.py imports it for its rebuild path, so there is one definition. Importing it runs nothing.
"""
import math
import os

import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(HERE, "Keeper.blend")
HAND_BONE = "Wrist.R"
SOCKET_NAME = "HandSocket"
CLIP_PREFIX = "CharacterArmature|"


def get_armature():
    return [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]


def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


def update():
    bpy.context.view_layer.update()


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

# Fist target relative to the right shoulder joint (world: +X left, -Y forward, Z up). The arm is 0.42 m long,
# so this is about as far as a nearly straight arm reaches from this height.
FIST_OFFSET = Vector((-0.25, -0.40, -0.29))
HAND_LEN = 0.07        # wrist joint to fist centre
HAND_TILT_DEG = 12.0   # hand direction off the forearm axis (towards the ground)
HAND_ROLL_DEG = 35.0   # roll about the forearm: curled fingers face inward, knuckles outward


def author_lantern_hold(arm):
    reset_pose(arm)
    old = bpy.data.actions.get(CLIP_PREFIX + "LanternHold")
    if old is not None:
        bpy.data.actions.remove(old)
    action = new_action(arm, CLIP_PREFIX + "LanternHold")
    shoulder = bone_world_head(arm, "UpperArm.R")
    elbow = bone_world_head(arm, "LowerArm.R")
    wrist = bone_world_head(arm, HAND_BONE)
    fist = shoulder + FIST_OFFSET
    # The wrist stays close to inline with the forearm (HAND_TILT_DEG off it, towards the ground) so the hand reads
    # as a fist hanging the lantern rather than a cocked wrist. Two passes: the forearm direction depends on the
    # wrist target, which depends on the hand direction.
    hand_dir = (fist - shoulder).normalized()
    for _ in range(3):
        wrist_target = fist - hand_dir * HAND_LEN
        new_elbow, new_wrist = two_bone_targets(shoulder, elbow, wrist, wrist_target, Vector((-0.4, 0.3, -1.0)))
        fore_dir = (new_wrist - new_elbow).normalized()
        side = fore_dir.cross(Vector((0.0, 0.0, -1.0))).normalized()
        hand_dir = (Quaternion(side, math.radians(HAND_TILT_DEG)) @ fore_dir).normalized()
    curl_hand(arm)
    aim(arm, "UpperArm.R", "LowerArm.R", new_elbow - shoulder)
    aim(arm, "LowerArm.R", HAND_BONE, new_wrist - bone_world_head(arm, "LowerArm.R"))
    aim(arm, HAND_BONE, "Middle1.R", hand_dir)
    forearm = (bone_world_head(arm, HAND_BONE) - bone_world_head(arm, "LowerArm.R")).normalized()
    rotate_about_head(arm, HAND_BONE, forearm, math.radians(HAND_ROLL_DEG))
    for frame in (1, 2):
        key_bones(arm, ARM_BONES, frame)
    update()
    return action


def grip_center(arm):
    pts = [bone_world_head(arm, n) for n in ("Middle1.R", "Middle2.R", "Index2.R", "Ring2.R", "Pinky2.R")]
    return sum(pts, Vector()) / len(pts)


def seat_socket(arm):
    """Place HandSocket in the fist (creating it, parented to the hand bone, when missing). Call in the hold pose:
    +Y up the fist (world up), so the lantern hangs down."""
    empty = bpy.data.objects.get(SOCKET_NAME)
    if empty is None:
        empty = bpy.data.objects.new(SOCKET_NAME, None)
        empty.empty_display_type = 'ARROWS'
        empty.empty_display_size = 0.05
        bpy.context.scene.collection.objects.link(empty)
        empty.parent = arm
        empty.parent_type = 'BONE'
        empty.parent_bone = HAND_BONE
        update()
    center = grip_center(arm)
    up = Vector((0.0, 0.0, 1.0))
    wrist_pb = arm.pose.bones[HAND_BONE]
    fore = (arm.matrix_world @ wrist_pb.matrix).to_3x3() @ Vector((0.0, 1.0, 0.0))
    z_axis = fore.cross(up).normalized() if abs(fore.normalized().dot(up)) < 0.99 else Vector((1.0, 0.0, 0.0))
    x_axis = up.cross(z_axis).normalized()
    rot = Matrix((x_axis, up, z_axis)).transposed()
    empty.matrix_world = Matrix.Translation(center) @ rot.to_4x4()
    update()
    return center


def main():
    arm = get_armature()
    author_lantern_hold(arm)
    center = seat_socket(arm)
    print("Fist centre", tuple(round(c, 3) for c in center), "shoulder", tuple(round(c, 3) for c in bone_world_head(arm, "UpperArm.R")))
    print("Elbow bend: upper-lower angle deg", round(math.degrees((bone_world_head(arm, "LowerArm.R") - bone_world_head(arm, "UpperArm.R")).angle(bone_world_head(arm, HAND_BONE) - bone_world_head(arm, "LowerArm.R"))), 1))
    reset_pose(arm)
    arm.animation_data.action = None
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


if __name__ == "__main__":
    main()
