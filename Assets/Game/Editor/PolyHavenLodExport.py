"""Decimate Poly Haven FBX downloads and write Unity-ready FBX plus textures.

Raw packs live in _ph_raw/<id>/<id>_1k.fbx. Output lands in
Assets/Game/Models/PolyHaven/<id>/ for PolyHavenImporter.
"""
import os
import re
import sys
import traceback

import bpy
from mathutils import Vector

ROOT = r"C:\Hemant\Prj\Learning\Unity3DPrj"
RAW = os.path.join(ROOT, "_ph_raw")
OUT_ROOT = os.path.join(ROOT, "Assets", "Game", "Models", "PolyHaven")

# height: scale so the tallest mesh is this many metres (Blender Z). None keeps authored size.
# longest: scale so the longest axis of a single mesh is this many metres.
SPECS = {
    "jacaranda_tree": {"height": 9.1, "hero": True},
    "tree_small_02": {"height": 9.0, "hero": True},
    "island_tree_02": {"height": 9.0, "hero": True},
    "fir_sapling_medium": {"height": 4.0, "hero": False},
    "shrub_01": {"height": 0.95, "hero": False},
    "shrub_03": {"height": 0.85, "hero": False},
    "nettle_plant": {"height": 0.6, "hero": False},
    "anthurium_botany_01": {"height": None, "hero": False},
    "periwinkle_plant": {"height": None, "hero": False},
    "weed_plant_02": {"height": 0.3, "hero": False},
    "rock_07": {"longest": 0.9, "hero": False},
    "rock_09": {"longest": 0.8, "hero": False},
    "boulder_01": {"longest": None, "hero": False},
}

RENAME = {
    "anthurium_botany_04_d": "anthurium_botany_01_d",
    "anthurium_botany_05_e": "anthurium_botany_01_e",
    "anthurium_botany_06_f": "anthurium_botany_01_f",
}

HEAVY = 150000
LOD0_TRIS = 60000
HERO_TRIS = 240000


def argv_ids():
    if "--" in sys.argv:
        return sys.argv[sys.argv.index("--") + 1 :]
    return list(SPECS.keys())


def wipe():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.armatures):
        for item in list(collection):
            if item.users == 0:
                collection.remove(item)


def tri_count(obj):
    mesh = obj.data
    return sum(max(len(poly.vertices) - 2, 0) for poly in mesh.polygons)


def lod_index(name):
    match = re.search(r"_LOD(\d+)$", name)
    return int(match.group(1)) if match else None


def base_name(name):
    name = re.sub(r"\.\d+$", "", name)
    stripped = re.sub(r"_LOD\d+$", "", name)
    return RENAME.get(stripped, stripped)


def local_bounds(obj):
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    if not xs:
        return Vector((0, 0, 0)), Vector((0, 0, 0))
    return Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))


def bake(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def reshape(obj, scale, shift):
    mesh = obj.data
    for vertex in mesh.vertices:
        vertex.co = (vertex.co + shift) * scale
    mesh.update()


def duplicate(obj, name):
    copy = obj.copy()
    copy.data = obj.data.copy()
    copy.name = name
    copy.data.name = name
    bpy.context.collection.objects.link(copy)
    return copy


def decimate_to(obj, target):
    current = tri_count(obj)
    if current <= target * 1.08:
        return current
    guard = 0
    while current > target * 1.08 and guard < 4:
        ratio = max(0.0004, min(0.92, (target / float(current)) * 0.96))
        mod = obj.modifiers.new("Decimate", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = ratio
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        before = current
        try:
            with bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj]):
                bpy.ops.object.modifier_apply(modifier=mod.name)
        except Exception as exc:
            print("  modifier apply failed", exc)
        current = tri_count(obj)
        if current > before * 0.8:
            if obj.modifiers.get("Decimate"):
                obj.modifiers.remove(obj.modifiers["Decimate"])
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.select_all(action="SELECT")
            bpy.ops.mesh.decimate(ratio=ratio)
            bpy.ops.object.mode_set(mode="OBJECT")
            current = tri_count(obj)
        guard += 1
        print("  decimate", obj.name, "ratio", round(ratio, 4), "tris", current, "target", target)
    return current


def ensure_dir(path):
    os.makedirs(path, exist_ok=True)


def image_pixels(path, noncolor):
    image = bpy.data.images.load(path, check_existing=False)
    if noncolor:
        image.colorspace_settings.name = "Non-Color"
    image.pixels[0]
    width, height = image.size[0], image.size[1]
    buf = [0.0] * (width * height * 4)
    image.pixels.foreach_get(buf)
    bpy.data.images.remove(image)
    return width, height, buf


def save_png(path, width, height, pixels, noncolor):
    image = bpy.data.images.new(os.path.basename(path), width, height, alpha=True)
    if noncolor:
        image.colorspace_settings.name = "Non-Color"
    image.pixels.foreach_set(pixels)
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def texture_groups(folder):
    groups = {}
    if not os.path.isdir(folder):
        return groups
    tokens = (
        ("_diff_", "diff"),
        ("_alpha_", "alpha"),
        ("_opacity_", "alpha"),
        ("_nor_dx_", "nordx"),
        ("_nor_gl_", "nor"),
        ("_rough_", "rough"),
    )
    for name in os.listdir(folder):
        lower = name.lower()
        for token, kind in tokens:
            index = lower.find(token)
            if index > 0:
                prefix = name[:index]
                groups.setdefault(prefix, {})[kind] = os.path.join(folder, name)
                break
    return groups


def match_prefix(material_name, groups):
    if material_name in groups:
        return material_name
    folded = material_name.replace("branches", "branch")
    for prefix in groups:
        if prefix.replace("branches", "branch") == folded:
            return prefix
    for prefix in groups:
        if material_name.startswith(prefix) or prefix.startswith(material_name):
            return prefix
    if len(groups) == 1:
        return next(iter(groups))
    return None


def write_textures(asset, materials):
    groups = texture_groups(os.path.join(RAW, asset, "textures"))
    out_dir = os.path.join(OUT_ROOT, asset)
    ensure_dir(out_dir)
    used = set()
    for material in materials:
        prefix = match_prefix(material, groups)
        if prefix is None or material in used:
            continue
        used.add(material)
        write_material_textures(groups[prefix], os.path.join(out_dir, material))
        print("  textures", material, "from", prefix)


def write_material_textures(files, stem):
    if "diff" not in files:
        return
    width, height, albedo = image_pixels(files["diff"], False)
    alpha_path = files.get("alpha")
    if alpha_path:
        aw, ah, alpha = image_pixels(alpha_path, True)
        if (aw, ah) == (width, height):
            for i in range(0, len(albedo), 4):
                albedo[i + 3] = alpha[i]
    save_png(stem + "_albedo.png", width, height, albedo, False)

    normal_path = files.get("nordx") or files.get("nor")
    if normal_path:
        nw, nh, normal = image_pixels(normal_path, True)
        flip = "nordx" not in files
        if flip:
            for i in range(1, len(normal), 4):
                normal[i] = 1.0 - normal[i]
        save_png(stem + "_normal.png", nw, nh, normal, True)

    if "rough" in files:
        rw, rh, rough = image_pixels(files["rough"], True)
        save_png(stem + "_roughness.png", rw, rh, rough, True)


def export_fbx(asset, objects):
    out_dir = os.path.join(OUT_ROOT, asset)
    ensure_dir(out_dir)
    path = os.path.join(out_dir, asset + ".fbx")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
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
    print("EXPORTED", path, os.path.getsize(path))


def process(asset):
    spec = SPECS[asset]
    print("====", asset, "====")
    wipe()
    src = os.path.join(RAW, asset, asset + "_1k.fbx")
    bpy.ops.import_scene.fbx(filepath=src)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    for obj in meshes:
        obj.name = base_name(obj.name) + ("" if lod_index(obj.name) is None else "_LOD%d" % lod_index(obj.name))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    groups = {}
    for obj in meshes:
        index = lod_index(obj.name)
        key = base_name(obj.name) if index is not None else re.sub(r"\.\d+$", "", obj.name)
        key = RENAME.get(re.sub(r"_LOD\d+$", "", key), re.sub(r"_LOD\d+$", "", key))
        groups.setdefault(key, []).append(obj)

    prepared = []
    for key, members in groups.items():
        for obj in members:
            bake(obj)
        by_lod = {}
        for obj in members:
            index = lod_index(obj.name)
            by_lod[0 if index is None else index] = obj
        source = by_lod[min(by_lod)]
        tris = tri_count(source)
        keep = [source]
        if tris <= HEAVY:
            for index in (1, 2):
                if index in by_lod:
                    keep.append(by_lod[index])
        else:
            for obj in members:
                if obj not in keep:
                    bpy.data.objects.remove(obj, do_unlink=True)
        prepared.append({"key": key, "source": source, "keep": keep, "tris": tris})

    heights = []
    longests = []
    for item in prepared:
        mn, mx = local_bounds(item["source"])
        item["mn"] = mn
        item["mx"] = mx
        heights.append(mx.z - mn.z)
        longests.append(max(mx.x - mn.x, mx.y - mn.y, mx.z - mn.z))
    if spec.get("height"):
        factor = spec["height"] / max(max(heights), 0.001)
    elif spec.get("longest"):
        factor = spec["longest"] / max(max(longests), 0.001)
    else:
        factor = 1.0
    print("  scale factor", round(factor, 4), "tallest", round(max(heights), 3))

    exported = []
    reports = []
    hero_done = False
    heaviest = max(prepared, key=lambda item: item["tris"])
    for item in prepared:
        shift = Vector((-(item["mn"].x + item["mx"].x) * 0.5, -(item["mn"].y + item["mx"].y) * 0.5, -item["mn"].z))
        for obj in item["keep"]:
            reshape(obj, factor, shift)
        source = item["source"]
        tris = tri_count(source)
        if tris > HEAVY:
            if spec.get("hero") and item is heaviest and not hero_done:
                hero = duplicate(source, asset + "_hero")
                hero_tris = decimate_to(hero, HERO_TRIS)
                exported.append(hero)
                reports.append((hero.name, hero_tris))
                hero_done = True
            lod0_tris = decimate_to(source, LOD0_TRIS)
            source.name = item["key"] + "_LOD0"
            source.data.name = source.name
            lod1 = duplicate(source, item["key"] + "_LOD1")
            lod1_tris = decimate_to(lod1, int(LOD0_TRIS * 0.35))
            lod2 = duplicate(source, item["key"] + "_LOD2")
            lod2_tris = decimate_to(lod2, int(LOD0_TRIS * 0.10))
            exported.extend([source, lod1, lod2])
            reports.append((source.name, lod0_tris))
            reports.append((lod1.name, lod1_tris))
            reports.append((lod2.name, lod2_tris))
        else:
            if len(item["keep"]) == 1:
                source.name = item["key"] + "_LOD0"
                source.data.name = source.name
            exported.extend(item["keep"])
            for obj in item["keep"]:
                reports.append((obj.name, tri_count(obj)))

    materials = []
    for obj in exported:
        for slot in obj.material_slots:
            if slot.material is None:
                continue
            slot.material.name = re.sub(r"\.\d+$", "", slot.material.name)
            if slot.material.name not in materials:
                materials.append(slot.material.name)
    export_fbx(asset, exported)
    write_textures(asset, materials)
    print("REPORT", asset, reports)
    return reports


def main():
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    failed = []
    for asset in argv_ids():
        try:
            process(asset)
        except Exception:
            failed.append(asset)
            traceback.print_exc()
            wipe()
    print("BUILD_DONE", "failed", failed)


if __name__ == "__main__":
    main()
