from __future__ import annotations

import os
from pathlib import Path

import bpy


PROJECT = Path(os.environ["R70_UNITY_PROJECT"]).resolve()
TEXTURE_ROOT = PROJECT / "Assets" / "_Project" / "R70VolcanoMigration" / "Source" / "Textures"
VOLCANO_NAME = "Volcano_Hero_02"
SIZE = int(os.environ.get("R70_BAKE_SIZE", "2048"))


def configure_scene():
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    scene.render.bake.margin = 16
    scene.render.bake.use_clear = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"


def save_image(image: bpy.types.Image, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    print(f"R70_BAKE_SAVED {path} {tuple(image.size)} {image.colorspace_settings.name}")


def bake_map(obj: bpy.types.Object, bake_type: str, image_name: str, filename: str, non_color: bool):
    image = bpy.data.images.get(image_name)
    if image is not None:
        bpy.data.images.remove(image)
    image = bpy.data.images.new(image_name, width=SIZE, height=SIZE, alpha=False, float_buffer=False)
    image.generated_color = (0.5, 0.5, 1.0, 1.0) if bake_type == "NORMAL" else (0.75, 0.75, 0.75, 1.0)
    image.colorspace_settings.name = "Non-Color" if non_color else "sRGB"

    material = obj.active_material
    nodes = material.node_tree.nodes
    node = nodes.new("ShaderNodeTexImage")
    node.name = f"UNITY_BAKE_TARGET_{bake_type}"
    node.label = f"Temporary Unity {bake_type} bake target"
    node.image = image
    nodes.active = node

    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.hide_render = False
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    kwargs = {"type": bake_type}
    if bake_type == "NORMAL":
        kwargs["normal_space"] = "TANGENT"
    bpy.ops.object.bake(**kwargs)
    save_image(image, TEXTURE_ROOT / filename)
    nodes.remove(node)


def main():
    configure_scene()
    volcano = bpy.data.objects[VOLCANO_NAME]
    bake_map(volcano, "NORMAL", "R70_Unity_Volcano_Normal", "R70_Volcano_Normal.png", True)
    bake_map(volcano, "ROUGHNESS", "R70_Unity_Volcano_Roughness", "R70_Volcano_Roughness.png", True)
    print("R70_VOLCANO_MAP_BAKE_COMPLETE")


if __name__ == "__main__":
    main()
