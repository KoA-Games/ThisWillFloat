# R70 Portable Volcano + Tropical Vegetation Package

This package preserves the approved R70 volcano result and the reusable source
asset kits used to build its tropical vegetation.

## Open the approved scene

For the easiest review experience, open:

`Volcano/Volcano_Hero_02_ColorZoneTransitionRefinement_R70_ReviewReady.blend`

This version starts at frame 1 with the approved three-quarter camera restored.
Its Layout workspace is already in camera view, and the pixel-identical approved
image is packed as `R70_APPROVED_THREE_QUARTER`. Open the Rendering workspace to
view that packed reference, or press F12 to render the live scene.

The original portable working file remains available at:

`Volcano/Volcano_Hero_02_ColorZoneTransitionRefinement_R70_Portable.blend`

The volcano albedo is packed inside this Blender file, so the scene does not
depend on the original workstation's absolute file paths. The editable source
terrain textures are also included under:

`Volcano/MaterialControlMasks_R20/Textures/`

## Included reusable vegetation kits

- `Vegetation/Production_Tropical_Tree_Kit_R37.blend`
  - tall, medium, and sapling tropical trees
- `Vegetation/Production_Tropical_Understory_Kit_R39.blend`
  - bushes, ferns, grass, and broadleaf accents
- `Vegetation/Production_Tropical_Groundcover_Mass_Kit_R40.blend`
  - reusable connected groundcover masses
- `Vegetation/Production_Vegetation_Mass_Correction_R50.blend`
  - low-canopy mounds, dense shrub masses, slope shelves, lower-belt blocks,
    and dark understory fillers

All four source asset files were checked for external Blender libraries and
unpacked external images. Both counts are zero.

## Approved review output

The direct reference image is:

`Volcano/ColorZoneTransitionRefinement_R70_Renders/02_three_quarter_overview.png`

The folder also contains front, opposite-side, gameplay-distance, and top-down
renders, plus the R70 build and verification reports.

## Validation

- Blender scene: 3,755 vegetation-related mesh objects inspected
- Portable scene external Blender libraries: 0
- Portable scene unpacked external images: 0
- Representative portable re-render: 1600 x 900
- Difference from the approved three-quarter render: at most 1/255 on a small
  number of RGB values due to render-file quantization; average error below
  0.000008 per channel
- Git LFS is required for `.blend` and image files

After cloning or pulling on another computer, run:

```bash
git lfs pull
```

Then open the ReviewReady R70 Blender file above. A normal viewport preview can
look brighter and flatter than the final output; use the Rendering workspace or
F12 when judging the approved look.
