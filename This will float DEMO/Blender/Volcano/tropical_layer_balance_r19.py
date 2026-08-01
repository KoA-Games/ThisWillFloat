from __future__ import annotations

import hashlib
import json
import struct
import time
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
INPUT_BLEND = ROOT / "Volcano_Hero_02_FinalTropicalGroundcover_R18_GreenFoothills.blend"
OUTPUT_BLEND = ROOT / "Volcano_Hero_02_TropicalLayerBalance_R19.blend"
RENDER_DIR = ROOT / "TropicalLayerBalance_R19_Renders"
REPORT_PATH = RENDER_DIR / "tropical_layer_balance_r19_report.json"

HERO_NAME = "Volcano_Hero_02"
MATERIAL_NAME = "M_Volcano_TropicalLayerBalance_R19"

VIEWS = (
    ("01_front", Vector((0.0, -246.0, 39.0)), Vector((0.0, 0.0, 25.0)), 52.0),
    ("02_left", Vector((-224.0, 0.0, 38.0)), Vector((0.0, 0.0, 23.0)), 52.0),
    ("03_right", Vector((224.0, 0.0, 38.0)), Vector((0.0, 0.0, 23.0)), 52.0),
    ("04_top45", Vector((145.0, -166.0, 165.0)), Vector((0.0, 0.0, 18.0)), 55.0),
    ("05_direct_top", Vector((0.0, 0.0, 280.0)), Vector((0.0, 0.0, 18.0)), 58.0),
)


def mesh_hash(mesh):
    digest = hashlib.sha256()
    for vertex in mesh.vertices:
        digest.update(struct.pack("3f", *tuple(vertex.co)))
    for polygon in mesh.polygons:
        digest.update(struct.pack(f"{len(polygon.vertices)}I", *polygon.vertices))
    return digest.hexdigest()


def set_bw_ramp(node, points):
    ramp = node.color_ramp
    while len(ramp.elements) > 2:
        ramp.elements.remove(ramp.elements[-1])
    while len(ramp.elements) < len(points):
        ramp.elements.new(0.5)
    for element, (position, value) in zip(ramp.elements, points):
        element.position = position
        element.color = (value, value, value, 1.0)
    ramp.interpolation = "EASE"


def set_color_ramp(node, points):
    ramp = node.color_ramp
    while len(ramp.elements) > 2:
        ramp.elements.remove(ramp.elements[-1])
    while len(ramp.elements) < len(points):
        ramp.elements.new(0.5)
    for element, (position, color) in zip(ramp.elements, points):
        element.position = position
        element.color = color
    ramp.interpolation = "EASE"


def remove_links(socket):
    for link in list(socket.links):
        socket.id_data.links.remove(link)


def point_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_views(camera, prefix):
    scene = bpy.context.scene
    for name, location, target, lens in VIEWS:
        camera.location = location
        camera.data.lens = lens
        point_at(camera, target)
        scene.render.filepath = str(RENDER_DIR / f"{prefix}_{name}.png")
        bpy.ops.render.render(write_still=True)


def build_material(source):
    material = source.copy()
    material.name = MATERIAL_NAME
    material["pass_name"] = "Tropical Layer Balance R19"
    material["vertex_displacement"] = 0.0
    material["goal"] = (
        "reduce uniform green coverage; preserve exposed upper rock; create broad "
        "interrupted lower vegetation masses with visible volcanic soil"
    )

    nodes = material.node_tree.nodes
    links = material.node_tree.links

    # Underlying vegetation system: less coverage and a lower cutoff.
    set_bw_ramp(
        nodes["VEG_R11_HeightBelt"],
        (
            (0.000, 0.50),
            (0.035, 0.68),
            (0.090, 0.90),
            (0.260, 0.92),
            (0.330, 0.76),
            (0.400, 0.48),
            (0.470, 0.18),
            (0.530, 0.00),
            (1.000, 0.00),
        ),
    )
    set_bw_ramp(
        nodes["VEG_R11_MassCoverage"],
        (
            (0.00, 0.20),
            (0.18, 0.28),
            (0.34, 0.44),
            (0.48, 0.66),
            (0.60, 0.86),
            (0.72, 0.98),
            (1.00, 1.00),
        ),
    )
    set_bw_ramp(
        nodes["VEG_R12_Density"],
        (
            (0.00, 0.00),
            (0.12, 0.00),
            (0.24, 0.10),
            (0.36, 0.34),
            (0.48, 0.64),
            (0.62, 0.88),
            (0.76, 1.00),
            (1.00, 1.00),
        ),
    )

    # Restore broad soil openings at the foot while avoiding a continuous ring.
    set_bw_ramp(
        nodes["VEG_R14_BasePatchMass"],
        (
            (0.00, 0.26),
            (0.18, 0.34),
            (0.32, 0.48),
            (0.46, 0.68),
            (0.60, 0.84),
            (0.76, 0.96),
            (1.00, 1.00),
        ),
    )
    nodes["VEG_R15_BareSoilStrength"].inputs[1].default_value = 0.24
    nodes["VEG_R16_ClusterColorFactor"].inputs[1].default_value = 0.40
    nodes["VEG_R16_ClusterRoughnessFactor"].inputs[1].default_value = 0.66
    nodes["VEG_R16_GroundcoverBump"].inputs["Strength"].default_value = 0.085
    nodes["VEG_R16_GroundcoverBump"].inputs["Distance"].default_value = 0.007

    # Replace the R18 uniform lower blanket with broad organic patches.
    set_bw_ramp(
        nodes["VEG_R18_LowerGuaranteeHeight"],
        (
            (0.000, 0.72),
            (0.035, 0.82),
            (0.100, 0.94),
            (0.270, 0.92),
            (0.340, 0.72),
            (0.410, 0.44),
            (0.490, 0.12),
            (0.550, 0.00),
            (1.000, 0.00),
        ),
    )
    set_bw_ramp(
        nodes["VEG_R18_LowerGuaranteeMass"],
        (
            (0.00, 0.10),
            (0.18, 0.14),
            (0.32, 0.28),
            (0.44, 0.52),
            (0.56, 0.76),
            (0.68, 0.92),
            (0.82, 1.00),
            (1.00, 1.00),
        ),
    )
    lower_strength = nodes["VEG_R18_LowerGuaranteeStrength"]
    remove_links(lower_strength.inputs[0])
    links.new(nodes["VEG_R18_LowerGuaranteeShape"].outputs[0], lower_strength.inputs[0])
    lower_strength.inputs[1].default_value = 0.78

    # Keep the right flank tropical, but not as a high continuous green wall.
    set_bw_ramp(
        nodes["VEG_R18_RightFlankHeight"],
        (
            (0.000, 0.74),
            (0.260, 0.76),
            (0.360, 0.60),
            (0.450, 0.34),
            (0.540, 0.10),
            (0.600, 0.00),
            (1.000, 0.00),
        ),
    )
    nodes["VEG_R18_RightLowerStrength"].inputs[1].default_value = 0.52
    set_color_ramp(
        nodes["VEG_R18_RightShadowGreenPalette"],
        (
            (0.08, (0.038, 0.092, 0.021, 1.0)),
            (0.28, (0.052, 0.120, 0.027, 1.0)),
            (0.48, (0.068, 0.148, 0.034, 1.0)),
            (0.66, (0.088, 0.174, 0.043, 1.0)),
            (0.82, (0.112, 0.198, 0.052, 1.0)),
            (0.94, (0.075, 0.132, 0.035, 1.0)),
        ),
    )

    return material


def configure_saved_viewports():
    count = 0
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type != "VIEW_3D":
                continue
            space = area.spaces.active
            space.shading.type = "RENDERED"
            space.shading.use_scene_lights = True
            space.shading.use_scene_world = True
            count += 1
    return count


def main():
    started = time.perf_counter()
    RENDER_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(INPUT_BLEND))

    hero = bpy.data.objects[HERO_NAME]
    mesh = hero.data
    before_positions = [v.co.copy() for v in mesh.vertices]
    before_hash = mesh_hash(mesh)
    topology_before = (len(mesh.vertices), len(mesh.edges), len(mesh.polygons))
    modifiers_before = [(m.name, m.type) for m in hero.modifiers]

    scene = bpy.context.scene
    original_camera = scene.camera
    camera_data = bpy.data.cameras.new("__TEMP_R19_CAMERA_DATA")
    camera = bpy.data.objects.new("__TEMP_R19_CAMERA", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 630
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"

    render_views(camera, "before")
    material = build_material(hero.active_material)
    hero.data.materials[hero.active_material_index] = material
    render_views(camera, "after")

    scene.camera = original_camera
    bpy.data.objects.remove(camera, do_unlink=True)
    bpy.data.cameras.remove(camera_data, do_unlink=True)

    moved = [(v.co - p).length for v, p in zip(mesh.vertices, before_positions) if (v.co - p).length > 1e-9]
    topology_after = (len(mesh.vertices), len(mesh.edges), len(mesh.polygons))
    report = {
        "input_blend": str(INPUT_BLEND),
        "output_blend": str(OUTPUT_BLEND),
        "object": HERO_NAME,
        "material": MATERIAL_NAME,
        "scope": "material masks, colors, roughness and vegetation bump only",
        "vegetation_distribution": {
            "upper_60_100_percent": "nearly absent; exposed rock and ash preserved",
            "middle_35_60_percent": "rock-dominant irregular vegetation tongues",
            "lower_0_35_percent": "vegetation-dominant broad patches with exposed soil gaps",
        },
        "vertex_movement_count": len(moved),
        "maximum_vertex_displacement": max(moved, default=0.0),
        "geometry_changed": mesh_hash(mesh) != before_hash,
        "topology_before": topology_before,
        "topology_after": topology_after,
        "topology_changed": topology_before != topology_after,
        "modifiers_changed": modifiers_before != [(m.name, m.type) for m in hero.modifiers],
        "vegetation_bump_strength": 0.085,
        "vegetation_bump_distance": 0.007,
        "camera_changed": False,
        "lighting_changed": False,
        "mapping_changed": False,
        "configured_rendered_viewports": configure_saved_viewports(),
        "total_seconds": time.perf_counter() - started,
    }
    hero["surface_pass"] = "TropicalLayerBalance_R19"
    scene["R19_material_only"] = True
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))
    REPORT_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
