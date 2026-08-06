from __future__ import annotations

import bpy
import hashlib
import importlib.util
import json
import math
import random
import struct
import sys
import time
from collections import Counter, defaultdict
from pathlib import Path

from mathutils import Quaternion, Vector
from mathutils.kdtree import KDTree


ROOT = Path(__file__).resolve().parent
INPUT = ROOT / "Volcano_Hero_02_ColorZoneClusterPlacement_R69.blend"
OUTPUT = ROOT / "Volcano_Hero_02_ColorZoneTransitionRefinement_R70.blend"
RENDER_DIR = ROOT / "ColorZoneTransitionRefinement_R70_Renders"
REPORT_PATH = RENDER_DIR / "color_zone_transition_refinement_r70_report.json"
R69_SCRIPT = ROOT / "build_color_zone_cluster_placement_r69.py"
HERO_NAME = "Volcano_Hero_02"
R69_MASTER = "R69_COLOR_ZONE_CLUSTER_PLACEMENT"
MASTER = "R70_GAP_FILL_TRANSITION_REFINEMENT"
SEED = 700806


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


r69 = load_module("r69_for_r70", R69_SCRIPT)
r69.RENDER_DIR = RENDER_DIR
r69.PREVIEW_MODE = False


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


def build_kdtree(objects):
    tree = KDTree(len(objects))
    for index, obj in enumerate(objects):
        tree.insert(Vector((obj.location.x, obj.location.y, 0.0)), index)
    tree.balance()
    return tree


def nearest_distance(tree, point):
    _, _, distance = tree.find(Vector((point.x, point.y, 0.0)))
    return float(distance)


def protected_open_channel(hit):
    radius = math.hypot(hit.point.x, hit.point.y)
    if not 25.0 <= radius <= 69.0:
        return False
    angle = math.atan2(hit.point.y, hit.point.x)
    channels = [
        (-2.63, 0.048, 0.6),
        (-1.44, 0.055, 1.7),
        (-0.16, 0.046, 2.5),
        (0.63, 0.050, 3.3),
        (2.08, 0.052, 4.1),
    ]
    for center, width, phase in channels:
        curved = center + math.sin(radius * 0.047 + phase) * 0.045
        if angle_difference(angle, curved) < width:
            return True
    return False


def build_candidates(sampler, existing_tree, existing_all, step=2.35):
    candidates = []
    row = 0
    x = sampler.x_min + 1.2
    while x <= sampler.x_max - 1.2:
        column = 0
        y = sampler.y_min + 1.2
        while y <= sampler.y_max - 1.2:
            jitter_x = (stable01(row, column, "r70x") - 0.5) * step * 0.46
            jitter_y = (stable01(row, column, "r70y") - 0.5) * step * 0.46
            hit = sampler.hit(x + jitter_x, y + jitter_y)
            if hit is not None and hit.zone != "ROCK" and not protected_open_channel(hit):
                candidates.append({
                    "hit": hit,
                    "distance_all": nearest_distance(existing_all, hit.point),
                    "distance_tree": nearest_distance(existing_tree, hit.point),
                    "noise": stable01(row, column, "r70score"),
                })
            y += step
            column += 1
        x += step
        row += 1
    return candidates


def center_band(candidate):
    hit = candidate["hit"]
    if hit.zone == "SOIL" and 0.12 <= hit.height < 0.42:
        return "SOIL_LIGHT"
    if hit.zone == "GREEN" and hit.height < 0.20:
        return "LOWER_GREEN"
    if hit.zone == "GREEN" and hit.height < 0.36:
        return "MID_GREEN"
    if hit.zone in {"GREEN", "SOIL"} and hit.height < 0.475:
        return "UPPER_LOW_ONLY"
    return None


def candidate_score(candidate, band):
    hit = candidate["hit"]
    distance_all = candidate["distance_all"]
    distance_tree = candidate["distance_tree"]
    angle = math.atan2(hit.point.y, hit.point.x)
    frontness = max(0.0, -math.sin(angle))
    if band == "LOWER_GREEN":
        target_all, target_tree = 5.8, 9.2
        score = abs(distance_all - target_all) * 0.55 + abs(distance_tree - target_tree) * 0.18
        score -= frontness * 1.45
    elif band == "MID_GREEN":
        target_all, target_tree = 5.1, 9.0
        score = abs(distance_all - target_all) * 0.58 + abs(distance_tree - target_tree) * 0.16
        score -= frontness * 0.45
    elif band == "SOIL_LIGHT":
        score = abs(distance_all - 4.7) * 0.70 - float(hit.cavity) * 0.25
        score += max(0.0, float(hit.mask[2]) - 0.72) * 0.9
    else:
        score = abs(distance_all - 4.4) * 0.72 + hit.height * 0.65
    return score + (candidate["noise"] - 0.5) * 0.85


def select_centers(candidates):
    targets = {
        "LOWER_GREEN": (14, 7.8),
        "MID_GREEN": (12, 8.8),
        "SOIL_LIGHT": (6, 9.8),
        "UPPER_LOW_ONLY": (5, 11.0),
    }
    selected = []
    selected_by_band = defaultdict(list)
    for band, (limit, spacing) in targets.items():
        options = []
        for candidate in candidates:
            if center_band(candidate) != band:
                continue
            hit = candidate["hit"]
            distance_all = candidate["distance_all"]
            distance_tree = candidate["distance_tree"]
            if band == "LOWER_GREEN":
                valid = 2.6 <= distance_all <= 10.5 and 4.8 <= distance_tree <= 15.0 and hit.normal.z >= 0.52
            elif band == "MID_GREEN":
                valid = 2.3 <= distance_all <= 9.5 and 4.2 <= distance_tree <= 14.5 and hit.normal.z >= 0.46
            elif band == "SOIL_LIGHT":
                sheltered = float(hit.mask[1]) > 0.035 or float(hit.cavity) > 0.025
                valid = 2.4 <= distance_all <= 8.5 and hit.normal.z >= 0.45 and sheltered
            else:
                valid = 2.4 <= distance_all <= 8.0 and hit.normal.z >= 0.44
            if valid:
                options.append((candidate_score(candidate, band), candidate))
        for _, candidate in sorted(options, key=lambda item: item[0]):
            point = candidate["hit"].point
            if any(math.hypot(point.x - other["hit"].point.x, point.y - other["hit"].point.y) < spacing for other in selected):
                continue
            selected.append(candidate)
            selected_by_band[band].append(candidate)
            if len(selected_by_band[band]) >= limit:
                break
    return selected, selected_by_band


def source_pools():
    collection = bpy.data.collections[r69.SOURCE_COLLECTIONS["A"]]
    members = r69.active_source_members(collection)
    small = [obj for obj in members if obj.get("placement_category") == "R61_TreeCanopy" and obj.get("tree_variant") == "TREE_E"]
    medium = [obj for obj in members if obj.get("placement_category") == "R61_TreeCanopy" and obj.get("tree_variant") in {"TREE_C", "TREE_D"}]
    bushes = [obj for obj in members if "Bush" in str(obj.get("placement_category"))]
    low = [
        obj for obj in members
        if obj.get("placement_category") not in {"R61_TreeCanopy"}
        and "Bush" not in str(obj.get("placement_category"))
    ]
    if not all((small, medium, bushes, low)):
        raise RuntimeError("Missing one or more approved R70 source pools")
    return {"SMALL_TREE": small, "MEDIUM_TREE": medium, "BUSH": bushes, "LOW": low}


def rotation_for_surface(normal, yaw, is_tree):
    up = Vector((0.0, 0.0, 1.0))
    strength = 0.14 if is_tree else 0.80
    blended = (up * (1.0 - strength) + normal * strength).normalized()
    return Quaternion(blended, yaw) @ up.rotation_difference(blended)


def place_linked(source, destination, name, hit, scale_factor, patch_id, band, source_role):
    obj = bpy.data.objects.new(name, source.data)
    destination.objects.link(obj)
    is_tree = source_role in {"SMALL_TREE", "MEDIUM_TREE"}
    obj.location = hit.point + hit.normal * (0.009 if is_tree else 0.005)
    obj.rotation_mode = "QUATERNION"
    yaw = stable01(name, "yaw") * math.tau
    obj.rotation_quaternion = rotation_for_surface(hit.normal, yaw, is_tree)
    source_scale = tuple(float(value) for value in source.scale)
    height_factor = 1.0
    if source_role == "SMALL_TREE":
        height_factor = 1.03 if hit.height < 0.18 else 0.88
    elif source_role == "MEDIUM_TREE":
        height_factor = 0.94 if hit.height < 0.18 else 0.80
    elif not is_tree:
        height_factor = 0.90
    obj.scale = (
        source_scale[0] * scale_factor,
        source_scale[1] * scale_factor,
        source_scale[2] * scale_factor * height_factor,
    )
    obj.hide_render = False
    obj.hide_viewport = False
    obj["production_source"] = source.name
    obj["r70_source_role"] = source_role
    obj["placement_category"] = source.get("placement_category")
    obj["transition_band"] = band
    obj["satellite_patch_id"] = patch_id
    obj["placement_mask_zone"] = hit.zone
    obj["height_normalized"] = float(hit.height)
    obj["surface_anchor"] = [float(value) for value in hit.point]
    obj["surface_normal"] = [float(value) for value in hit.normal]
    obj["source_height_m"] = float(source.dimensions.z)
    obj["placed_height_estimate_m"] = float(source.dimensions.z * scale_factor * height_factor)
    obj["linked_mesh_data"] = True
    obj["placement_intent"] = "gap fill and soft irregular transition; not a new major cluster"
    return obj


def local_hit(sampler, center, radius, rng):
    angle = rng.uniform(0.0, math.tau)
    distance = radius * math.sqrt(rng.random())
    return sampler.hit(center.point.x + math.cos(angle) * distance, center.point.y + math.sin(angle) * distance)


def place_patch(candidate, patch_id, pools, destination, sampler, occupied, rng):
    center = candidate["hit"]
    band = center_band(candidate)
    if band == "LOWER_GREEN":
        counts = {"SMALL_TREE": rng.randint(2, 4), "MEDIUM_TREE": rng.randint(0, 1), "BUSH": rng.randint(4, 6), "LOW": rng.randint(6, 9)}
        radius = rng.uniform(4.2, 6.4)
    elif band == "MID_GREEN":
        counts = {"SMALL_TREE": rng.randint(1, 3), "MEDIUM_TREE": rng.randint(0, 1), "BUSH": rng.randint(3, 5), "LOW": rng.randint(5, 8)}
        radius = rng.uniform(3.8, 5.8)
    elif band == "SOIL_LIGHT":
        counts = {"SMALL_TREE": 0, "MEDIUM_TREE": 0, "BUSH": rng.randint(1, 2), "LOW": rng.randint(3, 5)}
        radius = rng.uniform(3.2, 5.0)
    else:
        counts = {"SMALL_TREE": 0, "MEDIUM_TREE": 0, "BUSH": rng.randint(1, 2), "LOW": rng.randint(3, 5)}
        radius = rng.uniform(3.0, 4.8)

    records = []
    rejected = Counter()
    for role in ("SMALL_TREE", "MEDIUM_TREE", "BUSH", "LOW"):
        target = counts[role]
        attempts = 0
        while sum(obj["r70_source_role"] == role for obj in records) < target and attempts < max(10, target * 8):
            attempts += 1
            hit = local_hit(sampler, center, radius, rng)
            if hit is None or hit.zone == "ROCK" or protected_open_channel(hit):
                rejected["SURFACE_OR_CHANNEL"] += 1
                continue
            is_tree = role in {"SMALL_TREE", "MEDIUM_TREE"}
            if is_tree and (hit.zone != "GREEN" or hit.height >= 0.345 or hit.normal.z < 0.48):
                rejected["TREE_ZONE_HEIGHT_SLOPE"] += 1
                continue
            if not is_tree and (hit.height >= 0.475 or hit.normal.z < 0.40):
                rejected["LOW_ZONE_HEIGHT_SLOPE"] += 1
                continue
            if center.zone == "SOIL" and hit.zone == "GREEN" and rng.random() > 0.45:
                rejected["SOIL_PATCH_CROSSING"] += 1
                continue
            minimum = 2.4 if is_tree else 1.15 if role == "BUSH" else 0.72
            if any(math.hypot(hit.point.x - point.x, hit.point.y - point.y) < minimum for point in occupied):
                rejected["LOCAL_SPACING"] += 1
                continue
            source = rng.choice(pools[role])
            if role == "SMALL_TREE":
                scale = rng.uniform(0.78, 1.02) if band == "LOWER_GREEN" else rng.uniform(0.66, 0.88)
            elif role == "MEDIUM_TREE":
                scale = rng.uniform(0.68, 0.84) if band == "LOWER_GREEN" else rng.uniform(0.56, 0.72)
            elif role == "BUSH":
                scale = rng.uniform(0.72, 1.00) if hit.zone == "GREEN" else rng.uniform(0.62, 0.82)
            else:
                scale = rng.uniform(0.72, 1.05) if hit.zone == "GREEN" else rng.uniform(0.60, 0.82)
            obj = place_linked(
                source, destination, f"R70_{patch_id:02d}_{role}_{len(records) + 1:02d}",
                hit, scale, patch_id, band, role,
            )
            records.append(obj)
            occupied.append(hit.point.copy())
    return records, rejected


def validate(hero, hero_hash, hero_materials, r69_count_before, r69_count_after, records, centers_by_band):
    errors = []
    rock = [obj.name for obj in records if obj["placement_mask_zone"] == "ROCK"]
    soil_trees = [obj.name for obj in records if obj["r70_source_role"] in {"SMALL_TREE", "MEDIUM_TREE"} and obj["placement_mask_zone"] != "GREEN"]
    upper_trees = [obj.name for obj in records if obj["r70_source_role"] in {"SMALL_TREE", "MEDIUM_TREE"} and float(obj["height_normalized"]) >= 0.345]
    tall_additions = [obj.name for obj in records if obj["r70_source_role"] in {"SMALL_TREE", "MEDIUM_TREE"} and float(obj["placed_height_estimate_m"]) > 4.8]
    floating = []
    buried = []
    for obj in records:
        anchor = Vector(obj["surface_anchor"])
        normal = Vector(obj["surface_normal"])
        signed = (obj.location - anchor).dot(normal)
        if signed > 0.023:
            floating.append(obj.name)
        if signed < -0.018:
            buried.append(obj.name)
    if mesh_hash(hero.data) != hero_hash:
        errors.append("Volcano mesh changed")
    if material_signature(hero) != hero_materials:
        errors.append("Volcano materials changed")
    if r69_count_before != r69_count_after:
        errors.append("Existing R69 placement object count changed")
    if rock:
        errors.append(f"ROCK additions: {len(rock)}")
    if soil_trees:
        errors.append(f"Trees added outside GREEN: {len(soil_trees)}")
    if upper_trees:
        errors.append(f"Trees added above mid-height limit: {len(upper_trees)}")
    if tall_additions:
        errors.append(f"Overly tall refinement trees: {len(tall_additions)}")
    if floating:
        errors.append(f"Floating additions: {len(floating)}")
    if buried:
        errors.append(f"Buried additions: {len(buried)}")

    role_counts = Counter(obj["r70_source_role"] for obj in records)
    zone_counts = Counter(obj["placement_mask_zone"] for obj in records)
    band_counts = Counter(obj["transition_band"] for obj in records)
    tree_band_counts = Counter(
        obj["transition_band"] for obj in records if obj["r70_source_role"] in {"SMALL_TREE", "MEDIUM_TREE"}
    )
    return {
        "status": "PASS" if not errors else "FAIL",
        "errors": errors,
        "added_object_count": len(records),
        "source_role_counts": dict(sorted(role_counts.items())),
        "material_zone_counts": dict(sorted(zone_counts.items())),
        "transition_band_counts": dict(sorted(band_counts.items())),
        "tree_counts_by_transition_band": dict(sorted(tree_band_counts.items())),
        "satellite_patch_counts": {band: len(items) for band, items in centers_by_band.items()},
        "rock_addition_count": len(rock),
        "soil_tree_count": len(soil_trees),
        "upper_tree_count": len(upper_trees),
        "overly_tall_tree_count": len(tall_additions),
        "floating_count": len(floating),
        "buried_count": len(buried),
        "volcano_mesh_unchanged": mesh_hash(hero.data) == hero_hash,
        "volcano_materials_unchanged": material_signature(hero) == hero_materials,
        "r69_distribution_preserved": r69_count_before == r69_count_after,
    }


def main():
    started = time.perf_counter()
    RENDER_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(INPUT))
    scene = bpy.context.scene
    hero = bpy.data.objects[HERO_NAME]
    hero_hash = mesh_hash(hero.data)
    hero_materials = material_signature(hero)
    r69_master = bpy.data.collections[R69_MASTER]
    existing_records = [obj for obj in r69_master.all_objects if obj.get("cluster_role") in {"A", "B", "C"}]
    existing_trees = [obj for obj in existing_records if obj.get("placement_category") == "R61_TreeCanopy"]
    r69_count_before = len(existing_records)

    remove_collection_tree(MASTER)
    master = bpy.data.collections.new(MASTER)
    scene.collection.children.link(master)
    additions = child_collection(master, "R70_IRREGULAR_SATELLITE_PATCHES")

    sampler = r69.r24.MaterialSurfaceSampler(hero)
    candidates = build_candidates(sampler, build_kdtree(existing_trees), build_kdtree(existing_records))
    centers, centers_by_band = select_centers(candidates)
    pools = source_pools()
    rng = random.Random(SEED)
    occupied = [obj.location.copy() for obj in existing_records]
    records = []
    patch_summaries = []
    for patch_id, candidate in enumerate(centers, 1):
        patch_collection = child_collection(additions, f"R70_SATELLITE_{patch_id:02d}_{center_band(candidate)}")
        placed, rejected = place_patch(candidate, patch_id, pools, patch_collection, sampler, occupied, rng)
        records.extend(placed)
        hit = candidate["hit"]
        patch_summaries.append({
            "patch_id": patch_id,
            "band": center_band(candidate),
            "center": [round(value, 3) for value in hit.point],
            "center_zone": hit.zone,
            "center_height": round(float(hit.height), 4),
            "nearest_existing_vegetation_m": round(candidate["distance_all"], 3),
            "nearest_existing_tree_m": round(candidate["distance_tree"], 3),
            "objects_placed": len(placed),
            "source_role_counts": dict(Counter(obj["r70_source_role"] for obj in placed)),
            "rejected": dict(sorted(rejected.items())),
        })

    r69_count_after = len([obj for obj in r69_master.all_objects if obj.get("cluster_role") in {"A", "B", "C"}])
    validation = validate(hero, hero_hash, hero_materials, r69_count_before, r69_count_after, records, centers_by_band)
    camera = bpy.data.objects["CAM_R69_ColorZone_Placement"]
    water = bpy.data.objects["R69_Water"]
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.filepath = ""
    r69.point_camera(camera, (0, -255, sampler.z_min + 7.7), (0, 0, sampler.z_min + sampler.height * 0.38), 52)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT), check_existing=False, compress=True)
    render_names = r69.render_views(scene, camera, water, sampler)

    report = {
        "pass": "R70 COLOR-ZONE GAP FILL AND NATURAL TRANSITION REFINEMENT",
        "input_blend": str(INPUT),
        "output_blend": str(OUTPUT),
        "preserved_distribution_collection": R69_MASTER,
        "new_refinement_collection": MASTER,
        "strategy": {
            "existing_large_scale_distribution": "preserved unchanged",
            "lower_green": "small trees, restrained medium trees, bushes, and low vegetation in irregular edge/gap satellites",
            "mid_green": "smaller trees and shrub/low transitions; reduced height and density",
            "soil": "lighter bushes and low vegetation only; no added trees",
            "upper_transition": "sparse low vegetation only",
            "rock": "hard exclusion",
            "intentional_openings": "five curved terrain/drainage channels preserved",
            "excluded_tree_sources": ["TREE_A", "TREE_B", "TREE_F"],
        },
        "candidate_count": len(candidates),
        "selected_satellite_patch_count": len(centers),
        "patch_summaries": patch_summaries,
        "validation": validation,
        "renders": render_names,
        "blender_errors": validation["errors"],
        "elapsed_seconds": round(time.perf_counter() - started, 3),
    }
    REPORT_PATH.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("R70_TRANSITION_REFINEMENT_REPORT")
    print(json.dumps(report, indent=2))
    if validation["errors"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
