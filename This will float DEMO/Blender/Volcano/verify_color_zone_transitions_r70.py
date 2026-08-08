from __future__ import annotations

import bpy
import hashlib
import importlib.util
import json
import struct
import sys
from collections import Counter
from pathlib import Path

from mathutils import Vector


ROOT = Path(__file__).resolve().parent
INPUT = ROOT / "Volcano_Hero_02_ColorZoneClusterPlacement_R69.blend"
OUTPUT = ROOT / "Volcano_Hero_02_ColorZoneTransitionRefinement_R70.blend"
RENDER_DIR = ROOT / "ColorZoneTransitionRefinement_R70_Renders"
REPORT_PATH = RENDER_DIR / "color_zone_transition_refinement_r70_report.json"
VERIFY_PATH = RENDER_DIR / "color_zone_transition_refinement_r70_verification.json"
R69_SCRIPT = ROOT / "build_color_zone_cluster_placement_r69.py"
HERO = "Volcano_Hero_02"
R69_MASTER = "R69_COLOR_ZONE_CLUSTER_PLACEMENT"
R70_MASTER = "R70_GAP_FILL_TRANSITION_REFINEMENT"


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


r69 = load_module("r69_for_r70_verification", R69_SCRIPT)


def mesh_hash(mesh):
    digest = hashlib.sha256()
    for vertex in mesh.vertices:
        digest.update(struct.pack("3f", *tuple(vertex.co)))
    for polygon in mesh.polygons:
        digest.update(struct.pack(f"{len(polygon.vertices)}I", *polygon.vertices))
    return digest.hexdigest()


def material_signature(obj):
    return tuple(material.name for material in obj.data.materials)


def placement_fingerprint(collection):
    digest = hashlib.sha256()
    objects = sorted(
        (obj for obj in collection.all_objects if obj.get("cluster_role") in {"A", "B", "C"}),
        key=lambda obj: obj.name,
    )
    for obj in objects:
        digest.update(obj.name.encode("utf-8"))
        digest.update(obj.data.name.encode("utf-8"))
        digest.update(struct.pack("3f", *tuple(obj.location)))
        digest.update(struct.pack("3f", *tuple(obj.scale)))
        digest.update(str(obj.get("cluster_role")).encode("utf-8"))
        digest.update(str(obj.get("cluster_instance")).encode("utf-8"))
    return len(objects), digest.hexdigest()


errors = []


def require(condition, message):
    if not condition:
        errors.append(message)


bpy.ops.wm.open_mainfile(filepath=str(INPUT))
input_hero = bpy.data.objects[HERO]
input_hash = mesh_hash(input_hero.data)
input_materials = material_signature(input_hero)
input_count, input_placement_hash = placement_fingerprint(bpy.data.collections[R69_MASTER])

bpy.ops.wm.open_mainfile(filepath=str(OUTPUT))
hero = bpy.data.objects[HERO]
r69_master = bpy.data.collections.get(R69_MASTER)
r70_master = bpy.data.collections.get(R70_MASTER)
require(r69_master is not None, "Missing preserved R69 placement")
require(r70_master is not None, "Missing R70 refinement collection")
output_count, output_placement_hash = placement_fingerprint(r69_master)
require(output_count == input_count == 1668, f"R69 count changed: input {input_count}, output {output_count}")
require(output_placement_hash == input_placement_hash, "R69 placement transform/data fingerprint changed")
require(mesh_hash(hero.data) == input_hash, "Volcano mesh changed")
require(material_signature(hero) == input_materials, "Volcano material slots changed")

records = [obj for obj in (r70_master.all_objects if r70_master else []) if obj.get("r70_source_role")]
role_counts = Counter(obj["r70_source_role"] for obj in records)
zone_counts = Counter(obj["placement_mask_zone"] for obj in records)
band_counts = Counter(obj["transition_band"] for obj in records)
patch_ids = {int(obj["satellite_patch_id"]) for obj in records}
require(len(records) == 430, f"Expected 430 additions, found {len(records)}")
require(role_counts == Counter({"LOW": 228, "BUSH": 133, "SMALL_TREE": 61, "MEDIUM_TREE": 8}),
        f"Unexpected source-role counts: {dict(role_counts)}")
require(zone_counts == Counter({"GREEN": 391, "SOIL": 39}), f"Unexpected material-zone counts: {dict(zone_counts)}")
require(len(patch_ids) == 37, f"Expected 37 satellite patches, found {len(patch_ids)}")

sampler = r69.r24.MaterialSurfaceSampler(hero)
rock = []
surface_mismatch = []
soil_trees = []
upper_trees = []
tall_trees = []
floating = []
buried = []
for obj in records:
    anchor = Vector(obj["surface_anchor"])
    normal = Vector(obj["surface_normal"])
    hit = sampler.hit(anchor.x, anchor.y)
    if hit is None or hit.zone != obj["placement_mask_zone"]:
        surface_mismatch.append(obj.name)
    if obj["placement_mask_zone"] == "ROCK" or (hit is not None and hit.zone == "ROCK"):
        rock.append(obj.name)
    is_tree = obj["r70_source_role"] in {"SMALL_TREE", "MEDIUM_TREE"}
    if is_tree and obj["placement_mask_zone"] != "GREEN":
        soil_trees.append(obj.name)
    if is_tree and float(obj["height_normalized"]) >= 0.345:
        upper_trees.append(obj.name)
    if is_tree and float(obj["placed_height_estimate_m"]) > 4.8:
        tall_trees.append(obj.name)
    signed = (obj.location - anchor).dot(normal)
    if signed > 0.023:
        floating.append(obj.name)
    if signed < -0.018:
        buried.append(obj.name)

require(not rock, f"ROCK additions found: {rock[:8]}")
require(not surface_mismatch, f"Surface-zone mismatches found: {surface_mismatch[:8]}")
require(not soil_trees, f"Trees on soil found: {soil_trees[:8]}")
require(not upper_trees, f"Upper transition trees found: {upper_trees[:8]}")
require(not tall_trees, f"Overly tall additions found: {tall_trees[:8]}")
require(not floating, f"Floating additions found: {floating[:8]}")
require(not buried, f"Buried additions found: {buried[:8]}")

report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
require(not report.get("blender_errors"), f"Build report errors: {report.get('blender_errors')}")
require(report["validation"]["status"] == "PASS", "Build validation did not pass")
require(report["validation"]["r69_distribution_preserved"], "Build report does not preserve R69")
require(report["strategy"]["excluded_tree_sources"] == ["TREE_A", "TREE_B", "TREE_F"],
        "Tall-tree exclusion metadata is incorrect")

render_names = [
    "01_front_water_level.png",
    "02_three_quarter_overview.png",
    "03_opposite_side_overview.png",
    "04_gameplay_distance.png",
    "05_top_down_distribution.png",
]
render_hashes = {}
render_dimensions = {}
for name in render_names:
    path = RENDER_DIR / name
    require(path.exists() and path.stat().st_size > 150_000, f"Missing/small render: {name}")
    if path.exists():
        render_hashes[name] = hashlib.sha256(path.read_bytes()).hexdigest()
        image = bpy.data.images.load(str(path), check_existing=False)
        render_dimensions[name] = tuple(image.size)
        bpy.data.images.remove(image)
require(len(set(render_hashes.values())) == 5, "Duplicate validation renders found")
require(all(size == (1600, 900) for size in render_dimensions.values()),
        f"Unexpected render dimensions: {render_dimensions}")

verification = {
    "pass": "R70 independent color-zone transition refinement verification",
    "status": "PASS" if not errors else "FAIL",
    "errors": errors,
    "volcano_mesh_unchanged": mesh_hash(hero.data) == input_hash,
    "volcano_materials_unchanged": material_signature(hero) == input_materials,
    "r69_placement_count": output_count,
    "r69_transform_data_fingerprint_preserved": output_placement_hash == input_placement_hash,
    "r70_added_object_count": len(records),
    "source_role_counts": dict(role_counts),
    "material_zone_counts": dict(zone_counts),
    "transition_band_counts": dict(band_counts),
    "satellite_patch_count": len(patch_ids),
    "rock_additions": len(rock),
    "surface_zone_mismatches": len(surface_mismatch),
    "soil_trees": len(soil_trees),
    "upper_transition_trees": len(upper_trees),
    "overly_tall_trees": len(tall_trees),
    "floating_additions": len(floating),
    "buried_additions": len(buried),
    "render_dimensions": {name: list(size) for name, size in render_dimensions.items()},
    "render_sha256": render_hashes,
}
VERIFY_PATH.write_text(json.dumps(verification, indent=2), encoding="utf-8")
print(json.dumps(verification, indent=2))
raise SystemExit(1 if errors else 0)
