# Engineering Case Study

## Executive summary

This Will Float began as a Unity survival-game prototype and evolved into a practical testbed for graphics tooling, procedural environment construction, DCC-to-engine migration, and physics-driven gameplay feedback.

The strongest engineering outcome is not the amount of shipped game content. It is the workflow built around the content: deterministic scene generation, explicit preservation constraints, independent validation, machine-readable reports, and repeatable visual evidence.

## 1. Procedural environment pipeline

### Problem

The volcano environment combined a hero terrain mesh with thousands of vegetation instances. Manual placement was slow to revise and difficult to verify. Moving the scene between Blender and Unity also risked changing transforms, materials, orientation, scale, and approved source files.

### Approach

The Blender pipeline converts artistic rules into testable placement constraints:

- Sample candidate points on the volcano surface.
- Read surface height, normal, slope, cavity, and color/material-zone information.
- Select candidates for lower canopy, bushes, small trees, and transition vegetation.
- Exclude rock zones and unsafe height bands.
- Align instances to the surface while preserving readable silhouettes.
- Use stable seeds and fingerprints so the same inputs produce reviewable outputs.
- Write JSON build and verification reports alongside fixed-camera renders.

The Unity migration then reconstructs the scene from exported geometry and metadata, preserving transforms and translating the Blender look into an explicit URP material setup.

### Measured output

The current R70 migration report records:

| Metric | Value |
| --- | ---: |
| Volcano vertices | 48,924 |
| Volcano triangles | 97,844 |
| Vegetation placements | 2,098 |
| Unique vegetation meshes | 166 |
| Vegetation material definitions | 60 |
| Placement records exported to Unity | 2,098 |

The independent transition verifier additionally records:

| Validation | Result |
| --- | ---: |
| Preserved R69 placements | 1,668 |
| Added R70 transition objects | 430 |
| Added satellite patches | 37 |
| Surface-zone mismatches | 0 |
| Floating additions | 0 |
| Buried additions | 0 |
| Trees incorrectly placed on soil | 0 |
| Additions incorrectly placed on rock | 0 |
| Validation renders | 5 × 1600×900 |

### Verification strategy

The verifier is separate from the build script and fails the run when an invariant is broken. Checks include:

- Hero mesh hash unchanged.
- Material-slot signature unchanged.
- Previous placement count and transform/data fingerprint preserved.
- Expected role, zone, transition-band, and patch counts.
- Placement anchor still resolves to the expected surface zone.
- Signed normal distance stays within floating and burial tolerances.
- Required renders exist, have unique hashes, and use expected dimensions.

This turns subjective environment iteration into a hybrid workflow: artists still judge composition and silhouette, while code protects non-negotiable structural constraints.

### Evidence

- [R70 build script](https://github.com/KoA-Games/ThisWillFloat/blob/agent/r70-unity-migration/This%20will%20float%20DEMO/Blender/Volcano/refine_color_zone_transitions_r70.py)
- [Independent verifier](https://github.com/KoA-Games/ThisWillFloat/blob/agent/r70-unity-migration/This%20will%20float%20DEMO/Blender/Volcano/verify_color_zone_transitions_r70.py)
- [Machine-readable verification result](https://github.com/KoA-Games/ThisWillFloat/blob/agent/r70-unity-migration/This%20will%20float%20DEMO/Blender/Volcano/ColorZoneTransitionRefinement_R70_Renders/color_zone_transition_refinement_r70_verification.json)
- [Unity migration export report](https://github.com/KoA-Games/ThisWillFloat/blob/agent/r70-unity-migration/This%20will%20float%20DEMO/Assets/_Project/R70VolcanoMigration/Source/Reports/R70_UnityMigrationExportReport.json)
- [Unity migration builder](https://github.com/KoA-Games/ThisWillFloat/blob/agent/r70-unity-migration/This%20will%20float%20DEMO/Assets/_Project/R70VolcanoMigration/Editor/R70VolcanoMigrationBuilder.cs)

## 2. Modular raft simulation

### Problem

The core gameplay concept needs players to understand why a raft succeeds or fails. A binary health value would hide the relationship between construction choices and physical behavior.

### Approach

The prototype separates the simulation into readable components:

- `WaveSurface` provides a shared analytic water height and estimated normal.
- `BuoyancyBody` samples multiple points and applies lift, damping, drag, upright assist, splash feedback, flooding, and sinking.
- `RaftSection` updates rigidbody mass and center of mass from cargo position.
- `SectionConnector` evaluates relative section motion and applies warning, damage, reinforcement, breakage, separation, and sinking behavior.
- `FailurePrototypeController` provides balanced, overweight, unbalanced, and custom loading scenarios for repeatable demonstration.

### Important behavior

At each physics step, a buoyancy point calculates signed depth against the sampled water surface. A submerged point receives lift along a blend of world-up and the local wave normal. Damping is applied at the same point, allowing forces to create rotational as well as linear response.

Cargo contributes both mass and moment. The section center of mass is recalculated using the structural mass, base center, accumulated cargo mass, and cargo positions. This makes off-center loading visible in the raft's attitude.

Connector stress combines:

- Section mass difference
- Relative up-vector angle
- Relative linear velocity
- Relative angular velocity

Stress must persist through a warning interval before a connector fails. Reinforcement modifies strength and timing rather than simply toggling failure off.

### Evidence

- [`BuoyancyBody.cs`](../Assets/_Project/Scripts/Prototype/BuoyancyBody.cs)
- [`WaveSurface.cs`](../Assets/_Project/Scripts/Prototype/WaveSurface.cs)
- [`RaftSection.cs`](../Assets/_Project/Scripts/Prototype/RaftSection.cs)
- [`SectionConnector.cs`](../Assets/_Project/Scripts/Prototype/SectionConnector.cs)
- [`FailurePrototypeController.cs`](../Assets/_Project/Scripts/Prototype/FailurePrototypeController.cs)

## 3. What this project demonstrates

- Translating visual goals into deterministic algorithms and constraints
- Building tools across Blender Python, Unity C#, asset import, and scene assembly
- Preserving approved source data through hashes and working-copy workflows
- Separating builders from independent verification
- Producing machine-readable evidence instead of relying only on screenshots
- Prototyping physical systems that communicate causality to players
- Working across runtime behavior, editor automation, content pipelines, and technical art

## 4. Current limitations and next experiment

The project does not yet publish a controlled GPU performance comparison. Dense vegetation is therefore described as generated and validated, not optimized.

The next case study should use a fixed scene, camera path, resolution, quality preset, and hardware target to record a baseline and compare:

- GPU frame time
- CPU render-thread time
- Draw calls and state changes
- Visible instance and triangle counts
- Memory use
- GPU instancing
- LOD policy
- Material consolidation
- Distance and occlusion culling

The result should include capture evidence and before/after measurements. No optimization claim should be made until those measurements exist.
