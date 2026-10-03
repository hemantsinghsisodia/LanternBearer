"""Keeper true-scale build and export.

Run:  blender.exe -b -P ArtSource/Keeper/export_keeper.py

Stage 1 (build) runs only when ArtSource/Keeper/Keeper.blend does not exist. It imports the original
Quaternius-derived FBX (set KEEPER_SOURCE_FBX to its path; defaults to the FBX already in Assets),
bakes the armature scale of ~93 into the bones, scales the pose bone location curves by the same factor,
then runs the rest of the pipeline in this order:
  1. bake scale (bake_scale, tidy_bone_lengths, rename_actions)
  2. author the Lean action (here) and the LanternHold action plus the HandSocket (author_hold.py)
  3. build_outfit.py: cull the body, build Hood, Cloak and Satchel, save Keeper.blend
  4. export (below)
Stage 2 (export) always runs and writes Assets/Game/Models/Keeper/Keeper.fbx from Keeper.blend.
For a real rebuild KEEPER_SOURCE_FBX must point at the ORIGINAL Quaternius-derived FBX (the Keeper.fbx from git
history before the true-scale commit); the FBX now in Assets already has the outfit and the scale baked in.
To change only the hold or the outfit, open Keeper.blend and run author_hold.py or build_outfit.py on its own.

The FBX axis and bone settings must stay in step with the importer defaults: Y up, -Z forward,
primary bone axis Y, secondary bone axis X, and Automatic Bone Orientation off. The keeper clip guard
test (KeeperClipTests) fails when the local bone rotations change.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import author_hold
from author_hold import (CLIP_PREFIX, get_armature, key_bones, new_action, reset_pose, rotate_about_head)

PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
BLEND_PATH = os.path.join(HERE, "Keeper.blend")
FBX_PATH = os.path.join(PROJECT, "Assets", "Game", "Models", "Keeper", "Keeper.fbx")
SOURCE_FBX = os.environ.get("KEEPER_SOURCE_FBX", FBX_PATH)



def action_fcurves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    yield fc


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
    author_hold.author_lantern_hold(arm)
    author_hold.seat_socket(arm)
    reset_pose(arm)
    arm.animation_data.action = None
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    import build_outfit
    build_outfit.main()


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
        # Vertex colours carry data (R sway, G AO, B worn edge), so keep them linear. The default SRGB would curve R/G/B.
        colors_type='LINEAR',
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


def main():
    if os.path.exists(BLEND_PATH):
        bpy.ops.wm.open_mainfile(filepath=BLEND_PATH)
    else:
        build()
    export()


if __name__ == "__main__":
    main()
