"""Rebuild LOD1/LOD2 by thinning leaf cards when collapse decimation stalls."""
import os
import sys

import bmesh
import bpy
from mathutils import Vector

ROOT = r"C:\Hemant\Prj\Learning\Unity3DPrj"
OUT_ROOT = os.path.join(ROOT, "Assets", "Game", "Models", "PolyHaven")
FOLIAGE = ("leaf", "twig", "needle", "flower", "petal")


def argv_ids():
    if "--" in sys.argv:
        return sys.argv[sys.argv.index("--") + 1 :]
    return ["tree_small_02", "jacaranda_tree"]


def tri_count(obj):
    return sum(max(len(poly.vertices) - 2, 0) for poly in obj.data.polygons)


def is_foliage(obj, poly):
    if not obj.material_slots:
        return True
    material = obj.material_slots[poly.material_index].material
    name = material.name.lower() if material else ""
    return any(key in name for key in FOLIAGE)


def thin_to(obj, target):
    current = tri_count(obj)
    if current <= int(target * 1.08):
        return current
    foliage = 0
    branch = 0
    trunk = 0
    for poly in obj.data.polygons:
        tris = max(len(poly.vertices) - 2, 1)
        name = ""
        if obj.material_slots and obj.material_slots[poly.material_index].material:
            name = obj.material_slots[poly.material_index].material.name.lower()
        if any(key in name for key in FOLIAGE):
            foliage += tris
        elif "trunk" in name or "bark" in name:
            trunk += tris
        else:
            branch += tris
    print("  parts", obj.name, "leaf", foliage, "branch", branch, "trunk", trunk)
    need = current - target
    foliage_keep = 1.0
    branch_keep = 1.0
    trunk_keep = 1.0
    if foliage > 0 and need > 0:
        drop = min(foliage * 0.96, need)
        foliage_keep = 1.0 - drop / float(foliage)
        need -= drop
    if branch > 0 and need > 0:
        drop = min(branch * 0.98, need)
        branch_keep = 1.0 - drop / float(branch)
        need -= drop
    if trunk > 0 and need > 0:
        drop = min(trunk * 0.7, need)
        trunk_keep = 1.0 - drop / float(trunk)
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    doomed = []
    for face in bm.faces:
        name = ""
        if obj.material_slots and obj.material_slots[face.material_index].material:
            name = obj.material_slots[face.material_index].material.name.lower()
        if any(key in name for key in FOLIAGE):
            keep = foliage_keep
        elif "trunk" in name or "bark" in name:
            keep = trunk_keep
        else:
            keep = branch_keep
        if ((face.index * 47) % 1000) / 1000.0 > keep:
            doomed.append(face)
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    print("  thin", obj.name, current, "->", tri_count(obj), "target", target, "leaf_keep", round(foliage_keep, 3))
    return tri_count(obj)


def duplicate(obj, name):
    copy = obj.copy()
    copy.data = obj.data.copy()
    copy.name = name
    copy.data.name = name
    bpy.context.collection.objects.link(copy)
    return copy


def ground(obj):
    zs = [vertex.co.z for vertex in obj.data.vertices]
    xs = [vertex.co.x for vertex in obj.data.vertices]
    ys = [vertex.co.y for vertex in obj.data.vertices]
    if not zs:
        return
    shift = Vector((-0.5 * (min(xs) + max(xs)), -0.5 * (min(ys) + max(ys)), -min(zs)))
    for vertex in obj.data.vertices:
        vertex.co += shift
    obj.data.update()


def process(asset):
    print("====", asset, "====")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    path = os.path.join(OUT_ROOT, asset, asset + ".fbx")
    bpy.ops.import_scene.fbx(filepath=path)
    hero = None
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH":
            continue
        if obj.name.startswith(asset + "_hero"):
            hero = obj
            print("  hero", obj.name, tri_count(obj), "slots", [slot.name for slot in obj.material_slots])
        else:
            bpy.data.objects.remove(obj, do_unlink=True)
    if hero is None:
        raise RuntimeError("missing hero " + asset)
    bpy.ops.object.select_all(action="DESELECT")
    hero.select_set(True)
    bpy.context.view_layer.objects.active = hero
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    zs = [vertex.co.z for vertex in hero.data.vertices]
    height = max(zs) - min(zs) if zs else 0
    print("  imported height", round(height, 3))
    if height < 1.0:
        for vertex in hero.data.vertices:
            vertex.co *= 100.0
        hero.data.update()
    ground(hero)
    lod0 = duplicate(hero, asset + "_LOD0")
    thin_to(lod0, 60000)
    lod1 = duplicate(hero, asset + "_LOD1")
    thin_to(lod1, 21000)
    lod2 = duplicate(hero, asset + "_LOD2")
    thin_to(lod2, 6000)
    exported = [hero, lod0, lod1, lod2]
    print(" ", [(obj.name, tri_count(obj)) for obj in exported])
    bpy.ops.object.select_all(action="DESELECT")
    for obj in exported:
        if obj.name in bpy.data.objects:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = exported[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_NONE",
        apply_unit_scale=True,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
    )
    print("EXPORTED", path)


def main():
    for asset in argv_ids():
        process(asset)
    print("THIN_DONE")


if __name__ == "__main__":
    main()
