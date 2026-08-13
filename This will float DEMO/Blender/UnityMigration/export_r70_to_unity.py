from __future__ import annotations

import json
import math
import os
import shutil
import statistics
from collections import Counter, defaultdict
from pathlib import Path

import bpy


PROJECT = Path(os.environ["R70_UNITY_PROJECT"]).resolve()
ASSET_ROOT = PROJECT / "Assets" / "_Project" / "R70VolcanoMigration"
SOURCE_ROOT = ASSET_ROOT / "Source"
MESH_ROOT = SOURCE_ROOT / "Meshes"
TEXTURE_ROOT = SOURCE_ROOT / "Textures"
REPORT_ROOT = SOURCE_ROOT / "Reports"

FINAL_COLLECTIONS = (
    "R69_PLACED_CLUSTER_COMMUNITIES",
    "R70_GAP_FILL_TRANSITION_REFINEMENT",
)
VOLCANO_NAME = "Volcano_Hero_02"


def ensure_dirs():
    for path in (MESH_ROOT, TEXTURE_ROOT, REPORT_ROOT):
        path.mkdir(parents=True, exist_ok=True)


def select_only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.select_set(True)
    if objects:
        bpy.context.view_layer.objects.active = objects[0]


def export_fbx(path: Path, objects):
    select_only(objects)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        use_mesh_modifiers_render=True,
        mesh_smooth_type="FACE",
        use_subsurf=False,
        use_mesh_edges=False,
        use_tspace=True,
        use_custom_props=True,
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        path_mode="AUTO",
        embed_textures=False,
    )


def export_vegetation_prototypes_and_placements(vegetation):
    prototypes_collection = bpy.data.collections.new("R70_UNITY_EXPORT_PROTOTYPES")
    placements_collection = bpy.data.collections.new("R70_UNITY_EXPORT_PLACEMENTS")
    bpy.context.scene.collection.children.link(prototypes_collection)
    bpy.context.scene.collection.children.link(placements_collection)

    representatives = {}
    for obj in vegetation:
        representatives.setdefault(obj.data.as_pointer(), obj)

    prototype_by_pointer = {}
    prototype_objects = []
    for index, (pointer, representative) in enumerate(
        sorted(representatives.items(), key=lambda item: item[1].data.name), 1
    ):
        key = f"R70SRC_{index:03d}"
        prototype = representative.copy()
        prototype.data = representative.data.copy()
        prototype.name = key
        prototype.data.name = key
        prototype.matrix_world.identity()
        prototype.animation_data_clear()
        prototype.hide_render = False
        prototype.hide_viewport = False
        prototypes_collection.objects.link(prototype)
        prototype_by_pointer[pointer] = key
        prototype_objects.append(prototype)

    placement_records = []
    placement_empties = []
    for index, obj in enumerate(vegetation, 1):
        placement_name = f"R70P_{index:04d}"
        empty = bpy.data.objects.new(placement_name, None)
        empty.empty_display_type = "PLAIN_AXES"
        empty.empty_display_size = 0.2
        empty.matrix_world = obj.matrix_world.copy()
        placements_collection.objects.link(empty)
        placement_empties.append(empty)
        placement_records.append({
            "name": placement_name,
            "source": prototype_by_pointer[obj.data.as_pointer()],
            "materials": [material.name if material else "" for material in obj.data.materials],
            "sourceObject": obj.name,
            "sourceCollection": next(
                (name for name in FINAL_COLLECTIONS if obj.name in bpy.data.collections[name].all_objects),
                "",
            ),
        })

    export_fbx(MESH_ROOT / "R70_VegetationPrototypes.fbx", prototype_objects)

    select_only(placement_empties)
    bpy.ops.export_scene.fbx(
        filepath=str(MESH_ROOT / "R70_VegetationPlacementTransforms.fbx"),
        use_selection=True,
        object_types={"EMPTY"},
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        use_custom_props=False,
    )

    (SOURCE_ROOT / "R70_VegetationPlacement.json").write_text(
        json.dumps({"placements": placement_records}, indent=2), encoding="utf-8"
    )
    return len(prototype_objects), placement_records


def material_base_color(material):
    if material.use_nodes:
        principled = next(
            (node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"),
            None,
        )
        if principled is not None:
            base = principled.inputs.get("Base Color")
            if base is not None and not base.is_linked:
                rgba = list(base.default_value)
                # Blender node colors and Unity material Color properties are both
                # consumed as linear values in this Linear-color-space project.
                # Converting these values to sRGB here made foliage substantially
                # brighter and more yellow/mint than the approved Blender render.
                return [float(v) for v in rgba]
    return [float(v) for v in material.diffuse_color]


def material_vertex_color_mix(material):
    """Return the Blender Color Attribute multiplier when the final Base Color uses it.

    The production foliage materials use MixRGB/MULTIPLY at a restrained factor:
    output = vertexColor * ((1-factor) + rampColor*factor).  Preserve that
    structure in Unity instead of exporting the unused Principled default color.
    """
    if not material.use_nodes:
        return None
    principled = next(
        (node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"),
        None,
    )
    if principled is None:
        return None
    base = principled.inputs.get("Base Color")
    if base is None or not base.is_linked:
        return None
    mix = base.links[0].from_node
    if mix.bl_idname != "ShaderNodeMixRGB" or mix.blend_type != "MULTIPLY":
        return None
    if not mix.inputs[1].is_linked or mix.inputs[1].links[0].from_node.bl_idname != "ShaderNodeVertexColor":
        return None
    factor = float(mix.inputs[0].default_value)
    ramp_average = [1.0, 1.0, 1.0]
    if mix.inputs[2].is_linked and mix.inputs[2].links[0].from_node.bl_idname == "ShaderNodeValToRGB":
        ramp = mix.inputs[2].links[0].from_node.color_ramp
        ramp_average = [statistics.fmean(float(element.color[channel]) for element in ramp.elements) for channel in range(3)]
    multiplier = [(1.0 - factor) + ramp_average[channel] * factor for channel in range(3)]
    return multiplier + [1.0]


def material_roughness(material):
    if material.use_nodes:
        principled = next(
            (node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"),
            None,
        )
        if principled is not None:
            rough = principled.inputs.get("Roughness")
            if rough is not None and not rough.is_linked:
                return float(rough.default_value)
    return float(getattr(material, "roughness", 0.75))


def active_vertex_color_average(mesh):
    attribute = mesh.color_attributes.active_color if mesh.color_attributes else None
    if attribute is None or not attribute.data:
        return None
    step = max(1, len(attribute.data) // 4096)
    samples = [attribute.data[index].color for index in range(0, len(attribute.data), step)]
    if not samples:
        return None
    return [statistics.fmean(float(color[channel]) for color in samples) for channel in range(4)]


def material_manifest(objects):
    materials = sorted(
        {material for obj in objects for material in obj.data.materials if material},
        key=lambda material: material.name,
    )
    mesh_colors = defaultdict(list)
    seen_meshes = set()
    for obj in objects:
        pointer = obj.data.as_pointer()
        if pointer in seen_meshes:
            continue
        seen_meshes.add(pointer)
        average = active_vertex_color_average(obj.data)
        if average is None:
            continue
        for material in obj.data.materials:
            if material:
                mesh_colors[material.name].append(average)

    payload = []
    for material in materials:
        vertex_mix = material_vertex_color_mix(material)
        use_vertex_color = vertex_mix is not None
        color = vertex_mix if use_vertex_color else material_base_color(material)
        vertex_color_weight = 1.0 if use_vertex_color else 0.0

        name_lower = material.name.lower()
        is_bark = "bark" in name_lower or "stem" in name_lower or "trunk" in name_lower
        is_dark = "dark" in name_lower or "core" in name_lower or "charcoal" in name_lower or "deep" in name_lower
        is_light = "light" in name_lower or "outer" in name_lower or "exposed" in name_lower
        noise_strength = 0.025 if is_bark else 0.055 if is_dark else 0.07 if is_light else 0.06
        payload.append({
            "name": material.name,
            "color": [round(float(v), 6) for v in color],
            "roughness": round(material_roughness(material), 5),
            "doubleSided": not is_bark,
            "useVertexColor": use_vertex_color,
            "vertexColorWeight": vertex_color_weight,
            "noiseScale": 0.22 if is_bark else 0.55,
            "noiseStrength": noise_strength,
        })
    return payload


def image_by_name(name):
    image = bpy.data.images.get(name)
    if image is None:
        raise RuntimeError(f"Missing packed Blender image: {name}")
    return image


def save_image(image, destination: Path):
    destination.parent.mkdir(parents=True, exist_ok=True)
    original_raw = image.filepath_raw
    original_format = image.file_format
    try:
        image.filepath_raw = str(destination)
        image.file_format = "PNG"
        image.save()
    finally:
        image.filepath_raw = original_raw
        image.file_format = original_format


def object_stats(objects):
    return {
        "objectCount": len(objects),
        "uniqueMeshCount": len({obj.data.as_pointer() for obj in objects}),
        "vertexInstanceSum": sum(len(obj.data.vertices) for obj in objects),
        "triangleInstanceSum": sum(
            sum(len(poly.vertices) - 2 for poly in obj.data.polygons) for obj in objects
        ),
        "materialCount": len({material.name for obj in objects for material in obj.data.materials if material}),
    }


def main():
    ensure_dirs()
    scene = bpy.context.scene
    volcano = bpy.data.objects[VOLCANO_NAME]
    vegetation = sorted(
        {
            obj
            for collection_name in FINAL_COLLECTIONS
            for obj in bpy.data.collections[collection_name].all_objects
            if obj.type == "MESH" and not obj.hide_render
        },
        key=lambda obj: obj.name,
    )

    export_meshes = os.environ.get("R70_EXPORT_MESHES", "1") != "0"
    if export_meshes:
        export_fbx(MESH_ROOT / "R70_Volcano.fbx", [volcano])
        prototype_count, placement_records = export_vegetation_prototypes_and_placements(vegetation)
    else:
        placement_payload = json.loads((SOURCE_ROOT / "R70_VegetationPlacement.json").read_text(encoding="utf-8"))
        placement_records = placement_payload["placements"]
        prototype_count = len({record["source"] for record in placement_records})
        print("R70_MESH_EXPORT_SKIPPED material-only refresh")

    rig_objects = [
        bpy.data.objects[name]
        for name in ("CAM_R69_ColorZone_Placement", "L_R69_Neutral_Daylight", "L_R69_Sky_Fill")
        if name in bpy.data.objects
    ]
    if export_meshes:
        select_only(rig_objects)
        bpy.ops.export_scene.fbx(
            filepath=str(MESH_ROOT / "R70_ReviewRig.fbx"),
            use_selection=True,
            object_types={"CAMERA", "LIGHT"},
            bake_anim=False,
            axis_forward="-Z",
            axis_up="Y",
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_NONE",
            use_custom_props=True,
        )

    save_image(image_by_name("Volcano_Hero_R20_Albedo"), TEXTURE_ROOT / "R70_Volcano_Albedo.png")
    save_image(image_by_name("Volcano_MacroRelief_Step3A_R2"), TEXTURE_ROOT / "R70_Volcano_MacroRelief.png")
    save_image(image_by_name("Volcano_ErodedAsh_Albedo_v1.png"), TEXTURE_ROOT / "R70_Volcano_ErodedAsh.png")
    save_image(image_by_name("R70_APPROVED_THREE_QUARTER"), TEXTURE_ROOT / "R70_ApprovedReference.png")

    materials = material_manifest(vegetation)
    (SOURCE_ROOT / "R70_VegetationMaterials.json").write_text(
        json.dumps({"materials": materials}, indent=2), encoding="utf-8"
    )

    camera = bpy.data.objects["CAM_R69_ColorZone_Placement"]
    sun = bpy.data.objects["L_R69_Neutral_Daylight"]
    world = scene.world.node_tree.nodes.get("Background") if scene.world and scene.world.use_nodes else None
    report = {
        "sourceBlend": bpy.data.filepath,
        "exportCollections": list(FINAL_COLLECTIONS),
        "volcano": object_stats([volcano]),
        "vegetation": object_stats(vegetation),
        "prototypeCount": prototype_count,
        "placementRecordCount": len(placement_records),
        "camera": {
            "blenderLocation": [round(float(v), 6) for v in camera.location],
            "blenderEulerRadians": [round(float(v), 8) for v in camera.rotation_euler],
            "lensMm": float(camera.data.lens),
            "sensorWidthMm": float(camera.data.sensor_width),
        },
        "sun": {
            "blenderEulerRadians": [round(float(v), 8) for v in sun.rotation_euler],
            "energy": float(sun.data.energy),
            "color": [round(float(v), 6) for v in sun.data.color],
        },
        "world": {
            "color": [round(float(v), 6) for v in world.inputs["Color"].default_value] if world else [0.18, 0.18, 0.18, 1],
            "strength": float(world.inputs["Strength"].default_value) if world else 1.0,
        },
        "coordinateConversion": "FBX axis_forward=-Z, axis_up=Y; Unity import scale 1",
        "preservation": {
            "volcanoMesh": True,
            "vegetationTransforms": True,
            "vertexColors": True,
            "uvs": True,
            "normals": True,
            "blenderProceduralShaders": False,
            "strategy": "URP custom shaders plus exported colors/textures",
        },
    }
    (REPORT_ROOT / "R70_UnityMigrationExportReport.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print("R70_UNITY_EXPORT_COMPLETE")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
