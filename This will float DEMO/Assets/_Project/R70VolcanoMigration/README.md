# R70 Volcano Unity Migration

This package transfers the approved Blender R70 volcano, its authored terrain
color, and the complete final R69/R70 vegetation placement into Unity 6 URP.

## Open in Unity

Open the Unity project and load:

`Assets/_Project/Scenes/R70_Volcano_UnityMigration.unity`

If the generated scene or materials need to be recreated, use:

`This Will Float > R70 Migration > Build High-Fidelity Unity Scene`

## What is preserved

- The approved `Volcano_Hero_02` mesh, UVs, normals, and vertex colors
- The baked R70 terrain albedo and macro-relief textures
- All 2,098 final R69/R70 vegetation placements
- 166 shared vegetation source meshes
- Per-instance position, rotation, and scale
- The 60 authored vegetation material roles and color hierarchy
- A review camera, neutral daylight, water plane, and approval reference image

## What is translated

Blender EEVEE procedural material nodes are not understood by Unity. The
migration therefore uses custom URP shaders, baked volcano textures, exported
vertex colors, and generated Unity materials. Geometry and placement are exact;
lighting and shader response are calibrated approximations rather than
pixel-identical EEVEE output.

## Source and recovery

The portable and ReviewReady `.blend` files and all reusable vegetation kits are
also committed with Git LFS. This makes the package recoverable even without the
original Mac. The Blender exporter is stored at:

`Blender/UnityMigration/export_r70_to_unity.py`

## Another computer

After cloning or switching to the migration branch, run:

```bash
git lfs install
git lfs pull
```

Then open the Unity project with Unity `6000.5.1f1`. The generated Unity scene is
already included, so Blender is not required merely to review or use it.

## Performance note

The high-fidelity scene intentionally preserves the full approved placement and
contains roughly 13.58 million instanced triangles. It is the migration master,
not the final optimized shipping configuration. Before production deployment,
add LODs, distance culling, occlusion, and platform-specific vegetation budgets.
