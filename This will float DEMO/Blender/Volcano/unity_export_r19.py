from __future__ import annotations

import hashlib
import json
import struct
import time
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parent.parent / "Assets" / "_Project" / "Art" / "Volcano" / "Hero_R19"
MODEL_DIR = PROJECT / "Models"
TEXTURE_DIR = PROJECT / "Textures"
REPORT_PATH = PROJECT / "Volcano_Hero_R19_ExportReport.json"

INPUT_BLEND = ROOT / "Volcano_Hero_02_TropicalLayerBalance_R19.blend"
FBX_PATH = MODEL_DIR / "Volcano_Hero_R19.fbx"
HERO_NAME = "Volcano_Hero_02"
SIZE = 2048


def mesh_hash(mesh):
    digest = hashlib.sha256()
    for vertex in mesh.vertices:
        digest.update(struct.pack("3f", *tuple(vertex.co)))
    for polygon in mesh.polygons:
        digest.update(struct.pack(f"{len(polygon.vertices)}I", *polygon.vertices))
    return digest.hexdigest()


def make_image(name, colorspace, alpha=False):
    image = bpy.data.images.get(name)
    if image:
        bpy.data.images.remove(image)
    image = bpy.data.images.new(name, width=SIZE, height=SIZE, alpha=alpha, float_buffer=False)
    image.colorspace_settings.name = colorspace
    image.generated_color = (0.0, 0.0, 0.0, 1.0)
    return image


def bake_image(hero, material, image, bake_type, filename, *, normal_space="TANGENT"):
    nodes = material.node_tree.nodes
    target = nodes.get("UNITY_R19_BAKE_TARGET")
    if target is None:
        target = nodes.new("ShaderNodeTexImage")
        target.name = "UNITY_R19_BAKE_TARGET"
        target.label = "Temporary Unity Bake Target"
    target.image = image
    nodes.active = target
    target.select = True

    scene = bpy.context.scene
    scene.render.bake.use_clear = True
    scene.render.bake.margin = 24
    scene.render.bake.target = "IMAGE_TEXTURES"
    if bake_type == "DIFFUSE":
        scene.render.bake.use_pass_direct = False
        scene.render.bake.use_pass_indirect = False
        scene.render.bake.use_pass_color = True

    bpy.ops.object.bake(type=bake_type, normal_space=normal_space)
    output = TEXTURE_DIR / filename
    image.filepath_raw = str(output)
    image.file_format = "PNG"
    image.save()
    target.select = False
    return output


def main():
    started = time.perf_counter()
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    TEXTURE_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(INPUT_BLEND))

    hero = bpy.data.objects[HERO_NAME]
    mesh = hero.data
    before_hash = mesh_hash(mesh)
    topology = (len(mesh.vertices), len(mesh.edges), len(mesh.polygons))
    modifiers = [(m.name, m.type) for m in hero.modifiers]

    for obj in bpy.context.scene.objects:
        obj.select_set(False)
    hero.hide_render = False
    hero.hide_set(False)
    hero.select_set(True)
    bpy.context.view_layer.objects.active = hero

    source_material = hero.active_material
    bake_material = source_material.copy()
    bake_material.name = "M_Volcano_R19_UnityBakeSource"
    hero.data.materials[hero.active_material_index] = bake_material

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 8
    scene.cycles.use_denoising = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.bake.use_selected_to_active = False
    scene.render.bake.cage_extrusion = 0.0

    outputs = {}
    outputs["albedo"] = str(
        bake_image(
            hero,
            bake_material,
            make_image("Volcano_Hero_R19_Albedo", "sRGB"),
            "DIFFUSE",
            "Volcano_Hero_R19_Albedo.png",
        )
    )
    outputs["normal"] = str(
        bake_image(
            hero,
            bake_material,
            make_image("Volcano_Hero_R19_Normal", "Non-Color"),
            "NORMAL",
            "Volcano_Hero_R19_Normal.png",
        )
    )
    outputs["roughness"] = str(
        bake_image(
            hero,
            bake_material,
            make_image("Volcano_Hero_R19_Roughness", "Non-Color"),
            "ROUGHNESS",
            "Volcano_Hero_R19_Roughness.png",
        )
    )
    outputs["ao"] = str(
        bake_image(
            hero,
            bake_material,
            make_image("Volcano_Hero_R19_AO", "Non-Color"),
            "AO",
            "Volcano_Hero_R19_AO.png",
        )
    )

    # Export the source geometry only. Baking and FBX triangulation do not
    # modify or save the protected R19 .blend.
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        use_mesh_modifiers_render=True,
        mesh_smooth_type="OFF",
        use_tspace=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )

    report = {
        "source_blend": str(INPUT_BLEND),
        "fbx": str(FBX_PATH),
        "textures": outputs,
        "texture_resolution": [SIZE, SIZE],
        "object": HERO_NAME,
        "dimensions_m": [float(v) for v in hero.dimensions],
        "topology": topology,
        "modifiers": modifiers,
        "source_geometry_changed": mesh_hash(mesh) != before_hash,
        "vertex_displacement": 0.0,
        "unity_axis": "Y up / -Z forward",
        "elapsed_seconds": time.perf_counter() - started,
    }
    REPORT_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("UNITY_EXPORT_R19_RESULT_BEGIN")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    print("UNITY_EXPORT_R19_RESULT_END")


if __name__ == "__main__":
    main()
