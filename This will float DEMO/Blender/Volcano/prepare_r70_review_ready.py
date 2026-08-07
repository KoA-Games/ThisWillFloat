from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "Volcano_Hero_02_ColorZoneTransitionRefinement_R70_Portable.blend"
OUTPUT = ROOT / "Volcano_Hero_02_ColorZoneTransitionRefinement_R70_ReviewReady.blend"
APPROVED_RENDER = ROOT / "ColorZoneTransitionRefinement_R70_Renders" / "02_three_quarter_overview.png"


def point_camera(camera, location, target, lens=55):
    camera.location = location
    camera.rotation_euler = (Vector(target) - Vector(location)).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "PERSP"
    camera.data.lens = lens


bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene = bpy.context.scene
hero = bpy.data.objects["Volcano_Hero_02"]
camera = bpy.data.objects["CAM_R69_ColorZone_Placement"]
water = bpy.data.objects["R69_Water"]

world_points = [hero.matrix_world @ Vector(corner) for corner in hero.bound_box]
z_min = min(point.z for point in world_points)
z_max = max(point.z for point in world_points)
height = z_max - z_min
water_z = water.matrix_world.translation.z

# Restore the exact approved three-quarter review camera used for R70 render 02.
point_camera(
    camera,
    (145.0, -218.0, water_z + 48.0),
    (0.0, 0.0, z_min + height * 0.33),
    55,
)
scene.camera = camera
scene.frame_set(1)
scene.render.resolution_x = 1600
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.filepath = str(APPROVED_RENDER)
water.hide_render = False
water.hide_viewport = False

# Make the normal Layout workspace open in the approved camera composition.
layout = bpy.data.workspaces.get("Layout")
if layout:
    for screen in layout.screens:
        for area in screen.areas:
            if area.type != "VIEW_3D":
                continue
            space = area.spaces.active
            space.region_3d.view_perspective = "CAMERA"
            space.shading.type = "MATERIAL"
            space.shading.use_scene_world = True
            space.shading.use_scene_lights = True

# Keep a packed, pixel-identical approved image in the Rendering workspace.
approved = bpy.data.images.get("R70_APPROVED_THREE_QUARTER")
if approved is None:
    approved = bpy.data.images.load(str(APPROVED_RENDER), check_existing=False)
    approved.name = "R70_APPROVED_THREE_QUARTER"
approved.pack()

rendering = bpy.data.workspaces.get("Rendering")
if rendering:
    for screen in rendering.screens:
        for area in screen.areas:
            if area.type == "IMAGE_EDITOR":
                area.spaces.active.image = approved

# Save a short in-file handoff note.
note = bpy.data.texts.get("R70_READ_ME_FIRST") or bpy.data.texts.new("R70_READ_ME_FIRST")
note.clear()
note.write(
    "R70 REVIEW-READY FILE\n\n"
    "The exact approved output is packed as image R70_APPROVED_THREE_QUARTER.\n"
    "Open the Rendering workspace to view it pixel-for-pixel.\n"
    "The Layout workspace is set to the same camera composition.\n"
    "Press F12 to render the actual scene using that camera.\n"
    "All final volcano, terrain materials, trees, bushes, grass and placement geometry are inside this file.\n"
)
scene["r70_review_ready"] = True
scene["r70_approved_render"] = "02_three_quarter_overview.png"
scene["r70_open_instructions"] = "Rendering workspace = exact packed approved image; Layout/F12 = live scene"

# Prefer the lightweight static approved image on first open when a window exists.
if bpy.context.window and rendering:
    bpy.context.window.workspace = rendering

bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT), check_existing=False, compress=True)

print("R70_REVIEW_READY", OUTPUT)
print("CAMERA", tuple(round(v, 4) for v in camera.location), camera.data.type, camera.data.lens)
print("TARGET_Z", round(z_min + height * 0.33, 4))
print("APPROVED_PACKED", approved.packed_file is not None)
