from __future__ import annotations

import hashlib
import json
import math
import struct
import time
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
INPUT_BLEND = ROOT / "Volcano_Hero_02_TropicalLayerBalance_R19.blend"
OUTPUT_BLEND = ROOT / "Volcano_Hero_02_MaterialControlMasks_R20.blend"
OUTPUT_DIR = ROOT / "MaterialControlMasks_R20"
TEXTURE_DIR = OUTPUT_DIR / "Textures"
DIAGNOSTIC_DIR = OUTPUT_DIR / "Diagnostics"
RENDER_DIR = OUTPUT_DIR / "Renders"
REPORT_PATH = OUTPUT_DIR / "Volcano_Hero_R20_BakeReport.json"

HERO_NAME = "Volcano_Hero_02"
SOURCE_MATERIAL = "M_Volcano_TropicalLayerBalance_R19"
R20_MATERIAL = "M_Volcano_MaterialControl_R20"
UV_NAME = "Volcano_MacroRelief_Step3A_UV"
SIZE = 2048

VIEWS = (
    ("01_front", Vector((0.0, -246.0, 39.0)), Vector((0.0, 0.0, 25.0)), 52.0),
    ("02_left", Vector((-224.0, 0.0, 38.0)), Vector((0.0, 0.0, 23.0)), 52.0),
    ("03_right", Vector((224.0, 0.0, 38.0)), Vector((0.0, 0.0, 23.0)), 52.0),
    ("04_top45", Vector((145.0, -166.0, 165.0)), Vector((0.0, 0.0, 18.0)), 55.0),
    ("05_direct_top", Vector((0.0, 0.0, 280.0)), Vector((0.0, 0.0, 18.0)), 58.0),
)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def mesh_hash(mesh) -> str:
    digest = hashlib.sha256()
    for vertex in mesh.vertices:
        digest.update(struct.pack("3f", *tuple(vertex.co)))
    for polygon in mesh.polygons:
        digest.update(struct.pack(f"{len(polygon.vertices)}I", *polygon.vertices))
    return digest.hexdigest()


def uv_hash(mesh) -> str:
    digest = hashlib.sha256()
    for layer in mesh.uv_layers:
        digest.update(layer.name.encode("utf-8"))
        for datum in layer.data:
            digest.update(struct.pack("2f", *tuple(datum.uv)))
    return digest.hexdigest()


def modifier_signature(obj):
    result = []
    for modifier in obj.modifiers:
        values = {"name": modifier.name, "type": modifier.type}
        for prop in modifier.bl_rna.properties:
            if prop.is_readonly or prop.identifier in {"rna_type", "name", "type"}:
                continue
            try:
                value = getattr(modifier, prop.identifier)
            except Exception:
                continue
            if isinstance(value, (str, int, float, bool)):
                values[prop.identifier] = value
        result.append(values)
    return result


def scene_object_signature(types):
    rows = []
    for obj in sorted((o for o in bpy.data.objects if o.type in types), key=lambda x: x.name):
        rows.append(
            {
                "name": obj.name,
                "type": obj.type,
                "matrix_world": [float(x) for row in obj.matrix_world for x in row],
                "data": obj.data.name if obj.data else None,
            }
        )
    return rows


def math_node(nodes, name, operation, a=None, b=None, *, clamp=True, location=(0, 0)):
    node = nodes.new("ShaderNodeMath")
    node.name = name
    node.label = name
    node.operation = operation
    node.use_clamp = clamp
    node.location = location
    if isinstance(a, (int, float)):
        node.inputs[0].default_value = float(a)
    if isinstance(b, (int, float)):
        node.inputs[1].default_value = float(b)
    return node


def connect_or_value(links, source, target):
    if hasattr(source, "bl_idname") and source.bl_idname.startswith("NodeSocket"):
        links.new(source, target)
    else:
        target.default_value = float(source)


def build_r20_material(source):
    material = source.copy()
    material.name = R20_MATERIAL
    material["pass_name"] = "Volcano R20 Separate Material Control Masks"
    material["channel_R"] = "Upper exposed rock and ash"
    material["channel_G"] = "Vegetation ground"
    material["channel_B"] = "Bare soil / ochre / exposed earth"
    material["channel_A"] = "Dark summit and crater"
    material["source_blend"] = INPUT_BLEND.name
    material["vertex_displacement"] = 0.0

    nodes = material.node_tree.nodes
    links = material.node_tree.links
    zone = nodes["MASK_RGBA_NaturalZoneWeights"]

    sep = nodes.new("ShaderNodeSeparateColor")
    sep.name = "R20_SeparateLegacyZoneWeights"
    sep.label = "Actual Blender Zone Data: R Soil / G Ash / B Rock"
    sep.mode = "RGB"
    sep.location = (3500, 500)
    links.new(zone.outputs["Color"], sep.inputs["Color"])
    legacy_soil = sep.outputs["Red"]
    legacy_ash = sep.outputs["Green"]
    legacy_rock = sep.outputs["Blue"]
    legacy_summit = zone.outputs["Alpha"]

    # Vegetation is derived from the actual procedural system that produced R19.
    veg_max_lower = math_node(nodes, "R20_VegMaxLower", "MAXIMUM", location=(3740, 140))
    links.new(nodes["VEG_R14_AllVegetationUnion"].outputs[0], veg_max_lower.inputs[0])
    links.new(nodes["VEG_R18_LowerGuaranteeStrength"].outputs[0], veg_max_lower.inputs[1])
    veg_max_right = math_node(nodes, "R20_VegMaxRight", "MAXIMUM", location=(3940, 140))
    links.new(veg_max_lower.outputs[0], veg_max_right.inputs[0])
    links.new(nodes["VEG_R18_RightLowerStrength"].outputs[0], veg_max_right.inputs[1])

    inv_upper = math_node(nodes, "R20_InvLegacyUpperRock", "SUBTRACT", 1.0, location=(3740, -20))
    links.new(legacy_rock, inv_upper.inputs[1])
    inv_summit = math_node(nodes, "R20_InvSummit", "SUBTRACT", 1.0, location=(3740, -100))
    links.new(legacy_summit, inv_summit.inputs[1])
    inv_soil_gap = math_node(nodes, "R20_InvBareSoilGap", "SUBTRACT", 1.0, location=(3740, -180))
    soil_gap_scaled = math_node(nodes, "R20_BareSoilGapScaled", "MULTIPLY", location=(3540, -180))
    links.new(nodes["VEG_R15_BareSoilStrength"].outputs[0], soil_gap_scaled.inputs[0])
    soil_gap_scaled.inputs[1].default_value = 1.35
    links.new(soil_gap_scaled.outputs[0], inv_soil_gap.inputs[1])

    veg_upper_limit = math_node(nodes, "R20_VegUpperLimit", "MULTIPLY", location=(4140, 100))
    links.new(veg_max_right.outputs[0], veg_upper_limit.inputs[0])
    links.new(inv_upper.outputs[0], veg_upper_limit.inputs[1])
    veg_no_summit = math_node(nodes, "R20_VegNoSummit", "MULTIPLY", location=(4340, 100))
    links.new(veg_upper_limit.outputs[0], veg_no_summit.inputs[0])
    links.new(inv_summit.outputs[0], veg_no_summit.inputs[1])
    veg_raw_base = math_node(nodes, "R20_VegetationBase", "MULTIPLY", location=(4540, 100))
    links.new(veg_no_summit.outputs[0], veg_raw_base.inputs[0])
    links.new(inv_soil_gap.outputs[0], veg_raw_base.inputs[1])
    veg_raw = math_node(nodes, "R20_RAW_G_Vegetation", "MULTIPLY", location=(4740, 100))
    links.new(veg_raw_base.outputs[0], veg_raw.inputs[0])
    veg_raw.inputs[1].default_value = 1.24

    # Soil remains only as irregular gaps inside a vegetation-dominant lower flank.
    inv_veg = math_node(nodes, "R20_InvVegetation", "SUBTRACT", 1.0, location=(4140, -100))
    links.new(veg_raw.outputs[0], inv_veg.inputs[1])
    soil_remainder = math_node(nodes, "R20_SoilRemainder", "MULTIPLY", location=(4340, -160))
    links.new(legacy_soil, soil_remainder.inputs[0])
    links.new(inv_veg.outputs[0], soil_remainder.inputs[1])
    soil_remainder_scale = math_node(nodes, "R20_SoilRemainderScale", "MULTIPLY", location=(4540, -160))
    links.new(soil_remainder.outputs[0], soil_remainder_scale.inputs[0])
    soil_remainder_scale.inputs[1].default_value = 0.68
    soil_gap_keep = math_node(nodes, "R20_SoilGapKeep", "MULTIPLY", location=(4340, -240))
    links.new(nodes["VEG_R15_BareSoilStrength"].outputs[0], soil_gap_keep.inputs[0])
    soil_gap_keep.inputs[1].default_value = 0.72
    soil_raw = math_node(nodes, "R20_RAW_B_Soil", "MAXIMUM", location=(4940, -160))
    links.new(soil_remainder_scale.outputs[0], soil_raw.inputs[0])
    links.new(soil_gap_keep.outputs[0], soil_raw.inputs[1])

    # Ash and upper exposed rock become one channel, preserving their actual masks.
    rock_ash_max = math_node(nodes, "R20_RockAshMaximum", "MAXIMUM", location=(4140, 400))
    links.new(legacy_ash, rock_ash_max.inputs[0])
    links.new(legacy_rock, rock_ash_max.inputs[1])
    half_veg = math_node(nodes, "R20_HalfVegetation", "MULTIPLY", location=(4340, 320))
    links.new(veg_raw.outputs[0], half_veg.inputs[0])
    half_veg.inputs[1].default_value = 0.82
    inv_half_veg = math_node(nodes, "R20_InvHalfVegetation", "SUBTRACT", 1.0, location=(4540, 320))
    links.new(half_veg.outputs[0], inv_half_veg.inputs[1])
    rock_raw = math_node(nodes, "R20_RAW_R_RockAsh", "MULTIPLY", location=(4740, 400))
    links.new(rock_ash_max.outputs[0], rock_raw.inputs[0])
    links.new(inv_half_veg.outputs[0], rock_raw.inputs[1])

    summit_raw = math_node(nodes, "R20_RAW_A_Summit", "MULTIPLY", location=(4740, 520))
    links.new(legacy_summit, summit_raw.inputs[0])
    summit_raw.inputs[1].default_value = 1.15

    # Normalize the four masks, producing soft weights that always sum to one.
    sum_rg = math_node(nodes, "R20_SumRockVeg", "ADD", location=(4960, 300))
    links.new(rock_raw.outputs[0], sum_rg.inputs[0])
    links.new(veg_raw.outputs[0], sum_rg.inputs[1])
    sum_bs = math_node(nodes, "R20_SumSoilSummit", "ADD", location=(4960, 180))
    links.new(soil_raw.outputs[0], sum_bs.inputs[0])
    links.new(summit_raw.outputs[0], sum_bs.inputs[1])
    total = math_node(nodes, "R20_WeightTotal", "ADD", location=(5160, 240))
    links.new(sum_rg.outputs[0], total.inputs[0])
    links.new(sum_bs.outputs[0], total.inputs[1])
    epsilon = math_node(nodes, "R20_TotalWithEpsilon", "MAXIMUM", location=(5360, 240))
    links.new(total.outputs[0], epsilon.inputs[0])
    epsilon.inputs[1].default_value = 0.0001

    normalized = {}
    for channel, raw, y in (
        ("R_RockAsh", rock_raw, 440),
        ("G_Vegetation", veg_raw, 320),
        ("B_Soil", soil_raw, 200),
        ("A_Summit", summit_raw, 80),
    ):
        node = math_node(nodes, f"R20_MASK_{channel}", "DIVIDE", location=(5580, y))
        links.new(raw.outputs[0], node.inputs[0])
        links.new(epsilon.outputs[0], node.inputs[1])
        normalized[channel] = node.outputs[0]

    # R20 albedo uses the real R19 pre-vegetation surface and procedural vegetation,
    # but its distribution is driven by the corrected R20 vegetation weight.
    current_final = nodes["VEG_R18_RightGreenMix"].outputs[0]
    visible_green = nodes["VEG_R18_VisibleGreenPalette"].outputs[0]
    veg_candidate = nodes.new("ShaderNodeMixRGB")
    veg_candidate.name = "R20_VegetationCandidate"
    veg_candidate.label = "R19 organic groundcover + muted olive palette"
    veg_candidate.blend_type = "MIX"
    veg_candidate.inputs[0].default_value = 0.30
    veg_candidate.location = (5360, -120)
    links.new(current_final, veg_candidate.inputs[1])
    links.new(visible_green, veg_candidate.inputs[2])

    albedo_veg_factor = math_node(nodes, "R20_AlbedoVegetationFactor", "MULTIPLY", location=(5580, -20))
    links.new(normalized["G_Vegetation"], albedo_veg_factor.inputs[0])
    albedo_veg_factor.inputs[1].default_value = 1.55

    albedo = nodes.new("ShaderNodeMixRGB")
    albedo.name = "R20_ALBEDO_OUTPUT"
    albedo.label = "R20 Tropical Albedo — Corrected Control Distribution"
    albedo.blend_type = "MIX"
    albedo.location = (5800, -80)
    links.new(albedo_veg_factor.outputs[0], albedo.inputs[0])
    links.new(nodes["INTEGRATE_R6_InternalColorVariation"].outputs[0], albedo.inputs[1])
    links.new(veg_candidate.outputs[0], albedo.inputs[2])

    return material, normalized, albedo.outputs[0]


def make_image(name, colorspace, *, alpha=False):
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    image = bpy.data.images.new(name, width=SIZE, height=SIZE, alpha=alpha, float_buffer=False)
    image.colorspace_settings.name = colorspace
    image.generated_color = (0.0, 0.0, 0.0, 0.0 if alpha else 1.0)
    if alpha:
        image.alpha_mode = "STRAIGHT"
    return image


def prepare_hero(hero, material):
    for obj in bpy.context.scene.objects:
        obj.select_set(False)
    hero.hide_render = False
    hero.hide_set(False)
    hero.select_set(True)
    bpy.context.view_layer.objects.active = hero
    hero.data.materials[hero.active_material_index] = material


def bake_socket(hero, material, node_name, socket_name, image, output_path):
    bake_material = material.copy()
    bake_material.name = f"__R20_BAKE_{image.name}"
    nodes = bake_material.node_tree.nodes
    links = bake_material.node_tree.links
    output = next((n for n in nodes if n.type == "OUTPUT_MATERIAL" and n.is_active_output), None)
    if output is None:
        output = next(n for n in nodes if n.type == "OUTPUT_MATERIAL")
    for link in list(output.inputs["Surface"].links):
        links.remove(link)
    emission = nodes.new("ShaderNodeEmission")
    emission.name = "__R20_BAKE_EMISSION"
    source = nodes[node_name].outputs[socket_name]
    links.new(source, emission.inputs["Color"])
    emission.inputs["Strength"].default_value = 1.0
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    target = nodes.new("ShaderNodeTexImage")
    target.name = "__R20_BAKE_TARGET"
    target.image = image
    nodes.active = target
    target.select = True

    prepare_hero(hero, bake_material)
    scene = bpy.context.scene
    scene.render.bake.use_clear = True
    scene.render.bake.margin = 24
    scene.render.bake.target = "IMAGE_TEXTURES"
    scene.render.bake.use_selected_to_active = False
    bpy.ops.object.bake(type="EMIT")
    image.filepath_raw = str(output_path)
    image.file_format = "PNG"
    image.save()

    hero.data.materials[hero.active_material_index] = material
    bpy.data.materials.remove(bake_material)
    return output_path


def image_to_channel(image):
    rgba = np.empty(SIZE * SIZE * 4, dtype=np.float32)
    image.pixels.foreach_get(rgba)
    return rgba[0::4].copy()


def save_rgba(name, pixels, output_path, colorspace, *, alpha=True):
    image = make_image(name, colorspace, alpha=alpha)
    image.pixels.foreach_set(pixels.astype(np.float32).ravel())
    image.filepath_raw = str(output_path)
    image.file_format = "PNG"
    image.save()
    return image


def create_control_and_previews(channel_images):
    count = SIZE * SIZE
    control = np.zeros((count, 4), dtype=np.float32)
    for index, image in enumerate(channel_images):
        control[:, index] = image_to_channel(image)
    control = np.clip(control, 0.0, 1.0)

    actual_path = TEXTURE_DIR / "Volcano_Hero_R20_ControlRGBA.png"
    actual = save_rgba("Volcano_Hero_R20_ControlRGBA", control, actual_path, "Non-Color", alpha=True)

    visible = control.copy()
    visible[:, 3] = 1.0
    preview_path = DIAGNOSTIC_DIR / "03_ControlRGBA_RGBPreview.png"
    save_rgba("Volcano_Hero_R20_ControlRGBA_RGBPreview", visible, preview_path, "sRGB", alpha=True)

    occupied = control.sum(axis=1) > 0.05
    occupied_values = control[occupied]
    mean = occupied_values.mean(axis=0)
    normalized_mean = mean / max(float(mean.sum()), 1e-9)
    dominant = np.bincount(np.argmax(occupied_values, axis=1), minlength=4) / max(len(occupied_values), 1)
    return actual, actual_path, preview_path, normalized_mean, dominant


def point_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_render_material(material, albedo_image):
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    shader = nodes["SHADER_DryVolcanicPass02B"]
    for link in list(shader.inputs["Base Color"].links):
        links.remove(link)
    tex = nodes.get("R20_BakedAlbedo") or nodes.new("ShaderNodeTexImage")
    tex.name = "R20_BakedAlbedo"
    tex.label = "R20 Baked Albedo (Unity-ready source)"
    tex.image = albedo_image
    tex.interpolation = "Linear"
    tex.projection = "FLAT"
    tex.location = (5700, -360)
    uv = nodes.get("R20_AlbedoUV") or nodes.new("ShaderNodeUVMap")
    uv.name = "R20_AlbedoUV"
    uv.label = UV_NAME
    uv.uv_map = UV_NAME
    uv.location = (5480, -360)
    links.new(uv.outputs["UV"], tex.inputs["Vector"])
    links.new(tex.outputs["Color"], shader.inputs["Base Color"])


def render_views(hero, material):
    scene = bpy.context.scene
    original_camera = scene.camera
    camera_data = bpy.data.cameras.new("__TEMP_R20_CAMERA_DATA")
    camera = bpy.data.objects.new("__TEMP_R20_CAMERA", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    prepare_hero(hero, material)
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 630
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    for name, location, target, lens in VIEWS:
        camera.location = location
        camera.data.lens = lens
        point_at(camera, target)
        scene.render.filepath = str(RENDER_DIR / f"R20_{name}.png")
        bpy.ops.render.render(write_still=True)
    scene.camera = original_camera
    bpy.data.objects.remove(camera, do_unlink=True)
    bpy.data.cameras.remove(camera_data, do_unlink=True)


def main():
    started = time.perf_counter()
    for directory in (OUTPUT_DIR, TEXTURE_DIR, DIAGNOSTIC_DIR, RENDER_DIR):
        directory.mkdir(parents=True, exist_ok=True)
    input_sha_before = sha256_file(INPUT_BLEND)
    bpy.ops.wm.open_mainfile(filepath=str(INPUT_BLEND))

    hero = bpy.data.objects[HERO_NAME]
    mesh = hero.data
    source_material = hero.active_material
    assert source_material.name == SOURCE_MATERIAL, source_material.name
    assert mesh.uv_layers.get(UV_NAME) is not None
    mesh.uv_layers.active = mesh.uv_layers[UV_NAME]
    mesh.uv_layers[UV_NAME].active_render = True

    before = {
        "mesh_hash": mesh_hash(mesh),
        "uv_hash": uv_hash(mesh),
        "topology": [len(mesh.vertices), len(mesh.edges), len(mesh.polygons)],
        "positions": [v.co.copy() for v in mesh.vertices],
        "modifiers": modifier_signature(hero),
        "matrix_world": [float(x) for row in hero.matrix_world for x in row],
        "cameras_lights": scene_object_signature({"CAMERA", "LIGHT"}),
    }

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 1
    scene.cycles.use_denoising = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.bake.use_selected_to_active = False
    scene.render.bake.cage_extrusion = 0.0

    # Current R19 albedo reference: rebaked only into the isolated R20 output folder.
    r19_bsdf = source_material.node_tree.nodes["SHADER_DryVolcanicPass02B"]
    r19_base_link = r19_bsdf.inputs["Base Color"].links[0]
    r19_node_name = r19_base_link.from_node.name
    r19_socket_name = r19_base_link.from_socket.name
    r19_albedo = make_image("R19_Albedo_Reference", "sRGB")
    bake_socket(
        hero,
        source_material,
        r19_node_name,
        r19_socket_name,
        r19_albedo,
        DIAGNOSTIC_DIR / "01_Current_R19_Albedo.png",
    )

    r20_material, masks, albedo_socket = build_r20_material(source_material)
    channel_specs = (
        ("R_RockAsh", "R20_MASK_R_RockAsh", "R20_Channel_R_RockAsh.png"),
        ("G_Vegetation", "R20_MASK_G_Vegetation", "R20_Channel_G_Vegetation.png"),
        ("B_Soil", "R20_MASK_B_Soil", "R20_Channel_B_Soil.png"),
        ("A_Summit", "R20_MASK_A_Summit", "R20_Channel_A_Summit.png"),
    )
    channel_images = []
    channel_paths = {}
    for label, node_name, filename in channel_specs:
        image = make_image(f"Volcano_Hero_R20_{label}", "Non-Color")
        path = DIAGNOSTIC_DIR / filename
        bake_socket(hero, r20_material, node_name, "Value", image, path)
        channel_images.append(image)
        channel_paths[label] = str(path)

    control_image, control_path, control_preview, mean_weights, dominant_weights = create_control_and_previews(channel_images)

    r20_albedo = make_image("Volcano_Hero_R20_Albedo", "sRGB")
    albedo_path = TEXTURE_DIR / "Volcano_Hero_R20_Albedo.png"
    bake_socket(hero, r20_material, "R20_ALBEDO_OUTPUT", "Color", r20_albedo, albedo_path)

    configure_render_material(r20_material, r20_albedo)
    hero.data.materials[hero.active_material_index] = r20_material
    render_views(hero, r20_material)

    moved = [(v.co - original).length for v, original in zip(mesh.vertices, before["positions"])]
    changed = [value for value in moved if value > 1e-12]
    after = {
        "mesh_hash": mesh_hash(mesh),
        "uv_hash": uv_hash(mesh),
        "topology": [len(mesh.vertices), len(mesh.edges), len(mesh.polygons)],
        "modifiers": modifier_signature(hero),
        "matrix_world": [float(x) for row in hero.matrix_world for x in row],
        "cameras_lights": scene_object_signature({"CAMERA", "LIGHT"}),
    }

    hero["surface_pass"] = "MaterialControlMasks_R20"
    hero["R20_ControlRGBA"] = str(control_path)
    hero["R20_Albedo"] = str(albedo_path)
    scene["R20_bake_only"] = True
    scene["R20_vertex_movement"] = 0.0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))

    input_sha_after = sha256_file(INPUT_BLEND)
    report = {
        "pass": "VOLCANO R20 — BAKE SEPARATE MATERIAL CONTROL MASKS",
        "input_blend": str(INPUT_BLEND),
        "output_blend": str(OUTPUT_BLEND),
        "source_r19_sha256_before": input_sha_before,
        "source_r19_sha256_after": input_sha_after,
        "source_r19_unchanged": input_sha_before == input_sha_after,
        "object": HERO_NAME,
        "material": R20_MATERIAL,
        "texture_resolution": [SIZE, SIZE],
        "uv_set": UV_NAME,
        "outputs": {
            "control_rgba": str(control_path),
            "control_rgba_rgb_preview": str(control_preview),
            "albedo": str(albedo_path),
            "current_r19_albedo_reference": str(DIAGNOSTIC_DIR / "01_Current_R19_Albedo.png"),
            "channels": channel_paths,
            "renders": [str(RENDER_DIR / f"R20_{name}.png") for name, *_ in VIEWS],
        },
        "channel_assignment": {
            "R": "Upper exposed rock and ash",
            "G": "Vegetation ground",
            "B": "Bare soil / ochre / exposed earth",
            "A": "Dark summit and crater",
        },
        "approx_mean_surface_control_percent": {
            "R_rock_ash": round(float(mean_weights[0] * 100.0), 3),
            "G_vegetation": round(float(mean_weights[1] * 100.0), 3),
            "B_soil": round(float(mean_weights[2] * 100.0), 3),
            "A_summit": round(float(mean_weights[3] * 100.0), 3),
        },
        "approx_dominant_surface_percent": {
            "R_rock_ash": round(float(dominant_weights[0] * 100.0), 3),
            "G_vegetation": round(float(dominant_weights[1] * 100.0), 3),
            "B_soil": round(float(dominant_weights[2] * 100.0), 3),
            "A_summit": round(float(dominant_weights[3] * 100.0), 3),
        },
        "geometry_validation": {
            "vertex_count": len(mesh.vertices),
            "moved_vertex_count": len(changed),
            "maximum_vertex_displacement": max(changed, default=0.0),
            "mesh_hash_unchanged": before["mesh_hash"] == after["mesh_hash"],
            "topology_unchanged": before["topology"] == after["topology"],
            "modifiers_unchanged": before["modifiers"] == after["modifiers"],
            "uv_unchanged": before["uv_hash"] == after["uv_hash"],
            "transforms_unchanged": before["matrix_world"] == after["matrix_world"],
            "cameras_lights_unchanged": before["cameras_lights"] == after["cameras_lights"],
        },
        "existing_maps_rebaked": {
            "R19_Normal": False,
            "R19_AO": False,
            "R19_Roughness": False,
            "R19_MetallicSmoothness": False,
        },
        "fbx_exported": False,
        "unity_modified": False,
        "blender_errors": [],
        "elapsed_seconds": time.perf_counter() - started,
    }
    REPORT_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("R20_BAKE_RESULT_BEGIN")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    print("R20_BAKE_RESULT_END")


if __name__ == "__main__":
    main()
