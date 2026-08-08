from __future__ import annotations

import hashlib
import importlib.util
import json
import math
import random
import struct
import sys
import time
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path

import bpy
from mathutils import Quaternion, Vector


ROOT = Path(__file__).resolve().parent
INPUT = ROOT / "Volcano_Hero_02_ForestClusterC_OrganicBridge_R68.blend"
OUTPUT = ROOT / "Volcano_Hero_02_ColorZoneClusterPlacement_R69.blend"
RENDER_DIR = ROOT / "ColorZoneClusterPlacement_R69_Renders"
REPORT_PATH = RENDER_DIR / "color_zone_cluster_placement_r69_report.json"
R24_SCRIPT = ROOT / "revise_material_masked_vegetation_placement_r24.py"

HERO_NAME = "Volcano_Hero_02"
MASTER = "R69_COLOR_ZONE_CLUSTER_PLACEMENT"
PLACEMENT_ROOT = "R69_PLACED_CLUSTER_COMMUNITIES"
RENDER_SETUP = "R69_RENDER_SETUP"
SEED = 690805
PREVIEW_MODE = "--preview" in sys.argv

SOURCE_COLLECTIONS = {
    "A": "FOREST_TEST_CANOPY_SILHOUETTE_FINAL",
    "B": "FOREST_CLUSTER_B_ELONGATED_REFINED",
    "C": "FOREST_CLUSTER_C_DUAL_MASS_REFINED",
}


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


r24 = load_module("r24_for_color_zone_r69", R24_SCRIPT)


@dataclass(frozen=True)
class PlacementSpec:
    role: str
    desired_angle: float
    target_height: float
    preferred_zone: str
    scale: float
    contour_bias: float = 0.0


A_SPECS = [
    # Five uneven front/lateral communities create the dominant lush gameplay
    # belt. Only two smaller rear masses remain, avoiding a uniform green ring.
    PlacementSpec("A", -2.72, 0.100, "GREEN", 0.82, -0.11),
    PlacementSpec("A", -2.05, 0.130, "GREEN", 0.89, 0.07),
    PlacementSpec("A", -1.52, 0.090, "GREEN", 1.00, -0.03),
    PlacementSpec("A", -0.95, 0.120, "GREEN", 0.93, 0.10),
    PlacementSpec("A", -0.32, 0.155, "GREEN", 0.84, -0.09),
    PlacementSpec("A", 0.96, 0.120, "GREEN", 0.73, 0.08),
    PlacementSpec("A", 2.52, 0.095, "GREEN", 0.77, -0.12),
]

B_SPECS = [
    PlacementSpec("B", -2.48, 0.225, "GREEN", 0.66, 0.16),
    PlacementSpec("B", -1.72, 0.285, "GREEN", 0.59, -0.12),
    PlacementSpec("B", -0.76, 0.235, "GREEN", 0.68, 0.10),
    PlacementSpec("B", 0.33, 0.310, "GREEN", 0.55, -0.15),
    PlacementSpec("B", 1.55, 0.255, "GREEN", 0.62, 0.12),
    PlacementSpec("B", 2.72, 0.290, "GREEN", 0.57, -0.10),
]

C_SPECS = [
    PlacementSpec("C", -2.38, 0.345, "GREEN", 0.60, -0.10),
    PlacementSpec("C", -1.30, 0.400, "SOIL", 0.52, 0.14),
    PlacementSpec("C", -0.22, 0.330, "GREEN", 0.58, -0.13),
    PlacementSpec("C", 1.08, 0.405, "SOIL", 0.50, 0.11),
    PlacementSpec("C", 2.38, 0.360, "GREEN", 0.56, -0.10),
]


def mesh_hash(mesh):
    digest = hashlib.sha256()
    for vertex in mesh.vertices:
        digest.update(struct.pack("3f", *tuple(vertex.co)))
    for polygon in mesh.polygons:
        digest.update(struct.pack(f"{len(polygon.vertices)}I", *polygon.vertices))
    return digest.hexdigest()


def material_signature(obj):
    return tuple(material.name for material in obj.data.materials)


def stable01(*parts):
    payload = "|".join(str(part) for part in parts).encode("utf-8")
    return int.from_bytes(hashlib.sha256(payload).digest()[:8], "big") / float(2**64 - 1)


def angle_difference(a, b):
    return abs((a - b + math.pi) % math.tau - math.pi)


def remove_collection_tree(name):
    collection = bpy.data.collections.get(name)
    if collection is None:
        return
    for child in list(collection.children):
        remove_collection_tree(child.name)
    for obj in list(collection.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(collection)


def child_collection(parent, name):
    collection = bpy.data.collections.new(name)
    parent.children.link(collection)
    return collection


def active_source_members(collection):
    members = []
    for child in collection.children:
        if "FROZEN_MACRO" in child.name or "TRUNKS_ONLY" in child.name or "CROWNS_ONLY" in child.name:
            continue
        for obj in child.objects:
            if obj.type == "MESH" and obj.get("placement_category"):
                members.append(obj)
    return members


def source_fingerprint(source_members):
    return {
        role: {
            "collection": SOURCE_COLLECTIONS[role],
            "object_count": len(members),
            "mesh_hashes": sorted({mesh_hash(obj.data) for obj in members}),
        }
        for role, members in source_members.items()
    }


def build_candidates(sampler, step=2.8):
    candidates = []
    row = 0
    x = sampler.x_min + 1.4
    while x <= sampler.x_max - 1.4:
        y = sampler.y_min + 1.4
        column = 0
        while y <= sampler.y_max - 1.4:
            jitter_x = (stable01(row, column, "x") - 0.5) * step * 0.44
            jitter_y = (stable01(row, column, "y") - 0.5) * step * 0.44
            hit = sampler.hit(x + jitter_x, y + jitter_y)
            if hit is not None:
                candidates.append(hit)
            y += step
            column += 1
        x += step
        row += 1
    return candidates


def choose_anchor(spec, candidates, used):
    if spec.role == "A":
        height_min, height_max, slope_min = 0.035, 0.205, 0.68
    elif spec.role == "B":
        height_min, height_max, slope_min = 0.145, 0.345, 0.56
    else:
        height_min, height_max, slope_min = 0.235, 0.465, 0.49

    options = []
    for hit in candidates:
        if hit.zone == "ROCK" or not (height_min <= hit.height <= height_max) or hit.normal.z < slope_min:
            continue
        angle = math.atan2(hit.point.y, hit.point.x)
        angular = angle_difference(angle, spec.desired_angle)
        if angular > 0.52:
            continue
        zone_penalty = 0.0 if hit.zone == spec.preferred_zone else (0.42 if spec.role == "C" else 1.2)
        if spec.role in {"A", "B"} and hit.zone != "GREEN":
            continue
        minimum_spacing = 22.0 if spec.role == "A" else 15.0 if spec.role == "B" else 12.5
        crowding = min((math.hypot(hit.point.x - other.x, hit.point.y - other.y) for other in used), default=999.0)
        if crowding < minimum_spacing:
            continue
        green_confidence = float(hit.mask[1])
        soil_confidence = float(hit.mask[2])
        confidence = green_confidence if spec.preferred_zone == "GREEN" else soil_confidence
        score = angular * 3.2 + abs(hit.height - spec.target_height) * 5.4 + zone_penalty
        score += max(0.0, 0.72 - hit.normal.z) * 0.65
        score -= confidence * 0.22 + float(hit.cavity) * 0.08
        options.append((score, hit))
    if not options:
        return None
    chosen = min(options, key=lambda item: item[0])[1]
    used.append(chosen.point.copy())
    return chosen


def rotation_for_surface(normal, yaw, alignment_strength):
    up = Vector((0.0, 0.0, 1.0))
    blended = (up * (1.0 - alignment_strength) + normal * alignment_strength).normalized()
    alignment = up.rotation_difference(blended)
    return Quaternion(blended, yaw) @ alignment


def source_planar_yaw(source):
    if source.rotation_mode == "QUATERNION":
        return source.rotation_quaternion.to_euler("XYZ").z
    return source.rotation_euler.z


def member_keep_probability(role, hit, is_tree, source_height):
    if hit.zone == "ROCK" or hit.height >= 0.50:
        return 0.0
    if is_tree:
        if hit.zone != "GREEN" or hit.height >= 0.395:
            return 0.0
        if hit.height < 0.17:
            return 1.0
        if hit.height < 0.27:
            return 0.76 if source_height < 6.5 else 0.58
        if hit.height < 0.35:
            return 0.48 if source_height < 5.7 else 0.18
        return 0.18 if source_height < 4.3 else 0.0
    if hit.zone == "GREEN":
        if hit.height < 0.25:
            return 1.0
        if hit.height < 0.38:
            return 0.84
        return 0.58
    if hit.zone == "SOIL":
        if hit.height < 0.30:
            return 0.46
        if hit.height < 0.42:
            return 0.28
        return 0.15
    return 0.0


def tree_height_factor(height):
    if height < 0.14:
        return 1.08
    if height < 0.24:
        return 0.94
    if height < 0.33:
        return 0.82
    return 0.70


def clone_member(source, destination, name, hit, yaw, member_scale, role, instance_index, anchor_hit):
    obj = bpy.data.objects.new(name, source.data)
    destination.objects.link(obj)
    category = str(source.get("placement_category"))
    is_tree = category == "R61_TreeCanopy"
    alignment = 0.14 if is_tree else 0.78
    obj.location = hit.point + hit.normal * (0.010 if is_tree else 0.006)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = rotation_for_surface(hit.normal, yaw, alignment)
    source_scale = tuple(float(value) for value in source.scale)
    if is_tree:
        height_factor = tree_height_factor(hit.height)
        obj.scale = (
            source_scale[0] * member_scale,
            source_scale[1] * member_scale,
            source_scale[2] * member_scale * height_factor,
        )
    else:
        obj.scale = (
            source_scale[0] * member_scale,
            source_scale[1] * member_scale,
            source_scale[2] * member_scale * 0.92,
        )
    obj.hide_render = False
    obj.hide_viewport = False
    obj["production_source"] = source.name
    obj["cluster_role"] = role
    obj["cluster_instance"] = instance_index
    obj["placement_category"] = category
    obj["placement_mask_zone"] = hit.zone
    obj["height_normalized"] = float(hit.height)
    obj["surface_anchor"] = [float(value) for value in hit.point]
    obj["surface_normal"] = [float(value) for value in hit.normal]
    obj["anchor_mask_zone"] = anchor_hit.zone
    obj["anchor_height_normalized"] = float(anchor_hit.height)
    obj["source_height_m"] = float(source.dimensions.z)
    obj["placed_height_estimate_m"] = float(source.dimensions.z * member_scale * (tree_height_factor(hit.height) if is_tree else 0.92))
    obj["linked_mesh_data"] = True
    return obj


def place_cluster(spec, source_members, source_center, anchor_hit, destination, sampler, instance_index):
    theta = math.atan2(anchor_hit.point.y, anchor_hit.point.x)
    tangent_angle = theta + math.pi * 0.5 + spec.contour_bias
    tangent = Vector((math.cos(tangent_angle), math.sin(tangent_angle)))
    inward = Vector((-math.cos(theta), -math.sin(theta)))
    if spec.role == "A":
        lateral_scale, slope_scale = spec.scale, spec.scale * 0.88
    elif spec.role == "B":
        lateral_scale, slope_scale = spec.scale * 1.12, spec.scale * 0.78
    else:
        lateral_scale, slope_scale = spec.scale, spec.scale * 0.92
    member_scale = math.sqrt(lateral_scale * slope_scale)
    records = []
    rejected = Counter()
    for source_index, source in enumerate(source_members, 1):
        local_x = source.location.x - source_center.x
        local_y = source.location.y - source_center.y
        target_x = anchor_hit.point.x + tangent.x * local_x * lateral_scale + inward.x * local_y * slope_scale
        target_y = anchor_hit.point.y + tangent.y * local_x * lateral_scale + inward.y * local_y * slope_scale
        hit = sampler.hit(target_x, target_y)
        if hit is None:
            rejected["NO_SURFACE"] += 1
            continue
        category = str(source.get("placement_category"))
        is_tree = category == "R61_TreeCanopy"
        probability = member_keep_probability(spec.role, hit, is_tree, float(source.dimensions.z))
        if stable01(spec.role, instance_index, source.name, "keep") > probability:
            rejected["DENSITY_THINNING"] += 1
            continue
        if is_tree and hit.normal.z < 0.48:
            rejected["TREE_SLOPE"] += 1
            continue
        if not is_tree and hit.normal.z < 0.38:
            rejected["LOW_PLANT_SLOPE"] += 1
            continue
        yaw = source_planar_yaw(source) + tangent_angle
        yaw += (stable01(spec.role, instance_index, source.name, "yaw") - 0.5) * 0.28
        variation = 0.93 + stable01(spec.role, instance_index, source.name, "scale") * 0.14
        obj = clone_member(
            source, destination,
            f"R69_{spec.role}_{instance_index:02d}_{len(records) + 1:03d}",
            hit, yaw, member_scale * variation, spec.role, instance_index, anchor_hit,
        )
        records.append(obj)
    return records, rejected


def setup_render_scene(scene, sampler):
    remove_collection_tree(RENDER_SETUP)
    collection = bpy.data.collections.new(RENDER_SETUP)
    scene.collection.children.link(collection)
    for obj in scene.objects:
        if obj.type in {"LIGHT", "CAMERA"}:
            obj.hide_render = True

    camera_data = bpy.data.cameras.new("CAM_R69_ColorZone_Placement")
    camera = bpy.data.objects.new("CAM_R69_ColorZone_Placement", camera_data)
    collection.objects.link(camera)
    scene.camera = camera

    sun_data = bpy.data.lights.new("L_R69_Neutral_Daylight", "SUN")
    sun_data.energy = 2.55
    sun_data.angle = math.radians(8.0)
    sun_data.color = r24.srgb_hex("FFF0D8")[:3]
    sun = bpy.data.objects.new("L_R69_Neutral_Daylight", sun_data)
    collection.objects.link(sun)
    sun.rotation_euler = (math.radians(31), math.radians(-23), math.radians(-38))

    area_data = bpy.data.lights.new("L_R69_Sky_Fill", "AREA")
    area_data.energy = 1500
    area_data.shape = "DISK"
    area_data.size = 120
    area_data.color = r24.srgb_hex("DDE8E8")[:3]
    area = bpy.data.objects.new("L_R69_Sky_Fill", area_data)
    collection.objects.link(area)
    area.location = (-115, -145, 130)
    area.rotation_euler = (Vector((0, 0, 25)) - area.location).to_track_quat("-Z", "Y").to_euler()

    water_material = r24.make_principled_material("M_R69_Water", "708F96", 0.36, 0.04)
    water = r24.make_plane(collection, "R69_Water", 560, sampler.z_min + 0.16, water_material)

    scene.render.engine = "BLENDER_EEVEE"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.resolution_x = 1100 if PREVIEW_MODE else 1600
    scene.render.resolution_y = 619 if PREVIEW_MODE else 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = r24.linear_hex("B8BFBD")
    background.inputs["Strength"].default_value = 0.76
    return camera, water


def point_camera(camera, location, target, lens=55, ortho=None):
    camera.location = location
    camera.rotation_euler = (Vector(target) - Vector(location)).to_track_quat("-Z", "Y").to_euler()
    if ortho is None:
        camera.data.type = "PERSP"
        camera.data.lens = lens
    else:
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = ortho


def render(scene, name):
    RENDER_DIR.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(RENDER_DIR / name)
    bpy.ops.render.render(write_still=True)


def render_views(scene, camera, water, sampler):
    water_z = sampler.z_min + 0.16
    if PREVIEW_MODE:
        point_camera(camera, (0, -255, water_z + 7.5), (0, 0, sampler.z_min + sampler.height * 0.38), 52)
        render(scene, "PREVIEW_01_front.png")
        water.hide_render = True
        point_camera(camera, (0, -2, sampler.z_max + 130), (0, -2, sampler.z_min + 4), ortho=190)
        render(scene, "PREVIEW_02_top.png")
        water.hide_render = False
        return ["PREVIEW_01_front.png", "PREVIEW_02_top.png"]

    views = [
        ("01_front_water_level.png", (0, -255, water_z + 7.5), (0, 0, sampler.z_min + sampler.height * 0.38), 52),
        ("02_three_quarter_overview.png", (145, -218, water_z + 48), (0, 0, sampler.z_min + sampler.height * 0.33), 55),
        ("03_opposite_side_overview.png", (-145, 212, water_z + 45), (0, 0, sampler.z_min + sampler.height * 0.32), 55),
        ("04_gameplay_distance.png", (4, -162, water_z + 5.4), (0, -20, sampler.z_min + 17.0), 58),
    ]
    for name, location, target, lens in views:
        point_camera(camera, location, target, lens)
        render(scene, name)
    water.hide_render = True
    point_camera(camera, (0, -2, sampler.z_max + 130), (0, -2, sampler.z_min + 4), ortho=190)
    render(scene, "05_top_down_distribution.png")
    water.hide_render = False
    return [item[0] for item in views] + ["05_top_down_distribution.png"]


def validate(hero, hero_hash, hero_materials, source_before, source_after, records, summaries):
    errors = []
    rock = [obj.name for obj in records if obj.get("placement_mask_zone") == "ROCK"]
    high_trees = [
        obj.name for obj in records
        if obj.get("placement_category") == "R61_TreeCanopy" and float(obj.get("height_normalized", 0.0)) >= 0.395
    ]
    floating = []
    buried = []
    for obj in records:
        anchor = Vector(obj["surface_anchor"])
        normal = Vector(obj["surface_normal"])
        signed = (obj.location - anchor).dot(normal)
        if signed > 0.025:
            floating.append(obj.name)
        if signed < -0.02:
            buried.append(obj.name)
    if rock:
        errors.append(f"ROCK mask violations: {len(rock)}")
    if high_trees:
        errors.append(f"Trees above transition limit: {len(high_trees)}")
    if floating:
        errors.append(f"Floating placements: {len(floating)}")
    if buried:
        errors.append(f"Buried placements: {len(buried)}")
    if mesh_hash(hero.data) != hero_hash:
        errors.append("Volcano mesh changed")
    if material_signature(hero) != hero_materials:
        errors.append("Volcano materials changed")
    if source_before != source_after:
        errors.append("Cluster source assets changed")

    role_counts = Counter(obj["cluster_role"] for obj in records)
    zone_counts = Counter(obj["placement_mask_zone"] for obj in records)
    category_counts = Counter(obj["placement_category"] for obj in records)
    height_bands = Counter()
    tree_height_bands = Counter()
    tree_heights = defaultdict(list)
    for obj in records:
        height = float(obj["height_normalized"])
        band = "LOWER" if height < 0.20 else "MID" if height < 0.36 else "UPPER_TRANSITION"
        height_bands[band] += 1
        if obj["placement_category"] == "R61_TreeCanopy":
            tree_height_bands[band] += 1
            tree_heights[band].append(float(obj["placed_height_estimate_m"]))
    average_tree_height = {
        band: round(sum(values) / len(values), 3) if values else 0.0
        for band, values in tree_heights.items()
    }
    return {
        "status": "PASS" if not errors else "FAIL",
        "errors": errors,
        "placed_object_count": len(records),
        "cluster_role_object_counts": dict(sorted(role_counts.items())),
        "material_zone_counts": dict(sorted(zone_counts.items())),
        "category_counts": dict(sorted(category_counts.items())),
        "height_band_object_counts": dict(sorted(height_bands.items())),
        "height_band_tree_counts": dict(sorted(tree_height_bands.items())),
        "average_tree_height_m_by_band": average_tree_height,
        "cluster_instances": summaries,
        "rock_mask_violations": len(rock),
        "trees_above_transition_limit": len(high_trees),
        "floating_count": len(floating),
        "buried_count": len(buried),
        "volcano_mesh_unchanged": mesh_hash(hero.data) == hero_hash,
        "volcano_materials_unchanged": material_signature(hero) == hero_materials,
        "cluster_sources_unchanged": source_before == source_after,
    }


def main():
    started = time.perf_counter()
    RENDER_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(INPUT))
    scene = bpy.context.scene
    hero = bpy.data.objects[HERO_NAME]
    hero.hide_render = False
    hero.hide_viewport = False
    hero_hash = mesh_hash(hero.data)
    hero_materials = material_signature(hero)

    source_collections = {role: bpy.data.collections[name] for role, name in SOURCE_COLLECTIONS.items()}
    source_members = {role: active_source_members(collection) for role, collection in source_collections.items()}
    source_centers = {
        role: Vector((
            sum(obj.location.x for obj in members) / len(members),
            sum(obj.location.y for obj in members) / len(members),
        ))
        for role, members in source_members.items()
    }
    source_before = source_fingerprint(source_members)
    for collection in source_collections.values():
        collection.hide_render = True
        collection.hide_viewport = True

    remove_collection_tree(MASTER)
    master = bpy.data.collections.new(MASTER)
    scene.collection.children.link(master)
    placement_root = child_collection(master, PLACEMENT_ROOT)
    role_roots = {role: child_collection(placement_root, f"R69_CLUSTER_{role}_PLACEMENTS") for role in "ABC"}

    sampler = r24.MaterialSurfaceSampler(hero)
    candidates = build_candidates(sampler)
    used_anchors = []
    records = []
    summaries = []
    all_specs = A_SPECS + B_SPECS + C_SPECS
    per_role_index = Counter()
    for spec in all_specs:
        per_role_index[spec.role] += 1
        instance_index = per_role_index[spec.role]
        anchor = choose_anchor(spec, candidates, used_anchors)
        if anchor is None:
            summaries.append({"role": spec.role, "instance": instance_index, "status": "NO_VALID_COLOR_ZONE_ANCHOR"})
            continue
        destination = child_collection(role_roots[spec.role], f"R69_{spec.role}_{instance_index:02d}")
        placed, rejected = place_cluster(
            spec, source_members[spec.role], source_centers[spec.role], anchor,
            destination, sampler, instance_index,
        )
        records.extend(placed)
        summaries.append({
            "role": spec.role,
            "instance": instance_index,
            "status": "PLACED",
            "anchor": [round(value, 3) for value in anchor.point],
            "anchor_zone": anchor.zone,
            "anchor_height": round(float(anchor.height), 4),
            "scale": spec.scale,
            "objects_placed": len(placed),
            "trees_placed": sum(obj["placement_category"] == "R61_TreeCanopy" for obj in placed),
            "low_vegetation_placed": sum(obj["placement_category"] != "R61_TreeCanopy" for obj in placed),
            "rejected": dict(sorted(rejected.items())),
        })

    source_after = source_fingerprint(source_members)
    camera, water = setup_render_scene(scene, sampler)
    point_camera(camera, (0, -255, sampler.z_min + 7.7), (0, 0, sampler.z_min + sampler.height * 0.38), 52)
    scene.render.filepath = ""
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT), check_existing=False)
    render_names = render_views(scene, camera, water, sampler)
    validation = validate(hero, hero_hash, hero_materials, source_before, source_after, records, summaries)

    report = {
        "pass": "R69 COLOR-ZONE-DRIVEN CLUSTER A/B/C VOLCANO PLACEMENT",
        "input_blend": str(INPUT),
        "output_blend": str(OUTPUT),
        "production_collection": MASTER,
        "preview_mode": PREVIEW_MODE,
        "placement_strategy": {
            "surface_priority": "visible GREEN/SOIL/ROCK color zones from existing baked volcano material",
            "Cluster_A": "dense core forest masses on lower GREEN terrain",
            "Cluster_B": "elongated contour-following bands on lower-mid and mid GREEN terrain",
            "Cluster_C": "asymmetric transition communities at GREEN edges and selected SOIL pockets",
            "GREEN": "primary high-density vegetation zone",
            "SOIL": "secondary reduced-density low vegetation zone; no trees accepted on soil",
            "ROCK": "hard zero-placement mask",
            "elevation": "tree retention and placed height decrease continuously with normalized elevation",
        },
        "source_collections": SOURCE_COLLECTIONS,
        "source_object_counts": {role: len(members) for role, members in source_members.items()},
        "validation": validation,
        "renders": render_names,
        "volcano_mesh_changed": False,
        "volcano_material_changed": False,
        "terrain_repainted": False,
        "full_volcano_cluster_instance_counts": dict(per_role_index),
        "blender_errors": validation["errors"],
        "elapsed_seconds": round(time.perf_counter() - started, 3),
    }
    REPORT_PATH.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("R69_COLOR_ZONE_CLUSTER_PLACEMENT_REPORT")
    print(json.dumps(report, indent=2))
    if validation["errors"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
