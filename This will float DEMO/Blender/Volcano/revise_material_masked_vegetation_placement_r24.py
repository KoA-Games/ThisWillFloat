from __future__ import annotations

import hashlib
import json
import math
import random
import struct
import time
from array import array
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path

import bpy
from mathutils import Quaternion, Vector
from mathutils.kdtree import KDTree


ROOT = Path(__file__).resolve().parent
INPUT_BLEND = ROOT / "Volcano_Hero_02_MidPoly_R3_PlacementTest_R23.blend"
OUTPUT_BLEND = ROOT / "Volcano_Hero_02_MaterialMasked_VegetationPlacement_R24.blend"
RENDER_DIR = ROOT / "MaterialMasked_VegetationPlacement_R24_Renders"
REPORT_PATH = RENDER_DIR / "material_masked_vegetation_placement_r24_report.json"

HERO_NAME = "Volcano_Hero_02"
SOURCE_COLLECTION = "MidPoly_R3_Placement_Sources"
OLD_PLACEMENT_COLLECTION = "R22_Lush_Lower_Slope_Vegetation"
MASTER_COLLECTION = "R24_MaterialMasked_Vegetation"
PREVIEW_COLLECTION = "R24_MaterialMasked_Preview"
DIAGNOSTIC_COLLECTION = "R24_PlacementMask_Diagnostic"
MASK_ATTRIBUTE = "VolcanoNaturalZoneWeightsRGBA"
CAVITY_ATTRIBUTE = "VolcanoPass04_CavityPreview"
SEED = 240803
GRID_SPACING = 1.22
CONTACT_OFFSET = 0.012

ASSET_NAMES = {
    "Groundcover": [
        "VK_Groundcover_A_Dense_Low",
        "VK_Groundcover_B_Moss_Mound",
        "VK_Groundcover_C_Broad_Leaf",
        "VK_Groundcover_D_Slope_Fan",
        "VK_Groundcover_E_Elongated_Sparse",
    ],
    "Bush": [
        "VK_Bush_A_Rounded",
        "VK_Bush_B_Upright",
        "VK_Bush_C_Tiered",
        "VK_Bush_D_Asymmetric_Tall",
    ],
    "Fern": [
        "VK_Fern_A_Low_Fan",
        "VK_Fern_B_Wide_Arch",
        "VK_Fern_C_Young_Upright",
    ],
    "Grass": [
        "VG_Grass_Clump_Short_Dense_A",
        "VG_Grass_Clump_Short_Sparse_A",
        "VG_Grass_Clump_Medium_A",
        "VG_Grass_Clump_Leaning_A",
    ],
}

SLOPE_NORMAL_Z = {
    "Groundcover": 0.50,
    "Grass": 0.56,
    "Bush": 0.62,
    "Fern": 0.70,
}


@dataclass
class SurfaceHit:
    point: Vector
    normal: Vector
    height: float
    face_index: int
    mask: tuple[float, float, float, float]
    albedo: tuple[float, float, float]
    cavity: float
    zone: str


@dataclass
class Candidate:
    index: int
    x: float
    y: float
    hit: SurfaceHit


@dataclass
class Community:
    identifier: str
    zone: str
    center: Vector
    radius_x: float
    radius_y: float
    angle: float
    profile: str
    featured: bool = False


@dataclass
class InstanceRecord:
    obj: bpy.types.Object
    category: str
    source_name: str
    zone: str
    community_id: str
    anchor: Vector
    normal: Vector
    mask: tuple[float, float, float, float]
    yaw: float
    radius_x: float
    radius_y: float


def mesh_hash(mesh):
    digest = hashlib.sha256()
    for vertex in mesh.vertices:
        digest.update(struct.pack("3f", *tuple(vertex.co)))
    for polygon in mesh.polygons:
        digest.update(struct.pack(f"{len(polygon.vertices)}I", *polygon.vertices))
    return digest.hexdigest()


def srgb_hex(value):
    value = value.lstrip("#")
    return tuple(int(value[index:index + 2], 16) / 255.0 for index in (0, 2, 4)) + (1.0,)


def linear_hex(value):
    color = srgb_hex(value)
    linear = []
    for channel in color[:3]:
        linear.append(channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4)
    return tuple(linear) + (1.0,)


def remove_collection_tree(name):
    root = bpy.data.collections.get(name)
    if root is None:
        return 0
    objects = set(root.all_objects)
    children = []

    def collect(collection):
        for child in collection.children:
            collect(child)
            children.append(child)

    collect(root)
    for obj in objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    for child in children:
        if bpy.data.collections.get(child.name):
            bpy.data.collections.remove(child)
    bpy.data.collections.remove(root)
    return len(objects)


def new_child_collection(parent, name):
    collection = bpy.data.collections.new(name)
    parent.children.link(collection)
    return collection


def canonical_source_map():
    collection = bpy.data.collections.get(SOURCE_COLLECTION)
    if collection is None:
        raise RuntimeError(f"Missing source collection: {SOURCE_COLLECTION}")
    result = {}
    for obj in collection.objects:
        canonical = obj.get("canonical_asset_name")
        if canonical in {name for values in ASSET_NAMES.values() for name in values}:
            result[canonical] = obj
    expected = {name for values in ASSET_NAMES.values() for name in values}
    missing = sorted(expected - set(result))
    if missing:
        raise RuntimeError(f"Missing R3 source assets: {missing}")
    collection.hide_render = True
    collection.hide_viewport = True
    return result, collection


def mask_zone(mask, albedo=None, height_normalized=None):
    """Classify the visible material first, with the semantic channels enforcing hard summit/rock safety."""
    if albedo is None:
        dominant = max(range(4), key=lambda index: mask[index])
        return ("ROCK", "GREEN", "SOIL", "ROCK")[dominant]

    rock, vegetation, soil, summit = mask
    red, green, blue = albedo
    luminance = red * 0.2126 + green * 0.7152 + blue * 0.0722
    dominant = max(range(4), key=lambda index: mask[index])

    # Summit and the uppermost exposed belt stay absolute even where channels blend.
    if dominant == 3 or (height_normalized is not None and height_normalized >= 0.76):
        return "ROCK"
    if luminance < 0.305 and (summit > 0.05 or rock >= max(vegetation, soil) * 0.90):
        return "ROCK"
    if dominant == 0:
        # The legacy R channel also covers the low olive coastal skirt. Only that low visible belt is reclassified.
        if height_normalized is not None and height_normalized < 0.30 and green >= blue * 1.08:
            return "GREEN"
        return "ROCK"
    if dominant == 1:
        return "GREEN"
    if dominant == 2:
        return "SOIL"
    return "ROCK"


class MaterialSurfaceSampler:
    def __init__(self, obj):
        self.obj = obj
        self.inverse = obj.matrix_world.inverted()
        world_points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        self.z_min = min(point.z for point in world_points)
        self.z_max = max(point.z for point in world_points)
        self.height = self.z_max - self.z_min
        self.x_min = min(point.x for point in world_points)
        self.x_max = max(point.x for point in world_points)
        self.y_min = min(point.y for point in world_points)
        self.y_max = max(point.y for point in world_points)

        mask_attr = obj.data.color_attributes.get(MASK_ATTRIBUTE)
        if mask_attr is None or mask_attr.domain != "POINT":
            raise RuntimeError(f"Missing POINT color attribute: {MASK_ATTRIBUTE}")
        cavity_attr = obj.data.color_attributes.get(CAVITY_ATTRIBUTE)
        material = obj.data.materials[0]
        albedo_node = material.node_tree.nodes.get("R20_BakedAlbedo") if material and material.use_nodes else None
        albedo_image = albedo_node.image if albedo_node and albedo_node.image else None
        uv_layer = obj.data.uv_layers.active
        if albedo_image is None or uv_layer is None:
            raise RuntimeError("The visible R20 baked albedo image or active volcano UV layer is missing")
        albedo_image.colorspace_settings.name = albedo_image.colorspace_settings.name
        image_width, image_height = tuple(albedo_image.size)
        image_pixels = array("f", [0.0]) * (image_width * image_height * 4)
        albedo_image.pixels.foreach_get(image_pixels)

        def sample_albedo(u, v):
            u = u % 1.0
            v = v % 1.0
            px = min(image_width - 1, max(0, int(u * (image_width - 1) + 0.5)))
            py = min(image_height - 1, max(0, int(v * (image_height - 1) + 0.5)))
            offset = (py * image_width + px) * 4
            return tuple(float(image_pixels[offset + channel]) for channel in range(3))

        self.face_masks = []
        self.face_albedo = []
        self.face_height = []
        self.face_cavity = []
        self.rock_tree = None
        for polygon in obj.data.polygons:
            vertices = tuple(polygon.vertices)
            inverse_count = 1.0 / len(vertices)
            self.face_masks.append(tuple(
                sum(mask_attr.data[index].color[channel] for index in vertices) * inverse_count
                for channel in range(4)
            ))
            loop_uvs = [uv_layer.data[loop_index].uv for loop_index in polygon.loop_indices]
            # Faces are small enough that the mean loop UV accurately tracks the rendered material color.
            mean_u = sum(uv.x for uv in loop_uvs) / len(loop_uvs)
            mean_v = sum(uv.y for uv in loop_uvs) / len(loop_uvs)
            self.face_albedo.append(sample_albedo(mean_u, mean_v))
            face_world_z = sum((obj.matrix_world @ obj.data.vertices[index].co).z for index in vertices) * inverse_count
            self.face_height.append((face_world_z - self.z_min) / max(self.height, 1e-8))
            if cavity_attr is None:
                cavity = 0.0
            else:
                cavity = sum(cavity_attr.data[index].color[0] for index in vertices) * inverse_count
            self.face_cavity.append(float(cavity))

    def build_rock_index(self, candidates):
        rock_candidates = [candidate for candidate in candidates if candidate.hit.zone == "ROCK"]
        tree = KDTree(len(rock_candidates))
        for index, candidate in enumerate(rock_candidates):
            tree.insert(Vector((candidate.x, candidate.y, 0.0)), index)
        tree.balance()
        self.rock_tree = tree

    def hit(self, x, y):
        origin_world = Vector((x, y, self.z_max + 12.0))
        direction_world = Vector((0.0, 0.0, -1.0))
        origin_local = self.inverse @ origin_world
        direction_local = (self.inverse.to_3x3() @ direction_world).normalized()
        success, location, normal, face_index = self.obj.ray_cast(origin_local, direction_local)
        if not success:
            return None
        point_world = self.obj.matrix_world @ location
        normal_world = (self.obj.matrix_world.to_3x3() @ normal).normalized()
        height_normalized = (point_world.z - self.z_min) / max(self.height, 1e-8)
        mask = self.face_masks[face_index]
        albedo = self.face_albedo[face_index]
        return SurfaceHit(
            point_world,
            normal_world,
            height_normalized,
            face_index,
            mask,
            albedo,
            self.face_cavity[face_index],
            mask_zone(mask, albedo, height_normalized),
        )


def hash01(x, y, seed):
    value = (int(x) * 374761393 + int(y) * 668265263 + seed * 1442695041) & 0xFFFFFFFF
    value = (value ^ (value >> 13)) * 1274126177 & 0xFFFFFFFF
    value ^= value >> 16
    return value / 0xFFFFFFFF


def smoothstep(value):
    return value * value * (3.0 - 2.0 * value)


def value_noise(x, y, scale, seed):
    px = x / scale
    py = y / scale
    ix = math.floor(px)
    iy = math.floor(py)
    fx = smoothstep(px - ix)
    fy = smoothstep(py - iy)
    a = hash01(ix, iy, seed)
    b = hash01(ix + 1, iy, seed)
    c = hash01(ix, iy + 1, seed)
    d = hash01(ix + 1, iy + 1, seed)
    ab = a * (1.0 - fx) + b * fx
    cd = c * (1.0 - fx) + d * fx
    return ab * (1.0 - fy) + cd * fy


def fbm(x, y, seed):
    return (
        value_noise(x, y, 21.0, seed) * 0.52
        + value_noise(x, y, 9.0, seed + 37) * 0.31
        + value_noise(x, y, 4.2, seed + 91) * 0.17
    )


def generate_candidates(sampler, rng):
    candidates = []
    index = 0
    row = 0
    y = sampler.y_min + GRID_SPACING * 0.55
    while y < sampler.y_max - GRID_SPACING * 0.45:
        x_offset = GRID_SPACING * 0.5 if row % 2 else 0.0
        x = sampler.x_min + GRID_SPACING * 0.55 + x_offset
        while x < sampler.x_max - GRID_SPACING * 0.45:
            jitter_x = rng.uniform(-GRID_SPACING * 0.47, GRID_SPACING * 0.47)
            jitter_y = rng.uniform(-GRID_SPACING * 0.47, GRID_SPACING * 0.47)
            hit = sampler.hit(x + jitter_x, y + jitter_y)
            if hit is not None:
                candidates.append(Candidate(index, x + jitter_x, y + jitter_y, hit))
                index += 1
            x += GRID_SPACING
        row += 1
        y += GRID_SPACING * 0.8660254
    return candidates


def nearest_candidate(candidates, target, used, minimum_distance):
    options = []
    for candidate in candidates:
        if any(
            math.hypot(candidate.hit.point.x - point.x, candidate.hit.point.y - point.y) < minimum_distance
            for point in used
        ):
            continue
        distance = math.hypot(candidate.x - target[0], candidate.y - target[1])
        options.append((distance, -candidate.hit.mask[1], candidate))
    if not options:
        return None
    options.sort(key=lambda item: (item[0], item[1], item[2].index))
    return options[0][2]


def make_communities(candidates, rng):
    green = [
        candidate for candidate in candidates
        if candidate.hit.zone == "GREEN" and candidate.hit.normal.z >= SLOPE_NORMAL_Z["Groundcover"]
    ]
    soil = [candidate for candidate in candidates if candidate.hit.zone == "SOIL" and candidate.hit.normal.z >= 0.60]
    if not green or not soil:
        raise RuntimeError("Material mask candidate generation produced an empty green or soil zone")

    green_targets = [
        (-34.0, -58.0), (-6.0, -61.0), (27.0, -54.0), (53.0, -28.0),
        (60.0, 12.0), (34.0, 48.0), (-4.0, 54.0), (-42.0, 42.0),
        (-61.0, 8.0), (-56.0, -28.0), (-22.0, -39.0), (18.0, -37.0),
    ]
    green_profiles = [
        "layered", "groundcover_bush", "groundcover_only", "layered",
        "groundcover_bush", "groundcover_only", "layered", "groundcover_bush",
        "groundcover_only", "transition", "layered", "groundcover_bush",
    ]
    green_communities = []
    used = []
    for index, (target, profile) in enumerate(zip(green_targets, green_profiles), start=1):
        candidate = nearest_candidate(green, target, used, 10.0)
        if candidate is None:
            continue
        used.append(candidate.hit.point)
        radial = math.atan2(candidate.y, candidate.x)
        tangent = radial + math.pi * 0.5
        if index in {4, 10}:
            tangent = radial + rng.uniform(-0.18, 0.18)
        green_communities.append(Community(
            f"GREEN_{index:02d}",
            "GREEN",
            candidate.hit.point,
            rng.uniform(23.0, 32.0),
            rng.uniform(12.0, 17.0),
            tangent + rng.uniform(-0.22, 0.22),
            profile,
            index == 1,
        ))

    soil_targets = [
        (-39.0, -34.0), (-10.0, -40.0), (23.0, -36.0), (46.0, -18.0),
        (52.0, 10.0), (36.0, 34.0), (8.0, 42.0), (-22.0, 40.0),
        (-45.0, 27.0), (-50.0, 0.0), (-26.0, -18.0), (8.0, -18.0),
        (29.0, 3.0), (-6.0, 15.0),
    ]
    soil_communities = []
    used = []
    for index, target in enumerate(soil_targets, start=1):
        candidate = nearest_candidate(soil, target, used, 8.0)
        if candidate is None:
            continue
        used.append(candidate.hit.point)
        radial = math.atan2(candidate.y, candidate.x)
        profile = "soil_groundcover"
        if index in {2, 6, 11}:
            profile = "soil_bush"
        elif index in {4, 9}:
            profile = "soil_accent"
        soil_communities.append(Community(
            f"SOIL_{index:02d}",
            "SOIL",
            candidate.hit.point,
            rng.uniform(9.0, 14.0),
            rng.uniform(4.0, 7.0),
            radial + math.pi * 0.5 + rng.uniform(-0.30, 0.30),
            profile,
            False,
        ))
    return green_communities + soil_communities


def community_score(candidate, community):
    dx = candidate.x - community.center.x
    dy = candidate.y - community.center.y
    cos_a = math.cos(community.angle)
    sin_a = math.sin(community.angle)
    local_x = dx * cos_a + dy * sin_a
    local_y = -dx * sin_a + dy * cos_a
    distance = math.sqrt((local_x / community.radius_x) ** 2 + (local_y / community.radius_y) ** 2)
    irregularity = (fbm(candidate.x, candidate.y, SEED + (17 if community.zone == "GREEN" else 53)) - 0.5) * 0.66
    return 1.0 - distance + irregularity


def best_community(candidate, communities):
    relevant = [community for community in communities if community.zone == candidate.hit.zone]
    if not relevant:
        return None, -999.0
    scored = [(community_score(candidate, community), community) for community in relevant]
    scored.sort(key=lambda item: item[0], reverse=True)
    return scored[0][1], scored[0][0]


def footprint_clear_of_rock(sampler, point, radius, sample_count=6):
    if sampler.rock_tree is not None:
        safety_margin = GRID_SPACING * 0.92
        nearby = sampler.rock_tree.find_range(
            Vector((point.x, point.y, 0.0)), radius + safety_margin
        )
        return not nearby
    for index in range(sample_count):
        angle = math.tau * index / sample_count
        hit = sampler.hit(point.x + math.cos(angle) * radius, point.y + math.sin(angle) * radius)
        if hit is None or hit.zone == "ROCK":
            return False
    return True


def weighted_choice(rng, values, weights):
    return rng.choices(values, weights=weights, k=1)[0]


def choose_groundcover_source(rng, zone, normal):
    names = ASSET_NAMES["Groundcover"]
    slope = 1.0 - normal.z
    if zone == "SOIL":
        weights = [0.8, 0.8, 1.0, 4.8, 5.6]
    elif slope > 0.23:
        weights = [2.2, 1.5, 1.3, 4.8, 2.8]
    else:
        weights = [4.5, 3.8, 2.4, 2.6, 1.7]
    return weighted_choice(rng, names, weights)


def rotation_for_surface(normal, yaw):
    align = Vector((0.0, 0.0, 1.0)).rotation_difference(normal)
    spin = Quaternion(normal, yaw)
    return spin @ align


def category_scale(category, zone, rng):
    if category == "Groundcover":
        uniform = rng.uniform(1.02, 1.30) if zone == "GREEN" else rng.uniform(0.86, 1.06)
        anisotropy = rng.uniform(0.92, 1.08)
        return Vector((uniform * anisotropy, uniform / anisotropy, uniform))
    if category == "Bush":
        uniform = rng.uniform(0.90, 1.15) if zone == "GREEN" else rng.uniform(0.82, 0.98)
    elif category == "Fern":
        uniform = rng.uniform(0.88, 1.10) if zone == "GREEN" else rng.uniform(0.82, 0.96)
    else:
        uniform = rng.uniform(0.84, 1.12)
    return Vector((uniform, uniform, uniform))


def make_instance(source, collection, category, zone, community, hit, yaw, scale, index):
    prefix = {"Groundcover": "GC", "Bush": "BS", "Fern": "FN", "Grass": "GR"}[category]
    obj = bpy.data.objects.new(f"R24_{prefix}_{index:05d}", source.data)
    collection.objects.link(obj)
    obj.location = hit.point + hit.normal * CONTACT_OFFSET
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = rotation_for_surface(hit.normal, yaw)
    obj.scale = scale
    source_name = source.get("canonical_asset_name", source.name)
    obj["midpoly_source_asset"] = source_name
    obj["asset_tier"] = "Stylized Mid-Poly R3"
    obj["vegetation_category"] = category
    obj["placement_mask_zone"] = zone
    obj["community_id"] = community.identifier
    obj["community_profile"] = community.profile
    obj["surface_anchor"] = [float(value) for value in hit.point]
    obj["surface_normal"] = [float(value) for value in hit.normal]
    obj["surface_mask_rgba"] = [float(value) for value in hit.mask]
    obj["slope_degrees"] = math.degrees(math.acos(max(-1.0, min(1.0, hit.normal.z))))
    dims = source.dimensions
    radius_x = dims.x * scale.x * 0.48
    radius_y = dims.y * scale.y * 0.48
    return InstanceRecord(
        obj, category, source_name, zone, community.identifier,
        hit.point.copy(), hit.normal.copy(), hit.mask, yaw, radius_x, radius_y,
    )


def groundcover_yaw(source_name, normal, rng):
    if source_name.endswith("Slope_Fan") or source_name.endswith("Elongated_Sparse"):
        return math.atan2(normal.y, normal.x) + math.pi * 0.5 + rng.uniform(-0.28, 0.28)
    return rng.uniform(0.0, math.tau)


def point_in_footprint(point, record):
    dx = point.x - record.anchor.x
    dy = point.y - record.anchor.y
    cos_a = math.cos(record.yaw)
    sin_a = math.sin(record.yaw)
    local_x = dx * cos_a + dy * sin_a
    local_y = -dx * sin_a + dy * cos_a
    rx = max(record.radius_x, 0.08)
    ry = max(record.radius_y, 0.08)
    return (local_x / rx) ** 2 + (local_y / ry) ** 2 <= 1.0


def estimate_coverage(candidates, records, zone):
    targets = [
        candidate for candidate in candidates
        if candidate.hit.zone == zone and candidate.hit.normal.z >= SLOPE_NORMAL_Z["Groundcover"]
    ]
    ground_records = [record for record in records if record.category in {"Groundcover", "Bush", "Fern"}]
    if not targets:
        return 0.0
    cell_size = 3.0
    buckets = defaultdict(list)
    for record in ground_records:
        radius = max(record.radius_x, record.radius_y)
        min_x = math.floor((record.anchor.x - radius) / cell_size)
        max_x = math.floor((record.anchor.x + radius) / cell_size)
        min_y = math.floor((record.anchor.y - radius) / cell_size)
        max_y = math.floor((record.anchor.y + radius) / cell_size)
        for ix in range(min_x, max_x + 1):
            for iy in range(min_y, max_y + 1):
                buckets[(ix, iy)].append(record)
    covered = 0
    for candidate in targets:
        key = (math.floor(candidate.x / cell_size), math.floor(candidate.y / cell_size))
        if any(point_in_footprint(candidate.hit.point, record) for record in buckets.get(key, [])):
            covered += 1
    return covered / len(targets)


def place_groundcover(candidates, communities, sources, collection, sampler, rng, records, counters):
    by_community = defaultdict(list)
    eligible = []
    print("R24_STAGE groundcover_initial_begin", flush=True)
    for candidate in candidates:
        if candidate.hit.zone not in {"GREEN", "SOIL"}:
            continue
        if candidate.hit.normal.z < SLOPE_NORMAL_Z["Groundcover"]:
            continue
        community, score = best_community(candidate, communities)
        if community is None:
            continue
        minimum_score = -0.20 if candidate.hit.zone == "GREEN" else -0.12
        if score < minimum_score:
            continue
        if candidate.hit.zone == "GREEN":
            probability = 0.91 if score > 0.38 else 0.77 if score > 0.05 else 0.57
            probability *= 0.88 + min(0.16, candidate.hit.mask[1] * 0.18)
        else:
            shelter = min(1.0, candidate.hit.cavity * 1.8 + candidate.hit.mask[1] * 0.35)
            probability = (0.36 if score > 0.35 else 0.26 if score > 0.04 else 0.16) * (0.78 + shelter * 0.32)
        eligible.append((score, candidate, community))
        if hash01(candidate.index, int(score * 1000), SEED + 201) > probability:
            continue
        source_name = choose_groundcover_source(rng, candidate.hit.zone, candidate.hit.normal)
        source = sources[source_name]
        scale = category_scale("Groundcover", candidate.hit.zone, rng)
        clearance = max(source.dimensions.x * scale.x, source.dimensions.y * scale.y) * 0.49
        if not footprint_clear_of_rock(sampler, candidate.hit.point, clearance):
            continue
        counters["Groundcover"] += 1
        record = make_instance(
            source,
            collection,
            "Groundcover",
            candidate.hit.zone,
            community,
            candidate.hit,
            groundcover_yaw(source_name, candidate.hit.normal, rng),
            scale,
            counters["Groundcover"],
        )
        records.append(record)
        by_community[community.identifier].append((candidate, score, record))

    print(f"R24_STAGE groundcover_initial_end count={len(records)}", flush=True)

    # Coverage correction remains inside the authored broad communities; it never opens the rock mask.
    targets = {"GREEN": 0.67, "SOIL": 0.16}
    coverage_cell_size = 3.0
    coverage_buckets = defaultdict(list)

    def add_coverage_record(record):
        radius = max(record.radius_x, record.radius_y)
        min_x = math.floor((record.anchor.x - radius) / coverage_cell_size)
        max_x = math.floor((record.anchor.x + radius) / coverage_cell_size)
        min_y = math.floor((record.anchor.y - radius) / coverage_cell_size)
        max_y = math.floor((record.anchor.y + radius) / coverage_cell_size)
        for ix in range(min_x, max_x + 1):
            for iy in range(min_y, max_y + 1):
                coverage_buckets[(ix, iy)].append(record)

    for record in records:
        if record.category == "Groundcover":
            add_coverage_record(record)

    for zone, target in targets.items():
        current = estimate_coverage(candidates, records, zone)
        print(f"R24_STAGE topup_begin zone={zone} coverage={current:.4f} target={target:.4f}", flush=True)
        if current >= target:
            continue
        additions = [item for item in eligible if item[1].hit.zone == zone]
        additions.sort(key=lambda item: (-item[0], hash01(item[1].index, 7, SEED + 299)))
        occupied_indices = {item[0].index for values in by_community.values() for item in values}
        added_since_check = 0
        for score, candidate, community in additions:
            if candidate.index in occupied_indices:
                continue
            candidate_key = (
                math.floor(candidate.x / coverage_cell_size),
                math.floor(candidate.y / coverage_cell_size),
            )
            if any(
                point_in_footprint(candidate.hit.point, record)
                for record in coverage_buckets.get(candidate_key, [])
            ):
                continue
            source_name = choose_groundcover_source(rng, zone, candidate.hit.normal)
            source = sources[source_name]
            scale = category_scale("Groundcover", zone, rng)
            clearance = max(source.dimensions.x * scale.x, source.dimensions.y * scale.y) * 0.49
            if not footprint_clear_of_rock(sampler, candidate.hit.point, clearance):
                continue
            counters["Groundcover"] += 1
            record = make_instance(
                source, collection, "Groundcover", zone, community, candidate.hit,
                groundcover_yaw(source_name, candidate.hit.normal, rng), scale,
                counters["Groundcover"],
            )
            records.append(record)
            by_community[community.identifier].append((candidate, score, record))
            add_coverage_record(record)
            occupied_indices.add(candidate.index)
            added_since_check += 1
            check_batch = 10 if zone == "SOIL" else 180
            if added_since_check >= check_batch:
                current = estimate_coverage(candidates, records, zone)
                print(
                    f"R24_STAGE topup_progress zone={zone} records={len(records)} coverage={current:.4f}",
                    flush=True,
                )
                added_since_check = 0
                if current >= target:
                    break
        print(
            f"R24_STAGE topup_end zone={zone} records={len(records)} coverage={estimate_coverage(candidates, records, zone):.4f}",
            flush=True,
        )
    return by_community


def sufficiently_separated(point, radius, large_records):
    return all(math.hypot(point.x - other.anchor.x, point.y - other.anchor.y) >= radius + max(other.radius_x, other.radius_y) * 0.72 for other in large_records)


def place_companion_at(
    base_candidate,
    category,
    community,
    sources,
    collection,
    sampler,
    rng,
    records,
    counters,
    large_records,
):
    for _ in range(10):
        spread = {"Bush": (0.45, 1.45), "Fern": (0.55, 1.65), "Grass": (0.65, 1.85)}[category]
        angle = rng.uniform(0.0, math.tau)
        radius = rng.uniform(*spread)
        hit = sampler.hit(
            base_candidate.x + math.cos(angle) * radius,
            base_candidate.y + math.sin(angle) * radius,
        )
        if hit is None:
            continue
        allowed_zones = {community.zone}
        if category == "Grass" and community.zone == "GREEN":
            allowed_zones.add("SOIL")
        if hit.zone not in allowed_zones or hit.normal.z < SLOPE_NORMAL_Z[category]:
            continue
        source_name = rng.choice(ASSET_NAMES[category])
        source = sources[source_name]
        scale = category_scale(category, hit.zone, rng)
        clearance = max(source.dimensions.x * scale.x, source.dimensions.y * scale.y) * 0.48
        if not footprint_clear_of_rock(sampler, hit.point, clearance):
            continue
        separation = {"Bush": 1.00, "Fern": 0.90, "Grass": 0.34}[category]
        if not sufficiently_separated(hit.point, separation, large_records if category != "Grass" else []):
            continue
        counters[category] += 1
        record = make_instance(
            source, collection, category, hit.zone, community, hit,
            rng.uniform(0.0, math.tau), scale, counters[category],
        )
        records.append(record)
        if category in {"Bush", "Fern"}:
            large_records.append(record)
        return True
    return False


def place_companions(by_community, communities, sources, collections, sampler, rng, records, counters):
    community_map = {community.identifier: community for community in communities}
    large_records = []
    for identifier, placements in by_community.items():
        community = community_map[identifier]
        anchors = [(candidate, score) for candidate, score, _record in placements]
        if not anchors:
            continue
        rng.shuffle(anchors)
        groundcover_count = len(anchors)
        recipe = {"Bush": 0, "Fern": 0, "Grass": 0}
        if community.zone == "GREEN":
            if community.profile in {"groundcover_bush", "layered", "transition"}:
                recipe["Bush"] = max(4, round(groundcover_count * (0.048 if community.profile == "layered" else 0.035)))
            if community.profile == "layered":
                recipe["Fern"] = max(3, round(groundcover_count * 0.014))
            if community.profile in {"layered", "transition"}:
                recipe["Grass"] = max(10, round(groundcover_count * (0.11 if community.featured else 0.075)))
        else:
            if community.profile in {"soil_bush", "soil_accent"}:
                recipe["Bush"] = max(2, round(groundcover_count * 0.022))
            if community.profile == "soil_accent":
                recipe["Fern"] = min(3, max(1, round(groundcover_count * 0.010)))

        for category, amount in recipe.items():
            if amount <= 0:
                continue
            if category == "Fern":
                ordered = sorted(anchors, key=lambda item: (item[0].hit.cavity, item[0].hit.normal.z), reverse=True)
            elif category == "Grass":
                ordered = sorted(anchors, key=lambda item: (abs(item[1] - 0.04), item[0].y))
            else:
                ordered = list(anchors)
            placed = 0
            for candidate, _score in ordered * 2:
                if placed >= amount:
                    break
                if category == "Grass" and candidate.y > 22.0 and not community.featured:
                    continue
                if place_companion_at(
                    candidate, category, community, sources, collections[category], sampler,
                    rng, records, counters, large_records,
                ):
                    placed += 1


def make_principled_material(name, color, roughness, metallic=0.0):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = linear_hex(color)
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = metallic
    return material


def make_plane(collection, name, size, z, material):
    half = size * 0.5
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata([(-half, -half, z), (half, -half, z), (half, half, z), (-half, half, z)], [], [(0, 1, 2, 3)])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def point_camera(camera, location, target, lens):
    camera.location = location
    camera.data.type = "PERSP"
    camera.data.lens = lens
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()


def configure_preview(sampler):
    remove_collection_tree(PREVIEW_COLLECTION)
    collection = bpy.data.collections.new(PREVIEW_COLLECTION)
    bpy.context.scene.collection.children.link(collection)
    for obj in bpy.context.scene.objects:
        if obj.type == "LIGHT":
            obj.hide_render = True

    camera_data = bpy.data.cameras.new("R24_Render_Camera_Data")
    camera = bpy.data.objects.new("R24_Render_Camera", camera_data)
    collection.objects.link(camera)
    bpy.context.scene.camera = camera

    sun_data = bpy.data.lights.new("R24_Neutral_Daylight_Data", "SUN")
    sun_data.energy = 2.55
    sun_data.angle = math.radians(8.0)
    sun_data.color = srgb_hex("FFF1D8")[:3]
    sun = bpy.data.objects.new("R24_Neutral_Daylight", sun_data)
    collection.objects.link(sun)
    sun.rotation_euler = (math.radians(31.0), math.radians(-20.0), math.radians(-34.0))

    fill_data = bpy.data.lights.new("R24_Sky_Fill_Data", "AREA")
    fill_data.energy = 1550.0
    fill_data.shape = "DISK"
    fill_data.size = 100.0
    fill_data.color = srgb_hex("DDEAFF")[:3]
    fill = bpy.data.objects.new("R24_Sky_Fill", fill_data)
    collection.objects.link(fill)
    fill.location = (-105.0, -125.0, 120.0)
    fill.rotation_euler = (Vector((0.0, 0.0, 25.0)) - fill.location).to_track_quat("-Z", "Y").to_euler()

    water_material = make_principled_material("M_R24_Water_Preview", "698D96", 0.34, 0.06)
    water = make_plane(collection, "R24_Water_Preview", 520.0, sampler.z_min + 0.18, water_material)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.resolution_percentage = 100
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = linear_hex("AEB4B2")
    background.inputs["Strength"].default_value = 0.72
    return collection, camera, water


def create_diagnostic_terrain(hero, sampler):
    remove_collection_tree(DIAGNOSTIC_COLLECTION)
    collection = bpy.data.collections.new(DIAGNOSTIC_COLLECTION)
    bpy.context.scene.collection.children.link(collection)
    mesh = hero.data.copy()
    mesh.name = "Volcano_Hero_02_R24_PlacementMaskDiagnostic_Mesh"
    existing = mesh.color_attributes.get("R24_PlacementMaskDiagnostic")
    if existing:
        mesh.color_attributes.remove(existing)
    attr = mesh.color_attributes.new("R24_PlacementMaskDiagnostic", "FLOAT_COLOR", "CORNER")
    colors = {
        "ROCK": linear_hex("E4382B"),
        "SOIL": linear_hex("F2C84B"),
        "GREEN": linear_hex("39A852"),
    }
    for polygon in mesh.polygons:
        zone = mask_zone(
            sampler.face_masks[polygon.index],
            sampler.face_albedo[polygon.index],
            sampler.face_height[polygon.index],
        )
        for loop_index in polygon.loop_indices:
            attr.data[loop_index].color = colors[zone]

    material = bpy.data.materials.get("M_R24_PlacementMask_Diagnostic") or bpy.data.materials.new("M_R24_PlacementMask_Diagnostic")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    for node in list(nodes):
        nodes.remove(node)
    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    vertex_color = nodes.new("ShaderNodeVertexColor")
    vertex_color.layer_name = "R24_PlacementMaskDiagnostic"
    links.new(vertex_color.outputs["Color"], emission.inputs["Color"])
    emission.inputs["Strength"].default_value = 1.0
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    mesh.materials.clear()
    mesh.materials.append(material)
    obj = bpy.data.objects.new("Volcano_Hero_02_R24_PlacementMaskDiagnostic", mesh)
    collection.objects.link(obj)
    obj.matrix_world = hero.matrix_world.copy()
    obj.hide_render = True
    obj.hide_viewport = True
    return obj, collection


def render_view(scene, camera, path, location, target, lens, water, show_water):
    water.hide_render = not show_water
    point_camera(camera, location, target, lens)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def featured_community(communities):
    for community in communities:
        if community.featured:
            return community
    return next(community for community in communities if community.zone == "GREEN")


def render_validation_views(scene, camera, water, sampler, hero, master, diagnostic, communities):
    RENDER_DIR.mkdir(parents=True, exist_ok=True)
    z_water = sampler.z_min + 0.18
    hero.hide_render = False
    diagnostic.hide_render = True
    master.hide_render = False
    render_view(
        scene, camera, RENDER_DIR / "01_full_front_material_masked_placement.png",
        (0.0, -258.0, z_water + 7.4),
        (0.0, 0.0, sampler.z_min + sampler.height * 0.40),
        52.0, water, True,
    )
    render_view(
        scene, camera, RENDER_DIR / "02_three_quarter_material_masked_placement.png",
        (126.0, -216.0, z_water + 47.0),
        (0.0, 0.0, sampler.z_min + sampler.height * 0.31),
        55.0, water, True,
    )

    featured = featured_community(communities)
    outward = Vector((featured.center.x, featured.center.y, 0.0))
    if outward.length < 1e-6:
        outward = Vector((0.0, -1.0, 0.0))
    outward.normalize()
    camera_xy = featured.center + outward * 15.5
    camera_surface = sampler.hit(camera_xy.x, camera_xy.y)
    camera_z = (camera_surface.point.z + 1.75) if camera_surface else featured.center.z + 2.0
    render_view(
        scene, camera, RENDER_DIR / "03_close_lower_slope_layering.png",
        (camera_xy.x, camera_xy.y, camera_z),
        featured.center + Vector((0.0, 0.0, 0.82)),
        52.0, water, False,
    )

    master.hide_render = True
    hero.hide_render = True
    diagnostic.hide_viewport = False
    diagnostic.hide_render = False
    water.hide_render = True
    render_view(
        scene, camera, RENDER_DIR / "04_diagnostic_material_placement_mask.png",
        (126.0, -216.0, z_water + 47.0),
        (0.0, 0.0, sampler.z_min + sampler.height * 0.31),
        55.0, water, False,
    )

    diagnostic.hide_render = True
    diagnostic.hide_viewport = True
    hero.hide_render = False
    render_view(
        scene, camera, RENDER_DIR / "05_vegetation_hidden_material_boundaries.png",
        (126.0, -216.0, z_water + 47.0),
        (0.0, 0.0, sampler.z_min + sampler.height * 0.31),
        55.0, water, False,
    )
    master.hide_render = False
    water.hide_render = False


def validate(
    hero,
    hero_hash,
    hero_materials,
    hero_transform,
    sources,
    source_hashes,
    source_materials,
    source_transforms,
    sampler,
    candidates,
    communities,
    records,
    removed_old_instances,
):
    errors = []
    category_counts = Counter(record.category for record in records)
    zone_counts = Counter(record.zone for record in records)
    category_zone_counts = Counter((record.category, record.zone) for record in records)
    source_counts = Counter(record.source_name for record in records)
    community_counts = Counter(record.community_id for record in records)

    rock_anchor_violations = []
    rock_footprint_violations = []
    floating = []
    buried = []
    slope_violations = []
    for record in records:
        hit = sampler.hit(record.anchor.x, record.anchor.y)
        if hit is None or hit.zone == "ROCK":
            rock_anchor_violations.append(record.obj.name)
        if not footprint_clear_of_rock(
            sampler, record.anchor, max(record.radius_x, record.radius_y) * 0.96, sample_count=10
        ):
            rock_footprint_violations.append(record.obj.name)
        signed_offset = (record.obj.location - record.anchor).dot(record.normal)
        if signed_offset > 0.035:
            floating.append(record.obj.name)
        if signed_offset < -0.004:
            buried.append(record.obj.name)
        if record.normal.z + 1e-6 < SLOPE_NORMAL_Z[record.category]:
            slope_violations.append(record.obj.name)

    large = [record for record in records if record.category in {"Bush", "Fern"}]
    large_intersections = []
    for index, left in enumerate(large):
        for right in large[index + 1:]:
            distance = math.hypot(left.anchor.x - right.anchor.x, left.anchor.y - right.anchor.y)
            threshold = (max(left.radius_x, left.radius_y) + max(right.radius_x, right.radius_y)) * 0.38
            if distance < threshold:
                large_intersections.append((left.obj.name, right.obj.name))

    green_coverage = estimate_coverage(candidates, records, "GREEN")
    soil_coverage = estimate_coverage(candidates, records, "SOIL")
    current_source_hashes = {name: mesh_hash(obj.data) for name, obj in sources.items()}
    current_source_materials = {name: tuple(material.name for material in obj.data.materials) for name, obj in sources.items()}
    current_source_transforms = {
        name: (tuple(obj.location), tuple(obj.rotation_euler), tuple(obj.scale))
        for name, obj in sources.items()
    }
    current_hero_transform = (tuple(hero.location), tuple(hero.rotation_euler), tuple(hero.scale))

    if mesh_hash(hero.data) != hero_hash:
        errors.append("Volcano mesh hash changed")
    if tuple(material.name for material in hero.data.materials) != hero_materials:
        errors.append("Volcano material slots changed")
    if current_hero_transform != hero_transform:
        errors.append("Volcano transform changed")
    if current_source_hashes != source_hashes:
        errors.append("One or more R3 source meshes changed")
    if current_source_materials != source_materials:
        errors.append("One or more R3 source material slots changed")
    if current_source_transforms != source_transforms:
        errors.append("One or more R3 source transforms changed")
    if rock_anchor_violations:
        errors.append(f"Rock anchor violations: {len(rock_anchor_violations)}")
    if rock_footprint_violations:
        errors.append(f"Rock footprint violations: {len(rock_footprint_violations)}")
    if floating:
        errors.append(f"Floating instances: {len(floating)}")
    if buried:
        errors.append(f"Buried instances: {len(buried)}")
    if slope_violations:
        errors.append(f"Slope violations: {len(slope_violations)}")
    if large_intersections:
        errors.append(f"Visible large-asset intersections: {len(large_intersections)}")
    if not 0.55 <= green_coverage <= 0.75:
        errors.append(f"Green visual coverage outside 55-75%: {green_coverage:.3f}")
    if not 0.15 <= soil_coverage <= 0.30:
        errors.append(f"Soil visual coverage outside 15-30%: {soil_coverage:.3f}")
    missing_variants = sorted(set(sources) - set(source_counts))
    if missing_variants:
        errors.append(f"Unused R3 variants: {missing_variants}")

    mask_candidate_counts = Counter(candidate.hit.zone for candidate in candidates)
    return {
        "placed_instance_count": len(records),
        "category_counts": dict(sorted(category_counts.items())),
        "mask_zone_counts": dict(sorted(zone_counts.items())),
        "category_by_mask_zone": {
            f"{category}_{zone}": count
            for (category, zone), count in sorted(category_zone_counts.items())
        },
        "source_variant_counts": dict(sorted(source_counts.items())),
        "community_instance_counts": dict(sorted(community_counts.items())),
        "community_count": len(communities),
        "green_community_count": sum(community.zone == "GREEN" for community in communities),
        "soil_community_count": sum(community.zone == "SOIL" for community in communities),
        "surface_candidate_counts": dict(sorted(mask_candidate_counts.items())),
        "estimated_visual_surface_coverage": {
            "green_dense_region_percent": round(green_coverage * 100.0, 2),
            "brown_soil_sparse_region_percent": round(soil_coverage * 100.0, 2),
        },
        "upper_dark_rock_instance_count": len(rock_anchor_violations),
        "upper_dark_rock_footprint_overlap_count": len(rock_footprint_violations),
        "floating_instance_count": len(floating),
        "buried_instance_count": len(buried),
        "slope_violation_count": len(slope_violations),
        "visible_large_asset_intersection_count": len(large_intersections),
        "linked_source_mesh_count": len({record.obj.data.name for record in records}),
        "r3_source_meshes_unchanged": current_source_hashes == source_hashes,
        "r3_source_material_slots_unchanged": current_source_materials == source_materials,
        "r3_source_transforms_unchanged": current_source_transforms == source_transforms,
        "volcano_mesh_unchanged": mesh_hash(hero.data) == hero_hash,
        "volcano_material_slots_unchanged": tuple(material.name for material in hero.data.materials) == hero_materials,
        "volcano_transform_unchanged": current_hero_transform == hero_transform,
        "removed_old_placement_instance_count": removed_old_instances,
        "blender_errors": errors,
    }


def main():
    started = time.perf_counter()
    RENDER_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(INPUT_BLEND))
    hero = bpy.data.objects[HERO_NAME]
    hero.hide_render = False
    hero.hide_viewport = False
    hero_hash = mesh_hash(hero.data)
    hero_materials = tuple(material.name for material in hero.data.materials)
    hero_transform = (tuple(hero.location), tuple(hero.rotation_euler), tuple(hero.scale))

    sources, source_collection = canonical_source_map()
    source_hashes = {name: mesh_hash(obj.data) for name, obj in sources.items()}
    source_materials = {name: tuple(material.name for material in obj.data.materials) for name, obj in sources.items()}
    source_transforms = {
        name: (tuple(obj.location), tuple(obj.rotation_euler), tuple(obj.scale))
        for name, obj in sources.items()
    }

    removed_old_instances = remove_collection_tree(OLD_PLACEMENT_COLLECTION)
    remove_collection_tree(MASTER_COLLECTION)
    master = bpy.data.collections.new(MASTER_COLLECTION)
    bpy.context.scene.collection.children.link(master)
    category_collections = {
        category: new_child_collection(master, f"R24_{category}")
        for category in ASSET_NAMES
    }

    sampler = MaterialSurfaceSampler(hero)
    print("R24_STAGE sampler_ready", flush=True)
    rng = random.Random(SEED)
    candidates = generate_candidates(sampler, rng)
    sampler.build_rock_index(candidates)
    print(f"R24_STAGE candidates_ready count={len(candidates)}", flush=True)
    communities = make_communities(candidates, rng)
    print(f"R24_STAGE communities_ready count={len(communities)}", flush=True)
    records = []
    counters = Counter()
    by_community = place_groundcover(
        candidates,
        communities,
        sources,
        category_collections["Groundcover"],
        sampler,
        rng,
        records,
        counters,
    )
    print(f"R24_STAGE groundcover_ready count={len(records)}", flush=True)
    place_companions(
        by_community,
        communities,
        sources,
        category_collections,
        sampler,
        rng,
        records,
        counters,
    )
    print(f"R24_STAGE companions_ready count={len(records)}", flush=True)

    preview_collection, camera, water = configure_preview(sampler)
    diagnostic, diagnostic_collection = create_diagnostic_terrain(hero, sampler)
    print("R24_STAGE preview_ready", flush=True)
    scene = bpy.context.scene
    render_validation_views(
        scene, camera, water, sampler, hero, master, diagnostic, communities
    )

    report = validate(
        hero,
        hero_hash,
        hero_materials,
        hero_transform,
        sources,
        source_hashes,
        source_materials,
        source_transforms,
        sampler,
        candidates,
        communities,
        records,
        removed_old_instances,
    )
    report.update({
        "pass": "VOLCANO R24 — MATERIAL-MASKED VEGETATION PLACEMENT",
        "input_blend": str(INPUT_BLEND),
        "output_blend": str(OUTPUT_BLEND),
        "source_asset_collection": SOURCE_COLLECTION,
        "active_placement_collection": MASTER_COLLECTION,
        "mask_attribute": MASK_ATTRIBUTE,
        "mask_semantics": {
            "R": "exposed volcanic rock / ash support channel; hard exclusion when the visible albedo is dark",
            "G": "dense lower vegetation region",
            "B": "sparse brown soil colonization region",
            "A": "absolute exclusion: summit / crater",
        },
        "placement_method": "visible baked albedo plus semantic control channels first; large irregular authored communities second; height is diagnostic metadata only",
        "grid_spacing_m": GRID_SPACING,
        "contact_offset_m": CONTACT_OFFSET,
        "random_seed": SEED,
        "renders": [
            "01_full_front_material_masked_placement.png",
            "02_three_quarter_material_masked_placement.png",
            "03_close_lower_slope_layering.png",
            "04_diagnostic_material_placement_mask.png",
            "05_vegetation_hidden_material_boundaries.png",
        ],
        "unity_exported": False,
        "elapsed_seconds": round(time.perf_counter() - started, 3),
    })
    REPORT_PATH.write_text(json.dumps(report, indent=2), encoding="utf-8")
    scene.render.filepath = ""
    source_collection.hide_render = True
    source_collection.hide_viewport = True
    diagnostic.hide_render = True
    diagnostic.hide_viewport = True
    master.hide_render = False
    hero.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND), check_existing=False)
    print("R24_MATERIAL_MASKED_PLACEMENT_BEGIN")
    print(json.dumps(report, indent=2))
    print("R24_MATERIAL_MASKED_PLACEMENT_END")


if __name__ == "__main__":
    main()
